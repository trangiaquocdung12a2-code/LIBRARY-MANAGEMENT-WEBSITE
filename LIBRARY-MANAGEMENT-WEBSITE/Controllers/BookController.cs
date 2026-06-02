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
            // 1. Maintain filter states in ViewBag
            ViewBag.Search = search;
            ViewBag.CategoryId = categoryId;
            ViewBag.SelectedLetter = letter;
            ViewBag.SortBy = string.IsNullOrEmpty(sortBy) ? "title" : sortBy;
            ViewBag.ViewMode = string.IsNullOrEmpty(viewMode) ? "grid" : viewMode; // default to card grid layout

            // Pass categories for the filter dropdown
            ViewBag.Categories = db.Categories.Where(c => c.IsActive == true).ToList();

            // 2. Query Base
            var query = db.Books.AsQueryable();

            // 3. Apply Text Search Filter
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(b => b.Title.Contains(search) ||
                                         b.Author.FullName.Contains(search) ||
                                         b.Publisher.PublisherName.Contains(search));
            }

            // 4. Apply Category Dropdown Filter
            if (categoryId.HasValue)
            {
                query = query.Where(b => b.CategoryId == categoryId.Value);
            }

            // 5. Apply Alphabetical First-Letter Filter
            if (!string.IsNullOrEmpty(letter) && letter != "All")
            {
                query = query.Where(b => b.Title.StartsWith(letter));
            }

            // 6. Apply Selected Sorting Options
            switch (ViewBag.SortBy.ToString().ToLower())
            {
                case "author":
                    query = query.OrderBy(b => b.Author.FullName).ThenBy(b => b.Title);
                    break;
                case "date":
                    // Fallback sorting based on Id if CreatedAt column doesn't exist
                    query = query.OrderByDescending(b => b.BookId);
                    break;
                case "title":
                default:
                    query = query.OrderBy(b => b.Title);
                    break;
            }

            // 7. Pagination Computations
            int pageSize = 8;
            int totalItems = query.Count();
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            ViewBag.CurrentPage = page;

            var books = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return View(books);
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