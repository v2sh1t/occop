namespace Occop.Core.Security
{
    /// <summary>
    /// 安全上下文管理器，维护当前安全环境和配置
    /// Security context manager that maintains current security environment and configuration
    /// </summary>
    public class SecurityContext : IDisposable
    {
        private bool _disposed = false;
        private readonly object _lockObject = new object();

        /// <summary>
        /// 应用程序标识符
        /// Application identifier
        /// </summary>
        public string ApplicationId { get; }

        /// <summary>
        /// 安全级别
        /// Security level
        /// </summary>
        public SecurityLevel SecurityLevel { get; set; }

        /// <summary>
        /// 上下文创建时间
        /// Context creation time
        /// </summary>
        public DateTime CreatedAt { get; }

        /// <summary>
        /// 上下文最后更新时间
        /// Context last updated time
        /// </summary>
        public DateTime LastUpdatedAt { get; private set; }

        /// <summary>
        /// 是否启用自动清理
        /// Whether automatic cleanup is enabled
        /// </summary>
        public bool IsAutoCleanupEnabled { get; set; }

        /// <summary>
        /// 是否启用内存保护
        /// Whether memory protection is enabled
        /// </summary>
        public bool IsMemoryProtectionEnabled { get; set; }

        /// <summary>
        /// 是否启用审计日志
        /// Whether audit logging is enabled
        /// </summary>
        public bool IsAuditLoggingEnabled { get; set; }

        /// <summary>
        /// 清理触发器配置
        /// Cleanup trigger configuration
        /// </summary>
        public CleanupTriggers CleanupTriggers { get; set; }

        /// <summary>
        /// 安全策略设置
        /// Security policy settings
        /// </summary>
        public SecurityPolicy SecurityPolicy { get; set; }

        /// <summary>
        /// 上下文标签（用于分类和过滤）
        /// Context tags (for categorization and filtering)
        /// </summary>
        public HashSet<string> Tags { get; }

        /// <summary>
        /// 自定义属性
        /// Custom properties
        /// </summary>
        public Dictionary<string, object> Properties { get; }

        /// <summary>
        /// 环境变量映射（敏感环境变量的安全存储）
        /// Environment variables mapping (secure storage for sensitive environment variables)
        /// </summary>
        public Dictionary<string, string> EnvironmentVariables { get; }

        /// <summary>
        /// 当前用户信息
        /// Current user information
        /// </summary>
        public SecurityUser? CurrentUser { get; set; }

        /// <summary>
        /// 会话标识符
        /// Session identifier
        /// </summary>
        public string SessionId { get; }

        /// <summary>
        /// 是否处于调试模式
        /// Whether in debug mode
        /// </summary>
        public bool IsDebugMode { get; set; }

        /// <summary>
        /// 初始化安全上下文
        /// Initializes security context
        /// </summary>
        /// <param name="applicationId">应用程序标识符 Application identifier</param>
        /// <param name="securityLevel">安全级别 Security level</param>
        public SecurityContext(
            string applicationId,
            SecurityLevel securityLevel = SecurityLevel.High)
        {
            if (string.IsNullOrWhiteSpace(applicationId))
                throw new ArgumentException("Application ID cannot be null or empty", nameof(applicationId));

            ApplicationId = applicationId;
            SecurityLevel = securityLevel;
            CreatedAt = DateTime.UtcNow;
            LastUpdatedAt = CreatedAt;
            SessionId = Guid.NewGuid().ToString("N");

            // 初始化默认配置
            // Initialize default configuration
            IsAutoCleanupEnabled = true;
            IsMemoryProtectionEnabled = true;
            IsAuditLoggingEnabled = securityLevel >= SecurityLevel.High;
            IsDebugMode = false;

            CleanupTriggers = new CleanupTriggers();
            SecurityPolicy = new SecurityPolicy();
            Tags = new HashSet<string>();
            Properties = new Dictionary<string, object>();
            EnvironmentVariables = new Dictionary<string, string>();

            // 根据安全级别设置默认策略
            // Set default policies based on security level
            ConfigureSecurityPolicyForLevel(securityLevel);
        }

        /// <summary>
        /// 更新上下文的最后更新时间
        /// Updates the context's last updated time
        /// </summary>
        public void Touch()
        {
            lock (_lockObject)
            {
                ThrowIfDisposed();
                LastUpdatedAt = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// 添加标签
        /// Adds a tag
        /// </summary>
        /// <param name="tag">标签 Tag</param>
        public void AddTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return;

            lock (_lockObject)
            {
                ThrowIfDisposed();
                Tags.Add(tag.Trim().ToLowerInvariant());
                Touch();
            }
        }

        /// <summary>
        /// 移除标签
        /// Removes a tag
        /// </summary>
        /// <param name="tag">标签 Tag</param>
        public bool RemoveTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return false;

            lock (_lockObject)
            {
                ThrowIfDisposed();
                bool removed = Tags.Remove(tag.Trim().ToLowerInvariant());
                if (removed)
                    Touch();
                return removed;
            }
        }

        /// <summary>
        /// 检查是否包含标签
        /// Checks if contains tag
        /// </summary>
        /// <param name="tag">标签 Tag</param>
        /// <returns>是否包含 Whether contains</returns>
        public bool HasTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return false;

            lock (_lockObject)
            {
                ThrowIfDisposed();
                return Tags.Contains(tag.Trim().ToLowerInvariant());
            }
        }

        /// <summary>
        /// 设置属性
        /// Sets a property
        /// </summary>
        /// <param name="key">属性键 Property key</param>
        /// <param name="value">属性值 Property value</param>
        public void SetProperty(string key, object value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Property key cannot be null or empty", nameof(key));

            lock (_lockObject)
            {
                ThrowIfDisposed();
                Properties[key] = value;
                Touch();
            }
        }

        /// <summary>
        /// 获取属性
        /// Gets a property
        /// </summary>
        /// <typeparam name="T">属性类型 Property type</typeparam>
        /// <param name="key">属性键 Property key</param>
        /// <param name="defaultValue">默认值 Default value</param>
        /// <returns>属性值 Property value</returns>
        public T GetProperty<T>(string key, T defaultValue = default!)
        {
            if (string.IsNullOrWhiteSpace(key))
                return defaultValue;

            lock (_lockObject)
            {
                ThrowIfDisposed();
                if (Properties.TryGetValue(key, out object? value) && value is T typedValue)
                    return typedValue;
                return defaultValue;
            }
        }

        /// <summary>
        /// 设置环境变量
        /// Sets an environment variable
        /// </summary>
        /// <param name="name">变量名 Variable name</param>
        /// <param name="value">变量值 Variable value</param>
        public void SetEnvironmentVariable(string name, string value)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Environment variable name cannot be null or empty", nameof(name));

            lock (_lockObject)
            {
                ThrowIfDisposed();
                EnvironmentVariables[name] = value ?? string.Empty;
                Touch();
            }
        }

        /// <summary>
        /// 获取环境变量
        /// Gets an environment variable
        /// </summary>
        /// <param name="name">变量名 Variable name</param>
        /// <returns>变量值或null Variable value or null</returns>
        public string? GetEnvironmentVariable(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            lock (_lockObject)
            {
                ThrowIfDisposed();
                return EnvironmentVariables.TryGetValue(name, out string? value) ? value : null;
            }
        }

        /// <summary>
        /// 移除环境变量
        /// Removes an environment variable
        /// </summary>
        /// <param name="name">变量名 Variable name</param>
        /// <returns>是否移除成功 Whether removal was successful</returns>
        public bool RemoveEnvironmentVariable(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            lock (_lockObject)
            {
                ThrowIfDisposed();
                bool removed = EnvironmentVariables.Remove(name);
                if (removed)
                    Touch();
                return removed;
            }
        }

        /// <summary>
        /// 克隆安全上下文
        /// Clones the security context
        /// </summary>
        /// <returns>克隆的上下文 Cloned context</returns>
        public SecurityContext Clone()
        {
            lock (_lockObject)
            {
                ThrowIfDisposed();

                var cloned = new SecurityContext(ApplicationId, SecurityLevel)
                {
                    IsAutoCleanupEnabled = IsAutoCleanupEnabled,
                    IsMemoryProtectionEnabled = IsMemoryProtectionEnabled,
                    IsAuditLoggingEnabled = IsAuditLoggingEnabled,
                    IsDebugMode = IsDebugMode,
                    CurrentUser = CurrentUser?.Clone(),
                    CleanupTriggers = CleanupTriggers.Clone(),
                    SecurityPolicy = SecurityPolicy.Clone()
                };

                // 复制集合
                // Copy collections
                foreach (var tag in Tags)
                    cloned.Tags.Add(tag);

                foreach (var prop in Properties)
                    cloned.Properties[prop.Key] = prop.Value;

                foreach (var env in EnvironmentVariables)
                    cloned.EnvironmentVariables[env.Key] = env.Value;

                return cloned;
            }
        }

        /// <summary>
        /// 根据安全级别配置安全策略
        /// Configures security policy based on security level
        /// </summary>
        /// <param name="level">安全级别 Security level</param>
        private void ConfigureSecurityPolicyForLevel(SecurityLevel level)
        {
            switch (level)
            {
                case SecurityLevel.Low:
                    SecurityPolicy.RequireMemoryEncryption = false;
                    SecurityPolicy.MaxSecureDataLifetime = TimeSpan.FromHours(24);
                    SecurityPolicy.RequireAuditTrail = false;
                    CleanupTriggers.OnTimeout = false;
                    break;

                case SecurityLevel.Medium:
                    SecurityPolicy.RequireMemoryEncryption = true;
                    SecurityPolicy.MaxSecureDataLifetime = TimeSpan.FromHours(8);
                    SecurityPolicy.RequireAuditTrail = true;
                    CleanupTriggers.OnTimeout = true;
                    CleanupTriggers.TimeoutDuration = TimeSpan.FromHours(4);
                    break;

                case SecurityLevel.High:
                    SecurityPolicy.RequireMemoryEncryption = true;
                    SecurityPolicy.MaxSecureDataLifetime = TimeSpan.FromHours(2);
                    SecurityPolicy.RequireAuditTrail = true;
                    CleanupTriggers.OnTimeout = true;
                    CleanupTriggers.TimeoutDuration = TimeSpan.FromHours(1);
                    CleanupTriggers.OnMemoryPressure = true;
                    break;

                case SecurityLevel.Critical:
                    SecurityPolicy.RequireMemoryEncryption = true;
                    SecurityPolicy.MaxSecureDataLifetime = TimeSpan.FromMinutes(30);
                    SecurityPolicy.RequireAuditTrail = true;
                    CleanupTriggers.OnTimeout = true;
                    CleanupTriggers.TimeoutDuration = TimeSpan.FromMinutes(15);
                    CleanupTriggers.OnMemoryPressure = true;
                    break;
            }
        }

        /// <summary>
        /// 检查对象是否已释放并抛出异常
        /// Checks if object is disposed and throws exception
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SecurityContext));
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
                        // 清理敏感信息
                        // Clear sensitive information
                        EnvironmentVariables.Clear();
                        Properties.Clear();
                        Tags.Clear();

                        CurrentUser = null;
                        _disposed = true;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 安全级别枚举
    /// Security level enumeration
    /// </summary>
    public enum SecurityLevel
    {
        /// <summary>
        /// 低安全级别
        /// Low security level
        /// </summary>
        Low = 1,

        /// <summary>
        /// 中等安全级别
        /// Medium security level
        /// </summary>
        Medium = 2,

        /// <summary>
        /// 高安全级别
        /// High security level
        /// </summary>
        High = 3,

        /// <summary>
        /// 关键安全级别
        /// Critical security level
        /// </summary>
        Critical = 4
    }

    /// <summary>
    /// 安全策略配置
    /// Security policy configuration
    /// </summary>
    public class SecurityPolicy
    {
        /// <summary>
        /// 是否要求内存加密
        /// Whether memory encryption is required
        /// </summary>
        public bool RequireMemoryEncryption { get; set; } = true;

        /// <summary>
        /// 敏感数据最大生命周期
        /// Maximum secure data lifetime
        /// </summary>
        public TimeSpan MaxSecureDataLifetime { get; set; } = TimeSpan.FromHours(2);

        /// <summary>
        /// 是否要求审计跟踪
        /// Whether audit trail is required
        /// </summary>
        public bool RequireAuditTrail { get; set; } = true;

        /// <summary>
        /// 最大并发敏感数据项数量
        /// Maximum concurrent secure data items
        /// </summary>
        public int MaxConcurrentSecureDataItems { get; set; } = 100;

        /// <summary>
        /// 自动清理间隔
        /// Automatic cleanup interval
        /// </summary>
        public TimeSpan AutoCleanupInterval { get; set; } = TimeSpan.FromMinutes(30);

        /// <summary>
        /// 是否在调试时禁用安全功能
        /// Whether to disable security features during debugging
        /// </summary>
        public bool DisableSecurityInDebug { get; set; } = false;

        /// <summary>
        /// 克隆安全策略
        /// Clones the security policy
        /// </summary>
        /// <returns>克隆的策略 Cloned policy</returns>
        public SecurityPolicy Clone()
        {
            return new SecurityPolicy
            {
                RequireMemoryEncryption = RequireMemoryEncryption,
                MaxSecureDataLifetime = MaxSecureDataLifetime,
                RequireAuditTrail = RequireAuditTrail,
                MaxConcurrentSecureDataItems = MaxConcurrentSecureDataItems,
                AutoCleanupInterval = AutoCleanupInterval,
                DisableSecurityInDebug = DisableSecurityInDebug
            };
        }
    }

    /// <summary>
    /// 安全用户信息
    /// Security user information
    /// </summary>
    public class SecurityUser
    {
        /// <summary>
        /// 用户标识符
        /// User identifier
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// 用户名
        /// Username
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// 用户角色
        /// User roles
        /// </summary>
        public HashSet<string> Roles { get; set; } = new();

        /// <summary>
        /// 用户权限
        /// User permissions
        /// </summary>
        public HashSet<string> Permissions { get; set; } = new();

        /// <summary>
        /// 登录时间
        /// Login time
        /// </summary>
        public DateTime LoginTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 会话过期时间
        /// Session expiration time
        /// </summary>
        public DateTime? SessionExpiresAt { get; set; }

        /// <summary>
        /// 克隆用户信息
        /// Clones the user information
        /// </summary>
        /// <returns>克隆的用户信息 Cloned user information</returns>
        public SecurityUser Clone()
        {
            return new SecurityUser
            {
                UserId = UserId,
                Username = Username,
                LoginTime = LoginTime,
                SessionExpiresAt = SessionExpiresAt,
                Roles = new HashSet<string>(Roles),
                Permissions = new HashSet<string>(Permissions)
            };
        }
    }
}