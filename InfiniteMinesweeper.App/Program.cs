using System.Collections.Frozen;
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
Console.CancelKeyPress += (s, e) => Exit();

var cursor = new Pos((Chunk.Size - 1) / 2, (Chunk.Size - 1) / 2);
(int up, int down) offsets = (1, 0);
while (true)
{
    Draw();

    if (!Update(game, ref cursor))
        break;
}
Exit();

void Draw()
{
    Pos viewport = new(Console.WindowWidth, Console.WindowHeight - (offsets.up + offsets.down));
    Pos center = viewport / 2;
    Console.Clear();
    Console.Write(game.GetCell(cursor, ChunkState.NotGenerated));
    Console.WriteAtEnd($"Seed: {game.Seed}", 1);
    Console.SetCursorPosition(0, offsets.up);

    for (int y = offsets.up; y < viewport.Y; y++)
    {
        for (int x = 0; x < viewport.X; x++)
        {
            var cellPos = new Pos(x, y) - center + cursor;
            ref var cell = ref game.GetCell(cellPos, ChunkState.NotGenerated);
            if (cell.IsUnexplored)
            {
                (var back, Console.BackgroundColor) = (Console.BackgroundColor, Console.BackgroundColor = bgColors[(cellPos.ToChunkPos(out var posInChunk).IsEven, posInChunk.IsEven)]);
                if (cell.IsFlagged)
                    Console.Write('?');
                else if (new Pos(x, y) == center)
                    Console.Write('_');
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
                        if (new Pos(x, y) == center)
                            Console.Write('_');
                        else
                            Console.Write(' ');
                        break;
                }
            }
        }
        Console.WriteLine();
    }
}

bool Update(Game game, ref Pos cursor)
{
    switch (Console.ReadKey(intercept: true).Key)
    {
        case ConsoleKey.UpArrow:
            cursor = cursor.North;
            break;
        case ConsoleKey.DownArrow:
            cursor = cursor.South;
            break;
        case ConsoleKey.LeftArrow:
            cursor = cursor.West;
            break;
        case ConsoleKey.RightArrow:
            cursor = cursor.East;
            break;
        case ConsoleKey.Spacebar:
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
        case ConsoleKey.Enter:
            game.ToggleFlag(cursor);
            break;
    }
    return true;
}

static void Exit()
{
    Console.ResetColor();
    Console.CursorVisible = true;
};

file static class Ext
{
    extension(Pos pos)
    {
        public bool IsEven
        => (pos.X + pos.Y) % 2 == 0;
    }

    extension(Console)
    {
        public static void Write(int c, ConsoleColor foreground)
        {
            (var fore, Console.ForegroundColor) = (Console.ForegroundColor, foreground);
            Console.Write(c);
            Console.ForegroundColor = fore;
        }

        public static void WriteAtEnd(string text, int rightPadding = 0)
        {
            Console.CursorLeft = Console.WindowWidth - text.Length - rightPadding;
            Console.Write(text);
        }
    }
}
