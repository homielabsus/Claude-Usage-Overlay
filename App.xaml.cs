using System.Windows;
using WpfMessageBox = System.Windows.MessageBox;

namespace ClaudeUsageOverlay;

public partial class App : System.Windows.Application
{
    private static Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _mutex = new Mutex(true, "Global\\ClaudeUsageOverlay_v1", out bool isNew);
        if (!isNew)
        {
            WpfMessageBox.Show("Claude Usage Overlay is already running.",
                "Already Running", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }
        new MainWindow().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _mutex?.ReleaseMutex(); } catch { }
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
