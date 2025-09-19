using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Occop.Models.Configuration;
using Occop.Services.Configuration;
using Occop.Services.Security;
using Occop.Services.Logging;
using Occop.Core.Patterns.Observer;

namespace Occop.Services.Configuration
{
    /// <summary>
    /// 监控事件类型
    /// </summary>
    public enum MonitorEventType
    {
        /// <summary>
        /// 配置变更
        /// </summary>
        ConfigurationChanged,

        /// <summary>
        /// 健康状态变更
        /// </summary>
        HealthStatusChanged,

        /// <summary>
        /// 监控启动
        /// </summary>
        MonitoringStarted,

        /// <summary>
        /// 监控停止
        /// </summary>
        MonitoringStopped,

        /// <summary>
        /// 监控错误
        /// </summary>
        MonitoringError,

        /// <summary>
        /// 定期检查完成
        /// </summary>
        PeriodicCheckCompleted
    }

    /// <summary>
    /// 监控事件数据
    /// </summary>
    public class MonitorEventArgs : EventArgs
    {
        /// <summary>
        /// 事件类型
        /// </summary>
        public MonitorEventType EventType { get; }

        /// <summary>
        /// 事件消息
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 事件详细信息
        /// </summary>
        public string? Details { get; }

        /// <summary>
        /// 健康检查结果（如果适用）
        /// </summary>
        public HealthCheckResult? HealthResult { get; }

        /// <summary>
        /// 配置状态（如果适用）
        /// </summary>
        public Dictionary<string, object>? ConfigurationStatus { get; }

        /// <summary>
        /// 事件时间戳
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// 异常信息（如果有）
        /// </summary>
        public Exception? Exception { get; }

        /// <summary>
        /// 初始化监控事件数据
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <param name="message">事件消息</param>
        /// <param name="details">详细信息</param>
        /// <param name="healthResult">健康检查结果</param>
        /// <param name="configurationStatus">配置状态</param>
        /// <param name="exception">异常信息</param>
        public MonitorEventArgs(
            MonitorEventType eventType,
            string message,
            string? details = null,
            HealthCheckResult? healthResult = null,
            Dictionary<string, object>? configurationStatus = null,
            Exception? exception = null)
        {
            EventType = eventType;
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Details = details;
            HealthResult = healthResult;
            ConfigurationStatus = configurationStatus;
            Exception = exception;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// 监控配置
    /// </summary>
    public class MonitorConfiguration
    {
        /// <summary>
        /// 健康检查间隔（毫秒）
        /// </summary>
        public int HealthCheckIntervalMs { get; set; } = 30000; // 30秒

        /// <summary>
        /// 配置变更检查间隔（毫秒）
        /// </summary>
        public int ConfigurationCheckIntervalMs { get; set; } = 10000; // 10秒

        /// <summary>
        /// 是否启用自动恢复
        /// </summary>
        public bool EnableAutoRecovery { get; set; } = true;

        /// <summary>
        /// 最大重试次数
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// 重试间隔（毫秒）
        /// </summary>
        public int RetryIntervalMs { get; set; } = 5000; // 5秒

        /// <summary>
        /// 是否启用详细日志
        /// </summary>
        public bool EnableVerboseLogging { get; set; } = false;

        /// <summary>
        /// 健康分数警告阈值
        /// </summary>
        public int HealthScoreWarningThreshold { get; set; } = 70;

        /// <summary>
        /// 健康分数错误阈值
        /// </summary>
        public int HealthScoreErrorThreshold { get; set; } = 50;
    }

    /// <summary>
    /// 配置监控器
    /// 负责实时监控配置状态、健康检查和异常检测
    /// </summary>
    public class ConfigurationMonitor : IDisposable, ISubject<MonitorEventArgs>
    {
        private readonly ClaudeCodeConfigurator _configurator;
        private readonly ConfigurationValidator _validator;
        private readonly ConfigurationLogger _logger;
        private readonly MonitorConfiguration _config;
        private readonly System.Timers.Timer _healthCheckTimer;
        private readonly System.Timers.Timer _configurationCheckTimer;
        private readonly List<IObserver<MonitorEventArgs>> _observers;
        private readonly object _lockObject;
        private readonly SemaphoreSlim _semaphore;

        private bool _disposed;
        private bool _isMonitoring;
        private DateTime? _lastHealthCheckTime;
        private HealthCheckResult? _lastHealthResult;
        private Dictionary<string, object>? _lastConfigurationSnapshot;
        private int _consecutiveFailures;

        /// <summary>
        /// 是否正在监控
        /// </summary>
        public bool IsMonitoring => _isMonitoring;

        /// <summary>
        /// 监控开始时间
        /// </summary>
        public DateTime? MonitoringStartedAt { get; private set; }

        /// <summary>
        /// 上次健康检查时间
        /// </summary>
        public DateTime? LastHealthCheckTime => _lastHealthCheckTime;

        /// <summary>
        /// 上次健康检查结果
        /// </summary>
        public HealthCheckResult? LastHealthResult => _lastHealthResult;

        /// <summary>
        /// 连续失败次数
        /// </summary>
        public int ConsecutiveFailures => _consecutiveFailures;

        /// <summary>
        /// 监控事件
        /// </summary>
        public event EventHandler<MonitorEventArgs>? MonitorEvent;

        /// <summary>
        /// 健康状态变更事件
        /// </summary>
        public event EventHandler<HealthCheckResult>? HealthStatusChanged;

        /// <summary>
        /// 配置变更事件
        /// </summary>
        public event EventHandler<Dictionary<string, object>>? ConfigurationChanged;

        /// <summary>
        /// 初始化配置监控器
        /// </summary>
        /// <param name="configurator">Claude Code配置器</param>
        /// <param name="validator">配置验证器</param>
        /// <param name="logger">配置日志记录器</param>
        /// <param name="config">监控配置</param>
        public ConfigurationMonitor(
            ClaudeCodeConfigurator configurator,
            ConfigurationValidator validator,
            ConfigurationLogger logger,
            MonitorConfiguration? config = null)
        {
            _configurator = configurator ?? throw new ArgumentNullException(nameof(configurator));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? new MonitorConfiguration();
            _observers = new List<IObserver<MonitorEventArgs>>();
            _lockObject = new object();
            _semaphore = new SemaphoreSlim(1, 1);

            // 初始化定时器
            _healthCheckTimer = new System.Timers.Timer(_config.HealthCheckIntervalMs)
            {
                AutoReset = true,
                Enabled = false
            };
            _healthCheckTimer.Elapsed += OnHealthCheckTimerElapsed;

            _configurationCheckTimer = new System.Timers.Timer(_config.ConfigurationCheckIntervalMs)
            {
                AutoReset = true,
                Enabled = false
            };
            _configurationCheckTimer.Elapsed += OnConfigurationCheckTimerElapsed;
        }

        /// <summary>
        /// 开始监控
        /// </summary>
        /// <returns>启动结果</returns>
        public async Task<ConfigurationResult> StartMonitoringAsync()
        {
            ThrowIfDisposed();

            if (_isMonitoring)
            {
                return new ConfigurationResult(false, ConfigurationOperation.Validate,
                    "Monitoring is already started");
            }

            try
            {
                await _semaphore.WaitAsync();

                // 初始化监控状态
                _lastConfigurationSnapshot = _configurator.GetConfigurationStatus();
                _lastHealthResult = await _validator.PerformHealthCheckAsync();
                _lastHealthCheckTime = DateTime.UtcNow;
                _consecutiveFailures = 0;

                // 启动定时器
                _healthCheckTimer.Start();
                _configurationCheckTimer.Start();

                _isMonitoring = true;
                MonitoringStartedAt = DateTime.UtcNow;

                var eventArgs = new MonitorEventArgs(
                    MonitorEventType.MonitoringStarted,
                    "Configuration monitoring started",
                    $"Health check interval: {_config.HealthCheckIntervalMs}ms, Configuration check interval: {_config.ConfigurationCheckIntervalMs}ms",
                    _lastHealthResult,
                    _lastConfigurationSnapshot);

                NotifyObservers(eventArgs);
                MonitorEvent?.Invoke(this, eventArgs);

                await _logger.LogOperationAsync(ConfigurationOperation.Validate, true,
                    "Configuration monitoring started successfully");

                return new ConfigurationResult(true, ConfigurationOperation.Validate,
                    "Configuration monitoring started successfully");
            }
            catch (Exception ex)
            {
                var eventArgs = new MonitorEventArgs(
                    MonitorEventType.MonitoringError,
                    "Failed to start configuration monitoring",
                    ex.Message,
                    exception: ex);

                NotifyObservers(eventArgs);
                MonitorEvent?.Invoke(this, eventArgs);

                await _logger.LogOperationAsync(ConfigurationOperation.Validate, false,
                    $"Failed to start configuration monitoring: {ex.Message}");

                return new ConfigurationResult(false, ConfigurationOperation.Validate,
                    "Failed to start configuration monitoring", ex);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// 停止监控
        /// </summary>
        /// <returns>停止结果</returns>
        public async Task<ConfigurationResult> StopMonitoringAsync()
        {
            ThrowIfDisposed();

            if (!_isMonitoring)
            {
                return new ConfigurationResult(false, ConfigurationOperation.Validate,
                    "Monitoring is not started");
            }

            try
            {
                await _semaphore.WaitAsync();

                // 停止定时器
                _healthCheckTimer.Stop();
                _configurationCheckTimer.Stop();

                _isMonitoring = false;
                var monitoringDuration = MonitoringStartedAt.HasValue
                    ? DateTime.UtcNow - MonitoringStartedAt.Value
                    : TimeSpan.Zero;

                var eventArgs = new MonitorEventArgs(
                    MonitorEventType.MonitoringStopped,
                    "Configuration monitoring stopped",
                    $"Monitoring duration: {monitoringDuration.TotalMinutes:F1} minutes");

                NotifyObservers(eventArgs);
                MonitorEvent?.Invoke(this, eventArgs);

                await _logger.LogOperationAsync(ConfigurationOperation.Validate, true,
                    $"Configuration monitoring stopped after {monitoringDuration.TotalMinutes:F1} minutes");

                return new ConfigurationResult(true, ConfigurationOperation.Validate,
                    "Configuration monitoring stopped successfully");
            }
            catch (Exception ex)
            {
                var eventArgs = new MonitorEventArgs(
                    MonitorEventType.MonitoringError,
                    "Failed to stop configuration monitoring",
                    ex.Message,
                    exception: ex);

                NotifyObservers(eventArgs);
                MonitorEvent?.Invoke(this, eventArgs);

                await _logger.LogOperationAsync(ConfigurationOperation.Validate, false,
                    $"Failed to stop configuration monitoring: {ex.Message}");

                return new ConfigurationResult(false, ConfigurationOperation.Validate,
                    "Failed to stop configuration monitoring", ex);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// 手动触发健康检查
        /// </summary>
        /// <returns>健康检查结果</returns>
        public async Task<HealthCheckResult> TriggerHealthCheckAsync()
        {
            ThrowIfDisposed();

            try
            {
                var healthResult = await _validator.PerformHealthCheckAsync();
                await ProcessHealthCheckResult(healthResult);
                return healthResult;
            }
            catch (Exception ex)
            {
                var eventArgs = new MonitorEventArgs(
                    MonitorEventType.MonitoringError,
                    "Manual health check failed",
                    ex.Message,
                    exception: ex);

                NotifyObservers(eventArgs);
                MonitorEvent?.Invoke(this, eventArgs);

                // 创建错误健康结果
                var errorResults = new List<ConfigurationValidationResult>
                {
                    new ConfigurationValidationResult(
                        ValidationResultType.Fatal,
                        "MANUAL_HEALTH_CHECK",
                        "Manual health check failed with exception",
                        ex.Message,
                        "Check system integrity and retry",
                        ex)
                };

                return new HealthCheckResult(errorResults);
            }
        }

        /// <summary>
        /// 获取监控统计信息
        /// </summary>
        /// <returns>统计信息</returns>
        public Dictionary<string, object> GetMonitoringStatistics()
        {
            ThrowIfDisposed();

            lock (_lockObject)
            {
                var stats = new Dictionary<string, object>
                {
                    { "IsMonitoring", _isMonitoring },
                    { "MonitoringStartedAt", MonitoringStartedAt?.ToString("yyyy-MM-dd HH:mm:ss UTC") ?? "N/A" },
                    { "LastHealthCheckTime", _lastHealthCheckTime?.ToString("yyyy-MM-dd HH:mm:ss UTC") ?? "N/A" },
                    { "ConsecutiveFailures", _consecutiveFailures },
                    { "HealthCheckIntervalMs", _config.HealthCheckIntervalMs },
                    { "ConfigurationCheckIntervalMs", _config.ConfigurationCheckIntervalMs },
                    { "EnableAutoRecovery", _config.EnableAutoRecovery },
                    { "MaxRetryAttempts", _config.MaxRetryAttempts },
                    { "ObserversCount", _observers.Count },
                    { "Timestamp", DateTime.UtcNow }
                };

                if (_lastHealthResult != null)
                {
                    stats["LastHealthScore"] = _lastHealthResult.HealthScore;
                    stats["LastHealthStatus"] = _lastHealthResult.IsHealthy ? "Healthy" : "Unhealthy";
                    stats["LastHealthSummary"] = _lastHealthResult.Summary;
                }

                if (MonitoringStartedAt.HasValue)
                {
                    var uptime = DateTime.UtcNow - MonitoringStartedAt.Value;
                    stats["UptimeMinutes"] = uptime.TotalMinutes;
                }

                return stats;
            }
        }

        /// <summary>
        /// 健康检查定时器事件处理
        /// </summary>
        private async void OnHealthCheckTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            if (_disposed)
                return;

            try
            {
                var healthResult = await _validator.PerformHealthCheckAsync();
                await ProcessHealthCheckResult(healthResult);
            }
            catch (Exception ex)
            {
                var eventArgs = new MonitorEventArgs(
                    MonitorEventType.MonitoringError,
                    "Scheduled health check failed",
                    ex.Message,
                    exception: ex);

                NotifyObservers(eventArgs);
                MonitorEvent?.Invoke(this, eventArgs);

                if (_config.EnableVerboseLogging)
                {
                    await _logger.LogOperationAsync(ConfigurationOperation.Validate, false,
                        $"Scheduled health check failed: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 配置检查定时器事件处理
        /// </summary>
        private async void OnConfigurationCheckTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            if (_disposed)
                return;

            try
            {
                var currentSnapshot = _configurator.GetConfigurationStatus();
                await ProcessConfigurationChange(currentSnapshot);
            }
            catch (Exception ex)
            {
                var eventArgs = new MonitorEventArgs(
                    MonitorEventType.MonitoringError,
                    "Scheduled configuration check failed",
                    ex.Message,
                    exception: ex);

                NotifyObservers(eventArgs);
                MonitorEvent?.Invoke(this, eventArgs);

                if (_config.EnableVerboseLogging)
                {
                    await _logger.LogOperationAsync(ConfigurationOperation.Validate, false,
                        $"Scheduled configuration check failed: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 处理健康检查结果
        /// </summary>
        /// <param name="healthResult">健康检查结果</param>
        private async Task ProcessHealthCheckResult(HealthCheckResult healthResult)
        {
            var previousResult = _lastHealthResult;
            _lastHealthResult = healthResult;
            _lastHealthCheckTime = DateTime.UtcNow;

            // 检查健康状态是否变更
            bool statusChanged = previousResult == null ||
                                previousResult.IsHealthy != healthResult.IsHealthy ||
                                Math.Abs(previousResult.HealthScore - healthResult.HealthScore) >= 10;

            if (statusChanged)
            {
                HealthStatusChanged?.Invoke(this, healthResult);
            }

            // 处理失败计数
            if (!healthResult.IsHealthy)
            {
                _consecutiveFailures++;

                // 尝试自动恢复
                if (_config.EnableAutoRecovery && _consecutiveFailures <= _config.MaxRetryAttempts)
                {
                    await AttemptAutoRecovery();
                }
            }
            else
            {
                _consecutiveFailures = 0;
            }

            // 发送监控事件
            var eventType = healthResult.HealthScore >= _config.HealthScoreWarningThreshold
                ? MonitorEventType.PeriodicCheckCompleted
                : MonitorEventType.HealthStatusChanged;

            var eventArgs = new MonitorEventArgs(
                eventType,
                $"Health check completed with score {healthResult.HealthScore}/100",
                healthResult.Summary,
                healthResult);

            NotifyObservers(eventArgs);
            MonitorEvent?.Invoke(this, eventArgs);

            if (_config.EnableVerboseLogging || !healthResult.IsHealthy)
            {
                await _logger.LogOperationAsync(ConfigurationOperation.Validate, healthResult.IsHealthy,
                    $"Health check completed. Score: {healthResult.HealthScore}/100, Status: {(healthResult.IsHealthy ? "Healthy" : "Unhealthy")}");
            }
        }

        /// <summary>
        /// 处理配置变更
        /// </summary>
        /// <param name="currentSnapshot">当前配置快照</param>
        private async Task ProcessConfigurationChange(Dictionary<string, object> currentSnapshot)
        {
            if (_lastConfigurationSnapshot == null)
            {
                _lastConfigurationSnapshot = currentSnapshot;
                return;
            }

            // 检查是否有配置变更
            bool hasChanges = false;
            var changes = new List<string>();

            foreach (var kvp in currentSnapshot)
            {
                if (!_lastConfigurationSnapshot.TryGetValue(kvp.Key, out var previousValue) ||
                    !Equals(previousValue, kvp.Value))
                {
                    hasChanges = true;
                    changes.Add($"{kvp.Key}: {previousValue} -> {kvp.Value}");
                }
            }

            if (hasChanges)
            {
                _lastConfigurationSnapshot = currentSnapshot;

                ConfigurationChanged?.Invoke(this, currentSnapshot);

                var eventArgs = new MonitorEventArgs(
                    MonitorEventType.ConfigurationChanged,
                    "Configuration changes detected",
                    string.Join("; ", changes),
                    configurationStatus: currentSnapshot);

                NotifyObservers(eventArgs);
                MonitorEvent?.Invoke(this, eventArgs);

                await _logger.LogOperationAsync(ConfigurationOperation.Validate, true,
                    $"Configuration changes detected: {changes.Count} items changed");
            }
        }

        /// <summary>
        /// 尝试自动恢复
        /// </summary>
        private async Task AttemptAutoRecovery()
        {
            try
            {
                await _logger.LogOperationAsync(ConfigurationOperation.Validate, true,
                    $"Attempting auto recovery (attempt {_consecutiveFailures}/{_config.MaxRetryAttempts})");

                // 尝试重新应用配置
                var applyResult = await _configurator.ApplyConfigurationAsync();
                if (applyResult.IsSuccess)
                {
                    await _logger.LogOperationAsync(ConfigurationOperation.Apply, true,
                        "Auto recovery: Configuration reapplied successfully");
                }
                else
                {
                    await _logger.LogOperationAsync(ConfigurationOperation.Apply, false,
                        $"Auto recovery failed: {applyResult.Message}");
                }

                // 等待重试间隔
                await Task.Delay(_config.RetryIntervalMs);
            }
            catch (Exception ex)
            {
                await _logger.LogOperationAsync(ConfigurationOperation.Validate, false,
                    $"Auto recovery failed with exception: {ex.Message}");
            }
        }

        /// <summary>
        /// 注册观察者
        /// </summary>
        /// <param name="observer">观察者</param>
        public void RegisterObserver(IObserver<MonitorEventArgs> observer)
        {
            if (observer == null)
                throw new ArgumentNullException(nameof(observer));

            lock (_lockObject)
            {
                if (!_observers.Contains(observer))
                {
                    _observers.Add(observer);
                }
            }
        }

        /// <summary>
        /// 注销观察者
        /// </summary>
        /// <param name="observer">观察者</param>
        public void UnregisterObserver(IObserver<MonitorEventArgs> observer)
        {
            if (observer == null)
                return;

            lock (_lockObject)
            {
                _observers.Remove(observer);
            }
        }

        /// <summary>
        /// 通知观察者
        /// </summary>
        /// <param name="eventArgs">事件数据</param>
        private void NotifyObservers(MonitorEventArgs eventArgs)
        {
            lock (_lockObject)
            {
                foreach (var observer in _observers.ToList())
                {
                    try
                    {
                        observer.OnNext(eventArgs);
                    }
                    catch
                    {
                        // 忽略观察者处理异常
                    }
                }
            }
        }

        /// <summary>
        /// 检查对象是否已释放
        /// </summary>
        /// <exception cref="ObjectDisposedException">对象已释放时抛出</exception>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ConfigurationMonitor));
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    var stopTask = StopMonitoringAsync();
                    stopTask.Wait(TimeSpan.FromSeconds(5));
                }
                catch
                {
                    // 忽略停止时的异常
                }

                _healthCheckTimer?.Dispose();
                _configurationCheckTimer?.Dispose();
                _semaphore?.Dispose();

                // 通知观察者监控器已释放
                lock (_lockObject)
                {
                    foreach (var observer in _observers.ToList())
                    {
                        try
                        {
                            observer.OnCompleted();
                        }
                        catch
                        {
                            // 忽略观察者处理异常
                        }
                    }
                    _observers.Clear();
                }

                _disposed = true;
            }
        }
    }
}