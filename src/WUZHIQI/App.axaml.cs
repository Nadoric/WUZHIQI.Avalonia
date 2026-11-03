using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
namespace WUZHIQI;
public partial class App : Application
{
 public override void Initialize() => AvaloniaXamlLoader.Load(this);
 public override void OnFrameworkInitializationCompleted()
 {
  if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
   desktop.MainWindow = new Window { Title="五子棋 · Gomoku", Width=1040, Height=800, MinWidth=320, MinHeight=480, Content=new MainView() };
  else if (ApplicationLifetime is ISingleViewApplicationLifetime mobile) mobile.MainView=new MainView();
  base.OnFrameworkInitializationCompleted();
 }
}
