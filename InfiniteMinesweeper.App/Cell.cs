using System.Text.Json;
using System.Text.Json.Serialization;

namespace InfiniteMinesweeper;

public readonly record struct Cell(int MinesAround, Pos PosInChunk, Pos ChunkPos, bool IsMine, bool IsFlagged, bool IsUnexplored)
{
    public readonly int RemainingMines(Game game)
    => MinesAround - game.CountArround(PosInChunk.ToCellPos(ChunkPos), static c => c.IsFlagged || (!c.IsUnexplored && c.IsMine));

    public static JsonConverter<Cell> JsonConverter { get; } = new Converter();

    private sealed class Converter : JsonConverter<Cell>
    {
        public override Cell Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return default;
        }

        public override void Write(Utf8JsonWriter writer, Cell value, JsonSerializerOptions options)
        {
            return;
        }
    }
}
