# VRCX Predictor 开发者文档（全流程）

本文描述：从 VRCX SQLite 表读取事件 → 清洗 → 构建会话 → 构建周占用矩阵 → 生成概率矩阵 → 输出预测与指标 的全流程。

项目分层：
- `VrcxPredictor.App`：WPF UI（wpf-ui + CommunityToolkit.Mvvm）
- `VrcxPredictor.Data`：SQLite 只读访问（Microsoft.Data.Sqlite + Dapper）
- `VrcxPredictor.Core`：纯分析逻辑（清洗/会话/概率/指标）

---

## 1. 数据源与查询（VrcxPredictor.Data）

### 1.1 表枚举
`VrcxRepository.ListTablesAsync()`
- 查询 `sqlite_master`
- 仅返回匹配：`usr%_feed_online_offline`

### 1.2 用户事件读取
`VrcxRepository.ReadUserEventsRawAsync(table, displayName)`
- SQL：
  - `SELECT type, created_at FROM {table} WHERE display_name = @name COLLATE NOCASE ORDER BY created_at;`
- 输出：`List<(string Type, string CreatedAt)>`

### 1.3 全局 Online 事件（用于全局热力图）
`VrcxRepository.ReadAllOnlineCreatedAtAsync(table)`
- SQL：`WHERE type='Online'`
- 输出：`List<string created_at>`

### 1.4 表名安全
`EnsureSafeTableName(table)`：
- 必须 `StartsWith("usr")` 且 `EndsWith("_feed_online_offline")`
- 只允许字母数字与 `_`

---

## 2. 时间解析与时区（VrcxPredictor.Core.Time）

### 2.1 时区解析
`TimeZoneUtil.Resolve(id)`
- 优先用 Windows 时区 ID（如 `Taipei Standard Time`）
- 支持少量 IANA -> Windows 映射（如 `Asia/Taipei`）

### 2.2 created_at 解析
`CreatedAtParser.Parse(createdAt, tz, mode)`
- `mode` 只支持：`utc`（默认）或 `local`
- `utc`：
  - 若 `DateTimeOffset.TryParse` 成功：按其 offset 解析后 `ConvertTime(dto, tz)`
  - 否则：`DateTime.Parse` 并强制 `DateTimeKind.Utc`，再转到 tz
- `local`：
  - 把字符串当 `DateTimeKind.Unspecified`
  - 用 `tz.GetUtcOffset(local)` 构造 `DateTimeOffset(local, offset)`

---

## 3. 事件清洗（VrcxPredictor.Core.Analysis.EventCleaner）

入口：
`EventCleaner.CleanRawEvents(rawEvents, tz, createdAtMode, nowLocal)`

清洗规则：
1) 类型归一化：`NormalizeType`
- 仅接受 `Online/Offline`（大小写不敏感，Trim）
- 其他类型丢弃

2) 时间解析：
- 调用 `CreatedAtParser.Parse`

3) 丢弃未来事件：
- `t > nowLocal + 5min` 丢弃（`FutureSkew = 5min`）

4) 稳定排序：
- 先按 `Time` 升序，时间相同按原始索引升序

5) 合并连续重复：
- 若相邻事件类型相同，用后者覆盖前者（保留最后一次）

输出：
- `List<(string Type, DateTimeOffset Time)>`

---

## 4. 历史窗口裁剪（Analyzer）

`Analyzer.AnalyzeUser(...)`：
- `tz = Resolve(cfg.TimeZoneId)`
- `nowLocal = ConvertTime(DateTimeOffset.Now, tz)`
- 清洗后，如果 `cfg.HistoryDays > 0`：
  - `cutoff = nowLocal.AddDays(-HistoryDays)`
  - 保留 `Time >= cutoff`

---

## 5. 会话构建（VrcxPredictor.Core.Analysis.SessionBuilder）

`SessionBuilder.Build(events, nowLocal)`

配对逻辑：
- 遇到 `Online`：`lastOnline = t`
- 遇到 `Offline` 且 `lastOnline != null`：形成会话 `[start=lastOnline, end=t]`，然后 `lastOnline = null`

过滤条件：
- `end >= start`
- `durH > 0.02`（约 1.2 分钟）
- `durH < 24`

开放会话：
- 末尾若 `lastOnline != null`，创建 `[lastOnline, nowLocal]`，同样受 0.02h~24h 过滤

输出：
- `List<Session>`，其中 `Session(IsOpen: bool, DurationHours: double)`

---

## 6. 周占用矩阵（VrcxPredictor.Core.Analysis.WeekOccupancyBuilder）

### 6.1 按周占用（用于个人概率矩阵）
`BuildByWeek(sessions, binMinutes, tz)`
- `binsPerDay = 24*60/binMinutes`
- 以 `FloorToBin(start)` 为 cursor，步长 `step=binMinutes`
- 遍历 `cursor < end`：
  - `weekStart = WeekStartMonday(cursor, tz)`（周一 00:00，Monday=0）
  - `wd = ((int)cursor.DayOfWeek + 6) % 7`（Sunday->6）
  - `bin = (hour*60 + minute)/binMinutes`
  - `mat[wd, bin] = true`

输出：
- `Dictionary<DateTimeOffset weekStart, bool[7, binsPerDay]>`

### 6.2 全局 Online 计数矩阵
`BuildOnlineCounts(onlineEvents, binMinutes, tz, now, historyDays)`
- 先按 historyDays 做 cutoff
- 每条在线事件落到 `(wd, bin)` 上累加计数
- 最后按最大值归一化到 0..1

> 注意：Analyzer 会先对 global online events 做一次 Clean：
> - `t <= nowLocal + 5min`
> - `.Distinct()`
> - `.OrderBy(t)`

---

## 7. 概率矩阵生成（VrcxPredictor.Core.Analysis.ProbabilityMatrixCalculator）

### 7.1 周权重（指数衰减 + 最近N周线性衰减）
`BuildWeekWeights(keys, now, halfLifeDays, recentWeeks)`
- `mid = weekStart + 3.5 days`
- `ageDays = max(0, now - mid)`
- 指数衰减：
  - `w = (halfLifeDays<=0) ? 1 : exp(-ageDays / halfLifeDays)`
- 最近 N 周限制（可选）：
  - `ageWeeks = ageDays/7`
  - 若 `ageWeeks >= recentWeeks`：`w = 0`
  - 否则：`w *= (recentWeeks - ageWeeks) / recentWeeks`

### 7.2 直接按周占用建模（不区分工作日/周末）
`BuildProbabilityMatrix(weekOcc, now, halfLifeDays, recentWeeks, sigmaTime=1.2, sigmaDay=0.6)`
- `p[d,b] = sum_w(occ_w[d,b] ? 1 : 0) / sum_w`
- `GaussianSmoother.Smooth(p, sigmaTime, sigmaDay)`
  - time axis：wrap（循环）
  - day axis：clamp（不循环）
- clamp 到 0..1

### 7.3 从 sessions 建模（可区分工作日/周末/节假日）
`BuildProbabilityMatrixFromSessions(sessions, binMinutes, tz, now, halfLifeDays, recentWeeks, separateWeekdayWeekend, holidayDates, specialWorkdayDates)`
- 若 `separateWeekdayWeekend == false`：
  - 先 `BuildByWeek` 再 `BuildProbabilityMatrix(...)`
- 若 `true`：
  - 对每个 session 逐 bin 扫描，落到两个桶：
    - Weekday[b] / Weekend[b]
  - Weekend 规则：
    - specialWorkdayDates 命中 → 视为工作日
    - holidayDates 命中 → 视为周末
    - 否则：Sat/Sun 视为周末
  - 对每周桶按 weights 累加，除以 denom 得到 `weekday[b]` / `weekend[b]`
  - 映射回 7 天矩阵：Mon-Fri 用 weekday，Sat-Sun 用 weekend
  - 仅对时间轴做平滑：`GaussianSmoother.Smooth(p, sigmaTime, sigmaDay=0)`

---

## 8. 预测输出

### 8.1 未来 N 小时至少上线一次的概率
`ProbNextHours(p, now, hours, binMinutes, tz)`
- `steps = round(hours*60/binMinutes)`
- 从 `FloorToBin(nowLocal)` 开始，每步 `+binMinutes`
- 对每个落点取 `pi = p[weekday, bin]`
- 计算：
  - `q = ∏(1 - pi)`（每个 bin 都不上线的概率）
  - `P = 1 - q`
- clamp 到 0..1

### 8.2 未来 24h 的最佳上线窗口
`BestWindowNext24h(p, now, binMinutes, tz)`
- 扫描 `horizon = 24*60/binMinutes` 步
- 找到最大 `pi` 的时刻 `bestT`
- 返回窗口：
  - `(bestT - step, bestT + step, peakProb)`

---

## 9. 指标（Metrics）

### 9.1 规律性
`Metrics.Stability(sessions, tz)`
- sessions < 8：`数据不足`
- 取每个 session 的本地开始时刻：`hour + minute/60`
- 标准差 std：
  - `< 1.5`：极高
  - `< 3.0`：规律
  - else：随机

### 9.2 平均开始间隔（最近10个间隔默认）
`AverageStartIntervalHours(sessions, recentIntervals=10)`
- 按 start 排序
- 取最后 `take=min(recentIntervals, count-1)` 个间隔
- 平均正间隔小时数

### 9.3 近7天活跃时段
`RecentActiveHoursText(sessions, tz, nowLocal, days=7)`
- 将最近 7 天内会话覆盖的分钟数累计到 24 个小时桶
- 取分钟数 Top4 的小时，排序后合并连续小时区间输出文本

### 9.4 置信度
`ConfidenceLabel(sessions, nowLocal, days=90)`
- 按 session 数：
  - <5：很低
  - <15：偏低
  - <30：中等
  - >=30：进一步检查最近90天活跃天数（start 日期 distinct）
    - activeDays < 10：偏低
    - else：较高

---

## 10. UI -> Core 的调用链（当前实现）

`MainViewModel.AnalyzeAsync()`：
1) `repo.ReadUserEventsRawAsync(table, displayName)`
2) `repo.ReadAllOnlineCreatedAtAsync(table)` 并对每条 `created_at` 做 `CreatedAtParser.Parse`
3) 组装 `cfg` 并调用：
   `Analyzer.AnalyzeUser(rawEvents, onlineEventsParsed, cfg, DateTimeOffset.Now)`
4) `ApplyResult(result)` 更新 UI（Dashboard / Sessions / Heatmaps）

---

## 11. 配置持久化

`AppConfig.LoadOrCreate()`：
- `%APPDATA%\vrcx_predictor\config.json`
- 不存在就写默认配置
- 解析失败则回退默认并覆盖

`MainViewModel.SaveConfig()`：
- 把 UI 当前值写回 `_cfg` 并保存

---

## 12. ⚠ 配置传递注意点（重要）

在当前 `MainViewModel.AnalyzeAsync()` 中，传入 Analyzer 的 `cfg` 只设置了：
- `DbPath / TimeZoneId / CreatedAtMode / HalfLifeDays / HistoryDays / BinMinutes`

但 `Analyzer` 内部会读取这些字段：
- `SeparateWeekdayWeekend / RecentWeeks / HolidayDates / SpecialWorkdayDates`

如果你希望 UI 上的这些选项实际参与计算，需要在构造 `cfg` 时把它们也赋值进去，例如：
- `SeparateWeekdayWeekend = SeparateWeekdayWeekend`
- `RecentWeeks = RecentWeeks`
- `HolidayDates = NormalizeDateList(HolidayDatesText)`（或直接使用已保存的 `_cfg.HolidayDates`）
- `SpecialWorkdayDates = ...`

否则 Analyzer 会使用 `AppConfig` 的默认值（`SeparateWeekdayWeekend=true, RecentWeeks=12, HolidayDates=[] ...`）。

---

## 13. 构建与运行

### VS 2022
打开 `VrcxPredictor.sln`，启动 `VrcxPredictor.App`。

### dotnet CLI
```bash
dotnet restore
dotnet build -c Release
dotnet run --project VrcxPredictor.App -c Release
