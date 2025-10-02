using System;
using System.Collections.Generic;

namespace Occop.Models.Monitoring
{
    /// <summary>
    /// 进程监控事件模型
    /// 记录监控过程中发生的各种事件和状态变化
    /// </summary>
    public class MonitoringEvent
    {
        #region 基本属性

        /// <summary>
        /// 事件唯一标识符
        /// </summary>
        public Guid EventId { get; set; }

        /// <summary>
        /// 事件类型
        /// </summary>
        public MonitoringEventType EventType { get; set; }

        /// <summary>
        /// 事件发生时间（UTC）
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 相关的进程ID
        /// </summary>
        public int? ProcessId { get; set; }

        /// <summary>
        /// 进程名称
        /// </summary>
        public string ProcessName { get; set; }

        /// <summary>
        /// 事件标题
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// 事件描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 事件严重程度
        /// </summary>
        public EventSeverity Severity { get; set; }

        #endregion

        #region 详细信息

        /// <summary>
        /// 事件分类
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// 事件源（谁产生了这个事件）
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// 相关的异常信息（如果有）
        /// </summary>
        public string ExceptionDetails { get; set; }

        /// <summary>
        /// 额外的事件数据
        /// </summary>
        public Dictionary<string, object> Data { get; set; }

        /// <summary>
        /// 事件标签
        /// </summary>
        public HashSet<string> Tags { get; set; }

        #endregion

        #region 进程相关信息

        /// <summary>
        /// 进程启动时间（对于进程事件）
        /// </summary>
        public DateTime? ProcessStartTime { get; set; }

        /// <summary>
        /// 进程退出时间（对于退出事件）
        /// </summary>
        public DateTime? ProcessExitTime { get; set; }

        /// <summary>
        /// 进程退出代码（对于退出事件）
        /// </summary>
        public int? ProcessExitCode { get; set; }

        /// <summary>
        /// 父进程ID
        /// </summary>
        public int? ParentProcessId { get; set; }

        /// <summary>
        /// 进程命令行参数
        /// </summary>
        public string CommandLine { get; set; }

        /// <summary>
        /// 工作目录
        /// </summary>
        public string WorkingDirectory { get; set; }

        #endregion

        #region 性能信息

        /// <summary>
        /// 内存使用量（字节）
        /// </summary>
        public long? MemoryUsage { get; set; }

        /// <summary>
        /// CPU使用率（百分比）
        /// </summary>
        public double? CpuUsage { get; set; }

        /// <summary>
        /// 进程运行时长
        /// </summary>
        public TimeSpan? ProcessDuration { get; set; }

        /// <summary>
        /// 线程数量
        /// </summary>
        public int? ThreadCount { get; set; }

        /// <summary>
        /// 句柄数量
        /// </summary>
        public int? HandleCount { get; set; }

        #endregion

        #region 构造函数

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public MonitoringEvent()
        {
            EventId = Guid.NewGuid();
            Timestamp = DateTime.UtcNow;
            Data = new Dictionary<string, object>();
            Tags = new HashSet<string>();
            Severity = EventSeverity.Information;
        }

        /// <summary>
        /// 基础构造函数
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <param name="title">事件标题</param>
        /// <param name="description">事件描述</param>
        public MonitoringEvent(MonitoringEventType eventType, string title, string description = null) : this()
        {
            EventType = eventType;
            Title = title;
            Description = description;
            Category = GetCategoryFromEventType(eventType);
        }

        /// <summary>
        /// 进程相关事件构造函数
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <param name="processInfo">进程信息</param>
        /// <param name="title">事件标题</param>
        /// <param name="description">事件描述</param>
        public MonitoringEvent(MonitoringEventType eventType, ProcessInfo processInfo, string title, string description = null) : this()
        {
            EventType = eventType;
            Title = title;
            Description = description;
            Category = GetCategoryFromEventType(eventType);

            if (processInfo != null)
            {
                ProcessId = processInfo.ProcessId;
                ProcessName = processInfo.ProcessName;
                ProcessStartTime = processInfo.StartTime;
                ParentProcessId = processInfo.ParentProcessId;
                MemoryUsage = processInfo.WorkingSetSize;
                ThreadCount = processInfo.ThreadCount;
                HandleCount = processInfo.HandleCount;

                if (processInfo.HasExited)
                {
                    ProcessExitTime = processInfo.ExitTime;
                    ProcessExitCode = processInfo.ExitCode;
                    ProcessDuration = processInfo.GetRunningDuration();
                }

                // 复制标签
                foreach (var tag in processInfo.Tags)
                {
                    Tags.Add(tag);
                }

                // 复制相关元数据
                foreach (var kvp in processInfo.Metadata)
                {
                    Data[$"Process_{kvp.Key}"] = kvp.Value;
                }
            }
        }

        #endregion

        #region 静态工厂方法

        /// <summary>
        /// 创建进程启动事件
        /// </summary>
        /// <param name="processInfo">进程信息</param>
        /// <returns>监控事件</returns>
        public static MonitoringEvent CreateProcessStarted(ProcessInfo processInfo)
        {
            var evt = new MonitoringEvent(MonitoringEventType.ProcessStarted, processInfo,
                $"进程启动: {processInfo.ProcessName} [{processInfo.ProcessId}]",
                $"监控到进程 {processInfo.ProcessName} (ID: {processInfo.ProcessId}) 已启动")
            {
                Severity = EventSeverity.Information,
                Source = "ProcessMonitor"
            };

            evt.Tags.Add("PROCESS_LIFECYCLE");
            evt.Tags.Add("STARTED");

            return evt;
        }

        /// <summary>
        /// 创建进程正常退出事件
        /// </summary>
        /// <param name="processInfo">进程信息</param>
        /// <returns>监控事件</returns>
        public static MonitoringEvent CreateProcessExited(ProcessInfo processInfo)
        {
            var evt = new MonitoringEvent(MonitoringEventType.ProcessExited, processInfo,
                $"进程退出: {processInfo.ProcessName} [{processInfo.ProcessId}]",
                $"进程 {processInfo.ProcessName} (ID: {processInfo.ProcessId}) 正常退出，退出代码: {processInfo.ExitCode}")
            {
                Severity = EventSeverity.Information,
                Source = "ProcessMonitor"
            };

            evt.Tags.Add("PROCESS_LIFECYCLE");
            evt.Tags.Add("EXITED");

            return evt;
        }

        /// <summary>
        /// 创建进程异常终止事件
        /// </summary>
        /// <param name="processInfo">进程信息</param>
        /// <returns>监控事件</returns>
        public static MonitoringEvent CreateProcessKilled(ProcessInfo processInfo)
        {
            var evt = new MonitoringEvent(MonitoringEventType.ProcessKilled, processInfo,
                $"进程被终止: {processInfo.ProcessName} [{processInfo.ProcessId}]",
                $"进程 {processInfo.ProcessName} (ID: {processInfo.ProcessId}) 异常终止 - {processInfo.ExitReason}")
            {
                Severity = EventSeverity.Warning,
                Source = "ProcessMonitor"
            };

            evt.Tags.Add("PROCESS_LIFECYCLE");
            evt.Tags.Add("KILLED");
            evt.Tags.Add("ABNORMAL_EXIT");

            return evt;
        }

        /// <summary>
        /// 创建监控状态变化事件
        /// </summary>
        /// <param name="previousState">之前状态</param>
        /// <param name="currentState">当前状态</param>
        /// <param name="reason">变化原因</param>
        /// <returns>监控事件</returns>
        public static MonitoringEvent CreateStateChanged(MonitoringState previousState, MonitoringState currentState, string reason = null)
        {
            var evt = new MonitoringEvent(MonitoringEventType.StateChanged,
                $"监控状态变化: {previousState} -> {currentState}",
                $"进程监控器状态从 {previousState} 变更为 {currentState}" + (string.IsNullOrEmpty(reason) ? "" : $"，原因: {reason}"))
            {
                Severity = EventSeverity.Information,
                Source = "ProcessMonitor",
                Category = "StateChange"
            };

            evt.Data["PreviousState"] = previousState.ToString();
            evt.Data["CurrentState"] = currentState.ToString();
            evt.Data["Reason"] = reason;

            evt.Tags.Add("STATE_CHANGE");
            evt.Tags.Add($"FROM_{previousState}".ToUpperInvariant());
            evt.Tags.Add($"TO_{currentState}".ToUpperInvariant());

            return evt;
        }

        /// <summary>
        /// 创建监控错误事件
        /// </summary>
        /// <param name="exception">异常信息</param>
        /// <param name="context">错误上下文</param>
        /// <param name="processId">相关进程ID</param>
        /// <returns>监控事件</returns>
        public static MonitoringEvent CreateError(Exception exception, string context = null, int? processId = null)
        {
            var evt = new MonitoringEvent(MonitoringEventType.Error,
                $"监控错误: {exception.GetType().Name}",
                $"进程监控过程中发生错误: {exception.Message}")
            {
                Severity = EventSeverity.Error,
                Source = "ProcessMonitor",
                Category = "Error",
                ProcessId = processId,
                ExceptionDetails = exception.ToString()
            };

            if (!string.IsNullOrEmpty(context))
            {
                evt.Data["Context"] = context;
            }

            evt.Data["ExceptionType"] = exception.GetType().FullName;
            evt.Data["StackTrace"] = exception.StackTrace;

            evt.Tags.Add("ERROR");
            evt.Tags.Add(exception.GetType().Name.ToUpperInvariant());

            return evt;
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 添加数据项
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        public void AddData(string key, object value)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                Data[key] = value;
            }
        }

        /// <summary>
        /// 获取数据项
        /// </summary>
        /// <typeparam name="T">返回类型</typeparam>
        /// <param name="key">键</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>数据值</returns>
        public T GetData<T>(string key, T defaultValue = default)
        {
            if (string.IsNullOrWhiteSpace(key) || !Data.ContainsKey(key))
                return defaultValue;

            try
            {
                return (T)Data[key];
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// 添加标签
        /// </summary>
        /// <param name="tag">标签</param>
        public void AddTag(string tag)
        {
            if (!string.IsNullOrWhiteSpace(tag))
            {
                Tags.Add(tag.ToUpperInvariant());
            }
        }

        /// <summary>
        /// 检查是否有指定标签
        /// </summary>
        /// <param name="tag">标签</param>
        /// <returns>是否存在</returns>
        public bool HasTag(string tag)
        {
            return !string.IsNullOrWhiteSpace(tag) && Tags.Contains(tag.ToUpperInvariant());
        }

        /// <summary>
        /// 根据事件类型获取分类
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <returns>分类名称</returns>
        private static string GetCategoryFromEventType(MonitoringEventType eventType)
        {
            return eventType switch
            {
                MonitoringEventType.ProcessStarted or
                MonitoringEventType.ProcessExited or
                MonitoringEventType.ProcessKilled => "ProcessLifecycle",

                MonitoringEventType.StateChanged => "StateChange",

                MonitoringEventType.Error or
                MonitoringEventType.Warning => "Diagnostic",

                MonitoringEventType.Information => "Information",

                MonitoringEventType.PerformanceAlert => "Performance",

                _ => "General"
            };
        }

        /// <summary>
        /// 获取事件摘要信息
        /// </summary>
        /// <returns>摘要字符串</returns>
        public string GetSummary()
        {
            var processInfo = ProcessId.HasValue ? $" [PID: {ProcessId}]" : "";
            return $"[{Severity}] {Title}{processInfo} - {Timestamp:yyyy-MM-dd HH:mm:ss}";
        }

        #endregion

        #region 重写方法

        /// <summary>
        /// 获取对象字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString()
        {
            return GetSummary();
        }

        /// <summary>
        /// 计算哈希码
        /// </summary>
        /// <returns>哈希码</returns>
        public override int GetHashCode()
        {
            return EventId.GetHashCode();
        }

        /// <summary>
        /// 比较对象相等性
        /// </summary>
        /// <param name="obj">比较对象</param>
        /// <returns>是否相等</returns>
        public override bool Equals(object obj)
        {
            return obj is MonitoringEvent other && EventId == other.EventId;
        }

        #endregion
    }

    #region 枚举定义

    /// <summary>
    /// 监控事件类型
    /// </summary>
    public enum MonitoringEventType
    {
        /// <summary>
        /// 信息事件
        /// </summary>
        Information,

        /// <summary>
        /// 警告事件
        /// </summary>
        Warning,

        /// <summary>
        /// 错误事件
        /// </summary>
        Error,

        /// <summary>
        /// 进程启动
        /// </summary>
        ProcessStarted,

        /// <summary>
        /// 进程正常退出
        /// </summary>
        ProcessExited,

        /// <summary>
        /// 进程被终止
        /// </summary>
        ProcessKilled,

        /// <summary>
        /// 状态变化
        /// </summary>
        StateChanged,

        /// <summary>
        /// 性能警报
        /// </summary>
        PerformanceAlert
    }

    /// <summary>
    /// 事件严重程度
    /// </summary>
    public enum EventSeverity
    {
        /// <summary>
        /// 调试信息
        /// </summary>
        Debug = 0,

        /// <summary>
        /// 一般信息
        /// </summary>
        Information = 1,

        /// <summary>
        /// 警告
        /// </summary>
        Warning = 2,

        /// <summary>
        /// 错误
        /// </summary>
        Error = 3,

        /// <summary>
        /// 严重错误
        /// </summary>
        Critical = 4
    }

    /// <summary>
    /// 进程状态
    /// </summary>
    public enum ProcessState
    {
        /// <summary>
        /// 未知状态
        /// </summary>
        Unknown,

        /// <summary>
        /// 启动中
        /// </summary>
        Starting,

        /// <summary>
        /// 运行中
        /// </summary>
        Running,

        /// <summary>
        /// 已暂停
        /// </summary>
        Suspended,

        /// <summary>
        /// 退出中
        /// </summary>
        Exiting,

        /// <summary>
        /// 已退出
        /// </summary>
        Exited,

        /// <summary>
        /// 错误状态
        /// </summary>
        Error
    }

    /// <summary>
    /// 监控状态
    /// </summary>
    public enum MonitoringState
    {
        /// <summary>
        /// 已停止
        /// </summary>
        Stopped,

        /// <summary>
        /// 启动中
        /// </summary>
        Starting,

        /// <summary>
        /// 运行中
        /// </summary>
        Running,

        /// <summary>
        /// 暂停中
        /// </summary>
        Pausing,

        /// <summary>
        /// 已暂停
        /// </summary>
        Paused,

        /// <summary>
        /// 停止中
        /// </summary>
        Stopping,

        /// <summary>
        /// 错误状态
        /// </summary>
        Error
    }

    /// <summary>
    /// AI工具类型
    /// </summary>
    public enum AIToolType
    {
        /// <summary>
        /// 未知AI工具
        /// </summary>
        Unknown,

        /// <summary>
        /// Claude Code CLI
        /// </summary>
        ClaudeCode,

        /// <summary>
        /// OpenAI Codex CLI
        /// </summary>
        OpenAICodex,

        /// <summary>
        /// GitHub Copilot CLI
        /// </summary>
        GitHubCopilot,

        /// <summary>
        /// 其他AI工具
        /// </summary>
        Other
    }

    #endregion
}