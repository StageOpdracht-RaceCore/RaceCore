using Microsoft.AspNetCore.Mvc;

namespace StageProject_RaceCore.Controllers
{
    public class StageController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
