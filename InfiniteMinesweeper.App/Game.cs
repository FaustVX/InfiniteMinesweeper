namespace InfiniteMinesweeper;

public class Game(int? seed = null)
{
    public int MaxMinesPerChunk => 5;
    public readonly int Seed = seed ?? Random.Shared.Next();
    private readonly Dictionary<Pos, Chunk> _chunks = [];

    public Chunk GetChunk(Pos pos, ChunkState desiredState)
    {
        if (_chunks.TryGetValue(pos, out var chunk) && chunk.State >= desiredState)
        {
            return chunk;
        }

        return _chunks[pos] = desiredState switch
        {
            ChunkState.NotGenerated => new Chunk(pos),
            ChunkState.MineGenerated => new ChunkWithMines(pos, this),
            ChunkState.FullyGenerated => new ChunkGenerated(pos, this),
            _ => throw new ArgumentOutOfRangeException(nameof(desiredState))
        };
    }

    public Cell GetCell(Pos pos, ChunkState desiredState)
    => GetChunk(pos.ToChunkPos(out var posInChunk), desiredState)[posInChunk];
}
