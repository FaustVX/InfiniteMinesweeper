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
        ref var cell = ref GetCell(cellPos, ChunkState.MineGenerated);
        if (cell.IsUnexplored)
            cell = cell with { IsFlagged = !cell.IsFlagged };
        else if (cell.MinesAround == GetNeighbors(cellPos).Count(p => GetCell(p, ChunkState.MineGenerated) is { IsUnexplored: true }))
            foreach (var pos in GetNeighbors(cellPos))
            {
                ref var c = ref GetCell(pos, ChunkState.MineGenerated);
                if (c.IsUnexplored)
                    c = c with { IsFlagged = true };
            }
    }

    private bool _waitFor1stMove = true;

    public int Explore(Pos cellPos)
    {
        if (_waitFor1stMove)
        {
            _waitFor1stMove = false;
            ref var c = ref GetCell(cellPos, ChunkState.MineGenerated);
            c = c with { IsMine = false };
            foreach (var pos in GetNeighbors(cellPos))
            {
                c = ref GetCell(pos, ChunkState.MineGenerated);
                c = c with { IsMine = false };
            }
        }

        ref var cell = ref GetCell(cellPos, ChunkState.FullyGenerated);
        if (cell.IsUnexplored)
            return ExploreUnexplored(cellPos);
        if (cell.MinesAround > 0)
            return ExploreClue(cellPos);
        return 0;

        int ExploreUnexplored(Pos cellPos)
        {
            ref var cell = ref GetCell(cellPos, ChunkState.FullyGenerated);
            if (cell.IsFlagged || !cell.IsUnexplored)
                return 0;
            cell = cell with { IsUnexplored = false };
            if (cell.IsMine)
                throw new ExplodeException();
            var count = 1;
            if (cell.MinesAround == 0 && !cell.IsMine)
                foreach (var neighbor in GetNeighbors(cellPos))
                    count += ExploreUnexplored(neighbor);
            return count;
        }

        int ExploreClue(Pos cellPos)
        {
            ref var cell = ref GetCell(cellPos, ChunkState.FullyGenerated);
            if (cell.IsFlagged)
                return 0;
            var count = 0;
            if (cell.MinesAround == GetNeighbors(cellPos).Count(p => GetCell(p, ChunkState.MineGenerated) is { IsFlagged: true }) && !cell.IsMine)
                foreach (var neighbor in GetNeighbors(cellPos))
                    count += ExploreUnexplored(neighbor);
            return count;
        }
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

[Serializable]
public class ExplodeException() : Exception;
