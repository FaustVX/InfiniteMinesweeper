namespace InfiniteMinesweeper.xUnit;

public class PosTests
{
    [Fact]
    public void Operator_Add_Works()
    {
        var a = new Pos(2, 3);
        var b = new Pos(4, 5);
        Assert.Equal(new Pos(6, 8), a + b);
    }

    [Fact]
    public void Operator_Subtract_Works()
    {
        var a = new Pos(5, 7);
        var b = new Pos(2, 3);
        Assert.Equal(new Pos(3, 4), a - b);
    }

    [Fact]
    public void Operator_Multiply_Works()
    {
        var a = new Pos(2, 3);
        Assert.Equal(new Pos(4, 6), a * 2);
    }

    [Fact]
    public void Operator_Divide_Works()
    {
        var a = new Pos(6, 8);
        Assert.Equal(new Pos(3, 4), a / 2);
    }

    [Fact]
    public void Operator_Modulo_Works()
    {
        var a = new Pos(7, 9);
        Assert.Equal(new Pos(1, 0), a % 3);
    }

    [Fact]
    public void Directions_Work()
    {
        var p = new Pos(1, 1);
        Assert.Equal(new Pos(2, 1), p.East);
        Assert.Equal(new Pos(0, 1), p.West);
        Assert.Equal(new Pos(1, 0), p.North);
        Assert.Equal(new Pos(1, 2), p.South);
        Assert.Equal(new Pos(2, 0), p.NorthEast);
        Assert.Equal(new Pos(0, 0), p.NorthWest);
        Assert.Equal(new Pos(2, 2), p.SouthEast);
        Assert.Equal(new Pos(0, 2), p.SouthWest);
    }

    [Fact]
    public void ToChunkPos_And_ToPosInChunk_Work()
    {
        var pos = new Pos(7, 12);
        var chunkPos = pos.ToChunkPos(out var posInChunk);
        Assert.Equal(new Pos(7, 4), posInChunk);
        Assert.Equal(new Pos(0, 1), chunkPos);

        var posInChunk2 = pos.ToPosInChunk(out var chunkPos2);
        Assert.Equal(posInChunk, posInChunk2);
        Assert.Equal(chunkPos, chunkPos2);
    }

    [Fact]
    public void ToCellPos_Works()
    {
        var posInChunk = new Pos(2, 3);
        var chunkPos = new Pos(1, 1);
        Assert.Equal(new Pos(10, 11), posInChunk.ToCellPos(chunkPos));
    }
}
