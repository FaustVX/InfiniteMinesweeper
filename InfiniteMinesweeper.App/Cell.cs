using System.Text.Json;
using System.Text.Json.Serialization;

namespace InfiniteMinesweeper;

public readonly record struct Cell(int MinesAround, Pos PosInChunk, Pos ChunkPos, bool IsMine, bool IsFlagged, bool IsUnexplored)
{
    public readonly int RemainingMines(Game game)
    => MinesAround - game.CountArround(PosInChunk.ToCellPos(ChunkPos), static c => c.IsFlagged || (!c.IsUnexplored && c.IsMine));

    public bool IsDefault
    => this == default;

    public static JsonConverter<Cell> JsonConverter { get; } = new Converter();

    private sealed class Converter : JsonConverter<Cell>
    {
        public override Cell Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException();

            int? x = null;
            int? y = null;
            bool? isFlagged = null;
            bool? IsUnexplored = null;

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
                    case "IsFlagged":
                        isFlagged = JsonSerializer.Deserialize<bool>(ref reader, options);
                        break;
                    case "IsUnexplored":
                        IsUnexplored = JsonSerializer.Deserialize<bool>(ref reader, options);
                        break;
                }
            }

            if (x is int i && y is int j)
                return new(default, new(i, j), default, default, isFlagged ?? false, IsUnexplored ?? true);

            return default;
        }

        public override void Write(Utf8JsonWriter writer, Cell value, JsonSerializerOptions options)
        {
            return;
        }
    }
}
