namespace WUZIQI.Game
{
    public enum ForbiddenMove
    {
        None,
        Overline,
        DoubleFour,
        DoubleThree
    }

    public static class RenjuRules
    {
        private const int Size = 15;

        private static readonly (int Row, int Column)[] Directions =
        [
            (0, 1), (1, 0), (1, 1), (1, -1)
        ];

        private static readonly ulong[,] BlackKeys = CreateKeys();

        public static BlackMoveEvaluator CreateBlackMoveEvaluator(
            int[,] board,
            CancellationToken cancellation = default)
        {
            return new BlackMoveEvaluator(board, cancellation);
        }

        public static ForbiddenMove GetForbiddenMove(
            int[,] board,
            int row,
            int column,
            CancellationToken cancellation = default)
        {
            return CreateBlackMoveEvaluator(board, cancellation).GetForbiddenMove(row, column);
        }

        public static bool IsLegalMove(
            int[,] board,
            int row,
            int column,
            int player,
            CancellationToken cancellation = default)
        {
            if (!Inside(row, column) || board[row, column] != 0 || player is not (1 or 2))
            {
                return false;
            }

            return player == 2 || GetForbiddenMove(board, row, column, cancellation) == ForbiddenMove.None;
        }

        public static string GetMessage(ForbiddenMove reason)
        {
            return reason switch
            {
                ForbiddenMove.Overline => "长连禁手：黑棋不能连成六子或更多，请选择其他位置。",
                ForbiddenMove.DoubleFour => "四四禁手：黑棋不能同时形成两个四，请选择其他位置。",
                ForbiddenMove.DoubleThree => "三三禁手：黑棋不能同时形成两个活三，请选择其他位置。",
                _ => string.Empty
            };
        }

        private static bool Inside(int row, int column)
        {
            return (uint)row < Size && (uint)column < Size;
        }

        private static ulong[,] CreateKeys()
        {
            var random = new Random(20261117);
            var keys = new ulong[Size, Size];
            var bytes = new byte[8];

            for (var row = 0; row < Size; row++)
            {
                for (var column = 0; column < Size; column++)
                {
                    random.NextBytes(bytes);
                    keys[row, column] = BitConverter.ToUInt64(bytes);
                }
            }

            return keys;
        }

        // 仅在当前棋盘保持不变期间复用；内部试放都在 finally 中撤销。
        public sealed class BlackMoveEvaluator
        {
            private readonly int[,] board;
            private readonly CancellationToken cancellation;
            private readonly Dictionary<(ulong Position, int Point), ForbiddenMove> cache = new();
            private ulong hash;

            internal BlackMoveEvaluator(int[,] board, CancellationToken cancellation)
            {
                this.board = board;
                this.cancellation = cancellation;

                for (var row = 0; row < Size; row++)
                {
                    for (var column = 0; column < Size; column++)
                    {
                        if (board[row, column] == 1)
                        {
                            hash ^= BlackKeys[row, column];
                        }
                    }
                }
            }

            public ForbiddenMove GetForbiddenMove(int row, int column)
            {
                cancellation.ThrowIfCancellationRequested();

                if (!Inside(row, column) || board[row, column] != 0)
                {
                    throw new ArgumentException("禁手判定只能检查棋盘内的空位。");
                }

                Put(row, column);

                try
                {
                    return CheckPlaced(row, column);
                }
                finally
                {
                    Remove(row, column);
                }
            }

            private ForbiddenMove CheckPlaced(int row, int column)
            {
                cancellation.ThrowIfCancellationRequested();
                var key = (hash, row * Size + column);

                if (cache.TryGetValue(key, out var known))
                {
                    return known;
                }

                ForbiddenMove result;

                // 本应用采用长连始终禁落；恰好五连优先于三三、四四。
                if (HasLength(row, column, true))
                {
                    result = ForbiddenMove.Overline;
                }
                else if (HasLength(row, column, false))
                {
                    result = ForbiddenMove.None;
                }
                else if (CountFours(row, column) >= 2)
                {
                    result = ForbiddenMove.DoubleFour;
                }
                else if (CountOpenThrees(row, column) >= 2)
                {
                    result = ForbiddenMove.DoubleThree;
                }
                else
                {
                    result = ForbiddenMove.None;
                }

                cache[key] = result;
                return result;
            }

            private int CountFours(int row, int column)
            {
                var patterns = new HashSet<int>();

                for (var direction = 0; direction < Directions.Length; direction++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var (dr, dc) = Directions[direction];

                    for (var start = -4; start <= 0; start++)
                    {
                        if (!Inside(row + dr * start, column + dc * start)
                            || !Inside(row + dr * (start + 4), column + dc * (start + 4)))
                        {
                            continue;
                        }

                        var mask = 0;
                        var blacks = 0;
                        var emptyOffset = 0;
                        var blocked = false;

                        for (var offset = start; offset <= start + 4; offset++)
                        {
                            var value = board[row + dr * offset, column + dc * offset];

                            if (value == 2)
                            {
                                blocked = true;
                                break;
                            }

                            if (value == 1)
                            {
                                blacks++;
                                mask |= 1 << (offset + 4);
                            }
                            else
                            {
                                emptyOffset = offset;
                            }
                        }

                        if (blocked || blacks != 4)
                        {
                            continue;
                        }

                        var r = row + dr * emptyOffset;
                        var c = column + dc * emptyOffset;

                        if (IsLegalFiveCompletion(r, c))
                        {
                            // 同一组四子两端成五只算一个四；同方向不同四子分别计数。
                            patterns.Add((direction << 12) | mask);

                            if (patterns.Count >= 2)
                            {
                                return patterns.Count;
                            }
                        }
                    }
                }

                return patterns.Count;
            }

            private int CountOpenThrees(int row, int column)
            {
                var patterns = new HashSet<int>();

                for (var direction = 0; direction < Directions.Length; direction++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var (dr, dc) = Directions[direction];

                    for (var start = -3; start <= 0; start++)
                    {
                        if (!Empty(row + dr * (start - 1), column + dc * (start - 1))
                            || !Empty(row + dr * (start + 4), column + dc * (start + 4)))
                        {
                            continue;
                        }

                        var mask = 0;
                        var blacks = 0;
                        var emptyOffset = 0;
                        var blocked = false;

                        for (var offset = start; offset < start + 4; offset++)
                        {
                            var value = board[row + dr * offset, column + dc * offset];

                            if (value == 2)
                            {
                                blocked = true;
                                break;
                            }

                            if (value == 1)
                            {
                                blacks++;
                                mask |= 1 << (offset + 4);
                            }
                            else
                            {
                                emptyOffset = offset;
                            }
                        }

                        var pattern = (direction << 12) | mask;

                        if (blocked || blacks != 3 || patterns.Contains(pattern))
                        {
                            continue;
                        }

                        var r = row + dr * emptyOffset;
                        var c = column + dc * emptyOffset;
                        Put(r, c);

                        try
                        {
                            // 活三必须能合法变为两端均可成五的活四；递归排除假活三。
                            if (HasLength(r, c, true) || HasLength(r, c, false)
                                || !IsLegalFiveCompletion(
                                    row + dr * (start - 1), column + dc * (start - 1))
                                || !IsLegalFiveCompletion(
                                    row + dr * (start + 4), column + dc * (start + 4))
                                || CheckPlaced(r, c) != ForbiddenMove.None)
                            {
                                continue;
                            }

                            patterns.Add(pattern);

                            if (patterns.Count >= 2)
                            {
                                return patterns.Count;
                            }
                        }
                        finally
                        {
                            Remove(r, c);
                        }
                    }
                }

                return patterns.Count;
            }

            private bool IsLegalFiveCompletion(int row, int column)
            {
                if (!Empty(row, column))
                {
                    return false;
                }

                Put(row, column);

                try
                {
                    return !HasLength(row, column, true) && HasLength(row, column, false);
                }
                finally
                {
                    Remove(row, column);
                }
            }

            private bool HasLength(int row, int column, bool overline)
            {
                foreach (var (dr, dc) in Directions)
                {
                    var length = 1 + Count(row, column, dr, dc) + Count(row, column, -dr, -dc);

                    if (overline ? length >= 6 : length == 5)
                    {
                        return true;
                    }
                }

                return false;
            }

            private int Count(int row, int column, int dr, int dc)
            {
                var result = 0;
                row += dr;
                column += dc;

                while (Inside(row, column) && board[row, column] == 1)
                {
                    result++;
                    row += dr;
                    column += dc;
                }

                return result;
            }

            private bool Empty(int row, int column)
            {
                return Inside(row, column) && board[row, column] == 0;
            }

            private void Put(int row, int column)
            {
                board[row, column] = 1;
                hash ^= BlackKeys[row, column];
            }

            private void Remove(int row, int column)
            {
                board[row, column] = 0;
                hash ^= BlackKeys[row, column];
            }
        }
    }
}
