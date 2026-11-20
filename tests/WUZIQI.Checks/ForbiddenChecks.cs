using WUZIQI.Game;

namespace WUZIQI.Tests
{
    internal static class ForbiddenChecks
    {
        public static void Run()
        {
            var crossThree = Board((7, 6), (7, 8), (6, 7), (8, 7));
            var brokenThree = Board((7, 5), (7, 8), (5, 7), (8, 7));
            var crossFour = Board((7, 5), (7, 6), (7, 8), (5, 7), (6, 7), (8, 7));
            var sameLineFour = Board((7, 3), (7, 5), (7, 6), (7, 9));
            var overline = Board((7, 3), (7, 4), (7, 5), (7, 7), (7, 8));

            CheckSymmetry(crossThree, 7, 7, ForbiddenMove.DoubleThree);
            CheckSymmetry(brokenThree, 7, 7, ForbiddenMove.DoubleThree);
            CheckSymmetry(crossFour, 7, 7, ForbiddenMove.DoubleFour);
            CheckSymmetry(sameLineFour, 7, 7, ForbiddenMove.DoubleFour);
            CheckSymmetry(overline, 7, 6, ForbiddenMove.Overline);
            CheckReason(Board((7, 5), (7, 6), (7, 8)), 7, 7, ForbiddenMove.None);
            CheckReason(
                Board((7, 5), (7, 6), (7, 8), (6, 7), (8, 7)),
                7, 7, ForbiddenMove.None);

            var blockedThree = (int[,])crossThree.Clone();
            blockedThree[7, 4] = 2;
            blockedThree[7, 10] = 2;
            CheckReason(blockedThree, 7, 7, ForbiddenMove.None);
            CheckReason(Board((0, 6), (0, 8), (1, 7), (2, 7)), 0, 7, ForbiddenMove.None);

            // 延伸为活四后的成五端点会形成长连，所以横向不是有效活三。
            var overlineFalseThree = (int[,])crossThree.Clone();
            overlineFalseThree[7, 3] = 1;
            overlineFalseThree[7, 11] = 1;
            CheckReason(overlineFalseThree, 7, 7, ForbiddenMove.None);

            // 横向的两个延伸点各自都会形成四四；递归检查后只剩一个真活三。
            var forbiddenExtension = (int[,])crossThree.Clone();

            foreach (var column in new[] { 5, 9 })
            {
                forbiddenExtension[6, column] = 1;
                forbiddenExtension[8, column] = 1;
                forbiddenExtension[9, column] = 1;
            }

            CheckReason(forbiddenExtension, 7, 7, ForbiddenMove.None);
            var exactFive = Board(
                (7, 3), (7, 4), (7, 5), (7, 6),
                (4, 7), (5, 7), (6, 7), (4, 4), (5, 5), (6, 6));
            CheckReason(exactFive, 7, 7, ForbiddenMove.None);
            var fiveAndOverline = Board(
                (7, 2), (7, 3), (7, 4), (7, 5), (7, 6),
                (3, 7), (4, 7), (5, 7), (6, 7));
            CheckReason(fiveAndOverline, 7, 7, ForbiddenMove.Overline);

            foreach (var position in new[] { crossThree, crossFour, overline })
            {
                var whitePosition = new int[15, 15];

                for (var row = 0; row < 15; row++)
                {
                    for (var column = 0; column < 15; column++)
                    {
                        whitePosition[row, column] = position[row, column] == 1 ? 2 : 0;
                    }
                }

                var point = ReferenceEquals(position, overline) ? (7, 6) : (7, 7);
                Require(
                    RenjuRules.IsLegalMove(whitePosition, point.Item1, point.Item2, 2),
                    "White must not be subject to forbidden moves");
            }

            CheckSession(crossThree, (7, 7), ForbiddenMove.DoubleThree);
            CheckSession(crossFour, (7, 7), ForbiddenMove.DoubleFour);
            CheckSession(overline, (7, 6), ForbiddenMove.Overline);
            CheckDisabled(crossThree, (7, 7), false);
            CheckDisabled(crossFour, (7, 7), false);
            CheckDisabled(overline, (7, 6), true);


            foreach (var level in Enum.GetValues<Difficulty>())
            {
                foreach (var position in new[] { crossThree, crossFour, sameLineFour, overline })
                {
                    var original = (int[,])position.Clone();
                    var move = new SearchEngine(position, 1, level).FindComputerMove();
                    Require(move.Row >= 0, "Black AI missed an available legal move");
                    Require(
                        RenjuRules.IsLegalMove(position, move.Row, move.Column, 1),
                        $"Black AI chose forbidden move at {level}");
                    Require(position.Cast<int>().SequenceEqual(original.Cast<int>()), "AI mutated board");
                }
            }

            var noLegalMove = new int[15, 15];

            for (var row = 0; row < 15; row++)
            {
                for (var column = 0; column < 15; column++)
                {
                    noLegalMove[row, column] = 2;
                }
            }

            foreach (var column in new[] { 4, 5, 6, 8, 9 })
            {
                noLegalMove[7, column] = 1;
            }

            noLegalMove[7, 7] = 0;
            Require(
                new SearchEngine(noLegalMove, 1, Difficulty.Inferno).FindComputerMove() == (-1, -1),
                "AI must not play a forbidden last point");

            foreach (var level in Enum.GetValues<Difficulty>())
            {
                Require(
                    new SearchEngine(noLegalMove, 1, level, forbiddenRulesEnabled: false)
                        .FindComputerMove() == (7, 7),
                    "Disabled rules still excluded the black AI's overline");
            }

            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var restored = (int[,])crossThree.Clone();

            try
            {
                RenjuRules.GetForbiddenMove(restored, 7, 7, cancellation.Token);
                throw new Exception("Rule analysis ignored cancellation");
            }
            catch (OperationCanceledException)
            {
                Require(restored.Cast<int>().SequenceEqual(crossThree.Cast<int>()), "Cancelled rules mutated board");
            }

            Console.WriteLine(
                "PASS: forbidden patterns in 8 symmetries, false threes, exact five, "
                + "white exemption, rejection state, all 7 AI levels and no legal move");
        }

        private static int[,] Board(params (int Row, int Column)[] stones)
        {
            var result = new int[15, 15];

            foreach (var stone in stones)
            {
                result[stone.Row, stone.Column] = 1;
            }

            return result;
        }

        private static void CheckReason(int[,] board, int row, int column, ForbiddenMove expected)
        {
            var original = (int[,])board.Clone();
            var actual = RenjuRules.GetForbiddenMove(board, row, column);
            Require(actual == expected, $"Forbidden result: expected {expected}, got {actual}");
            Require(board.Cast<int>().SequenceEqual(original.Cast<int>()), "Rules mutated board");
        }

        private static void CheckSymmetry(int[,] board, int row, int column, ForbiddenMove expected)
        {
            for (var mirrored = 0; mirrored < 2; mirrored++)
            {
                for (var rotation = 0; rotation < 4; rotation++)
                {
                    var transformed = new int[15, 15];

                    for (var r = 0; r < 15; r++)
                    {
                        for (var c = 0; c < 15; c++)
                        {
                            var point = Transform(r, c, mirrored != 0, rotation);
                            transformed[point.Row, point.Column] = board[r, c];
                        }
                    }

                    var target = Transform(row, column, mirrored != 0, rotation);
                    CheckReason(transformed, target.Row, target.Column, expected);
                }
            }
        }

        private static (int Row, int Column) Transform(int row, int column, bool mirror, int rotations)
        {
            if (mirror)
            {
                column = 14 - column;
            }

            for (var index = 0; index < rotations; index++)
            {
                (row, column) = (column, 14 - row);
            }

            return (row, column);
        }

        private static void CheckSession(int[,] position, (int Row, int Column) target, ForbiddenMove reason)
        {
            var game = new GameSession();
            game.Start(1);
            var count = 0;

            for (var row = 0; row < 15; row++)
            {
                for (var column = 0; column < 15; column++)
                {
                    if (position[row, column] != 1)
                    {
                        continue;
                    }

                    Require(game.Place(row, column), "Session fixture black move rejected");
                    Require(game.Place(14, count * 2), "Session fixture white move rejected");
                    count++;
                }
            }

            var before = game.Snapshot();
            var history = game.History.Count;
            Require(!game.Place(target.Row, target.Column), "Session accepted forbidden move");
            Require(game.LastForbiddenMove == reason, "Session did not report correct reason");
            Require(game.Current == 1 && game.History.Count == history, "Rejected move changed turn/history");
            Require(game.Snapshot().Cast<int>().SequenceEqual(before.Cast<int>()), "Rejected move changed stones");
            Require(game.Winner == 0 && !game.Over, "Rejected move ended game");
            Require(game.Undo(), "Undo failed after forbidden move");
            Require(game.LastForbiddenMove == ForbiddenMove.None, "Undo did not clear forbidden reason");
        }

        private static void CheckDisabled(
            int[,] position,
            (int Row, int Column) target,
            bool wins)
        {
            var game = new GameSession();
            Require(game.ForbiddenRulesEnabled, "Rules must default to enabled");
            game.Start(1, false);
            var count = 0;

            for (var row = 0; row < 15; row++)
            {
                for (var column = 0; column < 15; column++)
                {
                    if (position[row, column] == 1)
                    {
                        Require(game.Place(row, column), "Disabled fixture black move rejected");
                        Require(game.Place(14, count * 2), "Disabled fixture white move rejected");
                        count++;
                    }
                }
            }

            Require(game.Place(target.Row, target.Column), "Disabled rules rejected black move");
            Require(game.LastForbiddenMove == ForbiddenMove.None, "Disabled rules reported forbidden");
            Require(game.Winner == (wins ? 1 : 0), "Freestyle long line did not win");
            game.Start(1);
            Require(game.ForbiddenRulesEnabled, "Default restart did not enable rules");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception(message);
            }
        }
    }
}
