using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WinForms   = System.Windows.Forms;
using Drawing    = System.Drawing;
using WpfApp     = System.Windows.Application;
using WpfColor   = System.Windows.Media.Color;

namespace ClaudeUsageOverlay;

public partial class MainWindow : Window
{
    private readonly UsageService    _svc      = new();
    private readonly AppSettings     _settings = SettingsManager.Load();
    private DispatcherTimer?         _timer;
    private WinForms.NotifyIcon?     _tray;

    // Prebuilt context menu (built once; ContextMenu API requires no live tree)
    private readonly ContextMenu _menu;
    private readonly MenuItem    _menuStartup;

    public MainWindow()
    {
        InitializeComponent();

        // Build context menu
        _menuStartup = new MenuItem
        {
            Header      = "⚡  Run at Startup",
            IsCheckable = true,
            IsChecked   = StartupHelper.IsEnabled()
        };
        _menuStartup.Click += OnToggleStartup;

        var menuRefresh = new MenuItem { Header = "🔄  Refresh" };
        menuRefresh.Click += OnRefresh;

        var menuExit = new MenuItem { Header = "✕  Exit" };
        menuExit.Click += OnExit;

        _menu = new ContextMenu();
        _menu.Items.Add(menuRefresh);
        _menu.Items.Add(new Separator());
        _menu.Items.Add(_menuStartup);
        _menu.Items.Add(new Separator());
        _menu.Items.Add(menuExit);

        ApplySettings();
        InitTray();
        Loaded  += async (_, _) => await StartAsync();
        Closing += OnClosing;
    }

    // ── System tray icon ───────────────────────────────────────────
    private void InitTray()
    {
        Drawing.Icon icon;
        var stream = WpfApp.GetResourceStream(
            new Uri("pack://application:,,,/app.ico"))?.Stream;
        icon = stream is not null
            ? new Drawing.Icon(stream)
            : Drawing.SystemIcons.Application;

        var trayMenu = new WinForms.ContextMenuStrip();
        trayMenu.Items.Add("Show",               null, (_, _) => Dispatcher.Invoke(() => { Show(); Activate(); }));
        trayMenu.Items.Add("🔄  Refresh",        null, (_, _) => { StatusLabel.Text = "Refreshing…"; _svc.Fetch(); });
        trayMenu.Items.Add(new WinForms.ToolStripSeparator());
        trayMenu.Items.Add("✕  Exit",            null, (_, _) => WpfApp.Current.Shutdown());

        _tray = new WinForms.NotifyIcon
        {
            Icon             = icon,
            Text             = "Claude Usage Overlay",
            Visible          = true,
            ContextMenuStrip = trayMenu
        };

        // Double-click tray icon → bring overlay to front
        _tray.DoubleClick += (_, _) => Dispatcher.Invoke(() => { Show(); Activate(); });
    }

    // ── Apply saved settings ─────────────────────────────────────────
    private void ApplySettings()
    {
        Left                 = _settings.Left;
        Top                  = _settings.Top;
        Opacity              = _settings.WindowOpacity;
        OpacitySlider.Value  = _settings.WindowOpacity;
        _menuStartup.IsChecked = _settings.RunAtStartup;
    }

    // ── Startup ──────────────────────────────────────────────────
    private async Task StartAsync()
    {
        _svc.DataReady   += OnDataReady;
        _svc.LoginNeeded += OnLoginNeeded;

        await _svc.InitAsync();
        _svc.Fetch();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(
                Math.Max(1, _settings.RefreshIntervalMinutes))
        };
        _timer.Tick += (_, _) => _svc.Fetch();
        _timer.Start();
    }

    // ── Drag overlay ───────────────────────────────────────────────
    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    // ── Right-click → context menu ───────────────────────────────────────
    private void OnRightClick(object sender, MouseButtonEventArgs e)
    {
        _menu.PlacementTarget = this;
        _menu.IsOpen          = true;
    }

    // ── Refresh button ───────────────────────────────────────────────
    private void OnRefresh(object sender, RoutedEventArgs e)
    {
        StatusLabel.Text = "Refreshing…";
        _svc.Fetch();
        _timer?.Stop();
        _timer?.Start();
    }

    // ── Expand / collapse ──────────────────────────────────────────
    private void OnToggleExpand(object sender, RoutedEventArgs e)
    {
        bool expanding = ExpandedSection.Visibility != Visibility.Visible;
        ExpandedSection.Visibility = expanding ? Visibility.Visible : Visibility.Collapsed;
        ExpandBtn.Content          = expanding ? "Show less ▴" : "Show more ▾";
    }

    // ── Opacity slider ───────────────────────────────────────────────
    private void OnOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        Opacity = e.NewValue;
        _settings.WindowOpacity = e.NewValue;
        SettingsManager.Save(_settings);
    }

    private void OnResetOpacity(object sender, RoutedEventArgs e)
    {
        OpacitySlider.Value = 0.90;
    }

    // ── Startup toggle ─────────────────────────────────────────────
    private void OnToggleStartup(object sender, RoutedEventArgs e)
    {
        bool enable = _menuStartup.IsChecked;
        StartupHelper.SetEnabled(enable);
        _settings.RunAtStartup = enable;
        SettingsManager.Save(_settings);
    }

    // ── Minimize to tray ───────────────────────────────────────────
    private void OnMinimize(object sender, RoutedEventArgs e) => Hide();

    // ── Exit ───────────────────────────────────────────────────────
    private void OnExit(object sender, RoutedEventArgs e)
        => WpfApp.Current.Shutdown();

    // ── Data ready ─────────────────────────────────────────────────
    private void OnDataReady(UsageData d)
    {
        Dispatcher.Invoke(() =>
        {
            if (d.Error is not null && d.SessionPercent == 0 && d.WeeklyAllPercent == 0)
            {
                StatusLabel.Text = $"Error: {d.Error}";
                return;
            }

            PlanLabel.Text           = d.PlanLabel;
            SessionPctLabel.Text     = $"{d.SessionPercent}% used";
            SessionResetLabel.Text   = d.SessionResetIn;
            SessionBar.Value         = d.SessionPercent;
            ApplyBarColor(SessionBar, d.SessionPercent);

            WeeklyAllPctLabel.Text   = $"{d.WeeklyAllPercent}% used";
            WeeklyResetLabel.Text    = d.WeeklyResetAt;
            WeeklyAllBar.Value       = d.WeeklyAllPercent;
            ApplyBarColor(WeeklyAllBar, d.WeeklyAllPercent);

            SonnetPctLabel.Text      = $"{d.WeeklySonnetPercent}% used";
            SonnetResetLabel.Text    = d.WeeklyResetAt;
            SonnetBar.Value          = d.WeeklySonnetPercent;
            ApplyBarColor(SonnetBar, d.WeeklySonnetPercent);

            StatusLabel.Text = $"Updated {d.FetchedAt:HH:mm:ss}";
        });
    }

    private static void ApplyBarColor(System.Windows.Controls.ProgressBar bar, int pct)
    {
        bar.Foreground = pct >= 90
            ? new SolidColorBrush(WpfColor.FromRgb(0xEF, 0x44, 0x44))  // red
            : pct >= 75
                ? new SolidColorBrush(WpfColor.FromRgb(0xF5, 0x9E, 0x0B)) // amber
                : new SolidColorBrush(WpfColor.FromRgb(0x3B, 0x82, 0xF6)); // blue
    }

    private void OnLoginNeeded()
        => Dispatcher.Invoke(() => StatusLabel.Text = "Sign in → see browser window");

    // ── Save window position on close ──────────────────────────────────
    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _settings.Left = Left;
        _settings.Top  = Top;
        SettingsManager.Save(_settings);
        _svc.Dispose();
        _timer?.Stop();
        _tray?.Dispose();
    }
}
