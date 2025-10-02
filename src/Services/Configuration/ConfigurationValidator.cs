using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Occop.Models.Configuration;
using Occop.Services.Configuration;
using Occop.Services.Security;
using Occop.Services.Logging;

namespace Occop.Services.Configuration
{
    /// <summary>
    /// 配置验证结果类型
    /// </summary>
    public enum ValidationResultType
    {
        /// <summary>
        /// 验证成功
        /// </summary>
        Success,

        /// <summary>
        /// 警告（配置可用但有问题）
        /// </summary>
        Warning,

        /// <summary>
        /// 错误（配置不可用）
        /// </summary>
        Error,

        /// <summary>
        /// 致命错误（系统不可用）
        /// </summary>
        Fatal
    }

    /// <summary>
    /// 配置验证结果详情
    /// </summary>
    public class ConfigurationValidationResult
    {
        /// <summary>
        /// 验证结果类型
        /// </summary>
        public ValidationResultType ResultType { get; }

        /// <summary>
        /// 验证的配置项键名
        /// </summary>
        public string ConfigurationKey { get; }

        /// <summary>
        /// 验证消息
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 详细信息
        /// </summary>
        public string? Details { get; }

        /// <summary>
        /// 验证时间戳
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// 异常信息（如果有）
        /// </summary>
        public Exception? Exception { get; }

        /// <summary>
        /// 建议的修复措施
        /// </summary>
        public string? RecommendedAction { get; }

        /// <summary>
        /// 初始化配置验证结果
        /// </summary>
        /// <param name="resultType">验证结果类型</param>
        /// <param name="configurationKey">配置项键名</param>
        /// <param name="message">验证消息</param>
        /// <param name="details">详细信息</param>
        /// <param name="recommendedAction">建议的修复措施</param>
        /// <param name="exception">异常信息</param>
        public ConfigurationValidationResult(
            ValidationResultType resultType,
            string configurationKey,
            string message,
            string? details = null,
            string? recommendedAction = null,
            Exception? exception = null)
        {
            ResultType = resultType;
            ConfigurationKey = configurationKey ?? throw new ArgumentNullException(nameof(configurationKey));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Details = details;
            RecommendedAction = recommendedAction;
            Exception = exception;
            Timestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// 是否为成功的验证结果
        /// </summary>
        public bool IsSuccess => ResultType == ValidationResultType.Success;

        /// <summary>
        /// 是否为关键错误
        /// </summary>
        public bool IsCritical => ResultType == ValidationResultType.Error || ResultType == ValidationResultType.Fatal;
    }

    /// <summary>
    /// 健康检查结果
    /// </summary>
    public class HealthCheckResult
    {
        /// <summary>
        /// 整体健康状态
        /// </summary>
        public bool IsHealthy { get; }

        /// <summary>
        /// 健康分数（0-100）
        /// </summary>
        public int HealthScore { get; }

        /// <summary>
        /// 检查项目结果列表
        /// </summary>
        public IReadOnlyList<ConfigurationValidationResult> CheckResults { get; }

        /// <summary>
        /// 健康检查时间戳
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// 总体摘要
        /// </summary>
        public string Summary { get; }

        /// <summary>
        /// 初始化健康检查结果
        /// </summary>
        /// <param name="checkResults">检查项目结果列表</param>
        public HealthCheckResult(IEnumerable<ConfigurationValidationResult> checkResults)
        {
            CheckResults = checkResults?.ToList() ?? throw new ArgumentNullException(nameof(checkResults));
            Timestamp = DateTime.UtcNow;

            // 计算健康状态
            var results = CheckResults.ToList();
            var totalChecks = results.Count;
            var successCount = results.Count(r => r.ResultType == ValidationResultType.Success);
            var warningCount = results.Count(r => r.ResultType == ValidationResultType.Warning);
            var errorCount = results.Count(r => r.ResultType == ValidationResultType.Error);
            var fatalCount = results.Count(r => r.ResultType == ValidationResultType.Fatal);

            // 如果有致命错误，系统不健康
            IsHealthy = fatalCount == 0 && errorCount == 0;

            // 计算健康分数
            if (totalChecks == 0)
            {
                HealthScore = 0;
            }
            else
            {
                var score = (successCount * 100 + warningCount * 70) / totalChecks;
                HealthScore = Math.Max(0, Math.Min(100, score));
            }

            // 生成摘要
            Summary = $"Health check completed: {successCount} success, {warningCount} warnings, {errorCount} errors, {fatalCount} fatal. Score: {HealthScore}/100";
        }
    }

    /// <summary>
    /// Claude Code配置验证器
    /// 负责验证配置的完整性、正确性和健康状态
    /// </summary>
    public class ConfigurationValidator : IDisposable
    {
        private readonly SecureStorage _secureStorage;
        private readonly ConfigurationLogger _logger;
        private readonly HttpClient _httpClient;
        private bool _disposed;

        /// <summary>
        /// 上次验证时间
        /// </summary>
        public DateTime? LastValidationTime { get; private set; }

        /// <summary>
        /// 上次健康检查时间
        /// </summary>
        public DateTime? LastHealthCheckTime { get; private set; }

        /// <summary>
        /// 验证完成事件
        /// </summary>
        public event EventHandler<HealthCheckResult>? ValidationCompleted;

        /// <summary>
        /// 初始化配置验证器
        /// </summary>
        /// <param name="secureStorage">安全存储服务</param>
        /// <param name="logger">配置日志记录器</param>
        public ConfigurationValidator(SecureStorage secureStorage, ConfigurationLogger logger)
        {
            _secureStorage = secureStorage ?? throw new ArgumentNullException(nameof(secureStorage));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        /// <summary>
        /// 验证存储的配置
        /// </summary>
        /// <returns>验证结果</returns>
        public async Task<HealthCheckResult> ValidateStoredConfigurationAsync()
        {
            ThrowIfDisposed();

            var results = new List<ConfigurationValidationResult>();

            try
            {
                await _logger.LogOperationAsync(ConfigurationOperation.Validate, true,
                    "Starting stored configuration validation");

                // 验证认证令牌
                results.Add(await ValidateAuthTokenInStorageAsync());

                // 验证基础URL
                results.Add(await ValidateBaseUrlInStorageAsync());

                // 验证必需配置项是否完整
                results.Add(ValidateRequiredConfigurationCompleteness());

                LastValidationTime = DateTime.UtcNow;

                var healthResult = new HealthCheckResult(results);

                await _logger.LogOperationAsync(ConfigurationOperation.Validate, healthResult.IsHealthy,
                    $"Stored configuration validation completed. Health score: {healthResult.HealthScore}/100");

                ValidationCompleted?.Invoke(this, healthResult);
                return healthResult;
            }
            catch (Exception ex)
            {
                results.Add(new ConfigurationValidationResult(
                    ValidationResultType.Fatal,
                    "SYSTEM",
                    "Configuration validation failed with exception",
                    ex.Message,
                    "Check system integrity and restart validation",
                    ex));

                var healthResult = new HealthCheckResult(results);
                await _logger.LogOperationAsync(ConfigurationOperation.Validate, false,
                    $"Stored configuration validation failed: {ex.Message}");

                return healthResult;
            }
        }

        /// <summary>
        /// 验证应用的环境变量配置
        /// </summary>
        /// <returns>验证结果</returns>
        public async Task<HealthCheckResult> ValidateAppliedConfigurationAsync()
        {
            ThrowIfDisposed();

            var results = new List<ConfigurationValidationResult>();

            try
            {
                await _logger.LogOperationAsync(ConfigurationOperation.Validate, true,
                    "Starting applied configuration validation");

                // 验证环境变量中的认证令牌
                results.Add(ValidateAuthTokenInEnvironment());

                // 验证环境变量中的基础URL
                results.Add(ValidateBaseUrlInEnvironment());

                // 验证Claude Code可执行性
                results.Add(await ValidateClaudeCodeExecutabilityAsync());

                LastValidationTime = DateTime.UtcNow;

                var healthResult = new HealthCheckResult(results);

                await _logger.LogOperationAsync(ConfigurationOperation.Validate, healthResult.IsHealthy,
                    $"Applied configuration validation completed. Health score: {healthResult.HealthScore}/100");

                ValidationCompleted?.Invoke(this, healthResult);
                return healthResult;
            }
            catch (Exception ex)
            {
                results.Add(new ConfigurationValidationResult(
                    ValidationResultType.Fatal,
                    "SYSTEM",
                    "Applied configuration validation failed with exception",
                    ex.Message,
                    "Check system state and reapply configuration",
                    ex));

                var healthResult = new HealthCheckResult(results);
                await _logger.LogOperationAsync(ConfigurationOperation.Validate, false,
                    $"Applied configuration validation failed: {ex.Message}");

                return healthResult;
            }
        }

        /// <summary>
        /// 执行完整健康检查
        /// </summary>
        /// <returns>健康检查结果</returns>
        public async Task<HealthCheckResult> PerformHealthCheckAsync()
        {
            ThrowIfDisposed();

            var results = new List<ConfigurationValidationResult>();

            try
            {
                await _logger.LogOperationAsync(ConfigurationOperation.Validate, true,
                    "Starting comprehensive health check");

                // 存储配置验证
                var storedResults = await ValidateStoredConfigurationAsync();
                results.AddRange(storedResults.CheckResults);

                // 应用配置验证
                var appliedResults = await ValidateAppliedConfigurationAsync();
                results.AddRange(appliedResults.CheckResults);

                // API连通性检查
                results.Add(await ValidateApiConnectivityAsync());

                // 系统资源检查
                results.Add(ValidateSystemResources());

                LastHealthCheckTime = DateTime.UtcNow;

                var healthResult = new HealthCheckResult(results);

                await _logger.LogOperationAsync(ConfigurationOperation.Validate, healthResult.IsHealthy,
                    $"Comprehensive health check completed. Health score: {healthResult.HealthScore}/100");

                ValidationCompleted?.Invoke(this, healthResult);
                return healthResult;
            }
            catch (Exception ex)
            {
                results.Add(new ConfigurationValidationResult(
                    ValidationResultType.Fatal,
                    "SYSTEM",
                    "Health check failed with exception",
                    ex.Message,
                    "Check system health and retry",
                    ex));

                var healthResult = new HealthCheckResult(results);
                await _logger.LogOperationAsync(ConfigurationOperation.Validate, false,
                    $"Health check failed: {ex.Message}");

                return healthResult;
            }
        }

        /// <summary>
        /// 验证存储的认证令牌
        /// </summary>
        /// <returns>验证结果</returns>
        private async Task<ConfigurationValidationResult> ValidateAuthTokenInStorageAsync()
        {
            try
            {
                var authToken = _secureStorage.GetSecureString(ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable);

                if (authToken == null)
                {
                    return new ConfigurationValidationResult(
                        ValidationResultType.Error,
                        ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable,
                        "Authentication token is not stored",
                        "No authentication token found in secure storage",
                        "Set authentication token using SetAuthTokenAsync method");
                }

                // 验证令牌格式
                if (!ClaudeCodeConfigValidator.ValidateAuthToken(authToken))
                {
                    return new ConfigurationValidationResult(
                        ValidationResultType.Error,
                        ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable,
                        "Invalid authentication token format",
                        "Token does not match expected Anthropic API token format",
                        "Verify and update authentication token");
                }

                return new ConfigurationValidationResult(
                    ValidationResultType.Success,
                    ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable,
                    "Authentication token is valid and properly stored");
            }
            catch (Exception ex)
            {
                return new ConfigurationValidationResult(
                    ValidationResultType.Error,
                    ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable,
                    "Failed to validate stored authentication token",
                    ex.Message,
                    "Check secure storage integrity",
                    ex);
            }
        }

        /// <summary>
        /// 验证存储的基础URL
        /// </summary>
        /// <returns>验证结果</returns>
        private async Task<ConfigurationValidationResult> ValidateBaseUrlInStorageAsync()
        {
            try
            {
                var baseUrl = _secureStorage.GetString(ClaudeCodeConfigConstants.BaseUrlEnvironmentVariable);

                if (string.IsNullOrEmpty(baseUrl))
                {
                    return new ConfigurationValidationResult(
                        ValidationResultType.Warning,
                        ClaudeCodeConfigConstants.BaseUrlEnvironmentVariable,
                        "Base URL is not set, will use default",
                        $"Will use default URL: {ClaudeCodeConfigConstants.DefaultBaseUrl}",
                        "Consider setting explicit base URL if using custom endpoint");
                }

                if (!ClaudeCodeConfigValidator.ValidateBaseUrl(baseUrl))
                {
                    return new ConfigurationValidationResult(
                        ValidationResultType.Error,
                        ClaudeCodeConfigConstants.BaseUrlEnvironmentVariable,
                        "Invalid base URL format",
                        $"URL '{baseUrl}' is not a valid HTTP/HTTPS URL",
                        "Set a valid HTTP/HTTPS URL");
                }

                return new ConfigurationValidationResult(
                    ValidationResultType.Success,
                    ClaudeCodeConfigConstants.BaseUrlEnvironmentVariable,
                    "Base URL is valid and properly stored",
                    $"Using URL: {baseUrl}");
            }
            catch (Exception ex)
            {
                return new ConfigurationValidationResult(
                    ValidationResultType.Error,
                    ClaudeCodeConfigConstants.BaseUrlEnvironmentVariable,
                    "Failed to validate stored base URL",
                    ex.Message,
                    "Check secure storage integrity",
                    ex);
            }
        }

        /// <summary>
        /// 验证必需配置项完整性
        /// </summary>
        /// <returns>验证结果</returns>
        private ConfigurationValidationResult ValidateRequiredConfigurationCompleteness()
        {
            try
            {
                var requiredKeys = ClaudeCodeConfig.GetRequiredConfigurationKeys().ToList();
                var missingKeys = new List<string>();

                foreach (var key in requiredKeys)
                {
                    if (!_secureStorage.ContainsKey(key))
                    {
                        missingKeys.Add(key);
                    }
                }

                if (missingKeys.Any())
                {
                    return new ConfigurationValidationResult(
                        ValidationResultType.Error,
                        "REQUIRED_CONFIG",
                        "Missing required configuration items",
                        $"Missing keys: {string.Join(", ", missingKeys)}",
                        "Set all required configuration items before applying configuration");
                }

                return new ConfigurationValidationResult(
                    ValidationResultType.Success,
                    "REQUIRED_CONFIG",
                    "All required configuration items are present");
            }
            catch (Exception ex)
            {
                return new ConfigurationValidationResult(
                    ValidationResultType.Error,
                    "REQUIRED_CONFIG",
                    "Failed to validate configuration completeness",
                    ex.Message,
                    "Check configuration system integrity",
                    ex);
            }
        }

        /// <summary>
        /// 验证环境变量中的认证令牌
        /// </summary>
        /// <returns>验证结果</returns>
        private ConfigurationValidationResult ValidateAuthTokenInEnvironment()
        {
            try
            {
                var envToken = Environment.GetEnvironmentVariable(ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable);

                if (string.IsNullOrEmpty(envToken))
                {
                    return new ConfigurationValidationResult(
                        ValidationResultType.Error,
                        ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable,
                        "Authentication token environment variable is not set",
                        "Environment variable is missing or empty",
                        "Apply configuration to set environment variables");
                }

                if (!ClaudeCodeConfigValidator.ValidateAuthToken(envToken))
                {
                    return new ConfigurationValidationResult(
                        ValidationResultType.Error,
                        ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable,
                        "Invalid authentication token in environment",
                        "Token format is invalid",
                        "Check and reapply valid authentication token");
                }

                return new ConfigurationValidationResult(
                    ValidationResultType.Success,
                    ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable,
                    "Authentication token environment variable is valid");
            }
            catch (Exception ex)
            {
                return new ConfigurationValidationResult(
                    ValidationResultType.Error,
                    ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable,
                    "Failed to validate authentication token in environment",
                    ex.Message,
                    "Check environment variable system",
                    ex);
            }
        }

        /// <summary>
        /// 验证环境变量中的基础URL
        /// </summary>
        /// <returns>验证结果</returns>
        private ConfigurationValidationResult ValidateBaseUrlInEnvironment()
        {
            try
            {
                var envBaseUrl = Environment.GetEnvironmentVariable(ClaudeCodeConfigConstants.BaseUrlEnvironmentVariable);

                if (string.IsNullOrEmpty(envBaseUrl))
                {
                    return new ConfigurationValidationResult(
                        ValidationResultType.Warning,
                        ClaudeCodeConfigConstants.BaseUrlEnvironmentVariable,
                        "Base URL environment variable is not set",
                        "Claude Code will use default API endpoint",
                        "Set base URL environment variable if using custom endpoint");
                }

                if (!ClaudeCodeConfigValidator.ValidateBaseUrl(envBaseUrl))
                {
                    return new ConfigurationValidationResult(
                        ValidationResultType.Error,
                        ClaudeCodeConfigConstants.BaseUrlEnvironmentVariable,
                        "Invalid base URL in environment",
                        $"URL '{envBaseUrl}' is not valid",
                        "Set valid HTTP/HTTPS URL in environment variable");
                }

                return new ConfigurationValidationResult(
                    ValidationResultType.Success,
                    ClaudeCodeConfigConstants.BaseUrlEnvironmentVariable,
                    "Base URL environment variable is valid",
                    $"Using URL: {envBaseUrl}");
            }
            catch (Exception ex)
            {
                return new ConfigurationValidationResult(
                    ValidationResultType.Error,
                    ClaudeCodeConfigConstants.BaseUrlEnvironmentVariable,
                    "Failed to validate base URL in environment",
                    ex.Message,
                    "Check environment variable system",
                    ex);
            }
        }

        /// <summary>
        /// 验证Claude Code可执行性
        /// </summary>
        /// <returns>验证结果</returns>
        private async Task<ConfigurationValidationResult> ValidateClaudeCodeExecutabilityAsync()
        {
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "claude",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processStartInfo);
                if (process == null)
                {
                    return new ConfigurationValidationResult(
                        ValidationResultType.Error,
                        "CLAUDE_CODE_EXECUTABLE",
                        "Cannot start Claude Code process",
                        "Process.Start returned null",
                        "Check if Claude Code is installed and in PATH");
                }

                await process.WaitForExitAsync();
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();

                if (process.ExitCode == 0)
                {
                    return new ConfigurationValidationResult(
                        ValidationResultType.Success,
                        "CLAUDE_CODE_EXECUTABLE",
                        "Claude Code is executable and responding",
                        $"Version: {output.Trim()}");
                }
                else
                {
                    return new ConfigurationValidationResult(
                        ValidationResultType.Error,
                        "CLAUDE_CODE_EXECUTABLE",
                        "Claude Code execution failed",
                        $"Exit code: {process.ExitCode}, Error: {error}",
                        "Check Claude Code installation and configuration");
                }
            }
            catch (FileNotFoundException)
            {
                return new ConfigurationValidationResult(
                    ValidationResultType.Fatal,
                    "CLAUDE_CODE_EXECUTABLE",
                    "Claude Code executable not found",
                    "Claude Code is not installed or not in PATH",
                    "Install Claude Code CLI tool");
            }
            catch (Exception ex)
            {
                return new ConfigurationValidationResult(
                    ValidationResultType.Error,
                    "CLAUDE_CODE_EXECUTABLE",
                    "Failed to test Claude Code executability",
                    ex.Message,
                    "Check system environment and Claude Code installation",
                    ex);
            }
        }

        /// <summary>
        /// 验证API连通性
        /// </summary>
        /// <returns>验证结果</returns>
        private async Task<ConfigurationValidationResult> ValidateApiConnectivityAsync()
        {
            try
            {
                var baseUrl = Environment.GetEnvironmentVariable(ClaudeCodeConfigConstants.BaseUrlEnvironmentVariable)
                              ?? ClaudeCodeConfigConstants.DefaultBaseUrl;

                var response = await _httpClient.GetAsync($"{baseUrl.TrimEnd('/')}/v1/models");

                if (response.IsSuccessStatusCode)
                {
                    return new ConfigurationValidationResult(
                        ValidationResultType.Success,
                        "API_CONNECTIVITY",
                        "API endpoint is reachable",
                        $"HTTP {(int)response.StatusCode} response from {baseUrl}");
                }
                else
                {
                    return new ConfigurationValidationResult(
                        ValidationResultType.Warning,
                        "API_CONNECTIVITY",
                        "API endpoint returned non-success status",
                        $"HTTP {(int)response.StatusCode} response from {baseUrl}",
                        "Check API endpoint and authentication");
                }
            }
            catch (HttpRequestException ex)
            {
                return new ConfigurationValidationResult(
                    ValidationResultType.Warning,
                    "API_CONNECTIVITY",
                    "Cannot reach API endpoint",
                    ex.Message,
                    "Check network connectivity and API endpoint",
                    ex);
            }
            catch (Exception ex)
            {
                return new ConfigurationValidationResult(
                    ValidationResultType.Error,
                    "API_CONNECTIVITY",
                    "API connectivity test failed",
                    ex.Message,
                    "Check network and system configuration",
                    ex);
            }
        }

        /// <summary>
        /// 验证系统资源
        /// </summary>
        /// <returns>验证结果</returns>
        private ConfigurationValidationResult ValidateSystemResources()
        {
            try
            {
                var process = Process.GetCurrentProcess();
                var workingSet = process.WorkingSet64;
                var virtualMemory = process.VirtualMemorySize64;

                // 检查内存使用（警告阈值：100MB，错误阈值：500MB）
                const long warningThreshold = 100 * 1024 * 1024; // 100MB
                const long errorThreshold = 500 * 1024 * 1024;   // 500MB

                if (workingSet > errorThreshold)
                {
                    return new ConfigurationValidationResult(
                        ValidationResultType.Error,
                        "SYSTEM_RESOURCES",
                        "High memory usage detected",
                        $"Working set: {workingSet / (1024 * 1024)}MB",
                        "Consider restarting the application");
                }
                else if (workingSet > warningThreshold)
                {
                    return new ConfigurationValidationResult(
                        ValidationResultType.Warning,
                        "SYSTEM_RESOURCES",
                        "Elevated memory usage",
                        $"Working set: {workingSet / (1024 * 1024)}MB",
                        "Monitor memory usage");
                }
                else
                {
                    return new ConfigurationValidationResult(
                        ValidationResultType.Success,
                        "SYSTEM_RESOURCES",
                        "System resources are healthy",
                        $"Working set: {workingSet / (1024 * 1024)}MB");
                }
            }
            catch (Exception ex)
            {
                return new ConfigurationValidationResult(
                    ValidationResultType.Warning,
                    "SYSTEM_RESOURCES",
                    "Cannot validate system resources",
                    ex.Message,
                    "Check system monitoring capabilities",
                    ex);
            }
        }

        /// <summary>
        /// 检查对象是否已释放
        /// </summary>
        /// <exception cref="ObjectDisposedException">对象已释放时抛出</exception>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ConfigurationValidator));
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _httpClient?.Dispose();
                _disposed = true;
            }
        }
    }
}