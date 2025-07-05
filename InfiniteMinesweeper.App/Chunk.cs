using System.Text.Json;
using System.Text.Json.Serialization;
using ZLinq;

namespace InfiniteMinesweeper;

public class Chunk(Pos pos, Game game)
{
    protected readonly Game _game = game;
    public const int Size = 8;
    private Cell _defaultCell = default(Cell) with { IsUnexplored = true, ChunkPos = pos };
    public Pos Pos { get; } = pos;
    public virtual int RemainingMines => _game.MinesPerChunk;
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

public sealed class ChunkWithMines : Chunk
{
    public ChunkWithMines(Pos pos, Game game)
    : base(pos, game)
    => _cells = GenerateCells(game, pos);
    internal ChunkWithMines(Pos pos, Game game, Cell[,] cells)
    : base(pos, game)
    => _cells = cells;
    private readonly Cell[,] _cells;
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

    public static JsonConverter<ChunkWithMines> JsonConverter { get; } = new Converter();

    private sealed class Converter : JsonConverter<ChunkWithMines>
    {
        public override ChunkWithMines? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return default;
        }

        public override void Write(Utf8JsonWriter writer, ChunkWithMines value, JsonSerializerOptions options)
        {
            return;
        }
    }
}

public sealed class ChunkGenerated : Chunk
{
    public ChunkGenerated(Pos pos, Game game)
    : base(pos, game)
    => _cells = GenerateCells(game, pos);
    internal ChunkGenerated(Pos pos, Game game, Cell[,] cells)
    : base(pos, game)
    => _cells = cells;
    private readonly Cell[,] _cells;
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

    public static JsonConverter<ChunkGenerated> JsonConverter { get; } = new Converter();

    private sealed class Converter : JsonConverter<ChunkGenerated>
    {
        public override ChunkGenerated? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return default;
        }

        public override void Write(Utf8JsonWriter writer, ChunkGenerated value, JsonSerializerOptions options)
        {
            return;
        }
    }
}

public enum ChunkState
{
    NotGenerated,
    MineGenerated,
    FullyGenerated,
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
