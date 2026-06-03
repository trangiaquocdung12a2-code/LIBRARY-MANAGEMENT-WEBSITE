using System;
using System.Configuration;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Mvc;
using System.Web.Security;
using LIBRARY_MANAGEMENT_WEBSITE.Models;

namespace LIBRARY_MANAGEMENT_WEBSITE.Controllers
{
    public class AccountController : Controller
    {
        private DataClasses1DataContext db = new DataClasses1DataContext(
            ConfigurationManager.ConnectionStrings["LibraryDBConnectionString"].ConnectionString
        );

        public ActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Register(string fullName, string email, string password, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                ViewBag.Error = "Full name is required.";
                return View();
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                ViewBag.Error = "Email is required.";
                return View();
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Password is required.";
                return View();
            }

            if (password.Length < 6)
            {
                ViewBag.Error = "Password must be at least 6 characters.";
                return View();
            }

            if (password != confirmPassword)
            {
                ViewBag.Error = "Confirm password does not match.";
                return View();
            }

            var existedUser = db.UserAccounts.FirstOrDefault(u => u.Email == email);

            if (existedUser != null)
            {
                ViewBag.Error = "Email already exists.";
                return View();
            }

            UserAccount user = new UserAccount();
            user.Email = email;
            user.PasswordHash = HashPassword(password);
            user.FullName = fullName;
            user.Role = "Reader";
            user.IsActive = true;
            user.IsEmailVerified = true;
            user.FailedLoginAttempts = 0;
            user.CreatedAt = DateTime.Now;
            user.UpdatedAt = DateTime.Now;

            db.UserAccounts.InsertOnSubmit(user);
            db.SubmitChanges();

            Reader reader = new Reader();
            reader.UserId = user.UserId;
            reader.ReaderCode = "RDR-" + user.UserId.ToString("000");
            reader.MembershipDate = DateTime.Now;
            reader.MembershipExpiry = DateTime.Now.AddYears(1);
            reader.TotalBorrowed = 0;
            reader.TotalFines = 0;

            db.Readers.InsertOnSubmit(reader);
            db.SubmitChanges();

            TempData["Success"] = "Register successfully. Please login.";
            return RedirectToAction("Login");
        }

        public ActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Login(string email, string password, bool rememberMe = false)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                ViewBag.Error = "Email is required.";
                return View();
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Password is required.";
                return View();
            }

            string hashedPassword = HashPassword(password);

             var user = db.UserAccounts.AsEnumerable().FirstOrDefault(u =>
             u.Email.Equals(email, StringComparison.OrdinalIgnoreCase) &&
             u.PasswordHash.Equals(hashedPassword, StringComparison.OrdinalIgnoreCase) &&
             u.IsActive == true
     );

            if (user == null)
            {
                ViewBag.Error = "Invalid email or password.";
                return View();
            }

            FormsAuthentication.SetAuthCookie(user.Email, rememberMe);

            Session["UserId"] = user.UserId;
            Session["FullName"] = user.FullName;
            Session["Role"] = user.Role;

            if (user.Role == "Admin")
            {
                return RedirectToAction("Dashboard", "Admin");
            }

            if (user.Role == "Librarian")
            {
                return RedirectToAction("Index", "Librarian");
            }

            return RedirectToAction("Index", "Home");
        }

        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            Session.Clear();

            return RedirectToAction("Index", "Home");
        }

        public ActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public ActionResult ForgotPassword(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                ViewBag.Error = "Please enter your email.";
                return View();
            }

            var user = db.UserAccounts.FirstOrDefault(u =>
                u.Email == email &&
                u.IsActive == true
            );

            if (user == null)
            {
                ViewBag.Error = "Email does not exist.";
                return View();
            }

            string token = Guid.NewGuid().ToString();

            user.PasswordResetToken = token;
            user.PasswordResetExpiry = DateTime.Now.AddMinutes(30);
            user.UpdatedAt = DateTime.Now;

            db.SubmitChanges();

            ViewBag.Success = "Reset password link has been created.";
            ViewBag.ResetLink = Url.Action("ResetPassword", "Account", new { token = token }, Request.Url.Scheme);

            return View();
        }

        public ActionResult ResetPassword(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return RedirectToAction("Login");
            }

            var user = db.UserAccounts.FirstOrDefault(u =>
                u.PasswordResetToken == token &&
                u.PasswordResetExpiry != null &&
                u.PasswordResetExpiry >= DateTime.Now
            );

            if (user == null)
            {
                ViewBag.Error = "Reset password link is invalid or expired.";
                return View();
            }

            ViewBag.Token = token;
            return View();
        }

        [HttpPost]
        public ActionResult ResetPassword(string token, string password, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                ViewBag.Error = "Invalid reset token.";
                return View();
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Password is required.";
                ViewBag.Token = token;
                return View();
            }

            if (password.Length < 6)
            {
                ViewBag.Error = "Password must be at least 6 characters.";
                ViewBag.Token = token;
                return View();
            }

            if (password != confirmPassword)
            {
                ViewBag.Error = "Confirm password does not match.";
                ViewBag.Token = token;
                return View();
            }

            var user = db.UserAccounts.FirstOrDefault(u =>
                u.PasswordResetToken == token &&
                u.PasswordResetExpiry != null &&
                u.PasswordResetExpiry >= DateTime.Now
            );

            if (user == null)
            {
                ViewBag.Error = "Reset password link is invalid or expired.";
                return View();
            }

            user.PasswordHash = HashPassword(password);
            user.PasswordResetToken = null;
            user.PasswordResetExpiry = null;
            user.UpdatedAt = DateTime.Now;

            db.SubmitChanges();

            TempData["Success"] = "Password reset successfully. Please login.";
            return RedirectToAction("Login");
        }

        private string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();

                foreach (byte b in bytes)
                {
                    builder.Append(b.ToString("X2"));
                }

                return builder.ToString();
            }
        }
    }
}