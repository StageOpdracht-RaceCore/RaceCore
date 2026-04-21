using Microsoft.AspNetCore.Mvc;

namespace StageProject_RaceCore.Controllers
{
    public class CyclistController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

    }
}
