using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Security;

namespace Occop.Core.Authentication
{
    /// <summary>
    /// Secure token storage implementation using SecureString
    /// 使用SecureString实现的安全令牌存储
    /// </summary>
    public class TokenStorage : IDisposable
    {
        private readonly ILogger<TokenStorage> _logger;
        private SecureString? _accessToken;
        private SecureString? _refreshToken;
        private DateTime _tokenExpirationTime;
        private DateTime _refreshTokenExpirationTime;
        private readonly object _lockObject = new object();
        private bool _disposed = false;

        /// <summary>
        /// Event fired when token expires
        /// 令牌过期时触发的事件
        /// </summary>
        public event EventHandler<TokenExpiredEventArgs>? TokenExpired;

        /// <summary>
        /// Gets whether an access token is currently stored and valid
        /// 获取当前是否存储了有效的访问令牌
        /// </summary>
        public bool HasValidAccessToken
        {
            get
            {
                lock (_lockObject)
                {
                    return _accessToken != null && DateTime.UtcNow < _tokenExpirationTime;
                }
            }
        }

        /// <summary>
        /// Gets whether a refresh token is currently stored and valid
        /// 获取当前是否存储了有效的刷新令牌
        /// </summary>
        public bool HasValidRefreshToken
        {
            get
            {
                lock (_lockObject)
                {
                    return _refreshToken != null && DateTime.UtcNow < _refreshTokenExpirationTime;
                }
            }
        }

        /// <summary>
        /// Gets the access token expiration time (UTC)
        /// 获取访问令牌过期时间（UTC）
        /// </summary>
        public DateTime? AccessTokenExpirationTime
        {
            get
            {
                lock (_lockObject)
                {
                    return _accessToken != null ? _tokenExpirationTime : null;
                }
            }
        }

        /// <summary>
        /// Gets the refresh token expiration time (UTC)
        /// 获取刷新令牌过期时间（UTC）
        /// </summary>
        public DateTime? RefreshTokenExpirationTime
        {
            get
            {
                lock (_lockObject)
                {
                    return _refreshToken != null ? _refreshTokenExpirationTime : null;
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the TokenStorage class
        /// 初始化TokenStorage类的新实例
        /// </summary>
        /// <param name="logger">Logger instance</param>
        public TokenStorage(ILogger<TokenStorage> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _tokenExpirationTime = DateTime.MinValue;
            _refreshTokenExpirationTime = DateTime.MinValue;
        }

        /// <summary>
        /// Stores an access token securely with expiration time
        /// 安全地存储访问令牌及其过期时间
        /// </summary>
        /// <param name="token">Access token to store</param>
        /// <param name="expiresIn">Token validity duration in seconds</param>
        /// <exception cref="ArgumentException">Thrown when token is null or empty</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when expiresIn is negative</exception>
        public void StoreAccessToken(string token, int expiresIn)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("Token cannot be null or empty", nameof(token));

            if (expiresIn < 0)
                throw new ArgumentOutOfRangeException(nameof(expiresIn), "Expiration time cannot be negative");

            lock (_lockObject)
            {
                ThrowIfDisposed();

                _logger.LogDebug("Storing access token with {ExpiresIn} seconds validity", expiresIn);

                // Clear existing token
                ClearAccessToken();

                // Store new token
                _accessToken = new SecureString();
                foreach (char c in token)
                {
                    _accessToken.AppendChar(c);
                }
                _accessToken.MakeReadOnly();

                // Set expiration time with some buffer (subtract 5 minutes for safety)
                _tokenExpirationTime = DateTime.UtcNow.AddSeconds(Math.Max(expiresIn - 300, expiresIn * 0.9));

                // Clear the original token from memory
                var tokenChars = token.ToCharArray();
                Array.Clear(tokenChars, 0, tokenChars.Length);

                _logger.LogInformation("Access token stored successfully, expires at {ExpirationTime} UTC", _tokenExpirationTime);
            }
        }

        /// <summary>
        /// Stores a refresh token securely with expiration time
        /// 安全地存储刷新令牌及其过期时间
        /// </summary>
        /// <param name="refreshToken">Refresh token to store</param>
        /// <param name="expiresIn">Token validity duration in seconds</param>
        /// <exception cref="ArgumentException">Thrown when refreshToken is null or empty</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when expiresIn is negative</exception>
        public void StoreRefreshToken(string refreshToken, int expiresIn)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                throw new ArgumentException("Refresh token cannot be null or empty", nameof(refreshToken));

            if (expiresIn < 0)
                throw new ArgumentOutOfRangeException(nameof(expiresIn), "Expiration time cannot be negative");

            lock (_lockObject)
            {
                ThrowIfDisposed();

                _logger.LogDebug("Storing refresh token with {ExpiresIn} seconds validity", expiresIn);

                // Clear existing refresh token
                ClearRefreshToken();

                // Store new refresh token
                _refreshToken = new SecureString();
                foreach (char c in refreshToken)
                {
                    _refreshToken.AppendChar(c);
                }
                _refreshToken.MakeReadOnly();

                // Set expiration time
                _refreshTokenExpirationTime = DateTime.UtcNow.AddSeconds(expiresIn);

                // Clear the original token from memory
                var tokenChars = refreshToken.ToCharArray();
                Array.Clear(tokenChars, 0, tokenChars.Length);

                _logger.LogInformation("Refresh token stored successfully, expires at {ExpirationTime} UTC", _refreshTokenExpirationTime);
            }
        }

        /// <summary>
        /// Retrieves the access token as a SecureString copy
        /// 以SecureString副本形式检索访问令牌
        /// </summary>
        /// <returns>Copy of the access token or null if not available</returns>
        public SecureString? GetAccessToken()
        {
            lock (_lockObject)
            {
                ThrowIfDisposed();

                if (_accessToken == null || DateTime.UtcNow >= _tokenExpirationTime)
                {
                    if (_accessToken != null)
                    {
                        _logger.LogWarning("Access token has expired");
                        OnTokenExpired(TokenType.Access);
                        ClearAccessToken();
                    }
                    return null;
                }

                return CreateSecureStringCopy(_accessToken);
            }
        }

        /// <summary>
        /// Retrieves the refresh token as a SecureString copy
        /// 以SecureString副本形式检索刷新令牌
        /// </summary>
        /// <returns>Copy of the refresh token or null if not available</returns>
        public SecureString? GetRefreshToken()
        {
            lock (_lockObject)
            {
                ThrowIfDisposed();

                if (_refreshToken == null || DateTime.UtcNow >= _refreshTokenExpirationTime)
                {
                    if (_refreshToken != null)
                    {
                        _logger.LogWarning("Refresh token has expired");
                        OnTokenExpired(TokenType.Refresh);
                        ClearRefreshToken();
                    }
                    return null;
                }

                return CreateSecureStringCopy(_refreshToken);
            }
        }

        /// <summary>
        /// Retrieves the access token as a plain string (use with caution)
        /// 以明文字符串形式检索访问令牌（谨慎使用）
        /// </summary>
        /// <returns>Access token string or null if not available</returns>
        public string? GetAccessTokenAsString()
        {
            using var secureToken = GetAccessToken();
            return secureToken != null ? ConvertSecureStringToString(secureToken) : null;
        }

        /// <summary>
        /// Checks if the access token will expire within the specified timespan
        /// 检查访问令牌是否会在指定时间内过期
        /// </summary>
        /// <param name="timespan">Time span to check</param>
        /// <returns>True if token will expire within the timespan</returns>
        public bool WillAccessTokenExpireWithin(TimeSpan timespan)
        {
            lock (_lockObject)
            {
                if (_accessToken == null)
                    return true;

                return DateTime.UtcNow.Add(timespan) >= _tokenExpirationTime;
            }
        }

        /// <summary>
        /// Clears the stored access token from memory
        /// 从内存中清除存储的访问令牌
        /// </summary>
        public void ClearAccessToken()
        {
            lock (_lockObject)
            {
                if (_accessToken != null)
                {
                    _logger.LogDebug("Clearing access token from memory");
                    _accessToken.Dispose();
                    _accessToken = null;
                    _tokenExpirationTime = DateTime.MinValue;
                }
            }
        }

        /// <summary>
        /// Clears the stored refresh token from memory
        /// 从内存中清除存储的刷新令牌
        /// </summary>
        public void ClearRefreshToken()
        {
            lock (_lockObject)
            {
                if (_refreshToken != null)
                {
                    _logger.LogDebug("Clearing refresh token from memory");
                    _refreshToken.Dispose();
                    _refreshToken = null;
                    _refreshTokenExpirationTime = DateTime.MinValue;
                }
            }
        }

        /// <summary>
        /// Clears all stored tokens from memory
        /// 从内存中清除所有存储的令牌
        /// </summary>
        public void ClearAllTokens()
        {
            lock (_lockObject)
            {
                _logger.LogInformation("Clearing all tokens from memory");
                ClearAccessToken();
                ClearRefreshToken();
            }
        }

        /// <summary>
        /// Creates a secure copy of a SecureString
        /// 创建SecureString的安全副本
        /// </summary>
        private SecureString CreateSecureStringCopy(SecureString original)
        {
            var copy = new SecureString();
            IntPtr ptr = IntPtr.Zero;

            try
            {
                ptr = Marshal.SecureStringToGlobalAllocUnicode(original);
                var chars = new char[original.Length];
                Marshal.Copy(ptr, chars, 0, chars.Length);

                foreach (char c in chars)
                {
                    copy.AppendChar(c);
                }

                Array.Clear(chars, 0, chars.Length);
                copy.MakeReadOnly();

                return copy;
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                {
                    Marshal.ZeroFreeGlobalAllocUnicode(ptr);
                }
            }
        }

        /// <summary>
        /// Converts SecureString to regular string (use with caution)
        /// 将SecureString转换为普通字符串（谨慎使用）
        /// </summary>
        private string ConvertSecureStringToString(SecureString secureString)
        {
            IntPtr ptr = IntPtr.Zero;
            try
            {
                ptr = Marshal.SecureStringToGlobalAllocUnicode(secureString);
                return Marshal.PtrToStringUni(ptr) ?? string.Empty;
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                {
                    Marshal.ZeroFreeGlobalAllocUnicode(ptr);
                }
            }
        }

        /// <summary>
        /// Raises the TokenExpired event
        /// 触发TokenExpired事件
        /// </summary>
        private void OnTokenExpired(TokenType tokenType)
        {
            try
            {
                TokenExpired?.Invoke(this, new TokenExpiredEventArgs(tokenType));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error firing TokenExpired event for {TokenType}", tokenType);
            }
        }

        /// <summary>
        /// Throws ObjectDisposedException if the instance is disposed
        /// 如果实例已释放则抛出ObjectDisposedException
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TokenStorage));
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
                        _logger.LogDebug("Disposing TokenStorage and clearing all tokens");
                        ClearAllTokens();
                        _disposed = true;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Event arguments for token expiration events
    /// 令牌过期事件的事件参数
    /// </summary>
    public class TokenExpiredEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the type of token that expired
        /// 获取过期的令牌类型
        /// </summary>
        public TokenType TokenType { get; }

        /// <summary>
        /// Gets the time when the token expired
        /// 获取令牌过期的时间
        /// </summary>
        public DateTime ExpiredAt { get; }

        /// <summary>
        /// Initializes a new instance of the TokenExpiredEventArgs class
        /// 初始化TokenExpiredEventArgs类的新实例
        /// </summary>
        /// <param name="tokenType">Type of token that expired</param>
        public TokenExpiredEventArgs(TokenType tokenType)
        {
            TokenType = tokenType;
            ExpiredAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Enumeration of token types
    /// 令牌类型枚举
    /// </summary>
    public enum TokenType
    {
        /// <summary>
        /// Access token
        /// 访问令牌
        /// </summary>
        Access,

        /// <summary>
        /// Refresh token
        /// 刷新令牌
        /// </summary>
        Refresh
    }
}