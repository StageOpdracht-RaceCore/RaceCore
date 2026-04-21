using Microsoft.AspNetCore.Mvc;

namespace StageProject_RaceCore.Controllers
{
    public class PlayerController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
