using System.ComponentModel.DataAnnotations;
using ArrowOut.Data.Common;
using ArrowOut.Game;
using Microsoft.AspNetCore.Mvc;

namespace ArrowOut.Services.Models;

public enum AdminLevelSort
{
    NumberAsc = 0,
    NumberDesc = 1,
    NameAsc = 2,
    Newest = 3,
}

public sealed class AdminLevelQuery
{
    [StringLength(60)]
    public string? Search { get; set; }

    public Difficulty? Difficulty { get; set; }

    public bool? IsPublished { get; set; }

    public AdminLevelSort Sort { get; set; } = AdminLevelSort.NumberAsc;

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, Paging.MaxPageSize)]
    public int PageSize { get; set; } = 15;
}

public sealed record AdminLevelListItem(
    int Id,
    int Number,
    string Name,
    Difficulty Difficulty,
    int Width,
    int Height,
    int ArrowCount,
    bool IsPublished,
    int CompletionCount,
    DateTime CreatedOn);

public sealed record DashboardStats(
    int TotalLevels,
    int PublishedLevels,
    int TotalUsers,
    int TotalCompletions,
    int TotalAttempts);

// The level create/edit form. The arrows come in as JSON from the grid editor.
public sealed class LevelInputModel
{
    public const int ArrowsJsonMaxLength = 20_000;

    // Only here for the remote "is this number taken" check in the form. The services never use it,
    // the id always comes from the route.
    public int? Id { get; set; }

    [Required]
    [Range(DataConstants.Level.NumberMin, DataConstants.Level.NumberMax)]
    [Remote("IsNumberAvailable", "Levels", "Administration", AdditionalFields = nameof(Id))]
    [Display(Name = "Level number")]
    public int Number { get; set; }

    [Required]
    [StringLength(DataConstants.Level.NameMaxLength, MinimumLength = DataConstants.Level.NameMinLength)]
    [RegularExpression(DataConstants.Level.NamePattern, ErrorMessage = "Use letters, digits, spaces and basic punctuation only.")]
    public string Name { get; set; } = string.Empty;

    [Range(DataConstants.Level.SizeMin, DataConstants.Level.SizeMax)]
    public int Width { get; set; } = 6;

    [Range(DataConstants.Level.SizeMin, DataConstants.Level.SizeMax)]
    public int Height { get; set; } = 6;

    [Range(DataConstants.Level.LivesMin, DataConstants.Level.LivesMax)]
    [Display(Name = "Lives")]
    public int MaxLives { get; set; } = 3;

    [EnumDataType(typeof(Difficulty))]
    public Difficulty Difficulty { get; set; }

    [Display(Name = "Published")]
    public bool IsPublished { get; set; }

    [Required(ErrorMessage = "Place at least one arrow on the board.")]
    [StringLength(ArrowsJsonMaxLength)]
    public string ArrowsJson { get; set; } = "[]";
}

// One arrow in the editor JSON or an import file.
public sealed class ArrowInputModel
{
    [Range(0, DataConstants.Level.SizeMax - 1)]
    public int X { get; set; }

    [Range(0, DataConstants.Level.SizeMax - 1)]
    public int Y { get; set; }

    [EnumDataType(typeof(Direction))]
    public Direction Direction { get; set; }

    [Range(DataConstants.Arrow.LengthMin, DataConstants.Arrow.LengthMax)]
    public int Length { get; set; } = 1;

    // Cells from the head back, for bent arrows. Leave it out for a straight arrow.
    [MaxLength(DataConstants.Arrow.LengthMax)]
    public List<CellInputModel>? Cells { get; set; }

    // Turns this into an engine arrow. Throws ArgumentException if the path is broken.
    public ArrowPiece ToPiece(int id) =>
        Cells is { Count: > 0 }
            ? new ArrowPiece(id, Cells.Select(c => new GridPoint(c.X, c.Y)).ToList(), Direction)
            : new ArrowPiece(id, new GridPoint(X, Y), Direction, Length);

    public static ArrowInputModel FromPiece(ArrowPiece piece)
    {
        ArgumentNullException.ThrowIfNull(piece);
        return new ArrowInputModel
        {
            X = piece.Head.X,
            Y = piece.Head.Y,
            Direction = piece.Direction,
            Length = piece.Length,
            Cells = piece.IsStraight ? null : piece.Cells.Select(c => new CellInputModel { X = c.X, Y = c.Y }).ToList(),
        };
    }
}

public sealed class CellInputModel
{
    [Range(0, DataConstants.Level.SizeMax - 1)]
    public int X { get; set; }

    [Range(0, DataConstants.Level.SizeMax - 1)]
    public int Y { get; set; }
}

public sealed class GenerateLevelRequest
{
    [Range(DataConstants.Level.SizeMin, DataConstants.Level.SizeMax)]
    public int Width { get; set; } = 6;

    [Range(DataConstants.Level.SizeMin, DataConstants.Level.SizeMax)]
    public int Height { get; set; } = 6;

    [Range(0.2, 1.0)]
    public double Fill { get; set; } = 1.0;

    [Range(2, 16)]
    public int MaxLength { get; set; } = 6;
}

public sealed record LevelDesignReport(
    IReadOnlyList<string> Errors,
    bool IsSolvable,
    int ArrowCount,
    int Depth,
    Difficulty SuggestedDifficulty)
{
    public bool IsValid => Errors.Count == 0;
}

public enum UserRoleFilter
{
    All = 0,
    Administrators = 1,
    Players = 2,
}

public sealed class UserQuery
{
    [StringLength(100)]
    public string? Search { get; set; }

    public UserRoleFilter Role { get; set; } = UserRoleFilter.All;

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, Paging.MaxPageSize)]
    public int PageSize { get; set; } = 15;
}

public sealed record UserListItem(
    string Id,
    string Email,
    string PublicName,
    bool IsAdministrator,
    bool IsLockedOut,
    int CompletedLevels,
    DateTime CreatedOn);
