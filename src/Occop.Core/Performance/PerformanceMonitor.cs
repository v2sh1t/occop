using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Occop.Core.Logging;

namespace Occop.Core.Performance
{
    /// <summary>
    /// 性能监控器实现
    /// Performance monitor implementation
    /// </summary>
    public class PerformanceMonitor : IPerformanceMonitor
    {
        private readonly ILoggerService? _logger;
        private readonly ConcurrentDictionary<string, OperationStatisticsInternal> _statistics;
        private readonly ConcurrentQueue<MemorySnapshot> _memorySnapshots;
        private readonly int _maxSnapshotsToKeep = 100;
        private readonly int _recentExecutionsCount = 10;

        /// <summary>
        /// 初始化性能监控器
        /// Initializes performance monitor
        /// </summary>
        /// <param name="logger">日志服务（可选） Logger service (optional)</param>
        public PerformanceMonitor(ILoggerService? logger = null)
        {
            _logger = logger;
            _statistics = new ConcurrentDictionary<string, OperationStatisticsInternal>();
            _memorySnapshots = new ConcurrentQueue<MemorySnapshot>();
        }

        /// <summary>
        /// 启动操作计时
        /// Start operation timing
        /// </summary>
        public IOperationTimer BeginOperation(string operationName, string category = "General")
        {
            if (string.IsNullOrWhiteSpace(operationName))
                throw new ArgumentException("Operation name cannot be null or empty", nameof(operationName));

            var timer = new OperationTimer(this, operationName, category);

            _logger?.LogDebug(
                LogCategory.Performance,
                $"Started operation: {operationName}",
                new LogContext
                {
                    OperationType = LogOperationType.Read,
                    CustomProperties = new Dictionary<string, object>
                    {
                        ["Category"] = category,
                        ["OperationName"] = operationName
                    }
                }
            );

            return timer;
        }

        /// <summary>
        /// 记录操作指标
        /// Record operation metrics
        /// </summary>
        public void RecordOperation(string operationName, long durationMs, bool success = true, Dictionary<string, object>? metadata = null)
        {
            if (string.IsNullOrWhiteSpace(operationName))
                throw new ArgumentException("Operation name cannot be null or empty", nameof(operationName));

            if (durationMs < 0)
                throw new ArgumentException("Duration cannot be negative", nameof(durationMs));

            var stats = _statistics.GetOrAdd(operationName, _ => new OperationStatisticsInternal
            {
                OperationName = operationName,
                FirstExecutionTime = DateTime.UtcNow
            });

            stats.RecordExecution(durationMs, success);

            // 记录到日志
            _logger?.LogPerformance(
                $"{operationName}.Duration",
                durationMs,
                "ms",
                new LogContext
                {
                    OperationType = success ? LogOperationType.Read : LogOperationType.Delete,
                    CustomProperties = metadata ?? new Dictionary<string, object>()
                }
            );

            // 如果执行失败或耗时过长，记录警告
            if (!success)
            {
                _logger?.LogWarning(
                    LogCategory.Performance,
                    $"Operation failed: {operationName}",
                    new LogContext
                    {
                        CustomProperties = new Dictionary<string, object>
                        {
                            ["DurationMs"] = durationMs,
                            ["Success"] = success
                        }
                    }
                );
            }
            else if (stats.AverageDurationMs > 0 && durationMs > stats.AverageDurationMs * 2)
            {
                _logger?.LogWarning(
                    LogCategory.Performance,
                    $"Operation took significantly longer than average: {operationName}",
                    new LogContext
                    {
                        CustomProperties = new Dictionary<string, object>
                        {
                            ["DurationMs"] = durationMs,
                            ["AverageDurationMs"] = stats.AverageDurationMs,
                            ["Ratio"] = durationMs / stats.AverageDurationMs
                        }
                    }
                );
            }
        }

        /// <summary>
        /// 记录内存使用情况
        /// Record memory usage
        /// </summary>
        public void RecordMemoryUsage()
        {
            var snapshot = CreateMemorySnapshot();
            _memorySnapshots.Enqueue(snapshot);

            // 限制快照数量
            while (_memorySnapshots.Count > _maxSnapshotsToKeep)
            {
                _memorySnapshots.TryDequeue(out _);
            }

            _logger?.LogDebug(
                LogCategory.Performance,
                $"Memory snapshot recorded: {snapshot.WorkingSetMB:F2} MB working set",
                new LogContext
                {
                    CustomProperties = new Dictionary<string, object>
                    {
                        ["WorkingSetMB"] = snapshot.WorkingSetMB,
                        ["PrivateMemoryMB"] = snapshot.PrivateMemoryMB,
                        ["ManagedHeapMB"] = snapshot.ManagedHeapMB,
                        ["Gen0Collections"] = snapshot.Gen0CollectionCount,
                        ["Gen1Collections"] = snapshot.Gen1CollectionCount,
                        ["Gen2Collections"] = snapshot.Gen2CollectionCount
                    }
                }
            );
        }

        /// <summary>
        /// 获取操作统计信息
        /// Get operation statistics
        /// </summary>
        public OperationStatistics? GetStatistics(string operationName)
        {
            if (string.IsNullOrWhiteSpace(operationName))
                throw new ArgumentException("Operation name cannot be null or empty", nameof(operationName));

            if (_statistics.TryGetValue(operationName, out var stats))
            {
                return stats.ToPublicStatistics();
            }

            return null;
        }

        /// <summary>
        /// 获取所有操作统计信息
        /// Get all operation statistics
        /// </summary>
        public Dictionary<string, OperationStatistics> GetAllStatistics()
        {
            return _statistics.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToPublicStatistics()
            );
        }

        /// <summary>
        /// 获取内存快照
        /// Get memory snapshot
        /// </summary>
        public MemorySnapshot GetMemorySnapshot()
        {
            return CreateMemorySnapshot();
        }

        /// <summary>
        /// 检测性能降级
        /// Detect performance degradation
        /// </summary>
        public bool DetectDegradation(string operationName, double thresholdPercentage = 20.0)
        {
            if (string.IsNullOrWhiteSpace(operationName))
                throw new ArgumentException("Operation name cannot be null or empty", nameof(operationName));

            if (thresholdPercentage < 0 || thresholdPercentage > 100)
                throw new ArgumentException("Threshold percentage must be between 0 and 100", nameof(thresholdPercentage));

            if (!_statistics.TryGetValue(operationName, out var stats))
                return false;

            // 需要足够的数据点才能检测降级
            if (stats.TotalExecutions < _recentExecutionsCount * 2)
                return false;

            var degraded = stats.RecentAverageDurationMs > stats.AverageDurationMs * (1 + thresholdPercentage / 100);

            if (degraded)
            {
                _logger?.LogWarning(
                    LogCategory.Performance,
                    $"Performance degradation detected for operation: {operationName}",
                    new LogContext
                    {
                        CustomProperties = new Dictionary<string, object>
                        {
                            ["OperationName"] = operationName,
                            ["AverageDurationMs"] = stats.AverageDurationMs,
                            ["RecentAverageDurationMs"] = stats.RecentAverageDurationMs,
                            ["DegradationPercentage"] = ((stats.RecentAverageDurationMs - stats.AverageDurationMs) / stats.AverageDurationMs * 100),
                            ["ThresholdPercentage"] = thresholdPercentage
                        }
                    }
                );
            }

            return degraded;
        }

        /// <summary>
        /// 重置所有统计数据
        /// Reset all statistics
        /// </summary>
        public void Reset()
        {
            _statistics.Clear();
            while (_memorySnapshots.TryDequeue(out _)) { }

            _logger?.LogInformation(
                LogCategory.Performance,
                "All performance statistics have been reset"
            );
        }

        /// <summary>
        /// 重置指定操作的统计数据
        /// Reset statistics for specific operation
        /// </summary>
        public void Reset(string operationName)
        {
            if (string.IsNullOrWhiteSpace(operationName))
                throw new ArgumentException("Operation name cannot be null or empty", nameof(operationName));

            _statistics.TryRemove(operationName, out _);

            _logger?.LogInformation(
                LogCategory.Performance,
                $"Performance statistics reset for operation: {operationName}"
            );
        }

        /// <summary>
        /// 创建内存快照
        /// Create memory snapshot
        /// </summary>
        private static MemorySnapshot CreateMemorySnapshot()
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();

            return new MemorySnapshot
            {
                Timestamp = DateTime.UtcNow,
                WorkingSetBytes = process.WorkingSet64,
                PrivateMemoryBytes = process.PrivateMemorySize64,
                ManagedHeapBytes = GC.GetTotalMemory(false),
                Gen0CollectionCount = GC.CollectionCount(0),
                Gen1CollectionCount = GC.CollectionCount(1),
                Gen2CollectionCount = GC.CollectionCount(2)
            };
        }

        /// <summary>
        /// 内部统计信息类（支持并发更新）
        /// Internal statistics class (supports concurrent updates)
        /// </summary>
        private class OperationStatisticsInternal
        {
            private readonly object _lock = new object();
            private readonly Queue<long> _recentDurations = new Queue<long>();
            private const int MaxRecentDurations = 10;

            public string OperationName { get; set; } = string.Empty;
            public string Category { get; set; } = "General";
            public long TotalExecutions { get; private set; }
            public long SuccessCount { get; private set; }
            public long FailureCount { get; private set; }
            public long TotalDurationMs { get; private set; }
            public long MinDurationMs { get; private set; } = long.MaxValue;
            public long MaxDurationMs { get; private set; }
            public DateTime LastExecutionTime { get; private set; }
            public DateTime FirstExecutionTime { get; set; }
            public double RecentAverageDurationMs { get; private set; }

            public double AverageDurationMs => TotalExecutions > 0 ? (double)TotalDurationMs / TotalExecutions : 0;
            public double SuccessRate => TotalExecutions > 0 ? (double)SuccessCount / TotalExecutions * 100 : 0;

            public void RecordExecution(long durationMs, bool success)
            {
                lock (_lock)
                {
                    TotalExecutions++;
                    TotalDurationMs += durationMs;
                    LastExecutionTime = DateTime.UtcNow;

                    if (success)
                        SuccessCount++;
                    else
                        FailureCount++;

                    if (durationMs < MinDurationMs)
                        MinDurationMs = durationMs;

                    if (durationMs > MaxDurationMs)
                        MaxDurationMs = durationMs;

                    // 更新最近执行的平均耗时
                    _recentDurations.Enqueue(durationMs);
                    if (_recentDurations.Count > MaxRecentDurations)
                    {
                        _recentDurations.Dequeue();
                    }

                    RecentAverageDurationMs = _recentDurations.Average();
                }
            }

            public OperationStatistics ToPublicStatistics()
            {
                lock (_lock)
                {
                    return new OperationStatistics
                    {
                        OperationName = OperationName,
                        Category = Category,
                        TotalExecutions = TotalExecutions,
                        SuccessCount = SuccessCount,
                        FailureCount = FailureCount,
                        TotalDurationMs = TotalDurationMs,
                        MinDurationMs = MinDurationMs == long.MaxValue ? 0 : MinDurationMs,
                        MaxDurationMs = MaxDurationMs,
                        LastExecutionTime = LastExecutionTime,
                        FirstExecutionTime = FirstExecutionTime,
                        RecentAverageDurationMs = RecentAverageDurationMs
                    };
                }
            }
        }
    }
}
