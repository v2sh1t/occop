using Occop.Core.Logging;
using Occop.Core.Performance;

namespace Occop.Core.Performance
{
    /// <summary>
    /// 性能警报事件参数
    /// Performance alert event args
    /// </summary>
    public class PerformanceAlertEventArgs : EventArgs
    {
        /// <summary>
        /// 警报类型
        /// Alert type
        /// </summary>
        public PerformanceAlertType AlertType { get; set; }

        /// <summary>
        /// 严重程度
        /// Severity
        /// </summary>
        public PerformanceAlertSeverity Severity { get; set; }

        /// <summary>
        /// 操作名称（如果适用）
        /// Operation name (if applicable)
        /// </summary>
        public string? OperationName { get; set; }

        /// <summary>
        /// 警报消息
        /// Alert message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 详细信息
        /// Details
        /// </summary>
        public Dictionary<string, object> Details { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// 时间戳
        /// Timestamp
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// 性能警报类型
    /// Performance alert type
    /// </summary>
    public enum PerformanceAlertType
    {
        /// <summary>性能降级 Performance degradation</summary>
        Degradation,
        /// <summary>高内存使用 High memory usage</summary>
        HighMemoryUsage,
        /// <summary>内存泄漏 Memory leak</summary>
        MemoryLeak,
        /// <summary>频繁GC Frequent GC</summary>
        FrequentGC,
        /// <summary>操作超时 Operation timeout</summary>
        OperationTimeout,
        /// <summary>高失败率 High failure rate</summary>
        HighFailureRate
    }

    /// <summary>
    /// 性能警报严重程度
    /// Performance alert severity
    /// </summary>
    public enum PerformanceAlertSeverity
    {
        /// <summary>信息 Info</summary>
        Info,
        /// <summary>警告 Warning</summary>
        Warning,
        /// <summary>严重 Critical</summary>
        Critical
    }

    /// <summary>
    /// 性能警报配置
    /// Performance alert configuration
    /// </summary>
    public class PerformanceAlertConfig
    {
        /// <summary>
        /// 是否启用警报
        /// Whether alerts are enabled
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 性能降级阈值（百分比）
        /// Degradation threshold (percentage)
        /// </summary>
        public double DegradationThreshold { get; set; } = 20.0;

        /// <summary>
        /// 高内存使用阈值（MB）
        /// High memory usage threshold (MB)
        /// </summary>
        public double HighMemoryThresholdMB { get; set; } = 500.0;

        /// <summary>
        /// 内存泄漏检测阈值（百分比）
        /// Memory leak detection threshold (percentage)
        /// </summary>
        public double MemoryLeakThreshold { get; set; } = 10.0;

        /// <summary>
        /// 操作超时阈值（毫秒）
        /// Operation timeout threshold (ms)
        /// </summary>
        public long OperationTimeoutMs { get; set; } = 5000;

        /// <summary>
        /// 高失败率阈值（百分比）
        /// High failure rate threshold (percentage)
        /// </summary>
        public double HighFailureRateThreshold { get; set; } = 10.0;

        /// <summary>
        /// 检查间隔（秒）
        /// Check interval (seconds)
        /// </summary>
        public int CheckIntervalSeconds { get; set; } = 60;
    }

    /// <summary>
    /// 性能警报管理器
    /// Performance alert manager
    /// </summary>
    public class PerformanceAlertManager : IDisposable
    {
        private readonly IPerformanceMonitor _monitor;
        private readonly IMemoryAnalyzer _analyzer;
        private readonly ILoggerService? _logger;
        private readonly PerformanceAlertConfig _config;
        private readonly Timer? _checkTimer;
        private readonly List<MemorySnapshot> _memorySnapshots;
        private bool _disposed = false;

        /// <summary>
        /// 警报事件
        /// Alert event
        /// </summary>
        public event EventHandler<PerformanceAlertEventArgs>? AlertRaised;

        /// <summary>
        /// 初始化性能警报管理器
        /// Initializes performance alert manager
        /// </summary>
        public PerformanceAlertManager(
            IPerformanceMonitor monitor,
            IMemoryAnalyzer analyzer,
            PerformanceAlertConfig? config = null,
            ILoggerService? logger = null)
        {
            _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
            _config = config ?? new PerformanceAlertConfig();
            _logger = logger;
            _memorySnapshots = new List<MemorySnapshot>();

            if (_config.Enabled)
            {
                _checkTimer = new Timer(
                    CheckPerformance,
                    null,
                    TimeSpan.FromSeconds(_config.CheckIntervalSeconds),
                    TimeSpan.FromSeconds(_config.CheckIntervalSeconds)
                );
            }
        }

        /// <summary>
        /// 立即检查性能
        /// Check performance immediately
        /// </summary>
        public void CheckNow()
        {
            CheckPerformance(null);
        }

        /// <summary>
        /// 检查性能（定时器回调）
        /// Check performance (timer callback)
        /// </summary>
        private void CheckPerformance(object? state)
        {
            if (!_config.Enabled || _disposed)
                return;

            try
            {
                // 检查内存使用
                CheckMemoryUsage();

                // 检查内存泄漏
                CheckMemoryLeak();

                // 检查操作性能
                CheckOperationPerformance();
            }
            catch (Exception ex)
            {
                _logger?.LogError(
                    LogCategory.Performance,
                    "Error during performance check",
                    exception: ex
                );
            }
        }

        /// <summary>
        /// 检查内存使用
        /// Check memory usage
        /// </summary>
        private void CheckMemoryUsage()
        {
            var snapshot = _monitor.GetMemorySnapshot();
            _memorySnapshots.Add(snapshot);

            // 限制快照历史数量
            while (_memorySnapshots.Count > 100)
            {
                _memorySnapshots.RemoveAt(0);
            }

            var analysis = _analyzer.Analyze(snapshot);

            // 检查高内存使用
            if (snapshot.WorkingSetMB > _config.HighMemoryThresholdMB)
            {
                RaiseAlert(new PerformanceAlertEventArgs
                {
                    AlertType = PerformanceAlertType.HighMemoryUsage,
                    Severity = snapshot.WorkingSetMB > _config.HighMemoryThresholdMB * 1.5
                        ? PerformanceAlertSeverity.Critical
                        : PerformanceAlertSeverity.Warning,
                    Message = $"High memory usage detected: {snapshot.WorkingSetMB:F2} MB",
                    Details = new Dictionary<string, object>
                    {
                        ["WorkingSetMB"] = snapshot.WorkingSetMB,
                        ["ThresholdMB"] = _config.HighMemoryThresholdMB,
                        ["ManagedHeapMB"] = snapshot.ManagedHeapMB
                    }
                });
            }

            // 检查内存分析问题
            foreach (var issue in analysis.Issues.Where(i => i.Severity >= MemoryIssueSeverity.Warning))
            {
                var alertSeverity = issue.Severity == MemoryIssueSeverity.Critical
                    ? PerformanceAlertSeverity.Critical
                    : PerformanceAlertSeverity.Warning;

                RaiseAlert(new PerformanceAlertEventArgs
                {
                    AlertType = issue.Type == MemoryIssueType.FrequentGC
                        ? PerformanceAlertType.FrequentGC
                        : PerformanceAlertType.HighMemoryUsage,
                    Severity = alertSeverity,
                    Message = issue.Description,
                    Details = new Dictionary<string, object>
                    {
                        ["Recommendation"] = issue.Recommendation,
                        ["IssueType"] = issue.Type.ToString()
                    }
                });
            }
        }

        /// <summary>
        /// 检查内存泄漏
        /// Check memory leak
        /// </summary>
        private void CheckMemoryLeak()
        {
            if (_memorySnapshots.Count < 3)
                return;

            var leaked = _analyzer.DetectMemoryLeak(_memorySnapshots, _config.MemoryLeakThreshold);

            if (leaked)
            {
                var first = _memorySnapshots.First();
                var last = _memorySnapshots.Last();
                var comparison = _analyzer.Compare(first, last);

                RaiseAlert(new PerformanceAlertEventArgs
                {
                    AlertType = PerformanceAlertType.MemoryLeak,
                    Severity = comparison.ManagedHeapGrowthPercentage > _config.MemoryLeakThreshold * 2
                        ? PerformanceAlertSeverity.Critical
                        : PerformanceAlertSeverity.Warning,
                    Message = $"Potential memory leak detected: {comparison.ManagedHeapGrowthPercentage:F1}% growth",
                    Details = new Dictionary<string, object>
                    {
                        ["InitialManagedHeapMB"] = first.ManagedHeapMB,
                        ["CurrentManagedHeapMB"] = last.ManagedHeapMB,
                        ["GrowthPercentage"] = comparison.ManagedHeapGrowthPercentage,
                        ["TimePeriod"] = comparison.TimeDelta.ToString()
                    }
                });
            }
        }

        /// <summary>
        /// 检查操作性能
        /// Check operation performance
        /// </summary>
        private void CheckOperationPerformance()
        {
            var allStats = _monitor.GetAllStatistics();

            foreach (var stat in allStats.Values)
            {
                // 检查性能降级
                if (_monitor.DetectDegradation(stat.OperationName, _config.DegradationThreshold))
                {
                    var percentage = (stat.RecentAverageDurationMs - stat.AverageDurationMs) / stat.AverageDurationMs * 100;

                    RaiseAlert(new PerformanceAlertEventArgs
                    {
                        AlertType = PerformanceAlertType.Degradation,
                        Severity = percentage > _config.DegradationThreshold * 2
                            ? PerformanceAlertSeverity.Critical
                            : PerformanceAlertSeverity.Warning,
                        OperationName = stat.OperationName,
                        Message = $"Performance degradation detected for {stat.OperationName}: {percentage:F1}% slower",
                        Details = new Dictionary<string, object>
                        {
                            ["AverageDurationMs"] = stat.AverageDurationMs,
                            ["RecentAverageDurationMs"] = stat.RecentAverageDurationMs,
                            ["DegradationPercentage"] = percentage
                        }
                    });
                }

                // 检查操作超时
                if (stat.MaxDurationMs > _config.OperationTimeoutMs)
                {
                    RaiseAlert(new PerformanceAlertEventArgs
                    {
                        AlertType = PerformanceAlertType.OperationTimeout,
                        Severity = stat.MaxDurationMs > _config.OperationTimeoutMs * 2
                            ? PerformanceAlertSeverity.Critical
                            : PerformanceAlertSeverity.Warning,
                        OperationName = stat.OperationName,
                        Message = $"Operation timeout detected for {stat.OperationName}: {stat.MaxDurationMs} ms",
                        Details = new Dictionary<string, object>
                        {
                            ["MaxDurationMs"] = stat.MaxDurationMs,
                            ["ThresholdMs"] = _config.OperationTimeoutMs
                        }
                    });
                }

                // 检查高失败率
                if (stat.TotalExecutions >= 10 && (100 - stat.SuccessRate) > _config.HighFailureRateThreshold)
                {
                    RaiseAlert(new PerformanceAlertEventArgs
                    {
                        AlertType = PerformanceAlertType.HighFailureRate,
                        Severity = (100 - stat.SuccessRate) > _config.HighFailureRateThreshold * 2
                            ? PerformanceAlertSeverity.Critical
                            : PerformanceAlertSeverity.Warning,
                        OperationName = stat.OperationName,
                        Message = $"High failure rate for {stat.OperationName}: {100 - stat.SuccessRate:F1}%",
                        Details = new Dictionary<string, object>
                        {
                            ["SuccessRate"] = stat.SuccessRate,
                            ["FailureCount"] = stat.FailureCount,
                            ["TotalExecutions"] = stat.TotalExecutions
                        }
                    });
                }
            }
        }

        /// <summary>
        /// 触发警报事件
        /// Raise alert event
        /// </summary>
        private void RaiseAlert(PerformanceAlertEventArgs args)
        {
            // 记录到日志
            var logLevel = args.Severity switch
            {
                PerformanceAlertSeverity.Critical => LogCategory.Security,
                PerformanceAlertSeverity.Warning => LogCategory.Performance,
                _ => LogCategory.Performance
            };

            var logMethod = args.Severity switch
            {
                PerformanceAlertSeverity.Critical => (Action<LogCategory, string, LogContext?, Exception?>)_logger!.LogError,
                PerformanceAlertSeverity.Warning => _logger!.LogWarning,
                _ => _logger!.LogInformation
            };

            _logger?.LogWarning(
                LogCategory.Performance,
                $"Performance Alert: {args.Message}",
                new LogContext
                {
                    CustomProperties = args.Details
                }
            );

            // 触发事件
            AlertRaised?.Invoke(this, args);
        }

        /// <summary>
        /// 释放资源
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _checkTimer?.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
