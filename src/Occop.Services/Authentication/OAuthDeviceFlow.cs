using Microsoft.Extensions.Logging;
using Occop.Services.Authentication.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Occop.Services.Authentication
{
    /// <summary>
    /// Implements GitHub OAuth Device Flow
    /// 实现GitHub OAuth设备流程
    /// </summary>
    public class OAuthDeviceFlow
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OAuthDeviceFlow> _logger;

        // GitHub OAuth endpoints
        private const string DeviceCodeEndpoint = "https://github.com/login/device/code";
        private const string AccessTokenEndpoint = "https://github.com/login/oauth/access_token";

        // Default values for device flow
        private const int DefaultTimeoutSeconds = 300; // 5 minutes
        private const int DefaultIntervalSeconds = 5;
        private const int MaxRetryAttempts = 3;
        private const int SlowDownAdditionalSeconds = 5;

        /// <summary>
        /// Initializes a new instance of the OAuthDeviceFlow class
        /// 初始化OAuth设备流程类的新实例
        /// </summary>
        /// <param name="httpClient">HTTP client for making requests</param>
        /// <param name="logger">Logger instance</param>
        public OAuthDeviceFlow(HttpClient httpClient, ILogger<OAuthDeviceFlow> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Configure HTTP client
            ConfigureHttpClient();
        }

        /// <summary>
        /// Initiates the device authorization flow by requesting device and user codes
        /// 通过请求设备码和用户码启动设备授权流程
        /// </summary>
        /// <param name="clientId">GitHub OAuth app client ID</param>
        /// <param name="scope">Requested OAuth scopes</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Device code response containing verification codes and URLs</returns>
        public async Task<DeviceCodeResponse> RequestDeviceCodeAsync(
            string clientId,
            string scope = "user:email",
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(clientId))
                throw new ArgumentException("Client ID cannot be null or empty", nameof(clientId));

            _logger.LogInformation("Requesting device code for client ID: {ClientId}", clientId);

            var requestData = new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["scope"] = scope ?? "user:email"
            };

            try
            {
                var response = await PostFormDataAsync<DeviceCodeResponse>(
                    DeviceCodeEndpoint,
                    requestData,
                    cancellationToken);

                if (!response.IsValid)
                {
                    _logger.LogError("Invalid device code response received");
                    throw new InvalidOperationException("Received invalid device code response from GitHub");
                }

                _logger.LogInformation("Device code requested successfully. User code: {UserCode}, Expires in: {ExpiresIn}s",
                    response.UserCode, response.ExpiresIn);

                return response;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Network error occurred while requesting device code");
                throw new InvalidOperationException("Failed to request device code due to network error", ex);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Request timeout while requesting device code");
                throw new InvalidOperationException("Request timeout while requesting device code", ex);
            }
        }

        /// <summary>
        /// Polls for an access token using the device code
        /// 使用设备码轮询访问令牌
        /// </summary>
        /// <param name="clientId">GitHub OAuth app client ID</param>
        /// <param name="deviceCode">Device code from device authorization request</param>
        /// <param name="intervalSeconds">Polling interval in seconds</param>
        /// <param name="timeoutSeconds">Total timeout in seconds</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Access token response</returns>
        public async Task<AccessTokenResponse> PollForAccessTokenAsync(
            string clientId,
            string deviceCode,
            int intervalSeconds = DefaultIntervalSeconds,
            int timeoutSeconds = DefaultTimeoutSeconds,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(clientId))
                throw new ArgumentException("Client ID cannot be null or empty", nameof(clientId));

            if (string.IsNullOrWhiteSpace(deviceCode))
                throw new ArgumentException("Device code cannot be null or empty", nameof(deviceCode));

            if (intervalSeconds < 1)
                intervalSeconds = DefaultIntervalSeconds;

            if (timeoutSeconds < 1)
                timeoutSeconds = DefaultTimeoutSeconds;

            _logger.LogInformation("Starting polling for access token. Device code: {DeviceCode}, Interval: {Interval}s, Timeout: {Timeout}s",
                deviceCode[..8] + "...", intervalSeconds, timeoutSeconds);

            var requestData = new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["device_code"] = deviceCode,
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code"
            };

            var startTime = DateTime.UtcNow;
            var currentInterval = intervalSeconds;
            var consecutiveSlowDowns = 0;

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            while (!combinedCts.Token.IsCancellationRequested)
            {
                try
                {
                    var response = await PostFormDataAsync<AccessTokenResponse>(
                        AccessTokenEndpoint,
                        requestData,
                        combinedCts.Token);

                    // Success case
                    if (response.IsSuccess)
                    {
                        var elapsed = DateTime.UtcNow - startTime;
                        _logger.LogInformation("Access token obtained successfully after {Elapsed}s",
                            elapsed.TotalSeconds);
                        return response;
                    }

                    // Handle different error cases
                    if (response.IsAccessDenied)
                    {
                        _logger.LogWarning("User denied authorization request");
                        return response;
                    }

                    if (response.IsExpiredToken)
                    {
                        _logger.LogWarning("Device code has expired");
                        return response;
                    }

                    if (response.IsUnsupportedGrantType)
                    {
                        _logger.LogError("Unsupported grant type or invalid device code");
                        return response;
                    }

                    if (response.IsSlowDown)
                    {
                        consecutiveSlowDowns++;
                        currentInterval += SlowDownAdditionalSeconds;
                        _logger.LogWarning("Received slow_down response. Increasing interval to {Interval}s (consecutive: {Count})",
                            currentInterval, consecutiveSlowDowns);

                        // Prevent infinite slow-down escalation
                        if (consecutiveSlowDowns > 3)
                        {
                            _logger.LogError("Too many consecutive slow_down responses, aborting");
                            throw new InvalidOperationException("Polling frequency too high, unable to continue");
                        }
                    }
                    else if (response.IsAuthorizationPending)
                    {
                        // Reset slow-down counter on successful pending response
                        consecutiveSlowDowns = 0;
                        currentInterval = intervalSeconds;
                        _logger.LogDebug("Authorization still pending, continuing to poll...");
                    }
                    else
                    {
                        // Unknown error
                        _logger.LogError("Unknown error while polling for access token: {Error} - {Description}",
                            response.Error, response.ErrorDescription);
                        return response;
                    }

                    // Wait before next poll
                    await Task.Delay(TimeSpan.FromSeconds(currentInterval), combinedCts.Token);
                }
                catch (TaskCanceledException) when (timeoutCts.Token.IsCancellationRequested)
                {
                    _logger.LogWarning("Polling timeout exceeded ({Timeout}s)", timeoutSeconds);
                    throw new TimeoutException($"Polling timeout exceeded ({timeoutSeconds}s)");
                }
                catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Polling cancelled by user");
                    throw new OperationCanceledException("Polling was cancelled");
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(ex, "Network error occurred while polling for access token");

                    // Retry with exponential backoff for network errors
                    await Task.Delay(TimeSpan.FromSeconds(currentInterval * 2), combinedCts.Token);
                }
            }

            throw new OperationCanceledException("Polling was cancelled");
        }

        /// <summary>
        /// Validates an access token by making a test API call
        /// 通过测试API调用验证访问令牌
        /// </summary>
        /// <param name="accessToken">Access token to validate</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if token is valid, false otherwise</returns>
        public async Task<bool> ValidateAccessTokenAsync(string accessToken, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
                return false;

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
                request.Headers.Authorization = new AuthenticationHeaderValue("token", accessToken);
                request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Occop", "1.0"));

                using var response = await _httpClient.SendAsync(request, cancellationToken);

                var isValid = response.IsSuccessStatusCode;
                _logger.LogInformation("Access token validation result: {IsValid}", isValid);

                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating access token");
                return false;
            }
        }

        /// <summary>
        /// Configures the HTTP client with default settings
        /// 使用默认设置配置HTTP客户端
        /// </summary>
        private void ConfigureHttpClient()
        {
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.UserAgent.Clear();
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Occop", "1.0"));
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// Posts form data to an endpoint and deserializes the JSON response
        /// 向端点发送表单数据并反序列化JSON响应
        /// </summary>
        /// <typeparam name="T">Response type</typeparam>
        /// <param name="endpoint">API endpoint URL</param>
        /// <param name="formData">Form data to send</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Deserialized response</returns>
        private async Task<T> PostFormDataAsync<T>(
            string endpoint,
            Dictionary<string, string> formData,
            CancellationToken cancellationToken) where T : new()
        {
            var retryCount = 0;
            while (retryCount < MaxRetryAttempts)
            {
                try
                {
                    using var content = new FormUrlEncodedContent(formData);
                    using var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);

                    var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("HTTP request failed with status {StatusCode}: {Content}",
                            response.StatusCode, jsonContent);
                    }

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var result = JsonSerializer.Deserialize<T>(jsonContent, options);
                    return result ?? new T();
                }
                catch (HttpRequestException ex) when (retryCount < MaxRetryAttempts - 1)
                {
                    retryCount++;
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount)); // Exponential backoff
                    _logger.LogWarning(ex, "Request failed, retrying in {Delay}s (attempt {Attempt}/{MaxAttempts})",
                        delay.TotalSeconds, retryCount, MaxRetryAttempts);

                    await Task.Delay(delay, cancellationToken);
                }
            }

            throw new InvalidOperationException($"Failed to complete request after {MaxRetryAttempts} attempts");
        }
    }
}