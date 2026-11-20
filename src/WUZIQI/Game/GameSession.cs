namespace WUZIQI.Game
{
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
        public bool ForbiddenRulesEnabled { get; private set; } = true;
        public bool Over => Winner != 0 || history.Count == Size * Size;
        public ForbiddenMove LastForbiddenMove { get; private set; }
        public int this[int row, int column] => board[row, column];

        public int[,] Snapshot()
        {
            return (int[,])board.Clone();
        }

        public void Start(int human, bool forbiddenRulesEnabled = true)
        {
            Array.Clear(board);
            history.Clear();
            winning.Clear();
            Human = human;
            ForbiddenRulesEnabled = forbiddenRulesEnabled;
            Current = 1;
            Winner = 0;
            LastForbiddenMove = ForbiddenMove.None;
            Started = true;
        }

        public bool Place(int row, int column)
        {
            LastForbiddenMove = ForbiddenMove.None;

            if (!Started || Over || row < 0 || row >= Size
                || column < 0 || column >= Size || board[row, column] != 0)
            {
                return false;
            }

            if (Current == 1 && ForbiddenRulesEnabled)
            {
                LastForbiddenMove = RenjuRules.GetForbiddenMove(board, row, column);

                if (LastForbiddenMove != ForbiddenMove.None)
                {
                    return false;
                }
            }

            board[row, column] = Current;
            history.Add(new Move(row, column, Current));

            foreach (var (dr, dc) in new[] { (0, 1), (1, 0), (1, 1), (1, -1) })
            {
                var line = new List<(int, int)> { (row, column) };

                foreach (var sign in new[] { -1, 1 })
                {
                    var r = row + dr * sign;
                    var c = column + dc * sign;

                    while (r >= 0 && r < Size && c >= 0 && c < Size && board[r, c] == Current)
                    {
                        line.Add((r, c));
                        r += dr * sign;
                        c += dc * sign;
                    }
                }

                if (Current == 1 && ForbiddenRulesEnabled ? line.Count == 5 : line.Count >= 5)
                {
                    Winner = Current;
                    winning.UnionWith(line);
                    break;
                }
            }

            if (!Over)
            {
                Current = 3 - Current;
                FinishIfNoLegalMoves();
            }

            return true;
        }

        public bool FinishIfNoLegalMoves()
        {
            if (!Started || Over || Current != 1 || !ForbiddenRulesEnabled)
            {
                return false;
            }

            var rules = RenjuRules.CreateBlackMoveEvaluator(board);

            for (var row = 0; row < Size; row++)
            {
                for (var column = 0; column < Size; column++)
                {
                    if (board[row, column] == 0
                        && rules.GetForbiddenMove(row, column) == ForbiddenMove.None)
                    {
                        return false;
                    }
                }
            }

            Winner = 2;
            return true;
        }

        public bool Undo()
        {
            if (history.Count == 0 || (Human == 2 && history.Count == 1))
            {
                return false;
            }

            do
            {
                var last = history[^1];
                history.RemoveAt(history.Count - 1);
                board[last.Row, last.Column] = 0;

                if (last.Player == Human)
                {
                    break;
                }
            }
            while (history.Count > 0);

            Winner = 0;
            winning.Clear();
            LastForbiddenMove = ForbiddenMove.None;
            Current = Human;
            return true;
        }
    }
}
