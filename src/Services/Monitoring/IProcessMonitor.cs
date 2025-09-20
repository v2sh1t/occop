using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Occop.Models.Monitoring;

namespace Occop.Services.Monitoring
{
    /// <summary>
    /// 进程监控器接口，提供AI工具进程的实时监控功能
    /// 支持进程启动检测、状态跟踪、异常退出监控和进程树管理
    /// </summary>
    public interface IProcessMonitor : IDisposable
    {
        #region 事件定义

        /// <summary>
        /// 监控的进程启动时触发的事件
        /// </summary>
        event EventHandler<ProcessMonitoringEventArgs> ProcessStarted;

        /// <summary>
        /// 监控的进程正常退出时触发的事件
        /// </summary>
        event EventHandler<ProcessMonitoringEventArgs> ProcessExited;

        /// <summary>
        /// 监控的进程异常终止时触发的事件
        /// </summary>
        event EventHandler<ProcessMonitoringEventArgs> ProcessKilled;

        /// <summary>
        /// 监控状态变化时触发的事件
        /// </summary>
        event EventHandler<MonitoringStateChangedEventArgs> StateChanged;

        /// <summary>
        /// 监控错误时触发的事件
        /// </summary>
        event EventHandler<MonitoringErrorEventArgs> ErrorOccurred;

        #endregion

        #region 属性

        /// <summary>
        /// 获取当前监控状态
        /// </summary>
        MonitoringState State { get; }

        /// <summary>
        /// 获取是否正在监控中
        /// </summary>
        bool IsMonitoring { get; }

        /// <summary>
        /// 获取当前监控的进程数量
        /// </summary>
        int MonitoredProcessCount { get; }

        /// <summary>
        /// 获取监控器启动时间
        /// </summary>
        DateTime? StartTime { get; }

        /// <summary>
        /// 获取所有被监控的进程信息
        /// </summary>
        IReadOnlyList<ProcessInfo> MonitoredProcesses { get; }

        #endregion

        #region 核心监控方法

        /// <summary>
        /// 异步启动进程监控器
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>启动结果</returns>
        Task<MonitoringResult> StartMonitoringAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步停止进程监控器
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>停止结果</returns>
        Task<MonitoringResult> StopMonitoringAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 添加要监控的进程（通过进程ID）
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="processName">进程名称（可选，用于标识）</param>
        /// <returns>添加结果</returns>
        MonitoringResult AddProcess(int processId, string processName = null);

        /// <summary>
        /// 添加要监控的进程（通过进程名称模式）
        /// </summary>
        /// <param name="processNamePattern">进程名称模式（支持通配符）</param>
        /// <returns>添加结果</returns>
        MonitoringResult AddProcessByName(string processNamePattern);

        /// <summary>
        /// 移除被监控的进程
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <returns>移除结果</returns>
        MonitoringResult RemoveProcess(int processId);

        /// <summary>
        /// 清除所有被监控的进程
        /// </summary>
        /// <returns>清除结果</returns>
        MonitoringResult ClearAllProcesses();

        #endregion

        #region 进程查询方法

        /// <summary>
        /// 检查指定进程是否正在被监控
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <returns>是否被监控</returns>
        bool IsProcessMonitored(int processId);

        /// <summary>
        /// 获取指定进程的监控信息
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <returns>进程监控信息，如果未找到则返回null</returns>
        ProcessInfo GetProcessInfo(int processId);

        /// <summary>
        /// 获取指定进程名称的所有监控进程
        /// </summary>
        /// <param name="processName">进程名称</param>
        /// <returns>匹配的进程列表</returns>
        IList<ProcessInfo> GetProcessesByName(string processName);

        /// <summary>
        /// 异步刷新所有监控进程的状态
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>刷新结果</returns>
        Task<MonitoringResult> RefreshProcessStatesAsync(CancellationToken cancellationToken = default);

        #endregion

        #region 高级功能 (为Stream B/C预留接口)

        /// <summary>
        /// 启用WMI事件监听（由Stream B实现）
        /// </summary>
        /// <param name="enabled">是否启用</param>
        /// <returns>设置结果</returns>
        MonitoringResult SetWmiEventListenerEnabled(bool enabled);

        /// <summary>
        /// 设置进程监控配置
        /// </summary>
        /// <param name="config">监控配置</param>
        /// <returns>设置结果</returns>
        MonitoringResult SetMonitoringConfig(ProcessMonitoringConfig config);

        /// <summary>
        /// 获取监控统计信息
        /// </summary>
        /// <returns>监控统计</returns>
        MonitoringStatistics GetStatistics();

        /// <summary>
        /// 异步执行健康检查
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>健康检查结果</returns>
        Task<MonitoringHealthResult> CheckHealthAsync(CancellationToken cancellationToken = default);

        #endregion
    }

    #region 相关事件参数类

    /// <summary>
    /// 进程监控事件参数
    /// </summary>
    public class ProcessMonitoringEventArgs : EventArgs
    {
        /// <summary>
        /// 进程信息
        /// </summary>
        public ProcessInfo ProcessInfo { get; }

        /// <summary>
        /// 事件类型
        /// </summary>
        public MonitoringEventType EventType { get; }

        /// <summary>
        /// 事件发生时间
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// 事件描述
        /// </summary>
        public string Description { get; }

        public ProcessMonitoringEventArgs(ProcessInfo processInfo, MonitoringEventType eventType, string description = null)
        {
            ProcessInfo = processInfo ?? throw new ArgumentNullException(nameof(processInfo));
            EventType = eventType;
            Timestamp = DateTime.UtcNow;
            Description = description;
        }
    }

    /// <summary>
    /// 监控状态变化事件参数
    /// </summary>
    public class MonitoringStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 之前的状态
        /// </summary>
        public MonitoringState PreviousState { get; }

        /// <summary>
        /// 当前状态
        /// </summary>
        public MonitoringState CurrentState { get; }

        /// <summary>
        /// 状态变化时间
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// 状态变化原因
        /// </summary>
        public string Reason { get; }

        public MonitoringStateChangedEventArgs(MonitoringState previousState, MonitoringState currentState, string reason = null)
        {
            PreviousState = previousState;
            CurrentState = currentState;
            Timestamp = DateTime.UtcNow;
            Reason = reason;
        }
    }

    /// <summary>
    /// 监控错误事件参数
    /// </summary>
    public class MonitoringErrorEventArgs : EventArgs
    {
        /// <summary>
        /// 错误异常
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 错误发生时间
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// 相关的进程ID（如果有）
        /// </summary>
        public int? ProcessId { get; }

        public MonitoringErrorEventArgs(Exception exception, string message = null, int? processId = null)
        {
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
            Message = message ?? exception.Message;
            Timestamp = DateTime.UtcNow;
            ProcessId = processId;
        }
    }

    #endregion
}