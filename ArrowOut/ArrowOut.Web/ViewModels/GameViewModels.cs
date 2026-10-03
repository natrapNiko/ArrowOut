using ArrowOut.Game.Generation;
using ArrowOut.Services.Models;
using ArrowOut.Services.Themes;
using ArrowOut.Services.Users;

namespace ArrowOut.Web.ViewModels;

public sealed class ChallengeViewModel
{
    public required ChallengeInfo Challenge { get; init; }

    public ChallengeKindOption Kind => ChallengeKindOption.For(Challenge.Kind);
}

// How each kind looks on the home page cards and the game page header.
public sealed record ChallengeKindOption(ChallengeKind Kind, string Name, string? Arrows, string Icon)
{
    public static readonly IReadOnlyList<ChallengeKindOption> All =
    [
        new(ChallengeKind.Easy, "Easy", "~40 arrows", "bi-emoji-smile"),
        new(ChallengeKind.Normal, "Normal", "~200 arrows", "bi-lightning-charge"),
        new(ChallengeKind.Hard, "Hard", "400–700 arrows", "bi-fire"),

        // No arrow count on purpose, the Challenge game should be a surprise.
        new(ChallengeKind.Challenge, "Challenge", null, "bi-trophy"),
    ];

    public string CssClass => $"is-{Kind.ToString().ToLowerInvariant()}";

    // What a win is worth on the leaderboard, e.g. "+4 points".
    public string PointsLabel
    {
        get
        {
            var points = ArrowOut.Data.Models.Challenge.PointsFor(Kind);
            return $"+{points} {(points == 1 ? "point" : "points")}";
        }
    }

    public static ChallengeKindOption For(ChallengeKind kind) => All.First(o => o.Kind == kind);
}

public sealed class LeaderboardViewModel
{
    public required ChallengeKind Kind { get; init; }

    public required PagedResult<LeaderboardEntry> Entries { get; init; }

    public ChallengeKindOption Option => ChallengeKindOption.For(Kind);

    // The board the player came from (win dialog), for a "Back to game" button.
    public int? FromChallengeId { get; init; }

    // The player's newest unfinished board, if it's a different one.
    public int? UnfinishedChallengeId { get; init; }

    // Page links keep the current tab (and the way back to the game).
    public PaginationViewModel Pagination => new(Entries, "Index", new Dictionary<string, string?>
    {
        ["kind"] = Kind.ToString(),
        ["from"] = FromChallengeId?.ToString(System.Globalization.CultureInfo.InvariantCulture),
    });
}

public sealed class SettingsViewModel
{
    public required PlayerSettingsModel Input { get; init; }

    public required IReadOnlyList<ThemeDefinition> Themes { get; init; }
}

public sealed class HomeViewModel
{
    public ChallengeSummary? Summary { get; init; }
}
