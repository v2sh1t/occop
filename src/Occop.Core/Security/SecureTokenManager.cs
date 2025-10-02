using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Occop.Core.Authentication;
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace Occop.Core.Security
{
    /// <summary>
    /// Enterprise-grade secure token manager with encryption and rotation capabilities
    /// 企业级安全令牌管理器，具有加密和轮换功能
    /// </summary>
    public class SecureTokenManager : IDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SecureTokenManager> _logger;
        private readonly TokenStorage _tokenStorage;
        private readonly object _lockObject = new object();

        private Timer? _tokenRefreshTimer;
        private Timer? _encryptionKeyRotationTimer;
        private byte[]? _encryptionKey;
        private byte[]? _encryptionIV;
        private bool _disposed = false;

        // Configuration keys
        private const string EncryptionEnabledKey = "Security:EncryptionEnabled";
        private const string KeyRotationIntervalKey = "Security:KeyRotationIntervalHours";
        private const string TokenRefreshIntervalKey = "Security:TokenRefreshIntervalMinutes";
        private const string TokenRefreshThresholdKey = "Security:TokenRefreshThresholdMinutes";

        // Default values
        private const int DefaultKeyRotationIntervalHours = 24;
        private const int DefaultTokenRefreshIntervalMinutes = 30;
        private const int DefaultTokenRefreshThresholdMinutes = 60;

        /// <summary>
        /// Event fired when token refresh is attempted
        /// 尝试刷新令牌时触发的事件
        /// </summary>
        public event EventHandler<TokenRefreshEventArgs>? TokenRefreshRequested;

        /// <summary>
        /// Event fired when encryption key rotation occurs
        /// 进行加密密钥轮换时触发的事件
        /// </summary>
        public event EventHandler<KeyRotationEventArgs>? EncryptionKeyRotated;

        /// <summary>
        /// Event fired when security events occur
        /// 发生安全事件时触发的事件
        /// </summary>
        public event EventHandler<SecurityEventArgs>? SecurityEvent;

        /// <summary>
        /// Gets whether encryption is enabled
        /// 获取是否启用了加密
        /// </summary>
        public bool IsEncryptionEnabled { get; private set; }

        /// <summary>
        /// Gets whether automatic token refresh is enabled
        /// 获取是否启用了自动令牌刷新
        /// </summary>
        public bool IsAutoRefreshEnabled { get; private set; }

        /// <summary>
        /// Gets the next scheduled key rotation time
        /// 获取下次计划的密钥轮换时间
        /// </summary>
        public DateTime? NextKeyRotationTime { get; private set; }

        /// <summary>
        /// Gets the last key rotation time
        /// 获取上次密钥轮换时间
        /// </summary>
        public DateTime? LastKeyRotationTime { get; private set; }

        /// <summary>
        /// Initializes a new instance of the SecureTokenManager class
        /// 初始化SecureTokenManager类的新实例
        /// </summary>
        /// <param name="configuration">Application configuration</param>
        /// <param name="logger">Logger instance</param>
        /// <param name="tokenStorage">Token storage instance</param>
        public SecureTokenManager(
            IConfiguration configuration,
            ILogger<SecureTokenManager> logger,
            TokenStorage tokenStorage)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _tokenStorage = tokenStorage ?? throw new ArgumentNullException(nameof(tokenStorage));

            LoadConfiguration();
            InitializeEncryption();
            SetupTimers();

            // Subscribe to token storage events
            _tokenStorage.TokenExpired += OnTokenExpired;

            _logger.LogInformation("SecureTokenManager initialized with encryption={Encryption}, autoRefresh={AutoRefresh}",
                IsEncryptionEnabled, IsAutoRefreshEnabled);
        }

        /// <summary>
        /// Stores a token securely with optional encryption
        /// 安全地存储令牌，可选择加密
        /// </summary>
        /// <param name="token">Token to store</param>
        /// <param name="expiresIn">Token validity duration in seconds</param>
        /// <param name="tokenType">Type of token being stored</param>
        /// <exception cref="ArgumentException">Thrown when token is null or empty</exception>
        /// <exception cref="SecurityException">Thrown when encryption fails</exception>
        public void StoreTokenSecurely(string token, int expiresIn, TokenType tokenType = TokenType.Access)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("Token cannot be null or empty", nameof(token));

            lock (_lockObject)
            {
                ThrowIfDisposed();

                try
                {
                    string processedToken = token;

                    // Apply encryption if enabled
                    if (IsEncryptionEnabled)
                    {
                        processedToken = EncryptToken(token);
                        _logger.LogDebug("Token encrypted before storage");
                    }

                    // Store the token
                    if (tokenType == TokenType.Access)
                    {
                        _tokenStorage.StoreAccessToken(processedToken, expiresIn);
                    }
                    else
                    {
                        _tokenStorage.StoreRefreshToken(processedToken, expiresIn);
                    }

                    OnSecurityEvent(SecurityEventType.TokenStored, $"{tokenType} token stored securely");

                    _logger.LogInformation("{TokenType} token stored securely, expires in {ExpiresIn} seconds", tokenType, expiresIn);
                }
                catch (Exception ex)
                {
                    OnSecurityEvent(SecurityEventType.TokenStorageError, $"Failed to store {tokenType} token: {ex.Message}");
                    _logger.LogError(ex, "Error storing {TokenType} token securely", tokenType);
                    throw;
                }
            }
        }

        /// <summary>
        /// Retrieves a token securely with optional decryption
        /// 安全地检索令牌，可选择解密
        /// </summary>
        /// <param name="tokenType">Type of token to retrieve</param>
        /// <returns>Decrypted token string or null if not available</returns>
        /// <exception cref="SecurityException">Thrown when decryption fails</exception>
        public string? RetrieveTokenSecurely(TokenType tokenType = TokenType.Access)
        {
            lock (_lockObject)
            {
                ThrowIfDisposed();

                try
                {
                    SecureString? encryptedToken = tokenType == TokenType.Access
                        ? _tokenStorage.GetAccessToken()
                        : _tokenStorage.GetRefreshToken();

                    if (encryptedToken == null)
                        return null;

                    // Convert SecureString to string
                    string tokenString = ConvertSecureStringToString(encryptedToken);
                    encryptedToken.Dispose();

                    // Apply decryption if enabled
                    if (IsEncryptionEnabled)
                    {
                        tokenString = DecryptToken(tokenString);
                        _logger.LogDebug("{TokenType} token decrypted for retrieval", tokenType);
                    }

                    OnSecurityEvent(SecurityEventType.TokenRetrieved, $"{tokenType} token retrieved securely");
                    return tokenString;
                }
                catch (Exception ex)
                {
                    OnSecurityEvent(SecurityEventType.TokenRetrievalError, $"Failed to retrieve {tokenType} token: {ex.Message}");
                    _logger.LogError(ex, "Error retrieving {TokenType} token securely", tokenType);
                    throw;
                }
            }
        }

        /// <summary>
        /// Forces rotation of encryption keys
        /// 强制轮换加密密钥
        /// </summary>
        /// <exception cref="SecurityException">Thrown when key rotation fails</exception>
        public void RotateEncryptionKeys()
        {
            if (!IsEncryptionEnabled)
            {
                _logger.LogWarning("Cannot rotate encryption keys when encryption is disabled");
                return;
            }

            lock (_lockObject)
            {
                ThrowIfDisposed();

                try
                {
                    _logger.LogInformation("Starting encryption key rotation");

                    // Store old keys for potential rollback
                    var oldKey = _encryptionKey;
                    var oldIV = _encryptionIV;

                    // Generate new encryption key and IV
                    GenerateEncryptionKeyAndIV();

                    LastKeyRotationTime = DateTime.UtcNow;
                    NextKeyRotationTime = CalculateNextKeyRotationTime();

                    OnEncryptionKeyRotated(new KeyRotationEventArgs(LastKeyRotationTime.Value, NextKeyRotationTime));
                    OnSecurityEvent(SecurityEventType.KeyRotation, "Encryption keys rotated successfully");

                    _logger.LogInformation("Encryption key rotation completed successfully. Next rotation: {NextRotation}",
                        NextKeyRotationTime);

                    // Securely clear old keys
                    if (oldKey != null) Array.Clear(oldKey, 0, oldKey.Length);
                    if (oldIV != null) Array.Clear(oldIV, 0, oldIV.Length);
                }
                catch (Exception ex)
                {
                    OnSecurityEvent(SecurityEventType.KeyRotationError, $"Key rotation failed: {ex.Message}");
                    _logger.LogError(ex, "Error during encryption key rotation");
                    throw new SecurityException("Encryption key rotation failed", ex);
                }
            }
        }

        /// <summary>
        /// Clears all stored tokens and security state
        /// 清除所有存储的令牌和安全状态
        /// </summary>
        public void ClearAllSecurityState()
        {
            lock (_lockObject)
            {
                ThrowIfDisposed();

                _logger.LogInformation("Clearing all security state");

                // Clear tokens
                _tokenStorage.ClearAllTokens();

                // Clear encryption keys
                if (_encryptionKey != null)
                {
                    Array.Clear(_encryptionKey, 0, _encryptionKey.Length);
                    _encryptionKey = null;
                }

                if (_encryptionIV != null)
                {
                    Array.Clear(_encryptionIV, 0, _encryptionIV.Length);
                    _encryptionIV = null;
                }

                // Regenerate encryption keys if encryption is enabled
                if (IsEncryptionEnabled)
                {
                    GenerateEncryptionKeyAndIV();
                }

                OnSecurityEvent(SecurityEventType.SecurityStateCleared, "All security state cleared");
                _logger.LogInformation("All security state cleared successfully");
            }
        }

        /// <summary>
        /// Gets security status information
        /// 获取安全状态信息
        /// </summary>
        /// <returns>Security status information</returns>
        public SecurityStatus GetSecurityStatus()
        {
            lock (_lockObject)
            {
                ThrowIfDisposed();

                return new SecurityStatus
                {
                    IsEncryptionEnabled = IsEncryptionEnabled,
                    IsAutoRefreshEnabled = IsAutoRefreshEnabled,
                    HasValidAccessToken = _tokenStorage.HasValidAccessToken,
                    HasValidRefreshToken = _tokenStorage.HasValidRefreshToken,
                    AccessTokenExpirationTime = _tokenStorage.AccessTokenExpirationTime,
                    RefreshTokenExpirationTime = _tokenStorage.RefreshTokenExpirationTime,
                    LastKeyRotationTime = LastKeyRotationTime,
                    NextKeyRotationTime = NextKeyRotationTime
                };
            }
        }

        /// <summary>
        /// Loads configuration settings
        /// 加载配置设置
        /// </summary>
        private void LoadConfiguration()
        {
            IsEncryptionEnabled = _configuration.GetValue<bool>(EncryptionEnabledKey, true);

            var refreshIntervalMinutes = _configuration.GetValue<int>(TokenRefreshIntervalKey, DefaultTokenRefreshIntervalMinutes);
            IsAutoRefreshEnabled = refreshIntervalMinutes > 0;
        }

        /// <summary>
        /// Initializes encryption components
        /// 初始化加密组件
        /// </summary>
        private void InitializeEncryption()
        {
            if (IsEncryptionEnabled)
            {
                GenerateEncryptionKeyAndIV();
                LastKeyRotationTime = DateTime.UtcNow;
                NextKeyRotationTime = CalculateNextKeyRotationTime();
                _logger.LogDebug("Encryption initialized with new keys");
            }
        }

        /// <summary>
        /// Generates new encryption key and initialization vector
        /// 生成新的加密密钥和初始化向量
        /// </summary>
        private void GenerateEncryptionKeyAndIV()
        {
            using var aes = Aes.Create();
            aes.GenerateKey();
            aes.GenerateIV();

            _encryptionKey = aes.Key;
            _encryptionIV = aes.IV;
        }

        /// <summary>
        /// Encrypts a token using AES encryption
        /// 使用AES加密令牌
        /// </summary>
        private string EncryptToken(string token)
        {
            if (_encryptionKey == null || _encryptionIV == null)
                throw new SecurityException("Encryption keys not initialized");

            using var aes = Aes.Create();
            aes.Key = _encryptionKey;
            aes.IV = _encryptionIV;

            using var encryptor = aes.CreateEncryptor();
            using var memoryStream = new MemoryStream();
            using var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write);
            using var writer = new StreamWriter(cryptoStream);

            writer.Write(token);
            writer.Flush();
            cryptoStream.FlushFinalBlock();

            return Convert.ToBase64String(memoryStream.ToArray());
        }

        /// <summary>
        /// Decrypts a token using AES decryption
        /// 使用AES解密令牌
        /// </summary>
        private string DecryptToken(string encryptedToken)
        {
            if (_encryptionKey == null || _encryptionIV == null)
                throw new SecurityException("Encryption keys not initialized");

            var encryptedBytes = Convert.FromBase64String(encryptedToken);

            using var aes = Aes.Create();
            aes.Key = _encryptionKey;
            aes.IV = _encryptionIV;

            using var decryptor = aes.CreateDecryptor();
            using var memoryStream = new MemoryStream(encryptedBytes);
            using var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
            using var reader = new StreamReader(cryptoStream);

            return reader.ReadToEnd();
        }

        /// <summary>
        /// Sets up periodic timers for key rotation and token refresh
        /// 设置密钥轮换和令牌刷新的定期计时器
        /// </summary>
        private void SetupTimers()
        {
            // Setup key rotation timer
            if (IsEncryptionEnabled)
            {
                var rotationIntervalHours = _configuration.GetValue<int>(KeyRotationIntervalKey, DefaultKeyRotationIntervalHours);
                var rotationInterval = TimeSpan.FromHours(rotationIntervalHours);

                _encryptionKeyRotationTimer = new Timer(
                    _ => RotateEncryptionKeys(),
                    null,
                    rotationInterval,
                    rotationInterval);

                _logger.LogDebug("Key rotation timer setup with {Interval} hours interval", rotationIntervalHours);
            }

            // Setup token refresh timer
            if (IsAutoRefreshEnabled)
            {
                var refreshIntervalMinutes = _configuration.GetValue<int>(TokenRefreshIntervalKey, DefaultTokenRefreshIntervalMinutes);
                var refreshInterval = TimeSpan.FromMinutes(refreshIntervalMinutes);

                _tokenRefreshTimer = new Timer(
                    _ => CheckAndRequestTokenRefresh(),
                    null,
                    refreshInterval,
                    refreshInterval);

                _logger.LogDebug("Token refresh timer setup with {Interval} minutes interval", refreshIntervalMinutes);
            }
        }

        /// <summary>
        /// Checks if token needs refresh and requests it
        /// 检查是否需要刷新令牌并请求刷新
        /// </summary>
        private void CheckAndRequestTokenRefresh()
        {
            try
            {
                var thresholdMinutes = _configuration.GetValue<int>(TokenRefreshThresholdKey, DefaultTokenRefreshThresholdMinutes);
                var threshold = TimeSpan.FromMinutes(thresholdMinutes);

                if (_tokenStorage.WillAccessTokenExpireWithin(threshold))
                {
                    _logger.LogInformation("Access token will expire within {Threshold} minutes, requesting refresh", thresholdMinutes);
                    OnTokenRefreshRequested(new TokenRefreshEventArgs(TokenType.Access, threshold));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during automatic token refresh check");
            }
        }

        /// <summary>
        /// Calculates the next key rotation time
        /// 计算下次密钥轮换时间
        /// </summary>
        private DateTime CalculateNextKeyRotationTime()
        {
            var intervalHours = _configuration.GetValue<int>(KeyRotationIntervalKey, DefaultKeyRotationIntervalHours);
            return DateTime.UtcNow.AddHours(intervalHours);
        }

        /// <summary>
        /// Handles token expiration events
        /// 处理令牌过期事件
        /// </summary>
        private void OnTokenExpired(object? sender, TokenExpiredEventArgs e)
        {
            _logger.LogWarning("{TokenType} token has expired", e.TokenType);
            OnSecurityEvent(SecurityEventType.TokenExpired, $"{e.TokenType} token expired");

            if (IsAutoRefreshEnabled && e.TokenType == TokenType.Access)
            {
                OnTokenRefreshRequested(new TokenRefreshEventArgs(e.TokenType, TimeSpan.Zero));
            }
        }

        /// <summary>
        /// Converts SecureString to regular string
        /// 将SecureString转换为普通字符串
        /// </summary>
        private string ConvertSecureStringToString(SecureString secureString)
        {
            IntPtr ptr = IntPtr.Zero;
            try
            {
                ptr = System.Runtime.InteropServices.Marshal.SecureStringToGlobalAllocUnicode(secureString);
                return System.Runtime.InteropServices.Marshal.PtrToStringUni(ptr) ?? string.Empty;
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                {
                    System.Runtime.InteropServices.Marshal.ZeroFreeGlobalAllocUnicode(ptr);
                }
            }
        }

        /// <summary>
        /// Raises the TokenRefreshRequested event
        /// 触发TokenRefreshRequested事件
        /// </summary>
        private void OnTokenRefreshRequested(TokenRefreshEventArgs args)
        {
            try
            {
                TokenRefreshRequested?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error firing TokenRefreshRequested event");
            }
        }

        /// <summary>
        /// Raises the EncryptionKeyRotated event
        /// 触发EncryptionKeyRotated事件
        /// </summary>
        private void OnEncryptionKeyRotated(KeyRotationEventArgs args)
        {
            try
            {
                EncryptionKeyRotated?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error firing EncryptionKeyRotated event");
            }
        }

        /// <summary>
        /// Raises the SecurityEvent event
        /// 触发SecurityEvent事件
        /// </summary>
        private void OnSecurityEvent(SecurityEventType eventType, string message)
        {
            try
            {
                SecurityEvent?.Invoke(this, new SecurityEventArgs(eventType, message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error firing SecurityEvent event");
            }
        }

        /// <summary>
        /// Throws ObjectDisposedException if the instance is disposed
        /// 如果实例已释放则抛出ObjectDisposedException
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SecureTokenManager));
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
                        _logger.LogDebug("Disposing SecureTokenManager");

                        // Dispose timers
                        _tokenRefreshTimer?.Dispose();
                        _encryptionKeyRotationTimer?.Dispose();

                        // Unsubscribe from events
                        if (_tokenStorage != null)
                        {
                            _tokenStorage.TokenExpired -= OnTokenExpired;
                        }

                        // Clear encryption keys
                        if (_encryptionKey != null)
                        {
                            Array.Clear(_encryptionKey, 0, _encryptionKey.Length);
                        }

                        if (_encryptionIV != null)
                        {
                            Array.Clear(_encryptionIV, 0, _encryptionIV.Length);
                        }

                        _disposed = true;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Security status information
    /// 安全状态信息
    /// </summary>
    public class SecurityStatus
    {
        public bool IsEncryptionEnabled { get; set; }
        public bool IsAutoRefreshEnabled { get; set; }
        public bool HasValidAccessToken { get; set; }
        public bool HasValidRefreshToken { get; set; }
        public DateTime? AccessTokenExpirationTime { get; set; }
        public DateTime? RefreshTokenExpirationTime { get; set; }
        public DateTime? LastKeyRotationTime { get; set; }
        public DateTime? NextKeyRotationTime { get; set; }
    }

    /// <summary>
    /// Event arguments for token refresh requests
    /// 令牌刷新请求的事件参数
    /// </summary>
    public class TokenRefreshEventArgs : EventArgs
    {
        public TokenType TokenType { get; }
        public TimeSpan TimeUntilExpiration { get; }
        public DateTime RequestTime { get; }

        public TokenRefreshEventArgs(TokenType tokenType, TimeSpan timeUntilExpiration)
        {
            TokenType = tokenType;
            TimeUntilExpiration = timeUntilExpiration;
            RequestTime = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Event arguments for key rotation events
    /// 密钥轮换事件的事件参数
    /// </summary>
    public class KeyRotationEventArgs : EventArgs
    {
        public DateTime RotationTime { get; }
        public DateTime? NextRotationTime { get; }

        public KeyRotationEventArgs(DateTime rotationTime, DateTime? nextRotationTime)
        {
            RotationTime = rotationTime;
            NextRotationTime = nextRotationTime;
        }
    }

    /// <summary>
    /// Event arguments for security events
    /// 安全事件的事件参数
    /// </summary>
    public class SecurityEventArgs : EventArgs
    {
        public SecurityEventType EventType { get; }
        public string Message { get; }
        public DateTime Timestamp { get; }

        public SecurityEventArgs(SecurityEventType eventType, string message)
        {
            EventType = eventType;
            Message = message;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Types of security events
    /// 安全事件类型
    /// </summary>
    public enum SecurityEventType
    {
        TokenStored,
        TokenRetrieved,
        TokenExpired,
        TokenStorageError,
        TokenRetrievalError,
        KeyRotation,
        KeyRotationError,
        SecurityStateCleared
    }
}