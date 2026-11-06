using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Sketch.App;

public partial class App : Application
{
    public static string LogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sketch", "sketch.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        if (e.Args.Length >= 2 && e.Args[0] == "--script" && File.Exists(e.Args[1])) Properties["Script"] = e.Args[1];
        else if (e.Args.Length > 0 && File.Exists(e.Args[0])) Properties["OpenFile"] = e.Args[0];
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex) Log(ex);
        };
        base.OnStartup(e);

        // Test mode starts straight away; otherwise the splash screen shows for a fixed time while the main window
        // is built behind it.
        if (Properties.Contains("Script"))
        {
            MainWindow = new MainWindow();
            MainWindow.Show();
            return;
        }
        var duration = TimeSpan.FromSeconds(3);
        var splash = new SplashWindow(duration);
        var shown = System.Diagnostics.Stopwatch.StartNew();
        ShutdownMode = ShutdownMode.OnExplicitShutdown; // closing the splash must not end the application
        splash.Show();
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () =>
        {
            var main = new MainWindow();
            MainWindow = main;
            var remaining = duration - shown.Elapsed;
            var timer = new DispatcherTimer { Interval = remaining > TimeSpan.Zero ? remaining : TimeSpan.FromMilliseconds(1) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                main.Show();
                main.Activate();
                splash.Close();
                ShutdownMode = ShutdownMode.OnMainWindowClose;
            };
            timer.Start();
        });
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ReportError(e.Exception);
        e.Handled = true;
    }

    /// <summary>Logs an error and tells the user without closing the application.</summary>
    public static void ReportError(Exception ex)
    {
        Log(ex);
        if (Current?.Properties.Contains("Script") == true) return; // test mode: never block on a dialog
        MessageBox.Show($"An unexpected error occurred:\n\n{ex.Message}\n\nDetails: {LogPath}",
            "Sketch", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    public static void Log(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
        }
        catch
        {
            // Logging must never throw.
        }
    }
}
