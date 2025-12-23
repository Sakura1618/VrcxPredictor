using VrcxPredictor.Core.Models;
using VrcxPredictor.Core.Time;

namespace VrcxPredictor.Core.Analysis;

public static class Analyzer
{
    public static AnalysisResult AnalyzeUser(
        IReadOnlyList<(string Type, string CreatedAt)> rawEvents,
        IReadOnlyList<DateTimeOffset>? globalOnlineEvents,
        AppConfig cfg,
        DateTimeOffset? nowOverride = null)
    {
        if (rawEvents.Count == 0)
            throw new InvalidOperationException("未找到该用户记录。");

        var tz = TimeZoneUtil.Resolve(cfg.TimeZoneId);
        var now = nowOverride ?? DateTimeOffset.Now;
        var nowLocal = TimeZoneInfo.ConvertTime(now, tz);

        var parsed = EventCleaner.CleanRawEvents(rawEvents, tz, cfg.CreatedAtMode, nowLocal);

        if (cfg.HistoryDays > 0)
        {
            var cutoff = nowLocal.AddDays(-cfg.HistoryDays);
            parsed = parsed.Where(x => x.Time >= cutoff).ToList();
        }

        if (parsed.Count == 0)
            throw new InvalidOperationException("筛选后数据为空（可能 history_days 太小或时间解析失败）。");

        var sessions = SessionBuilder.Build(parsed.Select(x => (x.Type, x.Time)).ToList(), nowLocal);

        var last = parsed[^1];
        bool isOnlineNow = last.Type == "Online";
        double avgDur = sessions.Count == 0 ? 0 : sessions.Average(s => s.DurationHours);

        int binsPerDay = 24 * 60 / cfg.BinMinutes;

        if (sessions.Count < 5)
        {
            var avgInterval = Metrics.AverageStartIntervalHours(sessions);
            var recentActive = Metrics.RecentActiveHoursText(sessions, tz, nowLocal);
            var confidence = Metrics.ConfidenceLabel(sessions, nowLocal);

            return new AnalysisResult
            {
                Sessions = sessions,
                IsOnlineNow = isOnlineNow,
                LastEventType = last.Type,
                LastEventTime = last.Time,
                SessionCount = sessions.Count,
                AvgDurationHours = avgDur,
                AvgStartIntervalHours = avgInterval,
                RecentActiveHoursText = recentActive,
                ConfidenceLabel = confidence,
                ProbNext2Hours = 0,
                StabilityLabel = "数据不足",
                StabilityStdHours = null,
                WindowStart = nowLocal,
                WindowEnd = nowLocal,
                WindowPeakProb = 0,
                ProbabilityMatrix = new double[7, binsPerDay],
                GlobalOnlineMatrix = globalOnlineEvents is null
                    ? null
                    : WeekOccupancyBuilder.BuildOnlineCounts(CleanGlobalOnline(globalOnlineEvents, nowLocal), cfg.BinMinutes, tz, nowLocal, cfg.HistoryDays)
            };
        }

        var holidayDates = BuildDateSet(cfg.HolidayDates);
        var specialWorkdayDates = BuildDateSet(cfg.SpecialWorkdayDates);

        var p = ProbabilityMatrixCalculator.BuildProbabilityMatrixFromSessions(
            sessions,
            cfg.BinMinutes,
            tz,
            nowLocal,
            cfg.HalfLifeDays,
            cfg.RecentWeeks,
            cfg.SeparateWeekdayWeekend,
            holidayDates,
            specialWorkdayDates);

        var p2h = ProbabilityMatrixCalculator.ProbNextHours(p, now, 2.0, cfg.BinMinutes, tz);
        var (label, std) = Metrics.Stability(sessions, tz);
        var (ws, we, peak) = ProbabilityMatrixCalculator.BestWindowNext24h(p, now, cfg.BinMinutes, tz);
        var avgIntervalFull = Metrics.AverageStartIntervalHours(sessions);
        var recentActiveFull = Metrics.RecentActiveHoursText(sessions, tz, nowLocal);
        var confidenceFull = Metrics.ConfidenceLabel(sessions, nowLocal);

        return new AnalysisResult
        {
            Sessions = sessions,
            IsOnlineNow = isOnlineNow,
            LastEventType = last.Type,
            LastEventTime = last.Time,
            SessionCount = sessions.Count,
            AvgDurationHours = avgDur,
            AvgStartIntervalHours = avgIntervalFull,
            RecentActiveHoursText = recentActiveFull,
            ConfidenceLabel = confidenceFull,
            ProbNext2Hours = p2h,
            StabilityLabel = label,
            StabilityStdHours = std,
            WindowStart = ws,
            WindowEnd = we,
            WindowPeakProb = peak,
            ProbabilityMatrix = p,
            GlobalOnlineMatrix = globalOnlineEvents is null
                ? null
                : WeekOccupancyBuilder.BuildOnlineCounts(CleanGlobalOnline(globalOnlineEvents, nowLocal), cfg.BinMinutes, tz, nowLocal, cfg.HistoryDays)
        };
    }

    private static IReadOnlyList<DateTimeOffset> CleanGlobalOnline(
        IReadOnlyList<DateTimeOffset> events,
        DateTimeOffset nowLocal)
    {
        return events
            .Where(t => t <= nowLocal.AddMinutes(5))
            .Distinct()
            .OrderBy(t => t)
            .ToList();
    }

    private static HashSet<DateOnly> BuildDateSet(IReadOnlyList<string>? dates)
    {
        var set = new HashSet<DateOnly>();
        if (dates is null)
            return set;

        foreach (var s in dates)
        {
            if (DateOnly.TryParse(s, out var d))
                set.Add(d);
        }

        return set;
    }
}
