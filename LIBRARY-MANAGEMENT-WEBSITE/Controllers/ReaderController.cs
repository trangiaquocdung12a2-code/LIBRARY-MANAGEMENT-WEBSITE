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
        [ValidateAntiForgeryToken]
        public ActionResult EditProfile(DateTime? dateOfBirth, string fullName, string phone, string address, string gender, HttpPostedFileBase avatar)
        {
            // 1. Session Authorization Sanity Check
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            int userId = Convert.ToInt32(Session["UserId"]);

            // 2. Fetch the existing Reader profile tracking entry along with its UserAccount link entity
            var reader = db.Readers.FirstOrDefault(r => r.UserId == userId);
            if (reader == null)
            {
                return HttpNotFound();
            }

            // 3. Strict Server-Side Business Rule Validations
            // Check to completely prevent back-door data manipulation of future birthday dates
            if (dateOfBirth.HasValue && dateOfBirth.Value > DateTime.Now)
            {
                ViewBag.Error = "Date of birth cannot be set to a future date calendar window.";
                return View(reader); // Return the view with the model record state to show the error message
            }

            try
            {
                // 4. File Upload Avatar Handling Logic (if an image file was supplied)
                if (avatar != null && avatar.ContentLength > 0)
                {
                    string fileName = System.IO.Path.GetFileName(avatar.FileName);
                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + fileName;
                    string folderPath = Server.MapPath("~/Uploads/Avatars/");

                    // Check if directory folder exists physically, create it if missing
                    if (!System.IO.Directory.Exists(folderPath))
                    {
                        System.IO.Directory.CreateDirectory(folderPath);
                    }

                    string physicalPath = System.IO.Path.Combine(folderPath, uniqueFileName);
                    avatar.SaveAs(physicalPath);

                    // Update database pointer path mapping asset
                    if (reader.UserAccount != null)
                    {
                        reader.UserAccount.AvatarUrl = "/Uploads/Avatars/" + uniqueFileName;
                    }
                }

                // 5. Update Editable Text Property Parameters safely
                if (reader.UserAccount != null)
                {
                    reader.UserAccount.FullName = fullName;
                    reader.UserAccount.Phone = phone;
                    reader.UserAccount.Address = address;

                    // NOTE: We intentionally do NOT modify reader.UserAccount.Email here.
                    // It preserves the old database value, completely satisfying the security rule.
                }

                reader.Gender = gender;
                reader.DateOfBirth = dateOfBirth;

                // 6. Save modifications directly to the SQL database tables
                db.SubmitChanges();

                TempData["Success"] = "Profile records updated successfully.";
                return RedirectToAction("Profile", "Reader");
            }
            catch (Exception ex)
            {
                ViewBag.Error = "An unexpected error occurred while saving your profile data changes: " + ex.Message;
                return View(reader);
            }
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