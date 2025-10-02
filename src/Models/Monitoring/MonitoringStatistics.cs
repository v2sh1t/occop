using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Occop.Models.Monitoring
{
    /// <summary>
    /// 监控统计信息管理类
    /// 用于收集、计算和展示监控系统的统计数据，支持实时更新和历史数据分析
    /// </summary>
    public class MonitoringStatistics
    {
        #region 基础统计信息

        /// <summary>
        /// 统计开始时间
        /// </summary>
        public DateTime StartTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 最后更新时间
        /// </summary>
        public DateTime LastUpdateTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 总运行时间
        /// </summary>
        public TimeSpan TotalRunTime => DateTime.UtcNow - StartTime;

        /// <summary>
        /// 统计数据版本
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// 是否正在收集统计信息
        /// </summary>
        public bool IsCollecting { get; set; } = true;

        #endregion

        #region 进程监控统计

        /// <summary>
        /// 当前监控的进程数量
        /// </summary>
        public int CurrentProcessCount { get; set; }

        /// <summary>
        /// 历史最大监控进程数量
        /// </summary>
        public int MaxProcessCount { get; set; }

        /// <summary>
        /// 总计监控过的进程数量
        /// </summary>
        public long TotalProcessesMonitored { get; set; }

        /// <summary>
        /// 活跃进程数量（当前运行中）
        /// </summary>
        public int ActiveProcessCount { get; set; }

        /// <summary>
        /// 启动的进程数量
        /// </summary>
        public long ProcessesStarted { get; set; }

        /// <summary>
        /// 正常退出的进程数量
        /// </summary>
        public long ProcessesExited { get; set; }

        /// <summary>
        /// 异常终止的进程数量
        /// </summary>
        public long ProcessesKilled { get; set; }

        /// <summary>
        /// 进程错误数量
        /// </summary>
        public long ProcessErrors { get; set; }

        #endregion

        #region 事件统计

        /// <summary>
        /// 事件计数器（按类型）
        /// </summary>
        public ConcurrentDictionary<MonitoringEventType, long> EventCounts { get; set; } = new();

        /// <summary>
        /// 总事件数量
        /// </summary>
        public long TotalEventCount => EventCounts.Values.Sum();

        /// <summary>
        /// 最近24小时事件数量
        /// </summary>
        public long EventsLast24Hours { get; set; }

        /// <summary>
        /// 最近1小时事件数量
        /// </summary>
        public long EventsLastHour { get; set; }

        /// <summary>
        /// 每分钟平均事件数
        /// </summary>
        public double EventsPerMinute { get; set; }

        /// <summary>
        /// 错误事件数量
        /// </summary>
        public long ErrorEventCount => EventCounts.GetValueOrDefault(MonitoringEventType.Error, 0);

        /// <summary>
        /// 警告事件数量
        /// </summary>
        public long WarningEventCount => EventCounts.GetValueOrDefault(MonitoringEventType.Warning, 0);

        #endregion

        #region 性能统计

        /// <summary>
        /// 平均内存使用量（MB）
        /// </summary>
        public double AverageMemoryUsageMB { get; set; }

        /// <summary>
        /// 峰值内存使用量（MB）
        /// </summary>
        public double PeakMemoryUsageMB { get; set; }

        /// <summary>
        /// 当前内存使用量（MB）
        /// </summary>
        public double CurrentMemoryUsageMB { get; set; }

        /// <summary>
        /// 平均CPU使用率（百分比）
        /// </summary>
        public double AverageCpuUsage { get; set; }

        /// <summary>
        /// 峰值CPU使用率（百分比）
        /// </summary>
        public double PeakCpuUsage { get; set; }

        /// <summary>
        /// 当前CPU使用率（百分比）
        /// </summary>
        public double CurrentCpuUsage { get; set; }

        /// <summary>
        /// 平均句柄数量
        /// </summary>
        public double AverageHandleCount { get; set; }

        /// <summary>
        /// 峰值句柄数量
        /// </summary>
        public int PeakHandleCount { get; set; }

        /// <summary>
        /// 当前句柄数量
        /// </summary>
        public int CurrentHandleCount { get; set; }

        #endregion

        #region 监控系统性能

        /// <summary>
        /// 监控系统内存使用量（MB）
        /// </summary>
        public double MonitoringSystemMemoryMB { get; set; }

        /// <summary>
        /// 监控系统CPU使用率（百分比）
        /// </summary>
        public double MonitoringSystemCpuUsage { get; set; }

        /// <summary>
        /// 监控延迟统计（毫秒）
        /// </summary>
        public LatencyStatistics MonitoringLatency { get; set; } = new();

        /// <summary>
        /// WMI事件处理统计
        /// </summary>
        public WmiEventStatistics WmiStatistics { get; set; } = new();

        /// <summary>
        /// 轮询统计
        /// </summary>
        public PollingStatistics PollingStats { get; set; } = new();

        #endregion

        #region 健康状态统计

        /// <summary>
        /// 健康检查次数
        /// </summary>
        public long HealthCheckCount { get; set; }

        /// <summary>
        /// 健康检查成功次数
        /// </summary>
        public long HealthCheckSuccessCount { get; set; }

        /// <summary>
        /// 健康检查失败次数
        /// </summary>
        public long HealthCheckFailureCount { get; set; }

        /// <summary>
        /// 健康检查成功率
        /// </summary>
        public double HealthCheckSuccessRate => HealthCheckCount > 0 ? (double)HealthCheckSuccessCount / HealthCheckCount * 100 : 0;

        /// <summary>
        /// 最后一次健康检查时间
        /// </summary>
        public DateTime? LastHealthCheckTime { get; set; }

        /// <summary>
        /// 最后一次健康检查结果
        /// </summary>
        public bool? LastHealthCheckResult { get; set; }

        #endregion

        #region 数据持久化统计

        /// <summary>
        /// 状态保存次数
        /// </summary>
        public long StateSaveCount { get; set; }

        /// <summary>
        /// 状态保存失败次数
        /// </summary>
        public long StateSaveFailureCount { get; set; }

        /// <summary>
        /// 状态恢复次数
        /// </summary>
        public long StateRestoreCount { get; set; }

        /// <summary>
        /// 状态恢复失败次数
        /// </summary>
        public long StateRestoreFailureCount { get; set; }

        /// <summary>
        /// 最后一次状态保存时间
        /// </summary>
        public DateTime? LastStateSaveTime { get; set; }

        /// <summary>
        /// 最后一次状态恢复时间
        /// </summary>
        public DateTime? LastStateRestoreTime { get; set; }

        #endregion

        #region AI工具特定统计

        /// <summary>
        /// AI工具类型计数
        /// </summary>
        public ConcurrentDictionary<AIToolType, long> AIToolTypeCounts { get; set; } = new();

        /// <summary>
        /// Claude Code 进程数量
        /// </summary>
        public int ClaudeCodeProcessCount { get; set; }

        /// <summary>
        /// OpenAI Codex 进程数量
        /// </summary>
        public int OpenAICodexProcessCount { get; set; }

        /// <summary>
        /// GitHub Copilot 进程数量
        /// </summary>
        public int GitHubCopilotProcessCount { get; set; }

        /// <summary>
        /// 其他AI工具进程数量
        /// </summary>
        public int OtherAIToolProcessCount { get; set; }

        #endregion

        #region 趋势数据

        /// <summary>
        /// 小时级别的统计数据
        /// </summary>
        [JsonIgnore]
        public SlidingWindow<HourlyStatistics> HourlyData { get; set; } = new(24); // 保留24小时

        /// <summary>
        /// 分钟级别的统计数据
        /// </summary>
        [JsonIgnore]
        public SlidingWindow<MinutelyStatistics> MinutelyData { get; set; } = new(60); // 保留60分钟

        #endregion

        #region 构造函数

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public MonitoringStatistics()
        {
            InitializeEventCounts();
            InitializeAIToolCounts();
        }

        /// <summary>
        /// 带开始时间的构造函数
        /// </summary>
        /// <param name="startTime">开始时间</param>
        public MonitoringStatistics(DateTime startTime) : this()
        {
            StartTime = startTime;
            LastUpdateTime = startTime;
        }

        #endregion

        #region 初始化方法

        /// <summary>
        /// 初始化事件计数器
        /// </summary>
        private void InitializeEventCounts()
        {
            foreach (MonitoringEventType eventType in Enum.GetValues<MonitoringEventType>())
            {
                EventCounts[eventType] = 0;
            }
        }

        /// <summary>
        /// 初始化AI工具计数器
        /// </summary>
        private void InitializeAIToolCounts()
        {
            foreach (AIToolType toolType in Enum.GetValues<AIToolType>())
            {
                AIToolTypeCounts[toolType] = 0;
            }
        }

        #endregion

        #region 统计更新方法

        /// <summary>
        /// 记录进程启动
        /// </summary>
        /// <param name="processInfo">进程信息</param>
        public void RecordProcessStarted(ProcessInfo processInfo)
        {
            ProcessesStarted++;
            TotalProcessesMonitored++;
            CurrentProcessCount++;
            ActiveProcessCount++;

            if (CurrentProcessCount > MaxProcessCount)
            {
                MaxProcessCount = CurrentProcessCount;
            }

            // 更新AI工具统计
            if (processInfo.IsAIToolProcess && processInfo.AIToolType.HasValue)
            {
                AIToolTypeCounts.AddOrUpdate(processInfo.AIToolType.Value, 1, (key, oldValue) => oldValue + 1);
                UpdateAIToolProcessCounts();
            }

            // 记录事件
            IncrementEventCount(MonitoringEventType.ProcessStarted);
            UpdateLastModified();
        }

        /// <summary>
        /// 记录进程退出
        /// </summary>
        /// <param name="processInfo">进程信息</param>
        /// <param name="isAbnormal">是否异常退出</param>
        public void RecordProcessExited(ProcessInfo processInfo, bool isAbnormal = false)
        {
            if (isAbnormal)
            {
                ProcessesKilled++;
                IncrementEventCount(MonitoringEventType.ProcessKilled);
            }
            else
            {
                ProcessesExited++;
                IncrementEventCount(MonitoringEventType.ProcessExited);
            }

            CurrentProcessCount = Math.Max(0, CurrentProcessCount - 1);
            ActiveProcessCount = Math.Max(0, ActiveProcessCount - 1);

            // 更新AI工具统计
            if (processInfo.IsAIToolProcess && processInfo.AIToolType.HasValue)
            {
                UpdateAIToolProcessCounts();
            }

            UpdateLastModified();
        }

        /// <summary>
        /// 记录进程错误
        /// </summary>
        public void RecordProcessError()
        {
            ProcessErrors++;
            IncrementEventCount(MonitoringEventType.Error);
            UpdateLastModified();
        }

        /// <summary>
        /// 增加事件计数
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <param name="count">增加数量</param>
        public void IncrementEventCount(MonitoringEventType eventType, long count = 1)
        {
            EventCounts.AddOrUpdate(eventType, count, (key, oldValue) => oldValue + count);
            UpdateEventRates();
            UpdateLastModified();
        }

        /// <summary>
        /// 更新性能统计
        /// </summary>
        /// <param name="memoryMB">内存使用量（MB）</param>
        /// <param name="cpuUsage">CPU使用率</param>
        /// <param name="handleCount">句柄数量</param>
        public void UpdatePerformanceStats(double memoryMB, double cpuUsage, int handleCount)
        {
            // 更新当前值
            CurrentMemoryUsageMB = memoryMB;
            CurrentCpuUsage = cpuUsage;
            CurrentHandleCount = handleCount;

            // 更新峰值
            if (memoryMB > PeakMemoryUsageMB)
            {
                PeakMemoryUsageMB = memoryMB;
            }

            if (cpuUsage > PeakCpuUsage)
            {
                PeakCpuUsage = cpuUsage;
            }

            if (handleCount > PeakHandleCount)
            {
                PeakHandleCount = handleCount;
            }

            // 更新平均值（简单移动平均）
            var runTimeMinutes = TotalRunTime.TotalMinutes;
            if (runTimeMinutes > 0)
            {
                AverageMemoryUsageMB = (AverageMemoryUsageMB * (runTimeMinutes - 1) + memoryMB) / runTimeMinutes;
                AverageCpuUsage = (AverageCpuUsage * (runTimeMinutes - 1) + cpuUsage) / runTimeMinutes;
                AverageHandleCount = (AverageHandleCount * (runTimeMinutes - 1) + handleCount) / runTimeMinutes;
            }
            else
            {
                AverageMemoryUsageMB = memoryMB;
                AverageCpuUsage = cpuUsage;
                AverageHandleCount = handleCount;
            }

            UpdateLastModified();
        }

        /// <summary>
        /// 更新监控系统性能
        /// </summary>
        /// <param name="memoryMB">监控系统内存使用量</param>
        /// <param name="cpuUsage">监控系统CPU使用率</param>
        public void UpdateMonitoringSystemPerformance(double memoryMB, double cpuUsage)
        {
            MonitoringSystemMemoryMB = memoryMB;
            MonitoringSystemCpuUsage = cpuUsage;
            UpdateLastModified();
        }

        /// <summary>
        /// 记录健康检查
        /// </summary>
        /// <param name="success">是否成功</param>
        public void RecordHealthCheck(bool success)
        {
            HealthCheckCount++;
            if (success)
            {
                HealthCheckSuccessCount++;
            }
            else
            {
                HealthCheckFailureCount++;
            }

            LastHealthCheckTime = DateTime.UtcNow;
            LastHealthCheckResult = success;
            UpdateLastModified();
        }

        /// <summary>
        /// 记录状态保存
        /// </summary>
        /// <param name="success">是否成功</param>
        public void RecordStateSave(bool success)
        {
            StateSaveCount++;
            if (!success)
            {
                StateSaveFailureCount++;
            }

            LastStateSaveTime = DateTime.UtcNow;
            UpdateLastModified();
        }

        /// <summary>
        /// 记录状态恢复
        /// </summary>
        /// <param name="success">是否成功</param>
        public void RecordStateRestore(bool success)
        {
            StateRestoreCount++;
            if (!success)
            {
                StateRestoreFailureCount++;
            }

            LastStateRestoreTime = DateTime.UtcNow;
            UpdateLastModified();
        }

        #endregion

        #region 计算方法

        /// <summary>
        /// 更新事件速率
        /// </summary>
        private void UpdateEventRates()
        {
            var totalMinutes = TotalRunTime.TotalMinutes;
            if (totalMinutes > 0)
            {
                EventsPerMinute = TotalEventCount / totalMinutes;
            }
        }

        /// <summary>
        /// 更新AI工具进程计数
        /// </summary>
        private void UpdateAIToolProcessCounts()
        {
            ClaudeCodeProcessCount = (int)AIToolTypeCounts.GetValueOrDefault(AIToolType.ClaudeCode, 0);
            OpenAICodexProcessCount = (int)AIToolTypeCounts.GetValueOrDefault(AIToolType.OpenAICodex, 0);
            GitHubCopilotProcessCount = (int)AIToolTypeCounts.GetValueOrDefault(AIToolType.GitHubCopilot, 0);
            OtherAIToolProcessCount = (int)AIToolTypeCounts.GetValueOrDefault(AIToolType.Other, 0);
        }

        /// <summary>
        /// 计算正常退出率
        /// </summary>
        /// <returns>正常退出率（百分比）</returns>
        public double GetNormalExitRate()
        {
            var totalExits = ProcessesExited + ProcessesKilled;
            return totalExits > 0 ? (double)ProcessesExited / totalExits * 100 : 0;
        }

        /// <summary>
        /// 计算异常终止率
        /// </summary>
        /// <returns>异常终止率（百分比）</returns>
        public double GetAbnormalExitRate()
        {
            var totalExits = ProcessesExited + ProcessesKilled;
            return totalExits > 0 ? (double)ProcessesKilled / totalExits * 100 : 0;
        }

        /// <summary>
        /// 计算错误率
        /// </summary>
        /// <returns>错误率（百分比）</returns>
        public double GetErrorRate()
        {
            return TotalEventCount > 0 ? (double)ErrorEventCount / TotalEventCount * 100 : 0;
        }

        /// <summary>
        /// 获取最活跃的AI工具类型
        /// </summary>
        /// <returns>最活跃的AI工具类型</returns>
        public AIToolType GetMostActiveAIToolType()
        {
            return AIToolTypeCounts.OrderByDescending(kvp => kvp.Value).FirstOrDefault().Key;
        }

        #endregion

        #region 数据快照

        /// <summary>
        /// 创建当前统计的快照
        /// </summary>
        /// <returns>统计快照</returns>
        public MonitoringStatisticsSnapshot CreateSnapshot()
        {
            return new MonitoringStatisticsSnapshot
            {
                Timestamp = DateTime.UtcNow,
                TotalRunTime = TotalRunTime,
                CurrentProcessCount = CurrentProcessCount,
                TotalEventCount = TotalEventCount,
                EventsPerMinute = EventsPerMinute,
                CurrentMemoryUsageMB = CurrentMemoryUsageMB,
                CurrentCpuUsage = CurrentCpuUsage,
                MonitoringSystemMemoryMB = MonitoringSystemMemoryMB,
                MonitoringSystemCpuUsage = MonitoringSystemCpuUsage,
                HealthCheckSuccessRate = HealthCheckSuccessRate,
                ErrorRate = GetErrorRate(),
                NormalExitRate = GetNormalExitRate()
            };
        }

        /// <summary>
        /// 添加分钟级统计数据
        /// </summary>
        public void AddMinutelyData()
        {
            var data = new MinutelyStatistics
            {
                Timestamp = DateTime.UtcNow,
                ProcessCount = CurrentProcessCount,
                EventCount = TotalEventCount,
                MemoryUsageMB = CurrentMemoryUsageMB,
                CpuUsage = CurrentCpuUsage
            };

            MinutelyData.Add(data);
        }

        /// <summary>
        /// 添加小时级统计数据
        /// </summary>
        public void AddHourlyData()
        {
            var data = new HourlyStatistics
            {
                Timestamp = DateTime.UtcNow,
                AverageProcessCount = MinutelyData.Data.Where(d => d.Timestamp > DateTime.UtcNow.AddHours(-1)).Select(d => d.ProcessCount).DefaultIfEmpty(0).Average(),
                TotalEvents = TotalEventCount,
                AverageMemoryUsageMB = MinutelyData.Data.Where(d => d.Timestamp > DateTime.UtcNow.AddHours(-1)).Select(d => d.MemoryUsageMB).DefaultIfEmpty(0).Average(),
                AverageCpuUsage = MinutelyData.Data.Where(d => d.Timestamp > DateTime.UtcNow.AddHours(-1)).Select(d => d.CpuUsage).DefaultIfEmpty(0).Average()
            };

            HourlyData.Add(data);
        }

        #endregion

        #region 重置和清理

        /// <summary>
        /// 重置所有统计数据
        /// </summary>
        public void Reset()
        {
            StartTime = DateTime.UtcNow;
            LastUpdateTime = DateTime.UtcNow;
            Version++;

            // 重置计数器
            CurrentProcessCount = 0;
            MaxProcessCount = 0;
            TotalProcessesMonitored = 0;
            ActiveProcessCount = 0;
            ProcessesStarted = 0;
            ProcessesExited = 0;
            ProcessesKilled = 0;
            ProcessErrors = 0;

            // 重置事件统计
            InitializeEventCounts();
            EventsLast24Hours = 0;
            EventsLastHour = 0;
            EventsPerMinute = 0;

            // 重置性能统计
            AverageMemoryUsageMB = 0;
            PeakMemoryUsageMB = 0;
            CurrentMemoryUsageMB = 0;
            AverageCpuUsage = 0;
            PeakCpuUsage = 0;
            CurrentCpuUsage = 0;
            AverageHandleCount = 0;
            PeakHandleCount = 0;
            CurrentHandleCount = 0;

            // 重置健康检查
            HealthCheckCount = 0;
            HealthCheckSuccessCount = 0;
            HealthCheckFailureCount = 0;
            LastHealthCheckTime = null;
            LastHealthCheckResult = null;

            // 重置状态保存
            StateSaveCount = 0;
            StateSaveFailureCount = 0;
            StateRestoreCount = 0;
            StateRestoreFailureCount = 0;
            LastStateSaveTime = null;
            LastStateRestoreTime = null;

            // 重置AI工具统计
            InitializeAIToolCounts();
            UpdateAIToolProcessCounts();

            // 清理历史数据
            HourlyData.Clear();
            MinutelyData.Clear();

            // 重置子对象
            MonitoringLatency.Reset();
            WmiStatistics.Reset();
            PollingStats.Reset();
        }

        /// <summary>
        /// 更新最后修改时间
        /// </summary>
        private void UpdateLastModified()
        {
            LastUpdateTime = DateTime.UtcNow;
        }

        #endregion

        #region 报告生成

        /// <summary>
        /// 生成统计报告
        /// </summary>
        /// <returns>统计报告</returns>
        public MonitoringStatisticsReport GenerateReport()
        {
            return new MonitoringStatisticsReport
            {
                GeneratedTime = DateTime.UtcNow,
                StartTime = StartTime,
                TotalRunTime = TotalRunTime,
                ProcessSummary = new ProcessStatisticsSummary
                {
                    Current = CurrentProcessCount,
                    Max = MaxProcessCount,
                    TotalMonitored = TotalProcessesMonitored,
                    Started = ProcessesStarted,
                    NormalExits = ProcessesExited,
                    AbnormalExits = ProcessesKilled,
                    Errors = ProcessErrors
                },
                EventSummary = new EventStatisticsSummary
                {
                    Total = TotalEventCount,
                    PerMinute = EventsPerMinute,
                    Errors = ErrorEventCount,
                    Warnings = WarningEventCount,
                    EventsByType = EventCounts.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value)
                },
                PerformanceSummary = new PerformanceStatisticsSummary
                {
                    CurrentMemoryMB = CurrentMemoryUsageMB,
                    PeakMemoryMB = PeakMemoryUsageMB,
                    AverageMemoryMB = AverageMemoryUsageMB,
                    CurrentCpuUsage = CurrentCpuUsage,
                    PeakCpuUsage = PeakCpuUsage,
                    AverageCpuUsage = AverageCpuUsage,
                    CurrentHandles = CurrentHandleCount,
                    PeakHandles = PeakHandleCount,
                    AverageHandles = AverageHandleCount
                },
                HealthSummary = new HealthStatisticsSummary
                {
                    TotalChecks = HealthCheckCount,
                    SuccessfulChecks = HealthCheckSuccessCount,
                    FailedChecks = HealthCheckFailureCount,
                    SuccessRate = HealthCheckSuccessRate,
                    LastCheckTime = LastHealthCheckTime,
                    LastCheckResult = LastHealthCheckResult
                }
            };
        }

        /// <summary>
        /// 获取摘要信息
        /// </summary>
        /// <returns>摘要字符串</returns>
        public string GetSummary()
        {
            return $"监控统计 - 运行时间: {TotalRunTime:hh\\:mm\\:ss}, " +
                   $"进程: {CurrentProcessCount}/{MaxProcessCount}, " +
                   $"事件: {TotalEventCount} ({EventsPerMinute:F1}/分钟), " +
                   $"内存: {CurrentMemoryUsageMB:F1}MB, " +
                   $"CPU: {CurrentCpuUsage:F1}%, " +
                   $"健康率: {HealthCheckSuccessRate:F1}%";
        }

        #endregion

        #region 重写方法

        /// <summary>
        /// 获取字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString()
        {
            return GetSummary();
        }

        #endregion
    }

    #region 辅助类

    /// <summary>
    /// 延迟统计
    /// </summary>
    public class LatencyStatistics
    {
        /// <summary>
        /// 平均延迟（毫秒）
        /// </summary>
        public double AverageLatencyMs { get; set; }

        /// <summary>
        /// 最小延迟（毫秒）
        /// </summary>
        public double MinLatencyMs { get; set; } = double.MaxValue;

        /// <summary>
        /// 最大延迟（毫秒）
        /// </summary>
        public double MaxLatencyMs { get; set; }

        /// <summary>
        /// 95百分位延迟（毫秒）
        /// </summary>
        public double P95LatencyMs { get; set; }

        /// <summary>
        /// 99百分位延迟（毫秒）
        /// </summary>
        public double P99LatencyMs { get; set; }

        /// <summary>
        /// 延迟测量次数
        /// </summary>
        public long MeasurementCount { get; set; }

        /// <summary>
        /// 重置统计
        /// </summary>
        public void Reset()
        {
            AverageLatencyMs = 0;
            MinLatencyMs = double.MaxValue;
            MaxLatencyMs = 0;
            P95LatencyMs = 0;
            P99LatencyMs = 0;
            MeasurementCount = 0;
        }
    }

    /// <summary>
    /// WMI事件统计
    /// </summary>
    public class WmiEventStatistics
    {
        /// <summary>
        /// 进程创建事件数量
        /// </summary>
        public long ProcessCreationEvents { get; set; }

        /// <summary>
        /// 进程删除事件数量
        /// </summary>
        public long ProcessDeletionEvents { get; set; }

        /// <summary>
        /// WMI错误数量
        /// </summary>
        public long WmiErrors { get; set; }

        /// <summary>
        /// WMI重连次数
        /// </summary>
        public long ReconnectionCount { get; set; }

        /// <summary>
        /// 最后一次WMI事件时间
        /// </summary>
        public DateTime? LastEventTime { get; set; }

        /// <summary>
        /// 重置统计
        /// </summary>
        public void Reset()
        {
            ProcessCreationEvents = 0;
            ProcessDeletionEvents = 0;
            WmiErrors = 0;
            ReconnectionCount = 0;
            LastEventTime = null;
        }
    }

    /// <summary>
    /// 轮询统计
    /// </summary>
    public class PollingStatistics
    {
        /// <summary>
        /// 轮询次数
        /// </summary>
        public long PollingCount { get; set; }

        /// <summary>
        /// 轮询错误次数
        /// </summary>
        public long PollingErrors { get; set; }

        /// <summary>
        /// 平均轮询耗时（毫秒）
        /// </summary>
        public double AveragePollingTimeMs { get; set; }

        /// <summary>
        /// 最后一次轮询时间
        /// </summary>
        public DateTime? LastPollingTime { get; set; }

        /// <summary>
        /// 重置统计
        /// </summary>
        public void Reset()
        {
            PollingCount = 0;
            PollingErrors = 0;
            AveragePollingTimeMs = 0;
            LastPollingTime = null;
        }
    }

    /// <summary>
    /// 滑动窗口数据结构
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    public class SlidingWindow<T>
    {
        private readonly Queue<T> _data;
        private readonly int _capacity;

        /// <summary>
        /// 数据列表
        /// </summary>
        public IReadOnlyList<T> Data => _data.ToList();

        /// <summary>
        /// 当前数据数量
        /// </summary>
        public int Count => _data.Count;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="capacity">容量</param>
        public SlidingWindow(int capacity)
        {
            _capacity = capacity;
            _data = new Queue<T>(capacity);
        }

        /// <summary>
        /// 添加数据
        /// </summary>
        /// <param name="item">数据项</param>
        public void Add(T item)
        {
            if (_data.Count >= _capacity)
            {
                _data.Dequeue();
            }
            _data.Enqueue(item);
        }

        /// <summary>
        /// 清空数据
        /// </summary>
        public void Clear()
        {
            _data.Clear();
        }
    }

    /// <summary>
    /// 分钟级统计数据
    /// </summary>
    public class MinutelyStatistics
    {
        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 进程数量
        /// </summary>
        public int ProcessCount { get; set; }

        /// <summary>
        /// 事件数量
        /// </summary>
        public long EventCount { get; set; }

        /// <summary>
        /// 内存使用量（MB）
        /// </summary>
        public double MemoryUsageMB { get; set; }

        /// <summary>
        /// CPU使用率
        /// </summary>
        public double CpuUsage { get; set; }
    }

    /// <summary>
    /// 小时级统计数据
    /// </summary>
    public class HourlyStatistics
    {
        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 平均进程数量
        /// </summary>
        public double AverageProcessCount { get; set; }

        /// <summary>
        /// 总事件数量
        /// </summary>
        public long TotalEvents { get; set; }

        /// <summary>
        /// 平均内存使用量（MB）
        /// </summary>
        public double AverageMemoryUsageMB { get; set; }

        /// <summary>
        /// 平均CPU使用率
        /// </summary>
        public double AverageCpuUsage { get; set; }
    }

    /// <summary>
    /// 监控统计快照
    /// </summary>
    public class MonitoringStatisticsSnapshot
    {
        /// <summary>
        /// 快照时间
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 总运行时间
        /// </summary>
        public TimeSpan TotalRunTime { get; set; }

        /// <summary>
        /// 当前进程数量
        /// </summary>
        public int CurrentProcessCount { get; set; }

        /// <summary>
        /// 总事件数量
        /// </summary>
        public long TotalEventCount { get; set; }

        /// <summary>
        /// 每分钟事件数
        /// </summary>
        public double EventsPerMinute { get; set; }

        /// <summary>
        /// 当前内存使用量（MB）
        /// </summary>
        public double CurrentMemoryUsageMB { get; set; }

        /// <summary>
        /// 当前CPU使用率
        /// </summary>
        public double CurrentCpuUsage { get; set; }

        /// <summary>
        /// 监控系统内存使用量（MB）
        /// </summary>
        public double MonitoringSystemMemoryMB { get; set; }

        /// <summary>
        /// 监控系统CPU使用率
        /// </summary>
        public double MonitoringSystemCpuUsage { get; set; }

        /// <summary>
        /// 健康检查成功率
        /// </summary>
        public double HealthCheckSuccessRate { get; set; }

        /// <summary>
        /// 错误率
        /// </summary>
        public double ErrorRate { get; set; }

        /// <summary>
        /// 正常退出率
        /// </summary>
        public double NormalExitRate { get; set; }
    }

    /// <summary>
    /// 监控统计报告
    /// </summary>
    public class MonitoringStatisticsReport
    {
        /// <summary>
        /// 报告生成时间
        /// </summary>
        public DateTime GeneratedTime { get; set; }

        /// <summary>
        /// 监控开始时间
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// 总运行时间
        /// </summary>
        public TimeSpan TotalRunTime { get; set; }

        /// <summary>
        /// 进程统计摘要
        /// </summary>
        public ProcessStatisticsSummary ProcessSummary { get; set; }

        /// <summary>
        /// 事件统计摘要
        /// </summary>
        public EventStatisticsSummary EventSummary { get; set; }

        /// <summary>
        /// 性能统计摘要
        /// </summary>
        public PerformanceStatisticsSummary PerformanceSummary { get; set; }

        /// <summary>
        /// 健康统计摘要
        /// </summary>
        public HealthStatisticsSummary HealthSummary { get; set; }
    }

    /// <summary>
    /// 进程统计摘要
    /// </summary>
    public class ProcessStatisticsSummary
    {
        /// <summary>
        /// 当前进程数
        /// </summary>
        public int Current { get; set; }

        /// <summary>
        /// 最大进程数
        /// </summary>
        public int Max { get; set; }

        /// <summary>
        /// 总监控进程数
        /// </summary>
        public long TotalMonitored { get; set; }

        /// <summary>
        /// 启动的进程数
        /// </summary>
        public long Started { get; set; }

        /// <summary>
        /// 正常退出进程数
        /// </summary>
        public long NormalExits { get; set; }

        /// <summary>
        /// 异常退出进程数
        /// </summary>
        public long AbnormalExits { get; set; }

        /// <summary>
        /// 错误进程数
        /// </summary>
        public long Errors { get; set; }
    }

    /// <summary>
    /// 事件统计摘要
    /// </summary>
    public class EventStatisticsSummary
    {
        /// <summary>
        /// 总事件数
        /// </summary>
        public long Total { get; set; }

        /// <summary>
        /// 每分钟事件数
        /// </summary>
        public double PerMinute { get; set; }

        /// <summary>
        /// 错误事件数
        /// </summary>
        public long Errors { get; set; }

        /// <summary>
        /// 警告事件数
        /// </summary>
        public long Warnings { get; set; }

        /// <summary>
        /// 按类型分组的事件数
        /// </summary>
        public Dictionary<string, long> EventsByType { get; set; }
    }

    /// <summary>
    /// 性能统计摘要
    /// </summary>
    public class PerformanceStatisticsSummary
    {
        /// <summary>
        /// 当前内存使用量（MB）
        /// </summary>
        public double CurrentMemoryMB { get; set; }

        /// <summary>
        /// 峰值内存使用量（MB）
        /// </summary>
        public double PeakMemoryMB { get; set; }

        /// <summary>
        /// 平均内存使用量（MB）
        /// </summary>
        public double AverageMemoryMB { get; set; }

        /// <summary>
        /// 当前CPU使用率
        /// </summary>
        public double CurrentCpuUsage { get; set; }

        /// <summary>
        /// 峰值CPU使用率
        /// </summary>
        public double PeakCpuUsage { get; set; }

        /// <summary>
        /// 平均CPU使用率
        /// </summary>
        public double AverageCpuUsage { get; set; }

        /// <summary>
        /// 当前句柄数
        /// </summary>
        public int CurrentHandles { get; set; }

        /// <summary>
        /// 峰值句柄数
        /// </summary>
        public int PeakHandles { get; set; }

        /// <summary>
        /// 平均句柄数
        /// </summary>
        public double AverageHandles { get; set; }
    }

    /// <summary>
    /// 健康统计摘要
    /// </summary>
    public class HealthStatisticsSummary
    {
        /// <summary>
        /// 总检查次数
        /// </summary>
        public long TotalChecks { get; set; }

        /// <summary>
        /// 成功检查次数
        /// </summary>
        public long SuccessfulChecks { get; set; }

        /// <summary>
        /// 失败检查次数
        /// </summary>
        public long FailedChecks { get; set; }

        /// <summary>
        /// 成功率
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// 最后检查时间
        /// </summary>
        public DateTime? LastCheckTime { get; set; }

        /// <summary>
        /// 最后检查结果
        /// </summary>
        public bool? LastCheckResult { get; set; }
    }

    #endregion
}