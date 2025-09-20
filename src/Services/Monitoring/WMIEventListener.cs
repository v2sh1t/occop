using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Occop.Models.Monitoring;

namespace Occop.Services.Monitoring
{
    /// <summary>
    /// WMI事件监听器实现
    /// 基于System.Management库实现进程创建/删除事件的实时监听
    /// 支持事件去重、性能优化和错误恢复
    /// </summary>
    public class WMIEventListener : WmiEventListenerBase
    {
        #region 私有字段

        private ManagementEventWatcher _processCreationWatcher;
        private ManagementEventWatcher _processDeletionWatcher;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly SemaphoreSlim _operationSemaphore;
        private readonly object _lockObject = new object();

        // 事件去重缓存
        private readonly ConcurrentDictionary<string, WMIProcessEvent> _eventCache;
        private readonly Timer _cacheCleanupTimer;

        // 性能优化相关
        private readonly ConcurrentQueue<WmiProcessEventArgs> _eventQueue;
        private readonly Timer _eventProcessingTimer;
        private volatile bool _isProcessingEvents;

        // AI工具进程过滤
        private readonly HashSet<string> _aiToolProcessNames;
        private readonly TimeSpan _eventCacheTimeout = TimeSpan.FromMinutes(5);
        private readonly TimeSpan _eventProcessingInterval = TimeSpan.FromMilliseconds(100);

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="config">监听配置</param>
        public WMIEventListener(WmiListenerConfig config = null) : base(config)
        {
            _operationSemaphore = new SemaphoreSlim(1, 1);
            _eventCache = new ConcurrentDictionary<string, WMIProcessEvent>();
            _eventQueue = new ConcurrentQueue<WmiProcessEventArgs>();

            // 初始化AI工具进程名称列表
            _aiToolProcessNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "claude", "claude-code", "claude-cli",
                "copilot", "gh-copilot",
                "openai", "openai-cli",
                "anthropic-cli"
            };

            // 添加配置中的额外进程名称
            if (_config.ProcessNameFilters != null)
            {
                foreach (var name in _config.ProcessNameFilters)
                {
                    _aiToolProcessNames.Add(name);
                }
            }

            // 启动缓存清理定时器
            _cacheCleanupTimer = new Timer(CleanupExpiredEvents, null,
                TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

            // 启动事件处理定时器
            _eventProcessingTimer = new Timer(ProcessQueuedEvents, null,
                _eventProcessingInterval, _eventProcessingInterval);
        }

        #endregion

        #region WmiEventListenerBase实现

        /// <summary>
        /// 启动WMI事件监听
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>启动结果</returns>
        public override async Task<MonitoringResult> StartListeningAsync(CancellationToken cancellationToken = default)
        {
            if (_isListening)
                return MonitoringResult.Failure("WMI监听器已在运行中");

            await _operationSemaphore.WaitAsync(cancellationToken);

            try
            {
                // 检查WMI可用性
                var availabilityResult = await CheckWmiAvailabilityAsync();
                if (!availabilityResult.IsSuccess)
                    return availabilityResult;

                _cancellationTokenSource = new CancellationTokenSource();

                var result = await StartWmiWatchersAsync(cancellationToken);
                if (result.IsSuccess)
                {
                    _isListening = true;
                    _statistics.StartTime = DateTime.UtcNow;

                    return MonitoringResult.Success("WMI事件监听器启动成功");
                }

                return result;
            }
            catch (Exception ex)
            {
                var errorArgs = new WmiEventErrorArgs(ex, "启动WMI监听器", false);
                OnEventError(errorArgs);
                return MonitoringResult.Failure($"启动WMI监听器失败: {ex.Message}");
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }

        /// <summary>
        /// 停止WMI事件监听
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>停止结果</returns>
        public override async Task<MonitoringResult> StopListeningAsync(CancellationToken cancellationToken = default)
        {
            if (!_isListening)
                return MonitoringResult.Success("WMI监听器未在运行");

            await _operationSemaphore.WaitAsync(cancellationToken);

            try
            {
                _cancellationTokenSource?.Cancel();

                await Task.Run(() => StopWmiWatchers(), cancellationToken);

                _isListening = false;
                _statistics.TotalRunTime = DateTime.UtcNow - _statistics.StartTime;

                // 处理队列中剩余的事件
                ProcessQueuedEvents(null);

                return MonitoringResult.Success("WMI事件监听器停止成功");
            }
            catch (Exception ex)
            {
                var errorArgs = new WmiEventErrorArgs(ex, "停止WMI监听器", false);
                OnEventError(errorArgs);
                return MonitoringResult.Failure($"停止WMI监听器失败: {ex.Message}");
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }

        /// <summary>
        /// 检查WMI服务可用性
        /// </summary>
        /// <returns>检查结果</returns>
        public override async Task<MonitoringResult> CheckWmiAvailabilityAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    using var scope = new ManagementScope(_config.WmiNamespace);
                    scope.Connect();

                    // 测试基本WMI查询
                    using var query = new ObjectQuery("SELECT ProcessId FROM Win32_Process WHERE ProcessId = 0");
                    using var searcher = new ManagementObjectSearcher(scope, query);
                    using var results = searcher.Get();

                    // 如果能执行查询就认为WMI可用
                });

                return MonitoringResult.Success("WMI服务可用");
            }
            catch (Exception ex)
            {
                return MonitoringResult.Failure($"WMI服务不可用: {ex.Message}");
            }
        }

        #endregion

        #region WMI监听器管理

        /// <summary>
        /// 启动WMI监听器
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>启动结果</returns>
        private async Task<MonitoringResult> StartWmiWatchersAsync(CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // 启动进程创建监听器
                    if (_config.ListenProcessCreation)
                    {
                        _processCreationWatcher = CreateProcessCreationWatcher();
                        _processCreationWatcher.Start();
                    }

                    // 启动进程删除监听器
                    if (_config.ListenProcessDeletion)
                    {
                        _processDeletionWatcher = CreateProcessDeletionWatcher();
                        _processDeletionWatcher.Start();
                    }

                    return MonitoringResult.Success("WMI监听器已启动");
                }
                catch (Exception ex)
                {
                    StopWmiWatchers();
                    throw new InvalidOperationException($"启动WMI监听器失败: {ex.Message}", ex);
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 停止WMI监听器
        /// </summary>
        private void StopWmiWatchers()
        {
            try
            {
                _processCreationWatcher?.Stop();
                _processCreationWatcher?.Dispose();
                _processCreationWatcher = null;
            }
            catch { }

            try
            {
                _processDeletionWatcher?.Stop();
                _processDeletionWatcher?.Dispose();
                _processDeletionWatcher = null;
            }
            catch { }
        }

        /// <summary>
        /// 创建进程创建监听器
        /// </summary>
        /// <returns>ManagementEventWatcher</returns>
        private ManagementEventWatcher CreateProcessCreationWatcher()
        {
            var query = new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace");
            var watcher = new ManagementEventWatcher(_config.WmiNamespace, query);

            watcher.Options.Timeout = TimeSpan.FromSeconds(_config.QueryTimeoutSeconds);
            watcher.EventArrived += OnProcessCreationEvent;

            return watcher;
        }

        /// <summary>
        /// 创建进程删除监听器
        /// </summary>
        /// <returns>ManagementEventWatcher</returns>
        private ManagementEventWatcher CreateProcessDeletionWatcher()
        {
            var query = new WqlEventQuery("SELECT * FROM Win32_ProcessStopTrace");
            var watcher = new ManagementEventWatcher(_config.WmiNamespace, query);

            watcher.Options.Timeout = TimeSpan.FromSeconds(_config.QueryTimeoutSeconds);
            watcher.EventArrived += OnProcessDeletionEvent;

            return watcher;
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 处理进程创建事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void OnProcessCreationEvent(object sender, EventArrivedEventArgs e)
        {
            try
            {
                var targetInstance = e.NewEvent;
                var processId = Convert.ToInt32(targetInstance["ProcessID"]);
                var processName = targetInstance["ProcessName"]?.ToString() ?? "Unknown";
                var parentProcessId = Convert.ToInt32(targetInstance["ParentProcessID"]);

                // AI工具过滤
                if (_config.OnlyAIToolProcesses && !IsAIToolProcess(processName))
                    return;

                var eventArgs = new WmiProcessEventArgs(
                    processId, processName, WmiProcessEventType.ProcessCreated,
                    null, parentProcessId, null);

                // 使用队列处理事件以提高性能
                _eventQueue.Enqueue(eventArgs);
            }
            catch (Exception ex)
            {
                OnEventError(new WmiEventErrorArgs(ex, "处理进程创建事件"));
            }
        }

        /// <summary>
        /// 处理进程删除事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void OnProcessDeletionEvent(object sender, EventArrivedEventArgs e)
        {
            try
            {
                var targetInstance = e.NewEvent;
                var processId = Convert.ToInt32(targetInstance["ProcessID"]);
                var processName = targetInstance["ProcessName"]?.ToString() ?? "Unknown";
                var parentProcessId = Convert.ToInt32(targetInstance["ParentProcessID"]);

                // AI工具过滤
                if (_config.OnlyAIToolProcesses && !IsAIToolProcess(processName))
                    return;

                var eventArgs = new WmiProcessEventArgs(
                    processId, processName, WmiProcessEventType.ProcessDeleted,
                    null, parentProcessId, null);

                // 使用队列处理事件以提高性能
                _eventQueue.Enqueue(eventArgs);
            }
            catch (Exception ex)
            {
                OnEventError(new WmiEventErrorArgs(ex, "处理进程删除事件"));
            }
        }

        /// <summary>
        /// 处理队列中的事件
        /// </summary>
        /// <param name="state">定时器状态</param>
        private void ProcessQueuedEvents(object state)
        {
            if (_isProcessingEvents || _eventQueue.IsEmpty)
                return;

            _isProcessingEvents = true;

            try
            {
                var processedCount = 0;
                var maxBatchSize = 50; // 批量处理最多50个事件

                while (_eventQueue.TryDequeue(out var eventArgs) && processedCount < maxBatchSize)
                {
                    ProcessSingleEvent(eventArgs);
                    processedCount++;
                }
            }
            catch (Exception ex)
            {
                OnEventError(new WmiEventErrorArgs(ex, "批量处理事件队列"));
            }
            finally
            {
                _isProcessingEvents = false;
            }
        }

        /// <summary>
        /// 处理单个事件
        /// </summary>
        /// <param name="eventArgs">事件参数</param>
        private void ProcessSingleEvent(WmiProcessEventArgs eventArgs)
        {
            try
            {
                // 创建WMI进程事件
                var wmiEvent = new WMIProcessEvent(eventArgs);

                // 事件去重检查
                if (IsDuplicateEvent(wmiEvent))
                {
                    HandleDuplicateEvent(wmiEvent);
                    return;
                }

                // 缓存事件用于去重
                _eventCache.TryAdd(wmiEvent.EventUniqueKey, wmiEvent);

                // 触发相应的事件
                if (eventArgs.EventType == WmiProcessEventType.ProcessCreated)
                {
                    OnProcessCreated(eventArgs);
                }
                else if (eventArgs.EventType == WmiProcessEventType.ProcessDeleted)
                {
                    OnProcessDeleted(eventArgs);
                }
            }
            catch (Exception ex)
            {
                OnEventError(new WmiEventErrorArgs(ex, $"处理事件 {eventArgs.ProcessName}[{eventArgs.ProcessId}]"));
            }
        }

        #endregion

        #region 事件去重和性能优化

        /// <summary>
        /// 检查是否为重复事件
        /// </summary>
        /// <param name="wmiEvent">WMI事件</param>
        /// <returns>是否为重复事件</returns>
        private bool IsDuplicateEvent(WMIProcessEvent wmiEvent)
        {
            return _eventCache.ContainsKey(wmiEvent.EventUniqueKey);
        }

        /// <summary>
        /// 处理重复事件
        /// </summary>
        /// <param name="wmiEvent">重复的WMI事件</param>
        private void HandleDuplicateEvent(WMIProcessEvent wmiEvent)
        {
            if (_eventCache.TryGetValue(wmiEvent.EventUniqueKey, out var existingEvent))
            {
                existingEvent.MarkAsDuplicate();
                // 可以选择记录重复事件的统计信息
            }
        }

        /// <summary>
        /// 清理过期的事件缓存
        /// </summary>
        /// <param name="state">定时器状态</param>
        private void CleanupExpiredEvents(object state)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow - _eventCacheTimeout;
                var expiredKeys = _eventCache
                    .Where(kvp => kvp.Value.FirstDetectedTime < cutoffTime)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    _eventCache.TryRemove(key, out _);
                }
            }
            catch (Exception ex)
            {
                OnEventError(new WmiEventErrorArgs(ex, "清理过期事件缓存"));
            }
        }

        /// <summary>
        /// 检查是否为AI工具进程
        /// </summary>
        /// <param name="processName">进程名称</param>
        /// <returns>是否为AI工具进程</returns>
        private bool IsAIToolProcess(string processName)
        {
            if (string.IsNullOrEmpty(processName))
                return false;

            return _aiToolProcessNames.Any(name =>
                processName.Contains(name, StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        #region 错误处理和重连

        /// <summary>
        /// 处理WMI错误并尝试重连
        /// </summary>
        /// <param name="ex">异常</param>
        /// <param name="context">错误上下文</param>
        private async Task HandleWmiErrorWithRetry(Exception ex, string context)
        {
            OnEventError(new WmiEventErrorArgs(ex, context, true));

            if (_isListening && _statistics.ReconnectionCount < _config.MaxRetryCount)
            {
                try
                {
                    _statistics.ReconnectionCount++;
                    await Task.Delay(_config.RetryIntervalMs);

                    // 尝试重新启动监听器
                    StopWmiWatchers();
                    await StartWmiWatchersAsync(CancellationToken.None);
                }
                catch (Exception retryEx)
                {
                    OnEventError(new WmiEventErrorArgs(retryEx, $"重连尝试 {_statistics.ReconnectionCount}", false));
                }
            }
        }

        #endregion

        #region 资源释放

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否正在释放</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                try
                {
                    StopListeningAsync().Wait(TimeSpan.FromSeconds(5));
                }
                catch { }

                _cacheCleanupTimer?.Dispose();
                _eventProcessingTimer?.Dispose();
                _operationSemaphore?.Dispose();
                _cancellationTokenSource?.Dispose();

                StopWmiWatchers();
            }

            base.Dispose(disposing);
        }

        #endregion

        #region 公共辅助方法

        /// <summary>
        /// 获取当前事件队列大小
        /// </summary>
        /// <returns>队列中待处理事件数量</returns>
        public int GetQueueSize()
        {
            return _eventQueue.Count;
        }

        /// <summary>
        /// 获取事件缓存大小
        /// </summary>
        /// <returns>缓存中事件数量</returns>
        public int GetCacheSize()
        {
            return _eventCache.Count;
        }

        /// <summary>
        /// 强制处理队列中的所有事件
        /// </summary>
        public void FlushEventQueue()
        {
            ProcessQueuedEvents(null);
        }

        /// <summary>
        /// 清空事件缓存
        /// </summary>
        public void ClearEventCache()
        {
            _eventCache.Clear();
        }

        /// <summary>
        /// 添加AI工具进程名称
        /// </summary>
        /// <param name="processName">进程名称</param>
        public void AddAIToolProcessName(string processName)
        {
            if (!string.IsNullOrWhiteSpace(processName))
            {
                _aiToolProcessNames.Add(processName);
            }
        }

        /// <summary>
        /// 移除AI工具进程名称
        /// </summary>
        /// <param name="processName">进程名称</param>
        public bool RemoveAIToolProcessName(string processName)
        {
            return !string.IsNullOrWhiteSpace(processName) && _aiToolProcessNames.Remove(processName);
        }

        #endregion
    }
}