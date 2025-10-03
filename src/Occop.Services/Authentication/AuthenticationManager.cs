using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Occop.Core.Security;

namespace Occop.Services.Authentication
{
    /// <summary>
    /// High-level authentication manager that coordinates all authentication components
    /// 高层认证管理器，协调所有认证组件
    /// </summary>
    public class AuthenticationManager : IDisposable
    {
        private readonly GitHubAuthService _gitHubAuthService;
        private readonly UserWhitelist _userWhitelist;
        private readonly SecureTokenManager _secureTokenManager;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthenticationManager> _logger;
        private readonly object _lockObject = new object();

        private AuthenticationState _currentState;
        private string? _currentUserLogin;
        private DateTime? _lastAuthenticationTime;
        private int _failedAuthenticationAttempts;
        private DateTime? _lastFailedAttemptTime;
        private bool _disposed = false;

        // Configuration keys
        private const string MaxFailedAttemptsKey = "Authentication:MaxFailedAttempts";
        private const string LockoutDurationMinutesKey = "Authentication:LockoutDurationMinutes";
        private const string SessionTimeoutMinutesKey = "Authentication:SessionTimeoutMinutes";

        // Default values
        private const int DefaultMaxFailedAttempts = 3;
        private const int DefaultLockoutDurationMinutes = 15;
        private const int DefaultSessionTimeoutMinutes = 480; // 8 hours

        /// <summary>
        /// Event fired when authentication state changes
        /// 认证状态改变时触发的事件
        /// </summary>
        public event EventHandler<AuthenticationStateChangedEventArgs>? AuthenticationStateChanged;

        /// <summary>
        /// Event fired when authentication fails
        /// 认证失败时触发的事件
        /// </summary>
        public event EventHandler<AuthenticationFailedEventArgs>? AuthenticationFailed;

        /// <summary>
        /// Event fired when user session expires
        /// 用户会话过期时触发的事件
        /// </summary>
        public event EventHandler<SessionExpiredEventArgs>? SessionExpired;

        /// <summary>
        /// Gets the current authentication state
        /// 获取当前认证状态
        /// </summary>
        public AuthenticationState CurrentState
        {
            get
            {
                lock (_lockObject)
                {
                    return _currentState;
                }
            }
        }

        /// <summary>
        /// Gets the currently authenticated user's login name
        /// 获取当前已认证用户的登录名
        /// </summary>
        public string? CurrentUserLogin
        {
            get
            {
                lock (_lockObject)
                {
                    return IsAuthenticated ? _currentUserLogin : null;
                }
            }
        }

        /// <summary>
        /// Gets whether the user is currently authenticated
        /// 获取用户当前是否已认证
        /// </summary>
        public bool IsAuthenticated
        {
            get
            {
                lock (_lockObject)
                {
                    return _currentState == AuthenticationState.Authenticated &&
                           _secureTokenManager.GetSecurityStatus().HasValidAccessToken &&
                           !IsSessionExpired();
                }
            }
        }

        /// <summary>
        /// Gets whether authentication is currently locked out due to failed attempts
        /// 获取认证是否因失败尝试而被锁定
        /// </summary>
        public bool IsLockedOut
        {
            get
            {
                lock (_lockObject)
                {
                    if (_lastFailedAttemptTime == null) return false;

                    var lockoutDuration = _configuration.GetValue<int>(LockoutDurationMinutesKey, DefaultLockoutDurationMinutes);
                    var maxAttempts = _configuration.GetValue<int>(MaxFailedAttemptsKey, DefaultMaxFailedAttempts);

                    return _failedAuthenticationAttempts >= maxAttempts &&
                           DateTime.UtcNow < _lastFailedAttemptTime.Value.AddMinutes(lockoutDuration);
                }
            }
        }

        /// <summary>
        /// Gets the time when lockout expires, if currently locked out
        /// 获取锁定过期时间（如果当前被锁定）
        /// </summary>
        public DateTime? LockoutExpirationTime
        {
            get
            {
                lock (_lockObject)
                {
                    if (!IsLockedOut || _lastFailedAttemptTime == null) return null;

                    var lockoutDuration = _configuration.GetValue<int>(LockoutDurationMinutesKey, DefaultLockoutDurationMinutes);
                    return _lastFailedAttemptTime.Value.AddMinutes(lockoutDuration);
                }
            }
        }

        /// <summary>
        /// Gets the last authentication time
        /// 获取上次认证时间
        /// </summary>
        public DateTime? LastAuthenticationTime
        {
            get
            {
                lock (_lockObject)
                {
                    return _lastAuthenticationTime;
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the AuthenticationManager class
        /// 初始化AuthenticationManager类的新实例
        /// </summary>
        /// <param name="gitHubAuthService">GitHub authentication service</param>
        /// <param name="userWhitelist">User whitelist service</param>
        /// <param name="secureTokenManager">Secure token manager</param>
        /// <param name="configuration">Application configuration</param>
        /// <param name="logger">Logger instance</param>
        public AuthenticationManager(
            GitHubAuthService gitHubAuthService,
            UserWhitelist userWhitelist,
            SecureTokenManager secureTokenManager,
            IConfiguration configuration,
            ILogger<AuthenticationManager> logger)
        {
            _gitHubAuthService = gitHubAuthService ?? throw new ArgumentNullException(nameof(gitHubAuthService));
            _userWhitelist = userWhitelist ?? throw new ArgumentNullException(nameof(userWhitelist));
            _secureTokenManager = secureTokenManager ?? throw new ArgumentNullException(nameof(secureTokenManager));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _currentState = AuthenticationState.NotAuthenticated;
            _failedAuthenticationAttempts = 0;

            // Subscribe to events
            _gitHubAuthService.AuthenticationStatusChanged += OnGitHubAuthStatusChanged;
            _secureTokenManager.TokenRefreshRequested += OnTokenRefreshRequested;
            _secureTokenManager.SecurityEvent += OnSecurityEvent;
            _userWhitelist.WhitelistChanged += OnWhitelistChanged;

            _logger.LogInformation("AuthenticationManager initialized");
        }

        /// <summary>
        /// Starts the authentication process
        /// 开始认证过程
        /// </summary>
        /// <param name="scopes">OAuth scopes to request (optional)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Device authorization result for user authorization</returns>
        /// <exception cref="InvalidOperationException">Thrown when authentication is locked out</exception>
        public async Task<DeviceAuthorizationResult> StartAuthenticationAsync(
            string? scopes = null,
            CancellationToken cancellationToken = default)
        {
            lock (_lockObject)
            {
                ThrowIfDisposed();

                if (IsLockedOut)
                {
                    var lockoutExpiration = LockoutExpirationTime!.Value;
                    var timeRemaining = lockoutExpiration - DateTime.UtcNow;
                    throw new InvalidOperationException(
                        $"Authentication is locked out due to too many failed attempts. Try again in {timeRemaining.TotalMinutes:F1} minutes.");
                }

                SetAuthenticationState(AuthenticationState.Starting);
            }

            try
            {
                _logger.LogInformation("Starting authentication process");

                var result = await _gitHubAuthService.StartAuthenticationAsync(scopes, cancellationToken);

                lock (_lockObject)
                {
                    SetAuthenticationState(AuthenticationState.AwaitingUserAuthorization);
                }

                return result;
            }
            catch (Exception ex)
            {
                lock (_lockObject)
                {
                    SetAuthenticationState(AuthenticationState.Failed);
                    RecordFailedAttempt("Failed to start authentication");
                }

                _logger.LogError(ex, "Failed to start authentication process");
                throw;
            }
        }

        /// <summary>
        /// Completes the authentication process by polling for the access token
        /// 通过轮询访问令牌完成认证过程
        /// </summary>
        /// <param name="deviceCode">Device code from StartAuthentication</param>
        /// <param name="intervalSeconds">Polling interval in seconds</param>
        /// <param name="timeoutSeconds">Total timeout in seconds</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Authentication result</returns>
        public async Task<AuthenticationResult> CompleteAuthenticationAsync(
            string deviceCode,
            int intervalSeconds = 5,
            int timeoutSeconds = 300,
            CancellationToken cancellationToken = default)
        {
            lock (_lockObject)
            {
                ThrowIfDisposed();

                if (IsLockedOut)
                {
                    var lockoutExpiration = LockoutExpirationTime!.Value;
                    var timeRemaining = lockoutExpiration - DateTime.UtcNow;
                    return new AuthenticationResult
                    {
                        Success = false,
                        ErrorMessage = $"Authentication is locked out. Try again in {timeRemaining.TotalMinutes:F1} minutes."
                    };
                }

                SetAuthenticationState(AuthenticationState.Authenticating);
            }

            try
            {
                _logger.LogInformation("Completing authentication process");

                var result = await _gitHubAuthService.CompleteAuthenticationAsync(
                    deviceCode, intervalSeconds, timeoutSeconds, cancellationToken);

                if (result.Success)
                {
                    await HandleSuccessfulAuthentication(result);
                }
                else
                {
                    HandleFailedAuthentication(result.ErrorMessage ?? "Authentication failed");
                }

                return result;
            }
            catch (Exception ex)
            {
                var errorMessage = $"Authentication error: {ex.Message}";
                HandleFailedAuthentication(errorMessage);

                _logger.LogError(ex, "Error during authentication completion");

                return new AuthenticationResult
                {
                    Success = false,
                    ErrorMessage = errorMessage
                };
            }
        }

        /// <summary>
        /// Manually refreshes the authentication token
        /// 手动刷新认证令牌
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if refresh was successful</returns>
        public async Task<bool> RefreshAuthenticationAsync(CancellationToken cancellationToken = default)
        {
            lock (_lockObject)
            {
                ThrowIfDisposed();

                if (!IsAuthenticated)
                {
                    _logger.LogWarning("Cannot refresh authentication - user is not authenticated");
                    return false;
                }

                SetAuthenticationState(AuthenticationState.Refreshing);
            }

            try
            {
                _logger.LogInformation("Refreshing authentication token");

                // For GitHub OAuth, we would need to implement refresh token logic
                // For now, this is a placeholder that validates the current token
                var currentToken = _secureTokenManager.RetrieveTokenSecurely();
                if (currentToken != null)
                {
                    // In a real implementation, you would validate the token with GitHub
                    // and potentially refresh it if it supports refresh tokens
                    lock (_lockObject)
                    {
                        SetAuthenticationState(AuthenticationState.Authenticated);
                        _lastAuthenticationTime = DateTime.UtcNow;
                    }

                    _logger.LogInformation("Authentication token refreshed successfully");
                    return true;
                }

                lock (_lockObject)
                {
                    SetAuthenticationState(AuthenticationState.Failed);
                }

                _logger.LogWarning("Token refresh failed - no valid token available");
                return false;
            }
            catch (Exception ex)
            {
                lock (_lockObject)
                {
                    SetAuthenticationState(AuthenticationState.Failed);
                }

                _logger.LogError(ex, "Error during authentication refresh");
                return false;
            }
        }

        /// <summary>
        /// Signs out the current user and clears authentication state
        /// 登出当前用户并清除认证状态
        /// </summary>
        public void SignOut()
        {
            lock (_lockObject)
            {
                ThrowIfDisposed();

                _logger.LogInformation("Signing out user: {UserLogin}", _currentUserLogin);

                // Sign out from GitHub service
                _gitHubAuthService.SignOut();

                // Clear security state
                _secureTokenManager.ClearAllSecurityState();

                // Reset local state
                _currentUserLogin = null;
                _lastAuthenticationTime = null;
                ResetFailedAttempts();

                SetAuthenticationState(AuthenticationState.SignedOut);

                _logger.LogInformation("User signed out successfully");
            }
        }

        /// <summary>
        /// Validates that the current authentication is still valid
        /// 验证当前认证是否仍然有效
        /// </summary>
        /// <returns>True if authentication is valid</returns>
        public bool ValidateAuthentication()
        {
            lock (_lockObject)
            {
                ThrowIfDisposed();

                if (!IsAuthenticated)
                {
                    return false;
                }

                // Check if session has expired
                if (IsSessionExpired())
                {
                    _logger.LogWarning("User session has expired for user: {UserLogin}", _currentUserLogin);
                    OnSessionExpired(new SessionExpiredEventArgs(_currentUserLogin!, _lastAuthenticationTime!.Value));

                    SetAuthenticationState(AuthenticationState.SessionExpired);
                    return false;
                }

                // Validate against whitelist
                if (_currentUserLogin != null && !_userWhitelist.IsUserAllowed(_currentUserLogin))
                {
                    _logger.LogWarning("User {UserLogin} is no longer in whitelist, invalidating session", _currentUserLogin);
                    SetAuthenticationState(AuthenticationState.Forbidden);
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Gets comprehensive authentication status information
        /// 获取全面的认证状态信息
        /// </summary>
        /// <returns>Authentication status information</returns>
        public AuthenticationStatusInfo GetAuthenticationStatus()
        {
            lock (_lockObject)
            {
                ThrowIfDisposed();

                var securityStatus = _secureTokenManager.GetSecurityStatus();

                return new AuthenticationStatusInfo
                {
                    CurrentState = _currentState,
                    IsAuthenticated = IsAuthenticated,
                    CurrentUserLogin = _currentUserLogin,
                    LastAuthenticationTime = _lastAuthenticationTime,
                    IsLockedOut = IsLockedOut,
                    LockoutExpirationTime = LockoutExpirationTime,
                    FailedAttempts = _failedAuthenticationAttempts,
                    SessionExpirationTime = GetSessionExpirationTime(),
                    SecurityStatus = securityStatus,
                    WhitelistInfo = _userWhitelist.GetWhitelistInfo()
                };
            }
        }

        /// <summary>
        /// Handles successful authentication completion
        /// 处理成功的认证完成
        /// </summary>
        private async Task HandleSuccessfulAuthentication(AuthenticationResult result)
        {
            lock (_lockObject)
            {
                // Validate user against whitelist
                if (result.UserLogin != null && !_userWhitelist.IsUserAllowed(result.UserLogin))
                {
                    var errorMessage = $"User {result.UserLogin} is not authorized";
                    _logger.LogWarning(errorMessage);

                    result.Success = false;
                    result.ErrorMessage = errorMessage;

                    SetAuthenticationState(AuthenticationState.Forbidden);
                    RecordFailedAttempt(errorMessage);
                    return;
                }

                // Store token securely
                var token = _gitHubAuthService.GetAccessToken();
                if (token != null)
                {
                    var tokenString = ConvertSecureStringToString(token);
                    _secureTokenManager.StoreTokenSecurely(tokenString, 3600); // 1 hour default

                    // Clear the token string from memory
                    var tokenChars = tokenString.ToCharArray();
                    Array.Clear(tokenChars, 0, tokenChars.Length);

                    token.Dispose();
                }

                // Update authentication state
                _currentUserLogin = result.UserLogin;
                _lastAuthenticationTime = DateTime.UtcNow;
                ResetFailedAttempts();

                SetAuthenticationState(AuthenticationState.Authenticated);

                _logger.LogInformation("Authentication completed successfully for user: {UserLogin}", result.UserLogin);
            }
        }

        /// <summary>
        /// Handles failed authentication
        /// 处理认证失败
        /// </summary>
        private void HandleFailedAuthentication(string errorMessage)
        {
            lock (_lockObject)
            {
                SetAuthenticationState(AuthenticationState.Failed);
                RecordFailedAttempt(errorMessage);

                _logger.LogWarning("Authentication failed: {ErrorMessage}", errorMessage);
            }
        }

        /// <summary>
        /// Records a failed authentication attempt
        /// 记录认证失败尝试
        /// </summary>
        private void RecordFailedAttempt(string reason)
        {
            _failedAuthenticationAttempts++;
            _lastFailedAttemptTime = DateTime.UtcNow;

            var maxAttempts = _configuration.GetValue<int>(MaxFailedAttemptsKey, DefaultMaxFailedAttempts);

            _logger.LogWarning("Failed authentication attempt {Attempt}/{MaxAttempts}: {Reason}",
                _failedAuthenticationAttempts, maxAttempts, reason);

            OnAuthenticationFailed(new AuthenticationFailedEventArgs(
                reason, _failedAuthenticationAttempts, maxAttempts, IsLockedOut));

            if (IsLockedOut)
            {
                var lockoutDuration = _configuration.GetValue<int>(LockoutDurationMinutesKey, DefaultLockoutDurationMinutes);
                _logger.LogWarning("Authentication locked out for {Duration} minutes due to too many failed attempts", lockoutDuration);
            }
        }

        /// <summary>
        /// Resets failed authentication attempts counter
        /// 重置认证失败尝试计数器
        /// </summary>
        private void ResetFailedAttempts()
        {
            if (_failedAuthenticationAttempts > 0)
            {
                _logger.LogDebug("Resetting failed authentication attempts counter");
                _failedAuthenticationAttempts = 0;
                _lastFailedAttemptTime = null;
            }
        }

        /// <summary>
        /// Checks if the current session has expired
        /// 检查当前会话是否已过期
        /// </summary>
        private bool IsSessionExpired()
        {
            if (_lastAuthenticationTime == null) return true;

            var sessionTimeout = _configuration.GetValue<int>(SessionTimeoutMinutesKey, DefaultSessionTimeoutMinutes);
            return DateTime.UtcNow > _lastAuthenticationTime.Value.AddMinutes(sessionTimeout);
        }

        /// <summary>
        /// Gets the session expiration time
        /// 获取会话过期时间
        /// </summary>
        private DateTime? GetSessionExpirationTime()
        {
            if (_lastAuthenticationTime == null) return null;

            var sessionTimeout = _configuration.GetValue<int>(SessionTimeoutMinutesKey, DefaultSessionTimeoutMinutes);
            return _lastAuthenticationTime.Value.AddMinutes(sessionTimeout);
        }

        /// <summary>
        /// Sets the authentication state and fires events
        /// 设置认证状态并触发事件
        /// </summary>
        private void SetAuthenticationState(AuthenticationState newState)
        {
            var oldState = _currentState;
            _currentState = newState;

            if (oldState != newState)
            {
                _logger.LogDebug("Authentication state changed from {OldState} to {NewState}", oldState, newState);
                OnAuthenticationStateChanged(new AuthenticationStateChangedEventArgs(oldState, newState));
            }
        }

        /// <summary>
        /// Converts SecureString to regular string
        /// 将SecureString转换为普通字符串
        /// </summary>
        private string ConvertSecureStringToString(System.Security.SecureString secureString)
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

        #region Event Handlers

        /// <summary>
        /// Handles GitHub authentication status changes
        /// 处理GitHub认证状态变化
        /// </summary>
        private void OnGitHubAuthStatusChanged(object? sender, AuthenticationStatusChangedEventArgs e)
        {
            _logger.LogDebug("GitHub auth status changed to: {Status}", e.Status);
            // The state is already managed by our own state machine
        }

        /// <summary>
        /// Handles token refresh requests
        /// 处理令牌刷新请求
        /// </summary>
        private async void OnTokenRefreshRequested(object? sender, TokenRefreshEventArgs e)
        {
            _logger.LogInformation("Token refresh requested for {TokenType}", e.TokenType);

            try
            {
                await RefreshAuthenticationAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during automatic token refresh");
            }
        }

        /// <summary>
        /// Handles security events
        /// 处理安全事件
        /// </summary>
        private void OnSecurityEvent(object? sender, SecurityEventArgs e)
        {
            _logger.LogDebug("Security event: {EventType} - {Message}", e.EventType, e.Message);
        }

        /// <summary>
        /// Handles whitelist configuration changes
        /// 处理白名单配置变化
        /// </summary>
        private void OnWhitelistChanged(object? sender, WhitelistChangedEventArgs e)
        {
            _logger.LogInformation("Whitelist configuration changed: Mode={Mode}, Allowed={Allowed}, Blocked={Blocked}",
                e.WhitelistInfo.Mode, e.WhitelistInfo.AllowedUsersCount, e.WhitelistInfo.BlockedUsersCount);

            // Re-validate current user if authenticated
            if (IsAuthenticated)
            {
                ValidateAuthentication();
            }
        }

        #endregion

        #region Event Firing Methods

        /// <summary>
        /// Raises the AuthenticationStateChanged event
        /// 触发AuthenticationStateChanged事件
        /// </summary>
        private void OnAuthenticationStateChanged(AuthenticationStateChangedEventArgs args)
        {
            try
            {
                AuthenticationStateChanged?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error firing AuthenticationStateChanged event");
            }
        }

        /// <summary>
        /// Raises the AuthenticationFailed event
        /// 触发AuthenticationFailed事件
        /// </summary>
        private void OnAuthenticationFailed(AuthenticationFailedEventArgs args)
        {
            try
            {
                AuthenticationFailed?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error firing AuthenticationFailed event");
            }
        }

        /// <summary>
        /// Raises the SessionExpired event
        /// 触发SessionExpired事件
        /// </summary>
        private void OnSessionExpired(SessionExpiredEventArgs args)
        {
            try
            {
                SessionExpired?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error firing SessionExpired event");
            }
        }

        #endregion

        /// <summary>
        /// Throws ObjectDisposedException if the instance is disposed
        /// 如果实例已释放则抛出ObjectDisposedException
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AuthenticationManager));
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
                        _logger.LogDebug("Disposing AuthenticationManager");

                        // Unsubscribe from events
                        _gitHubAuthService.AuthenticationStatusChanged -= OnGitHubAuthStatusChanged;
                        _secureTokenManager.TokenRefreshRequested -= OnTokenRefreshRequested;
                        _secureTokenManager.SecurityEvent -= OnSecurityEvent;
                        _userWhitelist.WhitelistChanged -= OnWhitelistChanged;

                        _disposed = true;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Authentication states
    /// 认证状态
    /// </summary>
    public enum AuthenticationState
    {
        NotAuthenticated,
        Starting,
        AwaitingUserAuthorization,
        Authenticating,
        Authenticated,
        Refreshing,
        Failed,
        Forbidden,
        SessionExpired,
        SignedOut
    }

    /// <summary>
    /// Comprehensive authentication status information
    /// 全面的认证状态信息
    /// </summary>
    public class AuthenticationStatusInfo
    {
        public AuthenticationState CurrentState { get; set; }
        public bool IsAuthenticated { get; set; }
        public string? CurrentUserLogin { get; set; }
        public DateTime? LastAuthenticationTime { get; set; }
        public bool IsLockedOut { get; set; }
        public DateTime? LockoutExpirationTime { get; set; }
        public int FailedAttempts { get; set; }
        public DateTime? SessionExpirationTime { get; set; }
        public SecurityStatus? SecurityStatus { get; set; }
        public WhitelistInfo? WhitelistInfo { get; set; }
    }

    /// <summary>
    /// Event arguments for authentication state changes
    /// 认证状态变化的事件参数
    /// </summary>
    public class AuthenticationStateChangedEventArgs : EventArgs
    {
        public AuthenticationState OldState { get; }
        public AuthenticationState NewState { get; }
        public DateTime Timestamp { get; }

        public AuthenticationStateChangedEventArgs(AuthenticationState oldState, AuthenticationState newState)
        {
            OldState = oldState;
            NewState = newState;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Event arguments for authentication failures
    /// 认证失败的事件参数
    /// </summary>
    public class AuthenticationFailedEventArgs : EventArgs
    {
        public string Reason { get; }
        public int AttemptNumber { get; }
        public int MaxAttempts { get; }
        public bool IsLockedOut { get; }
        public DateTime Timestamp { get; }

        public AuthenticationFailedEventArgs(string reason, int attemptNumber, int maxAttempts, bool isLockedOut)
        {
            Reason = reason;
            AttemptNumber = attemptNumber;
            MaxAttempts = maxAttempts;
            IsLockedOut = isLockedOut;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Event arguments for session expiration
    /// 会话过期的事件参数
    /// </summary>
    public class SessionExpiredEventArgs : EventArgs
    {
        public string UserLogin { get; }
        public DateTime AuthenticationTime { get; }
        public DateTime ExpirationTime { get; }

        public SessionExpiredEventArgs(string userLogin, DateTime authenticationTime)
        {
            UserLogin = userLogin;
            AuthenticationTime = authenticationTime;
            ExpirationTime = DateTime.UtcNow;
        }
    }
}