using WUZHIQI.Game;
static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
var g = new GameSession();
Check(!g.Place(7,7), "Cannot play before start");
g.Start(1);
Check(!g.Place(-1,0) && !g.Place(15,0), "Board bounds");
Check(g.Place(7,7) && !g.Place(7,7), "Occupied intersection");
Check(g.Undo() && g.History.Count == 0 && g.Current == 1, "Undo while computer is thinking");
g.Start(2); g.Place(7,7);
Check(!g.Undo() && g.Current == 2, "Keep black opening for white player");
g.Place(7,8); g.Place(8,8);
Check(g.Undo() && g.History.Count == 1 && g.Current == 2, "Undo full pair as white");
foreach (var (dr,dc) in new[] { (0,1),(1,0),(1,1),(1,-1) }) {
 g.Start(1);
 for (var i=0;i<5;i++) {
  Check(g.Place(3+dr*i,7+dc*i), "Winning move");
  if (i<4) Check(g.Place(14,i*2), "Opponent filler");
 }
 Check(g.Winner==1 && g.Winning.Count==5 && g.Over, "Win in each direction");
 Check(!g.Place(0,0), "Cannot play after win");
 Check(g.Undo() && !g.Over && g.Winning.Count==0, "Undo after win");
}
var board=new int[15,15];
for (int i=3;i<7;i++) board[7,i]=2;
var engine=new SearchEngine(board,2,Difficulty.Normal);
var move=engine.FindComputerMove();
Check(move.Row==7 && (move.Column==2 || move.Column==7), "AI takes immediate win");
Check(board[move.Row,move.Column]==0, "AI never mutates live board");
for (int i=3;i<7;i++) board[7,i]=1;
move=new SearchEngine(board,2,Difficulty.Hard).FindComputerMove();
Check(move.Row==7 && (move.Column==2 || move.Column==7), "AI blocks immediate win");
using var cancelled=new CancellationTokenSource(); cancelled.Cancel();
try { new SearchEngine(board,2,Difficulty.Inferno,cancelled.Token).FindComputerMove(); throw new Exception("Cancellation not observed"); }
catch (OperationCanceledException) { }
foreach (Difficulty level in Enum.GetValues<Difficulty>()) {
 move=new SearchEngine(new int[15,15],1,level).FindComputerMove();
 Check(move==(7,7), $"Center opening for {level}");
}
Console.WriteLine("PASS: bounds, turns, undo, four winning directions, AI wins/blocks, snapshot isolation, cancellation and seven difficulty openings.");

// 非直接胜负的局面也必须在预算内返回合法落点，并保持原棋盘不变。
var timedBoard = new int[15, 15];
timedBoard[7, 7] = 1;
timedBoard[6, 6] = 2;
timedBoard[7, 6] = 1;
var timedEngine = new SearchEngine(timedBoard, 2, Difficulty.Inferno);
var searchTime = System.Diagnostics.Stopwatch.StartNew();
var timedMove = timedEngine.FindComputerMove();
Check(searchTime.ElapsedMilliseconds < 2000, "Inferno search exceeded responsiveness limit");
Check(timedBoard[timedMove.Row, timedMove.Column] == 0, "Timed search changed source board");
Check(timedEngine.CompletedDepth >= 1, "Iterative search did not complete a layer");
Console.WriteLine(
    $"PASS: Inferno {searchTime.ElapsedMilliseconds} ms, "
    + $"completed depth {timedEngine.CompletedDepth}, nodes {timedEngine.VisitedNodes}");

using var midSearchCancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(10));

try
{
    new SearchEngine(timedBoard, 2, Difficulty.Inferno, midSearchCancellation.Token)
        .FindComputerMove();
    throw new Exception("Running search did not observe cancellation");
}
catch (OperationCanceledException)
{
    Console.WriteLine("PASS: cancellation during active search");
}

var attackingBoard = new int[15, 15];
attackingBoard[7, 5] = 2;
attackingBoard[7, 6] = 2;
attackingBoard[7, 8] = 2;
var attackingEngine = new SearchEngine(attackingBoard, 2, Difficulty.Inferno);
var attackingMove = attackingEngine.FindComputerMove();
Check(attackingMove == (7, 7), "Early game must create two winning continuations");
Check(!attackingEngine.IsLateGameAssistanceActive, "Early game assistance activated");

static int[,] CreateAssistanceBoard(int occupied)
{
    var position = new int[15, 15];
    var remaining = occupied - 6;

    // 周期棋形在四个方向均不含五连；保留一行构造玩家的唯一成五点。
    for (var row = 0; row < 15; row++)
    {
        for (var column = 0; column < 15; column++)
        {
            if (row == 7 || remaining == 0)
            {
                continue;
            }

            position[row, column] = (row + 2 * column) % 4 < 2 ? 1 : 2;
            remaining--;
        }
    }

    position[7, 2] = 2;
    position[7, 8] = 2;

    for (var column = 3; column <= 6; column++)
    {
        position[7, column] = 1;
    }

    return position;
}

var beforeAssistance = CreateAssistanceBoard(179);
var defendingEngine = new SearchEngine(beforeAssistance, 2, Difficulty.Inferno);
Check(!defendingEngine.IsLateGameAssistanceActive, "Assistance started before 180 stones");
Check(defendingEngine.FindComputerMove() == (7, 7), "Early game failed to block immediate loss");

foreach (var occupied in new[] { 180, 203 })
{
    var lateBoard = CreateAssistanceBoard(occupied);
    var lateEngine = new SearchEngine(lateBoard, 2, Difficulty.Inferno);
    var lateMove = lateEngine.FindComputerMove();
    Check(lateEngine.IsLateGameAssistanceActive, "Late game assistance did not activate");
    Check(lateMove != (7, 7), "Late game blocked the player's available win");
    Check(lateBoard[lateMove.Row, lateMove.Column] == 0, "Assisted move is not legal");
    Check(lateBoard[7, 7] == 0 && lateBoard[7, 3] == 1, "Assistance changed live stones");
}

var lastCellBoard = new int[15, 15];

for (var row = 0; row < 15; row++)
{
    for (var column = 0; column < 15; column++)
    {
        lastCellBoard[row, column] = (row + 2 * column) % 4 < 2 ? 1 : 2;
    }
}

lastCellBoard[7, 7] = 0;
Check(
    new SearchEngine(lastCellBoard, 2, Difficulty.Inferno).FindComputerMove() == (7, 7),
    "Last available intersection must remain playable");
Console.WriteLine("PASS: attacking fork, 179/180/203 stone stages, legal assistance and last cell");
