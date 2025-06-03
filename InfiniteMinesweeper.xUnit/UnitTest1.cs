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

        posInChunk = new(Chunk.Size - 1, Chunk.Size - 1);
        chunkPos = new(-1, -1);
        Assert.Equal(new(-1, -1), posInChunk.ToCellPos(chunkPos));
    }

    [Fact]
    public void Pos_ToChunkPos()
    {
        var cellPos = new Pos(0, 0);
        Assert.Equal(new(0, 0), cellPos.ToChunkPos(out var posInChunk));
        Assert.Equal(new(0, 0), posInChunk);

        cellPos = new(1, 1);
        Assert.Equal(new(0, 0), cellPos.ToChunkPos(out posInChunk));
        Assert.Equal(new(1, 1), posInChunk);

        cellPos = new(-1, -1);
        Assert.Equal(new(-1, -1), cellPos.ToChunkPos(out posInChunk));
        Assert.Equal(new(Chunk.Size - 1, Chunk.Size - 1), posInChunk);
    }
}
