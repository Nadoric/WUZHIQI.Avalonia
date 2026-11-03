using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WUZHIQI;
AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing=false }).SetupWithoutStarting();
var output=args.Length>0 ? args[0] : ".";
Directory.CreateDirectory(output);

// 直接检查打包字体的字形，避免桌面系统字体回退掩盖 Android 上的缺字。
foreach (var weight in new[] { FontWeight.Normal, FontWeight.SemiBold, FontWeight.Bold })
{
    var family = new FontFamily("avares://WUZHIQI/Assets/Fonts#Noto Sans CJK SC");
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
 }
 window.Close();
}
