using System.Text.Json.Serialization;
using ZLinq;

namespace InfiniteMinesweeper;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Chunk), 0)]
[JsonDerivedType(typeof(ChunkWithMines), 1)]
[JsonDerivedType(typeof(ChunkGenerated), 2)]
public class Chunk(Pos pos, Game game)
{
    public const int Size = 8;
    private Cell _defaultCell = default(Cell) with { IsUnexplored = true, ChunkPos = pos };
    public Pos Pos { get; } = pos;
    [JsonIgnore]
    public virtual int RemainingMines => game.MinesPerChunk;
    [JsonIgnore]
    public virtual ChunkState State => ChunkState.NotGenerated;
    public virtual ref Cell this[Pos pos]
    {
        get
        {
            ref var c = ref _defaultCell;
            c = c with { PosInChunk = pos };
            return ref _defaultCell;
        }
    }
}

public sealed class ChunkWithMines(Pos pos, Game game) : Chunk(pos, game)
{
    [JsonInclude]
    private readonly Cell[,] _cells = GenerateCells(game, pos);
    [JsonIgnore]
    public override ChunkState State => ChunkState.MineGenerated;
    public override int RemainingMines => _cells.AsValueEnumerable<Cell>()
        .Sum(static c => (c.IsMine ? 1 : 0) - (c.IsFlagged ? 1 : 0));

    public override ref Cell this[Pos pos]
    => ref _cells[pos.X, pos.Y];

    static Cell[,] GenerateCells(Game game, Pos pos)
    {
        var cells = new Cell[Size, Size];
        for (int i = 0; i < Size; i++)
            for (int j = 0; j < Size; j++)
                cells[i, j] = new Cell(0, new(i, j), pos, false, false, IsUnexplored: true);
        var rng = new Random(game.Seed + pos.GetHashCode());
        for (int i = 0; i < game.MinesPerChunk; i++)
        {
Loop:
            var mine = rng.NextPos();
            ref var cell = ref cells[mine.X, mine.Y];
            if (cell.IsMine)
                goto Loop;
            cell = cell with { IsMine = true };
        }
        return cells;
    }
}

public sealed class ChunkGenerated(Pos pos, Game game) : Chunk(pos, game)
{
    [JsonInclude]
    private readonly Cell[,] _cells = GenerateCells(game, pos);
    [JsonIgnore]
    public override ChunkState State => ChunkState.FullyGenerated;
    public override int RemainingMines => _cells.AsValueEnumerable<Cell>()
        .Sum(static c => (c.IsMine ? 1 : 0) - (c.IsFlagged ? 1 : 0) - (c is { IsMine: true, IsUnexplored: false } ? 1 : 0));

    public override ref Cell this[Pos pos]
    => ref _cells[pos.X, pos.Y];

    static Cell[,] GenerateCells(Game game, Pos pos)
    {
        ReadOnlySpan<Pos> neighborCells =
        [
            pos.NorthWest, pos.North, pos.NorthEast,
            pos.West,      pos,       pos.East,
            pos.SouthWest, pos.South, pos.SouthEast,
        ];

        foreach (var p in neighborCells)
            game.GetChunk(p, ChunkState.MineGenerated);

        var cells = new Cell[Size, Size];
        for (int i = 0; i < Size; i++)
            for (int j = 0; j < Size; j++)
            {
                Pos cellPos = new(i, j);
                cells[i, j] = game.GetCell(cellPos.ToCellPos(pos), ChunkState.MineGenerated) with
                {
                    MinesAround = MinesAround(game, cellPos.ToCellPos(pos)),
                    IsUnexplored = true
                };
            }

        return cells;

        static int MinesAround(Game game, Pos cellPos)
        => game.CountArround(cellPos, static c => c.IsMine);
    }
}

public enum ChunkState
{
    NotGenerated,
    MineGenerated,
    FullyGenerated,
}

public readonly record struct Cell(int MinesAround, Pos PosInChunk, Pos ChunkPos, bool IsMine, bool IsFlagged, bool IsUnexplored)
{
    public readonly int RemainingMines(Game game)
    => MinesAround - game.CountArround(PosInChunk.ToCellPos(ChunkPos), static c => c.IsFlagged || (!c.IsUnexplored && c.IsMine));
}

file static class Ext
{
    extension(Random rng)
    {
        public Pos NextPos()
        {
            int x = rng.Next(Chunk.Size);
            int y = rng.Next(Chunk.Size);
            return new(x, y);
        }
    }
}
