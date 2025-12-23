using VrcxPredictor.Core.Models;

namespace VrcxPredictor.Core.Analysis;

public static class Metrics
{
    public static (string Label, double? StdHours) Stability(IReadOnlyList<Session> sessions, TimeZoneInfo tz)
    {
        if (sessions.Count < 8)
            return ("数据不足", null);

        var xs = sessions
            .Select(s => TimeZoneInfo.ConvertTime(s.Start, tz))
            .Select(t => t.Hour + t.Minute / 60.0)
            .ToArray();

        double mean = xs.Average();
        double var = xs.Select(v => (v - mean) * (v - mean)).Average();
        double std = Math.Sqrt(var);

        if (std < 1.5) return ("极高", std);
        if (std < 3.0) return ("规律", std);
        return ("随机", std);
    }

    public static double? AverageStartIntervalHours(IReadOnlyList<Session> sessions, int recentIntervals = 10)
    {
        if (sessions.Count < 2)
            return null;

        var ordered = sessions.OrderBy(s => s.Start).ToList();
        int take = Math.Min(recentIntervals, ordered.Count - 1);
        if (take <= 0)
            return null;

        double totalHours = 0;
        int count = 0;
        for (int i = ordered.Count - take; i < ordered.Count; i++)
        {
            var prev = ordered[i - 1].Start;
            var cur = ordered[i].Start;
            var h = (cur - prev).TotalHours;
            if (h > 0)
            {
                totalHours += h;
                count++;
            }
        }

        if (count == 0)
            return null;

        return totalHours / count;
    }

    public static string RecentActiveHoursText(IReadOnlyList<Session> sessions, TimeZoneInfo tz, DateTimeOffset nowLocal, int days = 7)
    {
        if (sessions.Count == 0)
            return "无";

        var since = nowLocal.AddDays(-days);
        var hourMinutes = new double[24];

        foreach (var s in sessions)
        {
            var start = TimeZoneInfo.ConvertTime(s.Start, tz);
            var end = TimeZoneInfo.ConvertTime(s.End, tz);
            if (end < since || start > nowLocal)
                continue;

            var cur = start < since ? since : start;
            var stop = end > nowLocal ? nowLocal : end;

            while (cur < stop)
            {
                var nextHour = new DateTimeOffset(cur.Year, cur.Month, cur.Day, cur.Hour, 0, 0, cur.Offset).AddHours(1);
                var segEnd = nextHour < stop ? nextHour : stop;
                hourMinutes[cur.Hour] += (segEnd - cur).TotalMinutes;
                cur = segEnd;
            }
        }

        var ranked = hourMinutes
            .Select((m, h) => (Hour: h, Minutes: m))
            .Where(x => x.Minutes > 0)
            .OrderByDescending(x => x.Minutes)
            .Take(4)
            .Select(x => x.Hour)
            .OrderBy(h => h)
            .ToList();

        if (ranked.Count == 0)
            return "无";

        var ranges = new List<string>();
        int rangeStart = ranked[0];
        int prev = ranked[0];
        for (int i = 1; i < ranked.Count; i++)
        {
            int cur = ranked[i];
            if (cur == prev + 1)
            {
                prev = cur;
                continue;
            }

            ranges.Add(rangeStart == prev ? $"{rangeStart:00}:00" : $"{rangeStart:00}:00-{(prev + 1):00}:00");
            rangeStart = prev = cur;
        }

        ranges.Add(rangeStart == prev ? $"{rangeStart:00}:00" : $"{rangeStart:00}:00-{(prev + 1):00}:00");
        return string.Join("，", ranges);
    }

    public static string ConfidenceLabel(IReadOnlyList<Session> sessions, DateTimeOffset nowLocal, int days = 90)
    {
        if (sessions.Count < 5)
            return "很低";
        if (sessions.Count < 15)
            return "偏低";
        if (sessions.Count < 30)
            return "中等";

        var since = nowLocal.AddDays(-days);
        var activeDays = sessions
            .Select(s => s.Start)
            .Where(t => t >= since)
            .Select(t => t.Date)
            .Distinct()
            .Count();

        if (activeDays < 10)
            return "偏低";

        return "较高";
    }
}
