using Microsoft.AspNetCore.Mvc;
using StageProject_RaceCore.Models;

namespace StageProject_RaceCore.Controllers
{
    public class RaceController : Controller
    {

    private static List<Race> players = new List<Race>
        {
            new Race { Id = 1, Name = "Roel" },
            new Race { Id = 2, Name = "Casper" },
            new Race { Id = 3, Name = "Jonas" }
        };

    public IActionResult Index()
        {
            return View();
        }
    }
}
