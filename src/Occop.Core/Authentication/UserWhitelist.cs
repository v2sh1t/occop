using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Occop.Core.Authentication
{
    /// <summary>
    /// User whitelist management for authentication authorization
    /// 用户白名单管理，用于认证授权
    /// </summary>
    public class UserWhitelist : IDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<UserWhitelist> _logger;
        private readonly object _lockObject = new object();

        private HashSet<string> _allowedUsers;
        private HashSet<string> _blockedUsers;
        private DateTime _lastConfigurationReload;
        private bool _disposed = false;

        // Configuration keys
        private const string AllowedUsersKey = "GitHub:AllowedUsers";
        private const string BlockedUsersKey = "GitHub:BlockedUsers";
        private const string WhitelistModeKey = "GitHub:WhitelistMode";
        private const string CaseSensitiveKey = "GitHub:CaseSensitive";

        // Cache timeout for configuration reload
        private static readonly TimeSpan ConfigurationCacheTimeout = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Event fired when whitelist configuration changes
        /// 白名单配置改变时触发的事件
        /// </summary>
        public event EventHandler<WhitelistChangedEventArgs>? WhitelistChanged;

        /// <summary>
        /// Gets the current whitelist mode
        /// 获取当前白名单模式
        /// </summary>
        public WhitelistMode Mode { get; private set; }

        /// <summary>
        /// Gets whether the whitelist is case sensitive
        /// 获取白名单是否大小写敏感
        /// </summary>
        public bool IsCaseSensitive { get; private set; }

        /// <summary>
        /// Gets the count of allowed users
        /// 获取允许用户的数量
        /// </summary>
        public int AllowedUsersCount
        {
            get
            {
                lock (_lockObject)
                {
                    RefreshConfigurationIfNeeded();
                    return _allowedUsers.Count;
                }
            }
        }

        /// <summary>
        /// Gets the count of blocked users
        /// 获取被阻止用户的数量
        /// </summary>
        public int BlockedUsersCount
        {
            get
            {
                lock (_lockObject)
                {
                    RefreshConfigurationIfNeeded();
                    return _blockedUsers.Count;
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the UserWhitelist class
        /// 初始化UserWhitelist类的新实例
        /// </summary>
        /// <param name="configuration">Application configuration</param>
        /// <param name="logger">Logger instance</param>
        public UserWhitelist(IConfiguration configuration, ILogger<UserWhitelist> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _allowedUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _blockedUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _lastConfigurationReload = DateTime.MinValue;

            LoadConfiguration();
        }

        /// <summary>
        /// Checks if a user is allowed based on the current whitelist configuration
        /// 根据当前白名单配置检查用户是否被允许
        /// </summary>
        /// <param name="userLogin">User login name to check</param>
        /// <returns>True if user is allowed, false otherwise</returns>
        /// <exception cref="ArgumentException">Thrown when userLogin is null or empty</exception>
        public bool IsUserAllowed(string userLogin)
        {
            if (string.IsNullOrWhiteSpace(userLogin))
                throw new ArgumentException("User login cannot be null or empty", nameof(userLogin));

            lock (_lockObject)
            {
                ThrowIfDisposed();
                RefreshConfigurationIfNeeded();

                var normalizedLogin = IsCaseSensitive ? userLogin : userLogin.ToLowerInvariant();

                // Check if user is explicitly blocked
                if (_blockedUsers.Contains(normalizedLogin))
                {
                    _logger.LogWarning("User {UserLogin} is explicitly blocked", userLogin);
                    return false;
                }

                // Apply whitelist mode logic
                switch (Mode)
                {
                    case WhitelistMode.Disabled:
                        _logger.LogDebug("Whitelist disabled, allowing user {UserLogin}", userLogin);
                        return true;

                    case WhitelistMode.AllowList:
                        var isAllowed = _allowedUsers.Contains(normalizedLogin);
                        if (!isAllowed)
                        {
                            _logger.LogWarning("User {UserLogin} is not in the allow list", userLogin);
                        }
                        else
                        {
                            _logger.LogDebug("User {UserLogin} is in the allow list", userLogin);
                        }
                        return isAllowed;

                    case WhitelistMode.BlockList:
                        // If not in block list, user is allowed
                        _logger.LogDebug("User {UserLogin} is not in the block list, allowing access", userLogin);
                        return true;

                    default:
                        _logger.LogError("Unknown whitelist mode: {Mode}", Mode);
                        return false;
                }
            }
        }

        /// <summary>
        /// Validates multiple users against the whitelist
        /// 对多个用户进行白名单验证
        /// </summary>
        /// <param name="userLogins">Collection of user login names to validate</param>
        /// <returns>Dictionary with validation results for each user</returns>
        /// <exception cref="ArgumentNullException">Thrown when userLogins is null</exception>
        public Dictionary<string, bool> ValidateUsers(IEnumerable<string> userLogins)
        {
            if (userLogins == null)
                throw new ArgumentNullException(nameof(userLogins));

            var results = new Dictionary<string, bool>();

            foreach (var login in userLogins)
            {
                if (!string.IsNullOrWhiteSpace(login))
                {
                    results[login] = IsUserAllowed(login);
                }
            }

            return results;
        }

        /// <summary>
        /// Gets the list of currently allowed users (for administrative purposes)
        /// 获取当前允许的用户列表（用于管理目的）
        /// </summary>
        /// <returns>Read-only collection of allowed user names</returns>
        public IReadOnlyCollection<string> GetAllowedUsers()
        {
            lock (_lockObject)
            {
                ThrowIfDisposed();
                RefreshConfigurationIfNeeded();
                return _allowedUsers.ToList().AsReadOnly();
            }
        }

        /// <summary>
        /// Gets the list of currently blocked users (for administrative purposes)
        /// 获取当前被阻止的用户列表（用于管理目的）
        /// </summary>
        /// <returns>Read-only collection of blocked user names</returns>
        public IReadOnlyCollection<string> GetBlockedUsers()
        {
            lock (_lockObject)
            {
                ThrowIfDisposed();
                RefreshConfigurationIfNeeded();
                return _blockedUsers.ToList().AsReadOnly();
            }
        }

        /// <summary>
        /// Forces a reload of the whitelist configuration from the configuration source
        /// 强制从配置源重新加载白名单配置
        /// </summary>
        public void ReloadConfiguration()
        {
            lock (_lockObject)
            {
                ThrowIfDisposed();

                var oldAllowedCount = _allowedUsers.Count;
                var oldBlockedCount = _blockedUsers.Count;
                var oldMode = Mode;

                LoadConfiguration();

                _logger.LogInformation("Whitelist configuration reloaded. Mode: {Mode}, Allowed: {AllowedCount}, Blocked: {BlockedCount}",
                    Mode, _allowedUsers.Count, _blockedUsers.Count);

                // Fire event if configuration changed significantly
                if (oldMode != Mode || oldAllowedCount != _allowedUsers.Count || oldBlockedCount != _blockedUsers.Count)
                {
                    OnWhitelistChanged();
                }
            }
        }

        /// <summary>
        /// Gets detailed information about the current whitelist configuration
        /// 获取当前白名单配置的详细信息
        /// </summary>
        /// <returns>Whitelist configuration information</returns>
        public WhitelistInfo GetWhitelistInfo()
        {
            lock (_lockObject)
            {
                ThrowIfDisposed();
                RefreshConfigurationIfNeeded();

                return new WhitelistInfo
                {
                    Mode = Mode,
                    IsCaseSensitive = IsCaseSensitive,
                    AllowedUsersCount = _allowedUsers.Count,
                    BlockedUsersCount = _blockedUsers.Count,
                    LastUpdated = _lastConfigurationReload
                };
            }
        }

        /// <summary>
        /// Loads whitelist configuration from the configuration source
        /// 从配置源加载白名单配置
        /// </summary>
        private void LoadConfiguration()
        {
            try
            {
                // Load whitelist mode
                var modeString = _configuration[WhitelistModeKey];
                if (Enum.TryParse<WhitelistMode>(modeString, true, out var mode))
                {
                    Mode = mode;
                }
                else
                {
                    Mode = WhitelistMode.Disabled;
                    if (!string.IsNullOrEmpty(modeString))
                    {
                        _logger.LogWarning("Invalid whitelist mode '{Mode}' in configuration, using disabled mode", modeString);
                    }
                }

                // Load case sensitivity setting
                if (bool.TryParse(_configuration[CaseSensitiveKey], out var caseSensitive))
                {
                    IsCaseSensitive = caseSensitive;
                }
                else
                {
                    IsCaseSensitive = false; // Default to case insensitive
                }

                // Update string comparers based on case sensitivity
                var comparer = IsCaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

                // Load allowed users
                var allowedUsers = _configuration.GetSection(AllowedUsersKey).Get<string[]>();
                _allowedUsers = new HashSet<string>(comparer);
                if (allowedUsers != null)
                {
                    foreach (var user in allowedUsers.Where(u => !string.IsNullOrWhiteSpace(u)))
                    {
                        var normalizedUser = IsCaseSensitive ? user : user.ToLowerInvariant();
                        _allowedUsers.Add(normalizedUser);
                    }
                }

                // Load blocked users
                var blockedUsers = _configuration.GetSection(BlockedUsersKey).Get<string[]>();
                _blockedUsers = new HashSet<string>(comparer);
                if (blockedUsers != null)
                {
                    foreach (var user in blockedUsers.Where(u => !string.IsNullOrWhiteSpace(u)))
                    {
                        var normalizedUser = IsCaseSensitive ? user : user.ToLowerInvariant();
                        _blockedUsers.Add(normalizedUser);
                    }
                }

                _lastConfigurationReload = DateTime.UtcNow;

                _logger.LogDebug("Loaded whitelist configuration: Mode={Mode}, CaseSensitive={CaseSensitive}, Allowed={AllowedCount}, Blocked={BlockedCount}",
                    Mode, IsCaseSensitive, _allowedUsers.Count, _blockedUsers.Count);

                // Warn if no users in allow list when using AllowList mode
                if (Mode == WhitelistMode.AllowList && _allowedUsers.Count == 0)
                {
                    _logger.LogWarning("Whitelist is in AllowList mode but no users are configured. All users will be denied access.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading whitelist configuration, using safe defaults");

                // Use safe defaults on error
                Mode = WhitelistMode.Disabled;
                IsCaseSensitive = false;
                _allowedUsers.Clear();
                _blockedUsers.Clear();
                _lastConfigurationReload = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Refreshes configuration if enough time has passed since last reload
        /// 如果距离上次重新加载已经过了足够时间，则刷新配置
        /// </summary>
        private void RefreshConfigurationIfNeeded()
        {
            if (DateTime.UtcNow - _lastConfigurationReload > ConfigurationCacheTimeout)
            {
                LoadConfiguration();
            }
        }

        /// <summary>
        /// Raises the WhitelistChanged event
        /// 触发WhitelistChanged事件
        /// </summary>
        private void OnWhitelistChanged()
        {
            try
            {
                WhitelistChanged?.Invoke(this, new WhitelistChangedEventArgs(GetWhitelistInfo()));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error firing WhitelistChanged event");
            }
        }

        /// <summary>
        /// Throws ObjectDisposedException if the instance is disposed
        /// 如果实例已释放则抛出ObjectDisposedException
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(UserWhitelist));
        }

        /// <summary>
        /// Disposes of managed resources
        /// 释放托管资源
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                lock (_lockObject)
                {
                    if (!_disposed)
                    {
                        _logger.LogDebug("Disposing UserWhitelist");
                        _allowedUsers.Clear();
                        _blockedUsers.Clear();
                        _disposed = true;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Whitelist operation modes
    /// 白名单操作模式
    /// </summary>
    public enum WhitelistMode
    {
        /// <summary>
        /// Whitelist is disabled, all users are allowed (except those explicitly blocked)
        /// 白名单已禁用，允许所有用户（除了明确被阻止的用户）
        /// </summary>
        Disabled,

        /// <summary>
        /// Only users in the allow list are permitted
        /// 只有在允许列表中的用户才被允许
        /// </summary>
        AllowList,

        /// <summary>
        /// All users are allowed except those in the block list
        /// 允许所有用户，除了在阻止列表中的用户
        /// </summary>
        BlockList
    }

    /// <summary>
    /// Information about whitelist configuration
    /// 白名单配置信息
    /// </summary>
    public class WhitelistInfo
    {
        /// <summary>
        /// Current whitelist mode
        /// 当前白名单模式
        /// </summary>
        public WhitelistMode Mode { get; set; }

        /// <summary>
        /// Whether user name comparison is case sensitive
        /// 用户名比较是否大小写敏感
        /// </summary>
        public bool IsCaseSensitive { get; set; }

        /// <summary>
        /// Number of users in the allow list
        /// 允许列表中的用户数量
        /// </summary>
        public int AllowedUsersCount { get; set; }

        /// <summary>
        /// Number of users in the block list
        /// 阻止列表中的用户数量
        /// </summary>
        public int BlockedUsersCount { get; set; }

        /// <summary>
        /// When the configuration was last updated
        /// 配置最后更新的时间
        /// </summary>
        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// Event arguments for whitelist configuration changes
    /// 白名单配置更改的事件参数
    /// </summary>
    public class WhitelistChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Updated whitelist information
        /// 更新的白名单信息
        /// </summary>
        public WhitelistInfo WhitelistInfo { get; }

        /// <summary>
        /// Initializes a new instance of the WhitelistChangedEventArgs class
        /// 初始化WhitelistChangedEventArgs类的新实例
        /// </summary>
        /// <param name="whitelistInfo">Updated whitelist information</param>
        public WhitelistChangedEventArgs(WhitelistInfo whitelistInfo)
        {
            WhitelistInfo = whitelistInfo ?? throw new ArgumentNullException(nameof(whitelistInfo));
        }
    }
}