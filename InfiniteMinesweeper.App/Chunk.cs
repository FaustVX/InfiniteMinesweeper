using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
    => _cells = GenerateCells(pos);
    private ChunkWithMines(Pos pos)
    : base(pos, null!)
    => _cells = new Cell[Size, Size];

    internal void GenerateAfterDeserialization(Game game)
    {
        GetGame(this) = game;
        GetCells(this) = GenerateCells(Pos);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_game")]
        static extern ref Game GetGame(Chunk chunk);
        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = nameof(_cells))]
        static extern ref Cell[,] GetCells(ChunkWithMines chunk);
    }
    private readonly Cell[,] _cells;
    public override ChunkState State => ChunkState.MineGenerated;
    public override int RemainingMines => _cells.AsValueEnumerable<Cell>()
        .Sum(static c => (c.IsMine ? 1 : 0) - (c.IsFlagged ? 1 : 0));

    public override ref Cell this[Pos pos]
    => ref _cells[pos.X, pos.Y];

    private Cell[,] GenerateCells(Pos pos)
    {
        var cells = _cells ?? new Cell[Size, Size];
        for (int i = 0; i < Size; i++)
            for (int j = 0; j < Size; j++)
                cells[i, j] = new Cell(0, new(i, j), pos, IsMine: false, !cells[i, j].IsDefault && cells[i, j].IsFlagged, cells[i, j].IsDefault || cells[i, j].IsUnexplored);
        var rng = new Random(_game.Seed + pos.GetHashCode());
        for (int i = 0; i < _game.MinesPerChunk; i++)
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
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException();

            int? x = null;
            int? y = null;
            List<Cell> cells = [];

            while (reader.TryGetProperty(out var propertyName))
            {
                switch (propertyName)
                {
                    case "X":
                        x = JsonSerializer.Deserialize<int>(ref reader, options);
                        break;
                    case "Y":
                        y = JsonSerializer.Deserialize<int>(ref reader, options);
                        break;
                    case "Cells":
                        if (reader.TokenType == JsonTokenType.StartArray)
                            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                            {
                                var cell = JsonSerializer.Deserialize<Cell>(ref reader, options);
                                cells.Add(cell);
                            }
                        else
                            throw new JsonException();
                        break;
                }
            }

            if (x is int i && y is int j)
            {
                var chunk = new ChunkWithMines(new(i, j));
                foreach (ref var c in CollectionsMarshal.AsSpan(cells))
                {
                    ref var o = ref chunk[c.PosInChunk];
                    o = c with
                    {
                        ChunkPos = chunk.Pos,
                        IsMine = o.IsMine,
                        MinesAround = o.MinesAround,
                    };
                }
                return chunk;
            }

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
                ref var cell = ref game.GetCell(cellPos.ToCellPos(pos), ChunkState.MineGenerated);
                cell = cell with
                {
                    MinesAround = MinesAround(game, cellPos.ToCellPos(pos)),
                    IsUnexplored = cell.IsDefault || cell.IsUnexplored,
                    IsFlagged = !cell.IsDefault && cell.IsFlagged,
                };
                cells[i, j] = cell;
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
