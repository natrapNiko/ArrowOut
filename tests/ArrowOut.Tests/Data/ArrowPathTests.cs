using ArrowOut.Data.Models;
using ArrowOut.Game;

namespace ArrowOut.Tests.Data;

public class ArrowPathTests
{
    [Fact]
    public void BentArrow_RoundTripsThroughThePathColumn()
    {
        var piece = new ArrowPiece(7, [new GridPoint(1, 1), new GridPoint(1, 2), new GridPoint(2, 2)], Direction.Up);

        var entity = Arrow.FromPiece(piece);
        entity.Id = 7;

        Assert.Equal("1,1;1,2;2,2", entity.Path);
        Assert.Equal(3, entity.Length);
        Assert.Equal(piece, entity.ToPiece());
    }

    [Fact]
    public void EmptyPath_FallsBackToAStraightArrow()
    {
        var entity = new Arrow { Id = 1, X = 2, Y = 2, Direction = Direction.Left, Length = 2 };

        Assert.Equal([new GridPoint(2, 2), new GridPoint(3, 2)], entity.ToPiece().Cells);
    }

    [Theory]
    [InlineData("1,1;x,2")]
    [InlineData("1;2")]
    [InlineData("-1,2")]
    public void CorruptPath_IsRejected(string path)
    {
        Assert.Throws<FormatException>(() => Arrow.DecodePath(path));
    }
}
