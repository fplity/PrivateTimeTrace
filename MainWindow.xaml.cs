using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace PrivateTimeTrace;

/// <summary>
/// The application window. This hosts a Frame that displays pages. Add your
/// UI and logic to MainPage.xaml / MainPage.xaml.cs instead of here so you
/// can use Page features such as navigation events and the Loaded lifecycle.
/// </summary>
public sealed class MainWindow : Window
{
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint window);

    public MainWindow()
    {
        Title = "时间迹";
        var workArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(AppWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Primary).WorkArea;
        var scale = Math.Max(1, GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96d);
        var size = new Windows.Graphics.SizeInt32(Math.Min((int)(1380 * scale), workArea.Width - 48), Math.Min((int)(880 * scale), workArea.Height - 48));
        AppWindow.Resize(size);
        AppWindow.Move(new Windows.Graphics.PointInt32(workArea.X + (workArea.Width - size.Width) / 2, workArea.Y + (workArea.Height - size.Height) / 2));
        var page = new MainPage();
        Content = page;
        Closed += (_, _) => { App.IsClosing = true; page.ViewModel.StopClock(); };
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(page.TitleBarElement);
        AppWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
        AppWindow.TitleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 40, 63, 89);
        AppWindow.TitleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(90, 255, 255, 255);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
    }
}
