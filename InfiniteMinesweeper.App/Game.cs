using System.Text.Json;
using System.Text.Json.Serialization;
using ZLinq;

namespace InfiniteMinesweeper;

public class Game(int? seed = null, int? minesPerChunk = null)
{
    private Game(int seed, Dictionary<Pos, Chunk> chunks, int minesPerChunk)
    : this(seed, minesPerChunk)
    => _chunks = chunks;

    public int MinesPerChunk { get; } = minesPerChunk is >= 0 and < (Chunk.Size * Chunk.Size) ? minesPerChunk.Value : 10;
    public readonly int Seed = seed ?? Random.Shared.Next();
    private readonly Dictionary<Pos, Chunk> _chunks = [];

    public Chunk GetChunk(Pos pos, ChunkState desiredState)
    {
        if (_chunks.TryGetValue(pos, out var chunk) && chunk is not null && chunk.State >= desiredState)
            return chunk;

        return _chunks[pos] = desiredState switch
        {
            ChunkState.NotGenerated => new Chunk(pos, this),
            ChunkState.MineGenerated => new ChunkWithMines(pos, this),
            ChunkState.FullyGenerated => new ChunkGenerated(pos, this),
            _ => throw new ArgumentOutOfRangeException(nameof(desiredState))
        };
    }

    public HashSet<Pos>[] GetCollidingGroups(Pos pos1, Pos pos2)
    {
        var group1 = (stackalloc Pos[9]);
        int i = 0;
        foreach (var n in GetNeighbors(pos1))
            if (GetCell(n, ChunkState.NotGenerated).IsUnexplored)
                group1[i++] = n;
        if (GetCell(pos1, ChunkState.NotGenerated).IsUnexplored)
            group1[i++] = pos1;
        group1 = group1[..i];

        if (pos1 == pos2)
        {
            // Return group1 and group2 as hashsets
            HashSet<Pos> hash1 = [.. group1];
            return [hash1];
        }

        var group2 = (stackalloc Pos[9]);
        i = 0;
        foreach (var n in GetNeighbors(pos2))
            if (GetCell(n, ChunkState.NotGenerated).IsUnexplored)
                group2[i++] = n;
        if (GetCell(pos2, ChunkState.NotGenerated).IsUnexplored)
            group2[i++] = pos2;
        group2 = group2[..i];

        // Intersect
        var intersect = (stackalloc Pos[9]);
        int intersectCount = 0;
        foreach (var p1 in group1)
            if (group2.Contains(p1))
                intersect[intersectCount++] = p1;
        intersect = intersect[..intersectCount];

        if (intersectCount == 0)
        {
            // Return group1 and group2 as hashsets
            HashSet<Pos> hash1 = [.. group1];
            HashSet<Pos> hash2 = [.. group2];
            return [hash1, hash2];
        }

        // Only in group1
        var only1 = (stackalloc Pos[9]);
        int only1Count = 0;
        foreach (var p in group1)
            if (!intersect.Contains(p))
                only1[only1Count++] = p;
        only1 = only1[..only1Count];

        // Only in group2
        var only2 = (stackalloc Pos[9]);
        int only2Count = 0;
        foreach (var p in group2)
            if (!intersect.Contains(p))
                only2[only2Count++] = p;
        only2 = only2[..only2Count];

        {
            HashSet<Pos> hash1 = [.. only1];
            HashSet<Pos> hash2 = [.. only2];
            HashSet<Pos> inter = [.. intersect];
            return [hash1, hash2, inter];
        }
    }

    public ref Cell GetCell(Pos pos, ChunkState desiredState)
    => ref GetChunk(pos.ToChunkPos(out var posInChunk), desiredState)[posInChunk];

    public void ToggleFlag(Pos cellPos)
    {
        ref var cell = ref GetCell(cellPos, ChunkState.MineGenerated);
        if (cell.IsUnexplored)
            cell = cell with { IsFlagged = !cell.IsFlagged };
        else if (cell.MinesAround == CountArround(cellPos, static c => c.IsUnexplored || c.IsMine))
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
            if (cell.RemainingMines(this) == 0 && !cell.IsMine)
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
            if (cell.MinesAround == CountArround(cellPos, static c => c.IsFlagged || (!c.IsUnexplored && c.IsMine)) && !cell.IsMine)
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

    public int CountArround(Pos cellPos, Func<Cell, bool> predicate)
    {
        ReadOnlySpan<Pos> neighborCells = [.. GetNeighbors(cellPos)];

        return neighborCells
#if !NET10_0_OR_GREATER
        .ToArray()
#endif
        .AsValueEnumerable()
        .Count(p => predicate(GetCell(p, ChunkState.MineGenerated)));
    }

    public static JsonConverter<Game> JsonConverter { get; } = new Converter();

    internal static int DeserializationVersion { get; private set; }

    private sealed class Converter : JsonConverter<Game>
    {
        public override Game? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException();

            if (reader.TryGetProperty(options, out var propertyName, out byte? version) && propertyName == "$Version")
                DeserializationVersion = version.Value;
            else
                throw new JsonException();

            int? seed = null;
            int? mines = null;
            Dictionary<Pos, Chunk> chunks = [];

            while (reader.TryGetProperty(out propertyName))
            {
                switch (propertyName)
                {
                    case "Seed":
                        seed = JsonSerializer.Deserialize<int>(ref reader, options);
                        break;
                    case "MinesPerChunk":
                        mines = JsonSerializer.Deserialize<int>(ref reader, options);
                        break;
                    case "Chunks":
                        if (reader.TokenType == JsonTokenType.StartArray)
                            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                            {
                                var chunk = JsonSerializer.TryDeserialize<ChunkWithMines>(ref reader, options)
                                    ?? throw new JsonException();
                                chunks[chunk.Pos] = chunk;
                            }
                        else
                            throw new JsonException();
                        break;
                }
            }

            if (seed is int s && mines is int m)
            {
                var game = new Game(s, chunks, m) { _waitFor1stMove = false };
                foreach (var c in chunks.Values)
                    if (c is ChunkWithMines cwm)
                        cwm.GenerateAfterDeserialization(game);
                foreach (var p in chunks.Keys.ToArray())
                    _ = game.GetChunk(p, ChunkState.FullyGenerated);

                return game;
            }
            return default;
        }

        public override void Write(Utf8JsonWriter writer, Game value, JsonSerializerOptions options)
        {
            return;
        }
    }

    private readonly static JsonSerializerOptions SerializerOptions = new()
    {
        Converters =
        {
            JsonConverter,
            ChunkWithMines.JsonConverter,
            Cell.JsonConverter,
        },
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = false,
        ReferenceHandler = ReferenceHandler.Preserve
    };

    public void Save(FileInfo file)
    {
        var json = JsonSerializer.Serialize(this, SerializerOptions);
        File.WriteAllText(file.FullName, json);
    }

    public static Game Load(FileInfo file)
    => JsonSerializer.Deserialize<Game>(File.ReadAllText(file.FullName), SerializerOptions)!;
}

[Serializable]
public class ExplodeException() : Exception;
