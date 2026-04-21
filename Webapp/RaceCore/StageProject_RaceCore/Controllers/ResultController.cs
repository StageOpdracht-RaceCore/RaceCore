using Microsoft.AspNetCore.Mvc;

namespace StageProject_RaceCore.Controllers
{
  public class ResultVM
  {
    public string StageName { get; set; } = "";
    public string CyclistName { get; set; } = "";
    public int points { get; set; }

  }
    public class ResultController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

    }


}
