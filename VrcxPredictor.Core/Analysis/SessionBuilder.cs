using VrcxPredictor.Core.Models;

namespace VrcxPredictor.Core.Analysis;

public static class SessionBuilder
{
    public static List<Session> Build(IReadOnlyList<(string Type, DateTimeOffset Time)> events, DateTimeOffset now)
    {
        var sessions = new List<Session>();
        DateTimeOffset? lastOnline = null;

        foreach (var (type, t) in events)
        {
            if (type == "Online")
            {
                lastOnline = t;
                continue;
            }

            if (type == "Offline" && lastOnline is not null)
            {
                var start = lastOnline.Value;
                var end = t;
                var durH = (end - start).TotalHours;

                if (end >= start && durH > 0.02 && durH < 24)
                    sessions.Add(new Session(start, end, durH, IsOpen: false));

                lastOnline = null;
            }
        }

        if (lastOnline is not null)
        {
            var start = lastOnline.Value;
            var end = now;
            var durH = (end - start).TotalHours;
            if (durH > 0.02 && durH < 24)
                sessions.Add(new Session(start, end, durH, IsOpen: true));
        }

        return sessions;
    }
}
