using Microsoft.AspNetCore.Mvc;

namespace StageProject_RaceCore.Controllers
{
    public class DraftController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
