using VrcxPredictor.Core.Models;

namespace VrcxPredictor.Core.Analysis;

public static class WeekOccupancyBuilder
{
    public static Dictionary<DateTimeOffset, bool[,]> BuildByWeek(
        IReadOnlyList<Session> sessions,
        int binMinutes,
        TimeZoneInfo tz)
    {
        int binsPerDay = 24 * 60 / binMinutes;
        var byWeek = new Dictionary<DateTimeOffset, bool[,]>();

        TimeSpan step = TimeSpan.FromMinutes(binMinutes);

        foreach (var s in sessions)
        {
            var cursor = FloorToBin(s.Start, binMinutes);
            var end = s.End;

            while (cursor < end)
            {
                var weekStart = WeekStartMonday(cursor, tz);

                if (!byWeek.TryGetValue(weekStart, out var mat))
                {
                    mat = new bool[7, binsPerDay];
                    byWeek[weekStart] = mat;
                }

                int wd = ((int)cursor.DayOfWeek + 6) % 7; // Monday=0
                int bin = (cursor.Hour * 60 + cursor.Minute) / binMinutes;
                mat[wd, bin] = true;

                cursor = cursor.Add(step);
            }
        }

        return byWeek;
    }

    public static double[,] BuildOnlineCounts(
        IReadOnlyList<DateTimeOffset> onlineEvents,
        int binMinutes,
        TimeZoneInfo tz,
        DateTimeOffset now,
        int historyDays)
    {
        int binsPerDay = 24 * 60 / binMinutes;
        var m = new double[7, binsPerDay];

        DateTimeOffset cutoff = historyDays > 0
            ? TimeZoneInfo.ConvertTime(now, tz).AddDays(-historyDays)
            : DateTimeOffset.MinValue;

        foreach (var evt in onlineEvents)
        {
            var t = TimeZoneInfo.ConvertTime(evt, tz);
            if (t < cutoff) continue;

            int wd = ((int)t.DayOfWeek + 6) % 7;
            int bin = (t.Hour * 60 + t.Minute) / binMinutes;
            m[wd, bin] += 1.0;
        }

        double max = 0.0;
        for (int d = 0; d < 7; d++)
        for (int b = 0; b < binsPerDay; b++)
            if (m[d, b] > max) max = m[d, b];

        if (max <= 0) return m;

        for (int d = 0; d < 7; d++)
        for (int b = 0; b < binsPerDay; b++)
            m[d, b] = m[d, b] / max;

        return m;
    }

    private static DateTimeOffset FloorToBin(DateTimeOffset dt, int binMinutes)
    {
        int m = (dt.Minute / binMinutes) * binMinutes;
        return new DateTimeOffset(dt.Year, dt.Month, dt.Day, dt.Hour, m, 0, dt.Offset);
    }

    private static DateTimeOffset WeekStartMonday(DateTimeOffset dt, TimeZoneInfo tz)
    {
        var local = TimeZoneInfo.ConvertTime(dt, tz);
        int wd = ((int)local.DayOfWeek + 6) % 7; // Monday=0

        var mondayDate = local.Date.AddDays(-wd);
        var mondayLocal = new DateTime(mondayDate.Year, mondayDate.Month, mondayDate.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var offset = tz.GetUtcOffset(mondayLocal);

        return new DateTimeOffset(mondayLocal, offset);
    }
}
