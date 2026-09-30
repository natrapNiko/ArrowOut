using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArrowOut.Game;
using ArrowOut.Game.Solving;
using ArrowOut.Services.Models;

namespace ArrowOut.Services.Levels;

public interface ILevelDesignValidator
{
    // Reads the editor JSON into arrows. Bad input gives errors back instead of throwing.
    (IReadOnlyList<ArrowInputModel> Arrows, IReadOnlyList<string> Errors) ParseArrows(string? arrowsJson);

    // Checks bounds and overlaps and, most importantly, that the board can actually be cleared.
    LevelDesignReport Validate(int width, int height, IReadOnlyList<ArrowInputModel> arrows);
}

public sealed class LevelDesignValidator(IPuzzleSolver solver) : ILevelDesignValidator
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        MaxDepth = 8,
        Converters = { new JsonStringEnumConverter() },
    };

    public (IReadOnlyList<ArrowInputModel> Arrows, IReadOnlyList<string> Errors) ParseArrows(string? arrowsJson)
    {
        if (string.IsNullOrWhiteSpace(arrowsJson))
        {
            return ([], ["Place at least one arrow on the board."]);
        }

        if (arrowsJson.Length > LevelInputModel.ArrowsJsonMaxLength)
        {
            return ([], ["The arrow layout is too large."]);
        }

        List<ArrowInputModel>? arrows;
        try
        {
            arrows = JsonSerializer.Deserialize<List<ArrowInputModel>>(arrowsJson, JsonOptions);
        }
        catch (JsonException)
        {
            return ([], ["The arrow layout is not valid JSON."]);
        }

        if (arrows is null || arrows.Count == 0)
        {
            return ([], ["Place at least one arrow on the board."]);
        }

        var errors = new List<string>();
        for (var i = 0; i < arrows.Count; i++)
        {
            var results = new List<ValidationResult>();
            var valid = Validator.TryValidateObject(arrows[i], new ValidationContext(arrows[i]), results, validateAllProperties: true);
            foreach (var cell in arrows[i].Cells ?? [])
            {
                // DataAnnotations don't look inside lists, so check each path cell by hand.
                valid &= Validator.TryValidateObject(cell, new ValidationContext(cell), results, validateAllProperties: true);
            }

            if (!valid)
            {
                errors.AddRange(results.Select(r => $"Arrow {i + 1}: {r.ErrorMessage}"));
            }
        }

        return (arrows, errors);
    }

    public LevelDesignReport Validate(int width, int height, IReadOnlyList<ArrowInputModel> arrows)
    {
        ArgumentNullException.ThrowIfNull(arrows);

        List<ArrowPiece> pieces;
        try
        {
            pieces = arrows.Select((a, i) => a.ToPiece(i + 1)).ToList();
        }
        catch (ArgumentException ex)
        {
            // Catches bad lengths, broken or self-crossing paths, and heads that don't line up with the body.
            return Invalid([ex.Message.Split(Environment.NewLine)[0]], arrows.Count);
        }

        var structural = BoardValidator.Validate(width, height, pieces);
        if (structural.Count > 0)
        {
            return Invalid(structural, arrows.Count);
        }

        var board = Board.Create(width, height, pieces);
        var solution = solver.Solve(board);
        var metrics = PuzzleAnalyzer.Analyze(board);

        IReadOnlyList<string> errors = solution.IsSolvable
            ? []
            : [$"The level cannot be cleared: {solution.StuckArrowIds.Count} arrow(s) end up jammed."];

        return new LevelDesignReport(errors, solution.IsSolvable, board.Count, metrics.Depth, PuzzleAnalyzer.Classify(metrics));
    }

    private static LevelDesignReport Invalid(IReadOnlyList<string> errors, int count) =>
        new(errors, false, count, 0, Difficulty.Easy);
}
