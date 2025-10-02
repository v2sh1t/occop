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
    /// 监控管理器
    /// Stream C的核心组件，统一管理所有监控功能，提供监控系统的统一入口
    /// 整合进程监控器、WMI事件监听器和定时轮询机制，确保24/7稳定运行
    /// </summary>
    public class MonitoringManager : IDisposable
    {
        #region 字段和属性

        private readonly IProcessMonitor _processMonitor;
        private readonly IWmiEventListener _wmiEventListener;
        private readonly MonitoringPersistence _persistence;
        private readonly MonitoringConfiguration _configuration;
        private readonly MonitoringStatistics _statistics;

        private readonly Timer _healthCheckTimer;
        private readonly Timer _statisticsUpdateTimer;
        private readonly Timer _pollingFallbackTimer;
        private readonly Timer _cleanupTimer;

        private readonly ConcurrentQueue<MonitoringEvent> _eventQueue;
        private readonly SemaphoreSlim _operationSemaphore;
        private readonly CancellationTokenSource _cancellationTokenSource;

        private volatile bool _disposed;
        private volatile bool _isRunning;
        private volatile bool _isInitialized;

        /// <summary>
        /// 获取当前监控状态
        /// </summary>
        public MonitoringState State { get; private set; } = MonitoringState.Stopped;

        /// <summary>
        /// 获取监控管理器启动时间
        /// </summary>
        public DateTime? StartTime { get; private set; }

        /// <summary>
        /// 获取是否正在运行
        /// </summary>
        public bool IsRunning => _isRunning && !_disposed;

        /// <summary>
        /// 获取是否已初始化
        /// </summary>
        public bool IsInitialized => _isInitialized && !_disposed;

        /// <summary>
        /// 获取监控配置
        /// </summary>
        public MonitoringConfiguration Configuration => _configuration;

        /// <summary>
        /// 获取监控统计信息
        /// </summary>
        public MonitoringStatistics Statistics => _statistics;

        /// <summary>
        /// 获取当前监控的进程数量
        /// </summary>
        public int MonitoredProcessCount => _processMonitor?.MonitoredProcessCount ?? 0;

        /// <summary>
        /// 获取所有被监控的进程信息
        /// </summary>
        public IReadOnlyList<ProcessInfo> MonitoredProcesses => _processMonitor?.MonitoredProcesses ?? new List<ProcessInfo>();

        /// <summary>
        /// 获取待处理的事件数量
        /// </summary>
        public int PendingEventCount => _eventQueue.Count;

        #endregion

        #region 事件定义

        /// <summary>
        /// 监控状态变化事件
        /// </summary>
        public event EventHandler<MonitoringStateChangedEventArgs> StateChanged;

        /// <summary>
        /// 进程监控事件
        /// </summary>
        public event EventHandler<ProcessMonitoringEventArgs> ProcessEvent;

        /// <summary>
        /// 健康检查完成事件
        /// </summary>
        public event EventHandler<HealthCheckCompletedEventArgs> HealthCheckCompleted;

        /// <summary>
        /// 监控错误事件
        /// </summary>
        public event EventHandler<MonitoringErrorEventArgs> MonitoringError;

        /// <summary>
        /// 性能警报事件
        /// </summary>
        public event EventHandler<PerformanceAlertEventArgs> PerformanceAlert;

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="processMonitor">进程监控器</param>
        /// <param name="wmiEventListener">WMI事件监听器</param>
        /// <param name="configuration">监控配置</param>
        public MonitoringManager(
            IProcessMonitor processMonitor,
            IWmiEventListener wmiEventListener = null,
            MonitoringConfiguration configuration = null)
        {
            _processMonitor = processMonitor ?? throw new ArgumentNullException(nameof(processMonitor));
            _wmiEventListener = wmiEventListener;
            _configuration = configuration ?? new MonitoringConfiguration();
            _statistics = new MonitoringStatistics();
            _persistence = new MonitoringPersistence(_configuration);

            _eventQueue = new ConcurrentQueue<MonitoringEvent>();
            _operationSemaphore = new SemaphoreSlim(1, 1);
            _cancellationTokenSource = new CancellationTokenSource();

            // 初始化定时器
            InitializeTimers();

            // 订阅事件
            SubscribeToEvents();

            _isInitialized = true;
        }

        #endregion

        #region 初始化方法

        /// <summary>
        /// 初始化定时器
        /// </summary>
        private void InitializeTimers()
        {
            // 健康检查定时器
            if (_configuration.EnableHealthCheck && _configuration.HealthCheckIntervalMinutes > 0)
            {
                var healthCheckInterval = TimeSpan.FromMinutes(_configuration.HealthCheckIntervalMinutes);
                _healthCheckTimer = new Timer(HealthCheckCallback, null, Timeout.InfiniteTimeSpan, healthCheckInterval);
            }

            // 统计信息更新定时器
            var statisticsInterval = TimeSpan.FromMinutes(1); // 每分钟更新一次
            _statisticsUpdateTimer = new Timer(StatisticsUpdateCallback, null, Timeout.InfiniteTimeSpan, statisticsInterval);

            // 轮询兜底机制定时器
            if (_configuration.EnablePollingFallback && _configuration.PollingIntervalMs > 0)
            {
                var pollingInterval = TimeSpan.FromMilliseconds(_configuration.PollingIntervalMs);
                _pollingFallbackTimer = new Timer(PollingFallbackCallback, null, Timeout.InfiniteTimeSpan, pollingInterval);
            }

            // 清理定时器（每小时执行一次清理任务）
            var cleanupInterval = TimeSpan.FromHours(1);
            _cleanupTimer = new Timer(CleanupCallback, null, Timeout.InfiniteTimeSpan, cleanupInterval);
        }

        /// <summary>
        /// 订阅事件
        /// </summary>
        private void SubscribeToEvents()
        {
            // 订阅进程监控器事件
            if (_processMonitor != null)
            {
                _processMonitor.ProcessStarted += OnProcessStarted;
                _processMonitor.ProcessExited += OnProcessExited;
                _processMonitor.ProcessKilled += OnProcessKilled;
                _processMonitor.StateChanged += OnProcessMonitorStateChanged;
                _processMonitor.ErrorOccurred += OnProcessMonitorError;
            }

            // 订阅WMI事件监听器事件
            if (_wmiEventListener != null)
            {
                _wmiEventListener.ProcessCreated += OnWmiProcessCreated;
                _wmiEventListener.ProcessDeleted += OnWmiProcessDeleted;
                _wmiEventListener.EventError += OnWmiEventError;
            }

            // 订阅持久化事件
            if (_persistence != null)
            {
                _persistence.SaveCompleted += OnPersistenceSaveCompleted;
                _persistence.LoadCompleted += OnPersistenceLoadCompleted;
                _persistence.PersistenceError += OnPersistenceError;
            }
        }

        #endregion

        #region 启动和停止

        /// <summary>
        /// 异步启动监控管理器
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>启动结果</returns>
        public async Task<MonitoringResult> StartAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                return MonitoringResult.Failure("监控管理器已被释放");

            if (_isRunning)
                return MonitoringResult.Success("监控管理器已在运行中");

            await _operationSemaphore.WaitAsync(cancellationToken);
            try
            {
                ChangeState(MonitoringState.Starting);

                // 启动延迟
                if (_configuration.StartupDelaySeconds > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(_configuration.StartupDelaySeconds), cancellationToken);
                }

                // 恢复监控状态和数据
                await RestoreStateAsync(cancellationToken);

                // 启动进程监控器
                var processMonitorResult = await _processMonitor.StartMonitoringAsync(cancellationToken);
                if (!processMonitorResult.Success)
                {
                    ChangeState(MonitoringState.Error);
                    return MonitoringResult.Failure($"启动进程监控器失败: {processMonitorResult.ErrorMessage}", processMonitorResult.Exception);
                }

                // 启动WMI事件监听器
                if (_wmiEventListener != null && _configuration.EnableWmiEventListening)
                {
                    var wmiResult = await _wmiEventListener.StartListeningAsync(cancellationToken);
                    if (!wmiResult.Success)
                    {
                        // WMI失败不阻止启动，但记录警告
                        RecordEvent(MonitoringEvent.CreateError(
                            new Exception(wmiResult.ErrorMessage),
                            "WMI事件监听器启动失败，将使用轮询模式"));
                    }
                }

                // 启动定时器
                StartTimers();

                // 启动事件处理
                _ = Task.Run(ProcessEventQueueAsync, cancellationToken);

                _isRunning = true;
                StartTime = DateTime.UtcNow;
                ChangeState(MonitoringState.Running);

                // 记录启动事件
                _statistics.RecordProcessStarted(new ProcessInfo(Process.GetCurrentProcess().Id, "MonitoringManager"));

                return MonitoringResult.Success("监控管理器启动成功");
            }
            catch (Exception ex)
            {
                ChangeState(MonitoringState.Error);
                var errorResult = MonitoringResult.Failure($"启动监控管理器时发生异常: {ex.Message}", ex);
                OnMonitoringError(new MonitoringErrorEventArgs(ex, "启动监控管理器异常"));
                return errorResult;
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }

        /// <summary>
        /// 异步停止监控管理器
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>停止结果</returns>
        public async Task<MonitoringResult> StopAsync(CancellationToken cancellationToken = default)
        {
            if (!_isRunning)
                return MonitoringResult.Success("监控管理器已停止");

            await _operationSemaphore.WaitAsync(cancellationToken);
            try
            {
                ChangeState(MonitoringState.Stopping);

                // 停止定时器
                StopTimers();

                // 保存当前状态
                await SaveStateAsync(cancellationToken);

                // 停止WMI事件监听器
                if (_wmiEventListener != null)
                {
                    try
                    {
                        await _wmiEventListener.StopListeningAsync(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        OnMonitoringError(new MonitoringErrorEventArgs(ex, "停止WMI事件监听器失败"));
                    }
                }

                // 停止进程监控器
                var processMonitorResult = await _processMonitor.StopMonitoringAsync(cancellationToken);
                if (!processMonitorResult.Success)
                {
                    OnMonitoringError(new MonitoringErrorEventArgs(
                        processMonitorResult.Exception,
                        "停止进程监控器失败"));
                }

                // 处理剩余事件
                await ProcessRemainingEventsAsync(cancellationToken);

                // 取消后台任务
                _cancellationTokenSource.Cancel();

                _isRunning = false;
                ChangeState(MonitoringState.Stopped);

                return MonitoringResult.Success("监控管理器停止成功");
            }
            catch (Exception ex)
            {
                ChangeState(MonitoringState.Error);
                var errorResult = MonitoringResult.Failure($"停止监控管理器时发生异常: {ex.Message}", ex);
                OnMonitoringError(new MonitoringErrorEventArgs(ex, "停止监控管理器异常"));
                return errorResult;
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }

        /// <summary>
        /// 启动定时器
        /// </summary>
        private void StartTimers()
        {
            var now = DateTime.UtcNow;

            _healthCheckTimer?.Change(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(_configuration.HealthCheckIntervalMinutes));
            _statisticsUpdateTimer?.Change(TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1));
            _pollingFallbackTimer?.Change(TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(_configuration.PollingIntervalMs));
            _cleanupTimer?.Change(TimeSpan.FromMinutes(5), TimeSpan.FromHours(1));
        }

        /// <summary>
        /// 停止定时器
        /// </summary>
        private void StopTimers()
        {
            _healthCheckTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _statisticsUpdateTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _pollingFallbackTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _cleanupTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        #endregion

        #region 进程管理

        /// <summary>
        /// 添加要监控的进程
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="processName">进程名称</param>
        /// <returns>添加结果</returns>
        public MonitoringResult AddProcess(int processId, string processName = null)
        {
            if (!_isRunning)
                return MonitoringResult.Failure("监控管理器未运行");

            try
            {
                var result = _processMonitor.AddProcess(processId, processName);

                if (result.Success)
                {
                    _statistics.CurrentProcessCount = _processMonitor.MonitoredProcessCount;
                    RecordEvent(MonitoringEvent.CreateProcessStarted(new ProcessInfo(processId, processName)));
                }

                return result;
            }
            catch (Exception ex)
            {
                var errorResult = MonitoringResult.Failure($"添加进程监控失败: {ex.Message}", ex);
                OnMonitoringError(new MonitoringErrorEventArgs(ex, $"添加进程监控失败: {processId}"));
                return errorResult;
            }
        }

        /// <summary>
        /// 通过进程名称模式添加监控
        /// </summary>
        /// <param name="processNamePattern">进程名称模式</param>
        /// <returns>添加结果</returns>
        public MonitoringResult AddProcessByName(string processNamePattern)
        {
            if (!_isRunning)
                return MonitoringResult.Failure("监控管理器未运行");

            try
            {
                var result = _processMonitor.AddProcessByName(processNamePattern);

                if (result.Success)
                {
                    _statistics.CurrentProcessCount = _processMonitor.MonitoredProcessCount;
                }

                return result;
            }
            catch (Exception ex)
            {
                var errorResult = MonitoringResult.Failure($"按名称添加进程监控失败: {ex.Message}", ex);
                OnMonitoringError(new MonitoringErrorEventArgs(ex, $"按名称添加进程监控失败: {processNamePattern}"));
                return errorResult;
            }
        }

        /// <summary>
        /// 移除被监控的进程
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <returns>移除结果</returns>
        public MonitoringResult RemoveProcess(int processId)
        {
            if (!_isRunning)
                return MonitoringResult.Failure("监控管理器未运行");

            try
            {
                var result = _processMonitor.RemoveProcess(processId);

                if (result.Success)
                {
                    _statistics.CurrentProcessCount = _processMonitor.MonitoredProcessCount;
                }

                return result;
            }
            catch (Exception ex)
            {
                var errorResult = MonitoringResult.Failure($"移除进程监控失败: {ex.Message}", ex);
                OnMonitoringError(new MonitoringErrorEventArgs(ex, $"移除进程监控失败: {processId}"));
                return errorResult;
            }
        }

        /// <summary>
        /// 清除所有被监控的进程
        /// </summary>
        /// <returns>清除结果</returns>
        public MonitoringResult ClearAllProcesses()
        {
            if (!_isRunning)
                return MonitoringResult.Failure("监控管理器未运行");

            try
            {
                var result = _processMonitor.ClearAllProcesses();

                if (result.Success)
                {
                    _statistics.CurrentProcessCount = 0;
                }

                return result;
            }
            catch (Exception ex)
            {
                var errorResult = MonitoringResult.Failure($"清除所有进程监控失败: {ex.Message}", ex);
                OnMonitoringError(new MonitoringErrorEventArgs(ex, "清除所有进程监控失败"));
                return errorResult;
            }
        }

        #endregion

        #region 健康检查

        /// <summary>
        /// 异步执行健康检查
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>健康检查结果</returns>
        public async Task<MonitoringHealthResult> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            var healthResult = new MonitoringHealthResult();

            try
            {
                // 检查监控管理器状态
                healthResult.CheckResults["ManagerRunning"] = _isRunning;
                healthResult.CheckResults["ManagerInitialized"] = _isInitialized;

                // 检查进程监控器
                healthResult.CheckResults["ProcessMonitorRunning"] = _processMonitor?.IsMonitoring ?? false;

                // 检查WMI事件监听器
                if (_wmiEventListener != null)
                {
                    healthResult.CheckResults["WmiEventListenerRunning"] = _wmiEventListener.IsListening;
                }

                // 检查内存使用
                var currentProcess = Process.GetCurrentProcess();
                var memoryUsageMB = currentProcess.WorkingSet64 / 1024.0 / 1024.0;
                healthResult.PerformanceMetrics["MemoryUsageMB"] = memoryUsageMB;
                healthResult.CheckResults["MemoryUsageOK"] = memoryUsageMB < _configuration.MonitoringSystemMemoryLimitMB;

                // 检查事件队列
                var pendingEvents = _eventQueue.Count;
                healthResult.PerformanceMetrics["PendingEvents"] = pendingEvents;
                healthResult.CheckResults["EventQueueOK"] = pendingEvents < 1000; // 假设阈值

                // 检查进程数量
                var processCount = _processMonitor?.MonitoredProcessCount ?? 0;
                healthResult.PerformanceMetrics["MonitoredProcesses"] = processCount;
                healthResult.CheckResults["ProcessCountOK"] = processCount <= _configuration.ProcessMonitoring.MaxMonitoredProcesses;

                // 检查持久化
                if (_configuration.EnableStatePersistence)
                {
                    var persistenceStats = _persistence.GetStatistics();
                    healthResult.CheckResults["PersistenceEnabled"] = persistenceStats.IsEnabled;
                    healthResult.PerformanceMetrics["PersistenceSuccessRate"] = persistenceStats.SaveSuccessRate;
                }

                // 运行进程监控器健康检查
                if (_processMonitor != null)
                {
                    var processMonitorHealth = await _processMonitor.CheckHealthAsync(cancellationToken);
                    foreach (var checkResult in processMonitorHealth.CheckResults)
                    {
                        healthResult.CheckResults[$"ProcessMonitor_{checkResult.Key}"] = checkResult.Value;
                    }
                    foreach (var metric in processMonitorHealth.PerformanceMetrics)
                    {
                        healthResult.PerformanceMetrics[$"ProcessMonitor_{metric.Key}"] = metric.Value;
                    }
                }

                // 检查WMI可用性
                if (_wmiEventListener != null)
                {
                    var wmiAvailability = await _wmiEventListener.CheckWmiAvailabilityAsync();
                    healthResult.CheckResults["WmiAvailable"] = wmiAvailability.Success;
                }

                // 计算总体健康状态
                var failedChecks = healthResult.CheckResults.Values.Count(v => !v);
                healthResult.IsHealthy = failedChecks == 0;

                if (healthResult.IsHealthy)
                {
                    healthResult.Status = "监控系统运行正常";
                }
                else
                {
                    healthResult.Status = $"监控系统存在 {failedChecks} 个问题";

                    foreach (var failedCheck in healthResult.CheckResults.Where(kvp => !kvp.Value))
                    {
                        healthResult.Issues.Add($"检查项失败: {failedCheck.Key}");
                    }
                }

                // 生成建议
                GenerateHealthRecommendations(healthResult);

                // 记录健康检查统计
                _statistics.RecordHealthCheck(healthResult.IsHealthy);

                return healthResult;
            }
            catch (Exception ex)
            {
                healthResult.IsHealthy = false;
                healthResult.Status = $"健康检查执行失败: {ex.Message}";
                healthResult.Issues.Add($"健康检查异常: {ex.Message}");

                _statistics.RecordHealthCheck(false);
                OnMonitoringError(new MonitoringErrorEventArgs(ex, "健康检查执行失败"));

                return healthResult;
            }
        }

        /// <summary>
        /// 生成健康状态建议
        /// </summary>
        /// <param name="healthResult">健康检查结果</param>
        private void GenerateHealthRecommendations(MonitoringHealthResult healthResult)
        {
            // 内存使用建议
            if (healthResult.PerformanceMetrics.TryGetValue("MemoryUsageMB", out var memoryUsage) &&
                memoryUsage > _configuration.MonitoringSystemMemoryLimitMB * 0.8)
            {
                healthResult.Recommendations.Add("监控系统内存使用量较高，建议增加内存限制或优化监控配置");
            }

            // 事件队列建议
            if (healthResult.PerformanceMetrics.TryGetValue("PendingEvents", out var pendingEvents) &&
                pendingEvents > 500)
            {
                healthResult.Recommendations.Add("事件队列积压较多，建议检查事件处理性能");
            }

            // 进程数量建议
            if (healthResult.PerformanceMetrics.TryGetValue("MonitoredProcesses", out var processCount) &&
                processCount > _configuration.ProcessMonitoring.MaxMonitoredProcesses * 0.9)
            {
                healthResult.Recommendations.Add("监控进程数量接近上限，建议清理不必要的监控进程或增加限制");
            }

            // WMI建议
            if (healthResult.CheckResults.TryGetValue("WmiAvailable", out var wmiAvailable) && !wmiAvailable)
            {
                healthResult.Recommendations.Add("WMI服务不可用，监控将依赖轮询机制，可能影响实时性");
            }

            // 持久化建议
            if (healthResult.PerformanceMetrics.TryGetValue("PersistenceSuccessRate", out var successRate) &&
                successRate < 95)
            {
                healthResult.Recommendations.Add("数据持久化成功率较低，建议检查存储空间和权限");
            }
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 记录监控事件
        /// </summary>
        /// <param name="monitoringEvent">监控事件</param>
        public void RecordEvent(MonitoringEvent monitoringEvent)
        {
            if (monitoringEvent == null)
                return;

            _eventQueue.Enqueue(monitoringEvent);
            _statistics.IncrementEventCount(monitoringEvent.EventType);

            // 检查是否为性能警报
            if (monitoringEvent.EventType == MonitoringEventType.PerformanceAlert)
            {
                OnPerformanceAlert(new PerformanceAlertEventArgs(monitoringEvent));
            }
        }

        /// <summary>
        /// 异步处理事件队列
        /// </summary>
        /// <returns>处理任务</returns>
        private async Task ProcessEventQueueAsync()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    var eventsToProcess = new List<MonitoringEvent>();

                    // 批量取出事件
                    for (int i = 0; i < 100 && _eventQueue.TryDequeue(out var evt); i++)
                    {
                        eventsToProcess.Add(evt);
                    }

                    if (eventsToProcess.Count > 0)
                    {
                        // 保存事件历史
                        if (_configuration.EnableEventHistoryPersistence)
                        {
                            await _persistence.SaveEventHistoryAsync(eventsToProcess, _cancellationTokenSource.Token);
                        }

                        // 处理性能监控事件
                        ProcessPerformanceEvents(eventsToProcess);
                    }

                    // 等待一段时间再处理下一批
                    await Task.Delay(1000, _cancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    OnMonitoringError(new MonitoringErrorEventArgs(ex, "处理事件队列失败"));
                    await Task.Delay(5000, _cancellationTokenSource.Token);
                }
            }
        }

        /// <summary>
        /// 处理剩余事件
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>处理任务</returns>
        private async Task ProcessRemainingEventsAsync(CancellationToken cancellationToken)
        {
            var remainingEvents = new List<MonitoringEvent>();

            while (_eventQueue.TryDequeue(out var evt))
            {
                remainingEvents.Add(evt);
            }

            if (remainingEvents.Count > 0 && _configuration.EnableEventHistoryPersistence)
            {
                await _persistence.SaveEventHistoryAsync(remainingEvents, cancellationToken);
            }
        }

        /// <summary>
        /// 处理性能事件
        /// </summary>
        /// <param name="events">事件列表</param>
        private void ProcessPerformanceEvents(List<MonitoringEvent> events)
        {
            foreach (var evt in events.Where(e => e.MemoryUsage.HasValue || e.CpuUsage.HasValue))
            {
                var memoryMB = evt.MemoryUsage.HasValue ? evt.MemoryUsage.Value / 1024.0 / 1024.0 : 0;
                var cpuUsage = evt.CpuUsage ?? 0;
                var handleCount = evt.HandleCount ?? 0;

                // 检查性能阈值
                CheckPerformanceThresholds(evt.ProcessId, memoryMB, cpuUsage, handleCount);
            }
        }

        /// <summary>
        /// 检查性能阈值
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="memoryMB">内存使用量（MB）</param>
        /// <param name="cpuUsage">CPU使用率</param>
        /// <param name="handleCount">句柄数量</param>
        private void CheckPerformanceThresholds(int? processId, double memoryMB, double cpuUsage, int handleCount)
        {
            var alerts = new List<string>();

            // 检查内存阈值
            if (memoryMB > _configuration.MemoryWarningThresholdMB)
            {
                var severity = memoryMB > _configuration.MemoryCriticalThresholdMB ? "严重" : "警告";
                alerts.Add($"内存使用量{severity}: {memoryMB:F1}MB");
            }

            // 检查CPU阈值
            if (cpuUsage > _configuration.CpuWarningThreshold)
            {
                var severity = cpuUsage > _configuration.CpuCriticalThreshold ? "严重" : "警告";
                alerts.Add($"CPU使用率{severity}: {cpuUsage:F1}%");
            }

            // 检查句柄阈值
            if (handleCount > _configuration.HandleWarningThreshold)
            {
                alerts.Add($"句柄数量警告: {handleCount}");
            }

            // 生成性能警报事件
            if (alerts.Count > 0)
            {
                var alertEvent = new MonitoringEvent(MonitoringEventType.PerformanceAlert,
                    $"性能警报: 进程{processId}",
                    string.Join(", ", alerts))
                {
                    ProcessId = processId,
                    MemoryUsage = (long)(memoryMB * 1024 * 1024),
                    CpuUsage = cpuUsage,
                    HandleCount = handleCount,
                    Severity = memoryMB > _configuration.MemoryCriticalThresholdMB ||
                              cpuUsage > _configuration.CpuCriticalThreshold ?
                              EventSeverity.Critical : EventSeverity.Warning
                };

                RecordEvent(alertEvent);
            }
        }

        #endregion

        #region 状态管理

        /// <summary>
        /// 更改监控状态
        /// </summary>
        /// <param name="newState">新状态</param>
        private void ChangeState(MonitoringState newState)
        {
            var previousState = State;
            State = newState;

            if (previousState != newState)
            {
                var stateEvent = MonitoringEvent.CreateStateChanged(previousState, newState);
                RecordEvent(stateEvent);
                OnStateChanged(new MonitoringStateChangedEventArgs(previousState, newState));
            }
        }

        /// <summary>
        /// 异步保存状态
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>保存任务</returns>
        private async Task SaveStateAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // 保存监控状态
                await _persistence.SaveStateAsync(State, cancellationToken);

                // 保存统计信息
                await _persistence.SaveStatisticsAsync(_statistics, cancellationToken);

                // 保存进程信息
                if (_processMonitor?.MonitoredProcesses != null)
                {
                    await _persistence.SaveProcessInfoAsync(_processMonitor.MonitoredProcesses, cancellationToken);
                }

                _statistics.RecordStateSave(true);
            }
            catch (Exception ex)
            {
                _statistics.RecordStateSave(false);
                OnMonitoringError(new MonitoringErrorEventArgs(ex, "保存状态失败"));
            }
        }

        /// <summary>
        /// 异步恢复状态
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>恢复任务</returns>
        private async Task RestoreStateAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // 恢复监控状态
                var (stateResult, state) = await _persistence.LoadStateAsync(cancellationToken);
                if (stateResult.Success && state.HasValue)
                {
                    // 注意：这里不直接设置状态，因为我们正在启动过程中
                    // State = state.Value;
                }

                // 恢复统计信息
                var (statsResult, statistics) = await _persistence.LoadLatestStatisticsAsync(cancellationToken);
                if (statsResult.Success && statistics != null)
                {
                    // 选择性恢复统计信息（保留启动时间等）
                    _statistics.TotalProcessesMonitored = statistics.TotalProcessesMonitored;
                    _statistics.ProcessesStarted = statistics.ProcessesStarted;
                    _statistics.ProcessesExited = statistics.ProcessesExited;
                    _statistics.ProcessesKilled = statistics.ProcessesKilled;
                    // 其他需要恢复的字段...
                }

                // 恢复进程信息
                var (processResult, processes) = await _persistence.LoadProcessInfoAsync(cancellationToken);
                if (processResult.Success && processes != null)
                {
                    // 验证进程是否仍然存在，并重新添加到监控
                    foreach (var processInfo in processes)
                    {
                        try
                        {
                            var process = Process.GetProcessById(processInfo.ProcessId);
                            if (process != null && !process.HasExited)
                            {
                                _processMonitor.AddProcess(processInfo.ProcessId, processInfo.ProcessName);
                            }
                        }
                        catch
                        {
                            // 进程不存在，跳过
                        }
                    }
                }

                _statistics.RecordStateRestore(true);
            }
            catch (Exception ex)
            {
                _statistics.RecordStateRestore(false);
                OnMonitoringError(new MonitoringErrorEventArgs(ex, "恢复状态失败"));
            }
        }

        #endregion

        #region 定时器回调

        /// <summary>
        /// 健康检查回调
        /// </summary>
        /// <param name="state">状态对象</param>
        private async void HealthCheckCallback(object state)
        {
            if (!_isRunning || _disposed)
                return;

            try
            {
                var healthResult = await CheckHealthAsync(_cancellationTokenSource.Token);
                OnHealthCheckCompleted(new HealthCheckCompletedEventArgs(healthResult));
            }
            catch (Exception ex)
            {
                OnMonitoringError(new MonitoringErrorEventArgs(ex, "定时健康检查失败"));
            }
        }

        /// <summary>
        /// 统计信息更新回调
        /// </summary>
        /// <param name="state">状态对象</param>
        private void StatisticsUpdateCallback(object state)
        {
            if (!_isRunning || _disposed)
                return;

            try
            {
                // 更新监控系统性能
                var currentProcess = Process.GetCurrentProcess();
                var memoryMB = currentProcess.WorkingSet64 / 1024.0 / 1024.0;
                _statistics.UpdateMonitoringSystemPerformance(memoryMB, 0); // CPU使用率需要单独计算

                // 更新当前进程数量
                _statistics.CurrentProcessCount = _processMonitor?.MonitoredProcessCount ?? 0;
                _statistics.ActiveProcessCount = _statistics.CurrentProcessCount;

                // 添加分钟级数据
                _statistics.AddMinutelyData();

                // 每小时添加小时级数据
                if (DateTime.UtcNow.Minute == 0)
                {
                    _statistics.AddHourlyData();
                }
            }
            catch (Exception ex)
            {
                OnMonitoringError(new MonitoringErrorEventArgs(ex, "更新统计信息失败"));
            }
        }

        /// <summary>
        /// 轮询兜底回调
        /// </summary>
        /// <param name="state">状态对象</param>
        private async void PollingFallbackCallback(object state)
        {
            if (!_isRunning || _disposed)
                return;

            try
            {
                // 检查WMI事件监听器是否正常工作
                var wmiWorking = _wmiEventListener?.IsListening ?? false;

                if (!wmiWorking)
                {
                    // WMI不工作时，执行轮询检查
                    await _processMonitor.RefreshProcessStatesAsync(_cancellationTokenSource.Token);
                    _statistics.PollingStats.PollingCount++;
                }
            }
            catch (Exception ex)
            {
                _statistics.PollingStats.PollingErrors++;
                OnMonitoringError(new MonitoringErrorEventArgs(ex, "轮询兜底检查失败"));
            }
        }

        /// <summary>
        /// 清理回调
        /// </summary>
        /// <param name="state">状态对象</param>
        private async void CleanupCallback(object state)
        {
            if (!_isRunning || _disposed)
                return;

            try
            {
                // 自动保存状态
                await SaveStateAsync(_cancellationTokenSource.Token);

                // 清理内存中的过期数据
                // 这里可以添加其他清理逻辑
            }
            catch (Exception ex)
            {
                OnMonitoringError(new MonitoringErrorEventArgs(ex, "定时清理失败"));
            }
        }

        #endregion

        #region 事件处理器

        /// <summary>
        /// 处理进程启动事件
        /// </summary>
        private void OnProcessStarted(object sender, ProcessMonitoringEventArgs e)
        {
            _statistics.RecordProcessStarted(e.ProcessInfo);
            RecordEvent(MonitoringEvent.CreateProcessStarted(e.ProcessInfo));
            OnProcessEvent(e);
        }

        /// <summary>
        /// 处理进程退出事件
        /// </summary>
        private void OnProcessExited(object sender, ProcessMonitoringEventArgs e)
        {
            _statistics.RecordProcessExited(e.ProcessInfo, false);
            RecordEvent(MonitoringEvent.CreateProcessExited(e.ProcessInfo));
            OnProcessEvent(e);
        }

        /// <summary>
        /// 处理进程被终止事件
        /// </summary>
        private void OnProcessKilled(object sender, ProcessMonitoringEventArgs e)
        {
            _statistics.RecordProcessExited(e.ProcessInfo, true);
            RecordEvent(MonitoringEvent.CreateProcessKilled(e.ProcessInfo));
            OnProcessEvent(e);
        }

        /// <summary>
        /// 处理进程监控器状态变化事件
        /// </summary>
        private void OnProcessMonitorStateChanged(object sender, MonitoringStateChangedEventArgs e)
        {
            RecordEvent(MonitoringEvent.CreateStateChanged(e.PreviousState, e.CurrentState, e.Reason));
        }

        /// <summary>
        /// 处理进程监控器错误事件
        /// </summary>
        private void OnProcessMonitorError(object sender, MonitoringErrorEventArgs e)
        {
            _statistics.RecordProcessError();
            RecordEvent(MonitoringEvent.CreateError(e.Exception, e.Message, e.ProcessId));
            OnMonitoringError(e);
        }

        /// <summary>
        /// 处理WMI进程创建事件
        /// </summary>
        private void OnWmiProcessCreated(object sender, WmiProcessEventArgs e)
        {
            _statistics.WmiStatistics.ProcessCreationEvents++;

            // 如果配置了自动添加AI工具进程，则尝试添加
            if (IsAIToolProcess(e.ProcessName))
            {
                AddProcess(e.ProcessId, e.ProcessName);
            }
        }

        /// <summary>
        /// 处理WMI进程删除事件
        /// </summary>
        private void OnWmiProcessDeleted(object sender, WmiProcessEventArgs e)
        {
            _statistics.WmiStatistics.ProcessDeletionEvents++;
        }

        /// <summary>
        /// 处理WMI事件错误
        /// </summary>
        private void OnWmiEventError(object sender, WmiEventErrorArgs e)
        {
            _statistics.WmiStatistics.WmiErrors++;
            OnMonitoringError(new MonitoringErrorEventArgs(e.Exception, e.Context));
        }

        /// <summary>
        /// 处理持久化保存完成事件
        /// </summary>
        private void OnPersistenceSaveCompleted(object sender, PersistenceEventArgs e)
        {
            // 可以记录持久化事件或更新统计
        }

        /// <summary>
        /// 处理持久化加载完成事件
        /// </summary>
        private void OnPersistenceLoadCompleted(object sender, PersistenceEventArgs e)
        {
            // 可以记录持久化事件或更新统计
        }

        /// <summary>
        /// 处理持久化错误事件
        /// </summary>
        private void OnPersistenceError(object sender, PersistenceErrorEventArgs e)
        {
            OnMonitoringError(new MonitoringErrorEventArgs(e.Exception, e.Context));
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 判断是否为AI工具进程
        /// </summary>
        /// <param name="processName">进程名称</param>
        /// <returns>是否为AI工具进程</returns>
        private bool IsAIToolProcess(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return false;

            var lowerName = processName.ToLowerInvariant();
            return lowerName.Contains("claude") ||
                   lowerName.Contains("codex") ||
                   lowerName.Contains("copilot") ||
                   lowerName.Contains("openai");
        }

        /// <summary>
        /// 获取管理器摘要信息
        /// </summary>
        /// <returns>摘要字符串</returns>
        public string GetSummary()
        {
            var runTime = StartTime.HasValue ? DateTime.UtcNow - StartTime.Value : TimeSpan.Zero;
            return $"监控管理器 - 状态: {State}, 运行时间: {runTime:hh\\:mm\\:ss}, " +
                   $"监控进程: {MonitoredProcessCount}, 待处理事件: {PendingEventCount}, " +
                   $"内存: {_statistics.MonitoringSystemMemoryMB:F1}MB";
        }

        #endregion

        #region 事件触发方法

        /// <summary>
        /// 触发状态变化事件
        /// </summary>
        protected virtual void OnStateChanged(MonitoringStateChangedEventArgs e)
        {
            StateChanged?.Invoke(this, e);
        }

        /// <summary>
        /// 触发进程事件
        /// </summary>
        protected virtual void OnProcessEvent(ProcessMonitoringEventArgs e)
        {
            ProcessEvent?.Invoke(this, e);
        }

        /// <summary>
        /// 触发健康检查完成事件
        /// </summary>
        protected virtual void OnHealthCheckCompleted(HealthCheckCompletedEventArgs e)
        {
            HealthCheckCompleted?.Invoke(this, e);
        }

        /// <summary>
        /// 触发监控错误事件
        /// </summary>
        protected virtual void OnMonitoringError(MonitoringErrorEventArgs e)
        {
            MonitoringError?.Invoke(this, e);
        }

        /// <summary>
        /// 触发性能警报事件
        /// </summary>
        protected virtual void OnPerformanceAlert(PerformanceAlertEventArgs e)
        {
            PerformanceAlert?.Invoke(this, e);
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
                    if (_isRunning)
                    {
                        StopAsync(_cancellationTokenSource.Token).Wait(TimeSpan.FromSeconds(_configuration.GracefulShutdownTimeoutSeconds));
                    }

                    // 取消所有操作
                    _cancellationTokenSource?.Cancel();

                    // 释放定时器
                    _healthCheckTimer?.Dispose();
                    _statisticsUpdateTimer?.Dispose();
                    _pollingFallbackTimer?.Dispose();
                    _cleanupTimer?.Dispose();

                    // 释放信号量
                    _operationSemaphore?.Dispose();

                    // 释放取消令牌源
                    _cancellationTokenSource?.Dispose();

                    // 释放其他资源
                    _persistence?.Dispose();
                    _processMonitor?.Dispose();
                    _wmiEventListener?.Dispose();
                }
                catch { /* 忽略释放异常 */ }
            }

            _disposed = true;
        }

        /// <summary>
        /// 析构函数
        /// </summary>
        ~MonitoringManager()
        {
            Dispose(false);
        }

        #endregion
    }

    #region 事件参数类

    /// <summary>
    /// 健康检查完成事件参数
    /// </summary>
    public class HealthCheckCompletedEventArgs : EventArgs
    {
        /// <summary>
        /// 健康检查结果
        /// </summary>
        public MonitoringHealthResult HealthResult { get; }

        /// <summary>
        /// 检查时间
        /// </summary>
        public DateTime Timestamp { get; }

        public HealthCheckCompletedEventArgs(MonitoringHealthResult healthResult)
        {
            HealthResult = healthResult ?? throw new ArgumentNullException(nameof(healthResult));
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// 性能警报事件参数
    /// </summary>
    public class PerformanceAlertEventArgs : EventArgs
    {
        /// <summary>
        /// 警报事件
        /// </summary>
        public MonitoringEvent AlertEvent { get; }

        /// <summary>
        /// 警报时间
        /// </summary>
        public DateTime Timestamp { get; }

        public PerformanceAlertEventArgs(MonitoringEvent alertEvent)
        {
            AlertEvent = alertEvent ?? throw new ArgumentNullException(nameof(alertEvent));
            Timestamp = DateTime.UtcNow;
        }
    }

    #endregion
}