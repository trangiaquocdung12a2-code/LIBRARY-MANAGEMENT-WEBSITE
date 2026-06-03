using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
// Using your exact project namespace
using LIBRARY_MANAGEMENT_WEBSITE.Models;

namespace LIBRARY_MANAGEMENT_WEBSITE.Controllers
{
    public class AdminController : Controller
    {
        // Instantiating your exact LINQ to SQL DataContext from DataClasses1.dbml
        private DataClasses1DataContext db = new DataClasses1DataContext();
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            // 1. Check if the User Session exists at all
            if (Session["UserId"] == null || Session["Role"] == null)
            {
                // Set a warning message to display on the login page
                TempData["Error"] = "Please login to access the Admin area.";

                // Redirect straight to Account/Login
                filterContext.Result = new RedirectToRouteResult(
                    new System.Web.Routing.RouteValueDictionary(new { controller = "Account", action = "Login" })
                );
            }
            // 2. If a user is logged in, but their role is NOT Admin (e.g., a Reader or Librarian)
            else if (Session["Role"].ToString() != "Admin")
            {
                TempData["Error"] = "Access Denied. You do not have administrator permissions.";

                filterContext.Result = new RedirectToRouteResult(
                    new System.Web.Routing.RouteValueDictionary(new { controller = "Account", action = "Login" })
                );
            }

            base.OnActionExecuting(filterContext);
        }
        // ==========================================
        // 1/ DASHBOARD
        // ==========================================
        public ActionResult Dashboard()
        {
            ViewBag.TotalBooks = db.Books.Count();
            ViewBag.TotalReaders = db.Readers.Count();
            ViewBag.TotalBorrowings = db.Borrowings.Count();
            ViewBag.TotalFines = db.Fines.Sum(f => (decimal?)f.Amount) ?? 0;

            return View();
        }

        // ==========================================
        // 2/ MANAGE BOOKS
        // ==========================================
        public ActionResult ManageBooks()
        {
            // Includes corresponding attributes
            var books = db.Books.ToList();
            return View(books);
        }

        public ActionResult AddBook()
        {
            ViewBag.CategoryId = new SelectList(db.Categories, "CategoryId", "CategoryName");
            ViewBag.AuthorId = new SelectList(db.Authors, "AuthorId", "FullName");
            ViewBag.PublisherId = new SelectList(db.Publishers, "PublisherId", "PublisherName");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddBook(Book book, HttpPostedFileBase ImageFile)
        {
            if (ModelState.IsValid)
            {
                book.CreatedAt = DateTime.Now;
                book.UpdatedAt = DateTime.Now;

                db.Books.InsertOnSubmit(book);
                db.SubmitChanges(); // Submit first to generate an identity BookId

                // Upload Book Image (Inserts row into BookImage table)
                if (ImageFile != null && ImageFile.ContentLength > 0)
                {
                    var fileName = Guid.NewGuid().ToString() + Path.GetExtension(ImageFile.FileName);
                    var path = Path.Combine(Server.MapPath("~/Content/images/"), fileName);
                    ImageFile.SaveAs(path);

                    BookImage img = new BookImage
                    {
                        BookId = book.BookId,
                        ImageUrl = "/Content/images/" + fileName,
                        IsPrimary = true,
                        CreatedAt = DateTime.Now,
                    };
                    db.BookImages.InsertOnSubmit(img);
                    db.SubmitChanges();
                }

                return RedirectToAction("ManageBooks");
            }

            ViewBag.CategoryId = new SelectList(db.Categories, "CategoryId", "CategoryName", book.CategoryId);
            ViewBag.AuthorId = new SelectList(db.Authors, "AuthorId", "FullName", book.AuthorId);
            ViewBag.PublisherId = new SelectList(db.Publishers, "PublisherId", "PublisherName", book.PublisherId);
            return View(book);
        }

        public ActionResult EditBook(int id)
        {
            var book = db.Books.SingleOrDefault(b => b.BookId == id);
            if (book == null) return HttpNotFound();

            ViewBag.CategoryId = new SelectList(db.Categories, "CategoryId", "CategoryName", book.CategoryId);
            ViewBag.AuthorId = new SelectList(db.Authors, "AuthorId", "FullName", book.AuthorId);
            ViewBag.PublisherId = new SelectList(db.Publishers, "PublisherId", "PublisherName", book.PublisherId);
            return View(book);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditBook(Book book, HttpPostedFileBase ImageFile)
        {
            if (ModelState.IsValid)
            {
                var existingBook = db.Books.SingleOrDefault(b => b.BookId == book.BookId);
                if (existingBook != null)
                {
                    existingBook.ISBN = book.ISBN;
                    existingBook.Title = book.Title;
                    existingBook.AuthorId = book.AuthorId;
                    existingBook.CategoryId = book.CategoryId;
                    existingBook.PublisherId = book.PublisherId;
                    existingBook.PublishYear = book.PublishYear;
                    existingBook.TotalCopies = book.TotalCopies;
                    existingBook.AvailableCopies = book.AvailableCopies;
                    existingBook.ShelfLocation = book.ShelfLocation;
                    existingBook.IsActive = book.IsActive;
                    existingBook.UpdatedAt = DateTime.Now;

                    // Update Image if a new one is selected
                    if (ImageFile != null && ImageFile.ContentLength > 0)
                    {
                        var fileName = Guid.NewGuid().ToString() + Path.GetExtension(ImageFile.FileName);
                        var path = Path.Combine(Server.MapPath("~/Content/images/"), fileName);
                        ImageFile.SaveAs(path);

                        // Clear prior primary flag configuration if existing
                        var oldImages = db.BookImages.Where(i => i.BookId == existingBook.BookId);
                        foreach (var img in oldImages) { img.IsPrimary = false; }

                        BookImage newImg = new BookImage
                        {
                            BookId = existingBook.BookId,
                            ImageUrl = "/Content/images/" + fileName,
                            IsPrimary = true,
                            CreatedAt = DateTime.Now
                        };
                        db.BookImages.InsertOnSubmit(newImg);
                    }

                    db.SubmitChanges();
                }
                return RedirectToAction("ManageBooks");
            }
            return View(book);
        }

        public ActionResult DeleteBook(int id)
        {
            var book = db.Books.SingleOrDefault(b => b.BookId == id);
            if (book != null)
            {
                // Fix: Query via BorrowingDetail and cross-reference the Borrowing status
                bool isCurrentlyBorrowed = db.BorrowingDetails.Any(bd => bd.BookId == id &&
                    (bd.Borrowing.Status == "Approved" || bd.Borrowing.Status == "Pending" || bd.Borrowing.Status == "Overdue"));

                if (isCurrentlyBorrowed)
                {
                    TempData["ErrorMessage"] = "Cannot delete book! A reader is currently reading or requesting that book. Make sure all copies are returned first.";
                    return RedirectToAction("ManageBooks");
                }

                // --- SAFE TO DELETE ---
                // 1. Cascade delete images
                var images = db.BookImages.Where(i => i.BookId == id);
                db.BookImages.DeleteAllOnSubmit(images);

                // 2. Cascade delete status configurations
                var statuses = db.BookStatus.Where(s => s.BookId == id);
                db.BookStatus.DeleteAllOnSubmit(statuses);

                // 3. Cascade delete past transaction details referencing this book
                var details = db.BorrowingDetails.Where(bd => bd.BookId == id);
                db.BorrowingDetails.DeleteAllOnSubmit(details);

                // 4. Delete the root book record
                db.Books.DeleteOnSubmit(book);
                db.SubmitChanges();

                TempData["SuccessMessage"] = "Book entry and all its historical logs successfully removed.";
            }
            return RedirectToAction("ManageBooks");
        }
        // ==========================================
        // 3/ MANAGE CATEGORIES
        // ==========================================
        public ActionResult ManageCategories()
        {
            return View(db.Categories.ToList());
        }

        [HttpPost]
        public ActionResult AddCategory(Category cat)
        {
            if (ModelState.IsValid)
            {
                cat.CreatedAt = DateTime.Now;
                cat.IsActive = true;
                db.Categories.InsertOnSubmit(cat);
                db.SubmitChanges();
            }
            return RedirectToAction("ManageCategories");
        }

        public ActionResult DeleteCategory(int id)
        {
            var cat = db.Categories.SingleOrDefault(c => c.CategoryId == id);
            if (cat != null)
            {
                db.Categories.DeleteOnSubmit(cat);
                db.SubmitChanges();
            }
            return RedirectToAction("ManageCategories");
        }

        // ==========================================
        // 4/ MANAGE AUTHORS / PUBLISHERS
        // ==========================================
        public ActionResult ManageAuthorsPublishers()
        {
            ViewBag.Authors = db.Authors.ToList();
            ViewBag.Publishers = db.Publishers.ToList();
            return View();
        }

        [HttpPost]
        public ActionResult AddAuthor(Author author)
        {
            if (ModelState.IsValid)
            {
                author.CreatedAt = DateTime.Now;
                author.IsActive = true;
                db.Authors.InsertOnSubmit(author);
                db.SubmitChanges();
            }
            return RedirectToAction("ManageAuthorsPublishers");
        }

        [HttpPost]
        public ActionResult AddPublisher(Publisher pub)
        {
            if (ModelState.IsValid)
            {
                pub.CreatedAt = DateTime.Now;
                pub.IsActive = true;
                db.Publishers.InsertOnSubmit(pub);
                db.SubmitChanges();
            }
            return RedirectToAction("ManageAuthorsPublishers");
        }

        // ==========================================
        // 5/ MANAGE READERS
        // ==========================================
        public ActionResult ManageReaders()
        {
            // Reader details use an outer join/relation to UserAccount
            var readers = db.Readers.ToList();
            return View(readers);
        }

        public ActionResult ToggleReaderStatus(int id)
        {
            var reader = db.Readers.SingleOrDefault(r => r.ReaderId == id);
            if (reader != null)
            {
                // IsActive configuration sits directly on the linked UserAccount entity
                var account = db.UserAccounts.SingleOrDefault(u => u.UserId == reader.UserId);
                if (account != null)
                {
                    account.IsActive = !account.IsActive;
                    account.UpdatedAt = DateTime.Now;
                    db.SubmitChanges();
                }
            }
            return RedirectToAction("ManageReaders");
        }

        // ==========================================
        // 6/ MANAGE BORROWINGS
        // ==========================================
        public ActionResult ManageBorrowings()
        {
            var borrowings = db.Borrowings.ToList();
            return View(borrowings);
        }

        [HttpPost]
        public ActionResult UpdateBorrowStatus(int id, string status)
        {
            var borrowing = db.Borrowings.SingleOrDefault(b => b.BorrowingId == id);
            if (borrowing != null)
            {
                borrowing.Status = status; // 'Pending','Approved','Rejected','Returned','Overdue','Cancelled'
                borrowing.UpdatedAt = DateTime.Now;

                if (status == "Returned")
                {
                    borrowing.ReturnDate = DateTime.Now;
                }

                db.SubmitChanges();
            }
            return RedirectToAction("ManageBorrowings");
        }

        // ==========================================
        // 7/ MANAGE FINE PAYMENTS
        // ==========================================
        public ActionResult ManageFines()
        {
            return View(db.Fines.ToList());
        }

        [HttpPost]
        public ActionResult ConfirmPayment(int id, string status, string method)
        {
            var fine = db.Fines.SingleOrDefault(f => f.FineId == id);
            if (fine != null)
            {
                fine.PaymentStatus = status; // 'Unpaid','Partial','Paid'
                fine.PaymentMethod = method; // 'Cash','VNPay','MoMo','PayPal'

                if (status == "Paid")
                {
                    fine.PaidAmount = fine.Amount;
                    fine.PaymentDate = DateTime.Now;
                }

                db.SubmitChanges();
            }
            return RedirectToAction("ManageFines");
        }

        // ==========================================
        // 8/ MANAGE REVIEWS
        // ==========================================
        public ActionResult ManageReviews()
        {
            var reviews = db.Reviews.ToList();
            ViewBag.TotalReviews = reviews.Count;
            ViewBag.AvgRating = reviews.Any() ? reviews.Average(r => r.Rating) : 0;
            return View(reviews);
        }

        public ActionResult DeleteReview(int id)
        {
            var review = db.Reviews.SingleOrDefault(r => r.ReviewId == id);
            if (review != null)
            {
                db.Reviews.DeleteOnSubmit(review);
                db.SubmitChanges();
            }
            return RedirectToAction("ManageReviews");
        }
    }
}