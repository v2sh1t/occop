using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Occop.Models.Monitoring;
using Occop.Services.Monitoring;
using Timer = System.Timers.Timer;

namespace Occop.Services.Security
{
    /// <summary>
    /// 触发器类型枚举
    /// </summary>
    public enum TriggerType
    {
        /// <summary>
        /// 进程退出触发器
        /// </summary>
        ProcessExit,

        /// <summary>
        /// 应用程序关闭触发器
        /// </summary>
        ApplicationShutdown,

        /// <summary>
        /// 系统关机触发器
        /// </summary>
        SystemShutdown,

        /// <summary>
        /// 超时触发器
        /// </summary>
        Timeout,

        /// <summary>
        /// 异常触发器
        /// </summary>
        Exception,

        /// <summary>
        /// 定时触发器
        /// </summary>
        Scheduled,

        /// <summary>
        /// 手动触发器
        /// </summary>
        Manual
    }

    /// <summary>
    /// 触发器状态
    /// </summary>
    public enum TriggerState
    {
        /// <summary>
        /// 已停止
        /// </summary>
        Stopped,

        /// <summary>
        /// 正在启动
        /// </summary>
        Starting,

        /// <summary>
        /// 运行中
        /// </summary>
        Running,

        /// <summary>
        /// 正在停止
        /// </summary>
        Stopping,

        /// <summary>
        /// 错误状态
        /// </summary>
        Error
    }

    /// <summary>
    /// 清理触发事件参数
    /// </summary>
    public class CleanupTriggerEventArgs : EventArgs
    {
        /// <summary>
        /// 触发器类型
        /// </summary>
        public TriggerType TriggerType { get; }

        /// <summary>
        /// 触发原因
        /// </summary>
        public CleanupTriggerReason Reason { get; }

        /// <summary>
        /// 触发时间
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// 相关的进程ID（如果适用）
        /// </summary>
        public int? ProcessId { get; }

        /// <summary>
        /// 触发描述
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// 上下文数据
        /// </summary>
        public Dictionary<string, object> Context { get; }

        /// <summary>
        /// 是否紧急清理
        /// </summary>
        public bool IsUrgent { get; }

        /// <summary>
        /// 初始化触发事件参数
        /// </summary>
        /// <param name="triggerType">触发器类型</param>
        /// <param name="reason">触发原因</param>
        /// <param name="description">描述</param>
        /// <param name="processId">进程ID</param>
        /// <param name="isUrgent">是否紧急</param>
        public CleanupTriggerEventArgs(TriggerType triggerType, CleanupTriggerReason reason, string description = null, int? processId = null, bool isUrgent = false)
        {
            TriggerType = triggerType;
            Reason = reason;
            Description = description;
            ProcessId = processId;
            IsUrgent = isUrgent;
            Timestamp = DateTime.UtcNow;
            Context = new Dictionary<string, object>();
        }

        /// <summary>
        /// 添加上下文数据
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        public void AddContext(string key, object value)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                Context[key] = value;
            }
        }
    }

    /// <summary>
    /// 清理触发器
    /// 负责监控各种事件并触发相应的清理操作
    /// 支持进程退出、异常、超时、系统关机等多种触发机制
    /// </summary>
    public class CleanupTrigger : IDisposable
    {
        #region 事件定义

        /// <summary>
        /// 清理触发事件
        /// </summary>
        public event EventHandler<CleanupTriggerEventArgs> CleanupTriggered;

        /// <summary>
        /// 触发器状态变化事件
        /// </summary>
        public event EventHandler<TriggerState> StateChanged;

        /// <summary>
        /// 触发器错误事件
        /// </summary>
        public event EventHandler<Exception> ErrorOccurred;

        #endregion

        #region 私有字段

        private readonly IProcessMonitor _processMonitor;
        private readonly ConcurrentDictionary<int, ProcessInfo> _monitoredProcesses;
        private readonly ConcurrentDictionary<string, Timer> _timeoutTimers;
        private readonly Timer _scheduledTimer;
        private readonly object _stateLock = new object();
        private readonly CancellationTokenSource _cancellationTokenSource;

        private TriggerState _state;
        private bool _disposed;
        private bool _processExitHandlerRegistered;
        private bool _shutdownHandlerRegistered;

        #endregion

        #region 配置属性

        /// <summary>
        /// 是否启用进程退出监控
        /// </summary>
        public bool ProcessExitMonitoringEnabled { get; set; } = true;

        /// <summary>
        /// 是否启用应用程序关闭监控
        /// </summary>
        public bool ApplicationShutdownMonitoringEnabled { get; set; } = true;

        /// <summary>
        /// 是否启用系统关机监控
        /// </summary>
        public bool SystemShutdownMonitoringEnabled { get; set; } = true;

        /// <summary>
        /// 是否启用超时监控
        /// </summary>
        public bool TimeoutMonitoringEnabled { get; set; } = true;

        /// <summary>
        /// 是否启用定时清理
        /// </summary>
        public bool ScheduledCleanupEnabled { get; set; } = false;

        /// <summary>
        /// 定时清理间隔（分钟）
        /// </summary>
        public int ScheduledCleanupIntervalMinutes { get; set; } = 30;

        /// <summary>
        /// 默认超时时间（分钟）
        /// </summary>
        public int DefaultTimeoutMinutes { get; set; } = 60;

        /// <summary>
        /// 最大超时时间（分钟）
        /// </summary>
        public int MaxTimeoutMinutes { get; set; } = 120;

        #endregion

        #region 属性

        /// <summary>
        /// 当前状态
        /// </summary>
        public TriggerState State
        {
            get
            {
                lock (_stateLock)
                {
                    return _state;
                }
            }
            private set
            {
                TriggerState oldState;
                lock (_stateLock)
                {
                    oldState = _state;
                    _state = value;
                }

                if (oldState != value)
                {
                    StateChanged?.Invoke(this, value);
                }
            }
        }

        /// <summary>
        /// 是否正在运行
        /// </summary>
        public bool IsRunning => State == TriggerState.Running;

        /// <summary>
        /// 监控的进程数量
        /// </summary>
        public int MonitoredProcessCount => _monitoredProcesses.Count;

        /// <summary>
        /// 活动的超时计时器数量
        /// </summary>
        public int ActiveTimeoutCount => _timeoutTimers.Count;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化清理触发器
        /// </summary>
        /// <param name="processMonitor">进程监控器</param>
        public CleanupTrigger(IProcessMonitor processMonitor = null)
        {
            _processMonitor = processMonitor;
            _monitoredProcesses = new ConcurrentDictionary<int, ProcessInfo>();
            _timeoutTimers = new ConcurrentDictionary<string, Timer>();
            _cancellationTokenSource = new CancellationTokenSource();
            _state = TriggerState.Stopped;

            // 初始化定时器
            _scheduledTimer = new Timer();
            _scheduledTimer.Elapsed += OnScheduledCleanup;
            _scheduledTimer.AutoReset = true;
        }

        #endregion

        #region 启动和停止

        /// <summary>
        /// 启动触发器
        /// </summary>
        /// <returns>是否启动成功</returns>
        public async Task<bool> StartAsync()
        {
            if (State != TriggerState.Stopped)
            {
                return false;
            }

            try
            {
                State = TriggerState.Starting;

                // 注册应用程序关闭事件
                if (ApplicationShutdownMonitoringEnabled && !_processExitHandlerRegistered)
                {
                    AppDomain.CurrentDomain.ProcessExit += OnApplicationShutdown;
                    AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
                    _processExitHandlerRegistered = true;
                }

                // 注册系统关机事件
                if (SystemShutdownMonitoringEnabled && !_shutdownHandlerRegistered)
                {
                    try
                    {
                        Microsoft.Win32.SystemEvents.SessionEnding += OnSystemShutdown;
                        _shutdownHandlerRegistered = true;
                    }
                    catch (Exception ex)
                    {
                        // 在某些环境下可能无法注册系统事件，记录但不阻止启动
                        OnError(new InvalidOperationException("无法注册系统关机事件", ex));
                    }
                }

                // 连接进程监控器事件
                if (_processMonitor != null && ProcessExitMonitoringEnabled)
                {
                    _processMonitor.ProcessExited += OnProcessExited;
                    _processMonitor.ProcessKilled += OnProcessKilled;
                    _processMonitor.ErrorOccurred += OnProcessMonitorError;
                }

                // 启动定时清理
                if (ScheduledCleanupEnabled && ScheduledCleanupIntervalMinutes > 0)
                {
                    _scheduledTimer.Interval = TimeSpan.FromMinutes(ScheduledCleanupIntervalMinutes).TotalMilliseconds;
                    _scheduledTimer.Start();
                }

                State = TriggerState.Running;
                return true;
            }
            catch (Exception ex)
            {
                State = TriggerState.Error;
                OnError(ex);
                return false;
            }
        }

        /// <summary>
        /// 停止触发器
        /// </summary>
        /// <returns>是否停止成功</returns>
        public async Task<bool> StopAsync()
        {
            if (State == TriggerState.Stopped || State == TriggerState.Stopping)
            {
                return true;
            }

            try
            {
                State = TriggerState.Stopping;

                // 停止定时器
                _scheduledTimer?.Stop();

                // 清理所有超时计时器
                foreach (var timer in _timeoutTimers.Values)
                {
                    timer?.Stop();
                    timer?.Dispose();
                }
                _timeoutTimers.Clear();

                // 取消所有待处理的任务
                _cancellationTokenSource.Cancel();

                // 断开进程监控器事件
                if (_processMonitor != null)
                {
                    _processMonitor.ProcessExited -= OnProcessExited;
                    _processMonitor.ProcessKilled -= OnProcessKilled;
                    _processMonitor.ErrorOccurred -= OnProcessMonitorError;
                }

                // 注销系统事件
                if (_shutdownHandlerRegistered)
                {
                    try
                    {
                        Microsoft.Win32.SystemEvents.SessionEnding -= OnSystemShutdown;
                        _shutdownHandlerRegistered = false;
                    }
                    catch
                    {
                        // 忽略注销错误
                    }
                }

                // 注销应用程序事件（通常不需要，因为应用程序即将关闭）
                if (_processExitHandlerRegistered)
                {
                    AppDomain.CurrentDomain.ProcessExit -= OnApplicationShutdown;
                    AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
                    _processExitHandlerRegistered = false;
                }

                State = TriggerState.Stopped;
                return true;
            }
            catch (Exception ex)
            {
                State = TriggerState.Error;
                OnError(ex);
                return false;
            }
        }

        #endregion

        #region 进程监控

        /// <summary>
        /// 添加进程监控
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="timeoutMinutes">超时时间（分钟）</param>
        /// <returns>是否添加成功</returns>
        public bool AddProcessMonitoring(int processId, int timeoutMinutes = 0)
        {
            if (!IsRunning)
            {
                return false;
            }

            try
            {
                var process = Process.GetProcessById(processId);
                var processInfo = new ProcessInfo
                {
                    ProcessId = processId,
                    ProcessName = process.ProcessName,
                    StartTime = process.StartTime,
                    IsMonitored = true
                };

                if (_monitoredProcesses.TryAdd(processId, processInfo))
                {
                    // 设置超时监控
                    if (TimeoutMonitoringEnabled && timeoutMinutes > 0)
                    {
                        SetProcessTimeout(processId, Math.Min(timeoutMinutes, MaxTimeoutMinutes));
                    }
                    else if (TimeoutMonitoringEnabled && DefaultTimeoutMinutes > 0)
                    {
                        SetProcessTimeout(processId, DefaultTimeoutMinutes);
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                OnError(ex);
            }

            return false;
        }

        /// <summary>
        /// 移除进程监控
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <returns>是否移除成功</returns>
        public bool RemoveProcessMonitoring(int processId)
        {
            var removed = _monitoredProcesses.TryRemove(processId, out _);

            // 移除对应的超时计时器
            var timeoutKey = $"process_{processId}";
            if (_timeoutTimers.TryRemove(timeoutKey, out var timer))
            {
                timer.Stop();
                timer.Dispose();
            }

            return removed;
        }

        /// <summary>
        /// 设置进程超时
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="timeoutMinutes">超时时间（分钟）</param>
        public void SetProcessTimeout(int processId, int timeoutMinutes)
        {
            if (!TimeoutMonitoringEnabled || timeoutMinutes <= 0)
            {
                return;
            }

            var timeoutKey = $"process_{processId}";

            // 移除现有的超时计时器
            if (_timeoutTimers.TryRemove(timeoutKey, out var existingTimer))
            {
                existingTimer.Stop();
                existingTimer.Dispose();
            }

            // 创建新的超时计时器
            var timer = new Timer(TimeSpan.FromMinutes(timeoutMinutes).TotalMilliseconds);
            timer.Elapsed += (sender, e) => OnProcessTimeout(processId);
            timer.AutoReset = false;
            timer.Start();

            _timeoutTimers.TryAdd(timeoutKey, timer);
        }

        #endregion

        #region 手动触发

        /// <summary>
        /// 手动触发清理
        /// </summary>
        /// <param name="reason">触发原因</param>
        /// <param name="description">描述</param>
        /// <param name="isUrgent">是否紧急</param>
        public void TriggerCleanup(CleanupTriggerReason reason, string description = null, bool isUrgent = false)
        {
            var args = new CleanupTriggerEventArgs(TriggerType.Manual, reason, description, null, isUrgent);
            OnCleanupTriggered(args);
        }

        /// <summary>
        /// 为特定进程触发清理
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="reason">触发原因</param>
        /// <param name="description">描述</param>
        /// <param name="isUrgent">是否紧急</param>
        public void TriggerProcessCleanup(int processId, CleanupTriggerReason reason, string description = null, bool isUrgent = false)
        {
            var args = new CleanupTriggerEventArgs(TriggerType.ProcessExit, reason, description, processId, isUrgent);
            OnCleanupTriggered(args);
        }

        #endregion

        #region 事件处理器

        /// <summary>
        /// 处理进程正常退出事件
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">事件参数</param>
        private void OnProcessExited(object sender, ProcessMonitoringEventArgs e)
        {
            if (!ProcessExitMonitoringEnabled || !IsRunning)
            {
                return;
            }

            var processId = e.ProcessInfo.ProcessId;
            var args = new CleanupTriggerEventArgs(
                TriggerType.ProcessExit,
                CleanupTriggerReason.ProcessExit,
                $"进程 {e.ProcessInfo.ProcessName} (ID: {processId}) 正常退出",
                processId,
                false
            );

            args.AddContext("ProcessName", e.ProcessInfo.ProcessName);
            args.AddContext("ExitCode", e.ProcessInfo.ExitCode);
            args.AddContext("ExitTime", e.ProcessInfo.ExitTime);

            // 移除监控
            RemoveProcessMonitoring(processId);

            OnCleanupTriggered(args);
        }

        /// <summary>
        /// 处理进程异常终止事件
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">事件参数</param>
        private void OnProcessKilled(object sender, ProcessMonitoringEventArgs e)
        {
            if (!ProcessExitMonitoringEnabled || !IsRunning)
            {
                return;
            }

            var processId = e.ProcessInfo.ProcessId;
            var args = new CleanupTriggerEventArgs(
                TriggerType.ProcessExit,
                CleanupTriggerReason.ProcessCrash,
                $"进程 {e.ProcessInfo.ProcessName} (ID: {processId}) 异常终止",
                processId,
                true // 异常终止为紧急情况
            );

            args.AddContext("ProcessName", e.ProcessInfo.ProcessName);
            args.AddContext("ExitCode", e.ProcessInfo.ExitCode);
            args.AddContext("ExitReason", e.ProcessInfo.ExitReason);

            // 移除监控
            RemoveProcessMonitoring(processId);

            OnCleanupTriggered(args);
        }

        /// <summary>
        /// 处理进程监控错误事件
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">事件参数</param>
        private void OnProcessMonitorError(object sender, MonitoringErrorEventArgs e)
        {
            OnError(e.Exception);
        }

        /// <summary>
        /// 处理进程超时事件
        /// </summary>
        /// <param name="processId">进程ID</param>
        private void OnProcessTimeout(int processId)
        {
            if (!TimeoutMonitoringEnabled || !IsRunning)
            {
                return;
            }

            string processName = "Unknown";
            if (_monitoredProcesses.TryGetValue(processId, out var processInfo))
            {
                processName = processInfo.ProcessName;
            }

            var args = new CleanupTriggerEventArgs(
                TriggerType.Timeout,
                CleanupTriggerReason.Timeout,
                $"进程 {processName} (ID: {processId}) 运行超时",
                processId,
                true // 超时为紧急情况
            );

            args.AddContext("ProcessName", processName);
            args.AddContext("TimeoutMinutes", DefaultTimeoutMinutes);

            OnCleanupTriggered(args);
        }

        /// <summary>
        /// 处理应用程序关闭事件
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">事件参数</param>
        private void OnApplicationShutdown(object sender, EventArgs e)
        {
            if (!ApplicationShutdownMonitoringEnabled)
            {
                return;
            }

            var args = new CleanupTriggerEventArgs(
                TriggerType.ApplicationShutdown,
                CleanupTriggerReason.ApplicationShutdown,
                "应用程序正在关闭",
                null,
                true // 关闭为紧急情况
            );

            OnCleanupTriggered(args);
        }

        /// <summary>
        /// 处理未处理异常事件
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">事件参数</param>
        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;

            var args = new CleanupTriggerEventArgs(
                TriggerType.Exception,
                CleanupTriggerReason.Exception,
                $"发生未处理异常: {exception?.Message ?? "Unknown"}",
                null,
                true // 异常为紧急情况
            );

            args.AddContext("Exception", exception);
            args.AddContext("IsTerminating", e.IsTerminating);

            OnCleanupTriggered(args);
        }

        /// <summary>
        /// 处理系统关机事件
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">事件参数</param>
        private void OnSystemShutdown(object sender, Microsoft.Win32.SessionEndingEventArgs e)
        {
            if (!SystemShutdownMonitoringEnabled)
            {
                return;
            }

            var reason = e.Reason == Microsoft.Win32.SessionEndReasons.SystemShutdown
                ? CleanupTriggerReason.SystemShutdown
                : CleanupTriggerReason.ApplicationShutdown;

            var args = new CleanupTriggerEventArgs(
                TriggerType.SystemShutdown,
                reason,
                $"系统正在关闭: {e.Reason}",
                null,
                true // 系统关机为紧急情况
            );

            args.AddContext("ShutdownReason", e.Reason.ToString());

            OnCleanupTriggered(args);
        }

        /// <summary>
        /// 处理定时清理事件
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">事件参数</param>
        private void OnScheduledCleanup(object sender, ElapsedEventArgs e)
        {
            if (!ScheduledCleanupEnabled || !IsRunning)
            {
                return;
            }

            var args = new CleanupTriggerEventArgs(
                TriggerType.Scheduled,
                CleanupTriggerReason.Scheduled,
                "定时清理触发",
                null,
                false // 定时清理不是紧急情况
            );

            args.AddContext("ScheduledTime", e.SignalTime);
            args.AddContext("IntervalMinutes", ScheduledCleanupIntervalMinutes);

            OnCleanupTriggered(args);
        }

        #endregion

        #region 事件通知

        /// <summary>
        /// 触发清理事件
        /// </summary>
        /// <param name="args">事件参数</param>
        protected virtual void OnCleanupTriggered(CleanupTriggerEventArgs args)
        {
            try
            {
                CleanupTriggered?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                OnError(ex);
            }
        }

        /// <summary>
        /// 触发错误事件
        /// </summary>
        /// <param name="exception">异常</param>
        protected virtual void OnError(Exception exception)
        {
            try
            {
                ErrorOccurred?.Invoke(this, exception);
            }
            catch
            {
                // 避免错误处理中的无限循环
            }
        }

        #endregion

        #region 状态查询

        /// <summary>
        /// 获取监控的进程列表
        /// </summary>
        /// <returns>进程信息列表</returns>
        public IReadOnlyList<ProcessInfo> GetMonitoredProcesses()
        {
            return _monitoredProcesses.Values.ToList().AsReadOnly();
        }

        /// <summary>
        /// 获取活动的超时计时器信息
        /// </summary>
        /// <returns>超时信息字典</returns>
        public IReadOnlyDictionary<string, DateTime> GetActiveTimeouts()
        {
            var timeouts = new Dictionary<string, DateTime>();

            foreach (var kvp in _timeoutTimers)
            {
                var timer = kvp.Value;
                if (timer.Enabled)
                {
                    var remainingMs = timer.Interval - (DateTime.UtcNow - DateTime.UtcNow).TotalMilliseconds;
                    timeouts[kvp.Key] = DateTime.UtcNow.AddMilliseconds(Math.Max(0, remainingMs));
                }
            }

            return timeouts;
        }

        /// <summary>
        /// 检查进程是否被监控
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <returns>是否被监控</returns>
        public bool IsProcessMonitored(int processId)
        {
            return _monitoredProcesses.ContainsKey(processId);
        }

        #endregion

        #region 资源清理

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                // 停止触发器
                StopAsync().Wait(TimeSpan.FromSeconds(5));

                // 释放定时器
                _scheduledTimer?.Dispose();

                // 释放取消令牌源
                _cancellationTokenSource?.Dispose();
            }
            catch
            {
                // 在释放过程中忽略错误
            }
            finally
            {
                _disposed = true;
            }
        }

        #endregion
    }
}