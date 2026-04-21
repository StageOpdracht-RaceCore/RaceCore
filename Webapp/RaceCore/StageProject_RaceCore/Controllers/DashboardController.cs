using Microsoft.AspNetCore.Mvc;

namespace StageProject_RaceCore.Controllers
{
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}