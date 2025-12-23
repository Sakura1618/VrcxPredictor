using System.Collections.ObjectModel;
using System.Linq;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Wpf.Ui.Appearance;
using VrcxPredictor.App.Services;
using VrcxPredictor.Core.Analysis;
using VrcxPredictor.Core.Models;
using VrcxPredictor.Core.Time;
using VrcxPredictor.Data;
using System.IO;

namespace VrcxPredictor.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly AppConfig _cfg;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusText = "就绪";
    [ObservableProperty] private double _progress;

    [ObservableProperty] private string _dbPath = "";
    [ObservableProperty] private ObservableCollection<string> _tables = new();
    [ObservableProperty] private string? _selectedTable;

    [ObservableProperty] private string _displayName = "";
    [ObservableProperty] private ObservableCollection<string> _suggestedDisplayNames = new();

    [ObservableProperty] private ObservableCollection<string> _timeZones = new();
    [ObservableProperty] private string _timeZoneId = "Taipei Standard Time";

    [ObservableProperty] private ObservableCollection<string> _createdAtModes = new() { "utc", "local" };
    [ObservableProperty] private string _createdAtMode = "utc";

    [ObservableProperty] private int _halfLifeDays = 21;
    [ObservableProperty] private int _historyDays = 180;
    [ObservableProperty] private int _binMinutes = 15;
    [ObservableProperty] private bool _separateWeekdayWeekend = true;
    [ObservableProperty] private int _recentWeeks = 12;
    [ObservableProperty] private string _holidayDatesText = "";
    [ObservableProperty] private string _specialWorkdayDatesText = "";

    [ObservableProperty] private string _lastEventType = "—";
    [ObservableProperty] private DateTimeOffset _lastEventTime;
    [ObservableProperty] private bool _isOnlineNow;

    [ObservableProperty] private int _sessionCount;
    [ObservableProperty] private double _avgDurationHours;

    [ObservableProperty] private double _probNext2Hours;
    [ObservableProperty] private string _stabilityLabel = "—";
    [ObservableProperty] private double? _stabilityStdHours;
    [ObservableProperty] private string _avgIntervalText = "—";
    [ObservableProperty] private string _recentActiveHoursText = "—";
    [ObservableProperty] private string _confidenceText = "—";

    [ObservableProperty] private DateTimeOffset _bestWindowStart;
    [ObservableProperty] private DateTimeOffset _bestWindowEnd;
    [ObservableProperty] private double _bestWindowPeakProb;

    [ObservableProperty] private ObservableCollection<SessionRow> _sessions = new();
    [ObservableProperty] private AnalysisResult? _analysis;

    public MainViewModel(AppConfig cfg)
    {
        _cfg = cfg;

        DbPath = cfg.DbPath;
        TimeZoneId = cfg.TimeZoneId;
        CreatedAtMode = cfg.CreatedAtMode;
        HalfLifeDays = cfg.HalfLifeDays;
        HistoryDays = cfg.HistoryDays;
        BinMinutes = cfg.BinMinutes;
        SeparateWeekdayWeekend = cfg.SeparateWeekdayWeekend;
        RecentWeeks = cfg.RecentWeeks;
        HolidayDatesText = FormatDateList(cfg.HolidayDates);
        SpecialWorkdayDatesText = FormatDateList(cfg.SpecialWorkdayDates);

        foreach (var tz in TimeZoneInfo.GetSystemTimeZones().OrderBy(z => z.DisplayName))
            TimeZones.Add(tz.Id);

        if (!TimeZones.Contains(TimeZoneId))
            TimeZoneId = TimeZoneUtil.Resolve(TimeZoneId).Id;

        if (string.IsNullOrWhiteSpace(DbPath))
        {
            var guess = TryFindDefaultVrcxDb();
            if (!string.IsNullOrWhiteSpace(guess))
                DbPath = guess;
        }

        _ = RefreshTablesAsync();
    }

    public void PersistConfig()
    {
        _cfg.DbPath = DbPath;
        _cfg.TimeZoneId = TimeZoneId;
        _cfg.CreatedAtMode = CreatedAtMode;
        _cfg.HalfLifeDays = HalfLifeDays;
        _cfg.HistoryDays = HistoryDays;
        _cfg.BinMinutes = BinMinutes;
        _cfg.SeparateWeekdayWeekend = SeparateWeekdayWeekend;
        _cfg.RecentWeeks = RecentWeeks;
        _cfg.HolidayDates = NormalizeDateList(HolidayDatesText);
        _cfg.SpecialWorkdayDates = NormalizeDateList(SpecialWorkdayDatesText);
        _cfg.Save();
    }

    [RelayCommand]
    private void SaveConfig()
    {
        PersistConfig();
        SnackbarHost.Success("已保存", "配置已写入 %APPDATA%\\vrcx_predictor\\config.json");
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        var cur = ApplicationThemeManager.GetAppTheme();
        var next = cur == ApplicationTheme.Dark ? ApplicationTheme.Light : ApplicationTheme.Dark;
        ApplicationThemeManager.Apply(next);
        SnackbarHost.Info("主题", $"已切换为 {next}");
    }

    [RelayCommand]
    private async Task BrowseDbAsync()
    {
        var dlg = new OpenFileDialog
        {
            Title = "选择 VRCX.sqlite3",
            Filter = "SQLite DB (*.sqlite3;*.db;*.sqlite)|*.sqlite3;*.db;*.sqlite|All Files (*.*)|*.*"
        };

        if (dlg.ShowDialog() == true)
        {
            DbPath = dlg.FileName;
            await RefreshTablesAsync();
            SaveConfig();
        }
    }

    [RelayCommand]
    private async Task RefreshTablesAsync()
    {
        try
        {
            IsBusy = true;
            Progress = 0.1;
            StatusText = "读取表列表…";

            Tables.Clear();
            SuggestedDisplayNames.Clear();
            SelectedTable = null;

            if (string.IsNullOrWhiteSpace(DbPath) || !File.Exists(DbPath))
            {
                StatusText = "请先选择 VRCX.sqlite3";
                return;
            }

            var repo = new VrcxRepository(DbPath);
            var tables = await repo.ListTablesAsync();

            foreach (var t in tables)
                Tables.Add(t);

            SelectedTable = Tables.FirstOrDefault();
            Progress = 0.6;

            if (!string.IsNullOrWhiteSpace(SelectedTable))
            {
                StatusText = "读取常用显示名称（前200）…";
                var names = await repo.ListDistinctDisplayNamesAsync(SelectedTable, 200);
                foreach (var n in names)
                    SuggestedDisplayNames.Add(n);
            }

            Progress = 1.0;
            StatusText = $"就绪：{Tables.Count} 张表";
        }
        catch (Exception ex)
        {
            SnackbarHost.Error("读取失败", ex.Message);
            StatusText = "读取失败";
        }
        finally
        {
            IsBusy = false;
            Progress = 0;
        }
    }

    [RelayCommand(CanExecute = nameof(CanAnalyze))]
    private async Task AnalyzeAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        try
        {
            IsBusy = true;
            Progress = 0.05;
            StatusText = "读取用户事件…";

            if (string.IsNullOrWhiteSpace(SelectedTable))
                throw new InvalidOperationException("未选择数据表。");

            if (string.IsNullOrWhiteSpace(DisplayName))
                throw new InvalidOperationException("请输入显示名称。");

            var repo = new VrcxRepository(DbPath);

            var rawEvents = await repo.ReadUserEventsRawAsync(SelectedTable, DisplayName);
            Progress = 0.25;

            
if (rawEvents.Count == 0)
{
    StatusText = "未找到该用户记录（显示名称不匹配）";
    // 尝试用 LIKE 给出相似候选
    var hints = await repo.SearchDisplayNamesAsync(SelectedTable, DisplayName, 50);
    SuggestedDisplayNames.Clear();
    foreach (var n in hints)
        SuggestedDisplayNames.Add(n);

    if (hints.Count > 0)
    {
        var preview = string.Join("、", hints.Take(10));
        SnackbarHost.Warn("未找到记录", $"该表中没有完全匹配的显示名称。你是不是想选：{preview} …");
    }
    else
    {
        SnackbarHost.Warn("未找到记录", "该表中没有该显示名称。请从下拉列表选择或确认你选的表是否正确。");
    }
    return;
}

StatusText = "读取全局 上线 事件…";
            var onlineRaw = await repo.ReadAllOnlineCreatedAtAsync(SelectedTable);
            Progress = 0.35;

            StatusText = "解析时间…";
            var tz = TimeZoneUtil.Resolve(TimeZoneId);
            var onlineEventsParsed = new List<DateTimeOffset>(onlineRaw.Count);
            foreach (var s in onlineRaw)
            {
                _cts.Token.ThrowIfCancellationRequested();
                try { onlineEventsParsed.Add(CreatedAtParser.Parse(s, tz, CreatedAtMode)); }
                catch { }
            }
            Progress = 0.5;

            StatusText = "分析中…";
            var cfg = new AppConfig
            {
                DbPath = DbPath,
                TimeZoneId = TimeZoneId,
                CreatedAtMode = CreatedAtMode,
                HalfLifeDays = HalfLifeDays,
                HistoryDays = HistoryDays,
                BinMinutes = BinMinutes
            };

            var result = await Task.Run(() =>
            {
                _cts.Token.ThrowIfCancellationRequested();
                return Analyzer.AnalyzeUser(rawEvents, onlineEventsParsed, cfg, DateTimeOffset.Now);
            }, _cts.Token);

            Progress = 0.85;
            ApplyResult(result);

            StatusText = "完成";
            SnackbarHost.Success("完成", $"已分析：{DisplayName}");
        }
        catch (OperationCanceledException)
        {
            SnackbarHost.Warn("已取消", "分析任务已取消");
            StatusText = "已取消";
        }
        catch (Exception ex)
        {
            SnackbarHost.Error("分析失败", ex.Message);
            StatusText = "分析失败";
        }
        finally
        {
            IsBusy = false;
            Progress = 0;
            AnalyzeCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanAnalyze() => !IsBusy;

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();

    private void ApplyResult(AnalysisResult r)
    {
        Analysis = r;

        LastEventType = r.LastEventType;
        LastEventTime = r.LastEventTime;
        IsOnlineNow = r.IsOnlineNow;

        SessionCount = r.SessionCount;
        AvgDurationHours = r.AvgDurationHours;

        ProbNext2Hours = r.ProbNext2Hours;
        StabilityLabel = r.StabilityLabel;
        StabilityStdHours = r.StabilityStdHours;
        AvgIntervalText = r.AvgStartIntervalHours is null ? "—" : $"{r.AvgStartIntervalHours:0.0} 小时";
        RecentActiveHoursText = r.RecentActiveHoursText;
        ConfidenceText = $"样本 {r.SessionCount} | 置信度 {r.ConfidenceLabel}";

        BestWindowStart = r.WindowStart;
        BestWindowEnd = r.WindowEnd;
        BestWindowPeakProb = r.WindowPeakProb;

        Sessions.Clear();
        foreach (var s in r.Sessions.OrderByDescending(x => x.Start))
        {
            Sessions.Add(new SessionRow
            {
                Start = s.Start.LocalDateTime,
                End = s.End.LocalDateTime,
                DurationHours = s.DurationHours,
                IsOpen = s.IsOpen
            });
        }
    }

    private static string? TryFindDefaultVrcxDb()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var candidate = Path.Combine(appData, "VRCX", "VRCX.sqlite3");
        return File.Exists(candidate) ? candidate : null;
    }

    private static string FormatDateList(IReadOnlyList<string>? dates)
    {
        if (dates is null || dates.Count == 0)
            return "";

        return string.Join(", ", dates);
    }

    private static List<string> NormalizeDateList(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new List<string>();

        var parts = raw.Split(new[] { ',', ';', ' ', '\n', '\r', '\t', '，', '、' },
            StringSplitOptions.RemoveEmptyEntries);

        var list = new List<string>();
        foreach (var p in parts)
        {
            if (TryParseDate(p, out var d))
                list.Add(d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        return list.Distinct().OrderBy(x => x).ToList();
    }

    private static bool TryParseDate(string input, out DateOnly date)
    {
        var formats = new[] { "yyyy-MM-dd", "yyyy/M/d", "yyyy/MM/dd" };
        return DateOnly.TryParseExact(input, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
               || DateOnly.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }
}

public sealed class SessionRow
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public double DurationHours { get; set; }
    public bool IsOpen { get; set; }
}
