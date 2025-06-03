namespace InfiniteMinesweeper;

public class Chunk(Pos pos)
{
    public const int Size = 8;
    public Pos Pos { get; } = pos;
    public virtual ChunkState State => ChunkState.NotGenerated;
    public virtual Cell this[Pos pos]
    {
        get => default;
        set { }
    }
}

public sealed class ChunkWithMines(Pos pos) : Chunk(pos)
{
    private readonly Cell[,] _cells = new Cell[Size, Size];
    public override ChunkState State => ChunkState.MineGenerated;
    public override Cell this[Pos pos]
    {
        get => _cells[pos.X, pos.Y];
        set => _cells[pos.X, pos.Y] = value;
    }
}

public sealed class ChunkGenerated(Pos pos) : Chunk(pos)
{
    private readonly Cell[,] _cells = new Cell[Size, Size];
    public override ChunkState State => ChunkState.FullyGenerated;
    public override Cell this[Pos pos]
    {
        get => _cells[pos.X, pos.Y];
        set => _cells[pos.X, pos.Y] = value;
    }
}

public enum ChunkState
{
    NotGenerated,
    MineGenerated,
    FullyGenerated,
}

public readonly record struct Pos(int X, int Y)
{
    public static Pos operator +(Pos a, Pos b)
    => new(a.X + b.X, a.Y + b.Y);
    public static Pos operator -(Pos a, Pos b)
    => new(a.X - b.X, a.Y - b.Y);
    public static Pos operator *(Pos a, int scalar)
    => new(a.X * scalar, a.Y * scalar);
    public static Pos operator /(Pos a, int scalar)
    => new(a.X / scalar, a.Y / scalar);
    public static Pos operator %(Pos a, int scalar)
    => new(a.X % scalar, a.Y % scalar);

    public Pos ToPosInChunk(out Pos chunkPos)
    {
        chunkPos = this / Chunk.Size;
        return this % Chunk.Size;
    }

    public Pos ToCellPos(Pos chunkPos)
    => chunkPos * Chunk.Size + this;
}

public readonly record struct Cell(int MinesAround, Pos PosInChunk, Pos ChunkPos, bool IsMine, bool IsFlagged, bool IsVisible);
