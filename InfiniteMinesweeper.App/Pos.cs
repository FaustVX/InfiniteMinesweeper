namespace InfiniteMinesweeper;

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
    => new(
        ((a.X % scalar) + scalar) % scalar,
        ((a.Y % scalar) + scalar) % scalar
    );

    public Pos East
    => new(X + 1, Y);
    public Pos West
    => new(X - 1, Y);
    public Pos North
    => new(X, Y - 1);
    public Pos South
    => new(X, Y + 1);
    public Pos NorthEast
    => North.East;
    public Pos NorthWest
    => North.West;
    public Pos SouthEast
    => South.East;
    public Pos SouthWest
    => South.West;

    public Pos ToPosInChunk(out Pos chunkPos)
    {
        chunkPos = ToChunkPos(out var posInChunk);
        return posInChunk;
    }

    public Pos ToChunkPos(out Pos posInChunk)
    {
        posInChunk = (this % Chunk.Size + new Pos(Chunk.Size, Chunk.Size)) % Chunk.Size;
        return (this - posInChunk) / Chunk.Size;
    }

    public Pos ToCellPos(Pos chunkPos)
    => chunkPos * Chunk.Size + this;

    public override string ToString()
    => $$"""{ X: {{X}}, Y: {{Y}} }""";
}
