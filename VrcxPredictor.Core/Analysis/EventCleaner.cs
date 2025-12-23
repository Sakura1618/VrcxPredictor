using VrcxPredictor.Core.Time;

namespace VrcxPredictor.Core.Analysis;

public static class EventCleaner
{
    private static readonly TimeSpan FutureSkew = TimeSpan.FromMinutes(5);

    public static List<(string Type, DateTimeOffset Time)> CleanRawEvents(
        IReadOnlyList<(string Type, string CreatedAt)> rawEvents,
        TimeZoneInfo tz,
        string createdAtMode,
        DateTimeOffset nowLocal)
    {
        var parsed = new List<(string Type, DateTimeOffset Time, int Index)>(rawEvents.Count);

        for (int i = 0; i < rawEvents.Count; i++)
        {
            var (type, createdAt) = rawEvents[i];
            var canonical = NormalizeType(type);
            if (canonical is null)
                continue;

            try
            {
                var t = CreatedAtParser.Parse(createdAt, tz, createdAtMode);
                if (t > nowLocal + FutureSkew)
                    continue;

                parsed.Add((canonical, t, i));
            }
            catch
            {
                // Skip malformed timestamps
            }
        }

        if (parsed.Count == 0)
            return new List<(string Type, DateTimeOffset Time)>();

        parsed.Sort((a, b) =>
        {
            var c = a.Time.CompareTo(b.Time);
            return c != 0 ? c : a.Index.CompareTo(b.Index);
        });

        var cleaned = new List<(string Type, DateTimeOffset Time)>(parsed.Count);
        foreach (var item in parsed)
        {
            if (cleaned.Count == 0)
            {
                cleaned.Add((item.Type, item.Time));
                continue;
            }

            var last = cleaned[^1];
            if (last.Type == item.Type)
            {
                cleaned[^1] = (item.Type, item.Time);
                continue;
            }

            cleaned.Add((item.Type, item.Time));
        }

        return cleaned;
    }

    private static string? NormalizeType(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
            return null;

        return type.Trim().ToLowerInvariant() switch
        {
            "online" => "Online",
            "offline" => "Offline",
            _ => null
        };
    }
}
