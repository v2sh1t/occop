using System;
using System.Collections.Generic;

namespace Occop.Core.Performance
{
    /// <summary>
    /// 性能监控器接口
    /// Performance monitor interface
    /// </summary>
    public interface IPerformanceMonitor
    {
        /// <summary>
        /// 启动操作计时
        /// Start operation timing
        /// </summary>
        /// <param name="operationName">操作名称 Operation name</param>
        /// <param name="category">操作分类 Operation category</param>
        /// <returns>操作计时器 Operation timer</returns>
        IOperationTimer BeginOperation(string operationName, string category = "General");

        /// <summary>
        /// 记录操作指标
        /// Record operation metrics
        /// </summary>
        /// <param name="operationName">操作名称 Operation name</param>
        /// <param name="durationMs">耗时（毫秒） Duration in milliseconds</param>
        /// <param name="success">是否成功 Whether successful</param>
        /// <param name="metadata">元数据 Metadata</param>
        void RecordOperation(string operationName, long durationMs, bool success = true, Dictionary<string, object>? metadata = null);

        /// <summary>
        /// 记录内存使用情况
        /// Record memory usage
        /// </summary>
        void RecordMemoryUsage();

        /// <summary>
        /// 获取操作统计信息
        /// Get operation statistics
        /// </summary>
        /// <param name="operationName">操作名称 Operation name</param>
        /// <returns>操作统计 Operation statistics</returns>
        OperationStatistics? GetStatistics(string operationName);

        /// <summary>
        /// 获取所有操作统计信息
        /// Get all operation statistics
        /// </summary>
        /// <returns>操作统计字典 Dictionary of operation statistics</returns>
        Dictionary<string, OperationStatistics> GetAllStatistics();

        /// <summary>
        /// 获取内存快照
        /// Get memory snapshot
        /// </summary>
        /// <returns>内存快照 Memory snapshot</returns>
        MemorySnapshot GetMemorySnapshot();

        /// <summary>
        /// 检测性能降级
        /// Detect performance degradation
        /// </summary>
        /// <param name="operationName">操作名称 Operation name</param>
        /// <param name="thresholdPercentage">阈值百分比 Threshold percentage (e.g., 20 for 20% degradation)</param>
        /// <returns>是否检测到降级 Whether degradation detected</returns>
        bool DetectDegradation(string operationName, double thresholdPercentage = 20.0);

        /// <summary>
        /// 重置所有统计数据
        /// Reset all statistics
        /// </summary>
        void Reset();

        /// <summary>
        /// 重置指定操作的统计数据
        /// Reset statistics for specific operation
        /// </summary>
        /// <param name="operationName">操作名称 Operation name</param>
        void Reset(string operationName);
    }

    /// <summary>
    /// 操作统计信息
    /// Operation statistics
    /// </summary>
    public class OperationStatistics
    {
        /// <summary>
        /// 操作名称
        /// Operation name
        /// </summary>
        public string OperationName { get; set; } = string.Empty;

        /// <summary>
        /// 操作分类
        /// Operation category
        /// </summary>
        public string Category { get; set; } = "General";

        /// <summary>
        /// 总执行次数
        /// Total execution count
        /// </summary>
        public long TotalExecutions { get; set; }

        /// <summary>
        /// 成功次数
        /// Success count
        /// </summary>
        public long SuccessCount { get; set; }

        /// <summary>
        /// 失败次数
        /// Failure count
        /// </summary>
        public long FailureCount { get; set; }

        /// <summary>
        /// 总耗时（毫秒）
        /// Total duration in milliseconds
        /// </summary>
        public long TotalDurationMs { get; set; }

        /// <summary>
        /// 平均耗时（毫秒）
        /// Average duration in milliseconds
        /// </summary>
        public double AverageDurationMs => TotalExecutions > 0 ? (double)TotalDurationMs / TotalExecutions : 0;

        /// <summary>
        /// 最小耗时（毫秒）
        /// Minimum duration in milliseconds
        /// </summary>
        public long MinDurationMs { get; set; } = long.MaxValue;

        /// <summary>
        /// 最大耗时（毫秒）
        /// Maximum duration in milliseconds
        /// </summary>
        public long MaxDurationMs { get; set; }

        /// <summary>
        /// 最后执行时间
        /// Last execution time
        /// </summary>
        public DateTime LastExecutionTime { get; set; }

        /// <summary>
        /// 首次执行时间
        /// First execution time
        /// </summary>
        public DateTime FirstExecutionTime { get; set; }

        /// <summary>
        /// 成功率
        /// Success rate
        /// </summary>
        public double SuccessRate => TotalExecutions > 0 ? (double)SuccessCount / TotalExecutions * 100 : 0;

        /// <summary>
        /// 最近N次执行的平均耗时（毫秒）
        /// Average duration of recent N executions in milliseconds
        /// </summary>
        public double RecentAverageDurationMs { get; set; }
    }

    /// <summary>
    /// 内存快照
    /// Memory snapshot
    /// </summary>
    public class MemorySnapshot
    {
        /// <summary>
        /// 快照时间
        /// Snapshot time
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 工作集内存（字节）
        /// Working set memory in bytes
        /// </summary>
        public long WorkingSetBytes { get; set; }

        /// <summary>
        /// 私有内存（字节）
        /// Private memory in bytes
        /// </summary>
        public long PrivateMemoryBytes { get; set; }

        /// <summary>
        /// 托管堆大小（字节）
        /// Managed heap size in bytes
        /// </summary>
        public long ManagedHeapBytes { get; set; }

        /// <summary>
        /// GC代数0的回收次数
        /// GC generation 0 collection count
        /// </summary>
        public int Gen0CollectionCount { get; set; }

        /// <summary>
        /// GC代数1的回收次数
        /// GC generation 1 collection count
        /// </summary>
        public int Gen1CollectionCount { get; set; }

        /// <summary>
        /// GC代数2的回收次数
        /// GC generation 2 collection count
        /// </summary>
        public int Gen2CollectionCount { get; set; }

        /// <summary>
        /// 工作集内存（MB）
        /// Working set memory in MB
        /// </summary>
        public double WorkingSetMB => WorkingSetBytes / (1024.0 * 1024.0);

        /// <summary>
        /// 私有内存（MB）
        /// Private memory in MB
        /// </summary>
        public double PrivateMemoryMB => PrivateMemoryBytes / (1024.0 * 1024.0);

        /// <summary>
        /// 托管堆大小（MB）
        /// Managed heap size in MB
        /// </summary>
        public double ManagedHeapMB => ManagedHeapBytes / (1024.0 * 1024.0);
    }
}
