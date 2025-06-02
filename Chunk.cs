namespace InfiniteMinesweeper;

public class Chunk(Pos pos)
{
    public const int Size = 8;
    public Pos Pos { get; } = pos;
    public virtual ChunkState State => ChunkState.NotGenerated;
}

public sealed class ChunkWithMines(Pos pos) : Chunk(pos)
{
    private readonly Cell[,] _cells = new Cell[Size, Size];
    public override ChunkState State => ChunkState.MineGenerated;
}

public sealed class ChunkGenerated(Pos pos) : Chunk(pos)
{
    private readonly Cell[,] _cells = new Cell[Size, Size];
    public override ChunkState State => ChunkState.FullyGenerated;
}

public enum ChunkState
{
    NotGenerated,
    MineGenerated,
    FullyGenerated,
}

public readonly record struct Pos(int X, int Y)
{
    public static Pos operator +(Pos a, Pos b) => new(a.X + b.X, a.Y + b.Y);
    public static Pos operator -(Pos a, Pos b) => new(a.X - b.X, a.Y - b.Y);
}

public readonly record struct Cell(int MinesAround, Pos PosInChunk, Pos ChunkPos, bool IsMine, bool IsFlagged, bool IsVisible);
