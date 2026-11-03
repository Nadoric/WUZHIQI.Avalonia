namespace WUZHIQI.Game;
public sealed record Move(int Row, int Column, int Player);
public sealed class GameSession
{
    public const int Size = 15;
    private readonly int[,] board = new int[Size, Size];
    private readonly List<Move> history = [];
    private readonly HashSet<(int, int)> winning = [];
    public IReadOnlyList<Move> History => history;
    public IReadOnlySet<(int, int)> Winning => winning;
    public int Human { get; private set; } = 1;
    public int Computer => 3 - Human;
    public int Current { get; private set; } = 1;
    public int Winner { get; private set; }
    public bool Started { get; private set; }
    public bool Over => Winner != 0 || history.Count == Size * Size;
    public int this[int row, int column] => board[row, column];
    public int[,] Snapshot() => (int[,])board.Clone();
    public void Start(int human)
    {
        Array.Clear(board); history.Clear(); winning.Clear();
        Human = human; Current = 1; Winner = 0; Started = true;
    }
    public bool Place(int row, int column)
    {
        if (!Started || Over || row < 0 || row >= Size || column < 0 || column >= Size || board[row, column] != 0) return false;
        board[row, column] = Current; history.Add(new(row, column, Current));
        foreach (var (dr, dc) in new[] { (0, 1), (1, 0), (1, 1), (1, -1) })
        {
            var line = new List<(int,int)> { (row, column) };
            foreach (var sign in new[] { -1, 1 })
                for (int r = row + dr * sign, c = column + dc * sign; r >= 0 && r < Size && c >= 0 && c < Size && board[r,c] == Current; r += dr * sign, c += dc * sign)
                    line.Add((r,c));
            if (line.Count >= 5) { Winner = Current; winning.UnionWith(line); break; }
        }
        if (!Over) Current = 3 - Current;
        return true;
    }
    public bool Undo()
    {
        // White cannot undo the mandatory black opening by itself.
        if (history.Count == 0 || (Human == 2 && history.Count == 1)) return false;
        do {
            var last = history[^1]; history.RemoveAt(history.Count - 1); board[last.Row,last.Column] = 0;
            if (last.Player == Human) break;
        } while (history.Count > 0);
        Winner = 0; winning.Clear(); Current = Human; return true;
    }
}
