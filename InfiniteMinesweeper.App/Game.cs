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

public class Game(int? seed = null, int? minesPerChunk = null)
{
    [JsonConstructor]
    private Game(int seed, Dictionary<Pos, Chunk> chunks)
    : this(new int?(seed))
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
            group1[i++] = n;
        group1[i] = pos1;

        if (pos1 == pos2)
        {
            // Return group1 and group2 as hashsets
            HashSet<Pos> hash1 = [.. group1];
            return [hash1];
        }

        var group2 = (stackalloc Pos[9]);
        i = 0;
        foreach (var n in GetNeighbors(pos2))
            group2[i++] = n;
        group2[i] = pos2;

        // Intersect
        var intersect = (stackalloc Pos[9]);
        int intersectCount = 0;
        foreach (var p1 in group1)
            if (group2.Contains(p1))
                intersect[intersectCount++] = p1;

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
            if (!intersect[..intersectCount].Contains(p))
                only1[only1Count++] = p;

        // Only in group2
        var only2 = (stackalloc Pos[9]);
        int only2Count = 0;
        foreach (var p in group2)
            if (!intersect[..intersectCount].Contains(p))
                only2[only2Count++] = p;

        {
            HashSet<Pos> hash1 = [.. only1[..only1Count]];
            HashSet<Pos> hash2 = [.. only2[..only2Count]];
            HashSet<Pos> inter = [.. intersect[..intersectCount]];
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
}

[Serializable]
public class ExplodeException() : Exception;
