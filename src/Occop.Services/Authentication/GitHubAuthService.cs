using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Occop.Services.Authentication.Models;
using System.Security;

namespace Occop.Services.Authentication
{
    /// <summary>
    /// GitHub authentication service that implements OAuth Device Flow
    /// 实现OAuth设备流程的GitHub认证服务
    /// </summary>
    public class GitHubAuthService : IDisposable
    {
        private readonly OAuthDeviceFlow _deviceFlow;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GitHubAuthService> _logger;

        private SecureString? _accessToken;
        private DateTime _tokenExpirationTime;
        private string? _currentUserLogin;
        private string[]? _authorizedScopes;
        private bool _disposed = false;

        // Configuration keys
        private const string ClientIdKey = "GitHub:ClientId";
        private const string AllowedUsersKey = "GitHub:AllowedUsers";
        private const string DefaultScopesKey = "GitHub:DefaultScopes";

        // Default configuration values
        private const string DefaultScopes = "user:email,read:user";
        private const int TokenValidityHours = 24; // Assume tokens are valid for 24 hours

        /// <summary>
        /// Event fired when authentication status changes
        /// 认证状态改变时触发的事件
        /// </summary>
        public event EventHandler<AuthenticationStatusChangedEventArgs>? AuthenticationStatusChanged;

        /// <summary>
        /// Gets whether the user is currently authenticated
        /// 获取用户当前是否已认证
        /// </summary>
        public bool IsAuthenticated => _accessToken != null && DateTime.UtcNow < _tokenExpirationTime;

        /// <summary>
        /// Gets the currently authenticated user's login name
        /// 获取当前已认证用户的登录名
        /// </summary>
        public string? CurrentUserLogin => IsAuthenticated ? _currentUserLogin : null;

        /// <summary>
        /// Gets the authorized OAuth scopes for the current token
        /// 获取当前令牌的授权OAuth范围
        /// </summary>
        public string[]? AuthorizedScopes => IsAuthenticated ? _authorizedScopes : null;

        /// <summary>
        /// Initializes a new instance of the GitHubAuthService class
        /// 初始化GitHub认证服务类的新实例
        /// </summary>
        /// <param name="deviceFlow">OAuth device flow implementation</param>
        /// <param name="configuration">Application configuration</param>
        /// <param name="logger">Logger instance</param>
        public GitHubAuthService(
            OAuthDeviceFlow deviceFlow,
            IConfiguration configuration,
            ILogger<GitHubAuthService> logger)
        {
            _deviceFlow = deviceFlow ?? throw new ArgumentNullException(nameof(deviceFlow));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Starts the authentication process using GitHub OAuth Device Flow
        /// 使用GitHub OAuth设备流程开始认证过程
        /// </summary>
        /// <param name="scopes">OAuth scopes to request (optional)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Device code information for user authorization</returns>
        public async Task<DeviceAuthorizationResult> StartAuthenticationAsync(
            string? scopes = null,
            CancellationToken cancellationToken = default)
        {
            var clientId = GetClientId();
            var requestedScopes = scopes ?? GetDefaultScopes();

            _logger.LogInformation("Starting GitHub authentication for scopes: {Scopes}", requestedScopes);

            try
            {
                // Request device code
                var deviceCodeResponse = await _deviceFlow.RequestDeviceCodeAsync(
                    clientId,
                    requestedScopes,
                    cancellationToken);

                _logger.LogInformation("Device authorization started. User code: {UserCode}, Verification URI: {Uri}",
                    deviceCodeResponse.UserCode, deviceCodeResponse.VerificationUri);

                OnAuthenticationStatusChanged(AuthenticationStatus.AuthorizationPending,
                    $"等待用户授权。请访问 {deviceCodeResponse.VerificationUri} 并输入代码: {deviceCodeResponse.UserCode}");

                return new DeviceAuthorizationResult
                {
                    DeviceCode = deviceCodeResponse.DeviceCode,
                    UserCode = deviceCodeResponse.UserCode,
                    VerificationUri = deviceCodeResponse.VerificationUri,
                    VerificationUriComplete = deviceCodeResponse.VerificationUriComplete,
                    ExpiresIn = deviceCodeResponse.ExpiresIn,
                    Interval = deviceCodeResponse.Interval
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start authentication");
                OnAuthenticationStatusChanged(AuthenticationStatus.Failed, $"启动认证失败: {ex.Message}");
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
            if (string.IsNullOrWhiteSpace(deviceCode))
                throw new ArgumentException("Device code cannot be null or empty", nameof(deviceCode));

            var clientId = GetClientId();

            _logger.LogInformation("Completing authentication with device code");

            try
            {
                OnAuthenticationStatusChanged(AuthenticationStatus.PollingForToken, "正在轮询访问令牌...");

                // Poll for access token
                var tokenResponse = await _deviceFlow.PollForAccessTokenAsync(
                    clientId,
                    deviceCode,
                    intervalSeconds,
                    timeoutSeconds,
                    cancellationToken);

                if (!tokenResponse.IsSuccess)
                {
                    var errorMessage = tokenResponse.GetUserFriendlyErrorMessage();
                    _logger.LogWarning("Authentication failed: {Error}", errorMessage);

                    var status = tokenResponse.IsAccessDenied ? AuthenticationStatus.Denied : AuthenticationStatus.Failed;
                    OnAuthenticationStatusChanged(status, errorMessage);

                    return new AuthenticationResult
                    {
                        Success = false,
                        ErrorMessage = errorMessage,
                        ErrorCode = tokenResponse.Error
                    };
                }

                // Validate the token and get user information
                var isValidToken = await _deviceFlow.ValidateAccessTokenAsync(tokenResponse.AccessToken, cancellationToken);
                if (!isValidToken)
                {
                    const string errorMessage = "获取的访问令牌无效";
                    _logger.LogError(errorMessage);
                    OnAuthenticationStatusChanged(AuthenticationStatus.Failed, errorMessage);

                    return new AuthenticationResult
                    {
                        Success = false,
                        ErrorMessage = errorMessage
                    };
                }

                // Get user information for whitelist validation
                var userInfo = await GetUserInfoAsync(tokenResponse.AccessToken, cancellationToken);
                if (userInfo == null)
                {
                    const string errorMessage = "无法获取用户信息";
                    _logger.LogError(errorMessage);
                    OnAuthenticationStatusChanged(AuthenticationStatus.Failed, errorMessage);

                    return new AuthenticationResult
                    {
                        Success = false,
                        ErrorMessage = errorMessage
                    };
                }

                // Validate user against whitelist
                if (!IsUserAllowed(userInfo.Login))
                {
                    var errorMessage = $"用户 {userInfo.Login} 不在允许列表中";
                    _logger.LogWarning("User {Login} is not in the allowed users list", userInfo.Login);
                    OnAuthenticationStatusChanged(AuthenticationStatus.Forbidden, errorMessage);

                    return new AuthenticationResult
                    {
                        Success = false,
                        ErrorMessage = errorMessage,
                        UserLogin = userInfo.Login
                    };
                }

                // Store token securely
                StoreAccessToken(tokenResponse.AccessToken);
                _currentUserLogin = userInfo.Login;
                _authorizedScopes = tokenResponse.GetScopes();
                _tokenExpirationTime = DateTime.UtcNow.AddHours(TokenValidityHours);

                _logger.LogInformation("Authentication successful for user: {Login}", userInfo.Login);
                OnAuthenticationStatusChanged(AuthenticationStatus.Authenticated, $"认证成功，欢迎 {userInfo.Login}!");

                return new AuthenticationResult
                {
                    Success = true,
                    UserLogin = userInfo.Login,
                    Scopes = _authorizedScopes
                };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Authentication was cancelled");
                OnAuthenticationStatusChanged(AuthenticationStatus.Cancelled, "认证已取消");
                throw;
            }
            catch (TimeoutException ex)
            {
                _logger.LogWarning(ex, "Authentication timeout");
                OnAuthenticationStatusChanged(AuthenticationStatus.Timeout, "认证超时");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Authentication failed with unexpected error");
                OnAuthenticationStatusChanged(AuthenticationStatus.Failed, $"认证失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Signs out the current user and clears stored tokens
        /// 登出当前用户并清除存储的令牌
        /// </summary>
        public void SignOut()
        {
            _logger.LogInformation("Signing out user: {Login}", _currentUserLogin);

            ClearStoredToken();
            _currentUserLogin = null;
            _authorizedScopes = null;
            _tokenExpirationTime = DateTime.MinValue;

            OnAuthenticationStatusChanged(AuthenticationStatus.SignedOut, "已成功登出");
        }

        /// <summary>
        /// Gets the current access token as a SecureString
        /// 以SecureString形式获取当前访问令牌
        /// </summary>
        /// <returns>Access token or null if not authenticated</returns>
        public SecureString? GetAccessToken()
        {
            if (!IsAuthenticated || _accessToken == null)
                return null;

            // Create a copy to avoid sharing the internal SecureString
            var copy = new SecureString();
            var ptr = System.Runtime.InteropServices.Marshal.SecureStringToGlobalAllocUnicode(_accessToken);
            try
            {
                var chars = new char[_accessToken.Length];
                System.Runtime.InteropServices.Marshal.Copy(ptr, chars, 0, chars.Length);

                foreach (var c in chars)
                {
                    copy.AppendChar(c);
                }

                Array.Clear(chars, 0, chars.Length);
                copy.MakeReadOnly();

                return copy;
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.ZeroFreeGlobalAllocUnicode(ptr);
            }
        }

        /// <summary>
        /// Gets the GitHub client ID from configuration
        /// 从配置中获取GitHub客户端ID
        /// </summary>
        private string GetClientId()
        {
            var clientId = _configuration[ClientIdKey];
            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new InvalidOperationException($"GitHub client ID not configured. Please set '{ClientIdKey}' in configuration.");
            }
            return clientId;
        }

        /// <summary>
        /// Gets the default OAuth scopes from configuration
        /// 从配置中获取默认OAuth范围
        /// </summary>
        private string GetDefaultScopes()
        {
            return _configuration[DefaultScopesKey] ?? DefaultScopes;
        }

        /// <summary>
        /// Checks if a user is in the allowed users list
        /// 检查用户是否在允许用户列表中
        /// </summary>
        private bool IsUserAllowed(string login)
        {
            var allowedUsers = _configuration.GetSection(AllowedUsersKey).Get<string[]>();

            // If no whitelist is configured, allow all users
            if (allowedUsers == null || allowedUsers.Length == 0)
            {
                _logger.LogWarning("No user whitelist configured, allowing all users");
                return true;
            }

            return allowedUsers.Contains(login, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Stores the access token securely
        /// 安全地存储访问令牌
        /// </summary>
        private void StoreAccessToken(string token)
        {
            ClearStoredToken();

            _accessToken = new SecureString();
            foreach (var c in token)
            {
                _accessToken.AppendChar(c);
            }
            _accessToken.MakeReadOnly();

            // Clear the original token from memory
            var tokenChars = token.ToCharArray();
            Array.Clear(tokenChars, 0, tokenChars.Length);
        }

        /// <summary>
        /// Clears the stored access token from memory
        /// 从内存中清除存储的访问令牌
        /// </summary>
        private void ClearStoredToken()
        {
            _accessToken?.Dispose();
            _accessToken = null;
        }

        /// <summary>
        /// Gets user information from GitHub API using the access token
        /// 使用访问令牌从GitHub API获取用户信息
        /// </summary>
        private async Task<GitHubUserInfo?> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken)
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("token", accessToken);
                client.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("Occop", "1.0"));

                var response = await client.GetAsync("https://api.github.com/user", cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get user info from GitHub API. Status: {StatusCode}", response.StatusCode);
                    return null;
                }

                var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var userInfo = System.Text.Json.JsonSerializer.Deserialize<GitHubUserInfo>(jsonContent, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return userInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user info from GitHub API");
                return null;
            }
        }

        /// <summary>
        /// Raises the AuthenticationStatusChanged event
        /// 触发AuthenticationStatusChanged事件
        /// </summary>
        private void OnAuthenticationStatusChanged(AuthenticationStatus status, string message)
        {
            AuthenticationStatusChanged?.Invoke(this, new AuthenticationStatusChangedEventArgs(status, message));
        }

        /// <summary>
        /// Disposes of managed resources
        /// 释放托管资源
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                ClearStoredToken();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Represents the result of starting device authorization
    /// 表示启动设备授权的结果
    /// </summary>
    public class DeviceAuthorizationResult
    {
        public string DeviceCode { get; set; } = string.Empty;
        public string UserCode { get; set; } = string.Empty;
        public string VerificationUri { get; set; } = string.Empty;
        public string? VerificationUriComplete { get; set; }
        public int ExpiresIn { get; set; }
        public int Interval { get; set; }
    }

    /// <summary>
    /// Represents the result of authentication
    /// 表示认证结果
    /// </summary>
    public class AuthenticationResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ErrorCode { get; set; }
        public string? UserLogin { get; set; }
        public string[]? Scopes { get; set; }
    }

    /// <summary>
    /// Authentication status enumeration
    /// 认证状态枚举
    /// </summary>
    public enum AuthenticationStatus
    {
        NotStarted,
        AuthorizationPending,
        PollingForToken,
        Authenticated,
        Failed,
        Denied,
        Forbidden,
        Cancelled,
        Timeout,
        SignedOut
    }

    /// <summary>
    /// Event arguments for authentication status changes
    /// 认证状态改变的事件参数
    /// </summary>
    public class AuthenticationStatusChangedEventArgs : EventArgs
    {
        public AuthenticationStatus Status { get; }
        public string Message { get; }

        public AuthenticationStatusChangedEventArgs(AuthenticationStatus status, string message)
        {
            Status = status;
            Message = message;
        }
    }

    /// <summary>
    /// GitHub user information model
    /// GitHub用户信息模型
    /// </summary>
    public class GitHubUserInfo
    {
        [System.Text.Json.Serialization.JsonPropertyName("login")]
        public string Login { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public long Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("email")]
        public string? Email { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}