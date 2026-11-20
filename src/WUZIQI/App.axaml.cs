using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;

namespace WUZIQI
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                using var icon = AssetLoader.Open(new Uri("avares://WUZIQI/Assets/app.ico"));
                desktop.MainWindow = new Window
                {
                    Title = "五子棋 · Gomoku",
                    Width = 1040,
                    Height = 800,
                    MinWidth = 320,
                    MinHeight = 480,
                    Icon = new WindowIcon(icon),
                    Content = new MainView()
                };
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime mobile)
            {
                mobile.MainView = new MainView();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
