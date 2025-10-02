using System;
using System.Collections.Generic;
using System.Linq;
using Occop.Core.Logging;

namespace Occop.Core.Performance
{
    /// <summary>
    /// 内存分析器接口
    /// Memory analyzer interface
    /// </summary>
    public interface IMemoryAnalyzer
    {
        /// <summary>
        /// 分析内存快照
        /// Analyze memory snapshot
        /// </summary>
        /// <param name="snapshot">内存快照 Memory snapshot</param>
        /// <returns>分析结果 Analysis result</returns>
        MemoryAnalysisResult Analyze(MemorySnapshot snapshot);

        /// <summary>
        /// 比较两个内存快照
        /// Compare two memory snapshots
        /// </summary>
        /// <param name="baseline">基准快照 Baseline snapshot</param>
        /// <param name="current">当前快照 Current snapshot</param>
        /// <returns>比较结果 Comparison result</returns>
        MemoryComparisonResult Compare(MemorySnapshot baseline, MemorySnapshot current);

        /// <summary>
        /// 检测内存泄漏
        /// Detect memory leak
        /// </summary>
        /// <param name="snapshots">内存快照序列 Sequence of memory snapshots</param>
        /// <param name="threshold">增长阈值（百分比） Growth threshold (percentage)</param>
        /// <returns>是否检测到内存泄漏 Whether memory leak detected</returns>
        bool DetectMemoryLeak(IEnumerable<MemorySnapshot> snapshots, double threshold = 10.0);

        /// <summary>
        /// 生成内存趋势报告
        /// Generate memory trend report
        /// </summary>
        /// <param name="snapshots">内存快照序列 Sequence of memory snapshots</param>
        /// <returns>趋势报告 Trend report</returns>
        MemoryTrendReport GenerateTrendReport(IEnumerable<MemorySnapshot> snapshots);

        /// <summary>
        /// 触发GC并获取快照
        /// Trigger GC and get snapshot
        /// </summary>
        /// <param name="generation">GC代数 GC generation (-1 for full collection)</param>
        /// <returns>GC后的内存快照 Memory snapshot after GC</returns>
        MemorySnapshot TriggerGCAndSnapshot(int generation = -1);
    }

    /// <summary>
    /// 内存分析器实现
    /// Memory analyzer implementation
    /// </summary>
    public class MemoryAnalyzer : IMemoryAnalyzer
    {
        private readonly ILoggerService? _logger;
        private const double DefaultLeakThreshold = 10.0; // 10% 增长

        /// <summary>
        /// 初始化内存分析器
        /// Initializes memory analyzer
        /// </summary>
        /// <param name="logger">日志服务（可选） Logger service (optional)</param>
        public MemoryAnalyzer(ILoggerService? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// 分析内存快照
        /// Analyze memory snapshot
        /// </summary>
        public MemoryAnalysisResult Analyze(MemorySnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            var result = new MemoryAnalysisResult
            {
                Snapshot = snapshot,
                AnalysisTime = DateTime.UtcNow,
                Issues = new List<MemoryIssue>()
            };

            // 检查工作集内存是否过高（超过500MB）
            if (snapshot.WorkingSetMB > 500)
            {
                result.Issues.Add(new MemoryIssue
                {
                    Severity = MemoryIssueSeverity.Warning,
                    Type = MemoryIssueType.HighMemoryUsage,
                    Description = $"Working set memory is high: {snapshot.WorkingSetMB:F2} MB",
                    Recommendation = "Consider analyzing memory allocations and releasing unused resources"
                });
            }

            // 检查托管堆是否过大
            if (snapshot.ManagedHeapMB > 200)
            {
                result.Issues.Add(new MemoryIssue
                {
                    Severity = MemoryIssueSeverity.Warning,
                    Type = MemoryIssueType.HighManagedHeap,
                    Description = $"Managed heap is large: {snapshot.ManagedHeapMB:F2} MB",
                    Recommendation = "Consider forcing garbage collection or optimizing object lifetime"
                });
            }

            // 检查GC频率（Gen2应该相对较少）
            var totalCollections = snapshot.Gen0CollectionCount + snapshot.Gen1CollectionCount + snapshot.Gen2CollectionCount;
            if (totalCollections > 0)
            {
                var gen2Ratio = (double)snapshot.Gen2CollectionCount / totalCollections;
                if (gen2Ratio > 0.1) // Gen2回收超过10%
                {
                    result.Issues.Add(new MemoryIssue
                    {
                        Severity = MemoryIssueSeverity.Info,
                        Type = MemoryIssueType.FrequentGC,
                        Description = $"High Gen2 collection ratio: {gen2Ratio:P}",
                        Recommendation = "Frequent Gen2 collections may indicate memory pressure. Consider optimizing object allocations."
                    });
                }
            }

            _logger?.LogDebug(
                LogCategory.Performance,
                $"Memory analysis completed: {result.Issues.Count} issues found",
                new LogContext
                {
                    CustomProperties = new Dictionary<string, object>
                    {
                        ["WorkingSetMB"] = snapshot.WorkingSetMB,
                        ["ManagedHeapMB"] = snapshot.ManagedHeapMB,
                        ["IssueCount"] = result.Issues.Count
                    }
                }
            );

            return result;
        }

        /// <summary>
        /// 比较两个内存快照
        /// Compare two memory snapshots
        /// </summary>
        public MemoryComparisonResult Compare(MemorySnapshot baseline, MemorySnapshot current)
        {
            if (baseline == null)
                throw new ArgumentNullException(nameof(baseline));
            if (current == null)
                throw new ArgumentNullException(nameof(current));

            var result = new MemoryComparisonResult
            {
                Baseline = baseline,
                Current = current,
                WorkingSetDeltaMB = current.WorkingSetMB - baseline.WorkingSetMB,
                PrivateMemoryDeltaMB = current.PrivateMemoryMB - baseline.PrivateMemoryMB,
                ManagedHeapDeltaMB = current.ManagedHeapMB - baseline.ManagedHeapMB,
                Gen0CollectionsDelta = current.Gen0CollectionCount - baseline.Gen0CollectionCount,
                Gen1CollectionsDelta = current.Gen1CollectionCount - baseline.Gen1CollectionCount,
                Gen2CollectionsDelta = current.Gen2CollectionCount - baseline.Gen2CollectionCount,
                TimeDelta = current.Timestamp - baseline.Timestamp
            };

            // 计算增长百分比
            result.WorkingSetGrowthPercentage = baseline.WorkingSetMB > 0
                ? (result.WorkingSetDeltaMB / baseline.WorkingSetMB * 100)
                : 0;

            result.ManagedHeapGrowthPercentage = baseline.ManagedHeapMB > 0
                ? (result.ManagedHeapDeltaMB / baseline.ManagedHeapMB * 100)
                : 0;

            _logger?.LogDebug(
                LogCategory.Performance,
                $"Memory comparison: WS delta = {result.WorkingSetDeltaMB:F2} MB ({result.WorkingSetGrowthPercentage:F1}%)",
                new LogContext
                {
                    CustomProperties = new Dictionary<string, object>
                    {
                        ["WorkingSetDeltaMB"] = result.WorkingSetDeltaMB,
                        ["ManagedHeapDeltaMB"] = result.ManagedHeapDeltaMB,
                        ["TimeDeltaSeconds"] = result.TimeDelta.TotalSeconds
                    }
                }
            );

            return result;
        }

        /// <summary>
        /// 检测内存泄漏
        /// Detect memory leak
        /// </summary>
        public bool DetectMemoryLeak(IEnumerable<MemorySnapshot> snapshots, double threshold = DefaultLeakThreshold)
        {
            if (snapshots == null)
                throw new ArgumentNullException(nameof(snapshots));

            var snapshotList = snapshots.OrderBy(s => s.Timestamp).ToList();

            if (snapshotList.Count < 3)
            {
                _logger?.LogDebug(
                    LogCategory.Performance,
                    "Not enough snapshots for leak detection (minimum 3 required)"
                );
                return false;
            }

            // 比较第一个和最后一个快照
            var first = snapshotList.First();
            var last = snapshotList.Last();

            var comparison = Compare(first, last);

            // 检测托管堆是否持续增长
            var leakDetected = comparison.ManagedHeapGrowthPercentage > threshold;

            if (leakDetected)
            {
                _logger?.LogWarning(
                    LogCategory.Performance,
                    $"Potential memory leak detected: {comparison.ManagedHeapGrowthPercentage:F1}% growth in managed heap",
                    new LogContext
                    {
                        CustomProperties = new Dictionary<string, object>
                        {
                            ["InitialManagedHeapMB"] = first.ManagedHeapMB,
                            ["CurrentManagedHeapMB"] = last.ManagedHeapMB,
                            ["GrowthPercentage"] = comparison.ManagedHeapGrowthPercentage,
                            ["Threshold"] = threshold,
                            ["SnapshotCount"] = snapshotList.Count,
                            ["TimePeriod"] = comparison.TimeDelta.ToString()
                        }
                    }
                );
            }

            return leakDetected;
        }

        /// <summary>
        /// 生成内存趋势报告
        /// Generate memory trend report
        /// </summary>
        public MemoryTrendReport GenerateTrendReport(IEnumerable<MemorySnapshot> snapshots)
        {
            if (snapshots == null)
                throw new ArgumentNullException(nameof(snapshots));

            var snapshotList = snapshots.OrderBy(s => s.Timestamp).ToList();

            if (snapshotList.Count == 0)
            {
                throw new ArgumentException("No snapshots provided", nameof(snapshots));
            }

            var report = new MemoryTrendReport
            {
                StartTime = snapshotList.First().Timestamp,
                EndTime = snapshotList.Last().Timestamp,
                SnapshotCount = snapshotList.Count,
                AverageWorkingSetMB = snapshotList.Average(s => s.WorkingSetMB),
                AverageManagedHeapMB = snapshotList.Average(s => s.ManagedHeapMB),
                MinWorkingSetMB = snapshotList.Min(s => s.WorkingSetMB),
                MaxWorkingSetMB = snapshotList.Max(s => s.WorkingSetMB),
                MinManagedHeapMB = snapshotList.Min(s => s.ManagedHeapMB),
                MaxManagedHeapMB = snapshotList.Max(s => s.ManagedHeapMB),
                TotalGen0Collections = snapshotList.Last().Gen0CollectionCount - snapshotList.First().Gen0CollectionCount,
                TotalGen1Collections = snapshotList.Last().Gen1CollectionCount - snapshotList.First().Gen1CollectionCount,
                TotalGen2Collections = snapshotList.Last().Gen2CollectionCount - snapshotList.First().Gen2CollectionCount
            };

            // 计算趋势（线性回归）
            if (snapshotList.Count > 1)
            {
                var comparison = Compare(snapshotList.First(), snapshotList.Last());
                report.WorkingSetTrendMBPerHour = comparison.TimeDelta.TotalHours > 0
                    ? comparison.WorkingSetDeltaMB / comparison.TimeDelta.TotalHours
                    : 0;

                report.ManagedHeapTrendMBPerHour = comparison.TimeDelta.TotalHours > 0
                    ? comparison.ManagedHeapDeltaMB / comparison.TimeDelta.TotalHours
                    : 0;
            }

            _logger?.LogInformation(
                LogCategory.Performance,
                $"Memory trend report generated: Avg WS = {report.AverageWorkingSetMB:F2} MB, Trend = {report.WorkingSetTrendMBPerHour:F2} MB/h",
                new LogContext
                {
                    CustomProperties = new Dictionary<string, object>
                    {
                        ["SnapshotCount"] = report.SnapshotCount,
                        ["AverageWorkingSetMB"] = report.AverageWorkingSetMB,
                        ["WorkingSetTrendMBPerHour"] = report.WorkingSetTrendMBPerHour
                    }
                }
            );

            return report;
        }

        /// <summary>
        /// 触发GC并获取快照
        /// Trigger GC and get snapshot
        /// </summary>
        public MemorySnapshot TriggerGCAndSnapshot(int generation = -1)
        {
            _logger?.LogDebug(
                LogCategory.Performance,
                $"Triggering GC (generation {generation}) and taking memory snapshot"
            );

            // 触发垃圾回收
            if (generation == -1)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            else
            {
                GC.Collect(generation, GCCollectionMode.Forced, blocking: true);
            }

            // 创建快照
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var snapshot = new MemorySnapshot
            {
                Timestamp = DateTime.UtcNow,
                WorkingSetBytes = process.WorkingSet64,
                PrivateMemoryBytes = process.PrivateMemorySize64,
                ManagedHeapBytes = GC.GetTotalMemory(false),
                Gen0CollectionCount = GC.CollectionCount(0),
                Gen1CollectionCount = GC.CollectionCount(1),
                Gen2CollectionCount = GC.CollectionCount(2)
            };

            _logger?.LogInformation(
                LogCategory.Performance,
                $"GC completed: {snapshot.ManagedHeapMB:F2} MB managed heap after collection",
                new LogContext
                {
                    CustomProperties = new Dictionary<string, object>
                    {
                        ["Generation"] = generation,
                        ["ManagedHeapMB"] = snapshot.ManagedHeapMB,
                        ["WorkingSetMB"] = snapshot.WorkingSetMB
                    }
                }
            );

            return snapshot;
        }
    }

    /// <summary>
    /// 内存分析结果
    /// Memory analysis result
    /// </summary>
    public class MemoryAnalysisResult
    {
        /// <summary>
        /// 被分析的快照
        /// Snapshot being analyzed
        /// </summary>
        public MemorySnapshot Snapshot { get; set; } = null!;

        /// <summary>
        /// 分析时间
        /// Analysis time
        /// </summary>
        public DateTime AnalysisTime { get; set; }

        /// <summary>
        /// 发现的问题列表
        /// List of issues found
        /// </summary>
        public List<MemoryIssue> Issues { get; set; } = new List<MemoryIssue>();

        /// <summary>
        /// 是否有严重问题
        /// Whether there are critical issues
        /// </summary>
        public bool HasCriticalIssues => Issues.Any(i => i.Severity == MemoryIssueSeverity.Critical);

        /// <summary>
        /// 是否有警告
        /// Whether there are warnings
        /// </summary>
        public bool HasWarnings => Issues.Any(i => i.Severity == MemoryIssueSeverity.Warning);
    }

    /// <summary>
    /// 内存问题
    /// Memory issue
    /// </summary>
    public class MemoryIssue
    {
        /// <summary>
        /// 严重程度
        /// Severity
        /// </summary>
        public MemoryIssueSeverity Severity { get; set; }

        /// <summary>
        /// 问题类型
        /// Issue type
        /// </summary>
        public MemoryIssueType Type { get; set; }

        /// <summary>
        /// 描述
        /// Description
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 建议
        /// Recommendation
        /// </summary>
        public string Recommendation { get; set; } = string.Empty;
    }

    /// <summary>
    /// 内存问题严重程度
    /// Memory issue severity
    /// </summary>
    public enum MemoryIssueSeverity
    {
        /// <summary>信息 Info</summary>
        Info,
        /// <summary>警告 Warning</summary>
        Warning,
        /// <summary>严重 Critical</summary>
        Critical
    }

    /// <summary>
    /// 内存问题类型
    /// Memory issue type
    /// </summary>
    public enum MemoryIssueType
    {
        /// <summary>高内存使用 High memory usage</summary>
        HighMemoryUsage,
        /// <summary>高托管堆 High managed heap</summary>
        HighManagedHeap,
        /// <summary>频繁GC Frequent GC</summary>
        FrequentGC,
        /// <summary>内存泄漏 Memory leak</summary>
        MemoryLeak
    }

    /// <summary>
    /// 内存比较结果
    /// Memory comparison result
    /// </summary>
    public class MemoryComparisonResult
    {
        /// <summary>
        /// 基准快照
        /// Baseline snapshot
        /// </summary>
        public MemorySnapshot Baseline { get; set; } = null!;

        /// <summary>
        /// 当前快照
        /// Current snapshot
        /// </summary>
        public MemorySnapshot Current { get; set; } = null!;

        /// <summary>
        /// 工作集内存变化（MB）
        /// Working set delta in MB
        /// </summary>
        public double WorkingSetDeltaMB { get; set; }

        /// <summary>
        /// 私有内存变化（MB）
        /// Private memory delta in MB
        /// </summary>
        public double PrivateMemoryDeltaMB { get; set; }

        /// <summary>
        /// 托管堆变化（MB）
        /// Managed heap delta in MB
        /// </summary>
        public double ManagedHeapDeltaMB { get; set; }

        /// <summary>
        /// Gen0回收次数变化
        /// Gen0 collections delta
        /// </summary>
        public int Gen0CollectionsDelta { get; set; }

        /// <summary>
        /// Gen1回收次数变化
        /// Gen1 collections delta
        /// </summary>
        public int Gen1CollectionsDelta { get; set; }

        /// <summary>
        /// Gen2回收次数变化
        /// Gen2 collections delta
        /// </summary>
        public int Gen2CollectionsDelta { get; set; }

        /// <summary>
        /// 时间间隔
        /// Time delta
        /// </summary>
        public TimeSpan TimeDelta { get; set; }

        /// <summary>
        /// 工作集增长百分比
        /// Working set growth percentage
        /// </summary>
        public double WorkingSetGrowthPercentage { get; set; }

        /// <summary>
        /// 托管堆增长百分比
        /// Managed heap growth percentage
        /// </summary>
        public double ManagedHeapGrowthPercentage { get; set; }
    }

    /// <summary>
    /// 内存趋势报告
    /// Memory trend report
    /// </summary>
    public class MemoryTrendReport
    {
        /// <summary>
        /// 开始时间
        /// Start time
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// 结束时间
        /// End time
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// 快照数量
        /// Snapshot count
        /// </summary>
        public int SnapshotCount { get; set; }

        /// <summary>
        /// 平均工作集（MB）
        /// Average working set in MB
        /// </summary>
        public double AverageWorkingSetMB { get; set; }

        /// <summary>
        /// 平均托管堆（MB）
        /// Average managed heap in MB
        /// </summary>
        public double AverageManagedHeapMB { get; set; }

        /// <summary>
        /// 最小工作集（MB）
        /// Minimum working set in MB
        /// </summary>
        public double MinWorkingSetMB { get; set; }

        /// <summary>
        /// 最大工作集（MB）
        /// Maximum working set in MB
        /// </summary>
        public double MaxWorkingSetMB { get; set; }

        /// <summary>
        /// 最小托管堆（MB）
        /// Minimum managed heap in MB
        /// </summary>
        public double MinManagedHeapMB { get; set; }

        /// <summary>
        /// 最大托管堆（MB）
        /// Maximum managed heap in MB
        /// </summary>
        public double MaxManagedHeapMB { get; set; }

        /// <summary>
        /// 工作集趋势（MB/小时）
        /// Working set trend in MB per hour
        /// </summary>
        public double WorkingSetTrendMBPerHour { get; set; }

        /// <summary>
        /// 托管堆趋势（MB/小时）
        /// Managed heap trend in MB per hour
        /// </summary>
        public double ManagedHeapTrendMBPerHour { get; set; }

        /// <summary>
        /// Gen0回收总次数
        /// Total Gen0 collections
        /// </summary>
        public int TotalGen0Collections { get; set; }

        /// <summary>
        /// Gen1回收总次数
        /// Total Gen1 collections
        /// </summary>
        public int TotalGen1Collections { get; set; }

        /// <summary>
        /// Gen2回收总次数
        /// Total Gen2 collections
        /// </summary>
        public int TotalGen2Collections { get; set; }

        /// <summary>
        /// 时间跨度
        /// Time span
        /// </summary>
        public TimeSpan Duration => EndTime - StartTime;
    }
}
