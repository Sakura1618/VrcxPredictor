using System.Text.Json;
using System.IO;

namespace VrcxPredictor.Core.Models;

public sealed class AppConfig
{
    public string DbPath { get; set; } = "";

    /// <summary>
    /// Windows timezone id preferred (e.g. "Taipei Standard Time"). We accept IANA too (e.g. "Asia/Taipei") and try to map.
    /// </summary>
    public string TimeZoneId { get; set; } = "Taipei Standard Time";

    /// <summary>
    /// "utc" or "local" - how to interpret the created_at string in VRCX.
    /// </summary>
    public string CreatedAtMode { get; set; } = "utc";

    public int HalfLifeDays { get; set; } = 21;
    public int HistoryDays { get; set; } = 180;
    public int BinMinutes { get; set; } = 15;
    public bool SeparateWeekdayWeekend { get; set; } = true;
    public int RecentWeeks { get; set; } = 12;
    public List<string> HolidayDates { get; set; } = new();
    public List<string> SpecialWorkdayDates { get; set; } = new();

    public static string DefaultConfigDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "vrcx_predictor");

    public static string DefaultConfigPath => Path.Combine(DefaultConfigDir, "config.json");

    public static AppConfig LoadOrCreate()
    {
        Directory.CreateDirectory(DefaultConfigDir);

        if (!File.Exists(DefaultConfigPath))
        {
            var cfg = new AppConfig();
            cfg.Save();
            return cfg;
        }

        try
        {
            var json = File.ReadAllText(DefaultConfigPath);
            return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        }
        catch
        {
            var cfg = new AppConfig();
            cfg.Save();
            return cfg;
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(DefaultConfigDir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(DefaultConfigPath, json);
    }
}
