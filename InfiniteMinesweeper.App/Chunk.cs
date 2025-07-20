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
    public bool IsCompleted { get; set; } = false;
    public bool HasExploded { get; set; }
    public virtual ref Cell this[Pos pos]
    {
        get
        {
            ref var c = ref _defaultCell;
            c = c with { PosInChunk = pos };
            return ref _defaultCell;
        }
    }
    public virtual void ClearChunk()
    { }

    public virtual int CountCell(Func<Cell, bool> predicate)
    => predicate(_defaultCell) ? Size * Size : 0;

    public virtual void CheckCompletedChunk()
    { }

    public virtual void TryClearMines()
    { }
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

    public sealed override void ClearChunk()
    {
        foreach (ref var cell in MemoryMarshal.CreateSpan(ref _cells[0, 0], Size * Size))
            if (cell is { IsFlagged: false, IsUnexplored: true })
                cell = cell with { IsUnexplored = true };
    }

    public sealed override int CountCell(Func<Cell, bool> predicate)
    {
        var count = 0;
        foreach (ref readonly var cell in MemoryMarshal.CreateSpan(ref _cells[0, 0], Size * Size))
            if (predicate(cell))
                count++;
        return count;
    }

    private Cell[,] GenerateCells(Pos pos)
    {
        var cells = _cells ?? new Cell[Size, Size];
        var set = new HashSet<Pos>();
        for (var i = 0; i < Size; i++)
            for (var j = 0; j < Size; j++)
            {
                if (cells[i, j].IsMine)
                    set.Add(new(i, j));
                cells[i, j] = new Cell(0, new(i, j), pos, !cells[i, j].IsDefault && cells[i, j].IsMine, !cells[i, j].IsDefault && cells[i, j].IsFlagged, cells[i, j].IsDefault || cells[i, j].IsUnexplored);
            }
        var rng = new Random(_game.Seed + pos.GetHashCode());
        for (var i = 0; i < _game.MinesPerChunk; i++)
        {
        Loop:
            var mine = rng.NextPos();
            ref var cell = ref cells[mine.X, mine.Y];
            if (cell.IsMine && !set.Contains(mine))
                goto Loop;
            cell = cell with { IsMine = true };
        }

        foreach (ref var cell in MemoryMarshal.CreateSpan(ref cells[0, 0], Size * Size))
            if (!cell.IsUnexplored && !set.Remove(cell.PosInChunk))
                cell = cell with { IsMine = false };
        return cells;
    }

    public static JsonConverter<ChunkWithMines> JsonConverter { get; } = new Converter();

    private sealed class Converter : JsonConverter<ChunkWithMines>
    {
        public override ChunkWithMines? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => Game.DeserializationVersion switch
        {
            1 => ReadV1(ref reader, options),
            2 => ReadV2(ref reader, options),
            _ => throw new NotImplementedException(),
        };

        private static ChunkWithMines? ReadV1(ref Utf8JsonReader reader, JsonSerializerOptions options)
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
                        if (reader.TokenType != JsonTokenType.StartArray)
                            throw new JsonException();
                        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                        {
                            var cell = JsonSerializer.Deserialize<Cell>(ref reader, options);
                            cells.Add(cell);
                        }
                        break;
                }
            }

            if ((x, y, cells) is not (int i, int j, not []))
                throw new JsonException();

            var chunk = new ChunkWithMines(new(i, j));
            foreach (ref readonly var c in CollectionsMarshal.AsSpan(cells))
                chunk[c.PosInChunk] = c with
                {
                    ChunkPos = chunk.Pos,
                };
            return chunk;
        }

        private static ChunkWithMines? ReadV2(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException();

            int? x = null;
            int? y = null;
            List<Cell> cells = [];
            bool hasExploded = false;

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
                        if (reader.TokenType != JsonTokenType.StartArray)
                            throw new JsonException();
                        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                        {
                            var cell = JsonSerializer.Deserialize<Cell>(ref reader, options);
                            hasExploded |= cell.IsMine;
                            cells.Add(cell);
                        }
                        break;
                }
            }

            if ((x, y, cells) is not (int i, int j, not []))
                throw new JsonException();

            var chunk = new ChunkWithMines(new(i, j))
            {
                HasExploded = hasExploded
            };
            foreach (ref readonly var c in CollectionsMarshal.AsSpan(cells))
                chunk[c.PosInChunk] = c with
                {
                    ChunkPos = chunk.Pos,
                };
            return chunk;
        }

        public override void Write(Utf8JsonWriter writer, ChunkWithMines value, JsonSerializerOptions options)
        {
            using var obj = writer.StartObject(options);
            obj.WriteProperty("X", value.Pos.X);
            obj.WriteProperty("Y", value.Pos.Y);
            obj.WritePropertyArray("Cells", value._cells.Cast<Cell>());
        }
    }
}

public sealed class ChunkGenerated : Chunk
{
    public ChunkGenerated(Pos pos, Game game)
    : base(pos, game)
    => (HasExploded, _cells) = (game.GetChunk(pos, ChunkState.MineGenerated).HasExploded, GenerateCells(game, pos));
    private readonly Cell[,] _cells;
    public override ChunkState State => ChunkState.FullyGenerated;
    public override int RemainingMines => _cells.AsValueEnumerable<Cell>()
        .Sum(static c => (c.IsMine ? 1 : 0) - (c.IsFlagged ? 1 : 0) - (c is { IsMine: true, IsUnexplored: false } ? 1 : 0));

    public override ref Cell this[Pos pos]
    => ref _cells[pos.X, pos.Y];

    public sealed override void ClearChunk()
    {
        foreach (ref var cell in MemoryMarshal.CreateSpan(ref _cells[0, 0], Size * Size))
            if (cell is { IsFlagged: false, IsUnexplored: true })
                cell = cell with { IsUnexplored = false };
    }

    public override void CheckCompletedChunk()
    {
        var remainingMines = RemainingMines;
        foreach (ref readonly var cell in MemoryMarshal.CreateSpan(ref _cells[0, 0], Size * Size))
            if (cell is { IsUnexplored: true, IsFlagged: false } && --remainingMines < 0)
                return;
        IsCompleted = true;
        foreach (ref var cell in MemoryMarshal.CreateSpan(ref _cells[0, 0], Size * Size))
            if (cell.IsUnexplored)
                cell = cell with { IsFlagged = true };
        foreach (var pos in Game.GetNeighbors(Pos))
            _game.GetChunk(pos, ChunkState.NotGenerated).TryClearMines();
    }

    public override void TryClearMines()
    {
        if (!HasExploded || IsCompleted)
            return;
        var completedCount = 0;
        foreach (var pos in Game.GetNeighbors(Pos))
            switch (_game.GetChunk(pos, ChunkState.NotGenerated))
            {
                case { HasExploded: false, IsCompleted: false }:
                    return;
                case { IsCompleted: true }:
                    completedCount++;
                    break;
            }
        if (completedCount <= 1)
            return;
        HasExploded = false;
        foreach (ref var cell in MemoryMarshal.CreateSpan(ref _cells[0, 0], Size * Size))
            if (cell is { IsMine: true, IsUnexplored: false })
                cell = cell with { IsMine = false };
        CheckCompletedChunk();
    }

    public sealed override int CountCell(Func<Cell, bool> predicate)
    {
        var count = 0;
        foreach (ref readonly var cell in MemoryMarshal.CreateSpan(ref _cells[0, 0], Size * Size))
            if (predicate(cell))
                count++;
        return count;
    }

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
        for (var i = 0; i < Size; i++)
            for (var j = 0; j < Size; j++)
            {
                Pos cellPos = new(i, j);
                ref var cell = ref game.GetCell(cellPos.ToCellPos(pos), ChunkState.MineGenerated);
                cell = cell with
                {
                    MinesAround = MinesAround(game, cellPos.ToCellPos(pos)),
                    IsUnexplored = cell.IsDefault || cell.IsUnexplored,
                    IsFlagged = !cell.IsDefault && cell.IsFlagged,
                    IsMine = !cell.IsDefault && cell.IsMine,
                };
                cells[i, j] = cell;
            }
        return cells;

        static int MinesAround(Game game, Pos cellPos)
        => game.CountArround(cellPos, static c => c.IsMine);
    }

    public static JsonConverter<ChunkGenerated> JsonConverter { get; } = new Converter();

    private sealed class Converter : JsonConverter<ChunkGenerated>
    {
        public override ChunkGenerated? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => throw new NotImplementedException();

        public override void Write(Utf8JsonWriter writer, ChunkGenerated value, JsonSerializerOptions options)
        {
            using var obj = writer.StartObject(options);
            obj.WriteProperty("X", value.Pos.X);
            obj.WriteProperty("Y", value.Pos.Y);
            obj.WritePropertyArray("Cells", value._cells.Cast<Cell>());
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
