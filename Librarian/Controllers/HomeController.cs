using Librarian.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Librarian.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        // Reached two ways: UseStatusCodePagesWithReExecute("/error/{0}") passes the status code as id;
        // UseExceptionHandler("/error") re-executes with no id (the response is already 500).
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error(int? id)
        {
            int code = id ?? HttpContext.Response.StatusCode;
            if (code < 400) code = 500;
            Response.StatusCode = code;

            if (code >= 500)
                _logger.LogError("Served error page {Code} for {Path}", code,
                    HttpContext.Items["originalPath"] ?? HttpContext.Request.Path);

            return View(new ErrorViewModel
            {
                StatusCode = code,
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            });
        }
    }
}