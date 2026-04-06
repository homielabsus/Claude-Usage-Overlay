# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build

```bat
build.bat
```

Produces `dist\ClaudeUsage.exe` — a self-contained single-file Windows executable (no runtime required).

For a quick debug build without publishing:
```bash
dotnet build ClaudeUsageOverlay.csproj
```

## Architecture

This is a WPF + WinForms hybrid app targeting `net8.0-windows`. No MVVM framework — UI state is updated directly in code-behind (`MainWindow.xaml.cs`).

**Data flow:**
1. `UsageService` initializes a hidden `WebView2` browser pointed at `claude.ai/settings/usage`
2. After navigation completes, it waits 3 seconds (for React to render), then runs `ExtractJs` — a JS snippet that scrapes percentages, reset times, and plan label from the DOM
3. The scraped data is parsed in `ParseExtracted()` and fired via the `DataReady` event
4. `MainWindow` receives the event and updates labels/progress bars directly

**Key files:**
- `UsageService.cs` — all data fetching logic; if Anthropic changes their UI, update `ExtractJs` and `ParseExtracted()`
- `MainWindow.xaml` / `MainWindow.xaml.cs` — entire UI; custom-drawn with `WindowStyle="None"` and `AllowsTransparency="True"`
- `Models.cs` — `UsageData` (fetched data) and `AppSettings` (persisted settings)
- `SettingsManager.cs` — reads/writes `%LOCALAPPDATA%\ClaudeUsageOverlay\settings.json`
- `StartupHelper.cs` — manages the Windows registry run key for launch-at-startup
- `App.xaml.cs` — enforces single instance via a named `Mutex`

## User data

All persistent state lives in `%LOCALAPPDATA%\ClaudeUsageOverlay\`:
- `settings.json` — window position, opacity, refresh interval, startup toggle
- `WebData\` — WebView2 session/cookies (deleting this forces re-login)

Deleting the entire folder resets everything including login.

## Auto-refresh

The timer interval is set once at startup from `AppSettings.RefreshIntervalMinutes` (default: 1). Clicking the ↻ button fetches immediately and resets the timer countdown. If a user's `settings.json` has an old value, delete the file while the app is closed to get the new default.

## Commit conventions

- Work on `main` branch only
- Author commits as `homielabsus` (not Claude)
- Always push with `git push -u origin main`
