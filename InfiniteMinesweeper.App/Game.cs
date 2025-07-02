using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using ZLinq;

namespace InfiniteMinesweeper;

file abstract class DictionaryAsArrayJsonConverter<TKey, TValue>(Func<TValue, TKey> keySelector) : JsonConverter<Dictionary<TKey, TValue>>
    where TKey : notnull
{
    public override Dictionary<TKey, TValue>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var values = JsonSerializer.Deserialize<List<TValue>>(ref reader, options);
        if (values == null)
            return null;
        var dict = new Dictionary<TKey, TValue>();
        foreach (var value in values)
        {
            dict[keySelector(value)] = value;
        }
        return dict;
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<TKey, TValue> value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value.Values.ToList(), options);
    }
}

file sealed class ChunkDictJsonConverter : DictionaryAsArrayJsonConverter<Pos, Chunk>
{
    public ChunkDictJsonConverter() : base(static c => c.Pos) { }
}

file sealed class PosJsonConverter : JsonConverter<Pos>
{
    public override Pos ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var s = reader.GetString() ?? throw new JsonException("Property name is null.");
        // Expect input as "{ X: -5, Y: -3 }"
        // Remove braces and split
        s = s.Trim();
        if (!s.StartsWith('{') || !s.EndsWith('}'))
            throw new JsonException($"Invalid Pos format: {s}");
        s = s[1..^1].Trim(); // Remove '{' and '}'
        var parts = s.Split(',');
        int x = 0, y = 0;
        foreach (var part in parts)
        {
            var kv = part.Split(':', 2);
            if (kv.Length != 2)
                throw new JsonException($"Invalid Pos part: {part}");
            var key = kv[0].Trim();
            var value = kv[1].Trim();
            if (key == "X")
                x = int.Parse(value);
            else if (key == "Y")
                y = int.Parse(value);
            else
                throw new JsonException($"Unknown Pos key: {key}");
        }
        return new(x, y);
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, [DisallowNull] Pos value, JsonSerializerOptions options)
    {
        writer.WritePropertyName(value.ToString());
    }

    public override Pos Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    public override void Write(Utf8JsonWriter writer, Pos value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("X", value.X);
        writer.WriteNumber("Y", value.Y);
        writer.WriteEndObject();
    }
}

file sealed class Array2DJsonConverter<T> : JsonConverter<T[,]>
{
    public override T[,]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException();

        var items = new List<List<T>>();
        int? width = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                break;

            if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException();

            var row = new List<T>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    break;

                row.Add(JsonSerializer.Deserialize<T>(ref reader, options)!);
            }

            width ??= row.Count;
            if (row.Count != width)
                throw new JsonException("Jagged arrays are not supported.");

            items.Add(row);
        }

        var height = items.Count;
        if (width is null)
            return new T[0, 0];

        var array = new T[height, width.Value];
        for (int i = 0; i < height; i++)
            for (int j = 0; j < width; j++)
                array[i, j] = items[i][j];

        return array;
    }

    public override void Write(Utf8JsonWriter writer, T[,] value, JsonSerializerOptions options)
    {
        int height = value.GetLength(0);
        int width = value.GetLength(1);

        writer.WriteStartArray();
        for (int i = 0; i < height; i++)
        {
            writer.WriteStartArray();
            for (int j = 0; j < width; j++)
            {
                JsonSerializer.Serialize(writer, value[i, j], options);
            }
            writer.WriteEndArray();
        }
        writer.WriteEndArray();
    }
}

file sealed class GameJsonConverter : JsonConverter<Game>
{
    public override Game? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException();

        int? seed = null;
        int? minesPerChunk = null;
        Dictionary<Pos, Chunk>? chunks = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException();

            string propertyName = reader.GetString()!;
            reader.Read();

            switch (propertyName)
            {
                case "Seed":
                    seed = reader.GetInt32();
                    break;
                case "minesPerChunk":
                case "MinesPerChunk":
                    minesPerChunk = reader.GetInt32();
                    break;
                case "Chunks":
                    chunks = JsonSerializer.Deserialize<Dictionary<Pos, Chunk>>(ref reader, options);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        if (seed is null || chunks is null || minesPerChunk is null)
            throw new JsonException("Missing required properties for Game.");

        return new(seed.Value, chunks, minesPerChunk.Value);
    }

    public override void Write(Utf8JsonWriter writer, Game value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}

file sealed class ChunkJsonConverter() : JsonConverter<Chunk>
{
    public override Chunk? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException();

        Pos? pos = null;
        Cell[,]? cells = null;
        ChunkState? state = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException();

            string propertyName = reader.GetString()!;
            reader.Read();

            switch (propertyName)
            {
                case "State":
                    var stateStr = reader.GetString();
                    state = stateStr switch
                    {
                        nameof(ChunkState.MineGenerated) => ChunkState.MineGenerated,
                        nameof(ChunkState.FullyGenerated) => ChunkState.FullyGenerated,
                        _ => ChunkState.NotGenerated
                    };
                    break;
                case "Pos":
                    pos = JsonSerializer.Deserialize<Pos>(ref reader, options);
                    break;
                case "_cells":
                    cells = JsonSerializer.Deserialize<Cell[,]>(ref reader, options);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        if (pos is null || state is null)
            throw new JsonException("Missing required properties for Chunk.");

        // Find Game instance from options or context if needed
        // For this context, we can't get Game, so pass null (should be set after deserialization)
        // If you need Game, you must provide a way to resolve it here

        return state switch
        {
            ChunkState.MineGenerated => new ChunkWithMines(pos.Value, null!, cells!),
            ChunkState.FullyGenerated => new ChunkGenerated(pos.Value, null!, cells!),
            ChunkState.NotGenerated or _ => new Chunk(pos.Value, null!),
        };
    }

    public override void Write(Utf8JsonWriter writer, Chunk value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case ChunkWithMines withMines:
                writer.WriteStartObject();
                writer.WriteString("State", nameof(ChunkState.MineGenerated));
                writer.WritePropertyName("Pos");
                JsonSerializer.Serialize(writer, withMines.Pos, options);
                writer.WritePropertyName("_cells");
                var cells = GetCell(withMines);
                JsonSerializer.Serialize(writer, cells, options);
                writer.WriteEndObject();
                break;
            case ChunkGenerated generated:
                writer.WriteStartObject();
                writer.WriteString("State", nameof(ChunkState.FullyGenerated));
                writer.WritePropertyName("Pos");
                JsonSerializer.Serialize(writer, generated.Pos, options);
                writer.WritePropertyName("_cells");
                var genCells = GetCell(generated);
                JsonSerializer.Serialize(writer, genCells, options);
                writer.WriteEndObject();
                break;
            case { State: ChunkState.NotGenerated }:
                break;
            default:
                throw new JsonException($"Unknown chunk state: {value.State}");
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_cells")]
    private extern ref readonly Cell[,] GetCell(ChunkWithMines chunk);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_cells")]
    private extern ref readonly Cell[,] GetCell(ChunkGenerated chunk);
}

public class Game(int? seed = null, int? minesPerChunk = null)
{
    [JsonConstructor]
    internal Game(int seed, Dictionary<Pos, Chunk> chunks, int minesPerChunk)
    : this(seed, minesPerChunk)
    {
        _chunks = chunks;
    }

    public int MinesPerChunk { get; } = minesPerChunk is >= 0 and < (Chunk.Size * Chunk.Size) ? minesPerChunk.Value : 10;
    [JsonInclude]
    public readonly int Seed = seed ?? Random.Shared.Next();
    [JsonInclude, JsonPropertyName("Chunks")]
    // [JsonConverter(typeof(ChunkDictJsonConverter))]
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

    public void Save(FileInfo file)
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions() { Converters = { new PosJsonConverter(), new Array2DJsonConverter<Cell>(), new ChunkJsonConverter() }, ReferenceHandler = ReferenceHandler.Preserve });
        File.WriteAllText(file.FullName, json);
    }

    public static Game Load(FileInfo file)
    => JsonSerializer.Deserialize<Game>(File.ReadAllText(file.FullName), new JsonSerializerOptions() { Converters = { new PosJsonConverter(), new Array2DJsonConverter<Cell>(), new GameJsonConverter(), new ChunkJsonConverter() }, IncludeFields = true, PropertyNameCaseInsensitive = false, ReferenceHandler = ReferenceHandler.Preserve })!;
}

[Serializable]
public class ExplodeException() : Exception;
