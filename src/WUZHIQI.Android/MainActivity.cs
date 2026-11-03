using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Avalonia.Android;
namespace WUZHIQI.Android;
[Activity(Label="五子棋", Theme="@style/AppTheme", Icon="@drawable/icon", MainLauncher=true, Exported=true,
    ConfigurationChanges=ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        // Android 15/16 enforce edge-to-edge. Keep the board and buttons clear
        // of the status bar, camera cutout, and gesture navigation area.
        if (OperatingSystem.IsAndroidVersionAtLeast(35))
        {
            var content = FindViewById<global::Android.Views.View>(global::Android.Resource.Id.Content);
            if (content is not null)
            {
                content.SetOnApplyWindowInsetsListener(new SafeInsetsListener());
                content.RequestApplyInsets();
            }
        }
    }
    private sealed class SafeInsetsListener : Java.Lang.Object, global::Android.Views.View.IOnApplyWindowInsetsListener
    {
        public WindowInsets OnApplyWindowInsets(global::Android.Views.View view, WindowInsets insets)
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                var safe = insets.GetInsets(WindowInsets.Type.SystemBars() | WindowInsets.Type.DisplayCutout());
                view.SetPadding(safe.Left, safe.Top, safe.Right, safe.Bottom);
            }
            return insets;
        }
    }
}
