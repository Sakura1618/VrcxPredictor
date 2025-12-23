using VrcxPredictor.Core.Models;

namespace VrcxPredictor.Core.Analysis;

public static class ProbabilityMatrixCalculator
{
    public static double[,] BuildProbabilityMatrix(
        Dictionary<DateTimeOffset, bool[,]> weekOcc,
        DateTimeOffset now,
        int halfLifeDays,
        int recentWeeks,
        double sigmaTime = 1.2,
        double sigmaDay = 0.6)
    {
        if (weekOcc.Count == 0)
            throw new InvalidOperationException("No sessions/occupancy available.");

        int binsPerDay = weekOcc.First().Value.GetLength(1);
        var keys = weekOcc.Keys.OrderBy(x => x).ToArray();

        double[] weights = BuildWeekWeights(keys, now, halfLifeDays, recentWeeks);

        double denom = weights.Sum();
        if (denom <= 0) denom = 1;

        var p = new double[7, binsPerDay];

        for (int wi = 0; wi < keys.Length; wi++)
        {
            var mat = weekOcc[keys[wi]];
            double w = weights[wi];

            for (int d = 0; d < 7; d++)
            for (int b = 0; b < binsPerDay; b++)
                p[d, b] += (mat[d, b] ? 1.0 : 0.0) * w;
        }

        for (int d = 0; d < 7; d++)
        for (int b = 0; b < binsPerDay; b++)
            p[d, b] = Math.Clamp(p[d, b] / denom, 0.0, 1.0);

        p = GaussianSmoother.Smooth(p, sigmaTime, sigmaDay);

        for (int d = 0; d < 7; d++)
        for (int b = 0; b < binsPerDay; b++)
            p[d, b] = Math.Clamp(p[d, b], 0.0, 1.0);

        return p;
    }

    public static double[,] BuildProbabilityMatrixFromSessions(
        IReadOnlyList<Session> sessions,
        int binMinutes,
        TimeZoneInfo tz,
        DateTimeOffset now,
        int halfLifeDays,
        int recentWeeks,
        bool separateWeekdayWeekend,
        IReadOnlyCollection<DateOnly> holidayDates,
        IReadOnlyCollection<DateOnly> specialWorkdayDates,
        double sigmaTime = 1.2)
    {
        var weekOcc = WeekOccupancyBuilder.BuildByWeek(sessions, binMinutes, tz);
        if (!separateWeekdayWeekend)
            return BuildProbabilityMatrix(weekOcc, now, halfLifeDays, recentWeeks, sigmaTime, sigmaDay: 0.6);

        if (weekOcc.Count == 0)
            throw new InvalidOperationException("No sessions/occupancy available.");

        int binsPerDay = 24 * 60 / binMinutes;
        var keys = weekOcc.Keys.OrderBy(x => x).ToArray();
        double[] weights = BuildWeekWeights(keys, now, halfLifeDays, recentWeeks);

        double denom = weights.Sum();
        if (denom <= 0) denom = 1;

        var weekday = new double[binsPerDay];
        var weekend = new double[binsPerDay];

        var holidaySet = holidayDates is HashSet<DateOnly> h ? h : new HashSet<DateOnly>(holidayDates);
        var specialSet = specialWorkdayDates is HashSet<DateOnly> sw ? sw : new HashSet<DateOnly>(specialWorkdayDates);

        var byWeek = new Dictionary<DateTimeOffset, (bool[] Weekday, bool[] Weekend)>();
        TimeSpan step = TimeSpan.FromMinutes(binMinutes);

        foreach (var session in sessions)
        {
            var cursor = FloorToBin(session.Start, binMinutes);
            var end = session.End;

            while (cursor < end)
            {
                var weekStart = WeekStartMonday(cursor, tz);
                if (!byWeek.TryGetValue(weekStart, out var buckets))
                {
                    buckets = (new bool[binsPerDay], new bool[binsPerDay]);
                    byWeek[weekStart] = buckets;
                }

                int bin = (cursor.Hour * 60 + cursor.Minute) / binMinutes;
                bool isWeekend = IsWeekendOrHoliday(cursor, tz, holidaySet, specialSet);

                if (isWeekend)
                    buckets.Weekend[bin] = true;
                else
                    buckets.Weekday[bin] = true;

                cursor = cursor.Add(step);
            }
        }

        for (int i = 0; i < keys.Length; i++)
        {
            var w = weights[i];
            if (w <= 0) continue;

            if (!byWeek.TryGetValue(keys[i], out var buckets))
                continue;

            for (int b = 0; b < binsPerDay; b++)
            {
                if (buckets.Weekday[b]) weekday[b] += w;
                if (buckets.Weekend[b]) weekend[b] += w;
            }
        }

        var p = new double[7, binsPerDay];
        for (int d = 0; d < 7; d++)
        {
            bool isWeekendDay = d >= 5;
            for (int b = 0; b < binsPerDay; b++)
            {
                double v = isWeekendDay ? weekend[b] : weekday[b];
                p[d, b] = Math.Clamp(v / denom, 0.0, 1.0);
            }
        }

        p = GaussianSmoother.Smooth(p, sigmaTime, sigmaDay: 0.0);

        for (int d = 0; d < 7; d++)
        for (int b = 0; b < binsPerDay; b++)
            p[d, b] = Math.Clamp(p[d, b], 0.0, 1.0);

        return p;
    }

    public static double ProbNextHours(double[,] p, DateTimeOffset now, double hours, int binMinutes, TimeZoneInfo tz)
    {
        int binsPerDay = p.GetLength(1);
        int steps = (int)Math.Round((hours * 60.0) / binMinutes);
        if (steps <= 0) return 0;

        var start = FloorToBin(TimeZoneInfo.ConvertTime(now, tz), binMinutes);
        TimeSpan step = TimeSpan.FromMinutes(binMinutes);

        double q = 1.0;
        var cur = start;

        for (int i = 0; i < steps; i++)
        {
            cur = cur.Add(step);
            int wd = ((int)cur.DayOfWeek + 6) % 7;
            int b = ((cur.Hour * 60 + cur.Minute) / binMinutes) % binsPerDay;
            double pi = p[wd, b];
            q *= (1.0 - pi);
        }

        return Math.Clamp(1.0 - q, 0.0, 1.0);
    }

    public static (DateTimeOffset Start, DateTimeOffset End, double Peak) BestWindowNext24h(
        double[,] p, DateTimeOffset now, int binMinutes, TimeZoneInfo tz)
    {
        int binsPerDay = p.GetLength(1);
        int horizon = (24 * 60) / binMinutes;

        var start = FloorToBin(TimeZoneInfo.ConvertTime(now, tz), binMinutes);
        TimeSpan step = TimeSpan.FromMinutes(binMinutes);

        DateTimeOffset bestT = start;
        double bestP = -1;

        var cur = start;
        for (int i = 0; i < horizon; i++)
        {
            cur = cur.Add(step);
            int wd = ((int)cur.DayOfWeek + 6) % 7;
            int b = ((cur.Hour * 60 + cur.Minute) / binMinutes) % binsPerDay;
            double pi = p[wd, b];
            if (pi > bestP)
            {
                bestP = pi;
                bestT = cur;
            }
        }

        return (bestT - step, bestT + step, Math.Clamp(bestP, 0, 1));
    }

    private static DateTimeOffset FloorToBin(DateTimeOffset dt, int binMinutes)
    {
        int m = (dt.Minute / binMinutes) * binMinutes;
        return new DateTimeOffset(dt.Year, dt.Month, dt.Day, dt.Hour, m, 0, dt.Offset);
    }

    private static double[] BuildWeekWeights(DateTimeOffset[] keys, DateTimeOffset now, int halfLifeDays, int recentWeeks)
    {
        var weights = new double[keys.Length];

        for (int i = 0; i < keys.Length; i++)
        {
            var ws = keys[i];
            var mid = ws.AddDays(3.5);
            var ageDays = (now - mid).TotalDays;
            if (ageDays < 0) ageDays = 0;

            double w = (halfLifeDays <= 0) ? 1.0 : Math.Exp(-ageDays / halfLifeDays);

            if (recentWeeks > 0)
            {
                var ageWeeks = ageDays / 7.0;
                if (ageWeeks >= recentWeeks)
                    w = 0.0;
                else
                    w *= (recentWeeks - ageWeeks) / recentWeeks;
            }

            weights[i] = w;
        }

        return weights;
    }

    private static bool IsWeekendOrHoliday(
        DateTimeOffset dt,
        TimeZoneInfo tz,
        HashSet<DateOnly> holidays,
        HashSet<DateOnly> specialWorkdays)
    {
        var local = TimeZoneInfo.ConvertTime(dt, tz);
        var date = DateOnly.FromDateTime(local.DateTime);

        if (specialWorkdays.Contains(date))
            return false;
        if (holidays.Contains(date))
            return true;

        return local.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
    }

    private static DateTimeOffset WeekStartMonday(DateTimeOffset dt, TimeZoneInfo tz)
    {
        var local = TimeZoneInfo.ConvertTime(dt, tz);
        int wd = ((int)local.DayOfWeek + 6) % 7;

        var mondayDate = local.Date.AddDays(-wd);
        var mondayLocal = new DateTime(mondayDate.Year, mondayDate.Month, mondayDate.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var offset = tz.GetUtcOffset(mondayLocal);

        return new DateTimeOffset(mondayLocal, offset);
    }
}
