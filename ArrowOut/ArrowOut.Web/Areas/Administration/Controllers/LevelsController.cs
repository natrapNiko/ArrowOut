using ArrowOut.Services.Exceptions;
using ArrowOut.Services.Levels;
using ArrowOut.Services.Models;
using ArrowOut.Web.Areas.Administration.Models;
using Microsoft.AspNetCore.Mvc;

namespace ArrowOut.Web.Areas.Administration.Controllers;

public class LevelsController(ILevelAdminService levelAdminService) : AdministrationController
{
    public const long MaxImportBytes = LevelFile.MaxFileBytes;

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] AdminLevelQuery query, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            query = new AdminLevelQuery();
        }

        var levels = await levelAdminService.GetPagedAsync(query, cancellationToken);
        return View(new AdminLevelsIndexViewModel { Query = query, Levels = levels });
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken) =>
        View("Form", new LevelFormViewModel { Input = await levelAdminService.CreateDraftAsync(cancellationToken) });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind(Prefix = "Input")] LevelInputModel input, CancellationToken cancellationToken)
    {
        input.Id = null;
        if (!ModelState.IsValid)
        {
            return FormView(null, input);
        }

        try
        {
            var id = await levelAdminService.CreateAsync(input, cancellationToken);
            Success($"Level {input.Number} created.");
            return RedirectToAction(nameof(Edit), new { id });
        }
        catch (Exception ex) when (TryAddDomainErrors(ex))
        {
            return FormView(null, input);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var input = await levelAdminService.GetForEditAsync(id, cancellationToken);
        input.Id = id;
        return FormView(id, input);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind(Prefix = "Input")] LevelInputModel input, CancellationToken cancellationToken)
    {
        input.Id = id; // always use the id from the route, whatever the hidden field says
        if (!ModelState.IsValid)
        {
            return FormView(id, input);
        }

        try
        {
            await levelAdminService.UpdateAsync(id, input, cancellationToken);
            Success($"Level {input.Number} saved.");
            return RedirectToAction(nameof(Edit), new { id });
        }
        catch (Exception ex) when (TryAddDomainErrors(ex))
        {
            return FormView(id, input);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var input = await levelAdminService.GetForEditAsync(id, cancellationToken);
        input.Id = id;
        return View(input);
    }

    [HttpPost]
    [ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken cancellationToken)
    {
        await levelAdminService.DeleteAsync(id, cancellationToken);
        Success("Level deleted.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPublished(int id, bool isPublished, CancellationToken cancellationToken)
    {
        await levelAdminService.SetPublishedAsync(id, isPublished, cancellationToken);
        Success(isPublished ? "Level published." : "Level moved to drafts.");
        return RedirectToAction(nameof(Index));
    }

    // For the jQuery [Remote] check on the level number.
    [HttpGet]
    public async Task<IActionResult> IsNumberAvailable([Bind(Prefix = "Input.Number")] int number, [Bind(Prefix = "Input.Id")] int? id, CancellationToken cancellationToken) =>
        await levelAdminService.IsNumberAvailableAsync(number, id, cancellationToken)
            ? Json(true)
            : Json($"Level number {number} is already taken.");

    // The editor's "Generate" button: returns a new random solvable layout as JSON.
    [HttpPost]
    public IActionResult Generate([FromBody] GenerateLevelRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        return Json(levelAdminService.Generate(request));
    }

    [HttpGet]
    public async Task<IActionResult> Export(int id, CancellationToken cancellationToken)
    {
        var (fileName, content) = await levelAdminService.ExportAsync(id, cancellationToken);
        return File(content, "application/json", fileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxImportBytes + 8_192)]
    public async Task<IActionResult> Import(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            Error("Choose a level JSON file to import.");
            return RedirectToAction(nameof(Index));
        }

        if (file.Length > MaxImportBytes || !string.Equals(Path.GetExtension(file.FileName), ".json", StringComparison.OrdinalIgnoreCase))
        {
            Error($"Only .json files up to {MaxImportBytes / 1024} KB are accepted.");
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var id = await levelAdminService.ImportAsync(stream, cancellationToken);
            Success("Level imported as a draft. Review it and publish when ready.");
            return RedirectToAction(nameof(Edit), new { id });
        }
        catch (Exception ex) when (ex is InvalidLevelDesignException or DuplicateLevelNumberException)
        {
            Error(ex.Message);
            return RedirectToAction(nameof(Index));
        }
    }

    private ViewResult FormView(int? id, LevelInputModel input)
    {
        var report = levelAdminService.Analyze(input.Width, input.Height, input.ArrowsJson);
        return View("Form", new LevelFormViewModel { Id = id, Input = input, Report = report });
    }

    private bool TryAddDomainErrors(Exception exception)
    {
        switch (exception)
        {
            case InvalidLevelDesignException design:
                foreach (var error in design.Errors)
                {
                    ModelState.AddModelError("Input.ArrowsJson", error);
                }

                return true;

            case DuplicateLevelNumberException duplicate:
                ModelState.AddModelError("Input.Number", duplicate.Message);
                return true;

            default:
                return false;
        }
    }
}
