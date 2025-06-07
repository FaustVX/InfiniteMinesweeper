using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using InfiniteMinesweeper;
using Spectre.Console;

var bgColors = FrozenDictionary.Create<(bool isChunkEven, bool isCellEven), ConsoleColor>(null,
    new((true, true), ConsoleColor.Gray),
    new((true, false), ConsoleColor.DarkGray),
    new((false, true), ConsoleColor.White),
    new((false, false), ConsoleColor.Black));

var cluesColors = FrozenDictionary.Create<int, ConsoleColor>(null,
    new(1, ConsoleColor.Blue),
    new(2, ConsoleColor.Green),
    new(3, ConsoleColor.Red),
    new(4, ConsoleColor.DarkBlue),
    new(5, ConsoleColor.DarkRed),
    new(6, ConsoleColor.Cyan),
    new(7, ConsoleColor.Yellow),
    new(8, ConsoleColor.Magenta));

var game = new Game(AnsiConsole.Ask<int?>("Game seed :", null));
Console.CursorVisible = false;
var cts = new CancellationTokenSource();
var timer = Task.Run(async () =>
{
    var start = DateTime.Now;
    var timer = new PeriodicTimer(TimeSpan.FromSeconds(.5));
    do
        lock (cts)
        {
            Console.CursorVisible = false;
            var (left, top) = Console.GetCursorPosition();
            Console.SetCursorPosition(0, 0);
            Console.WriteAtEnd((DateTime.Now - start) is { TotalMinutes: var mins, Seconds: var sec } ? $"{mins:00}:{sec:00}" : "00:00");
            Console.SetCursorPosition(left, top);
            Console.CursorVisible = true;
        }
    while (await timer.WaitForNextTickAsync(cts.Token));
});
Console.CancelKeyPress += (s, e) => Exit();

var cursor = new Pos(Chunk.Size - 1, Chunk.Size - 1) / 2;
(int up, int down) offsets = (1, 1);
Console.Clear();
while (true)
{
    Draw();

    if (!Update(game, ref cursor))
        break;
}
Exit();

void Draw()
{
    lock (cts)
    {
        Pos viewport = new(Console.WindowWidth, Console.WindowHeight - (offsets.up + offsets.down));
        Pos center = viewport / 2;
        Console.SetCursorPosition(0, 0);
        Console.Write($"Cell: {game.GetCell(cursor, ChunkState.NotGenerated).ToColoredString(cluesColors)}   ");
        Console.WriteCentered($"   Chunk: {game.GetChunk(cursor.ToChunkPos(out _), ChunkState.NotGenerated).ToColoredString()}   ");
        Console.SetCursorPosition(0, offsets.up);
        Console.CursorVisible = false;

        for (int y = offsets.up; y < viewport.Y; y++)
        {
            for (int x = 0; x < viewport.X; x++)
            {
                var cellPos = new Pos(x, y) - center + cursor;
                ref var cell = ref game.GetCell(cellPos, ChunkState.NotGenerated);
                if (cell.IsUnexplored)
                {
                    (var back, Console.BackgroundColor) = (Console.BackgroundColor, bgColors[(cellPos.ToChunkPos(out var posInChunk).IsEven, posInChunk.IsEven)]);
                    if (cell.IsFlagged)
                        Console.Write('?', ConsoleColor.Red);
                    else
                        Console.Write(' ');
                    Console.BackgroundColor = back;
                }
                else
                {
                    switch (cell)
                    {
                        case { IsMine: true }:
                            Console.Write('*');
                            break;
                        case { MinesAround: > 0 and var mines }:
                            Console.Write(mines, cluesColors[mines]);
                            break;
                        default:
                            Console.Write(' ');
                            break;
                    }
                }
            }
            Console.WriteLine();
        }
        Console.WriteCentered($"Seed: {Console.WithItalic(game.Seed)}");
        Console.SetCursorPosition(center.X, center.Y);
        Console.CursorVisible = true;
    }
}

bool Update(Game game, ref Pos cursor)
{
    switch (Console.ReadKey(intercept: true))
    {
        case { Key: ConsoleKey.UpArrow }:
            cursor = cursor.North;
            break;
        case { Key: ConsoleKey.DownArrow }:
            cursor = cursor.South;
            break;
        case { Key: ConsoleKey.LeftArrow }:
            cursor = cursor.West;
            break;
        case { Key: ConsoleKey.RightArrow }:
            cursor = cursor.East;
            break;
        case { Key: ConsoleKey.Spacebar }:
            try
            {
                game.Explore(cursor);
            }
            catch (ExplodeException)
            {
                Draw();
                return false;
            }
            break;
        case { Key: ConsoleKey.Enter }:
            game.ToggleFlag(cursor);
            break;
        case { Key: ConsoleKey.S }:
            var json = JsonSerializer.Serialize(game, new JsonSerializerOptions(){ Converters = { new PosJsonConverter(), new Array2DJsonConverter<Cell>() } });
            File.WriteAllText("save.json", json);
            var g = JsonSerializer.Deserialize<Game>(json, new JsonSerializerOptions() { Converters = { new PosJsonConverter(), new Array2DJsonConverter<Cell>() }, IncludeFields = true, PropertyNameCaseInsensitive = false })!;
            break;
    }
    return true;
}

void Exit()
{
    cts.Cancel();
    Console.ResetColor();
    Console.CursorVisible = true;
};

file static class Ext
{
    extension(Pos pos)
    {
        public bool IsEven
        => (pos.X + pos.Y) % 2 == 0;

        public string ToColoredString()
        => $$"""{ X: {{Console.WithForeground(ConsoleColor.Red, pos.X)}}, Y: {{Console.WithForeground(ConsoleColor.Green, pos.Y)}} }""";
    }

    extension(Console)
    {
        public static void Write(int c, ConsoleColor foreground)
        {
            (var fore, Console.ForegroundColor) = (Console.ForegroundColor, foreground);
            Console.Write(c);
            Console.ForegroundColor = fore;
        }

        public static void Write(char c, ConsoleColor foreground)
        {
            (var fore, Console.ForegroundColor) = (Console.ForegroundColor, foreground);
            Console.Write(c);
            Console.ForegroundColor = fore;
        }

        public static void WriteAtEnd(string text, int rightPadding = 0)
        {
            Console.CursorLeft = Console.WindowWidth - text.LengthWithoutEscape - rightPadding;
            Console.Write(text);
        }

        public static void WriteCentered(string text)
        {
            Console.CursorLeft = (Console.WindowWidth - text.LengthWithoutEscape) / 2;
            Console.Write(text);
        }

        public static string WithForeground(ConsoleColor color, char c)
        => $"{color.ToEscapedForegroundColor}{c}{((ConsoleColor)(-1)).ToEscapedForegroundColor}";

        public static string WithForeground(ConsoleColor color, int i)
        => $"{color.ToEscapedForegroundColor}{i}{((ConsoleColor)(-1)).ToEscapedForegroundColor}";

        public static string WithUnderline(int i)
        => $"\e[4m{i}\e[24m";

        public static string WithItalic(int i)
        => $"\e[3m{i}\e[23m";
    }

    extension(ReadOnlySpan<char> s)
    {
        public int LengthWithoutEscape
        {
            get
            {
                var totalLength = s.Length;
                var removed = 0;
                while (s.IndexOf('\e') is > 0 and var e)
                {
                    s = s[e..];
                    var length = s.IndexOf('m') + 1;
                    removed += length;
                    s = s[length..];
                }
                return totalLength - removed;
            }
        }
    }

    extension(ref readonly Cell c)
    {
        public string ToColoredString(IReadOnlyDictionary<int, ConsoleColor> cluesColor)
        {
            return c switch
            {
                { IsUnexplored: true } => $$"""Pos: {{c.PosInChunk.ToCellPos(c.ChunkPos).ToColoredString()}}, Flag: {{(c.IsFlagged ? Console.WithForeground(ConsoleColor.Blue, 'Y') : "N")}}""",
                { IsMine: true } => $$"""Pos: {{c.PosInChunk.ToCellPos(c.ChunkPos).ToColoredString()}}, Mine: {{Console.WithForeground(ConsoleColor.Red, 'Y')}}""",
                { MinesAround: > 0 and var mines } => $$"""Pos: {{c.PosInChunk.ToCellPos(c.ChunkPos).ToColoredString()}}, Mines: {{Console.WithForeground(cluesColor[mines], mines)}}""",
                { } => $$"""Pos: {{c.PosInChunk.ToCellPos(c.ChunkPos).ToColoredString()}}, Mines: 0""",
            };
        }
    }

    extension(Chunk chunk)
    {
        public string ToColoredString()
        => $$"""Pos: {{chunk.Pos.ToColoredString()}}, Mines: {{chunk.RemainingMines}}""";
    }

    extension(ConsoleColor color)
    {
        public string ToEscapedForegroundColor => $"\e[{color.FGColor}m";

        public int FGColor => color switch
        {
            ConsoleColor.Black => 30,
            ConsoleColor.DarkBlue => 34,
            ConsoleColor.DarkGreen => 32,
            ConsoleColor.DarkCyan => 36,
            ConsoleColor.DarkRed => 31,
            ConsoleColor.DarkMagenta => 35,
            ConsoleColor.DarkYellow => 33,
            ConsoleColor.Gray => 37,
            ConsoleColor.DarkGray => 90,
            ConsoleColor.Blue => 94,
            ConsoleColor.Green => 92,
            ConsoleColor.Cyan => 96,
            ConsoleColor.Red => 91,
            ConsoleColor.Magenta => 95,
            ConsoleColor.Yellow => 93,
            ConsoleColor.White => 97,
            (ConsoleColor)(-1) => 39,
            _ => throw new ArgumentOutOfRangeException(nameof(color), color, null),
        };

        public int BGColor => color.FGColor + 10;
    }
}

file sealed class PosJsonConverter : JsonConverter<Pos>
{
    public override Pos ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return base.ReadAsPropertyName(ref reader, typeToConvert, options);
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
