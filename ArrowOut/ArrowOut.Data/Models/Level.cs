using ArrowOut.Data.Models.Common;
using ArrowOut.Game;

namespace ArrowOut.Data.Models;

public class Level : BaseAuditableModel<int>
{
    public int Number { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Width { get; set; }

    public int Height { get; set; }

    public int MaxLives { get; set; } = 3;

    public Difficulty Difficulty { get; set; }

    public bool IsPublished { get; set; }

    public virtual ICollection<Arrow> Arrows { get; private set; } = new HashSet<Arrow>();

    public virtual ICollection<PlayerProgress> Progress { get; private set; } = new HashSet<PlayerProgress>();

    // Swaps out all the arrows. Check the new board is valid before calling this.
    public void ReplaceArrows(IEnumerable<Arrow> arrows)
    {
        ArgumentNullException.ThrowIfNull(arrows);

        Arrows.Clear();
        foreach (var arrow in arrows)
        {
            Arrows.Add(arrow);
        }
    }

    // Arrow ids on the board are the database ids, so they stay the same between requests.
    public Board ToBoard() => Board.Create(Width, Height, Arrows.Select(a => a.ToPiece()));
}
