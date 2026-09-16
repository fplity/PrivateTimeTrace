using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace PrivateTimeTrace;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    public static string? DatabasePath { get; private set; }
    // Instantiate UI-affine settings after XAML has initialized, on first UI use.
    private static readonly Lazy<Windows.UI.ViewManagement.AccessibilitySettings> AccessibilitySettings = new(() => new());
    private static readonly Lazy<Windows.UI.ViewManagement.UISettings> AppearanceSettings = new(() => new());
    public static bool IsClosing { get; internal set; }
    public static bool HighContrast => AccessibilitySettings.Value.HighContrast;
    public static bool ReducedEffects => IsClosing || HighContrast || !AppearanceSettings.Value.AnimationsEnabled || !AppearanceSettings.Value.AdvancedEffectsEnabled;
    /// <summary>
    /// The main application window. Use <c>App.Window</c> from any class that needs
    /// the window reference (for dialogs, pickers, interop, etc.).
    /// </summary>
    public static Window Window { get; private set; } = null!;

    /// <summary>
    /// The UI thread dispatcher. Use <c>App.DispatcherQueue</c> to marshal calls
    /// to the UI thread. Fully qualified to avoid CS0104 ambiguity with
    /// <see cref="Windows.System.DispatcherQueue"/>.
    /// </summary>
    public static Microsoft.UI.Dispatching.DispatcherQueue DispatcherQueue { get; private set; } = null!;

    /// <summary>
    /// The native window handle (HWND). Use for file pickers,
    /// <c>DataTransferManager</c>, and any WinRT interop that requires
    /// <c>InitializeWithWindow</c>.
    /// </summary>
    public static nint WindowHandle =>
        WinRT.Interop.WindowNative.GetWindowHandle(Window);

    /// <summary>
    /// Initializes the singleton application object.
    /// </summary>
    public App()
    {
        InitializeComponent();
        UnhandledException += (_, eventArgs) =>
        {
            var directory = DatabasePath is not null ? Path.GetDirectoryName(DatabasePath)! : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PrivateTimeTrace");
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(directory, "startup-error.log"),
                $"{DateTime.Now:O}\r\n{eventArgs.Exception}");
        };
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var arguments = Environment.GetCommandLineArgs();
        var dataArgument = Array.IndexOf(arguments, "--data-file");
        if (dataArgument >= 0 && dataArgument + 1 < arguments.Length) DatabasePath = Path.GetFullPath(arguments[dataArgument + 1]);
        Window = new MainWindow();
        DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        // Isolated UI automation can show a real window without taking the user's keyboard focus.
        if (DatabasePath is not null && arguments.Contains("--background-test")) Window.AppWindow.Show(false);
        else Window.Activate();
    }
}
