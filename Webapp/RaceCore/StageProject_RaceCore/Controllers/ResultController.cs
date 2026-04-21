using Microsoft.AspNetCore.Mvc;

namespace StageProject_RaceCore.Controllers
{
  public class ResultVM
  {
    public string StageName { get; set; } = "";
    public string CyclistName { get; set; } = "";
    public int points { get; set; }
    public string JerseyType { get; set; } = "";
    public int totalPoints { get; set; }

  }

    public class ResultController : Controller
    {
        public IActionResult Index()
      {
      var dummyResultVM = new List<ResultVM>
          {
            new ResultVM { CyclistName = "John Doe", StageName = "Stage 1", points = 10, JerseyType = "Yellow", totalPoints = 100 },
            new ResultVM { CyclistName = "John Doe", StageName = "Stage 1", points = 10, JerseyType = "Yellow", totalPoints = 400 },
            new ResultVM { CyclistName = "John Doe", StageName = "Stage 1", points = 10, JerseyType = "Yellow", totalPoints = 300 },
            new ResultVM { CyclistName = "John Doe", StageName = "Stage 1", points = 10, JerseyType = "Yellow", totalPoints = 5000 }
          };

      var rankedData = dummyResultVM.OrderByDescending(r => r.totalPoints).ToList();

      return View(rankedData);

    }

    }


}
