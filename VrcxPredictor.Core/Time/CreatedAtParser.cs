namespace VrcxPredictor.Core.Time;

public static class CreatedAtParser
{
    public static DateTimeOffset Parse(string createdAt, TimeZoneInfo tz, string mode)
    {
        mode = (mode?.Trim().ToLowerInvariant() == "local") ? "local" : "utc";

        if (string.IsNullOrWhiteSpace(createdAt))
            throw new FormatException("记录时间为空。");

        if (mode == "utc")
        {
            if (DateTimeOffset.TryParse(createdAt, out var dto))
                return TimeZoneInfo.ConvertTime(dto, tz);

            var dt = DateTime.SpecifyKind(DateTime.Parse(createdAt), DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTime(new DateTimeOffset(dt), tz);
        }
        else
        {
            var local = DateTime.SpecifyKind(DateTime.Parse(createdAt), DateTimeKind.Unspecified);
            var offset = tz.GetUtcOffset(local);
            return new DateTimeOffset(local, offset);
        }
    }
}
