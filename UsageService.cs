using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Windows;

namespace ClaudeUsageOverlay;

public sealed class UsageService : IDisposable
{
    // Persistent session so user only logs in once
    private static readonly string WebDataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                     "ClaudeUsageOverlay", "WebData");

    private Window?   _host;
    private WebView2? _wv;
    private bool      _initialized;
    private bool      _inLogin;

    public event Action<UsageData>? DataReady;
    public event Action?            LoginNeeded;

    // ── Synchronous DOM extraction (no async needed; we Task.Delay before calling) ──────────
    private const string ExtractJs = """
        (function () {
            var leaves = Array.prototype.slice.call(document.querySelectorAll('*'))
                              .filter(function(e){ return e.children.length === 0; });

            var pcts = leaves
                .filter(function(e){ return /^\d{1,3}%\s*used$/i.test(e.textContent.trim()); })
                .map(function(e){ return e.textContent.trim(); });

            var resets = leaves
                .filter(function(e){ return /^Resets\b/i.test(e.textContent.trim()); })
                .map(function(e){ return e.textContent.trim(); });

            var planEl = leaves.filter(function(e){
                return /^max\s*\(\d+x\)$/i.test(e.textContent.trim());
            })[0] || leaves.filter(function(e){
                return /^(pro|free)$/i.test(e.textContent.trim());
            })[0];

            var sections = leaves
                .filter(function(e){
                    var t = e.textContent.trim();
                    return t === 'Current session' || t === 'All models' || t === 'Sonnet only';
                })
                .map(function(e){ return e.textContent.trim(); });

            return JSON.stringify({
                pcts:     pcts,
                resets:   resets,
                plan:     planEl ? planEl.textContent.trim() : '',
                sections: sections,
                url:      window.location.href
            });
        })()
        """;

    // ─────────────────────────────────────────────────────────────────────────
    public async Task InitAsync()
    {
        _host = new Window
        {
            Title         = "Sign in to Claude — Claude Usage Overlay",
            Width         = 960,  Height       = 680,
            Left          = -9999, Top          = -9999,
            WindowStyle   = WindowStyle.SingleBorderWindow,
            ShowInTaskbar = false,
            Opacity       = 0
        };

        _wv = new WebView2();
        _host.Content = _wv;
        _host.Show();   // Must show once to initialize WebView2

        var env = await CoreWebView2Environment.CreateAsync(userDataFolder: WebDataDir);
        await _wv.EnsureCoreWebView2Async(env);

        _wv.CoreWebView2.NavigationCompleted += OnNavCompleted;
        _initialized = true;
    }

    public void Fetch()
    {
        if (!_initialized || _wv is null) return;
        _wv.CoreWebView2.Navigate("https://claude.ai/settings/usage");
    }

    // ── Show the browser window so the user can log in ───────────────────────
    private void ShowLoginWindow()
    {
        if (_host is null) return;
        _inLogin              = true;
        _host.Width           = 960;
        _host.Height          = 680;
        _host.Left            = (SystemParameters.PrimaryScreenWidth  - 960) / 2;
        _host.Top             = (SystemParameters.PrimaryScreenHeight - 680) / 2;
        _host.ShowInTaskbar   = true;
        _host.Opacity         = 1;
        _host.Activate();
    }

    private void HideHost()
    {
        if (_host is null) return;
        _host.Left          = -9999;
        _host.Top           = -9999;
        _host.ShowInTaskbar = false;
        _host.Opacity       = 0;
        _inLogin            = false;
    }

    // ── Navigation handler ────────────────────────────────────────────────────
    private async void OnNavCompleted(object? _, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (_wv is null) return;
        var url = _wv.CoreWebView2.Source;

        // Redirect to login page
        if (!_inLogin && IsLoginUrl(url))
        {
            ShowLoginWindow();
            LoginNeeded?.Invoke();
            return;
        }

        // Came back from login → now fetch data
        if (_inLogin && !IsLoginUrl(url) && url.StartsWith("https://claude.ai"))
        {
            HideHost();
            await Task.Delay(1000);
            _wv.CoreWebView2.Navigate("https://claude.ai/settings/usage");
            return;
        }

        // On a claude.ai page and not in login flow → extract
        if (!_inLogin && url.Contains("claude.ai"))
        {
            await ExtractAndPublish();
        }
    }

    private static bool IsLoginUrl(string url) =>
        url.Contains("/login") || url.Contains("auth0") ||
        url.Contains("accounts.google") || url == "https://claude.ai/login";

    // ── Extract DOM data and fire event ───────────────────────────────────────
    private async Task ExtractAndPublish()
    {
        if (_wv is null) return;
        try
        {
            // Give React time to finish painting (NavigationCompleted fires early)
            await Task.Delay(3000);

            var raw   = await _wv.CoreWebView2.ExecuteScriptAsync(ExtractJs);
            // raw is a JSON-encoded string: "\"{ ... }\""
            var inner = JsonSerializer.Deserialize<string>(raw);
            var data  = ParseExtracted(inner ?? "{}");
            DataReady?.Invoke(data);
        }
        catch (Exception ex)
        {
            DataReady?.Invoke(new UsageData { Error = ex.Message, FetchedAt = DateTime.Now });
        }
        finally
        {
            HideHost();
        }
    }

    // ── DOM result model ──────────────────────────────────────────────────────
    private record Extracted(
        [property: JsonPropertyName("pcts")]     string[]? Pcts,
        [property: JsonPropertyName("resets")]   string[]? Resets,
        [property: JsonPropertyName("plan")]     string?   Plan,
        [property: JsonPropertyName("sections")] string[]? Sections,
        [property: JsonPropertyName("url")]      string?   Url
    );

    private static readonly JsonSerializerOptions Jo =
        new() { PropertyNameCaseInsensitive = true };

    private static UsageData ParseExtracted(string json)
    {
        var u = new UsageData { FetchedAt = DateTime.Now };
        try
        {
            var x = JsonSerializer.Deserialize<Extracted>(json, Jo);
            if (x is null) return u;

            // Plan label e.g. "Max (5x)"
            u.PlanLabel = CleanPlan(x.Plan ?? "");

            // Percentages in DOM order: session, weekly-all, weekly-sonnet
            var pcts = (x.Pcts ?? []).Select(ParsePct).ToArray();
            if (pcts.Length > 0) u.SessionPercent      = pcts[0];
            if (pcts.Length > 1) u.WeeklyAllPercent    = pcts[1];
            if (pcts.Length > 2) u.WeeklySonnetPercent = pcts[2];

            // Reset strings in DOM order: session-reset, weekly-reset
            var resets = x.Resets ?? [];
            if (resets.Length > 0) u.SessionResetIn = resets[0];
            if (resets.Length > 1) u.WeeklyResetAt  = resets[1];
        }
        catch (Exception ex) { u.Error = ex.Message; }
        return u;
    }

    private static int ParsePct(string s)
    {
        var m = Regex.Match(s, @"(\d+)");
        return m.Success ? Math.Clamp(int.Parse(m.Value), 0, 100) : 0;
    }

    private static string CleanPlan(string s)
    {
        var m = Regex.Match(s, @"Max\s*\(\d+x\)|Pro|Free", RegexOptions.IgnoreCase);
        return m.Success ? m.Value : (s.Length > 0 ? s : "—");
    }

    public void Dispose()
    {
        _wv?.Dispose();
        _host?.Close();
    }
}
