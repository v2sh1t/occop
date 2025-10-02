using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Security;

namespace Occop.Core.Security
{
    /// <summary>
    /// 安全存储服务，提供SecureString敏感信息的安全存储和管理
    /// Secure storage service providing secure storage and management of SecureString sensitive information
    /// </summary>
    public class SecureStorage : IDisposable
    {
        private readonly ILogger<SecureStorage> _logger;
        private readonly SecurityContext _securityContext;
        private readonly object _lockObject = new object();
        private readonly ConcurrentDictionary<string, SecureData> _secureDataStore;
        private readonly Timer _cleanupTimer;
        private bool _disposed = false;

        private static readonly TimeSpan DefaultCleanupInterval = TimeSpan.FromMinutes(10);

        /// <summary>
        /// 存储的安全数据项数量
        /// Number of stored secure data items
        /// </summary>
        public int Count => _secureDataStore.Count;

        /// <summary>
        /// 是否已释放
        /// Whether disposed
        /// </summary>
        public bool IsDisposed => _disposed;

        /// <summary>
        /// 安全数据过期事件
        /// Event fired when secure data expires
        /// </summary>
        public event EventHandler<SecureDataExpiredEventArgs>? SecureDataExpired;

        /// <summary>
        /// 存储操作事件
        /// Event fired during storage operations
        /// </summary>
        public event EventHandler<StorageOperationEventArgs>? StorageOperation;

        /// <summary>
        /// 初始化SecureStorage实例
        /// Initializes SecureStorage instance
        /// </summary>
        /// <param name="securityContext">安全上下文 Security context</param>
        /// <param name="logger">日志记录器 Logger</param>
        public SecureStorage(
            SecurityContext securityContext,
            ILogger<SecureStorage> logger)
        {
            _securityContext = securityContext ?? throw new ArgumentNullException(nameof(securityContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _secureDataStore = new ConcurrentDictionary<string, SecureData>();

            // 设置自动清理定时器
            // Setup automatic cleanup timer
            var cleanupInterval = _securityContext.SecurityPolicy.AutoCleanupInterval;
            if (cleanupInterval <= TimeSpan.Zero)
                cleanupInterval = DefaultCleanupInterval;

            _cleanupTimer = new Timer(
                _ => PerformAutomaticCleanup(),
                null,
                cleanupInterval,
                cleanupInterval);

            _logger.LogDebug("SecureStorage initialized with cleanup interval: {Interval}", cleanupInterval);
        }

        /// <summary>
        /// 安全地存储敏感数据
        /// Securely stores sensitive data
        /// </summary>
        /// <param name="id">数据标识符 Data identifier</param>
        /// <param name="data">敏感数据 Sensitive data</param>
        /// <param name="dataType">数据类型 Data type</param>
        /// <param name="expiresAt">过期时间 Expiration time</param>
        /// <param name="metadata">元数据 Metadata</param>
        /// <returns>存储的安全数据对象 Stored secure data object</returns>
        /// <exception cref="ArgumentException">参数无效时抛出 Thrown when arguments are invalid</exception>
        /// <exception cref="InvalidOperationException">存储失败时抛出 Thrown when storage fails</exception>
        public SecureData StoreSecureData(
            string id,
            SecureString data,
            SecureDataType dataType = SecureDataType.ApiToken,
            DateTime? expiresAt = null,
            Dictionary<string, string>? metadata = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Data ID cannot be null or empty", nameof(id));

            if (data == null)
                throw new ArgumentNullException(nameof(data));

            lock (_lockObject)
            {
                ThrowIfDisposed();

                // 检查存储限制
                // Check storage limits
                if (_secureDataStore.Count >= _securityContext.SecurityPolicy.MaxConcurrentSecureDataItems)
                {
                    _logger.LogWarning("Maximum concurrent secure data items limit reached: {Limit}",
                        _securityContext.SecurityPolicy.MaxConcurrentSecureDataItems);
                    throw new InvalidOperationException($"Maximum concurrent secure data items limit reached: {_securityContext.SecurityPolicy.MaxConcurrentSecureDataItems}");
                }

                // 如果数据已存在，先释放旧数据
                // If data already exists, dispose old data first
                if (_secureDataStore.TryRemove(id, out SecureData? existingData))
                {
                    existingData.Dispose();
                    _logger.LogDebug("Replaced existing secure data with ID: {Id}", id);
                }

                // 设置默认过期时间
                // Set default expiration time
                if (!expiresAt.HasValue && _securityContext.SecurityPolicy.MaxSecureDataLifetime > TimeSpan.Zero)
                {
                    expiresAt = DateTime.UtcNow.Add(_securityContext.SecurityPolicy.MaxSecureDataLifetime);
                }

                // 创建并存储安全数据
                // Create and store secure data
                var secureData = new SecureData(id, data, dataType, expiresAt, metadata);

                if (!_secureDataStore.TryAdd(id, secureData))
                {
                    secureData.Dispose();
                    throw new InvalidOperationException($"Failed to store secure data with ID: {id}");
                }

                OnStorageOperation(new StorageOperationEventArgs(
                    StorageOperationType.Store,
                    id,
                    dataType,
                    true,
                    $"Secure data stored successfully"));

                _logger.LogInformation("Secure data stored with ID: {Id}, Type: {Type}, ExpiresAt: {ExpiresAt}",
                    id, dataType, expiresAt);

                return secureData;
            }
        }

        /// <summary>
        /// 安全地检索敏感数据
        /// Securely retrieves sensitive data
        /// </summary>
        /// <param name="id">数据标识符 Data identifier</param>
        /// <returns>敏感数据或null Sensitive data or null</returns>
        public SecureString? RetrieveSecureData(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            lock (_lockObject)
            {
                ThrowIfDisposed();

                if (!_secureDataStore.TryGetValue(id, out SecureData? secureData))
                {
                    OnStorageOperation(new StorageOperationEventArgs(
                        StorageOperationType.Retrieve,
                        id,
                        SecureDataType.Other,
                        false,
                        "Secure data not found"));

                    return null;
                }

                if (secureData.IsExpired)
                {
                    _logger.LogWarning("Attempted to retrieve expired secure data: {Id}", id);

                    // 移除过期数据
                    // Remove expired data
                    RemoveSecureData(id);

                    OnStorageOperation(new StorageOperationEventArgs(
                        StorageOperationType.Retrieve,
                        id,
                        secureData.DataType,
                        false,
                        "Secure data has expired"));

                    return null;
                }

                try
                {
                    var data = secureData.GetSecureData();

                    OnStorageOperation(new StorageOperationEventArgs(
                        StorageOperationType.Retrieve,
                        id,
                        secureData.DataType,
                        true,
                        "Secure data retrieved successfully"));

                    _logger.LogDebug("Secure data retrieved: {Id}, AccessCount: {AccessCount}",
                        id, secureData.AccessCount);

                    return data;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error retrieving secure data: {Id}", id);

                    OnStorageOperation(new StorageOperationEventArgs(
                        StorageOperationType.Retrieve,
                        id,
                        secureData.DataType,
                        false,
                        $"Error retrieving secure data: {ex.Message}"));

                    throw;
                }
            }
        }

        /// <summary>
        /// 使用敏感数据执行操作（推荐的安全访问方式）
        /// Uses sensitive data to perform operation (recommended secure access method)
        /// </summary>
        /// <param name="id">数据标识符 Data identifier</param>
        /// <param name="action">使用数据的操作 Action to use the data</param>
        /// <returns>操作是否成功执行 Whether operation was executed successfully</returns>
        public bool UseSecureData(string id, Action<string> action)
        {
            if (string.IsNullOrWhiteSpace(id) || action == null)
                return false;

            lock (_lockObject)
            {
                ThrowIfDisposed();

                if (!_secureDataStore.TryGetValue(id, out SecureData? secureData))
                    return false;

                if (secureData.IsExpired)
                {
                    RemoveSecureData(id);
                    return false;
                }

                try
                {
                    secureData.UseSecureString(action);

                    OnStorageOperation(new StorageOperationEventArgs(
                        StorageOperationType.Use,
                        id,
                        secureData.DataType,
                        true,
                        "Secure data used successfully"));

                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error using secure data: {Id}", id);

                    OnStorageOperation(new StorageOperationEventArgs(
                        StorageOperationType.Use,
                        id,
                        secureData.DataType,
                        false,
                        $"Error using secure data: {ex.Message}"));

                    return false;
                }
            }
        }

        /// <summary>
        /// 使用敏感数据执行操作并返回结果（推荐的安全访问方式）
        /// Uses sensitive data to perform operation and return result (recommended secure access method)
        /// </summary>
        /// <typeparam name="T">返回类型 Return type</typeparam>
        /// <param name="id">数据标识符 Data identifier</param>
        /// <param name="function">使用数据并返回结果的函数 Function to use data and return result</param>
        /// <returns>函数执行结果或默认值 Function execution result or default value</returns>
        public T? UseSecureData<T>(string id, Func<string, T> function)
        {
            if (string.IsNullOrWhiteSpace(id) || function == null)
                return default;

            lock (_lockObject)
            {
                ThrowIfDisposed();

                if (!_secureDataStore.TryGetValue(id, out SecureData? secureData))
                    return default;

                if (secureData.IsExpired)
                {
                    RemoveSecureData(id);
                    return default;
                }

                try
                {
                    var result = secureData.UseSecureString(function);

                    OnStorageOperation(new StorageOperationEventArgs(
                        StorageOperationType.Use,
                        id,
                        secureData.DataType,
                        true,
                        "Secure data used successfully"));

                    return result;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error using secure data: {Id}", id);

                    OnStorageOperation(new StorageOperationEventArgs(
                        StorageOperationType.Use,
                        id,
                        secureData.DataType,
                        false,
                        $"Error using secure data: {ex.Message}"));

                    return default;
                }
            }
        }

        /// <summary>
        /// 检查安全数据是否存在
        /// Checks if secure data exists
        /// </summary>
        /// <param name="id">数据标识符 Data identifier</param>
        /// <returns>是否存在 Whether exists</returns>
        public bool ContainsSecureData(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            lock (_lockObject)
            {
                ThrowIfDisposed();
                return _secureDataStore.ContainsKey(id);
            }
        }

        /// <summary>
        /// 移除安全数据
        /// Removes secure data
        /// </summary>
        /// <param name="id">数据标识符 Data identifier</param>
        /// <returns>是否移除成功 Whether removal was successful</returns>
        public bool RemoveSecureData(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            lock (_lockObject)
            {
                ThrowIfDisposed();

                if (_secureDataStore.TryRemove(id, out SecureData? secureData))
                {
                    secureData.Dispose();

                    OnStorageOperation(new StorageOperationEventArgs(
                        StorageOperationType.Remove,
                        id,
                        secureData.DataType,
                        true,
                        "Secure data removed successfully"));

                    _logger.LogDebug("Secure data removed: {Id}", id);
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// 清理所有安全数据
        /// Clears all secure data
        /// </summary>
        /// <returns>清理的项目数量 Number of items cleared</returns>
        public int ClearAllSecureData()
        {
            lock (_lockObject)
            {
                ThrowIfDisposed();

                var count = _secureDataStore.Count;
                var items = _secureDataStore.ToArray();

                _secureDataStore.Clear();

                // 释放所有数据
                // Dispose all data
                foreach (var kvp in items)
                {
                    try
                    {
                        kvp.Value.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error disposing secure data: {Id}", kvp.Key);
                    }
                }

                OnStorageOperation(new StorageOperationEventArgs(
                    StorageOperationType.ClearAll,
                    "ALL",
                    SecureDataType.Other,
                    true,
                    $"All secure data cleared, {count} items"));

                _logger.LogInformation("All secure data cleared, {Count} items", count);

                // 强制垃圾回收
                // Force garbage collection
                ForceGarbageCollection();

                return count;
            }
        }

        /// <summary>
        /// 获取所有安全数据的标识符
        /// Gets identifiers of all secure data
        /// </summary>
        /// <returns>标识符列表 List of identifiers</returns>
        public List<string> GetAllDataIds()
        {
            lock (_lockObject)
            {
                ThrowIfDisposed();
                return _secureDataStore.Keys.ToList();
            }
        }

        /// <summary>
        /// 获取指定类型的安全数据标识符
        /// Gets identifiers of secure data of specified type
        /// </summary>
        /// <param name="dataType">数据类型 Data type</param>
        /// <returns>标识符列表 List of identifiers</returns>
        public List<string> GetDataIdsByType(SecureDataType dataType)
        {
            lock (_lockObject)
            {
                ThrowIfDisposed();
                return _secureDataStore
                    .Where(kvp => kvp.Value.DataType == dataType)
                    .Select(kvp => kvp.Key)
                    .ToList();
            }
        }

        /// <summary>
        /// 强制垃圾回收和内存清理
        /// Forces garbage collection and memory cleanup
        /// </summary>
        public void ForceGarbageCollection()
        {
            try
            {
                // 强制垃圾回收三代
                // Force garbage collection for all generations
                for (int i = 0; i <= GC.MaxGeneration; i++)
                {
                    GC.Collect(i, GCCollectionMode.Forced, true);
                    GC.WaitForPendingFinalizers();
                }

                // 压缩大对象堆
                // Compact large object heap
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect();

                _logger.LogDebug("Forced garbage collection completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during forced garbage collection");
            }
        }

        /// <summary>
        /// 执行自动清理
        /// Performs automatic cleanup
        /// </summary>
        private void PerformAutomaticCleanup()
        {
            try
            {
                lock (_lockObject)
                {
                    if (_disposed)
                        return;

                    var expiredItems = _secureDataStore
                        .Where(kvp => kvp.Value.IsExpired)
                        .ToArray();

                    var cleanedCount = 0;
                    foreach (var kvp in expiredItems)
                    {
                        if (_secureDataStore.TryRemove(kvp.Key, out SecureData? secureData))
                        {
                            secureData.Dispose();
                            cleanedCount++;

                            OnSecureDataExpired(new SecureDataExpiredEventArgs(
                                kvp.Key,
                                secureData.DataType,
                                secureData.ExpiresAt!.Value));
                        }
                    }

                    if (cleanedCount > 0)
                    {
                        _logger.LogInformation("Automatic cleanup completed, {Count} expired items removed", cleanedCount);
                        ForceGarbageCollection();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during automatic cleanup");
            }
        }

        /// <summary>
        /// 触发存储操作事件
        /// Triggers storage operation event
        /// </summary>
        private void OnStorageOperation(StorageOperationEventArgs args)
        {
            try
            {
                StorageOperation?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error firing StorageOperation event");
            }
        }

        /// <summary>
        /// 触发安全数据过期事件
        /// Triggers secure data expired event
        /// </summary>
        private void OnSecureDataExpired(SecureDataExpiredEventArgs args)
        {
            try
            {
                SecureDataExpired?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error firing SecureDataExpired event");
            }
        }

        /// <summary>
        /// 检查对象是否已释放并抛出异常
        /// Checks if object is disposed and throws exception
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SecureStorage));
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
                        _logger.LogDebug("Disposing SecureStorage");

                        // 停止清理定时器
                        // Stop cleanup timer
                        _cleanupTimer?.Dispose();

                        // 清理所有安全数据
                        // Clear all secure data
                        ClearAllSecureData();

                        _disposed = true;
                    }
                }
            }
        }

        /// <summary>
        /// 析构函数确保资源被释放
        /// Finalizer ensures resources are released
        /// </summary>
        ~SecureStorage()
        {
            Dispose(false);
        }
    }

    /// <summary>
    /// 安全数据过期事件参数
    /// Secure data expired event arguments
    /// </summary>
    public class SecureDataExpiredEventArgs : EventArgs
    {
        /// <summary>
        /// 数据标识符
        /// Data identifier
        /// </summary>
        public string DataId { get; }

        /// <summary>
        /// 数据类型
        /// Data type
        /// </summary>
        public SecureDataType DataType { get; }

        /// <summary>
        /// 过期时间
        /// Expiration time
        /// </summary>
        public DateTime ExpiredAt { get; }

        /// <summary>
        /// 初始化安全数据过期事件参数
        /// Initializes secure data expired event arguments
        /// </summary>
        public SecureDataExpiredEventArgs(string dataId, SecureDataType dataType, DateTime expiredAt)
        {
            DataId = dataId;
            DataType = dataType;
            ExpiredAt = expiredAt;
        }
    }

    /// <summary>
    /// 存储操作事件参数
    /// Storage operation event arguments
    /// </summary>
    public class StorageOperationEventArgs : EventArgs
    {
        /// <summary>
        /// 操作类型
        /// Operation type
        /// </summary>
        public StorageOperationType OperationType { get; }

        /// <summary>
        /// 数据标识符
        /// Data identifier
        /// </summary>
        public string DataId { get; }

        /// <summary>
        /// 数据类型
        /// Data type
        /// </summary>
        public SecureDataType DataType { get; }

        /// <summary>
        /// 操作是否成功
        /// Whether operation was successful
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// 操作消息
        /// Operation message
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 操作时间戳
        /// Operation timestamp
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// 初始化存储操作事件参数
        /// Initializes storage operation event arguments
        /// </summary>
        public StorageOperationEventArgs(
            StorageOperationType operationType,
            string dataId,
            SecureDataType dataType,
            bool isSuccess,
            string message)
        {
            OperationType = operationType;
            DataId = dataId;
            DataType = dataType;
            IsSuccess = isSuccess;
            Message = message;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// 存储操作类型
    /// Storage operation types
    /// </summary>
    public enum StorageOperationType
    {
        /// <summary>
        /// 存储操作
        /// Store operation
        /// </summary>
        Store,

        /// <summary>
        /// 检索操作
        /// Retrieve operation
        /// </summary>
        Retrieve,

        /// <summary>
        /// 使用操作
        /// Use operation
        /// </summary>
        Use,

        /// <summary>
        /// 移除操作
        /// Remove operation
        /// </summary>
        Remove,

        /// <summary>
        /// 清理所有操作
        /// Clear all operation
        /// </summary>
        ClearAll
    }
}