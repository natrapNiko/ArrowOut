using System.Globalization;
using System.Text;
using ArrowOut.Data.Models.Common;
using ArrowOut.Game;
using ArrowOut.Game.Generation;

namespace ArrowOut.Data.Models;

// A random board that belongs to the player who started it. It never changes once it's
// created, so the whole layout is saved as one string instead of hundreds of rows.
// Arrow ids are just their position in that string (starting at 1).
public class Challenge : BaseAuditableModel<int>
{
    public Challenge(string ownerId, ChallengeKind kind, int seed, int width, int height, int maxLives, IReadOnlyList<ArrowPiece> arrows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentNullException.ThrowIfNull(arrows);

        OwnerId = ownerId;
        Kind = kind;
        Seed = seed;
        Width = width;
        Height = height;
        MaxLives = maxLives;
        ArrowCount = arrows.Count;
        Layout = EncodeLayout(arrows);
    }

    // For EF Core.
    protected Challenge()
    {
        OwnerId = string.Empty;
        Layout = string.Empty;
    }

    public string OwnerId { get; private set; }

    public virtual ApplicationUser Owner { get; private set; } = null!;

    public ChallengeKind Kind { get; private set; }

    public int Seed { get; private set; }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public int MaxLives { get; private set; }

    public int ArrowCount { get; private set; }

    // Arrows in id order: "d:x,y;x,y|d:x,y;..." where d is the direction number.
    public string Layout { get; private set; }

    public int Attempts { get; private set; }

    public bool IsCompleted { get; private set; }

    public int Stars { get; private set; }

    public int? BestMistakes { get; private set; }

    public DateTime? CompletedOn { get; private set; }

    public DateTime? LastPlayedOn { get; private set; }

    // 0 until the board is won. The leaderboard adds these up.
    public int Points { get; private set; }

    // How many hints the server gave out since the last restart. Only used for stats.
    public int HintsThisAttempt { get; private set; }

    public void RecordAttempt(DateTime utcNow)
    {
        Attempts++;
        HintsThisAttempt = 0;
        LastPlayedOn = utcNow;
    }

    public void RecordHint() => HintsThisAttempt++;

    // Saves a win. Points are only given the first time a board is won, so replaying it
    // doesn't earn anything. A replay with fewer crashes still counts as a new personal best.
    public ChallengeWin RecordWin(int mistakes, DateTime utcNow)
    {
        var points = PointsFor(Kind);
        var gained = IsCompleted ? 0 : points;
        var isNewBest = BestMistakes is null || mistakes < BestMistakes;

        if (!IsCompleted)
        {
            IsCompleted = true;
            CompletedOn = utcNow;
        }

        if (isNewBest)
        {
            BestMistakes = mistakes;
        }

        Stars = Math.Max(Stars, PlayerProgress.CalculateStars(mistakes));
        Points = points;
        LastPlayedOn = utcNow;
        return new ChallengeWin(points, gained, isNewBest);
    }

    // Easy 1, Medium 4, Hard 10. Doesn't matter how many arrows the board had.
    public static int PointsFor(ChallengeKind kind) => kind switch
    {
        ChallengeKind.Easy => 1,
        ChallengeKind.Medium => 4,
        ChallengeKind.Hard => 10,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown challenge kind."),
    };

    public IReadOnlyList<ArrowPiece> GetArrows() => DecodeLayout(Layout);

    public Board ToBoard() => Board.Create(Width, Height, GetArrows());

    public static string EncodeLayout(IEnumerable<ArrowPiece> arrows)
    {
        ArgumentNullException.ThrowIfNull(arrows);

        var builder = new StringBuilder();
        foreach (var arrow in arrows)
        {
            if (builder.Length > 0)
            {
                builder.Append('|');
            }

            builder.Append(CultureInfo.InvariantCulture, $"{(int)arrow.Direction}:");
            builder.Append(Arrow.EncodePath(arrow.Cells));
        }

        return builder.ToString();
    }

    public static IReadOnlyList<ArrowPiece> DecodeLayout(string layout)
    {
        ArgumentException.ThrowIfNullOrEmpty(layout);

        return layout
            .Split('|')
            .Select((entry, index) =>
            {
                var separator = entry.IndexOf(':', StringComparison.Ordinal);
                if (separator < 1
                    || !int.TryParse(entry.AsSpan(0, separator), NumberStyles.None, CultureInfo.InvariantCulture, out var direction)
                    || !Enum.IsDefined((Direction)direction))
                {
                    throw new FormatException($"Invalid challenge layout entry #{index + 1}.");
                }

                return new ArrowPiece(index + 1, Arrow.DecodePath(entry[(separator + 1)..]), (Direction)direction);
            })
            .ToList();
    }
}

// What a win gave you: the board's points, how many were new (0 on a replay) and whether it's a new best.
public sealed record ChallengeWin(int Points, int PointsGained, bool IsNewBest);
