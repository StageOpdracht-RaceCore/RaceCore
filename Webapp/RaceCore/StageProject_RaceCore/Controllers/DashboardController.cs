using Microsoft.AspNetCore.Mvc;

namespace StageProject_RaceCore.Controllers
{
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult TeamOverview()
        {
            return View();
        }

        public IActionResult Riders()
        {
            return View();
        }

        public IActionResult Calendar()
        {
            return View();
        }

        public IActionResult Settings()
        {
            return View();
        }
    }
}