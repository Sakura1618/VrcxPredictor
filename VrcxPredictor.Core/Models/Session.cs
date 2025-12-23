namespace VrcxPredictor.Core.Models;

public sealed record Session(DateTimeOffset Start, DateTimeOffset End, double DurationHours, bool IsOpen);
