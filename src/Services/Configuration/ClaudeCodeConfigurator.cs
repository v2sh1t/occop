using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using Occop.Models.Configuration;
using Occop.Services.Configuration;
using Occop.Services.Security;
using Occop.Services.Logging;

namespace Occop.Services.Configuration
{
    /// <summary>
    /// Claude Code专用配置器
    /// 负责Claude Code环境变量的设置、验证和清理
    /// </summary>
    public class ClaudeCodeConfigurator : IDisposable
    {
        private readonly SecureStorage _secureStorage;
        private readonly ConfigurationLogger _logger;
        private readonly Dictionary<string, string?> _originalEnvironmentValues;
        private readonly object _lockObject;
        private bool _disposed;
        private bool _configurationApplied;

        /// <summary>
        /// 配置是否已应用
        /// </summary>
        public bool IsConfigurationApplied => _configurationApplied;

        /// <summary>
        /// 配置应用时间
        /// </summary>
        public DateTime? ConfigurationAppliedAt { get; private set; }

        /// <summary>
        /// 配置应用事件
        /// </summary>
        public event EventHandler<ConfigurationResult>? ConfigurationApplied;

        /// <summary>
        /// 配置清理事件
        /// </summary>
        public event EventHandler<ConfigurationResult>? ConfigurationCleared;

        /// <summary>
        /// 初始化Claude Code配置器
        /// </summary>
        /// <param name="secureStorage">安全存储服务</param>
        /// <param name="logger">配置日志记录器</param>
        public ClaudeCodeConfigurator(SecureStorage secureStorage, ConfigurationLogger logger)
        {
            _secureStorage = secureStorage ?? throw new ArgumentNullException(nameof(secureStorage));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _originalEnvironmentValues = new Dictionary<string, string?>();
            _lockObject = new object();
        }

        /// <summary>
        /// 设置Claude Code认证令牌
        /// </summary>
        /// <param name="authToken">认证令牌</param>
        /// <returns>设置结果</returns>
        public async Task<ConfigurationResult> SetAuthTokenAsync(SecureString authToken)
        {
            ThrowIfDisposed();

            if (authToken == null)
            {
                return new ConfigurationResult(false, ConfigurationOperation.Set,
                    "Authentication token cannot be null");
            }

            try
            {
                // 验证令牌格式
                if (!ClaudeCodeConfigValidator.ValidateAuthToken(authToken))
                {
                    await _logger.LogOperationAsync(ConfigurationOperation.Set, false,
                        "Invalid authentication token format", isSensitive: true);
                    return new ConfigurationResult(false, ConfigurationOperation.Set,
                        "Invalid authentication token format");
                }

                lock (_lockObject)
                {
                    // 存储到安全存储
                    _secureStorage.Store(ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable, authToken);
                }

                await _logger.LogOperationAsync(ConfigurationOperation.Set, true,
                    "Authentication token set successfully", isSensitive: true);

                return new ConfigurationResult(true, ConfigurationOperation.Set,
                    "Authentication token set successfully");
            }
            catch (Exception ex)
            {
                await _logger.LogOperationAsync(ConfigurationOperation.Set, false,
                    $"Failed to set authentication token: {ex.Message}", isSensitive: true);
                return new ConfigurationResult(false, ConfigurationOperation.Set,
                    "Failed to set authentication token", ex);
            }
        }

        /// <summary>
        /// 设置Claude Code API基础URL
        /// </summary>
        /// <param name="baseUrl">API基础URL</param>
        /// <returns>设置结果</returns>
        public async Task<ConfigurationResult> SetBaseUrlAsync(string? baseUrl)
        {
            ThrowIfDisposed();

            try
            {
                // 如果URL为空，使用默认值
                var urlToSet = string.IsNullOrWhiteSpace(baseUrl)
                    ? ClaudeCodeConfigConstants.DefaultBaseUrl
                    : baseUrl;

                // 验证URL格式
                if (!ClaudeCodeConfigValidator.ValidateBaseUrl(urlToSet))
                {
                    await _logger.LogOperationAsync(ConfigurationOperation.Set, false,
                        $"Invalid base URL format: {urlToSet}");
                    return new ConfigurationResult(false, ConfigurationOperation.Set,
                        "Invalid base URL format");
                }

                lock (_lockObject)
                {
                    // 存储到安全存储
                    _secureStorage.Store(ClaudeCodeConfigConstants.BaseUrlEnvironmentVariable, urlToSet!);
                }

                await _logger.LogOperationAsync(ConfigurationOperation.Set, true,
                    $"Base URL set to: {urlToSet}");

                return new ConfigurationResult(true, ConfigurationOperation.Set,
                    $"Base URL set successfully to: {urlToSet}");
            }
            catch (Exception ex)
            {
                await _logger.LogOperationAsync(ConfigurationOperation.Set, false,
                    $"Failed to set base URL: {ex.Message}");
                return new ConfigurationResult(false, ConfigurationOperation.Set,
                    "Failed to set base URL", ex);
            }
        }

        /// <summary>
        /// 应用配置到环境变量
        /// </summary>
        /// <returns>应用结果</returns>
        public async Task<ConfigurationResult> ApplyConfigurationAsync()
        {
            ThrowIfDisposed();

            try
            {
                lock (_lockObject)
                {
                    // 保存原始环境变量值（用于回滚）
                    foreach (var envVar in ClaudeCodeConfig.GetEnvironmentVariableMapping().Values)
                    {
                        if (!_originalEnvironmentValues.ContainsKey(envVar))
                        {
                            _originalEnvironmentValues[envVar] = Environment.GetEnvironmentVariable(envVar);
                        }
                    }

                    // 应用认证令牌
                    var authToken = _secureStorage.GetString(ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable);
                    if (!string.IsNullOrEmpty(authToken))
                    {
                        Environment.SetEnvironmentVariable(
                            ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable,
                            authToken,
                            EnvironmentVariableTarget.Process);
                    }

                    // 应用基础URL
                    var baseUrl = _secureStorage.GetString(ClaudeCodeConfigConstants.BaseUrlEnvironmentVariable);
                    if (!string.IsNullOrEmpty(baseUrl))
                    {
                        Environment.SetEnvironmentVariable(
                            ClaudeCodeConfigConstants.BaseUrlEnvironmentVariable,
                            baseUrl,
                            EnvironmentVariableTarget.Process);
                    }

                    _configurationApplied = true;
                    ConfigurationAppliedAt = DateTime.UtcNow;
                }

                await _logger.LogOperationAsync(ConfigurationOperation.Apply, true,
                    "Configuration applied to environment variables successfully");

                var result = new ConfigurationResult(true, ConfigurationOperation.Apply,
                    "Configuration applied successfully");

                ConfigurationApplied?.Invoke(this, result);
                return result;
            }
            catch (Exception ex)
            {
                await _logger.LogOperationAsync(ConfigurationOperation.Apply, false,
                    $"Failed to apply configuration: {ex.Message}");

                var result = new ConfigurationResult(false, ConfigurationOperation.Apply,
                    "Failed to apply configuration", ex);
                return result;
            }
        }

        /// <summary>
        /// 清理配置（清除环境变量和内存中的敏感信息）
        /// </summary>
        /// <returns>清理结果</returns>
        public async Task<ConfigurationResult> ClearConfigurationAsync()
        {
            ThrowIfDisposed();

            try
            {
                lock (_lockObject)
                {
                    // 清除进程级环境变量
                    foreach (var envVar in ClaudeCodeConfig.GetEnvironmentVariableMapping().Values)
                    {
                        Environment.SetEnvironmentVariable(envVar, null, EnvironmentVariableTarget.Process);
                    }

                    // 清理安全存储
                    var clearResult = _secureStorage.ClearAll(MemoryCleanupType.Forced);

                    _configurationApplied = false;
                    ConfigurationAppliedAt = null;
                }

                // 强制垃圾回收
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                await _logger.LogOperationAsync(ConfigurationOperation.Clear, true,
                    "Configuration cleared successfully (environment variables and secure storage)");

                var result = new ConfigurationResult(true, ConfigurationOperation.Clear,
                    "Configuration cleared successfully");

                ConfigurationCleared?.Invoke(this, result);
                return result;
            }
            catch (Exception ex)
            {
                await _logger.LogOperationAsync(ConfigurationOperation.Clear, false,
                    $"Failed to clear configuration: {ex.Message}");

                var result = new ConfigurationResult(false, ConfigurationOperation.Clear,
                    "Failed to clear configuration", ex);
                return result;
            }
        }

        /// <summary>
        /// 回滚配置到原始状态
        /// </summary>
        /// <returns>回滚结果</returns>
        public async Task<ConfigurationResult> RollbackConfigurationAsync()
        {
            ThrowIfDisposed();

            try
            {
                lock (_lockObject)
                {
                    // 恢复原始环境变量值
                    foreach (var kvp in _originalEnvironmentValues)
                    {
                        Environment.SetEnvironmentVariable(kvp.Key, kvp.Value, EnvironmentVariableTarget.Process);
                    }

                    // 清理安全存储
                    _secureStorage.ClearAll(MemoryCleanupType.Immediate);

                    _configurationApplied = false;
                    ConfigurationAppliedAt = null;
                }

                await _logger.LogOperationAsync(ConfigurationOperation.Rollback, true,
                    "Configuration rolled back to original state successfully");

                return new ConfigurationResult(true, ConfigurationOperation.Rollback,
                    "Configuration rolled back successfully");
            }
            catch (Exception ex)
            {
                await _logger.LogOperationAsync(ConfigurationOperation.Rollback, false,
                    $"Failed to rollback configuration: {ex.Message}");

                return new ConfigurationResult(false, ConfigurationOperation.Rollback,
                    "Failed to rollback configuration", ex);
            }
        }

        /// <summary>
        /// 获取当前配置状态
        /// </summary>
        /// <returns>配置状态信息</returns>
        public Dictionary<string, object> GetConfigurationStatus()
        {
            ThrowIfDisposed();

            lock (_lockObject)
            {
                var status = new Dictionary<string, object>
                {
                    { "IsConfigurationApplied", _configurationApplied },
                    { "ConfigurationAppliedAt", ConfigurationAppliedAt?.ToString("yyyy-MM-dd HH:mm:ss UTC") ?? "N/A" },
                    { "HasAuthToken", _secureStorage.ContainsKey(ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable) },
                    { "HasBaseUrl", _secureStorage.ContainsKey(ClaudeCodeConfigConstants.BaseUrlEnvironmentVariable) },
                    { "OriginalEnvironmentValuesCount", _originalEnvironmentValues.Count },
                    { "SecureStorageItemsCount", _secureStorage.Count },
                    { "Timestamp", DateTime.UtcNow }
                };

                // 添加环境变量状态（不包含敏感值）
                foreach (var envVar in ClaudeCodeConfig.GetEnvironmentVariableMapping().Values)
                {
                    var currentValue = Environment.GetEnvironmentVariable(envVar);
                    status[$"EnvironmentVariable_{envVar}_IsSet"] = !string.IsNullOrEmpty(currentValue);

                    // 对于敏感信息，只显示是否设置和长度
                    if (envVar == ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable && !string.IsNullOrEmpty(currentValue))
                    {
                        status[$"EnvironmentVariable_{envVar}_Length"] = currentValue.Length;
                        status[$"EnvironmentVariable_{envVar}_HasValidPrefix"] = currentValue.StartsWith(ClaudeCodeConfigConstants.TokenPrefix);
                    }
                }

                return status;
            }
        }

        /// <summary>
        /// 测试Claude Code是否能够正常工作
        /// </summary>
        /// <returns>测试结果</returns>
        public async Task<ConfigurationResult> TestClaudeCodeAsync()
        {
            ThrowIfDisposed();

            try
            {
                // 检查环境变量是否设置
                var authToken = Environment.GetEnvironmentVariable(ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable);
                if (string.IsNullOrEmpty(authToken))
                {
                    return new ConfigurationResult(false, ConfigurationOperation.Validate,
                        "Authentication token environment variable is not set");
                }

                // 尝试运行claude --version命令来测试Claude Code是否可用
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
                    return new ConfigurationResult(false, ConfigurationOperation.Validate,
                        "Failed to start Claude Code process");
                }

                await process.WaitForExitAsync();
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();

                if (process.ExitCode == 0)
                {
                    await _logger.LogOperationAsync(ConfigurationOperation.Validate, true,
                        $"Claude Code test successful. Version: {output.Trim()}");

                    return new ConfigurationResult(true, ConfigurationOperation.Validate,
                        $"Claude Code is working correctly. Version: {output.Trim()}");
                }
                else
                {
                    await _logger.LogOperationAsync(ConfigurationOperation.Validate, false,
                        $"Claude Code test failed. Exit code: {process.ExitCode}, Error: {error}");

                    return new ConfigurationResult(false, ConfigurationOperation.Validate,
                        $"Claude Code test failed. Exit code: {process.ExitCode}");
                }
            }
            catch (Exception ex)
            {
                await _logger.LogOperationAsync(ConfigurationOperation.Validate, false,
                    $"Claude Code test failed with exception: {ex.Message}");

                return new ConfigurationResult(false, ConfigurationOperation.Validate,
                    "Claude Code test failed", ex);
            }
        }

        /// <summary>
        /// 检查对象是否已释放
        /// </summary>
        /// <exception cref="ObjectDisposedException">对象已释放时抛出</exception>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ClaudeCodeConfigurator));
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    // 同步清理配置
                    var clearTask = ClearConfigurationAsync();
                    clearTask.Wait(TimeSpan.FromSeconds(5)); // 等待最多5秒
                }
                catch
                {
                    // 忽略清理时的异常，确保Dispose能够完成
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// 析构函数，确保资源得到释放
        /// </summary>
        ~ClaudeCodeConfigurator()
        {
            Dispose();
        }
    }
}