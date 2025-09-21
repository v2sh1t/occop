using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Security;

namespace Occop.Core.Security
{
    /// <summary>
    /// 核心安全管理器，实现敏感信息管理和自动清理功能
    /// Core security manager implementing sensitive information management and automatic cleanup
    /// </summary>
    public class SecurityManager : ISecurityManager
    {
        private readonly ILogger<SecurityManager> _logger;
        private readonly object _lockObject = new object();
        private bool _disposed = false;
        private bool _initialized = false;

        private SecurityContext? _securityContext;
        private SecureStorage? _secureStorage;
        private Timer? _cleanupTimer;
        private readonly Dictionary<CleanupTriggers, bool> _registeredTriggers;

        // 统计信息
        // Statistics
        private int _totalCleanupOperations = 0;
        private DateTime? _lastCleanupTime = null;
        private readonly DateTime _startupTime = DateTime.UtcNow;

        /// <summary>
        /// 安全事件发生时触发
        /// Fired when security events occur
        /// </summary>
        public event EventHandler<SecurityEventArgs>? SecurityEvent;

        /// <summary>
        /// 清理操作完成时触发
        /// Fired when cleanup operations complete
        /// </summary>
        public event EventHandler<CleanupCompletedEventArgs>? CleanupCompleted;

        /// <summary>
        /// 获取安全管理器是否已初始化
        /// Gets whether the security manager is initialized
        /// </summary>
        public bool IsInitialized => _initialized && !_disposed;

        /// <summary>
        /// 获取当前安全上下文
        /// Gets the current security context
        /// </summary>
        public SecurityContext SecurityContext
        {
            get
            {
                lock (_lockObject)
                {
                    ThrowIfNotInitialized();
                    return _securityContext!;
                }
            }
        }

        /// <summary>
        /// 初始化SecurityManager实例
        /// Initializes SecurityManager instance
        /// </summary>
        /// <param name="logger">日志记录器 Logger</param>
        public SecurityManager(ILogger<SecurityManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _registeredTriggers = new Dictionary<CleanupTriggers, bool>();

            _logger.LogDebug("SecurityManager instance created");
        }

        /// <summary>
        /// 初始化安全管理器
        /// Initializes the security manager
        /// </summary>
        /// <param name="context">安全上下文 Security context</param>
        /// <returns>初始化任务 Initialization task</returns>
        public async Task InitializeAsync(SecurityContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            lock (_lockObject)
            {
                ThrowIfDisposed();

                if (_initialized)
                {
                    _logger.LogWarning("SecurityManager is already initialized");
                    return;
                }

                _logger.LogInformation("Initializing SecurityManager for application: {ApplicationId}, SecurityLevel: {SecurityLevel}",
                    context.ApplicationId, context.SecurityLevel);

                try
                {
                    _securityContext = context;
                    _secureStorage = new SecureStorage(_securityContext, _logger.CreateLogger<SecureStorage>());

                    // 订阅存储事件
                    // Subscribe to storage events
                    _secureStorage.StorageOperation += OnStorageOperation;
                    _secureStorage.SecureDataExpired += OnSecureDataExpired;

                    // 注册默认清理触发器
                    // Register default cleanup triggers
                    RegisterCleanupTriggers(_securityContext.CleanupTriggers);

                    // 设置自动清理定时器
                    // Setup automatic cleanup timer
                    SetupAutomaticCleanup();

                    _initialized = true;

                    OnSecurityEvent(SecurityEventType.SecurityManagerInitialized,
                        $"SecurityManager initialized for {context.ApplicationId}");

                    _logger.LogInformation("SecurityManager initialized successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize SecurityManager");
                    throw;
                }
            }

            // 异步初始化任务（如果有的话）
            // Asynchronous initialization tasks (if any)
            await Task.CompletedTask;
        }

        /// <summary>
        /// 安全地存储敏感数据
        /// Securely stores sensitive data
        /// </summary>
        /// <param name="sensitiveData">敏感数据 Sensitive data</param>
        /// <returns>存储操作任务 Storage operation task</returns>
        public async Task<SecureData> StoreSecureDataAsync(SecureString sensitiveData)
        {
            if (sensitiveData == null)
                throw new ArgumentNullException(nameof(sensitiveData));

            lock (_lockObject)
            {
                ThrowIfNotInitialized();

                try
                {
                    // 生成唯一标识符
                    // Generate unique identifier
                    var dataId = Guid.NewGuid().ToString("N");

                    // 根据安全策略设置过期时间
                    // Set expiration time based on security policy
                    DateTime? expiresAt = null;
                    if (_securityContext!.SecurityPolicy.MaxSecureDataLifetime > TimeSpan.Zero)
                    {
                        expiresAt = DateTime.UtcNow.Add(_securityContext.SecurityPolicy.MaxSecureDataLifetime);
                    }

                    // 存储数据
                    // Store data
                    var secureData = _secureStorage!.StoreSecureData(
                        dataId,
                        sensitiveData,
                        SecureDataType.ApiToken,
                        expiresAt);

                    OnSecurityEvent(SecurityEventType.SecureDataStored,
                        $"Secure data stored with ID: {dataId}");

                    _logger.LogDebug("Secure data stored successfully: {DataId}", dataId);

                    return secureData;
                }
                catch (Exception ex)
                {
                    OnSecurityEvent(SecurityEventType.SecureDataStorageError,
                        $"Failed to store secure data: {ex.Message}");
                    _logger.LogError(ex, "Error storing secure data");
                    throw;
                }
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 安全地检索敏感数据
        /// Securely retrieves sensitive data
        /// </summary>
        /// <param name="dataId">数据标识 Data identifier</param>
        /// <returns>敏感数据或null Sensitive data or null</returns>
        public async Task<SecureString?> RetrieveSecureDataAsync(string dataId)
        {
            if (string.IsNullOrWhiteSpace(dataId))
                return null;

            lock (_lockObject)
            {
                ThrowIfNotInitialized();

                try
                {
                    var secureData = _secureStorage!.RetrieveSecureData(dataId);

                    if (secureData != null)
                    {
                        OnSecurityEvent(SecurityEventType.SecureDataRetrieved,
                            $"Secure data retrieved: {dataId}");
                    }

                    return secureData;
                }
                catch (Exception ex)
                {
                    OnSecurityEvent(SecurityEventType.SecureDataRetrievalError,
                        $"Failed to retrieve secure data {dataId}: {ex.Message}");
                    _logger.LogError(ex, "Error retrieving secure data: {DataId}", dataId);
                    throw;
                }
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 清理指定的敏感数据
        /// Clears specified sensitive data
        /// </summary>
        /// <param name="dataId">数据标识 Data identifier</param>
        /// <returns>清理操作任务 Cleanup operation task</returns>
        public async Task<bool> ClearSecureDataAsync(string dataId)
        {
            if (string.IsNullOrWhiteSpace(dataId))
                return false;

            var startTime = DateTime.UtcNow;
            var success = false;

            try
            {
                lock (_lockObject)
                {
                    ThrowIfNotInitialized();
                    success = _secureStorage!.RemoveSecureData(dataId);
                }

                var duration = DateTime.UtcNow - startTime;

                OnCleanupCompleted(new CleanupCompletedEventArgs(
                    CleanupOperationType.SingleItem,
                    success,
                    success ? 1 : 0,
                    duration));

                if (success)
                {
                    OnSecurityEvent(SecurityEventType.SecureDataCleared,
                        $"Secure data cleared: {dataId}");
                    _logger.LogDebug("Secure data cleared: {DataId}", dataId);
                }

                return success;
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;

                OnCleanupCompleted(new CleanupCompletedEventArgs(
                    CleanupOperationType.SingleItem,
                    false,
                    0,
                    duration,
                    ex.Message));

                OnSecurityEvent(SecurityEventType.SecureDataClearError,
                    $"Failed to clear secure data {dataId}: {ex.Message}");
                _logger.LogError(ex, "Error clearing secure data: {DataId}", dataId);
                return false;
            }
            finally
            {
                await Task.CompletedTask;
            }
        }

        /// <summary>
        /// 清理所有敏感数据和安全状态
        /// Clears all sensitive data and security state
        /// </summary>
        /// <returns>清理操作任务 Cleanup operation task</returns>
        public async Task<bool> ClearAllSecurityStateAsync()
        {
            var startTime = DateTime.UtcNow;
            var success = false;
            var itemsCleared = 0;

            try
            {
                lock (_lockObject)
                {
                    ThrowIfNotInitialized();

                    _logger.LogInformation("Starting complete security state cleanup");

                    // 清理安全存储中的所有数据
                    // Clear all data in secure storage
                    itemsCleared = _secureStorage!.ClearAllSecureData();

                    // 清理上下文中的敏感环境变量
                    // Clear sensitive environment variables in context
                    var envVarsCleaned = ClearSensitiveEnvironmentVariables();
                    itemsCleared += envVarsCleaned;

                    success = true;
                    _totalCleanupOperations++;
                    _lastCleanupTime = DateTime.UtcNow;
                }

                var duration = DateTime.UtcNow - startTime;

                OnCleanupCompleted(new CleanupCompletedEventArgs(
                    CleanupOperationType.AllSecurityState,
                    success,
                    itemsCleared,
                    duration));

                OnSecurityEvent(SecurityEventType.AllSecurityStateCleared,
                    $"All security state cleared, {itemsCleared} items");

                _logger.LogInformation("All security state cleared successfully, {ItemsCleared} items, duration: {Duration}ms",
                    itemsCleared, duration.TotalMilliseconds);

                // 强制垃圾回收
                // Force garbage collection
                ForceGarbageCollection();

                return success;
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;

                OnCleanupCompleted(new CleanupCompletedEventArgs(
                    CleanupOperationType.AllSecurityState,
                    false,
                    itemsCleared,
                    duration,
                    ex.Message));

                OnSecurityEvent(SecurityEventType.AllSecurityStateClearError,
                    $"Failed to clear all security state: {ex.Message}");
                _logger.LogError(ex, "Error clearing all security state");
                return false;
            }
            finally
            {
                await Task.CompletedTask;
            }
        }

        /// <summary>
        /// 强制执行垃圾回收和内存清理
        /// Forces garbage collection and memory cleanup
        /// </summary>
        public void ForceGarbageCollection()
        {
            lock (_lockObject)
            {
                ThrowIfNotInitialized();
                _secureStorage!.ForceGarbageCollection();

                OnSecurityEvent(SecurityEventType.MemoryCleanupCompleted,
                    "Forced garbage collection completed");
            }
        }

        /// <summary>
        /// 注册清理触发器
        /// Registers cleanup triggers
        /// </summary>
        /// <param name="triggers">清理触发条件 Cleanup trigger conditions</param>
        public void RegisterCleanupTriggers(CleanupTriggers triggers)
        {
            if (triggers == null)
                throw new ArgumentNullException(nameof(triggers));

            lock (_lockObject)
            {
                ThrowIfNotInitialized();

                _logger.LogDebug("Registering cleanup triggers");

                // 注册应用程序退出清理
                // Register application exit cleanup
                if (triggers.OnApplicationExit)
                {
                    AppDomain.CurrentDomain.ProcessExit += OnApplicationExit;
                    _logger.LogDebug("Registered ProcessExit cleanup trigger");
                }

                // 注册系统关机清理
                // Register system shutdown cleanup
                if (triggers.OnSystemShutdown)
                {
                    SystemEvents.SessionEnding += OnSystemShutdown;
                    _logger.LogDebug("Registered SessionEnding cleanup trigger");
                }

                // 注册内存压力清理（如果支持）
                // Register memory pressure cleanup (if supported)
                if (triggers.OnMemoryPressure)
                {
                    // 在实际实现中，这里可以注册内存压力事件
                    // In actual implementation, memory pressure events can be registered here
                    _logger.LogDebug("Memory pressure cleanup trigger registered");
                }

                _registeredTriggers[triggers] = true;

                OnSecurityEvent(SecurityEventType.CleanupTriggersRegistered,
                    "Cleanup triggers registered successfully");
            }
        }

        /// <summary>
        /// 验证安全状态完整性
        /// Validates security state integrity
        /// </summary>
        /// <returns>验证结果 Validation result</returns>
        public async Task<SecurityValidationResult> ValidateSecurityStateAsync()
        {
            lock (_lockObject)
            {
                ThrowIfNotInitialized();

                var result = new SecurityValidationResult();
                var messages = new List<string>();

                try
                {
                    // 验证安全存储状态
                    // Validate secure storage state
                    var dataIds = _secureStorage!.GetAllDataIds();
                    result.ValidatedItems = dataIds.Count;

                    // 检查过期数据
                    // Check for expired data
                    var expiredCount = 0;
                    foreach (var dataId in dataIds)
                    {
                        if (_secureStorage.ContainsSecureData(dataId))
                        {
                            // 这里可以添加更详细的验证逻辑
                            // More detailed validation logic can be added here
                        }
                        else
                        {
                            expiredCount++;
                        }
                    }

                    if (expiredCount > 0)
                    {
                        messages.Add($"Found {expiredCount} expired or invalid data items");
                        result.IssuesFound += expiredCount;
                    }

                    // 验证安全上下文
                    // Validate security context
                    if (_securityContext == null)
                    {
                        messages.Add("Security context is null");
                        result.IssuesFound++;
                    }
                    else
                    {
                        result.ValidatedItems++;

                        if (_securityContext.SecurityLevel < SecurityLevel.Medium)
                        {
                            messages.Add("Security level is below recommended minimum (Medium)");
                            result.IssuesFound++;
                        }
                    }

                    // 验证清理触发器
                    // Validate cleanup triggers
                    if (_registeredTriggers.Count == 0)
                    {
                        messages.Add("No cleanup triggers are registered");
                        result.IssuesFound++;
                    }

                    result.IsValid = result.IssuesFound == 0;
                    result.ValidationMessages = messages;

                    _logger.LogDebug("Security state validation completed: Valid={IsValid}, Items={ValidatedItems}, Issues={IssuesFound}",
                        result.IsValid, result.ValidatedItems, result.IssuesFound);

                    return result;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during security state validation");
                    result.IsValid = false;
                    result.IssuesFound++;
                    result.ValidationMessages.Add($"Validation error: {ex.Message}");
                    return result;
                }
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 获取安全状态摘要
        /// Gets security state summary
        /// </summary>
        /// <returns>安全状态摘要 Security state summary</returns>
        public SecurityStateSummary GetSecurityStateSummary()
        {
            lock (_lockObject)
            {
                ThrowIfNotInitialized();

                return new SecurityStateSummary
                {
                    SecureDataItemCount = _secureStorage!.Count,
                    ActiveCleanupTriggers = _registeredTriggers.Count,
                    LastCleanupTime = _lastCleanupTime,
                    TotalCleanupOperations = _totalCleanupOperations,
                    StartupTime = _startupTime,
                    HasMemoryLeakRisk = _secureStorage.Count > _securityContext!.SecurityPolicy.MaxConcurrentSecureDataItems * 0.8
                };
            }
        }

        /// <summary>
        /// 设置自动清理
        /// Sets up automatic cleanup
        /// </summary>
        private void SetupAutomaticCleanup()
        {
            if (_securityContext!.CleanupTriggers.OnTimeout &&
                _securityContext.CleanupTriggers.TimeoutDuration.HasValue)
            {
                var interval = _securityContext.CleanupTriggers.TimeoutDuration.Value;

                _cleanupTimer = new Timer(
                    async _ => await PerformAutomaticCleanupAsync(),
                    null,
                    interval,
                    interval);

                _logger.LogDebug("Automatic cleanup timer setup with interval: {Interval}", interval);
            }
        }

        /// <summary>
        /// 执行自动清理
        /// Performs automatic cleanup
        /// </summary>
        private async Task PerformAutomaticCleanupAsync()
        {
            try
            {
                _logger.LogDebug("Starting automatic cleanup");

                var success = await ClearAllSecurityStateAsync();

                OnCleanupCompleted(new CleanupCompletedEventArgs(
                    CleanupOperationType.AutomaticCleanup,
                    success,
                    _secureStorage?.Count ?? 0,
                    TimeSpan.Zero));

                _logger.LogDebug("Automatic cleanup completed: {Success}", success);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during automatic cleanup");

                OnCleanupCompleted(new CleanupCompletedEventArgs(
                    CleanupOperationType.AutomaticCleanup,
                    false,
                    0,
                    TimeSpan.Zero,
                    ex.Message));
            }
        }

        /// <summary>
        /// 清理敏感环境变量
        /// Clears sensitive environment variables
        /// </summary>
        /// <returns>清理的环境变量数量 Number of environment variables cleared</returns>
        private int ClearSensitiveEnvironmentVariables()
        {
            var sensitiveEnvVars = new[]
            {
                "ANTHROPIC_AUTH_TOKEN",
                "GITHUB_TOKEN",
                "API_KEY",
                "SECRET_KEY",
                "PASSWORD",
                "TOKEN"
            };

            var clearedCount = 0;

            foreach (var envVar in sensitiveEnvVars)
            {
                try
                {
                    // 清理系统环境变量
                    // Clear system environment variables
                    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(envVar)))
                    {
                        Environment.SetEnvironmentVariable(envVar, null);
                        clearedCount++;
                    }

                    // 清理上下文环境变量
                    // Clear context environment variables
                    if (_securityContext!.RemoveEnvironmentVariable(envVar))
                    {
                        clearedCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to clear environment variable: {EnvVar}", envVar);
                }
            }

            if (clearedCount > 0)
            {
                _logger.LogDebug("Cleared {Count} sensitive environment variables", clearedCount);
            }

            return clearedCount;
        }

        /// <summary>
        /// 应用程序退出事件处理
        /// Application exit event handler
        /// </summary>
        private async void OnApplicationExit(object? sender, EventArgs e)
        {
            try
            {
                _logger.LogInformation("Application exit detected, performing cleanup");
                await ClearAllSecurityStateAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during application exit cleanup");
            }
        }

        /// <summary>
        /// 系统关机事件处理
        /// System shutdown event handler
        /// </summary>
        private async void OnSystemShutdown(object? sender, EventArgs e)
        {
            try
            {
                _logger.LogInformation("System shutdown detected, performing cleanup");
                await ClearAllSecurityStateAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during system shutdown cleanup");
            }
        }

        /// <summary>
        /// 存储操作事件处理
        /// Storage operation event handler
        /// </summary>
        private void OnStorageOperation(object? sender, StorageOperationEventArgs e)
        {
            OnSecurityEvent(SecurityEventType.StorageOperation,
                $"Storage operation: {e.OperationType} - {e.Message}");
        }

        /// <summary>
        /// 安全数据过期事件处理
        /// Secure data expired event handler
        /// </summary>
        private void OnSecureDataExpired(object? sender, SecureDataExpiredEventArgs e)
        {
            OnSecurityEvent(SecurityEventType.SecureDataExpired,
                $"Secure data expired: {e.DataId} ({e.DataType})");
        }

        /// <summary>
        /// 触发安全事件
        /// Triggers security event
        /// </summary>
        private void OnSecurityEvent(SecurityEventType eventType, string message)
        {
            try
            {
                SecurityEvent?.Invoke(this, new SecurityEventArgs(eventType, message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error firing SecurityEvent");
            }
        }

        /// <summary>
        /// 触发清理完成事件
        /// Triggers cleanup completed event
        /// </summary>
        private void OnCleanupCompleted(CleanupCompletedEventArgs args)
        {
            try
            {
                CleanupCompleted?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error firing CleanupCompleted event");
            }
        }

        /// <summary>
        /// 检查是否已初始化并抛出异常
        /// Checks if initialized and throws exception
        /// </summary>
        private void ThrowIfNotInitialized()
        {
            ThrowIfDisposed();
            if (!_initialized)
                throw new InvalidOperationException("SecurityManager is not initialized");
        }

        /// <summary>
        /// 检查对象是否已释放并抛出异常
        /// Checks if object is disposed and throws exception
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SecurityManager));
        }

        /// <summary>
        /// 释放资源
        /// Disposes resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源的具体实现
        /// Actual implementation of resource disposal
        /// </summary>
        /// <param name="disposing">是否正在释放托管资源 Whether disposing managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                lock (_lockObject)
                {
                    if (!_disposed && disposing)
                    {
                        _logger.LogDebug("Disposing SecurityManager");

                        try
                        {
                            // 执行最终清理
                            // Perform final cleanup
                            if (_initialized)
                            {
                                ClearAllSecurityStateAsync().Wait(TimeSpan.FromSeconds(5));
                            }

                            // 停止定时器
                            // Stop timers
                            _cleanupTimer?.Dispose();

                            // 注销事件处理器
                            // Unregister event handlers
                            AppDomain.CurrentDomain.ProcessExit -= OnApplicationExit;
                            SystemEvents.SessionEnding -= OnSystemShutdown;

                            // 释放存储
                            // Dispose storage
                            _secureStorage?.Dispose();

                            // 释放上下文
                            // Dispose context
                            _securityContext?.Dispose();

                            _initialized = false;
                            _disposed = true;

                            _logger.LogInformation("SecurityManager disposed successfully");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error during SecurityManager disposal");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 析构函数确保资源被释放
        /// Finalizer ensures resources are released
        /// </summary>
        ~SecurityManager()
        {
            Dispose(false);
        }
    }

    /// <summary>
    /// 扩展的安全事件类型
    /// Extended security event types
    /// </summary>
    public static class SecurityEventTypeExtensions
    {
        public const SecurityEventType SecurityManagerInitialized = (SecurityEventType)100;
        public const SecurityEventType SecureDataStored = (SecurityEventType)101;
        public const SecurityEventType SecureDataRetrieved = (SecurityEventType)102;
        public const SecurityEventType SecureDataCleared = (SecurityEventType)103;
        public const SecurityEventType SecureDataExpired = (SecurityEventType)104;
        public const SecurityEventType SecureDataStorageError = (SecurityEventType)105;
        public const SecurityEventType SecureDataRetrievalError = (SecurityEventType)106;
        public const SecurityEventType SecureDataClearError = (SecurityEventType)107;
        public const SecurityEventType AllSecurityStateCleared = (SecurityEventType)108;
        public const SecurityEventType AllSecurityStateClearError = (SecurityEventType)109;
        public const SecurityEventType CleanupTriggersRegistered = (SecurityEventType)110;
        public const SecurityEventType MemoryCleanupCompleted = (SecurityEventType)111;
        public const SecurityEventType StorageOperation = (SecurityEventType)112;
    }

    /// <summary>
    /// 系统事件处理（简化版本）
    /// System events handling (simplified version)
    /// </summary>
    public static class SystemEvents
    {
        public static event EventHandler? SessionEnding;

        static SystemEvents()
        {
            // 在实际实现中，这里会注册真正的系统事件
            // In actual implementation, real system events would be registered here
        }
    }
}