using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WUZIQI;
AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing=false }).SetupWithoutStarting();
var output=args.Length>0 ? args[0] : ".";
Directory.CreateDirectory(output);

// 直接检查打包字体的字形，避免桌面系统字体回退掩盖 Android 上的缺字。
foreach (var weight in new[] { FontWeight.Normal, FontWeight.SemiBold, FontWeight.Bold })
{
    var family = new FontFamily("avares://WUZIQI/Assets/Fonts#Noto Sans CJK SC");
    var typeface = new Typeface(family, FontStyle.Normal, weight).GlyphTypeface;

    foreach (var character in "五子棋你的回合电脑回合对局设置炼狱重新开始悔棋")
    {
        if (!typeface.TryGetGlyph(character, out var glyph) || glyph == 0)
        {
            throw new Exception($"Missing embedded Chinese glyph: {character}, {weight}");
        }
    }

    Console.WriteLine($"PASS embedded Chinese glyphs: {weight}");
}
foreach (var (width,height) in new[] { (320,640),(390,844),(844,390),(1040,800) })
{
 var view=new MainView();
 var window=new Window { Width=width,Height=height,Content=view };
 window.Show(); Dispatcher.UIThread.RunJobs();
 var heading = view.GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == "五子棋");

 if (!heading.FontFamily.Name.Contains("Noto Sans CJK SC"))
 {
     throw new Exception("Heading is not using the embedded Chinese font");
 }
 var board=view.FindControl<Viewbox>("BoardView")!;
 var settings=view.FindControl<Border>("InfoPanel")!;
 if (board.Bounds.Width>width-30 || Math.Abs(board.Bounds.Width-board.Bounds.Height)>1) throw new Exception($"Board overflow at {width}");
 if (width<820 && Grid.GetRow(settings)!=1 || width>=820 && Grid.GetColumn(settings)!=1) throw new Exception("Responsive layout failed");
 using var capture=window.CaptureRenderedFrame();
 capture?.Save(Path.Combine(output,$"layout-{width}x{height}.png"));
 Console.WriteLine($"PASS layout {width}x{height}, square board {board.Bounds.Width:0}, settings row {Grid.GetRow(settings)}");
 if (width==390)
 {
  var start=view.FindControl<Button>("StartButton")!;
  start.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  var canvas=view.FindControl<Canvas>("BoardCanvas")!;
  var point=canvas.TranslatePoint(new Point(324,324),window)!.Value;
  window.MouseDown(point,MouseButton.Left); window.MouseUp(point,MouseButton.Left);
  var moves=view.FindControl<TextBlock>("MovesText")!;
  for (var retry=0;retry<100 && moves.Text!="落子数  2";retry++) { Thread.Sleep(20); Dispatcher.UIThread.RunJobs(); }
  if (moves.Text!="落子数  2") throw new Exception("Touch/mouse placement or background AI failed");
  view.FindControl<Button>("UndoButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  if (moves.Text!="落子数  0") throw new Exception("UI undo failed");
  window.MouseDown(point,MouseButton.Left); window.MouseUp(point,MouseButton.Left);
  start.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  for (var retry=0;retry<30;retry++) { Thread.Sleep(20); Dispatcher.UIThread.RunJobs(); }
  if (moves.Text!="落子数  0") throw new Exception("Stale AI move applied after restart");
  Console.WriteLine("PASS UI placement, background AI, undo and restart during search");

    var field = typeof(MainView).GetField(
        "game",
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
    var session = (WUZIQI.Game.GameSession)field.GetValue(view)!;
    var cases = new[]
    {
        (Stones: new[] { (7, 6), (7, 8), (6, 7), (8, 7) }, Target: (7, 7), Reason: "三三禁手"),
        (Stones: new[] { (7, 5), (7, 6), (7, 8), (5, 7), (6, 7), (8, 7) }, Target: (7, 7), Reason: "四四禁手"),
        (Stones: new[] { (7, 3), (7, 4), (7, 5), (7, 7), (7, 8) }, Target: (7, 6), Reason: "长连禁手")
    };

    foreach (var test in cases)
    {
        start.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        for (var index = 0; index < test.Stones.Length; index++)
        {
            var stone = test.Stones[index];

            if (!session.Place(stone.Item1, stone.Item2) || !session.Place(14, index * 2))
            {
                throw new Exception("Could not set up forbidden-move UI fixture");
            }
        }

        var target = canvas.TranslatePoint(
            new Point(24 + test.Target.Item2 * 600.0 / 14, 24 + test.Target.Item1 * 600.0 / 14),
            window)!.Value;
        var countBefore = session.History.Count;
        window.MouseDown(target, MouseButton.Left);
        window.MouseUp(target, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        var status = view.FindControl<TextBlock>("StatusText")!;

        if (!status.Text!.Contains(test.Reason) || session.History.Count != countBefore
            || session.Current != 1 || session[test.Target.Item1, test.Target.Item2] != 0)
        {
            throw new Exception("Forbidden move UI did not reject and explain: " + test.Reason);
        }
    }

    var forbiddenBox = view.FindControl<CheckBox>("ForbiddenRulesBox")!;

    if (forbiddenBox.IsChecked != true)
    {
        throw new Exception("Forbidden rules must be enabled by default");
    }

    forbiddenBox.IsChecked = false;
    start.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    if (session.ForbiddenRulesEnabled)
    {
        throw new Exception("Restart did not disable forbidden rules");
    }

    forbiddenBox.IsChecked = true;
    start.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    if (!session.ForbiddenRulesEnabled)
    {
        throw new Exception("Restart did not restore forbidden rules");
    }

    Console.WriteLine("PASS UI forbidden rejection, default setting, disabling and enabling");
 }
 window.Close();
}
