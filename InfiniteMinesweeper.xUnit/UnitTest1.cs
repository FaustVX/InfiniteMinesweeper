namespace InfiniteMinesweeper.xUnit;

public class UnitTest1
{
    [Fact]
    public void Pos_ToCellPos()
    {
        var posInChunk = new Pos(0, 0);
        var chunkPos = new Pos(0, 0);
        Assert.Equal(new(0, 0), posInChunk.ToCellPos(chunkPos));

        posInChunk = new(1, 1);
        chunkPos = new(1, 1);
        Assert.Equal(new(9, 9), posInChunk.ToCellPos(chunkPos));
    }
}
