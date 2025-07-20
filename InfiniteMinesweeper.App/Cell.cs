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
        => Game.DeserializationVersion switch
        {
            1 => ReadV1(ref reader, options),
            2 => ReadV2(ref reader, options),
            _ => throw new NotImplementedException(),
        };

        private static Cell ReadV1(ref Utf8JsonReader reader, JsonSerializerOptions options)
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

            throw new JsonException();
        }

        private static Cell ReadV2(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException();

            int? x = null;
            int? y = null;
            bool? isFlagged = null;
            bool? isUnexplored = null;
            bool? isMine = null;

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
                    case "State":
                        switch (JsonSerializer.Deserialize<string>(ref reader, options))
                        {
                            case "flag":
                                isFlagged = true;
                                break;
                            case "mine":
                                isMine = true;
                                isUnexplored = false;
                                break;
                            case "explored":
                                isUnexplored = false;
                                break;
                            default:
                                throw new JsonException();
                        }
                        break;
                }
            }

            if ((x, y, isFlagged ?? isUnexplored ?? isMine) is (int i, int j, not null))
                return new(default, new(i, j), default, isMine ?? false, isFlagged ?? false, isUnexplored ?? true);

            throw new JsonException();
        }

        public override void Write(Utf8JsonWriter writer, Cell value, JsonSerializerOptions options)
        {
            if (value is { IsFlagged: true } or { IsUnexplored: false } or { IsMine: true, IsUnexplored: false })
            {
                using var obj = writer.StartObject(options);
                obj.WriteProperty("X", value.PosInChunk.X);
                obj.WriteProperty("Y", value.PosInChunk.Y);
                var state = value switch
                {
                    { IsFlagged: true } => "flag",
                    { IsMine: true } => "mine",
                    { IsUnexplored: false } => "explored",
                    _ => throw new JsonException(),
                };
                if (state is string s)
                    obj.WriteProperty("State", s);
            }
        }
    }
}
