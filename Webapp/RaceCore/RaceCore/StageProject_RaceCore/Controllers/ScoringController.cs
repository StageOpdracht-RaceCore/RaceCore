using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StageProject_RaceCore.Models;
using StageProject_RaceCore.ViewModels;

namespace StageProject_RaceCore.Controllers
{
    /// <summary>
    /// Controller verantwoordelijk voor het beheren van scoring,
    /// ritresultaten en truien binnen een wedstrijd.
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
                    TempData["Error"] = "Geen wedstrijden gevonden in de database.";
                    return View(viewModel);
                }

                // --- FIX: bepaal geselecteerde race correct ---
                int selectedRaceId;

                if (raceId.HasValue)
                {
                    selectedRaceId = raceId.Value;
                }
                else if (stageId.HasValue)
                {
                    var selectedStage = await _context.Stages
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s => s.Id == stageId.Value);

                    selectedRaceId = selectedStage?.RaceId ?? races.First().Id;
                }
                else
                {
                    selectedRaceId = races.First().Id;
                }

                // --- Haal stages op ---
                var stages = await _context.Stages
                    .Where(s => s.RaceId == selectedRaceId)
                    .OrderBy(s => s.StageNumber)
                    .ToListAsync();

                if (!stages.Any())
                {
                    TempData["Error"] = "Geen ritten gevonden voor deze wedstrijd.";
                    ViewBag.Races = races;
                    ViewBag.SelectedRaceId = selectedRaceId;
                    ViewBag.AvailableStages = new List<SelectListItem>();
                    return View(viewModel);
                }

                // --- FIX: stage moet bij race horen ---
                int selectedStageId;

                if (stageId.HasValue && stages.Any(s => s.Id == stageId.Value))
                {
                    selectedStageId = stageId.Value;
                }
                else
                {
                    selectedStageId = stages.First().Id;
                }

                viewModel.StageId = selectedStageId;

                // --- Cyclisten ---
                viewModel.AvailableCyclists = await _context.Cyclists
                    .OrderBy(c => c.LastName)
                    .ThenBy(c => c.FirstName)
                    .Select(c => new SelectListItem
                    {
                        Value = c.Id.ToString(),
                        Text = c.FirstName + " " + c.LastName
                    })
                    .ToListAsync();

                // --- Bestaande resultaten ---
                var existingResults = await _context.StageResults
                    .Where(r => r.StageId == selectedStageId)
                    .Include(r => r.Cyclist)
                    .OrderBy(r => r.Position)
                    .ToListAsync();

                var existingJerseys = await _context.Jerseys
                    .Where(j => j.StageId == selectedStageId)
                    .Include(j => j.Cyclist)
                    .ToListAsync();

                // --- Top 25 ---
                for (int i = 1; i <= 25; i++)
                {
                    var result = existingResults.FirstOrDefault(r => r.Position == i);

                    viewModel.Results.Add(new StageResultViewModel
                    {
                        Position = i,
                        CyclistId = result?.CyclistId,
                        CyclistName = result?.Cyclist?.FullName ?? "",
                        HasYellowJersey = result != null && HasJersey(existingJerseys, result.CyclistId, "Red"),
                        HasGreenJersey = result != null && HasJersey(existingJerseys, result.CyclistId, "Green"),
                        HasPolkaJersey = result != null && HasJersey(existingJerseys, result.CyclistId, "Blue"),
                        HasWhiteJersey = result != null && HasJersey(existingJerseys, result.CyclistId, "White")
                    });
                }

                // --- Buiten top 25 ---
                var top25Ids = existingResults
                    .Select(r => r.CyclistId)
                    .ToHashSet();

                SetOutsideJersey(viewModel, existingJerseys, top25Ids, "Red");
                SetOutsideJersey(viewModel, existingJerseys, top25Ids, "Green");
                SetOutsideJersey(viewModel, existingJerseys, top25Ids, "Blue");
                SetOutsideJersey(viewModel, existingJerseys, top25Ids, "White");

                // --- ViewBag ---
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
                TempData["DatabaseError"] = "Database niet bereikbaar. Start OpenVPN om scoring gegevens te zien.";
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
        public async Task<IActionResult> SaveScores(ScoringViewModel model)
        {
            try
            {
                if (model.Results == null || model.StageId <= 0)
                    return RedirectToAction("Index");

                // --- VALIDATIE DUBBELE RENNERS ---
                var cyclistIds = model.Results
                    .Where(r => r.CyclistId.HasValue && r.CyclistId.Value > 0)
                    .Select(r => r.CyclistId.Value)
                    .ToList();

                if (model.YellowOutsideTop25CyclistId.HasValue)
                    cyclistIds.Add(model.YellowOutsideTop25CyclistId.Value);
                if (model.GreenOutsideTop25CyclistId.HasValue)
                    cyclistIds.Add(model.GreenOutsideTop25CyclistId.Value);
                if (model.PolkaOutsideTop25CyclistId.HasValue)
                    cyclistIds.Add(model.PolkaOutsideTop25CyclistId.Value);
                if (model.WhiteOutsideTop25CyclistId.HasValue)
                    cyclistIds.Add(model.WhiteOutsideTop25CyclistId.Value);

                var duplicates = cyclistIds
                    .GroupBy(x => x)
                    .Where(g => g.Count() > 1)
                    .Select(y => y.Key)
                    .ToList();

                if (duplicates.Any())
                {
                    var duplicateCyclist = await _context.Cyclists.FindAsync(duplicates.First());
                    TempData["Error"] = $"De renner '{duplicateCyclist?.FullName}' komt meerdere keren voor.";
                    return RedirectToAction("Index", new { stageId = model.StageId });
                }

                var stage = await _context.Stages.FindAsync(model.StageId);
                if (stage == null)
                {
                    TempData["Error"] = "Deze rit bestaat niet.";
                    return RedirectToAction("Index");
                }

                var oldResults = await _context.StageResults
                    .Where(r => r.StageId == model.StageId)
                    .ToListAsync();

                var oldJerseys = await _context.Jerseys
                    .Where(j => j.StageId == model.StageId)
                    .ToListAsync();

                _context.StageResults.RemoveRange(oldResults);
                _context.Jerseys.RemoveRange(oldJerseys);
                await _context.SaveChangesAsync();

                var usedJerseys = new HashSet<string>();

                foreach (var row in model.Results.OrderBy(r => r.Position))
                {
                    if (!row.CyclistId.HasValue)
                        continue;

                    _context.StageResults.Add(new StageResult
                    {
                        StageId = model.StageId,
                        CyclistId = row.CyclistId.Value,
                        Position = row.Position,
                        Status = "Finished"
                    });

                    if (row.HasYellowJersey)
                        AddJerseyOnce(model.StageId, row.CyclistId.Value, "Red", usedJerseys);
                    if (row.HasGreenJersey)
                        AddJerseyOnce(model.StageId, row.CyclistId.Value, "Green", usedJerseys);
                    if (row.HasPolkaJersey)
                        AddJerseyOnce(model.StageId, row.CyclistId.Value, "Blue", usedJerseys);
                    if (row.HasWhiteJersey)
                        AddJerseyOnce(model.StageId, row.CyclistId.Value, "White", usedJerseys);
                }

                AddOutsideJerseyIfNotAlreadyUsed(model.StageId, model.YellowOutsideTop25CyclistId, "Red", usedJerseys);
                AddOutsideJerseyIfNotAlreadyUsed(model.StageId, model.GreenOutsideTop25CyclistId, "Green", usedJerseys);
                AddOutsideJerseyIfNotAlreadyUsed(model.StageId, model.PolkaOutsideTop25CyclistId, "Blue", usedJerseys);
                AddOutsideJerseyIfNotAlreadyUsed(model.StageId, model.WhiteOutsideTop25CyclistId, "White", usedJerseys);

                await _context.SaveChangesAsync();

                TempData["Success"] = "Scores succesvol opgeslagen.";
                return RedirectToAction("StageResults", "Result");
            }
            catch
            {
                TempData["Error"] = "Fout bij opslaan.";
                return RedirectToAction("Index", new { stageId = model.StageId });
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
        private bool HasJersey(List<Jersey> jerseys, int cyclistId, string type)
        {
            return jerseys.Any(j => j.CyclistId == cyclistId && j.Type == type);
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
            var jersey = jerseys.FirstOrDefault(j => j.Type == type && !top25.Contains(j.CyclistId));
            if (jersey == null) return;

            if (type == "Red") vm.YellowOutsideTop25CyclistId = jersey.CyclistId;
            if (type == "Green") vm.GreenOutsideTop25CyclistId = jersey.CyclistId;
            if (type == "Blue") vm.PolkaOutsideTop25CyclistId = jersey.CyclistId;
            if (type == "White") vm.WhiteOutsideTop25CyclistId = jersey.CyclistId;
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
        private void AddOutsideJerseyIfNotAlreadyUsed(int stageId, int? cyclistId, string type, HashSet<string> used)
        {
            if (!cyclistId.HasValue) return;
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