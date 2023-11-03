using Microsoft.AspNetCore.Mvc;

namespace Librarian.Controllers
{
    public class SearchController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Advanced()
        {
            return View();
        }
    }
}
