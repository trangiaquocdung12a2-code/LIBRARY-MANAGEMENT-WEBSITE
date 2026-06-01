using System.Configuration;
using System.Linq;
using System.Web.Mvc;
using LIBRARY_MANAGEMENT_WEBSITE.Models;

namespace LIBRARY_MANAGEMENT_WEBSITE.Controllers
{
    public class HomeController : Controller
    {
        private DataClasses1DataContext db = new DataClasses1DataContext(
            ConfigurationManager.ConnectionStrings["LibraryDBConnectionString"].ConnectionString
        );

        public ActionResult Index()
        {
            ViewBag.NewBooks = db.Books
                .Where(b => b.IsActive == true)
                .OrderByDescending(b => b.CreatedAt)
                .Take(8)
                .ToList();

            ViewBag.FeaturedBooks = db.Books
                .Where(b => b.IsActive == true && b.IsFeatured == true)
                .Take(8)
                .ToList();

            ViewBag.Categories = db.Categories
                .Where(c => c.IsActive == true)
                .ToList();

            return View();
        }
    }
}