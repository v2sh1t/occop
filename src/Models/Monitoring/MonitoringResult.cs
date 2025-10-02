using System;
using System.Collections.Generic;

namespace Occop.Models.Monitoring
{
    /// <summary>
    /// 进程监控配置
    /// </summary>
    public class ProcessMonitoringConfig
    {
        #region 基本配置

        /// <summary>
        /// 监控轮询间隔（毫秒）
        /// </summary>
        public int PollingIntervalMs { get; set; } = 1000;

        /// <summary>
        /// 是否启用WMI事件监听
        /// </summary>
        public bool EnableWmiEventListening { get; set; } = true;

        /// <summary>
        /// 是否监控子进程
        /// </summary>
        public bool MonitorChildProcesses { get; set; } = true;

        /// <summary>
        /// 进程树最大深度
        /// </summary>
        public int MaxProcessTreeDepth { get; set; } = 3;

        /// <summary>
        /// 是否启用性能监控
        /// </summary>
        public bool EnablePerformanceMonitoring { get; set; } = true;

        #endregion

        #region 高级配置

        /// <summary>
        /// 监控超时时间（毫秒）
        /// </summary>
        public int MonitoringTimeoutMs { get; set; } = 30000;

        /// <summary>
        /// 最大监控进程数量
        /// </summary>
        public int MaxMonitoredProcesses { get; set; } = 100;

        /// <summary>
        /// 事件历史记录数量限制
        /// </summary>
        public int MaxEventHistory { get; set; } = 1000;

        /// <summary>
        /// 自动清理过期事件（小时）
        /// </summary>
        public int AutoCleanupEventHours { get; set; } = 24;

        #endregion

        #region 过滤配置

        /// <summary>
        /// 监控的进程名称模式列表
        /// </summary>
        public List<string> ProcessNamePatterns { get; set; } = new();

        /// <summary>
        /// 排除的进程名称模式列表
        /// </summary>
        public List<string> ExcludedProcessPatterns { get; set; } = new();

        /// <summary>
        /// 监控的进程路径模式列表
        /// </summary>
        public List<string> ProcessPathPatterns { get; set; } = new();

        #endregion

        #region 性能阈值

        /// <summary>
        /// 内存使用警报阈值（MB）
        /// </summary>
        public long MemoryWarningThresholdMB { get; set; } = 500;

        /// <summary>
        /// CPU使用率警报阈值（百分比）
        /// </summary>
        public double CpuWarningThreshold { get; set; } = 80.0;

        /// <summary>
        /// 句柄数量警报阈值
        /// </summary>
        public int HandleWarningThreshold { get; set; } = 1000;

        #endregion

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public ProcessMonitoringConfig()
        {
            // 添加常见的AI工具进程模式
            ProcessNamePatterns.Add("claude-code*");
            ProcessNamePatterns.Add("codex*");
            ProcessNamePatterns.Add("openai*");
            ProcessNamePatterns.Add("copilot*");

            // 排除系统进程
            ExcludedProcessPatterns.Add("system*");
            ExcludedProcessPatterns.Add("svchost*");
            ExcludedProcessPatterns.Add("winlogon*");
        }
    }

    /// <summary>
    /// 监控操作结果
    /// </summary>
    public class MonitoringResult
    {
        /// <summary>
        /// 操作是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 结果消息
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 错误信息（如果失败）
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 相关异常（如果有）
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// 操作时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 操作耗时
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// 额外数据
        /// </summary>
        public Dictionary<string, object> Data { get; set; }

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public MonitoringResult()
        {
            Timestamp = DateTime.UtcNow;
            Data = new Dictionary<string, object>();
        }

        /// <summary>
        /// 创建成功结果
        /// </summary>
        /// <param name="message">成功消息</param>
        /// <returns>成功结果</returns>
        public static MonitoringResult Success(string message)
        {
            return new MonitoringResult
            {
                Success = true,
                Message = message
            };
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        /// <param name="errorMessage">错误消息</param>
        /// <param name="exception">相关异常</param>
        /// <returns>失败结果</returns>
        public static MonitoringResult Failure(string errorMessage, Exception exception = null)
        {
            return new MonitoringResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                Exception = exception
            };
        }
    }

    /// <summary>
    /// 监控统计信息
    /// </summary>
    public class MonitoringStatistics
    {
        /// <summary>
        /// 监控器启动时间
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// 总运行时间
        /// </summary>
        public TimeSpan TotalRunTime { get; set; }

        /// <summary>
        /// 当前监控的进程数量
        /// </summary>
        public int CurrentProcessCount { get; set; }

        /// <summary>
        /// 总计监控过的进程数量
        /// </summary>
        public int TotalProcessesMonitored { get; set; }

        /// <summary>
        /// 事件计数器
        /// </summary>
        public Dictionary<MonitoringEventType, int> EventCounts { get; set; }

        /// <summary>
        /// 启动的进程数量
        /// </summary>
        public int ProcessesStarted { get; set; }

        /// <summary>
        /// 正常退出的进程数量
        /// </summary>
        public int ProcessesExited { get; set; }

        /// <summary>
        /// 异常终止的进程数量
        /// </summary>
        public int ProcessesKilled { get; set; }

        /// <summary>
        /// 错误数量
        /// </summary>
        public int ErrorCount { get; set; }

        /// <summary>
        /// 警告数量
        /// </summary>
        public int WarningCount { get; set; }

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public MonitoringStatistics()
        {
            StartTime = DateTime.UtcNow;
            EventCounts = new Dictionary<MonitoringEventType, int>();
        }
    }

    /// <summary>
    /// 监控健康检查结果
    /// </summary>
    public class MonitoringHealthResult
    {
        /// <summary>
        /// 是否健康
        /// </summary>
        public bool IsHealthy { get; set; }

        /// <summary>
        /// 健康状态描述
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// 检查时间
        /// </summary>
        public DateTime CheckTime { get; set; }

        /// <summary>
        /// 检查项目结果
        /// </summary>
        public Dictionary<string, bool> CheckResults { get; set; }

        /// <summary>
        /// 性能指标
        /// </summary>
        public Dictionary<string, double> PerformanceMetrics { get; set; }

        /// <summary>
        /// 健康问题列表
        /// </summary>
        public List<string> Issues { get; set; }

        /// <summary>
        /// 建议措施
        /// </summary>
        public List<string> Recommendations { get; set; }

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public MonitoringHealthResult()
        {
            CheckTime = DateTime.UtcNow;
            CheckResults = new Dictionary<string, bool>();
            PerformanceMetrics = new Dictionary<string, double>();
            Issues = new List<string>();
            Recommendations = new List<string>();
        }
    }
}