using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Occop.Models.Monitoring;

namespace Occop.Services.Monitoring
{
    /// <summary>
    /// 进程监控器主要实现类
    /// 提供AI工具进程的实时监控功能，基于.NET Process类实现基础监控
    /// </summary>
    public class ProcessMonitor : IProcessMonitor
    {
        #region 字段和属性

        private readonly ProcessTracker _processTracker;
        private readonly ProcessMonitoringConfig _config;
        private volatile MonitoringState _state;
        private volatile bool _disposed;
        private DateTime? _startTime;
        private MonitoringStatistics _statistics;
        private readonly object _statisticsLock = new object();

        /// <summary>
        /// 获取当前监控状态
        /// </summary>
        public MonitoringState State => _state;

        /// <summary>
        /// 获取是否正在监控中
        /// </summary>
        public bool IsMonitoring => _state == MonitoringState.Running && !_disposed;

        /// <summary>
        /// 获取当前监控的进程数量
        /// </summary>
        public int MonitoredProcessCount => _processTracker?.TrackedProcessCount ?? 0;

        /// <summary>
        /// 获取监控器启动时间
        /// </summary>
        public DateTime? StartTime => _startTime;

        /// <summary>
        /// 获取所有被监控的进程信息
        /// </summary>
        public IReadOnlyList<ProcessInfo> MonitoredProcesses => _processTracker?.TrackedProcesses ?? new List<ProcessInfo>().AsReadOnly();

        #endregion

        #region 事件定义

        /// <summary>
        /// 监控的进程启动时触发的事件
        /// </summary>
        public event EventHandler<ProcessMonitoringEventArgs> ProcessStarted;

        /// <summary>
        /// 监控的进程正常退出时触发的事件
        /// </summary>
        public event EventHandler<ProcessMonitoringEventArgs> ProcessExited;

        /// <summary>
        /// 监控的进程异常终止时触发的事件
        /// </summary>
        public event EventHandler<ProcessMonitoringEventArgs> ProcessKilled;

        /// <summary>
        /// 监控状态变化时触发的事件
        /// </summary>
        public event EventHandler<MonitoringStateChangedEventArgs> StateChanged;

        /// <summary>
        /// 监控错误时触发的事件
        /// </summary>
        public event EventHandler<MonitoringErrorEventArgs> ErrorOccurred;

        #endregion

        #region 构造函数和初始化

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public ProcessMonitor() : this(new ProcessMonitoringConfig())
        {
        }

        /// <summary>
        /// 带配置的构造函数
        /// </summary>
        /// <param name="config">监控配置</param>
        public ProcessMonitor(ProcessMonitoringConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _processTracker = new ProcessTracker(_config);
            _state = MonitoringState.Stopped;
            _statistics = new MonitoringStatistics();

            // 订阅进程跟踪器事件
            _processTracker.ProcessAdded += OnProcessAdded;
            _processTracker.ProcessRemoved += OnProcessRemoved;
            _processTracker.ProcessStateChanged += OnProcessStateChanged;
            _processTracker.TrackingError += OnTrackingError;
        }

        #endregion

        #region 核心监控方法

        /// <summary>
        /// 异步启动进程监控器
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>启动结果</returns>
        public async Task<MonitoringResult> StartMonitoringAsync(CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (_disposed)
                        return MonitoringResult.Failure("进程监控器已被释放");

                    if (_state == MonitoringState.Running)
                        return MonitoringResult.Success("进程监控器已在运行中");

                    var previousState = _state;
                    _state = MonitoringState.Starting;
                    OnStateChanged(new MonitoringStateChangedEventArgs(previousState, _state, "启动监控器"));

                    // 启动进程跟踪器
                    var trackerResult = _processTracker.StartTracking();
                    if (!trackerResult.Success)
                    {
                        _state = MonitoringState.Error;
                        OnStateChanged(new MonitoringStateChangedEventArgs(MonitoringState.Starting, _state, "启动跟踪器失败"));
                        return MonitoringResult.Failure($"启动进程跟踪器失败: {trackerResult.ErrorMessage}", trackerResult.Exception);
                    }

                    // 更新状态
                    _startTime = DateTime.UtcNow;
                    _state = MonitoringState.Running;
                    _statistics.StartTime = _startTime.Value;

                    OnStateChanged(new MonitoringStateChangedEventArgs(MonitoringState.Starting, _state, "监控器启动成功"));

                    return MonitoringResult.Success("进程监控器已启动");
                }
                catch (Exception ex)
                {
                    _state = MonitoringState.Error;
                    OnStateChanged(new MonitoringStateChangedEventArgs(MonitoringState.Starting, _state, $"启动异常: {ex.Message}"));
                    OnErrorOccurred(new MonitoringErrorEventArgs(ex, "启动进程监控器失败"));
                    return MonitoringResult.Failure("启动失败", ex);
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 异步停止进程监控器
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>停止结果</returns>
        public async Task<MonitoringResult> StopMonitoringAsync(CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (_state == MonitoringState.Stopped)
                        return MonitoringResult.Success("进程监控器已停止");

                    var previousState = _state;
                    _state = MonitoringState.Stopping;
                    OnStateChanged(new MonitoringStateChangedEventArgs(previousState, _state, "停止监控器"));

                    // 停止进程跟踪器
                    var trackerResult = _processTracker.StopTracking();
                    if (!trackerResult.Success)
                    {
                        OnErrorOccurred(new MonitoringErrorEventArgs(trackerResult.Exception, $"停止进程跟踪器失败: {trackerResult.ErrorMessage}"));
                    }

                    // 更新状态
                    _state = MonitoringState.Stopped;
                    OnStateChanged(new MonitoringStateChangedEventArgs(MonitoringState.Stopping, _state, "监控器停止成功"));

                    return MonitoringResult.Success("进程监控器已停止");
                }
                catch (Exception ex)
                {
                    _state = MonitoringState.Error;
                    OnStateChanged(new MonitoringStateChangedEventArgs(MonitoringState.Stopping, _state, $"停止异常: {ex.Message}"));
                    OnErrorOccurred(new MonitoringErrorEventArgs(ex, "停止进程监控器失败"));
                    return MonitoringResult.Failure("停止失败", ex);
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 添加要监控的进程（通过进程ID）
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="processName">进程名称（可选，用于标识）</param>
        /// <returns>添加结果</returns>
        public MonitoringResult AddProcess(int processId, string processName = null)
        {
            try
            {
                if (_disposed)
                    return MonitoringResult.Failure("进程监控器已被释放");

                var result = _processTracker.AddProcess(processId, processName);

                if (result.Success)
                {
                    lock (_statisticsLock)
                    {
                        _statistics.TotalProcessesMonitored++;
                        _statistics.CurrentProcessCount = MonitoredProcessCount;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new MonitoringErrorEventArgs(ex, $"添加进程监控失败: {processId}", processId));
                return MonitoringResult.Failure("添加进程失败", ex);
            }
        }

        /// <summary>
        /// 添加要监控的进程（通过进程名称模式）
        /// </summary>
        /// <param name="processNamePattern">进程名称模式（支持通配符）</param>
        /// <returns>添加结果</returns>
        public MonitoringResult AddProcessByName(string processNamePattern)
        {
            try
            {
                if (_disposed)
                    return MonitoringResult.Failure("进程监控器已被释放");

                if (string.IsNullOrWhiteSpace(processNamePattern))
                    return MonitoringResult.Failure("进程名称模式不能为空");

                var addedCount = 0;
                var errorCount = 0;
                var errors = new List<string>();

                // 获取所有匹配的进程
                var allProcesses = Process.GetProcesses();
                foreach (var process in allProcesses)
                {
                    try
                    {
                        if (IsProcessNameMatch(process.ProcessName, processNamePattern))
                        {
                            var result = AddProcess(process.Id, process.ProcessName);
                            if (result.Success)
                            {
                                addedCount++;
                            }
                            else
                            {
                                errorCount++;
                                errors.Add($"进程 {process.ProcessName}[{process.Id}]: {result.ErrorMessage}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        errors.Add($"处理进程 {process.Id} 时出错: {ex.Message}");
                    }
                    finally
                    {
                        try
                        {
                            process.Dispose();
                        }
                        catch { /* 忽略释放异常 */ }
                    }
                }

                var message = $"按名称添加进程完成 - 成功: {addedCount}, 失败: {errorCount}";
                if (errors.Any())
                {
                    message += $"\n错误详情:\n{string.Join("\n", errors)}";
                }

                return addedCount > 0
                    ? MonitoringResult.Success(message)
                    : MonitoringResult.Failure($"未找到匹配的进程: {processNamePattern}");
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new MonitoringErrorEventArgs(ex, $"按名称添加进程失败: {processNamePattern}"));
                return MonitoringResult.Failure("按名称添加进程失败", ex);
            }
        }

        /// <summary>
        /// 移除被监控的进程
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <returns>移除结果</returns>
        public MonitoringResult RemoveProcess(int processId)
        {
            try
            {
                if (_disposed)
                    return MonitoringResult.Failure("进程监控器已被释放");

                var result = _processTracker.RemoveProcess(processId);

                if (result.Success)
                {
                    lock (_statisticsLock)
                    {
                        _statistics.CurrentProcessCount = MonitoredProcessCount;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new MonitoringErrorEventArgs(ex, $"移除进程监控失败: {processId}", processId));
                return MonitoringResult.Failure("移除进程失败", ex);
            }
        }

        /// <summary>
        /// 清除所有被监控的进程
        /// </summary>
        /// <returns>清除结果</returns>
        public MonitoringResult ClearAllProcesses()
        {
            try
            {
                if (_disposed)
                    return MonitoringResult.Failure("进程监控器已被释放");

                var result = _processTracker.ClearAllProcesses();

                if (result.Success)
                {
                    lock (_statisticsLock)
                    {
                        _statistics.CurrentProcessCount = 0;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new MonitoringErrorEventArgs(ex, "清除所有进程监控失败"));
                return MonitoringResult.Failure("清除所有进程失败", ex);
            }
        }

        #endregion

        #region 进程查询方法

        /// <summary>
        /// 检查指定进程是否正在被监控
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <returns>是否被监控</returns>
        public bool IsProcessMonitored(int processId)
        {
            return _processTracker?.IsProcessTracked(processId) ?? false;
        }

        /// <summary>
        /// 获取指定进程的监控信息
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <returns>进程监控信息，如果未找到则返回null</returns>
        public ProcessInfo GetProcessInfo(int processId)
        {
            return _processTracker?.GetProcessInfo(processId);
        }

        /// <summary>
        /// 获取指定进程名称的所有监控进程
        /// </summary>
        /// <param name="processName">进程名称</param>
        /// <returns>匹配的进程列表</returns>
        public IList<ProcessInfo> GetProcessesByName(string processName)
        {
            return _processTracker?.GetProcessesByName(processName) ?? new List<ProcessInfo>();
        }

        /// <summary>
        /// 异步刷新所有监控进程的状态
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>刷新结果</returns>
        public async Task<MonitoringResult> RefreshProcessStatesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (_disposed)
                    return MonitoringResult.Failure("进程监控器已被释放");

                if (_processTracker == null)
                    return MonitoringResult.Failure("进程跟踪器未初始化");

                return await _processTracker.RefreshProcessStatesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new MonitoringErrorEventArgs(ex, "刷新进程状态失败"));
                return MonitoringResult.Failure("刷新进程状态失败", ex);
            }
        }

        #endregion

        #region 高级功能 (为Stream B/C预留接口)

        /// <summary>
        /// 启用WMI事件监听（由Stream B实现）
        /// </summary>
        /// <param name="enabled">是否启用</param>
        /// <returns>设置结果</returns>
        public MonitoringResult SetWmiEventListenerEnabled(bool enabled)
        {
            // 预留接口，Stream B将实现WMI事件监听功能
            // 目前仅记录设置状态
            var message = enabled ? "WMI事件监听已启用（由Stream B实现）" : "WMI事件监听已禁用";
            return MonitoringResult.Success(message);
        }

        /// <summary>
        /// 设置进程监控配置
        /// </summary>
        /// <param name="config">监控配置</param>
        /// <returns>设置结果</returns>
        public MonitoringResult SetMonitoringConfig(ProcessMonitoringConfig config)
        {
            try
            {
                if (config == null)
                    return MonitoringResult.Failure("监控配置不能为空");

                // 更新配置（部分配置需要重启监控器才能生效）
                var requiresRestart =
                    config.PollingIntervalMs != _config.PollingIntervalMs ||
                    config.EnableWmiEventListening != _config.EnableWmiEventListening;

                // 复制配置值
                _config.PollingIntervalMs = config.PollingIntervalMs;
                _config.EnableWmiEventListening = config.EnableWmiEventListening;
                _config.MonitorChildProcesses = config.MonitorChildProcesses;
                _config.MaxProcessTreeDepth = config.MaxProcessTreeDepth;
                _config.EnablePerformanceMonitoring = config.EnablePerformanceMonitoring;
                _config.MonitoringTimeoutMs = config.MonitoringTimeoutMs;
                _config.MaxMonitoredProcesses = config.MaxMonitoredProcesses;
                _config.MaxEventHistory = config.MaxEventHistory;
                _config.AutoCleanupEventHours = config.AutoCleanupEventHours;

                var message = "监控配置已更新";
                if (requiresRestart && IsMonitoring)
                {
                    message += "（部分配置需要重启监控器才能生效）";
                }

                return MonitoringResult.Success(message);
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new MonitoringErrorEventArgs(ex, "设置监控配置失败"));
                return MonitoringResult.Failure("设置监控配置失败", ex);
            }
        }

        /// <summary>
        /// 获取监控统计信息
        /// </summary>
        /// <returns>监控统计</returns>
        public MonitoringStatistics GetStatistics()
        {
            lock (_statisticsLock)
            {
                // 更新统计信息
                _statistics.TotalRunTime = _startTime.HasValue ? DateTime.UtcNow - _startTime.Value : TimeSpan.Zero;
                _statistics.CurrentProcessCount = MonitoredProcessCount;

                // 返回统计信息的副本
                return new MonitoringStatistics
                {
                    StartTime = _statistics.StartTime,
                    TotalRunTime = _statistics.TotalRunTime,
                    CurrentProcessCount = _statistics.CurrentProcessCount,
                    TotalProcessesMonitored = _statistics.TotalProcessesMonitored,
                    ProcessesStarted = _statistics.ProcessesStarted,
                    ProcessesExited = _statistics.ProcessesExited,
                    ProcessesKilled = _statistics.ProcessesKilled,
                    ErrorCount = _statistics.ErrorCount,
                    WarningCount = _statistics.WarningCount,
                    EventCounts = new Dictionary<MonitoringEventType, int>(_statistics.EventCounts)
                };
            }
        }

        /// <summary>
        /// 异步执行健康检查
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>健康检查结果</returns>
        public async Task<MonitoringHealthResult> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var result = new MonitoringHealthResult();

                try
                {
                    // 基本状态检查
                    result.CheckResults["IsRunning"] = IsMonitoring;
                    result.CheckResults["TrackerActive"] = _processTracker?.IsTracking ?? false;
                    result.CheckResults["NoErrors"] = _state != MonitoringState.Error;

                    // 性能检查
                    var stats = GetStatistics();
                    result.PerformanceMetrics["ProcessCount"] = stats.CurrentProcessCount;
                    result.PerformanceMetrics["RunTimeHours"] = stats.TotalRunTime.TotalHours;
                    result.PerformanceMetrics["ErrorRate"] = stats.TotalProcessesMonitored > 0 ?
                        (double)stats.ErrorCount / stats.TotalProcessesMonitored : 0;

                    // 评估整体健康状态
                    result.IsHealthy = result.CheckResults.Values.All(v => v) &&
                                      result.PerformanceMetrics["ErrorRate"] < 0.1; // 错误率低于10%

                    result.Status = result.IsHealthy ? "健康" : "异常";

                    // 添加建议
                    if (!result.IsHealthy)
                    {
                        if (_state == MonitoringState.Error)
                        {
                            result.Issues.Add("监控器处于错误状态");
                            result.Recommendations.Add("重启监控器");
                        }

                        if (!IsMonitoring)
                        {
                            result.Issues.Add("监控器未运行");
                            result.Recommendations.Add("启动监控器");
                        }

                        if (result.PerformanceMetrics["ErrorRate"] >= 0.1)
                        {
                            result.Issues.Add("错误率过高");
                            result.Recommendations.Add("检查系统资源和权限");
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.IsHealthy = false;
                    result.Status = "健康检查失败";
                    result.Issues.Add($"健康检查异常: {ex.Message}");
                    OnErrorOccurred(new MonitoringErrorEventArgs(ex, "健康检查失败"));
                }

                return result;
            }, cancellationToken);
        }

        #endregion

        #region 私有辅助方法

        /// <summary>
        /// 检查进程名称是否匹配模式
        /// </summary>
        /// <param name="processName">进程名称</param>
        /// <param name="pattern">匹配模式</param>
        /// <returns>是否匹配</returns>
        private bool IsProcessNameMatch(string processName, string pattern)
        {
            if (string.IsNullOrWhiteSpace(processName) || string.IsNullOrWhiteSpace(pattern))
                return false;

            // 简单的通配符匹配（支持*通配符）
            if (pattern.Contains("*"))
            {
                var regexPattern = "^" + pattern.Replace("*", ".*") + "$";
                return System.Text.RegularExpressions.Regex.IsMatch(processName, regexPattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            return string.Equals(processName, pattern, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region 事件处理方法

        /// <summary>
        /// 处理进程添加事件
        /// </summary>
        private void OnProcessAdded(object sender, ProcessTrackedEventArgs e)
        {
            lock (_statisticsLock)
            {
                _statistics.ProcessesStarted++;
                _statistics.EventCounts[MonitoringEventType.ProcessStarted] =
                    _statistics.EventCounts.GetValueOrDefault(MonitoringEventType.ProcessStarted) + 1;
            }

            ProcessStarted?.Invoke(this, new ProcessMonitoringEventArgs(e.ProcessInfo, MonitoringEventType.ProcessStarted, "进程已添加到监控"));
        }

        /// <summary>
        /// 处理进程移除事件
        /// </summary>
        private void OnProcessRemoved(object sender, ProcessTrackedEventArgs e)
        {
            lock (_statisticsLock)
            {
                if (e.ProcessInfo.IsAbnormalExit)
                {
                    _statistics.ProcessesKilled++;
                    _statistics.EventCounts[MonitoringEventType.ProcessKilled] =
                        _statistics.EventCounts.GetValueOrDefault(MonitoringEventType.ProcessKilled) + 1;

                    ProcessKilled?.Invoke(this, new ProcessMonitoringEventArgs(e.ProcessInfo, MonitoringEventType.ProcessKilled, e.ProcessInfo.ExitReason));
                }
                else
                {
                    _statistics.ProcessesExited++;
                    _statistics.EventCounts[MonitoringEventType.ProcessExited] =
                        _statistics.EventCounts.GetValueOrDefault(MonitoringEventType.ProcessExited) + 1;

                    ProcessExited?.Invoke(this, new ProcessMonitoringEventArgs(e.ProcessInfo, MonitoringEventType.ProcessExited, "进程正常退出"));
                }
            }
        }

        /// <summary>
        /// 处理进程状态变化事件
        /// </summary>
        private void OnProcessStateChanged(object sender, ProcessStateChangedEventArgs e)
        {
            // 可以在此处添加状态变化的额外处理逻辑
            // 例如：性能监控、异常检测等
        }

        /// <summary>
        /// 处理跟踪错误事件
        /// </summary>
        private void OnTrackingError(object sender, TrackingErrorEventArgs e)
        {
            lock (_statisticsLock)
            {
                _statistics.ErrorCount++;
                _statistics.EventCounts[MonitoringEventType.Error] =
                    _statistics.EventCounts.GetValueOrDefault(MonitoringEventType.Error) + 1;
            }

            OnErrorOccurred(new MonitoringErrorEventArgs(e.Exception, e.Context));
        }

        /// <summary>
        /// 触发状态变化事件
        /// </summary>
        private void OnStateChanged(MonitoringStateChangedEventArgs e)
        {
            StateChanged?.Invoke(this, e);
        }

        /// <summary>
        /// 触发错误事件
        /// </summary>
        private void OnErrorOccurred(MonitoringErrorEventArgs e)
        {
            ErrorOccurred?.Invoke(this, e);
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
                    // 停止监控
                    if (_state == MonitoringState.Running)
                    {
                        StopMonitoringAsync().Wait(TimeSpan.FromSeconds(5));
                    }

                    // 释放进程跟踪器
                    _processTracker?.Dispose();
                }
                catch { /* 忽略清理异常 */ }
            }

            _disposed = true;
        }

        /// <summary>
        /// 析构函数
        /// </summary>
        ~ProcessMonitor()
        {
            Dispose(false);
        }

        #endregion
    }
}