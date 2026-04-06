# Claude Usage Overlay

Lightweight always-on-top overlay for Windows that shows your Claude plan usage — current session % and weekly limits — in real time.

## Requirements

- Windows 10/11 (x64)
- .NET 8 SDK → https://dotnet.microsoft.com/download/dotnet/8.0
- WebView2 runtime (built into Windows 11 / Edge; auto-installed if missing)

## Build

Double-click **build.bat** → produces `dist\ClaudeUsage.exe`

Copy that single EXE anywhere — no installer, no runtime needed.

## First run

1. The login browser window appears automatically
2. Sign in to claude.ai as usual
3. Browser closes; overlay appears with your live usage data
4. Credentials are saved — you only log in once

## Controls

| Action           | How                                        |
|------------------|--------------------------------------------|
| Move overlay     | Left-click + drag                          |
| Expand / collapse| Click **Show more ▾** / **Show less ▴**   |
| Opacity          | Drag the ○──● slider (in expanded section) |
| Reset opacity    | Click **Reset to Default**                 |
| Refresh          | Click ↻ button or right-click → Refresh   |
| Run at startup   | Right-click → Run at Startup               |
| Exit             | Click ✕ button or right-click → Exit      |

## Layout

By default only the **Current session** bar is visible to keep the overlay compact. Click **Show more ▾** to reveal:
- Weekly limits (All models + Sonnet only), each with reset times
- Opacity slider with Reset to Default

## Auto-refresh

Fetches fresh data every **5 minutes** automatically.

## Data files

Settings and WebView2 session stored in:
`%LOCALAPPDATA%\ClaudeUsageOverlay\`

Delete that folder to reset (clears login session too).

## Adjusting selectors

`UsageService.cs` contains `ExtractJs` — the JS that reads percentages from the `claude.ai/settings/usage` DOM. If Anthropic changes their UI, update the regex patterns in `ParseExtracted()` to match new markup.
