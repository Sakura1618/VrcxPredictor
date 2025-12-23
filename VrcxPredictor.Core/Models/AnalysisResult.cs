namespace VrcxPredictor.Core.Models;

public sealed class AnalysisResult
{
    public required IReadOnlyList<Session> Sessions { get; init; }

    public required bool IsOnlineNow { get; init; }
    public required string LastEventType { get; init; }
    public required DateTimeOffset LastEventTime { get; init; }

    public required int SessionCount { get; init; }
    public required double AvgDurationHours { get; init; }
    public required double? AvgStartIntervalHours { get; init; }
    public required string RecentActiveHoursText { get; init; }
    public required string ConfidenceLabel { get; init; }

    /// <summary>Probability user will be online at least once within the next 2 hours.</summary>
    public required double ProbNext2Hours { get; init; } // 0~1

    public required string StabilityLabel { get; init; }
    public required double? StabilityStdHours { get; init; }

    public required DateTimeOffset WindowStart { get; init; }
    public required DateTimeOffset WindowEnd { get; init; }
    public required double WindowPeakProb { get; init; }

    /// <summary>Probability matrix [7, binsPerDay]. Monday=0.</summary>
    public required double[,] ProbabilityMatrix { get; init; }

    /// <summary>Global online activity matrix [7, binsPerDay] (normalized 0~1), optional.</summary>
    public double[,]? GlobalOnlineMatrix { get; init; }
}
