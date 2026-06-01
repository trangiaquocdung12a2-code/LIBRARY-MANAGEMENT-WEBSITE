using System;
using System.Configuration;
using System.Linq;
using System.Web.Mvc;
using LIBRARY_MANAGEMENT_WEBSITE.Models;

namespace LIBRARY_MANAGEMENT_WEBSITE.Controllers
{
    public class BorrowingController : Controller
    {
        private DataClasses1DataContext db = new DataClasses1DataContext(
            ConfigurationManager.ConnectionStrings["LibraryDBConnectionString"].ConnectionString
        );

        public ActionResult Create(int bookId)
        {
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var book = db.Books.FirstOrDefault(b => b.BookId == bookId && b.IsActive == true);

            if (book == null)
            {
                return HttpNotFound();
            }

            if (book.AvailableCopies <= 0)
            {
                TempData["Error"] = "This book is currently unavailable.";
                return RedirectToAction("Details", "Book", new { id = bookId });
            }

            int userId = Convert.ToInt32(Session["UserId"]);

            var reader = db.Readers.FirstOrDefault(r => r.UserId == userId);

            if (reader == null)
            {
                return HttpNotFound();
            }

            ViewBag.Reader = reader;
            ViewBag.BorrowDate = DateTime.Now;
            ViewBag.DueDate = DateTime.Now.AddDays(14);

            return View(book);
        }

        [HttpPost]
        public ActionResult ConfirmBorrow(int bookId)
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

            var book = db.Books.FirstOrDefault(b => b.BookId == bookId && b.IsActive == true);

            if (book == null)
            {
                return HttpNotFound();
            }

            if (book.AvailableCopies <= 0)
            {
                TempData["Error"] = "This book is currently unavailable.";
                return RedirectToAction("Details", "Book", new { id = bookId });
            }

            var existingBorrow = db.Borrowings.FirstOrDefault(b =>
                b.ReaderId == reader.ReaderId &&
                b.Status != "Returned" &&
                b.Status != "Cancelled" &&
                b.Status != "Rejected" &&
                b.BorrowingDetails.Any(d => d.BookId == bookId)
            );

            if (existingBorrow != null)
            {
                TempData["Error"] = "You already have a borrowing request for this book.";
                return RedirectToAction("Details", "Book", new { id = bookId });
            }

            Borrowing borrowing = new Borrowing();
            borrowing.ReaderId = reader.ReaderId;
            borrowing.LibrarianId = null;
            borrowing.BorrowDate = DateTime.Now;
            borrowing.DueDate = DateTime.Now.AddDays(14);
            borrowing.ReturnDate = null;
            borrowing.Status = "Pending";
            borrowing.Notes = "Borrow request created by reader.";
            borrowing.CreatedAt = DateTime.Now;
            borrowing.UpdatedAt = DateTime.Now;

            db.Borrowings.InsertOnSubmit(borrowing);
            db.SubmitChanges();

            BorrowingDetail detail = new BorrowingDetail();
            detail.BorrowingId = borrowing.BorrowingId;
            detail.BookId = book.BookId;
            detail.ReturnedAt = null;
            detail.Condition = null;
            detail.Notes = "Waiting for librarian approval.";

            db.BorrowingDetails.InsertOnSubmit(detail);
            db.SubmitChanges();

            TempData["Success"] = "Borrow request created successfully. Please wait for librarian approval.";

            return RedirectToAction("MyBorrowings");
        }

        public ActionResult MyBorrowings()
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

            var borrowings = db.Borrowings
                .Where(b => b.ReaderId == reader.ReaderId)
                .OrderByDescending(b => b.BorrowDate)
                .ToList();

            return View(borrowings);
        }

        public ActionResult ReturnBook(int id)
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

            var borrowing = db.Borrowings.FirstOrDefault(b =>
                b.BorrowingId == id &&
                b.ReaderId == reader.ReaderId
            );

            if (borrowing == null)
            {
                return HttpNotFound();
            }

            if (borrowing.Status != "Approved" && borrowing.Status != "Overdue")
            {
                TempData["Error"] = "Only approved or overdue borrowings can be returned.";
                return RedirectToAction("MyBorrowings");
            }

            return View(borrowing);
        }

        [HttpPost]
        public ActionResult ConfirmReturn(int borrowingId)
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

            var borrowing = db.Borrowings.FirstOrDefault(b =>
                b.BorrowingId == borrowingId &&
                b.ReaderId == reader.ReaderId
            );

            if (borrowing == null)
            {
                return HttpNotFound();
            }

            if (borrowing.Status != "Approved" && borrowing.Status != "Overdue")
            {
                TempData["Error"] = "This borrowing cannot be returned.";
                return RedirectToAction("MyBorrowings");
            }

            borrowing.Status = "Returned";
            borrowing.ReturnDate = DateTime.Now;
            borrowing.UpdatedAt = DateTime.Now;
            borrowing.Notes = "Book returned by reader.";

            var detail = borrowing.BorrowingDetails.FirstOrDefault();

            if (detail != null)
            {
                detail.ReturnedAt = DateTime.Now;
                detail.Condition = "Good";
                detail.Notes = "Returned in good condition.";

                var book = detail.Book;

                if (book != null)
                {
                    book.AvailableCopies = book.AvailableCopies + 1;
                    book.UpdatedAt = DateTime.Now;
                }
            }

            db.SubmitChanges();

            TempData["Success"] = "Book returned successfully.";
            return RedirectToAction("MyBorrowings");
        }
    }
}