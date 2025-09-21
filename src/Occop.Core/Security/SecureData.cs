using System.Security;
using System.Runtime.InteropServices;

namespace Occop.Core.Security
{
    /// <summary>
    /// 表示安全存储的敏感数据项
    /// Represents a securely stored sensitive data item
    /// </summary>
    public class SecureData : IDisposable
    {
        private bool _disposed = false;
        private SecureString? _data;
        private readonly object _lockObject = new object();

        /// <summary>
        /// 数据的唯一标识符
        /// Unique identifier for the data
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// 数据类型
        /// Data type
        /// </summary>
        public SecureDataType DataType { get; }

        /// <summary>
        /// 数据创建时间
        /// Data creation time
        /// </summary>
        public DateTime CreatedAt { get; }

        /// <summary>
        /// 数据最后访问时间
        /// Data last accessed time
        /// </summary>
        public DateTime LastAccessedAt { get; private set; }

        /// <summary>
        /// 数据过期时间（可选）
        /// Data expiration time (optional)
        /// </summary>
        public DateTime? ExpiresAt { get; }

        /// <summary>
        /// 数据是否已过期
        /// Whether the data has expired
        /// </summary>
        public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;

        /// <summary>
        /// 数据是否已被释放
        /// Whether the data has been disposed
        /// </summary>
        public bool IsDisposed => _disposed;

        /// <summary>
        /// 数据访问次数
        /// Number of times data has been accessed
        /// </summary>
        public int AccessCount { get; private set; }

        /// <summary>
        /// 数据元数据（非敏感信息）
        /// Data metadata (non-sensitive information)
        /// </summary>
        public Dictionary<string, string> Metadata { get; }

        /// <summary>
        /// 初始化SecureData实例
        /// Initializes SecureData instance
        /// </summary>
        /// <param name="id">数据标识符 Data identifier</param>
        /// <param name="data">敏感数据 Sensitive data</param>
        /// <param name="dataType">数据类型 Data type</param>
        /// <param name="expiresAt">过期时间 Expiration time</param>
        /// <param name="metadata">元数据 Metadata</param>
        public SecureData(
            string id,
            SecureString data,
            SecureDataType dataType = SecureDataType.ApiToken,
            DateTime? expiresAt = null,
            Dictionary<string, string>? metadata = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Data ID cannot be null or empty", nameof(id));

            Id = id;
            _data = data?.Copy() ?? throw new ArgumentNullException(nameof(data));
            DataType = dataType;
            CreatedAt = DateTime.UtcNow;
            LastAccessedAt = CreatedAt;
            ExpiresAt = expiresAt;
            Metadata = metadata ?? new Dictionary<string, string>();
            AccessCount = 0;

            // 如果原始SecureString是只读的，设置我们的副本为只读
            // If original SecureString is read-only, make our copy read-only too
            if (data.IsReadOnly())
            {
                _data.MakeReadOnly();
            }
        }

        /// <summary>
        /// 安全地访问敏感数据
        /// Securely accesses sensitive data
        /// </summary>
        /// <returns>敏感数据的副本 Copy of sensitive data</returns>
        /// <exception cref="ObjectDisposedException">对象已释放时抛出 Thrown when object is disposed</exception>
        /// <exception cref="InvalidOperationException">数据已过期时抛出 Thrown when data is expired</exception>
        public SecureString GetSecureData()
        {
            lock (_lockObject)
            {
                ThrowIfDisposed();

                if (IsExpired)
                    throw new InvalidOperationException($"Secure data '{Id}' has expired");

                // 更新访问统计
                // Update access statistics
                LastAccessedAt = DateTime.UtcNow;
                AccessCount++;

                // 返回数据的副本以防止外部修改
                // Return copy of data to prevent external modification
                return _data!.Copy();
            }
        }

        /// <summary>
        /// 安全地将SecureString转换为普通字符串用于一次性使用
        /// Securely converts SecureString to regular string for one-time use
        /// </summary>
        /// <param name="action">处理字符串的操作 Action to process the string</param>
        /// <exception cref="ObjectDisposedException">对象已释放时抛出 Thrown when object is disposed</exception>
        /// <exception cref="InvalidOperationException">数据已过期时抛出 Thrown when data is expired</exception>
        public void UseSecureString(Action<string> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            lock (_lockObject)
            {
                ThrowIfDisposed();

                if (IsExpired)
                    throw new InvalidOperationException($"Secure data '{Id}' has expired");

                IntPtr ptr = IntPtr.Zero;
                try
                {
                    // 更新访问统计
                    // Update access statistics
                    LastAccessedAt = DateTime.UtcNow;
                    AccessCount++;

                    // 安全地转换为字符串并立即使用
                    // Securely convert to string and use immediately
                    ptr = Marshal.SecureStringToGlobalAllocUnicode(_data!);
                    string plainText = Marshal.PtrToStringUni(ptr) ?? string.Empty;

                    action(plainText);
                }
                finally
                {
                    // 确保内存被清零
                    // Ensure memory is zeroed
                    if (ptr != IntPtr.Zero)
                    {
                        Marshal.ZeroFreeGlobalAllocUnicode(ptr);
                    }
                }
            }
        }

        /// <summary>
        /// 安全地将SecureString转换为普通字符串并返回结果
        /// Securely converts SecureString to regular string and returns result
        /// </summary>
        /// <typeparam name="T">返回类型 Return type</typeparam>
        /// <param name="function">处理字符串并返回结果的函数 Function to process string and return result</param>
        /// <returns>函数执行结果 Function execution result</returns>
        /// <exception cref="ObjectDisposedException">对象已释放时抛出 Thrown when object is disposed</exception>
        /// <exception cref="InvalidOperationException">数据已过期时抛出 Thrown when data is expired</exception>
        public T UseSecureString<T>(Func<string, T> function)
        {
            if (function == null)
                throw new ArgumentNullException(nameof(function));

            lock (_lockObject)
            {
                ThrowIfDisposed();

                if (IsExpired)
                    throw new InvalidOperationException($"Secure data '{Id}' has expired");

                IntPtr ptr = IntPtr.Zero;
                try
                {
                    // 更新访问统计
                    // Update access statistics
                    LastAccessedAt = DateTime.UtcNow;
                    AccessCount++;

                    // 安全地转换为字符串并立即使用
                    // Securely convert to string and use immediately
                    ptr = Marshal.SecureStringToGlobalAllocUnicode(_data!);
                    string plainText = Marshal.PtrToStringUni(ptr) ?? string.Empty;

                    return function(plainText);
                }
                finally
                {
                    // 确保内存被清零
                    // Ensure memory is zeroed
                    if (ptr != IntPtr.Zero)
                    {
                        Marshal.ZeroFreeGlobalAllocUnicode(ptr);
                    }
                }
            }
        }

        /// <summary>
        /// 检查数据是否即将过期
        /// Checks if data is about to expire
        /// </summary>
        /// <param name="threshold">提前警告阈值 Early warning threshold</param>
        /// <returns>是否即将过期 Whether expiration is imminent</returns>
        public bool WillExpireWithin(TimeSpan threshold)
        {
            if (!ExpiresAt.HasValue)
                return false;

            return DateTime.UtcNow.Add(threshold) >= ExpiresAt.Value;
        }

        /// <summary>
        /// 更新元数据
        /// Updates metadata
        /// </summary>
        /// <param name="key">元数据键 Metadata key</param>
        /// <param name="value">元数据值 Metadata value</param>
        public void UpdateMetadata(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Metadata key cannot be null or empty", nameof(key));

            lock (_lockObject)
            {
                ThrowIfDisposed();
                Metadata[key] = value ?? string.Empty;
            }
        }

        /// <summary>
        /// 获取元数据
        /// Gets metadata
        /// </summary>
        /// <param name="key">元数据键 Metadata key</param>
        /// <returns>元数据值或null Metadata value or null</returns>
        public string? GetMetadata(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            lock (_lockObject)
            {
                ThrowIfDisposed();
                return Metadata.TryGetValue(key, out string? value) ? value : null;
            }
        }

        /// <summary>
        /// 检查对象是否已释放并抛出异常
        /// Checks if object is disposed and throws exception
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SecureData));
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
                        // 安全清理敏感数据
                        // Securely clear sensitive data
                        _data?.Dispose();
                        _data = null;

                        // 清理元数据
                        // Clear metadata
                        Metadata.Clear();

                        _disposed = true;
                    }
                }
            }
        }

        /// <summary>
        /// 析构函数确保资源被释放
        /// Finalizer ensures resources are released
        /// </summary>
        ~SecureData()
        {
            Dispose(false);
        }
    }

    /// <summary>
    /// 安全数据类型枚举
    /// Secure data type enumeration
    /// </summary>
    public enum SecureDataType
    {
        /// <summary>
        /// API令牌
        /// API Token
        /// </summary>
        ApiToken,

        /// <summary>
        /// 访问令牌
        /// Access Token
        /// </summary>
        AccessToken,

        /// <summary>
        /// 刷新令牌
        /// Refresh Token
        /// </summary>
        RefreshToken,

        /// <summary>
        /// 密码
        /// Password
        /// </summary>
        Password,

        /// <summary>
        /// 私钥
        /// Private Key
        /// </summary>
        PrivateKey,

        /// <summary>
        /// 证书
        /// Certificate
        /// </summary>
        Certificate,

        /// <summary>
        /// 其他敏感信息
        /// Other Sensitive Information
        /// </summary>
        Other
    }
}