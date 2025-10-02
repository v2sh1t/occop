using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Occop.Models.Monitoring;

namespace Occop.Services.Monitoring
{
    /// <summary>
    /// 进程跟踪器
    /// 负责跟踪和管理被监控进程的状态，提供PID跟踪和进程Handle管理
    /// </summary>
    public class ProcessTracker : IDisposable
    {
        #region 字段和属性

        private readonly ConcurrentDictionary<int, ProcessInfo> _trackedProcesses;
        private readonly ConcurrentDictionary<int, Process> _processHandles;
        private readonly List<MonitoringEvent> _eventHistory;
        private readonly object _eventHistoryLock = new object();
        private readonly Timer _refreshTimer;
        private readonly ProcessMonitoringConfig _config;
        private volatile bool _disposed;
        private volatile bool _isTracking;

        /// <summary>
        /// 获取是否正在跟踪中
        /// </summary>
        public bool IsTracking => _isTracking && !_disposed;

        /// <summary>
        /// 获取被跟踪的进程数量
        /// </summary>
        public int TrackedProcessCount => _trackedProcesses.Count;

        /// <summary>
        /// 获取所有被跟踪的进程信息
        /// </summary>
        public IReadOnlyList<ProcessInfo> TrackedProcesses => _trackedProcesses.Values.ToList().AsReadOnly();

        /// <summary>
        /// 获取事件历史记录
        /// </summary>
        public IReadOnlyList<MonitoringEvent> EventHistory
        {
            get
            {
                lock (_eventHistoryLock)
                {
                    return _eventHistory.ToList().AsReadOnly();
                }
            }
        }

        #endregion

        #region 事件定义

        /// <summary>
        /// 进程状态变化事件
        /// </summary>
        public event EventHandler<ProcessStateChangedEventArgs> ProcessStateChanged;

        /// <summary>
        /// 进程添加事件
        /// </summary>
        public event EventHandler<ProcessTrackedEventArgs> ProcessAdded;

        /// <summary>
        /// 进程移除事件
        /// </summary>
        public event EventHandler<ProcessTrackedEventArgs> ProcessRemoved;

        /// <summary>
        /// 跟踪错误事件
        /// </summary>
        public event EventHandler<TrackingErrorEventArgs> TrackingError;

        #endregion

        #region 构造函数和初始化

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="config">监控配置</param>
        public ProcessTracker(ProcessMonitoringConfig config = null)
        {
            _config = config ?? new ProcessMonitoringConfig();
            _trackedProcesses = new ConcurrentDictionary<int, ProcessInfo>();
            _processHandles = new ConcurrentDictionary<int, Process>();
            _eventHistory = new List<MonitoringEvent>();

            // 创建定时刷新定时器
            _refreshTimer = new Timer(RefreshProcessStates, null, Timeout.Infinite, Timeout.Infinite);
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 开始跟踪
        /// </summary>
        /// <returns>操作结果</returns>
        public MonitoringResult StartTracking()
        {
            try
            {
                if (_disposed)
                    return MonitoringResult.Failure("进程跟踪器已被释放");

                if (_isTracking)
                    return MonitoringResult.Success("进程跟踪器已在运行中");

                _isTracking = true;

                // 启动定时刷新
                _refreshTimer.Change(0, _config.PollingIntervalMs);

                var evt = MonitoringEvent.CreateStateChanged(MonitoringState.Stopped, MonitoringState.Running, "开始进程跟踪");
                AddEvent(evt);

                return MonitoringResult.Success("进程跟踪器已启动");
            }
            catch (Exception ex)
            {
                var errorEvent = MonitoringEvent.CreateError(ex, "启动进程跟踪器");
                AddEvent(errorEvent);
                OnTrackingError(new TrackingErrorEventArgs(ex, "启动进程跟踪器失败"));
                return MonitoringResult.Failure("启动失败", ex);
            }
        }

        /// <summary>
        /// 停止跟踪
        /// </summary>
        /// <returns>操作结果</returns>
        public MonitoringResult StopTracking()
        {
            try
            {
                if (!_isTracking)
                    return MonitoringResult.Success("进程跟踪器已停止");

                _isTracking = false;

                // 停止定时刷新
                _refreshTimer.Change(Timeout.Infinite, Timeout.Infinite);

                var evt = MonitoringEvent.CreateStateChanged(MonitoringState.Running, MonitoringState.Stopped, "停止进程跟踪");
                AddEvent(evt);

                return MonitoringResult.Success("进程跟踪器已停止");
            }
            catch (Exception ex)
            {
                var errorEvent = MonitoringEvent.CreateError(ex, "停止进程跟踪器");
                AddEvent(errorEvent);
                OnTrackingError(new TrackingErrorEventArgs(ex, "停止进程跟踪器失败"));
                return MonitoringResult.Failure("停止失败", ex);
            }
        }

        /// <summary>
        /// 添加要跟踪的进程
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="processName">进程名称（可选）</param>
        /// <param name="tags">标签（可选）</param>
        /// <returns>操作结果</returns>
        public MonitoringResult AddProcess(int processId, string processName = null, params string[] tags)
        {
            try
            {
                if (_disposed)
                    return MonitoringResult.Failure("进程跟踪器已被释放");

                if (_trackedProcesses.ContainsKey(processId))
                    return MonitoringResult.Success($"进程 {processId} 已在跟踪列表中");

                if (_trackedProcesses.Count >= _config.MaxMonitoredProcesses)
                    return MonitoringResult.Failure($"已达到最大监控进程数量限制: {_config.MaxMonitoredProcesses}");

                // 尝试获取进程对象
                Process process = null;
                try
                {
                    process = Process.GetProcessById(processId);
                }
                catch (ArgumentException)
                {
                    return MonitoringResult.Failure($"进程 {processId} 不存在");
                }

                // 创建进程信息
                var processInfo = new ProcessInfo(process)
                {
                    ProcessName = processName ?? process.ProcessName
                };

                // 添加标签
                if (tags != null)
                {
                    foreach (var tag in tags)
                    {
                        processInfo.AddTag(tag);
                    }
                }

                // 检测AI工具类型
                DetectAIToolType(processInfo);

                // 添加到跟踪列表
                _trackedProcesses[processId] = processInfo;
                _processHandles[processId] = process;

                // 触发事件
                var evt = MonitoringEvent.CreateProcessStarted(processInfo);
                AddEvent(evt);
                OnProcessAdded(new ProcessTrackedEventArgs(processInfo));

                return MonitoringResult.Success($"已添加进程到跟踪列表: {processInfo}");
            }
            catch (Exception ex)
            {
                var errorEvent = MonitoringEvent.CreateError(ex, $"添加进程跟踪: {processId}");
                AddEvent(errorEvent);
                OnTrackingError(new TrackingErrorEventArgs(ex, $"添加进程 {processId} 失败"));
                return MonitoringResult.Failure("添加进程失败", ex);
            }
        }

        /// <summary>
        /// 移除被跟踪的进程
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <returns>操作结果</returns>
        public MonitoringResult RemoveProcess(int processId)
        {
            try
            {
                if (!_trackedProcesses.TryRemove(processId, out var processInfo))
                    return MonitoringResult.Failure($"进程 {processId} 不在跟踪列表中");

                // 清理进程句柄
                if (_processHandles.TryRemove(processId, out var process))
                {
                    try
                    {
                        process?.Dispose();
                    }
                    catch { /* 忽略释放异常 */ }
                }

                // 创建移除事件
                var evt = new MonitoringEvent(MonitoringEventType.Information, processInfo,
                    $"移除进程跟踪: {processInfo.ProcessName} [{processId}]",
                    $"进程 {processInfo.ProcessName} (ID: {processId}) 已从跟踪列表中移除");
                AddEvent(evt);

                OnProcessRemoved(new ProcessTrackedEventArgs(processInfo));

                return MonitoringResult.Success($"已移除进程跟踪: {processInfo}");
            }
            catch (Exception ex)
            {
                var errorEvent = MonitoringEvent.CreateError(ex, $"移除进程跟踪: {processId}");
                AddEvent(errorEvent);
                OnTrackingError(new TrackingErrorEventArgs(ex, $"移除进程 {processId} 失败"));
                return MonitoringResult.Failure("移除进程失败", ex);
            }
        }

        /// <summary>
        /// 清除所有跟踪的进程
        /// </summary>
        /// <returns>操作结果</returns>
        public MonitoringResult ClearAllProcesses()
        {
            try
            {
                var processCount = _trackedProcesses.Count;

                // 清理所有进程句柄
                foreach (var kvp in _processHandles)
                {
                    try
                    {
                        kvp.Value?.Dispose();
                    }
                    catch { /* 忽略释放异常 */ }
                }

                _trackedProcesses.Clear();
                _processHandles.Clear();

                var evt = new MonitoringEvent(MonitoringEventType.Information,
                    "清除所有进程跟踪",
                    $"已清除 {processCount} 个被跟踪的进程");
                AddEvent(evt);

                return MonitoringResult.Success($"已清除 {processCount} 个被跟踪的进程");
            }
            catch (Exception ex)
            {
                var errorEvent = MonitoringEvent.CreateError(ex, "清除所有进程跟踪");
                AddEvent(errorEvent);
                OnTrackingError(new TrackingErrorEventArgs(ex, "清除所有进程跟踪失败"));
                return MonitoringResult.Failure("清除失败", ex);
            }
        }

        /// <summary>
        /// 获取进程信息
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <returns>进程信息，未找到则返回null</returns>
        public ProcessInfo GetProcessInfo(int processId)
        {
            return _trackedProcesses.TryGetValue(processId, out var info) ? info : null;
        }

        /// <summary>
        /// 检查进程是否被跟踪
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <returns>是否被跟踪</returns>
        public bool IsProcessTracked(int processId)
        {
            return _trackedProcesses.ContainsKey(processId);
        }

        /// <summary>
        /// 按名称获取进程列表
        /// </summary>
        /// <param name="processName">进程名称</param>
        /// <returns>匹配的进程列表</returns>
        public IList<ProcessInfo> GetProcessesByName(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return new List<ProcessInfo>();

            return _trackedProcesses.Values
                .Where(p => string.Equals(p.ProcessName, processName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>
        /// 异步刷新所有进程状态
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果</returns>
        public async Task<MonitoringResult> RefreshProcessStatesAsync(CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => RefreshProcessStatesInternal(), cancellationToken);
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 定时刷新进程状态
        /// </summary>
        /// <param name="state">状态对象</param>
        private void RefreshProcessStates(object state)
        {
            if (!_isTracking || _disposed)
                return;

            try
            {
                RefreshProcessStatesInternal();
            }
            catch (Exception ex)
            {
                var errorEvent = MonitoringEvent.CreateError(ex, "定时刷新进程状态");
                AddEvent(errorEvent);
                OnTrackingError(new TrackingErrorEventArgs(ex, "定时刷新进程状态失败"));
            }
        }

        /// <summary>
        /// 内部刷新进程状态逻辑
        /// </summary>
        /// <returns>操作结果</returns>
        private MonitoringResult RefreshProcessStatesInternal()
        {
            var updateCount = 0;
            var errorCount = 0;

            foreach (var kvp in _trackedProcesses.ToList())
            {
                var processId = kvp.Key;
                var processInfo = kvp.Value;

                try
                {
                    if (_processHandles.TryGetValue(processId, out var process))
                    {
                        var previousState = processInfo.State;
                        processInfo.UpdateFromProcess(process);

                        // 检查状态是否发生变化
                        if (previousState != processInfo.State)
                        {
                            OnProcessStateChanged(new ProcessStateChangedEventArgs(processInfo, previousState, processInfo.State));

                            // 如果进程已退出，创建相应事件并移除跟踪
                            if (processInfo.HasExited)
                            {
                                var exitEvent = processInfo.IsAbnormalExit
                                    ? MonitoringEvent.CreateProcessKilled(processInfo)
                                    : MonitoringEvent.CreateProcessExited(processInfo);

                                AddEvent(exitEvent);

                                // 异步移除进程（避免在枚举过程中修改集合）
                                _ = Task.Run(() => RemoveProcess(processId));
                            }
                        }

                        updateCount++;
                    }
                }
                catch (Exception ex)
                {
                    errorCount++;
                    var errorEvent = MonitoringEvent.CreateError(ex, $"刷新进程状态: {processId}", processId);
                    AddEvent(errorEvent);

                    // 如果进程访问异常，标记为错误状态
                    processInfo.State = ProcessState.Error;
                    processInfo.ExitReason = ex.Message;
                }
            }

            // 清理过期事件
            CleanupEventHistory();

            return MonitoringResult.Success($"刷新完成 - 更新: {updateCount}, 错误: {errorCount}");
        }

        /// <summary>
        /// 检测AI工具类型
        /// </summary>
        /// <param name="processInfo">进程信息</param>
        private void DetectAIToolType(ProcessInfo processInfo)
        {
            var processName = processInfo.ProcessName?.ToLowerInvariant();
            var fullPath = processInfo.FullPath?.ToLowerInvariant();

            if (string.IsNullOrEmpty(processName))
                return;

            // 检测Claude Code
            if (processName.Contains("claude-code") || processName.Contains("claude") && processName.Contains("code"))
            {
                processInfo.MarkAsAITool(AIToolType.ClaudeCode);
            }
            // 检测OpenAI Codex
            else if (processName.Contains("codex") || processName.Contains("openai"))
            {
                processInfo.MarkAsAITool(AIToolType.OpenAICodex);
            }
            // 检测GitHub Copilot
            else if (processName.Contains("copilot") || processName.Contains("github") && processName.Contains("copilot"))
            {
                processInfo.MarkAsAITool(AIToolType.GitHubCopilot);
            }
            // 通过路径检测
            else if (!string.IsNullOrEmpty(fullPath))
            {
                if (fullPath.Contains("claude") || fullPath.Contains("anthropic"))
                {
                    processInfo.MarkAsAITool(AIToolType.ClaudeCode);
                }
                else if (fullPath.Contains("openai") || fullPath.Contains("codex"))
                {
                    processInfo.MarkAsAITool(AIToolType.OpenAICodex);
                }
                else if (fullPath.Contains("copilot") || fullPath.Contains("github"))
                {
                    processInfo.MarkAsAITool(AIToolType.GitHubCopilot);
                }
            }
        }

        /// <summary>
        /// 添加事件到历史记录
        /// </summary>
        /// <param name="evt">监控事件</param>
        private void AddEvent(MonitoringEvent evt)
        {
            if (evt == null)
                return;

            lock (_eventHistoryLock)
            {
                _eventHistory.Add(evt);

                // 限制事件历史记录数量
                while (_eventHistory.Count > _config.MaxEventHistory)
                {
                    _eventHistory.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// 清理过期事件历史
        /// </summary>
        private void CleanupEventHistory()
        {
            if (_config.AutoCleanupEventHours <= 0)
                return;

            var cutoffTime = DateTime.UtcNow.AddHours(-_config.AutoCleanupEventHours);

            lock (_eventHistoryLock)
            {
                _eventHistory.RemoveAll(e => e.Timestamp < cutoffTime);
            }
        }

        #endregion

        #region 事件触发方法

        /// <summary>
        /// 触发进程状态变化事件
        /// </summary>
        private void OnProcessStateChanged(ProcessStateChangedEventArgs e)
        {
            ProcessStateChanged?.Invoke(this, e);
        }

        /// <summary>
        /// 触发进程添加事件
        /// </summary>
        private void OnProcessAdded(ProcessTrackedEventArgs e)
        {
            ProcessAdded?.Invoke(this, e);
        }

        /// <summary>
        /// 触发进程移除事件
        /// </summary>
        private void OnProcessRemoved(ProcessTrackedEventArgs e)
        {
            ProcessRemoved?.Invoke(this, e);
        }

        /// <summary>
        /// 触发跟踪错误事件
        /// </summary>
        private void OnTrackingError(TrackingErrorEventArgs e)
        {
            TrackingError?.Invoke(this, e);
        }

        #endregion

        #region IDisposable 实现

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否正在释放</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                try
                {
                    // 停止跟踪
                    StopTracking();

                    // 释放定时器
                    _refreshTimer?.Dispose();

                    // 清理所有进程句柄
                    foreach (var process in _processHandles.Values)
                    {
                        try
                        {
                            process?.Dispose();
                        }
                        catch { /* 忽略释放异常 */ }
                    }

                    _processHandles.Clear();
                    _trackedProcesses.Clear();

                    lock (_eventHistoryLock)
                    {
                        _eventHistory.Clear();
                    }
                }
                catch { /* 忽略清理异常 */ }
            }

            _disposed = true;
        }

        /// <summary>
        /// 析构函数
        /// </summary>
        ~ProcessTracker()
        {
            Dispose(false);
        }

        #endregion
    }

    #region 事件参数类

    /// <summary>
    /// 进程状态变化事件参数
    /// </summary>
    public class ProcessStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 进程信息
        /// </summary>
        public ProcessInfo ProcessInfo { get; }

        /// <summary>
        /// 之前的状态
        /// </summary>
        public ProcessState PreviousState { get; }

        /// <summary>
        /// 当前状态
        /// </summary>
        public ProcessState CurrentState { get; }

        /// <summary>
        /// 变化时间
        /// </summary>
        public DateTime Timestamp { get; }

        public ProcessStateChangedEventArgs(ProcessInfo processInfo, ProcessState previousState, ProcessState currentState)
        {
            ProcessInfo = processInfo ?? throw new ArgumentNullException(nameof(processInfo));
            PreviousState = previousState;
            CurrentState = currentState;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// 进程跟踪事件参数
    /// </summary>
    public class ProcessTrackedEventArgs : EventArgs
    {
        /// <summary>
        /// 进程信息
        /// </summary>
        public ProcessInfo ProcessInfo { get; }

        /// <summary>
        /// 事件时间
        /// </summary>
        public DateTime Timestamp { get; }

        public ProcessTrackedEventArgs(ProcessInfo processInfo)
        {
            ProcessInfo = processInfo ?? throw new ArgumentNullException(nameof(processInfo));
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// 跟踪错误事件参数
    /// </summary>
    public class TrackingErrorEventArgs : EventArgs
    {
        /// <summary>
        /// 异常信息
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// 错误上下文
        /// </summary>
        public string Context { get; }

        /// <summary>
        /// 错误时间
        /// </summary>
        public DateTime Timestamp { get; }

        public TrackingErrorEventArgs(Exception exception, string context = null)
        {
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
            Context = context;
            Timestamp = DateTime.UtcNow;
        }
    }

    #endregion
}