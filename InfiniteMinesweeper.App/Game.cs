namespace InfiniteMinesweeper;

public class Game(int? seed = null)
{
    public int MinesPerChunk => 10;
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

    public ref Cell GetCell(Pos pos, ChunkState desiredState)
    => ref GetChunk(pos.ToChunkPos(out var posInChunk), desiredState)[posInChunk];

    public void ToggleFlag(Pos cellPos)
    {
        ref var cell = ref GetCell(cellPos, ChunkState.FullyGenerated);
        if (!cell.IsUnexplored)
            return;
        cell = cell with { IsFlagged = !cell.IsFlagged };
    }

    public void Explore(Pos cellPos)
    {
        ref var cell = ref GetCell(cellPos, ChunkState.FullyGenerated);
        if (!cell.IsUnexplored || cell.IsFlagged)
            return;
        cell = cell with { IsUnexplored = false };
        if (cell.MinesAround == 0 && !cell.IsMine)
            foreach (var neighbor in GetNeighbors(cellPos))
                Explore(neighbor);
    }

    private static readonly Pos[] NeighborCells =
    [
        new(-1, -1), new(0, -1), new(1, -1),
        new(-1,  0),             new(1,  0),
        new(-1,  1), new(0,  1), new(1,  1),
    ];

    public static IEnumerable<Pos> GetNeighbors(Pos cellPos)
    {
        foreach (var pos in NeighborCells)
            yield return pos + cellPos;
    }
}
