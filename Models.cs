namespace ClaudeUsageOverlay;

public class UsageData
{
    public string PlanLabel          { get; set; } = "—";
    public int    SessionPercent     { get; set; }
    public string SessionResetIn    { get; set; } = "—";
    public int    WeeklyAllPercent   { get; set; }
    public int    WeeklySonnetPercent{ get; set; }
    public string WeeklyResetAt     { get; set; } = "—";
    public DateTime FetchedAt       { get; set; } = DateTime.Now;
    public string?  Error           { get; set; }
}

public class AppSettings
{
    public double WindowOpacity          { get; set; } = 0.90;
    public double Left                   { get; set; } = 20;
    public double Top                    { get; set; } = 20;
    public bool   RunAtStartup           { get; set; } = false;
    public int    RefreshIntervalMinutes { get; set; } = 1;
}
