using System.Windows;

namespace ImageToolkit;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (TryParseCaptureArgs(e.Args, out var sampleImagePath, out var initialScreenshotPath, out var processedScreenshotPath))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var window = new MainWindow();
            MainWindow = window;
            window.Show();

            try
            {
                await window.CaptureDocumentationScreenshotsAsync(sampleImagePath, initialScreenshotPath, processedScreenshotPath);
            }
            finally
            {
                Shutdown();
            }

            return;
        }

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    private static bool TryParseCaptureArgs(
        IReadOnlyList<string> args,
        out string sampleImagePath,
        out string initialScreenshotPath,
        out string processedScreenshotPath)
    {
        sampleImagePath = string.Empty;
        initialScreenshotPath = string.Empty;
        processedScreenshotPath = string.Empty;

        if (args.Count != 4 || !string.Equals(args[0], "--capture-screenshots", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        sampleImagePath = args[1];
        initialScreenshotPath = args[2];
        processedScreenshotPath = args[3];
        return true;
    }
}
