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

        public ActionResult Index(string search, int? categoryId, int page = 1)
        {
            int pageSize = 8;

            var books = db.Books
                .Where(b => b.IsActive == true);

            if (!string.IsNullOrWhiteSpace(search))
            {
                books = books.Where(b =>
                    b.Title.Contains(search) ||
                    (b.Author != null && b.Author.FullName.Contains(search)) ||
                    (b.Category != null && b.Category.CategoryName.Contains(search)) ||
                    (b.Publisher != null && b.Publisher.PublisherName.Contains(search))
                );
            }

            if (categoryId.HasValue)
            {
                books = books.Where(b => b.CategoryId == categoryId.Value);
            }

            int totalBooks = books.Count();
            int totalPages = (int)Math.Ceiling((double)totalBooks / pageSize);

            var bookList = books
                .OrderBy(b => b.Title)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.Search = search;
            ViewBag.CategoryId = categoryId;
            ViewBag.Categories = db.Categories
                .Where(c => c.IsActive == true)
                .OrderBy(c => c.CategoryName)
                .ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View(bookList);
        }

        public ActionResult Details(int id)
        {
            var book = db.Books
                .FirstOrDefault(b => b.BookId == id && b.IsActive == true);

            if (book == null)
            {
                return HttpNotFound();
            }

            return View(book);
        }
    }
}