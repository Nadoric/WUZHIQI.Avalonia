using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WUZHIQI.Game;
namespace WUZHIQI;
public partial class MainView : UserControl
{
    private const double Cell = 600.0 / 14, Pad = 24;
    private readonly GameSession game = new();
    private readonly Stopwatch elapsed = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private CancellationTokenSource? search;
    private bool thinking;
    private int generation;
    private Difficulty activeDifficulty;
    private Point? pressed;
    public MainView()
    {
        InitializeComponent();
        timer.Tick += (_, _) => TimeText.Text = elapsed.Elapsed.ToString(@"hh\:mm\:ss");
        SizeChanged += (_, _) => AdaptLayout();
        AttachedToVisualTree += (_, _) => { AdaptLayout(); if (game.Started && !game.Over) { timer.Start(); ResumeComputer(); } };
        DetachedFromVisualTree += (_, _) => { CancelSearch(); timer.Stop(); };
        Draw(); UpdateStatus();
    }
    private void AdaptLayout()
    {
        var wide = Bounds.Width >= 820;
        var width = Math.Max(200, Math.Min(600, wide ? Bounds.Width - 380 : Bounds.Width - 32));
        BoardView.Width = width; BoardView.Height = width;
        GameLayout.ColumnDefinitions = new ColumnDefinitions(wide ? "*,300" : "*");
        GameLayout.RowDefinitions = new RowDefinitions(wide ? "Auto" : "Auto,Auto");
        Grid.SetRow(InfoPanel, wide ? 0 : 1); Grid.SetColumn(InfoPanel, wide ? 1 : 0);
        InfoPanel.Margin = wide ? new Thickness(20,0,0,0) : new Thickness(0,16,0,0);
    }
    private void CancelSearch()
    {
        generation++; search?.Cancel(); search?.Dispose(); search = null; thinking = false;
    }
    private void StartClicked(object? sender, RoutedEventArgs e)
    {
        CancelSearch(); game.Start(ColorBox.SelectedIndex == 1 ? 2 : 1);
        activeDifficulty = (Difficulty)Math.Clamp(DifficultyBox.SelectedIndex,0,6);
        elapsed.Restart(); timer.Start(); TimeText.Text = "00:00:00";
        StartButton.Content = "重新开始"; UpdateStatus(); Draw(); ResumeComputer();
    }
    private void UndoClicked(object? sender, RoutedEventArgs e)
    {
        CancelSearch();
        if (game.Undo()) { elapsed.Start(); timer.Start(); }
        UpdateStatus(); Draw(); ResumeComputer();
    }
    private void BoardPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(BoardCanvas).Properties.IsLeftButtonPressed) pressed = e.GetPosition(BoardCanvas);
    }
    private void BoardCaptureLost(object? sender, PointerCaptureLostEventArgs e) => pressed = null;
    private void BoardReleased(object? sender, PointerReleasedEventArgs e)
    {
        var start = pressed; pressed = null;
        if (start is null || thinking || game.Current != game.Human) return;
        var p = e.GetPosition(BoardCanvas);
        // Releasing after a scroll/drag must not place a stone.
        if (Math.Abs(p.X-start.Value.X) > Cell/2 || Math.Abs(p.Y-start.Value.Y) > Cell/2) return;
        if (game.Place((int)Math.Round((p.Y-Pad)/Cell), (int)Math.Round((p.X-Pad)/Cell)))
        { Draw(); UpdateStatus(); ResumeComputer(); }
    }
    private async void ResumeComputer()
    {
        if (!game.Started || game.Over || game.Current != game.Computer || thinking || !this.IsAttachedToVisualTree()) return;
        thinking = true; var version = generation;
        search?.Dispose(); search = new CancellationTokenSource(); var token = search.Token;
        var snapshot = game.Snapshot(); var computer = game.Computer; var level = activeDifficulty;
        UpdateStatus();
        try
        {
            var move = await Task.Run(() => new SearchEngine(snapshot, computer, level, token).FindComputerMove(), token);
            if (version != generation || token.IsCancellationRequested) return;
            thinking = false; game.Place(move.Row,move.Column); Draw(); UpdateStatus();
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            if (version != generation) return;
            thinking = false; UpdateStatus(); StatusText.Text = "电脑计算失败，请重新开始或降低难度。";
        }
    }
    private void UpdateStatus()
    {
        TurnText.Text = !game.Started ? "等待开始" : game.Over ? "本局结束" : game.Current == game.Human ? "你的回合" : "电脑回合";
        StatusText.Text = !game.Started ? "选择设置后，点击开始游戏" : game.Winner != 0 ? (game.Winner == game.Human ? "你赢了！" : "电脑获胜，再来一局吧") : game.Over ? "和棋" : thinking ? "电脑思考中… 可以悔棋或重新开始" : "轻触棋盘交叉点落子";
        MovesText.Text = $"落子数  {game.History.Count}";
        UndoButton.IsEnabled = game.Started && game.History.Any(m => m.Player == game.Human);
        if (game.Over) { elapsed.Stop(); timer.Stop(); }
    }
    private void Draw()
    {
        BoardCanvas.Children.Clear();
        var ink = Brush.Parse("#66431F");
        for (var i=0; i<15; i++)
        {
            var p = Pad+i*Cell;
            BoardCanvas.Children.Add(new Line { StartPoint=new(p,Pad),EndPoint=new(p,624),Stroke=ink,StrokeThickness=i is 0 or 14 ? 2 : 1,IsHitTestVisible=false });
            BoardCanvas.Children.Add(new Line { StartPoint=new(Pad,p),EndPoint=new(624,p),Stroke=ink,StrokeThickness=i is 0 or 14 ? 2 : 1,IsHitTestVisible=false });
        }
        foreach (var (r,c) in new[] { (3,3),(3,11),(7,7),(11,3),(11,11) }) Circle(r,c,8,ink,null,0);
        var lastComputer = game.History.LastOrDefault(m=>m.Player==game.Computer);
        foreach (var m in game.History)
            Circle(m.Row,m.Column,Cell*.82,m.Player==1 ? Brush.Parse("#111827") : Brushes.White,
                game.Winning.Contains((m.Row,m.Column)) ? Brushes.Gold : m==lastComputer ? Brushes.Red : ink, 3);
    }
    private void Circle(int row,int col,double size,IBrush fill,IBrush? stroke,double thickness)
    {
        var shape=new Ellipse { Width=size,Height=size,Fill=fill,Stroke=stroke,StrokeThickness=thickness,IsHitTestVisible=false };
        Canvas.SetLeft(shape,Pad+col*Cell-size/2); Canvas.SetTop(shape,Pad+row*Cell-size/2); BoardCanvas.Children.Add(shape);
    }
}
