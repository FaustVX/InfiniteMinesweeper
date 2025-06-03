using System.Collections.Frozen;
using ZLinq;

namespace InfiniteMinesweeper;

public class Chunk(Pos pos)
{
    public const int Size = 8;
    public Pos Pos { get; } = pos;
    public virtual ChunkState State => ChunkState.NotGenerated;
    public virtual Cell this[Pos pos]
    {
        get => default;
        set { }
    }
}

public sealed class ChunkWithMines(Pos pos, Game game) : Chunk(pos)
{
    private readonly Cell[,] _cells = GenerateCells(game, pos);
    public override ChunkState State => ChunkState.MineGenerated;
    public override Cell this[Pos pos]
    {
        get => _cells[pos.X, pos.Y];
        set => _cells[pos.X, pos.Y] = value;
    }

    static Cell[,] GenerateCells(Game game, Pos pos)
    {
        var cells = new Cell[Size, Size];
        for (int i = 0; i < Size; i++)
            for (int j = 0; j < Size; j++)
                cells[i, j] = new Cell(0, new(i, j), pos, false, false, IsUnexplored: true);
        var rng = new Random(game.Seed + pos.GetHashCode());
        for (int i = 0; i < game.MaxMinesPerChunk; i++)
        {
            var mine = rng.NextPos();
            ref var cell = ref cells[mine.X, mine.Y];
            cell = cell with { IsMine = true };
        }
        return cells;
    }
}

public sealed class ChunkGenerated(Pos pos, Game game) : Chunk(pos)
{
    private readonly Cell[,] _cells = GenerateCells(game, pos);
    public override ChunkState State => ChunkState.FullyGenerated;
    public override Cell this[Pos pos]
    {
        get => _cells[pos.X, pos.Y];
        set => _cells[pos.X, pos.Y] = value;
    }

    static Cell[,] GenerateCells(Game game, Pos pos)
    {
        ReadOnlySpan<Pos> neighborCells =
        [
            new Pos(-1, -1), new Pos(0, -1), new Pos(1, -1),
            new Pos(-1,  0), new Pos(0,  0), new Pos(1,  0),
            new Pos(-1,  1), new Pos(0,  1), new Pos(1,  1),
        ];

        var neighborChunks = neighborCells.AsValueEnumerable()
            .ToFrozenDictionary(static p => p, p => game.GetChunk(p + pos, ChunkState.MineGenerated));

        var thisChunk = neighborChunks[new(0, 0)];

        var cells = new Cell[Size, Size];
        for (int i = 0; i < Size; i++)
            for (int j = 0; j < Size; j++)
            {
                Pos cellPos = new(i, j);
                cells[i, j] = new Cell(MinesAround(neighborChunks, cellPos), cellPos, pos, thisChunk[cellPos].IsMine, thisChunk[cellPos].IsFlagged, IsUnexplored: true);
            }

        return cells;

        static int MinesAround(FrozenDictionary<Pos, Chunk> chunks, Pos pos)
        {
            ReadOnlySpan<Pos> neighborCells =
            [
                new Pos(-1, -1), new Pos(0, -1), new Pos(1, -1),
                new Pos(-1,  0),                 new Pos(1,  0),
                new Pos(-1,  1), new Pos(0,  1), new Pos(1,  1),
            ];

            return neighborCells.AsValueEnumerable()
                .Select(p => p + pos)
                .Count(p => chunks[p.ToChunkPos(out var posInChunk)][posInChunk].IsMine);
        }
    }
}

public enum ChunkState
{
    NotGenerated,
    MineGenerated,
    FullyGenerated,
}

public readonly record struct Pos(int X, int Y)
{
    public static Pos operator +(Pos a, Pos b)
    => new(a.X + b.X, a.Y + b.Y);
    public static Pos operator -(Pos a, Pos b)
    => new(a.X - b.X, a.Y - b.Y);
    public static Pos operator *(Pos a, int scalar)
    => new(a.X * scalar, a.Y * scalar);
    public static Pos operator /(Pos a, int scalar)
    => new(a.X / scalar, a.Y / scalar);
    public static Pos operator %(Pos a, int scalar)
    => new(a.X % scalar, a.Y % scalar);

    public Pos ToPosInChunk(out Pos chunkPos)
    {
        chunkPos = ToChunkPos(out var posInChunk);
        return posInChunk;
    }

    public Pos ToChunkPos(out Pos posInChunk)
    {
        posInChunk = (this % Chunk.Size + new Pos(Chunk.Size, Chunk.Size)) % Chunk.Size;
        return (this - posInChunk) / Chunk.Size;
    }

    public Pos ToCellPos(Pos chunkPos)
    => chunkPos * Chunk.Size + this;
}

public readonly record struct Cell(int MinesAround, Pos PosInChunk, Pos ChunkPos, bool IsMine, bool IsFlagged, bool IsUnexplored);

file static class Ext
{
    extension(Random rng)
    {
        public Pos NextPos()
        {
            int x = rng.Next(Chunk.Size);
            int y = rng.Next(Chunk.Size);
            return new Pos(x, y);
        }
    }
}
