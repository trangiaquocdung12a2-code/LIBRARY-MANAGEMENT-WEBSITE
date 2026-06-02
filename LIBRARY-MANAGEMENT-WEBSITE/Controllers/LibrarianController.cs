using LIBRARY_MANAGEMENT_WEBSITE.Models;
using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Mvc;

namespace LIBRARY_MANAGEMENT_WEBSITE.Controllers
{
    public class LibrarianAuthorizeAttribute : AuthorizeAttribute
    {
        protected override bool AuthorizeCore(System.Web.HttpContextBase httpContext)
        {
            return httpContext.Session["Role"] != null && httpContext.Session["Role"].ToString() == "Librarian";
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            filterContext.Result = new RedirectToRouteResult(
                new System.Web.Routing.RouteValueDictionary(new { controller = "Account", action = "Login" })
            );
        }
    }

    [LibrarianAuthorize]
    public class LibrarianController : Controller
    {
        private DataClasses1DataContext db = new DataClasses1DataContext(
            ConfigurationManager.ConnectionStrings["LibraryDBConnectionString"].ConnectionString
        );

        // ==========================================
        // 1/ LIBRARIAN DASHBOARD
        // ==========================================
        public ActionResult Index()
        {
            ViewBag.TotalBooks = db.Books.Count();
            ViewBag.TotalReaders = db.Readers.Count();
            ViewBag.ActiveBorrowings = db.Borrowings.Count(b => b.Status == "Approved");
            ViewBag.PendingRequests = db.Borrowings.Count(b => b.Status == "Pending");

            var recentActivities = db.Borrowings
                .OrderByDescending(b => b.BorrowDate)
                .Take(5)
                .AsEnumerable()
                .Select(b => {
                    var detail = db.BorrowingDetails.FirstOrDefault(bd => bd.BorrowingId == b.BorrowingId);
                    return new DashboardActivityViewModel
                    {
                        ReaderName = b.Reader?.UserAccount?.FullName ?? "Unknown",
                        BookTitle = detail?.Book?.Title ?? "Unknown Book",
                        Status = b.Status,
                        // FIXED: Removed the invalid ?? fallback because b.BorrowDate is already a clean DateTime object
                        ActivityDate = b.BorrowDate
                    };
                }).ToList();

            return View(recentActivities);
        }

        // ==========================================
        // 2/ & 3/ MANAGE & APPROVE/REJECT REQUESTS
        // ==========================================
        public ActionResult BorrowRequests()
        {
            var requests = db.Borrowings
                .OrderByDescending(b => b.BorrowDate)
                .AsEnumerable()
                .Select(b => {
                    var detail = db.BorrowingDetails.FirstOrDefault(bd => bd.BorrowingId == b.BorrowingId);
                    return new BorrowRequestViewModel
                    {
                        BorrowingId = b.BorrowingId,
                        ReaderName = b.Reader?.UserAccount?.FullName ?? "Unknown",
                        ReaderCode = b.Reader?.ReaderCode ?? "",
                        ReaderEmail = b.Reader?.UserAccount?.Email ?? "",
                        BookTitle = detail?.Book?.Title ?? "Missing Book",
                        BookISBN = detail?.Book?.ISBN ?? "",
                        AvailableCopies = detail?.Book?.AvailableCopies ?? 0,
                        BorrowDateText = ((DateTime)b.BorrowDate).ToString("yyyy-MM-dd"),
                        DueDateText = ((DateTime)b.DueDate).ToString("yyyy-MM-dd"),
                        Status = b.Status
                    };
                }).ToList();

            return View(requests);
        }

        [HttpPost]
        public ActionResult ProcessRequest(int borrowingId, string decision)
        {
            var record = db.Borrowings.FirstOrDefault(r => r.BorrowingId == borrowingId);
            if (record == null) return HttpNotFound();

            var detail = db.BorrowingDetails.FirstOrDefault(d => d.BorrowingId == borrowingId);
            var book = detail != null ? db.Books.FirstOrDefault(b => b.BookId == detail.BookId) : null;

            if (decision == "Approve")
            {
                if (book != null && book.AvailableCopies > 0)
                {
                    record.Status = "Approved";
                    record.BorrowDate = DateTime.Now;
                    book.AvailableCopies--;
                }
                else
                {
                    TempData["Error"] = "Cannot approve. No physical copies available.";
                    return RedirectToAction("BorrowRequests");
                }
            }
            else if (decision == "Reject")
            {
                record.Status = "Rejected";
            }
            else if (decision == "Return")
            {
                record.Status = "Returned";
                record.ReturnDate = DateTime.Now;
                if (detail != null) detail.ReturnedAt = DateTime.Now;
                if (book != null) book.AvailableCopies++;
            }

            db.SubmitChanges();
            TempData["Success"] = $"Record updated to {record.Status} successfully.";
            return RedirectToAction("BorrowRequests");
        }

        // ==========================================
        // 4/ MANAGE BOOK AVAILABILITY
        // ==========================================
        public ActionResult BookAvailability()
        {
            var books = db.Books.OrderBy(b => b.Title).ToList();
            return View(books);
        }

        // ==========================================
        // POST: DECLARE BOOK STATUS DROP INDIDENT
        // ==========================================
        [HttpPost]
        public ActionResult ReportBookIncident(int bookId, string incidentType, string internalNotes)
        {
            try
            {
                // 1. Locate the book profile record within the database context
                var bookRecord = db.Books.FirstOrDefault(b => b.BookId == bookId);

                if (bookRecord != null)
                {
                    // Verify availability counts before deducting stock items
                    if (bookRecord.TotalCopies > 0)
                    {
                        // 2. Reduce the current available copy count ledger
                        bookRecord.TotalCopies -= 1;

                        // 3. Optional: If the incident type means the item is completely gone (e.g., Missing/Lost),
                        // you can also choose to decrement TotalStock here if desired:
                        // if (incidentType == "Missing") { bookRecord.TotalStock -= 1; }

                        db.SubmitChanges();
                        TempData["Success"] = $"Successfully logged incident ({incidentType}). Available inventory stock updated smoothly.";
                    }
                    else
                    {
                        TempData["Error"] = "Action aborted: There are zero copies currently logged as available on shelves.";
                    }
                }
                else
                {
                    TempData["Error"] = "System Error: Book tracking key identifier not found.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Database execution anomaly: " + ex.Message;
            }

            return RedirectToAction("BookAvailability");
        }

        // ==========================================
        // 5/ VIEW READER INFORMATION
        // ==========================================
        public ActionResult ReaderProfiles()
        {
            var readers = db.Readers.ToList();
            return View(readers);
        }

        public ActionResult ReaderDetails(int id)
        {
            var reader = db.Readers.FirstOrDefault(r => r.ReaderId == id);
            if (reader == null) return HttpNotFound();

            ViewBag.History = db.Borrowings
                .Where(b => b.ReaderId == id)
                .OrderByDescending(b => b.BorrowDate)
                .AsEnumerable()
                .Select(b => {
                    var detail = db.BorrowingDetails.FirstOrDefault(bd => bd.BorrowingId == b.BorrowingId);
                    return new BorrowRequestViewModel
                    {
                        BorrowingId = b.BorrowingId,
                        BookTitle = detail?.Book?.Title ?? "Unknown Title",
                        // FIX: Force casting to standard DateTime objects before formatting
                        BorrowDateText = ((DateTime)b.BorrowDate).ToString("yyyy-MM-dd"),
                        DueDateText = ((DateTime)b.DueDate).ToString("yyyy-MM-dd"),
                        Status = b.Status
                    };
                }).ToList();

            return View(reader);
        }

        // ==========================================
        // 6/ MANAGE FINE RECORDS
        // ==========================================
        public ActionResult Fines()
        {
            // 1. Get all historical fines
            var fines = db.Fines.OrderByDescending(f => f.IssuedDate).ToList();

            // 2. Query active items that are currently overdue but haven't been resolved yet
            // This feeds the new pending overdue actions table
            ViewBag.OverdueBorrowings = db.Borrowings
                .Where(b => b.Status == "Approved" && b.DueDate < DateTime.Today)
                .OrderBy(b => b.DueDate)
                .ToList();

            return View(fines);
        }

        // ==========================================
        // POST: CREATE AND SYNC FINE (LINQ TO SQL)
        // ==========================================
        [HttpPost]
        public ActionResult AddFine(int borrowingId, int readerId, string fineType, decimal amount, string notes)
        {
            try
            {
                // 1. Create the new Fine entity object
                Fine newFine = new Fine
                {
                    BorrowingId = borrowingId,
                    ReaderId = readerId,
                    FineType = fineType,
                    Amount = amount,
                    Notes = notes,
                    PaymentStatus = "Unpaid",
                    IssuedDate = DateTime.Now
                };

                // 2. LINQ to SQL: Insert record into the table map context
                db.Fines.InsertOnSubmit(newFine);

                // 3. DATABASE SYNC LAYER: Locate the associated borrowing transaction record
                var borrowingRecord = db.Borrowings.FirstOrDefault(b => b.BorrowingId == borrowingId);
                if (borrowingRecord != null)
                {
                    // Update its core table status field to Overdue
                    borrowingRecord.Status = "Overdue";
                }

                // 4. LINQ to SQL: Save everything to the SQL Server database
                db.SubmitChanges();

                TempData["Success"] = "Fine assessment posted successfully and borrowing record status synced to Overdue!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to update record systems: " + ex.Message;
            }

            return RedirectToAction("Fines");
        }
        [HttpPost]
        public ActionResult ProcessPayment(FinePaymentSubmissionViewModel model)
        {
            if (Session["UserId"] == null) return RedirectToAction("Login", "Account");

            var fine = db.Fines.FirstOrDefault(f => f.FineId == model.FineId);
            if (fine == null) return HttpNotFound();

            // Enforce file upload validation check
            if (model.TransactionReceiptFile == null || model.TransactionReceiptFile.ContentLength == 0)
            {
                TempData["Error"] = "Payment verification failed: A valid digital transaction receipt image must be uploaded.";
                return RedirectToAction("Fines");
            }

            try
            {
                // 1. Process and save file attachment stream secure path
                string fileName = "Receipt_" + fine.FineId + "_" + DateTime.Now.Ticks + System.IO.Path.GetExtension(model.TransactionReceiptFile.FileName);
                string directoryPath = Server.MapPath("~/Uploads/Receipts/");

                // Ensure folder directory exists on the hosting server partition filesystem
                if (!System.IO.Directory.Exists(directoryPath))
                {
                    System.IO.Directory.CreateDirectory(directoryPath);
                }

                string fullSavePath = System.IO.Path.Combine(directoryPath, fileName);
                model.TransactionReceiptFile.SaveAs(fullSavePath);

                // 2. Commit transaction metadata and update database records
                fine.PaidAmount = fine.Amount;
                fine.PaymentStatus = "Paid";
                fine.PaymentMethod = model.PaymentMethod;
                fine.PaymentDate = DateTime.Now;

                // Save the receipt location reference path string inside your database 
                fine.Notes = (fine.Notes ?? "") + $" [Receipt Attachment Saved: /Uploads/Receipts/{fileName}]";

                // 3. Deduct from Reader's cumulative outstanding balance fine account
                var reader = db.Readers.FirstOrDefault(r => r.ReaderId == fine.ReaderId);
                if (reader != null)
                {
                    reader.TotalFines -= fine.Amount;
                    if (reader.TotalFines < 0) reader.TotalFines = 0; // Guard against negative numbers
                }

                db.SubmitChanges();
                TempData["Success"] = "Payment receipt confirmed. Ledger balance updated successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "System failed processing file streaming execution: " + ex.Message;
            }

            return RedirectToAction("Fines");
        }

        // ==========================================
        // 7/ LIBRARIAN STAFF PROFILE MANAGEMENT
        // ==========================================
        // ==========================================
        // 7/ LIBRARIAN STAFF PROFILE MANAGEMENT
        // ==========================================
        public ActionResult Profile()
        {
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            int currentUserId = Convert.ToInt32(Session["UserId"]);

            // Query your Linq-to-SQL Data Context for the Librarian record matching the User
            var librarian = db.Librarians.FirstOrDefault(l => l.UserId == currentUserId);

            if (librarian == null)
            {
                return HttpNotFound("Librarian record not found matching the active session.");
            }

            return View(librarian);
        }
        // ==========================================
        // 8/ EDIT LIBRARIAN PROFILE (GET)
        // ==========================================
        public ActionResult EditProfile()
        {
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            int userId = Convert.ToInt32(Session["UserId"]);
            var librarian = db.Librarians.FirstOrDefault(l => l.UserId == userId);

            if (librarian == null)
            {
                return HttpNotFound();
            }

            return View(librarian);
        }

        // ==========================================
        // 9/ SAVE LIBRARIAN PROFILE CHANGES (POST)
        // ==========================================
        [HttpPost]
        public ActionResult EditProfile(
            string fullName,
            string phone,
            string address,
            string department,
            HttpPostedFileBase avatar)
        {
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            int userId = Convert.ToInt32(Session["UserId"]);
            var librarian = db.Librarians.FirstOrDefault(l => l.UserId == userId);

            if (librarian == null)
            {
                return HttpNotFound();
            }

            if (string.IsNullOrWhiteSpace(fullName))
            {
                ViewBag.Error = "Full name is required.";
                return View(librarian);
            }

            // Save common fields down into UserAccount reference entity
            librarian.UserAccount.FullName = fullName;
            librarian.UserAccount.Phone = phone;
            librarian.UserAccount.Address = address;
            librarian.UserAccount.UpdatedAt = DateTime.Now;

            // Save specific domain field down to Librarian table
            librarian.Department = department;

            // Handle profile image file stream uploads
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
                librarian.UserAccount.AvatarUrl = "/Content/avatars/" + fileName;
            }

            db.SubmitChanges();

            // Dynamically patch layout title greeting variable
            Session["FullName"] = fullName;

            TempData["Success"] = "Profile updated successfully.";
            return RedirectToAction("Profile");
        }

        // ==========================================
        // 10/ LIBRARIAN PASSWORD UPDATE (GET)
        // ==========================================
        public ActionResult ChangePassword()
        {
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            return View();
        }

        // ==========================================
        // 11/ SAVE NEW PASSWORD HASH (POST)
        // ==========================================
        [HttpPost]
        public ActionResult ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
            {
                ViewBag.Error = "All fields are required.";
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

            // Reflecting the exact hashing mechanism your Reader controller uses
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

    public class DashboardActivityViewModel
    {
        public string ReaderName { get; set; }
        public string BookTitle { get; set; }
        public string Status { get; set; }
        public DateTime ActivityDate { get; set; }
    }

    public class BorrowRequestViewModel
    {
        public int BorrowingId { get; set; }
        public string ReaderName { get; set; }
        public string ReaderCode { get; set; }
        public string ReaderEmail { get; set; }
        public string BookTitle { get; set; }
        public string BookISBN { get; set; }
        public int AvailableCopies { get; set; }
        public string BorrowDateText { get; set; }
        public string DueDateText { get; set; }
        public string Status { get; set; }
    }
    public class FinePaymentSubmissionViewModel
    {
        public int FineId { get; set; }
        public string PaymentMethod { get; set; }
        public System.Web.HttpPostedFileBase TransactionReceiptFile { get; set; }
    }
}