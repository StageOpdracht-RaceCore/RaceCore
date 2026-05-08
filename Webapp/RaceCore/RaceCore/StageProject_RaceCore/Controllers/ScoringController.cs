using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StageProject_RaceCore.Models;
using StageProject_RaceCore.ViewModels;

namespace StageProject_RaceCore.Controllers
{
    /* ScoringController.cs
       Purpose: Manage stage scoring input and persistence. Handles
       rendering the scoring UI, validating input (unique cyclist
       checks) and saving StageResults and Jersey data. */
    /// <summary>
    /// Controller for stage scoring (input, validation, save).
    /// </summary>
    public class ScoringController : Controller
    {
        /// <summary>
        /// Databasecontext voor toegang tot de applicatiedatabase.
        /// </summary>
        private readonly AppDbContext _context;

        /// <summary>
        /// Initialiseert een nieuwe instantie van de ScoringController.
        /// </summary>
        /// <param name="context">
        /// Databasecontext gebruikt voor databankoperaties.
        /// </param>
        public ScoringController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Toont de scoringpagina voor een geselecteerde wedstrijd en rit.
        /// 
        /// Functionaliteiten:
        /// - ophalen van wedstrijden
        /// - bepalen van geselecteerde race en rit
        /// - ophalen van renners
        /// - laden van bestaande ritresultaten
        /// - laden van truien
        /// - opbouwen van het ScoringViewModel
        /// - voorbereiden van dropdowns voor de View
        /// </summary>
        /// <param name="raceId">
        /// Optionele ID van de geselecteerde wedstrijd.
        /// </param>
        /// <param name="stageId">
        /// Optionele ID van de geselecteerde rit.
        /// </param>
        /// <returns>
        /// Een View met een ingevuld ScoringViewModel.
        /// </returns>
        public async Task<IActionResult> Index(int? raceId = null, int? stageId = null)
        {
            var viewModel = new ScoringViewModel();

            try
            {
                var races = await _context.Races
                    .OrderBy(r => r.Id)
                    .ToListAsync();

                if (!races.Any())
                {
                    TempData["Error"] = "Geen wedstrijden gevonden.";
                    return View(viewModel);
                }

                // --- BEPAAL RACE ---
                int selectedRaceId = raceId
                    ?? (stageId.HasValue
                        ? await _context.Stages
                            .Where(s => s.Id == stageId.Value)
                            .Select(s => s.RaceId)
                            .FirstOrDefaultAsync()
                        : races.First().Id);

                // fallback als stage niet bestaat
                if (selectedRaceId == 0)
                    selectedRaceId = races.First().Id;

                // --- STAGES ---
                var stages = await _context.Stages
                    .Where(s => s.RaceId == selectedRaceId)
                    .OrderBy(s => s.StageNumber)
                    .ToListAsync();

                if (!stages.Any())
                {
                    TempData["Error"] = "Geen ritten gevonden.";
                    ViewBag.Races = races;
                    ViewBag.SelectedRaceId = selectedRaceId;
                    ViewBag.AvailableStages = new List<SelectListItem>();
                    return View(viewModel);
                }

                // --- BEPAAL STAGE ---
                int selectedStageId = stageId.HasValue && stages.Any(s => s.Id == stageId.Value)
                    ? stageId.Value
                    : stages.First().Id;

                viewModel.StageId = selectedStageId;

                // --- CYCLISTEN ---
                viewModel.AvailableCyclists = await _context.Cyclists
                    .OrderBy(c => c.LastName)
                    .ThenBy(c => c.FirstName)
                    .Select(c => new SelectListItem
                    {
                        Value = c.Id.ToString(),
                        Text = c.FirstName + " " + c.LastName
                    })
                    .ToListAsync();

                // --- RESULTATEN ---
                var results = await _context.StageResults
                    .Where(r => r.StageId == selectedStageId)
                    .Include(r => r.Cyclist)
                    .OrderBy(r => r.Position)
                    .ToListAsync();

                var jerseys = await _context.Jerseys
                    .Where(j => j.StageId == selectedStageId)
                    .ToListAsync();

                // --- TOP 25 ---
                for (int i = 1; i <= 25; i++)
                {
                    var r = results.FirstOrDefault(x => x.Position == i);

                    viewModel.Results.Add(new StageResultViewModel
                    {
                        Position = i,
                        CyclistId = r?.CyclistId,
                        CyclistName = r?.Cyclist?.FullName ?? "",
                        HasYellowJersey = HasJersey(jerseys, r.CyclistId, "Red"),
                        HasGreenJersey = HasJersey(jerseys, r.CyclistId, "Green"),
                        HasPolkaJersey = HasJersey(jerseys, r.CyclistId, "Blue"),
                        HasWhiteJersey = HasJersey(jerseys, r.CyclistId, "White")
                    });
                }

                // --- BUITEN TOP 25 ---
                var top25Ids = results.Select(r => r.CyclistId).ToHashSet();

                SetOutsideJersey(viewModel, jerseys, top25Ids, "Red");
                SetOutsideJersey(viewModel, jerseys, top25Ids, "Green");
                SetOutsideJersey(viewModel, jerseys, top25Ids, "Blue");
                SetOutsideJersey(viewModel, jerseys, top25Ids, "White");

                // --- VIEWBAG ---
                ViewBag.Races = races;
                ViewBag.SelectedRaceId = selectedRaceId;
                ViewBag.AvailableStages = stages.Select(s => new SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = $"Rit {s.StageNumber} - {s.Name}",
                    Selected = s.Id == selectedStageId
                }).ToList();
            }
            catch
            {
                TempData["Error"] = "Database fout.";
            }

            return View(viewModel);
        }

        /// <summary>
        /// Slaat ritresultaten en truien op voor een specifieke rit.
        /// 
        /// Functionaliteiten:
        /// - valideren van dubbele renners
        /// - verwijderen van oude resultaten
        /// - opslaan van nieuwe resultaten
        /// - opslaan van truien
        /// - tonen van succes- of foutmeldingen
        /// </summary>
        /// <param name="model">
        /// ScoringViewModel met alle ritresultaten en trui-informatie.
        /// </param>
        /// <returns>
        /// Redirect naar de juiste pagina na opslaan.
        /// </returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveScores(ScoringViewModel model, int raceId)
        {
            try
            {
                if (model.StageId <= 0)
                    return RedirectToAction("Index");

                var stage = await _context.Stages.FindAsync(model.StageId);
                if (stage == null)
                {
                    TempData["Error"] = "Rit niet gevonden.";
                    return RedirectToAction("Index");
                }

                // --- DUPLICATES CHECK ---
                var ids = model.Results
                    .Where(r => r.CyclistId.HasValue)
                    .Select(r => r.CyclistId.Value)
                    .ToList();

                if (model.YellowOutsideTop25CyclistId.HasValue) ids.Add(model.YellowOutsideTop25CyclistId.Value);
                if (model.GreenOutsideTop25CyclistId.HasValue) ids.Add(model.GreenOutsideTop25CyclistId.Value);
                if (model.PolkaOutsideTop25CyclistId.HasValue) ids.Add(model.PolkaOutsideTop25CyclistId.Value);
                if (model.WhiteOutsideTop25CyclistId.HasValue) ids.Add(model.WhiteOutsideTop25CyclistId.Value);

                var duplicate = ids.GroupBy(x => x).FirstOrDefault(g => g.Count() > 1);

                if (duplicate != null)
                {
                    var rider = await _context.Cyclists.FindAsync(duplicate.Key);
                    TempData["Error"] = $"Renner '{rider?.FullName}' staat dubbel.";
                    return RedirectToAction("Index", new { stageId = model.StageId, raceId });
                }

                // --- DELETE OUDE DATA ---
                var oldResults = _context.StageResults.Where(r => r.StageId == model.StageId);
                var oldJerseys = _context.Jerseys.Where(j => j.StageId == model.StageId);

                _context.StageResults.RemoveRange(oldResults);
                _context.Jerseys.RemoveRange(oldJerseys);
                await _context.SaveChangesAsync();

                var used = new HashSet<string>();

                // --- SAVE TOP 25 ---
                foreach (var r in model.Results.OrderBy(x => x.Position))
                {
                    if (!r.CyclistId.HasValue) continue;

                    _context.StageResults.Add(new StageResult
                    {
                        StageId = model.StageId,
                        CyclistId = r.CyclistId.Value,
                        Position = r.Position,
                        Status = "Finished"
                    });

                    if (r.HasYellowJersey) AddJerseyOnce(model.StageId, r.CyclistId.Value, "Red", used);
                    if (r.HasGreenJersey) AddJerseyOnce(model.StageId, r.CyclistId.Value, "Green", used);
                    if (r.HasPolkaJersey) AddJerseyOnce(model.StageId, r.CyclistId.Value, "Blue", used);
                    if (r.HasWhiteJersey) AddJerseyOnce(model.StageId, r.CyclistId.Value, "White", used);
                }

                // --- BUITEN TOP 25 ---
                AddOutsideJerseyIfNotAlreadyUsed(model.StageId, model.YellowOutsideTop25CyclistId, "Red", used);

                AddOutsideJerseyIfNotAlreadyUsed(model.StageId, model.GreenOutsideTop25CyclistId, "Green", used);

                AddOutsideJerseyIfNotAlreadyUsed(model.StageId, model.PolkaOutsideTop25CyclistId, "Blue", used);

                AddOutsideJerseyIfNotAlreadyUsed(model.StageId, model.WhiteOutsideTop25CyclistId, "White", used);

                await _context.SaveChangesAsync();

                TempData["Success"] = "Opgeslagen!";
                return RedirectToAction("Index", new { stageId = model.StageId, raceId });
            }
            catch
            {
                TempData["Error"] = "Opslaan mislukt.";
                return RedirectToAction("Index", new { stageId = model.StageId, raceId });
            }
        }

        /// <summary>
        /// Controleert of een renner een specifieke trui bezit.
        /// </summary>
        /// <param name="jerseys">
        /// Lijst met truien van een rit.
        /// </param>
        /// <param name="cyclistId">
        /// ID van de renner.
        /// </param>
        /// <param name="type">
        /// Type trui dat gecontroleerd moet worden.
        /// </param>
        /// <returns>
        /// True indien de renner de trui bezit, anders false.
        /// </returns>
        private bool HasJersey(List<Jersey> jerseys, int? cyclistId, string type)
        {
            if (!cyclistId.HasValue)
                return false;

            return jerseys.Any(j =>
                j.CyclistId == cyclistId.Value &&
                j.Type == type);
        }

        /// <summary>
        /// Zet een truihouder buiten de top 25 in het ViewModel.
        /// </summary>
        /// <param name="vm">
        /// Het scoring viewmodel.
        /// </param>
        /// <param name="jerseys">
        /// Lijst met truien.
        /// </param>
        /// <param name="top25">
        /// HashSet met IDs van renners binnen de top 25.
        /// </param>
        /// <param name="type">
        /// Type trui dat verwerkt wordt.
        /// </param>
        private void SetOutsideJersey(ScoringViewModel vm, List<Jersey> jerseys, HashSet<int> top25, string type)
        {
            var j = jerseys.FirstOrDefault(x => x.Type == type && !top25.Contains(x.CyclistId));
            if (j == null) return;

            if (type == "Red") vm.YellowOutsideTop25CyclistId = j.CyclistId;
            if (type == "Green") vm.GreenOutsideTop25CyclistId = j.CyclistId;
            if (type == "Blue") vm.PolkaOutsideTop25CyclistId = j.CyclistId;
            if (type == "White") vm.WhiteOutsideTop25CyclistId = j.CyclistId;
        }

        /// <summary>
        /// Voegt een trui toe indien deze nog niet gebruikt werd.
        /// </summary>
        /// <param name="stageId">
        /// ID van de rit.
        /// </param>
        /// <param name="cyclistId">
        /// ID van de renner.
        /// </param>
        /// <param name="type">
        /// Type trui.
        /// </param>
        /// <param name="used">
        /// HashSet met reeds gebruikte truien.
        /// </param>
        private void AddOutsideJerseyIfNotAlreadyUsed(
    int stageId,
    int? cyclistId,
    string type,
    HashSet<string> used)
        {
            if (!cyclistId.HasValue)
                return;

            AddJerseyOnce(stageId, cyclistId.Value, type, used);
        }

        /// <summary>
        /// Voegt een trui éénmalig toe aan de database.
        /// </summary>
        /// <param name="stageId">
        /// ID van de rit.
        /// </param>
        /// <param name="cyclistId">
        /// ID van de renner.
        /// </param>
        /// <param name="type">
        /// Type trui.
        /// </param>
        /// <param name="used">
        /// HashSet met reeds gebruikte truien.
        /// </param>
        private void AddJerseyOnce(int stageId, int cyclistId, string type, HashSet<string> used)
        {
            if (used.Contains(type)) return;

            _context.Jerseys.Add(new Jersey
            {
                StageId = stageId,
                CyclistId = cyclistId,
                Type = type
            });

            used.Add(type);
        }
    }
}
