namespace VrcxPredictor.Core.Time;

public static class TimeZoneUtil
{
    private static readonly Dictionary<string, string> IanaToWindows = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Asia/Taipei"] = "Taipei Standard Time",
        ["Etc/UTC"] = "UTC",
        ["UTC"] = "UTC",
        ["Asia/Shanghai"] = "China Standard Time",
        ["Asia/Hong_Kong"] = "China Standard Time",
        ["Asia/Tokyo"] = "Tokyo Standard Time",
        ["America/Los_Angeles"] = "Pacific Standard Time",
        ["America/New_York"] = "Eastern Standard Time",
        ["Europe/London"] = "GMT Standard Time",
        ["Europe/Berlin"] = "W. Europe Standard Time",
    };

    public static TimeZoneInfo Resolve(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return TimeZoneInfo.Local;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch
        {
            if (IanaToWindows.TryGetValue(id, out var win))
                return TimeZoneInfo.FindSystemTimeZoneById(win);

            return TimeZoneInfo.Local;
        }
    }
}
