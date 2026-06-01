using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Mvc;
using LIBRARY_MANAGEMENT_WEBSITE.Models;

namespace LIBRARY_MANAGEMENT_WEBSITE.Controllers
{
    public class ReaderController : Controller
    {
        private DataClasses1DataContext db = new DataClasses1DataContext(
            ConfigurationManager.ConnectionStrings["LibraryDBConnectionString"].ConnectionString
        );

        public ActionResult Profile()
        {
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            int userId = Convert.ToInt32(Session["UserId"]);

            var reader = db.Readers.FirstOrDefault(r => r.UserId == userId);

            if (reader == null)
            {
                return HttpNotFound();
            }

            return View(reader);
        }

        public ActionResult EditProfile()
        {
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            int userId = Convert.ToInt32(Session["UserId"]);

            var reader = db.Readers.FirstOrDefault(r => r.UserId == userId);

            if (reader == null)
            {
                return HttpNotFound();
            }

            return View(reader);
        }

        [HttpPost]
        public ActionResult EditProfile(
            string fullName,
            string phone,
            string address,
            DateTime? dateOfBirth,
            string gender,
            HttpPostedFileBase avatar)
        {
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            int userId = Convert.ToInt32(Session["UserId"]);

            var reader = db.Readers.FirstOrDefault(r => r.UserId == userId);

            if (reader == null)
            {
                return HttpNotFound();
            }

            if (string.IsNullOrWhiteSpace(fullName))
            {
                ViewBag.Error = "Full name is required.";
                return View(reader);
            }

            reader.UserAccount.FullName = fullName;
            reader.UserAccount.Phone = phone;
            reader.UserAccount.Address = address;
            reader.UserAccount.UpdatedAt = DateTime.Now;

            reader.DateOfBirth = dateOfBirth;
            reader.Gender = gender;

            if (avatar != null && avatar.ContentLength > 0)
            {
                string folderPath = Server.MapPath("~/Content/avatars");

                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                string extension = Path.GetExtension(avatar.FileName);
                string fileName = Guid.NewGuid().ToString() + extension;
                string savePath = Path.Combine(folderPath, fileName);

                avatar.SaveAs(savePath);

                reader.UserAccount.AvatarUrl = "/Content/avatars/" + fileName;
            }

            db.SubmitChanges();

            Session["FullName"] = fullName;

            TempData["Success"] = "Profile updated successfully.";
            return RedirectToAction("Profile");
        }

        public ActionResult ChangePassword()
        {
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            return View();
        }

        [HttpPost]
        public ActionResult ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (string.IsNullOrWhiteSpace(currentPassword))
            {
                ViewBag.Error = "Current password is required.";
                return View();
            }

            if (string.IsNullOrWhiteSpace(newPassword))
            {
                ViewBag.Error = "New password is required.";
                return View();
            }

            if (newPassword.Length < 6)
            {
                ViewBag.Error = "New password must be at least 6 characters.";
                return View();
            }

            if (newPassword != confirmPassword)
            {
                ViewBag.Error = "Confirm password does not match.";
                return View();
            }

            int userId = Convert.ToInt32(Session["UserId"]);

            var user = db.UserAccounts.FirstOrDefault(u => u.UserId == userId);

            if (user == null)
            {
                return HttpNotFound();
            }

            string currentHash = HashPassword(currentPassword);

            if (user.PasswordHash != currentHash)
            {
                ViewBag.Error = "Current password is incorrect.";
                return View();
            }

            user.PasswordHash = HashPassword(newPassword);
            user.UpdatedAt = DateTime.Now;

            db.SubmitChanges();

            TempData["Success"] = "Password changed successfully.";
            return RedirectToAction("Profile");
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