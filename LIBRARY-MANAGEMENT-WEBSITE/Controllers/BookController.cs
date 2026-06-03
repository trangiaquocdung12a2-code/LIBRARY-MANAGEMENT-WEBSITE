using System;
using System.Configuration;
using System.Linq;
using System.Web.Mvc;
using LIBRARY_MANAGEMENT_WEBSITE.Models;

namespace LIBRARY_MANAGEMENT_WEBSITE.Controllers
{
    public class BookController : Controller
    {
        private DataClasses1DataContext db = new DataClasses1DataContext(
            ConfigurationManager.ConnectionStrings["LibraryDBConnectionString"].ConnectionString
        );

        public ActionResult Index(string search, int? categoryId, string letter, string sortBy, string viewMode, int page = 1)
        {
            ViewBag.Search = search;
            ViewBag.CategoryId = categoryId;
            ViewBag.SelectedLetter = letter;
            ViewBag.SortBy = string.IsNullOrEmpty(sortBy) ? "title" : sortBy;
            ViewBag.ViewMode = string.IsNullOrEmpty(viewMode) ? "grid" : viewMode;

            ViewBag.Categories = db.Categories.Where(c => c.IsActive == true).ToList();

            // Modified to load ALL books (even inactive ones) so they stay visible in the catalog layout listings
            var query = db.Books.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(b => b.Title.Contains(search) ||
                                         b.Author.FullName.Contains(search) ||
                                         b.Publisher.PublisherName.Contains(search));
            }

            if (categoryId.HasValue)
            {
                query = query.Where(b => b.CategoryId == categoryId.Value);
            }

            if (!string.IsNullOrEmpty(letter) && letter != "All")
            {
                query = query.Where(b => b.Title.StartsWith(letter));
            }

            switch (ViewBag.SortBy.ToString().ToLower())
            {
                case "author":
                    query = query.OrderBy(b => b.Author.FullName).ThenBy(b => b.Title);
                    break;
                case "date":
                    query = query.OrderByDescending(b => b.BookId);
                    break;
                case "title":
                default:
                    query = query.OrderBy(b => b.Title);
                    break;
            }

            int pageSize = 8;
            int totalItems = query.Count();
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            ViewBag.CurrentPage = page;

            var books = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return View(books);
        }

        public ActionResult Details(int id)
        {
            var book = db.Books.FirstOrDefault(b => b.BookId == id);

            if (book == null)
            {
                return HttpNotFound();
            }

            bool isUnavailable = false;
            string statusMessage = "";

            // Check if user is logged in
            if (Session["UserId"] != null)
            {
                int currentUserId = Convert.ToInt32(Session["UserId"]);

                var reader = db.Readers.FirstOrDefault(r => r.UserId == currentUserId);
                if (reader != null)
                {
                    // Fix: Look up through BorrowingDetails to see if this reader ever checked out this book
                    bool hasBorrowedBefore = db.BorrowingDetails.Any(bd => bd.BookId == id && bd.Borrowing.ReaderId == reader.ReaderId);

                    if (hasBorrowedBefore)
                    {
                        isUnavailable = true;
                        statusMessage = "Book currently unavailable (You have already borrowed this book previously).";
                    }
                }
            }

            // Fallback general checkout check rule (If explicitly set inactive, or out of stock)
            if (!book.IsActive || book.AvailableCopies <= 0)
            {
                isUnavailable = true;
                statusMessage = string.IsNullOrEmpty(statusMessage) ? "Book currently unavailable (Out of copies)." : statusMessage;
            }

            ViewBag.IsUnavailable = isUnavailable;
            ViewBag.StatusMessage = statusMessage;

            return View(book);
        }
    }
}