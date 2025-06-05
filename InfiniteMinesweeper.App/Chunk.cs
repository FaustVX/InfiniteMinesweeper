using ZLinq;

namespace InfiniteMinesweeper;

public class Chunk(Pos pos)
{
    public const int Size = 8;
    private Cell _defaultCell = default(Cell) with { IsUnexplored = true, ChunkPos = pos };
    public Pos Pos { get; } = pos;
    public virtual ChunkState State => ChunkState.NotGenerated;
    public virtual ref Cell this[Pos pos]
    {
        get => ref _defaultCell;
    }
}

public sealed class ChunkWithMines(Pos pos, Game game) : Chunk(pos)
{
    private readonly Cell[,] _cells = GenerateCells(game, pos);
    public override ChunkState State => ChunkState.MineGenerated;
    public override ref Cell this[Pos pos]
    {
        get => ref _cells[pos.X, pos.Y];
    }

    static Cell[,] GenerateCells(Game game, Pos pos)
    {
        var cells = new Cell[Size, Size];
        for (int i = 0; i < Size; i++)
            for (int j = 0; j < Size; j++)
                cells[i, j] = new Cell(0, new(i, j), pos, false, false, IsUnexplored: true);
        var rng = new Random(game.Seed + pos.GetHashCode());
        for (int i = 0; i < game.MinesPerChunk; i++)
        {
Loop:
            var mine = rng.NextPos();
            ref var cell = ref cells[mine.X, mine.Y];
            if (cell.IsMine)
                goto Loop;
            cell = cell with { IsMine = true };
        }
        return cells;
    }
}

public sealed class ChunkGenerated(Pos pos, Game game) : Chunk(pos)
{
    private readonly Cell[,] _cells = GenerateCells(game, pos);
    public override ChunkState State => ChunkState.FullyGenerated;
    public override ref Cell this[Pos pos]
    {
        get => ref _cells[pos.X, pos.Y];
    }

    static Cell[,] GenerateCells(Game game, Pos pos)
    {
        ReadOnlySpan<Pos> neighborCells =
        [
            pos.NorthWest, pos.North, pos.NorthEast,
            pos.West,      pos,       pos.East,
            pos.SouthWest, pos.South, pos.SouthEast,
        ];

        foreach (var p in neighborCells)
            game.GetChunk(p, ChunkState.MineGenerated);

        var cells = new Cell[Size, Size];
        for (int i = 0; i < Size; i++)
            for (int j = 0; j < Size; j++)
            {
                Pos cellPos = new(i, j);
                cells[i, j] = game.GetCell(cellPos.ToCellPos(pos), ChunkState.MineGenerated) with
                {
                    MinesAround = MinesAround(game, cellPos.ToCellPos(pos)),
                    IsUnexplored = true
                };
            }

        return cells;

        static int MinesAround(Game game, Pos cellPos)
        {
            ReadOnlySpan<Pos> neighborCells =
            [
                cellPos.NorthWest, cellPos.North, cellPos.NorthEast,
                cellPos.West,                     cellPos.East,
                cellPos.SouthWest, cellPos.South, cellPos.SouthEast,
            ];

            return neighborCells.AsValueEnumerable()
                .Count(p => game.GetCell(p, ChunkState.MineGenerated).IsMine);
        }
    }
}

public enum ChunkState
{
    NotGenerated,
    MineGenerated,
    FullyGenerated,
}

public readonly record struct Cell(int MinesAround, Pos PosInChunk, Pos ChunkPos, bool IsMine, bool IsFlagged, bool IsUnexplored)
{
    public override string ToString()
    {
        return this switch
        {
            { IsUnexplored: true } => $$"""{ Pos: {{PosInChunk.ToCellPos(ChunkPos)}}, Flag: {{(IsFlagged ? 'Y' : 'N')}} }""",
            { IsMine: true } => $$"""{ Pos: {{PosInChunk.ToCellPos(ChunkPos)}}, Mine: Y }""",
            { MinesAround: var mines } => $$"""{ Pos: {{PosInChunk.ToCellPos(ChunkPos)}}, Mines: {{mines}} }""",
        };
    }
}

file static class Ext
{
    extension(Random rng)
    {
        public Pos NextPos()
        {
            int x = rng.Next(Chunk.Size);
            int y = rng.Next(Chunk.Size);
            return new Pos(x, y);
        }
    }
}
