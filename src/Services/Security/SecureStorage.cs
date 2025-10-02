using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace Occop.Services.Security
{
    /// <summary>
    /// 安全存储项
    /// </summary>
    public class SecureStorageItem : IDisposable
    {
        /// <summary>
        /// 安全字符串值
        /// </summary>
        public SecureString Value { get; private set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; }

        /// <summary>
        /// 最后访问时间
        /// </summary>
        public DateTime LastAccessedAt { get; private set; }

        /// <summary>
        /// 是否已被释放
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// 初始化安全存储项
        /// </summary>
        /// <param name="value">安全字符串值</param>
        public SecureStorageItem(SecureString value)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
            CreatedAt = DateTime.UtcNow;
            LastAccessedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// 更新最后访问时间
        /// </summary>
        internal void UpdateLastAccessed()
        {
            LastAccessedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (!IsDisposed)
            {
                Value?.Dispose();
                IsDisposed = true;
            }
        }
    }

    /// <summary>
    /// 内存清理操作类型
    /// </summary>
    public enum MemoryCleanupType
    {
        /// <summary>
        /// 立即清理
        /// </summary>
        Immediate,

        /// <summary>
        /// 延迟清理
        /// </summary>
        Delayed,

        /// <summary>
        /// 强制清理
        /// </summary>
        Forced
    }

    /// <summary>
    /// 内存清理结果
    /// </summary>
    public class MemoryCleanupResult
    {
        /// <summary>
        /// 清理是否成功
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// 清理的项数量
        /// </summary>
        public int ClearedItemsCount { get; }

        /// <summary>
        /// 清理类型
        /// </summary>
        public MemoryCleanupType CleanupType { get; }

        /// <summary>
        /// 清理时间戳
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// 清理消息
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 异常信息（如果有）
        /// </summary>
        public Exception? Exception { get; }

        /// <summary>
        /// 初始化内存清理结果
        /// </summary>
        /// <param name="isSuccess">是否成功</param>
        /// <param name="clearedItemsCount">清理的项数量</param>
        /// <param name="cleanupType">清理类型</param>
        /// <param name="message">清理消息</param>
        /// <param name="exception">异常信息</param>
        public MemoryCleanupResult(bool isSuccess, int clearedItemsCount, MemoryCleanupType cleanupType,
            string message, Exception? exception = null)
        {
            IsSuccess = isSuccess;
            ClearedItemsCount = clearedItemsCount;
            CleanupType = cleanupType;
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Exception = exception;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// 安全存储管理器
    /// 专门用于管理敏感信息的存储和清理
    /// </summary>
    public class SecureStorage : IDisposable
    {
        private readonly Dictionary<string, SecureStorageItem> _storage;
        private readonly object _lockObject;
        private bool _disposed;

        /// <summary>
        /// 存储项数量
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lockObject)
                {
                    return _storage.Count;
                }
            }
        }

        /// <summary>
        /// 是否已被释放
        /// </summary>
        public bool IsDisposed => _disposed;

        /// <summary>
        /// 所有存储的键
        /// </summary>
        public IEnumerable<string> Keys
        {
            get
            {
                lock (_lockObject)
                {
                    return new List<string>(_storage.Keys);
                }
            }
        }

        /// <summary>
        /// 内存清理事件
        /// </summary>
        public event EventHandler<MemoryCleanupResult>? MemoryCleared;

        /// <summary>
        /// 初始化安全存储
        /// </summary>
        public SecureStorage()
        {
            _storage = new Dictionary<string, SecureStorageItem>();
            _lockObject = new object();
        }

        /// <summary>
        /// 存储安全字符串
        /// </summary>
        /// <param name="key">存储键</param>
        /// <param name="value">安全字符串值</param>
        /// <exception cref="ArgumentNullException">键或值为空时抛出</exception>
        /// <exception cref="ObjectDisposedException">对象已释放时抛出</exception>
        public void Store(string key, SecureString value)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            lock (_lockObject)
            {
                // 如果已经存在，先清理旧值
                if (_storage.TryGetValue(key, out var existingItem))
                {
                    existingItem.Dispose();
                }

                _storage[key] = new SecureStorageItem(value);
            }
        }

        /// <summary>
        /// 存储普通字符串（将其转换为安全字符串）
        /// </summary>
        /// <param name="key">存储键</param>
        /// <param name="value">字符串值</param>
        /// <exception cref="ArgumentNullException">键或值为空时抛出</exception>
        /// <exception cref="ObjectDisposedException">对象已释放时抛出</exception>
        public void Store(string key, string value)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            var secureString = CreateSecureString(value);
            Store(key, secureString);
        }

        /// <summary>
        /// 获取安全字符串
        /// </summary>
        /// <param name="key">存储键</param>
        /// <returns>安全字符串，如果不存在返回null</returns>
        /// <exception cref="ArgumentNullException">键为空时抛出</exception>
        /// <exception cref="ObjectDisposedException">对象已释放时抛出</exception>
        public SecureString? GetSecureString(string key)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            lock (_lockObject)
            {
                if (_storage.TryGetValue(key, out var item))
                {
                    item.UpdateLastAccessed();
                    return item.Value;
                }
            }

            return null;
        }

        /// <summary>
        /// 获取普通字符串（临时解密）
        /// 注意：此方法会临时将SecureString转换为普通字符串，使用后应立即清理
        /// </summary>
        /// <param name="key">存储键</param>
        /// <returns>字符串值，如果不存在返回null</returns>
        /// <exception cref="ArgumentNullException">键为空时抛出</exception>
        /// <exception cref="ObjectDisposedException">对象已释放时抛出</exception>
        public string? GetString(string key)
        {
            ThrowIfDisposed();

            var secureString = GetSecureString(key);
            if (secureString == null)
                return null;

            return ConvertSecureStringToString(secureString);
        }

        /// <summary>
        /// 检查键是否存在
        /// </summary>
        /// <param name="key">存储键</param>
        /// <returns>是否存在</returns>
        /// <exception cref="ArgumentNullException">键为空时抛出</exception>
        /// <exception cref="ObjectDisposedException">对象已释放时抛出</exception>
        public bool ContainsKey(string key)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            lock (_lockObject)
            {
                return _storage.ContainsKey(key);
            }
        }

        /// <summary>
        /// 移除存储项
        /// </summary>
        /// <param name="key">存储键</param>
        /// <returns>是否成功移除</returns>
        /// <exception cref="ArgumentNullException">键为空时抛出</exception>
        /// <exception cref="ObjectDisposedException">对象已释放时抛出</exception>
        public bool Remove(string key)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            lock (_lockObject)
            {
                if (_storage.TryGetValue(key, out var item))
                {
                    item.Dispose();
                    return _storage.Remove(key);
                }
            }

            return false;
        }

        /// <summary>
        /// 清理所有存储项
        /// </summary>
        /// <param name="cleanupType">清理类型</param>
        /// <returns>清理结果</returns>
        public MemoryCleanupResult ClearAll(MemoryCleanupType cleanupType = MemoryCleanupType.Immediate)
        {
            try
            {
                int clearedCount;

                lock (_lockObject)
                {
                    clearedCount = _storage.Count;

                    foreach (var item in _storage.Values)
                    {
                        item.Dispose();
                    }

                    _storage.Clear();
                }

                // 强制垃圾回收以确保内存释放
                if (cleanupType == MemoryCleanupType.Forced)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }

                var result = new MemoryCleanupResult(true, clearedCount, cleanupType,
                    $"Successfully cleared {clearedCount} secure storage items");

                MemoryCleared?.Invoke(this, result);
                return result;
            }
            catch (Exception ex)
            {
                var result = new MemoryCleanupResult(false, 0, cleanupType,
                    "Failed to clear secure storage", ex);

                MemoryCleared?.Invoke(this, result);
                return result;
            }
        }

        /// <summary>
        /// 获取存储统计信息
        /// </summary>
        /// <returns>统计信息</returns>
        public Dictionary<string, object> GetStatistics()
        {
            ThrowIfDisposed();

            lock (_lockObject)
            {
                var stats = new Dictionary<string, object>
                {
                    { \"ItemCount\", _storage.Count },
                    { \"IsDisposed\", _disposed },
                    { \"Timestamp\", DateTime.UtcNow }
                };

                if (_storage.Count > 0)
                {
                    var oldestItem = DateTime.MaxValue;
                    var newestItem = DateTime.MinValue;
                    var mostRecentAccess = DateTime.MinValue;

                    foreach (var item in _storage.Values)
                    {
                        if (item.CreatedAt < oldestItem)
                            oldestItem = item.CreatedAt;
                        if (item.CreatedAt > newestItem)
                            newestItem = item.CreatedAt;
                        if (item.LastAccessedAt > mostRecentAccess)
                            mostRecentAccess = item.LastAccessedAt;
                    }

                    stats[\"OldestItemCreatedAt\"] = oldestItem;
                    stats[\"NewestItemCreatedAt\"] = newestItem;
                    stats[\"MostRecentAccessAt\"] = mostRecentAccess;
                }

                return stats;
            }
        }

        /// <summary>
        /// 创建安全字符串
        /// </summary>
        /// <param name="value">普通字符串</param>
        /// <returns>安全字符串</returns>
        private static SecureString CreateSecureString(string value)
        {
            var secureString = new SecureString();

            foreach (var c in value)
            {
                secureString.AppendChar(c);
            }

            secureString.MakeReadOnly();
            return secureString;
        }

        /// <summary>
        /// 将安全字符串转换为普通字符串
        /// 注意：此方法存在安全风险，应谨慎使用
        /// </summary>
        /// <param name="secureString">安全字符串</param>
        /// <returns>普通字符串</returns>
        private static string ConvertSecureStringToString(SecureString secureString)
        {
            IntPtr unmanagedString = IntPtr.Zero;
            try
            {
                unmanagedString = Marshal.SecureStringToGlobalAllocUnicode(secureString);
                return Marshal.PtrToStringUni(unmanagedString) ?? string.Empty;
            }
            finally
            {
                if (unmanagedString != IntPtr.Zero)
                {
                    Marshal.ZeroFreeGlobalAllocUnicode(unmanagedString);
                }
            }
        }

        /// <summary>
        /// 检查对象是否已释放
        /// </summary>
        /// <exception cref=\"ObjectDisposedException\">对象已释放时抛出</exception>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SecureStorage));
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                ClearAll(MemoryCleanupType.Forced);
                _disposed = true;
            }
        }

        /// <summary>
        /// 析构函数，确保资源得到释放
        /// </summary>
        ~SecureStorage()
        {
            Dispose();
        }
    }
}