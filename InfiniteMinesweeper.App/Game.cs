namespace InfiniteMinesweeper;

public class Game
{
    private Dictionary<Pos, Chunk> _chunks = [];

    public Chunk GetChunk(Pos pos, ChunkState desiredState)
    {
        if (_chunks.TryGetValue(pos, out var chunk) && chunk.State >= desiredState)
        {
            return chunk;
        }

        Chunk newChunk = desiredState switch
        {
            ChunkState.NotGenerated => new Chunk(pos),
            ChunkState.MineGenerated => new ChunkWithMines(pos),
            ChunkState.FullyGenerated => new ChunkGenerated(pos),
            _ => throw new ArgumentOutOfRangeException(nameof(desiredState))
        };
        // Ensure all 8 neighboring chunks exist and are at least (desiredState - 1) or better
        var minNeighborState = desiredState > ChunkState.NotGenerated ? desiredState - 1 : ChunkState.NotGenerated;
        var neighborOffsets = new[]
        {
            new Pos(-1, -1), new Pos(0, -1), new Pos(1, -1),
            new Pos(-1,  0),                 new Pos(1,  0),
            new Pos(-1,  1), new Pos(0,  1), new Pos(1,  1),
        };

        foreach (var offset in neighborOffsets)
        {
            var neighborPos = pos + offset;
            if (!_chunks.TryGetValue(neighborPos, out var neighbor) || neighbor.State < minNeighborState)
            {
                GetChunk(neighborPos, minNeighborState);
            }
        }
        _chunks[pos] = newChunk;
        return newChunk;
    }
}
