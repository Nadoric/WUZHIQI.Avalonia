using System.Diagnostics;

namespace WUZHIQI.Game
{
    public enum Difficulty
    {
        Easy,
        Normal,
        Hard,
        Expert,
        Master,
        Hell,
        Inferno
    }

    public sealed class SearchEngine
    {
        private const int Size = 15;
        private const int WinScore = 10_000_000;
        private const int TableLimit = 40_000;
        private const int AssistanceStart = 180;
        private const int StrongAssistanceStart = 203;

        private static readonly (int Row, int Column)[] Directions =
        [
            (0, 1), (1, 0), (1, 1), (1, -1)
        ];

        private static readonly ulong[,,] PositionKeys = CreatePositionKeys();
        private readonly int[,] board;
        private readonly int[,] neighbors = new int[Size, Size];
        private readonly int[] rowScores = new int[Size];
        private readonly int[] columnScores = new int[Size];
        private readonly int[] diagonalScores = new int[Size * 2 - 1];
        private readonly int[] antiDiagonalScores = new int[Size * 2 - 1];
        private readonly Dictionary<ulong, TableEntry> table = new();
        private readonly Stopwatch clock = new();
        private readonly CancellationToken cancellation;
        private readonly int computer;
        private readonly Difficulty difficulty;
        private readonly Profile profile;
        private int evaluation;
        private int stones;
        private ulong positionKey;

        public int CompletedDepth { get; private set; }

        public int VisitedNodes { get; private set; }

        public int ThinkingBudgetMilliseconds => profile.Milliseconds;

        public bool IsLateGameAssistanceActive { get; }

        private bool StrongAssistance { get; }

        private sealed record Profile(int Depth, int RootLimit, int BranchLimit, int Milliseconds);

        private readonly record struct Candidate(int Row, int Column, int Attack, int Defense)
        {
            public long Priority => Attack >= 1_000_000
                ? 10_000_000_000L + Attack
                : Defense >= 1_000_000
                    ? 5_000_000_000L + Defense
                    : Attack * 14L + Defense * 10L;
        }

        private readonly record struct TableEntry(int Depth, int Score, int Bound);

        private sealed class BudgetExceededException : Exception
        {
        }

        public SearchEngine(
            int[,] snapshot,
            int computer,
            Difficulty difficulty,
            CancellationToken cancellation = default)
        {
            board = (int[,])snapshot.Clone();
            this.computer = computer;
            this.difficulty = difficulty;
            this.cancellation = cancellation;
            profile = difficulty switch
            {
                Difficulty.Hard => new Profile(2, 8, 6, 150),
                Difficulty.Expert => new Profile(3, 10, 7, 300),
                Difficulty.Master => new Profile(4, 12, 7, 500),
                Difficulty.Hell => new Profile(5, 12, 8, 700),
                Difficulty.Inferno => new Profile(6, 14, 8, 900),
                _ => new Profile(0, 0, 0, 0)
            };

            for (var row = 0; row < Size; row++)
            {
                for (var column = 0; column < Size; column++)
                {
                    var player = board[row, column];

                    if (player != 0)
                    {
                        stones++;
                        positionKey ^= PositionKeys[row, column, player - 1];
                        UpdateNeighbors(row, column, 1);
                    }

                    RefreshLines(row, column);
                }
            }

            // 阶段由真实棋盘决定，搜索中的模拟落子不能触发让步策略。
            IsLateGameAssistanceActive = stones >= AssistanceStart;
            StrongAssistance = stones >= StrongAssistanceStart;
        }

        public (int Row, int Column) FindComputerMove()
        {
            cancellation.ThrowIfCancellationRequested();
            CompletedDepth = 0;
            VisitedNodes = 0;
            table.Clear();
            clock.Restart();

            if (stones == 0)
            {
                return (7, 7);
            }

            // 先做完整的必胜、必防检查，不让搜索预算漏掉直接成五。
            var candidates = GenerateMoves(computer, false);

            if (candidates.Count == 0)
            {
                throw new InvalidOperationException("棋盘上没有可落子的位置。");
            }

            if (IsLateGameAssistanceActive)
            {
                var assisted = FindLateGameMove(candidates);
                return (assisted.Row, assisted.Column);
            }

            if (difficulty == Difficulty.Easy)
            {
                var random = candidates[Random.Shared.Next(candidates.Count)];
                return (random.Row, random.Column);
            }

            foreach (var move in candidates)
            {
                if (move.Attack >= 1_000_000)
                {
                    return (move.Row, move.Column);
                }
            }

            foreach (var move in candidates)
            {
                if (move.Defense >= 1_000_000)
                {
                    return (move.Row, move.Column);
                }
            }

            var best = candidates[0];

            if (profile.Depth == 0 || stones == 1)
            {
                return (best.Row, best.Column);
            }

            var winningFork = FindWinningFork(candidates);

            if (winningFork.HasValue)
            {
                return (winningFork.Value.Row, winningFork.Value.Column);
            }

            // 只提交已完整搜索的一层，预算用尽时保留上一层的可靠结果。
            try
            {
                for (var depth = 1; depth <= profile.Depth; depth++)
                {
                    var layerBest = best;
                    var layerScore = int.MinValue;
                    var alpha = -WinScore * 2;
                    var count = Math.Min(candidates.Count, profile.RootLimit);

                    for (var index = 0; index < count; index++)
                    {
                        CheckBudget();
                        var move = candidates[index];
                        Play(move.Row, move.Column, computer);
                        int score;

                        try
                        {
                            score = Search(depth - 1, 3 - computer, alpha, WinScore * 2);
                        }
                        finally
                        {
                            Undo(move.Row, move.Column, computer);
                        }

                        if (score > layerScore)
                        {
                            layerScore = score;
                            layerBest = move;
                        }

                        alpha = Math.Max(alpha, score);
                    }

                    best = layerBest;
                    CompletedDepth = depth;
                    candidates.Remove(best);
                    candidates.Insert(0, best);

                    if (layerScore >= WinScore - Size * Size)
                    {
                        break;
                    }
                }
            }
            catch (BudgetExceededException)
            {
                // 正常结束限时思考；外部取消仍由 OperationCanceledException 传递。
            }

            cancellation.ThrowIfCancellationRequested();
            return (best.Row, best.Column);
        }

        private Candidate? FindWinningFork(List<Candidate> candidates)
        {
            var limit = Math.Min(candidates.Count, profile.RootLimit);
            var tacticalBudget = Math.Min(120, profile.Milliseconds / 3);

            for (var index = 0; index < limit; index++)
            {
                cancellation.ThrowIfCancellationRequested();

                if (clock.ElapsedMilliseconds >= tacticalBudget)
                {
                    break;
                }

                var move = candidates[index];
                Play(move.Row, move.Column, computer);

                try
                {
                    var continuations = 0;

                    for (var row = 0; row < Size; row++)
                    {
                        cancellation.ThrowIfCancellationRequested();

                        for (var column = 0; column < Size; column++)
                        {
                            if (!IsEmpty(row, column) || neighbors[row, column] == 0)
                            {
                                continue;
                            }

                            foreach (var (dr, dc) in Directions)
                            {
                                if (1 + Count(row, column, dr, dc, computer)
                                    + Count(row, column, -dr, -dc, computer) >= 5)
                                {
                                    continuations++;
                                    break;
                                }
                            }

                            if (continuations >= 2)
                            {
                                // 一步制造两个成五点，优先兑现进攻，而非继续被动堵棋。
                                return move;
                            }
                        }
                    }
                }
                finally
                {
                    Undo(move.Row, move.Column, computer);
                }
            }

            return null;
        }

        private Candidate FindLateGameMove(List<Candidate> candidates)
        {
            var best = candidates[0];
            var bestScore = long.MinValue;
            var hasQuietMove = candidates.Exists(move => move.Attack < 1_000_000);

            foreach (var move in candidates)
            {
                cancellation.ThrowIfCancellationRequested();

                if (bestScore != long.MinValue && clock.ElapsedMilliseconds >= 150)
                {
                    break;
                }

                if (hasQuietMove && move.Attack >= 1_000_000)
                {
                    continue;
                }

                Play(move.Row, move.Column, computer);
                var playerOpportunity = 0;

                try
                {
                    for (var row = 0; row < Size; row++)
                    {
                        cancellation.ThrowIfCancellationRequested();

                        for (var column = 0; column < Size; column++)
                        {
                            if (IsEmpty(row, column))
                            {
                                playerOpportunity = Math.Max(
                                    playerOpportunity,
                                    MoveScore(row, column, 3 - computer));
                            }
                        }
                    }
                }
                finally
                {
                    Undo(move.Row, move.Column, computer);
                }

                // 最后约两成空位时保留玩家发展空间；最后约一成进一步放宽防守。
                // 只调整合法落子的选择，不改已有棋子、不改胜负规则。
                var score = StrongAssistance
                    ? playerOpportunity * 32L - move.Defense * 8L - move.Attack * 2L
                    : playerOpportunity * 16L - move.Defense * 4L + move.Attack;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = move;
                }
            }

            return best;
        }

        private int Search(int depth, int player, int alpha, int beta)
        {
            CheckBudget();
            VisitedNodes++;

            if (depth == 0 || stones == Size * Size)
            {
                return evaluation;
            }

            var key = player == 1 ? positionKey : ~positionKey;
            var originalAlpha = alpha;
            var originalBeta = beta;

            if (table.TryGetValue(key, out var cached) && cached.Depth >= depth)
            {
                if (cached.Bound == 0)
                {
                    return cached.Score;
                }

                if (cached.Bound > 0)
                {
                    alpha = Math.Max(alpha, cached.Score);
                }
                else
                {
                    beta = Math.Min(beta, cached.Score);
                }

                if (alpha >= beta)
                {
                    return cached.Score;
                }
            }

            var maximizing = player == computer;
            var moves = GenerateMoves(player, true);

            if (moves.Count == 0)
            {
                return 0;
            }

            if (moves[0].Attack >= 1_000_000)
            {
                return maximizing ? WinScore - stones : -WinScore + stones;
            }

            var best = maximizing ? -WinScore * 2 : WinScore * 2;
            var count = Math.Min(moves.Count, profile.BranchLimit);

            for (var index = 0; index < count; index++)
            {
                var move = moves[index];
                Play(move.Row, move.Column, player);
                int score;

                try
                {
                    score = Search(depth - 1, 3 - player, alpha, beta);
                }
                finally
                {
                    Undo(move.Row, move.Column, player);
                }

                if (maximizing)
                {
                    best = Math.Max(best, score);
                    alpha = Math.Max(alpha, best);
                }
                else
                {
                    best = Math.Min(best, score);
                    beta = Math.Min(beta, best);
                }

                if (alpha >= beta)
                {
                    break;
                }
            }

            if (table.Count < TableLimit)
            {
                var bound = best <= originalAlpha ? -1 : best >= originalBeta ? 1 : 0;
                table[key] = new TableEntry(depth, best, bound);
            }

            return best;
        }

        private List<Candidate> GenerateMoves(int player, bool limited)
        {
            var result = new List<Candidate>(64);

            for (var row = 0; row < Size; row++)
            {
                cancellation.ThrowIfCancellationRequested();

                if (limited)
                {
                    CheckBudget();
                }

                for (var column = 0; column < Size; column++)
                {
                    if (board[row, column] == 0 && neighbors[row, column] > 0)
                    {
                        result.Add(new Candidate(
                            row,
                            column,
                            MoveScore(row, column, player),
                            MoveScore(row, column, 3 - player)));
                    }
                }
            }

            result.Sort((left, right) => right.Priority.CompareTo(left.Priority));
            return result;
        }

        private int MoveScore(int row, int column, int player)
        {
            var score = 0;

            foreach (var (dr, dc) in Directions)
            {
                var forward = Count(row, column, dr, dc, player);
                var backward = Count(row, column, -dr, -dc, player);
                var open = IsEmpty(row + dr * (forward + 1), column + dc * (forward + 1)) ? 1 : 0;
                open += IsEmpty(row - dr * (backward + 1), column - dc * (backward + 1)) ? 1 : 0;
                score += PatternScore(forward + backward + 1, open);

                // 补上跳三、断四等带空位的进攻形状，减少只看连续棋子的保守落子。
                for (var offset = -4; offset <= 0; offset++)
                {
                    var startRow = row + dr * offset;
                    var startColumn = column + dc * offset;

                    if (!Inside(startRow, startColumn)
                        || !Inside(startRow + dr * 4, startColumn + dc * 4))
                    {
                        continue;
                    }

                    var own = 0;
                    var blocked = false;

                    for (var step = 0; step < 5; step++)
                    {
                        var r = startRow + dr * step;
                        var c = startColumn + dc * step;
                        var value = r == row && c == column ? player : board[r, c];

                        if (value == 3 - player)
                        {
                            blocked = true;
                            break;
                        }

                        if (value == player)
                        {
                            own++;
                        }
                    }

                    if (!blocked)
                    {
                        score += own switch
                        {
                            4 => 12_000,
                            3 => 400,
                            2 => 30,
                            _ => 0
                        };
                    }
                }
            }

            return score + (14 - Math.Abs(row - 7) - Math.Abs(column - 7)) * 3;
        }

        private int Count(int row, int column, int dr, int dc, int player)
        {
            var count = 0;
            row += dr;
            column += dc;

            while (Inside(row, column) && board[row, column] == player)
            {
                count++;
                row += dr;
                column += dc;
            }

            return count;
        }

        private static int PatternScore(int length, int openEnds)
        {
            if (length >= 5)
            {
                return 1_000_000;
            }

            if (openEnds == 0)
            {
                return 0;
            }

            return length switch
            {
                4 => openEnds == 2 ? 100_000 : 10_000,
                3 => openEnds == 2 ? 5_000 : 800,
                2 => openEnds == 2 ? 500 : 80,
                _ => 10
            };
        }

        private void Play(int row, int column, int player)
        {
            board[row, column] = player;
            stones++;
            positionKey ^= PositionKeys[row, column, player - 1];
            UpdateNeighbors(row, column, 1);
            RefreshLines(row, column);
        }

        private void Undo(int row, int column, int player)
        {
            board[row, column] = 0;
            stones--;
            positionKey ^= PositionKeys[row, column, player - 1];
            UpdateNeighbors(row, column, -1);
            RefreshLines(row, column);
        }

        private void UpdateNeighbors(int row, int column, int change)
        {
            for (var r = Math.Max(0, row - 2); r <= Math.Min(Size - 1, row + 2); r++)
            {
                for (var c = Math.Max(0, column - 2); c <= Math.Min(Size - 1, column + 2); c++)
                {
                    neighbors[r, c] += change;
                }
            }
        }

        private void RefreshLines(int row, int column)
        {
            // 每次落子、撤销只重算相交的四条线，叶节点直接读取缓存总分。
            ReplaceScore(rowScores, row, EvaluateLine(row, 0, 0, 1));
            ReplaceScore(columnScores, column, EvaluateLine(0, column, 1, 0));
            var difference = row - column;
            ReplaceScore(
                diagonalScores,
                difference + Size - 1,
                EvaluateLine(Math.Max(difference, 0), Math.Max(-difference, 0), 1, 1));
            var sum = row + column;
            ReplaceScore(
                antiDiagonalScores,
                sum,
                EvaluateLine(Math.Max(0, sum - Size + 1), Math.Min(Size - 1, sum), 1, -1));
        }

        private void ReplaceScore(int[] scores, int index, int value)
        {
            evaluation += value - scores[index];
            scores[index] = value;
        }

        private int EvaluateLine(int row, int column, int dr, int dc)
        {
            var total = 0;

            while (Inside(row, column))
            {
                var player = board[row, column];

                if (player == 0)
                {
                    row += dr;
                    column += dc;
                    continue;
                }

                var open = IsEmpty(row - dr, column - dc) ? 1 : 0;
                var length = 0;

                do
                {
                    length++;
                    row += dr;
                    column += dc;
                }
                while (Inside(row, column) && board[row, column] == player);

                open += IsEmpty(row, column) ? 1 : 0;
                var score = PatternScore(length, open);
                total += player == computer ? score : -score;
            }

            return total;
        }

        private void CheckBudget()
        {
            cancellation.ThrowIfCancellationRequested();

            if (clock.ElapsedMilliseconds >= profile.Milliseconds || VisitedNodes >= 40_000)
            {
                throw new BudgetExceededException();
            }
        }

        private bool IsEmpty(int row, int column)
        {
            return Inside(row, column) && board[row, column] == 0;
        }

        private static bool Inside(int row, int column)
        {
            return (uint)row < Size && (uint)column < Size;
        }

        private static ulong[,,] CreatePositionKeys()
        {
            var keys = new ulong[Size, Size, 2];
            var random = new Random(20261102);
            var bytes = new byte[8];

            for (var row = 0; row < Size; row++)
            {
                for (var column = 0; column < Size; column++)
                {
                    for (var player = 0; player < 2; player++)
                    {
                        random.NextBytes(bytes);
                        keys[row, column, player] = BitConverter.ToUInt64(bytes);
                    }
                }
            }

            return keys;
        }
    }
}
