using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using Occop.Core.Patterns.Observer;
using Occop.Models.Configuration;
using Occop.Services.Security;

namespace Occop.Services.Configuration
{
    /// <summary>
    /// Claude Code配置管理器
    /// 专门用于管理Claude Code的环境变量配置和安全存储
    /// </summary>
    public class ConfigurationManager : IConfigurationManager
    {
        #region 常量定义

        /// <summary>
        /// ANTHROPIC认证令牌环境变量名
        /// </summary>
        public const string ANTHROPIC_AUTH_TOKEN = \"ANTHROPIC_AUTH_TOKEN\";

        /// <summary>
        /// ANTHROPIC基础URL环境变量名
        /// </summary>
        public const string ANTHROPIC_BASE_URL = \"ANTHROPIC_BASE_URL\";

        /// <summary>
        /// 默认ANTHROPIC基础URL
        /// </summary>
        public const string DEFAULT_ANTHROPIC_BASE_URL = \"https://api.anthropic.com\";

        /// <summary>
        /// 配置项存储键：认证令牌
        /// </summary>
        private const string AUTH_TOKEN_KEY = \"auth_token\";

        /// <summary>
        /// 配置项存储键：基础URL
        /// </summary>
        private const string BASE_URL_KEY = \"base_url\";

        #endregion

        #region 私有字段

        private readonly SecureStorage _secureStorage;
        private readonly Dictionary<string, ConfigurationItem> _configurationSchema;
        private readonly List<ConfigurationResult> _operationHistory;
        private readonly SubjectBase<ConfigurationStateChangedEventArgs> _stateSubject;
        private readonly object _lockObject;

        private Models.Configuration.ConfigurationState _state;
        private Dictionary<string, string> _appliedEnvironmentVariables;
        private Dictionary<string, string> _backupEnvironmentVariables;
        private bool _disposed;

        #endregion

        #region 事件

        /// <summary>
        /// 配置状态变更事件
        /// </summary>
        public event EventHandler<ConfigurationStateChangedEventArgs>? StateChanged;

        /// <summary>
        /// 配置操作完成事件
        /// </summary>
        public event EventHandler<ConfigurationResult>? OperationCompleted;

        #endregion

        #region 属性

        /// <summary>
        /// 当前配置状态
        /// </summary>
        public Services.Configuration.ConfigurationState CurrentState => _state.Current;

        /// <summary>
        /// 配置是否已应用（环境变量是否已设置）
        /// </summary>
        public bool IsApplied => _state.EnvironmentVariablesApplied;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化配置管理器
        /// </summary>
        public ConfigurationManager()
        {
            _secureStorage = new SecureStorage();
            _configurationSchema = new Dictionary<string, ConfigurationItem>();
            _operationHistory = new List<ConfigurationResult>();
            _stateSubject = new ConfigurationStateSubject();
            _lockObject = new object();

            _state = new Models.Configuration.ConfigurationState();
            _appliedEnvironmentVariables = new Dictionary<string, string>();
            _backupEnvironmentVariables = new Dictionary<string, string>();

            InitializeConfigurationSchema();
            RegisterCleanupHandlers();
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 初始化配置管理器
        /// </summary>
        /// <returns>初始化结果</returns>
        public async Task<ConfigurationResult> InitializeAsync()
        {
            try
            {
                lock (_lockObject)
                {
                    if (_state.Current != Services.Configuration.ConfigurationState.Uninitialized)
                    {
                        return new ConfigurationResult(false, ConfigurationOperation.Set,
                            \"Configuration manager is already initialized\");
                    }

                    UpdateState(Services.Configuration.ConfigurationState.Initialized, \"Configuration manager initialized\");
                }

                var result = new ConfigurationResult(true, ConfigurationOperation.Set,
                    \"Configuration manager initialized successfully\");

                await NotifyOperationCompleted(result);
                return result;
            }
            catch (Exception ex)
            {
                var result = new ConfigurationResult(false, ConfigurationOperation.Set,
                    \"Failed to initialize configuration manager\", ex);

                UpdateState(Services.Configuration.ConfigurationState.Error, \"Initialization failed\");
                await NotifyOperationCompleted(result);
                return result;
            }
        }

        /// <summary>
        /// 设置Claude Code认证令牌
        /// </summary>
        /// <param name=\"token\">认证令牌（SecureString）</param>
        /// <returns>设置结果</returns>
        public async Task<ConfigurationResult> SetAuthTokenAsync(SecureString token)
        {
            ThrowIfDisposed();

            try
            {
                if (token == null || token.Length == 0)
                {
                    return new ConfigurationResult(false, ConfigurationOperation.Set,
                        \"Authentication token cannot be null or empty\");
                }

                lock (_lockObject)
                {
                    _secureStorage.Store(AUTH_TOKEN_KEY, token);
                }

                var result = new ConfigurationResult(true, ConfigurationOperation.Set,
                    \"Authentication token set successfully\");

                UpdateConfigurationState();
                await NotifyOperationCompleted(result);
                return result;
            }
            catch (Exception ex)
            {
                var result = new ConfigurationResult(false, ConfigurationOperation.Set,
                    \"Failed to set authentication token\", ex);

                _state.AddConfigurationError(\"Failed to set authentication token: \" + ex.Message);
                await NotifyOperationCompleted(result);
                return result;
            }
        }

        /// <summary>
        /// 设置Claude Code API基础URL
        /// </summary>
        /// <param name=\"baseUrl\">API基础URL</param>
        /// <returns>设置结果</returns>
        public async Task<ConfigurationResult> SetBaseUrlAsync(string baseUrl)
        {
            ThrowIfDisposed();

            try
            {
                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    baseUrl = DEFAULT_ANTHROPIC_BASE_URL;
                }

                // 验证URL格式
                if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != \"http\" && uri.Scheme != \"https\"))
                {
                    return new ConfigurationResult(false, ConfigurationOperation.Set,
                        \"Invalid base URL format\");
                }

                lock (_lockObject)
                {
                    _secureStorage.Store(BASE_URL_KEY, baseUrl);
                }

                var result = new ConfigurationResult(true, ConfigurationOperation.Set,
                    \"Base URL set successfully\");

                UpdateConfigurationState();
                await NotifyOperationCompleted(result);
                return result;
            }
            catch (Exception ex)
            {
                var result = new ConfigurationResult(false, ConfigurationOperation.Set,
                    \"Failed to set base URL\", ex);

                _state.AddConfigurationError(\"Failed to set base URL: \" + ex.Message);
                await NotifyOperationCompleted(result);
                return result;
            }
        }

        /// <summary>
        /// 应用配置（设置进程级环境变量）
        /// </summary>
        /// <returns>应用结果</returns>
        public async Task<ConfigurationResult> ApplyConfigurationAsync()
        {
            ThrowIfDisposed();

            try
            {
                // 验证配置
                var validationResult = await ValidateConfigurationAsync();
                if (!validationResult.IsSuccess)
                {
                    return new ConfigurationResult(false, ConfigurationOperation.Apply,
                        \"Configuration validation failed before applying\", validationResult.Exception);
                }

                // 备份当前环境变量
                BackupCurrentEnvironmentVariables();

                var appliedVariables = new List<string>();

                lock (_lockObject)
                {
                    // 设置认证令牌
                    if (_secureStorage.ContainsKey(AUTH_TOKEN_KEY))
                    {
                        var token = _secureStorage.GetString(AUTH_TOKEN_KEY);
                        if (!string.IsNullOrEmpty(token))
                        {
                            Environment.SetEnvironmentVariable(ANTHROPIC_AUTH_TOKEN, token, EnvironmentVariableTarget.Process);
                            _appliedEnvironmentVariables[ANTHROPIC_AUTH_TOKEN] = token;
                            appliedVariables.Add(ANTHROPIC_AUTH_TOKEN);
                        }
                    }

                    // 设置基础URL
                    if (_secureStorage.ContainsKey(BASE_URL_KEY))
                    {
                        var baseUrl = _secureStorage.GetString(BASE_URL_KEY);
                        if (!string.IsNullOrEmpty(baseUrl))
                        {
                            Environment.SetEnvironmentVariable(ANTHROPIC_BASE_URL, baseUrl, EnvironmentVariableTarget.Process);
                            _appliedEnvironmentVariables[ANTHROPIC_BASE_URL] = baseUrl;
                            appliedVariables.Add(ANTHROPIC_BASE_URL);
                        }
                    }
                }

                _state.UpdateEnvironmentVariableStatus(true, appliedVariables);
                UpdateState(Services.Configuration.ConfigurationState.Applied, \"Configuration applied to environment variables\");

                var result = new ConfigurationResult(true, ConfigurationOperation.Apply,
                    $\"Configuration applied successfully. Set {appliedVariables.Count} environment variables\");

                await NotifyOperationCompleted(result);
                return result;
            }
            catch (Exception ex)
            {
                var result = new ConfigurationResult(false, ConfigurationOperation.Apply,
                    \"Failed to apply configuration\", ex);

                _state.AddConfigurationError(\"Failed to apply configuration: \" + ex.Message);
                UpdateState(Services.Configuration.ConfigurationState.Error, \"Configuration application failed\");
                await NotifyOperationCompleted(result);
                return result;
            }
        }

        /// <summary>
        /// 验证当前配置
        /// </summary>
        /// <returns>验证结果</returns>
        public async Task<ConfigurationResult> ValidateConfigurationAsync()
        {
            ThrowIfDisposed();

            try
            {
                var errors = new List<string>();

                lock (_lockObject)
                {
                    // 验证必需的配置项
                    foreach (var schema in _configurationSchema.Values.Where(s => s.IsRequired))
                    {
                        if (schema.Key == AUTH_TOKEN_KEY && !_secureStorage.ContainsKey(AUTH_TOKEN_KEY))
                        {
                            errors.Add(\"Authentication token is required but not set\");
                        }
                    }

                    // 验证配置值
                    foreach (var schema in _configurationSchema.Values)
                    {
                        if (_secureStorage.ContainsKey(schema.Key))
                        {
                            var value = _secureStorage.GetString(schema.Key);
                            if (!schema.ValidateValue(value))
                            {
                                errors.Add($\"Invalid value for configuration item '{schema.Key}'\");
                            }
                        }
                    }
                }

                bool isValid = errors.Count == 0;
                _state.UpdateValidationResult(isValid);

                if (isValid)
                {
                    _state.ClearConfigurationErrors();
                }
                else
                {
                    foreach (var error in errors)
                    {
                        _state.AddConfigurationError(error);
                    }
                }

                var message = isValid
                    ? \"Configuration validation passed\"
                    : $\"Configuration validation failed with {errors.Count} errors\";

                var result = new ConfigurationResult(isValid, ConfigurationOperation.Validate, message);
                await NotifyOperationCompleted(result);
                return result;
            }
            catch (Exception ex)
            {
                var result = new ConfigurationResult(false, ConfigurationOperation.Validate,
                    \"Configuration validation encountered an error\", ex);

                _state.UpdateValidationResult(false);
                _state.AddConfigurationError(\"Validation error: \" + ex.Message);
                await NotifyOperationCompleted(result);
                return result;
            }
        }

        /// <summary>
        /// 健康检查（验证Claude Code是否能正常工作）
        /// </summary>
        /// <returns>健康检查结果</returns>
        public async Task<ConfigurationResult> HealthCheckAsync()
        {
            ThrowIfDisposed();

            try
            {
                // 检查环境变量是否设置
                var authToken = Environment.GetEnvironmentVariable(ANTHROPIC_AUTH_TOKEN, EnvironmentVariableTarget.Process);
                var baseUrl = Environment.GetEnvironmentVariable(ANTHROPIC_BASE_URL, EnvironmentVariableTarget.Process);

                if (string.IsNullOrEmpty(authToken))
                {
                    var result = new ConfigurationResult(false, ConfigurationOperation.Validate,
                        \"Health check failed: ANTHROPIC_AUTH_TOKEN environment variable is not set\");

                    _state.UpdateHealthCheckResult(false);
                    await NotifyOperationCompleted(result);
                    return result;
                }

                // 尝试执行claude-code命令进行健康检查
                var healthCheckResult = await PerformClaudeCodeHealthCheck();

                _state.UpdateHealthCheckResult(healthCheckResult);

                var message = healthCheckResult
                    ? \"Health check passed: Claude Code is working correctly\"
                    : \"Health check failed: Claude Code is not responding correctly\";

                var finalResult = new ConfigurationResult(healthCheckResult, ConfigurationOperation.Validate, message);
                await NotifyOperationCompleted(finalResult);
                return finalResult;
            }
            catch (Exception ex)
            {
                var result = new ConfigurationResult(false, ConfigurationOperation.Validate,
                    \"Health check encountered an error\", ex);

                _state.UpdateHealthCheckResult(false);
                await NotifyOperationCompleted(result);
                return result;
            }
        }

        /// <summary>
        /// 清理配置（清除环境变量和内存中的敏感信息）
        /// </summary>
        /// <returns>清理结果</returns>
        public async Task<ConfigurationResult> ClearConfigurationAsync()
        {
            try
            {
                var clearedItems = new List<string>();

                // 清除环境变量
                foreach (var envVar in _appliedEnvironmentVariables.Keys.ToList())
                {
                    Environment.SetEnvironmentVariable(envVar, null, EnvironmentVariableTarget.Process);
                    clearedItems.Add(envVar);
                }
                _appliedEnvironmentVariables.Clear();

                // 清除安全存储
                var memoryCleanupResult = _secureStorage.ClearAll(MemoryCleanupType.Forced);

                // 更新状态
                _state.UpdateEnvironmentVariableStatus(false);
                _state.ClearConfigurationErrors();
                UpdateState(Services.Configuration.ConfigurationState.Cleared, \"Configuration cleared\");

                var message = $\"Configuration cleared successfully. Removed {clearedItems.Count} environment variables and {memoryCleanupResult.ClearedItemsCount} secure storage items\";
                var result = new ConfigurationResult(true, ConfigurationOperation.Clear, message);

                await NotifyOperationCompleted(result);
                return result;
            }
            catch (Exception ex)
            {
                var result = new ConfigurationResult(false, ConfigurationOperation.Clear,
                    \"Failed to clear configuration\", ex);

                await NotifyOperationCompleted(result);
                return result;
            }
        }

        /// <summary>
        /// 回滚到上一个工作配置
        /// </summary>
        /// <returns>回滚结果</returns>
        public async Task<ConfigurationResult> RollbackConfigurationAsync()
        {
            ThrowIfDisposed();

            try
            {
                // 清除当前配置
                await ClearConfigurationAsync();

                // 恢复备份的环境变量
                var restoredVariables = new List<string>();
                foreach (var backup in _backupEnvironmentVariables)
                {
                    Environment.SetEnvironmentVariable(backup.Key, backup.Value, EnvironmentVariableTarget.Process);
                    restoredVariables.Add(backup.Key);
                }

                if (restoredVariables.Count > 0)
                {
                    _state.UpdateEnvironmentVariableStatus(true, restoredVariables);
                    UpdateState(Services.Configuration.ConfigurationState.Applied, \"Configuration rolled back\");
                }
                else
                {
                    UpdateState(Services.Configuration.ConfigurationState.Cleared, \"Configuration rolled back (no previous state)\");
                }

                var message = $\"Configuration rolled back successfully. Restored {restoredVariables.Count} environment variables\";
                var result = new ConfigurationResult(true, ConfigurationOperation.Rollback, message);

                await NotifyOperationCompleted(result);
                return result;
            }
            catch (Exception ex)
            {
                var result = new ConfigurationResult(false, ConfigurationOperation.Rollback,
                    \"Failed to rollback configuration\", ex);

                UpdateState(Services.Configuration.ConfigurationState.Error, \"Rollback failed\");
                await NotifyOperationCompleted(result);
                return result;
            }
        }

        /// <summary>
        /// 获取配置状态详情
        /// </summary>
        /// <returns>配置状态信息</returns>
        public Dictionary<string, object> GetStateDetails()
        {
            ThrowIfDisposed();

            lock (_lockObject)
            {
                var details = _state.GetSummary();
                details[\"SecureStorageStatistics\"] = _secureStorage.GetStatistics();
                details[\"AppliedEnvironmentVariables\"] = new Dictionary<string, object>(_appliedEnvironmentVariables.ToDictionary(
                    kvp => kvp.Key,
                    kvp => \"***HIDDEN***\" // 不暴露实际值
                ));
                return details;
            }
        }

        /// <summary>
        /// 获取配置操作历史（不包含敏感信息）
        /// </summary>
        /// <returns>操作历史</returns>
        public IEnumerable<ConfigurationResult> GetOperationHistory()
        {
            ThrowIfDisposed();

            lock (_lockObject)
            {
                return _operationHistory.ToList();
            }
        }

        /// <summary>
        /// 注册状态观察者
        /// </summary>
        /// <param name=\"observer\">观察者</param>
        public void RegisterStateObserver(IObserver<ConfigurationStateChangedEventArgs> observer)
        {
            _stateSubject.Attach(observer);
        }

        /// <summary>
        /// 注销状态观察者
        /// </summary>
        /// <param name=\"observer\">观察者</param>
        public void UnregisterStateObserver(IObserver<ConfigurationStateChangedEventArgs> observer)
        {
            _stateSubject.Detach(observer);
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 初始化配置架构
        /// </summary>
        private void InitializeConfigurationSchema()
        {
            _configurationSchema[AUTH_TOKEN_KEY] = new ConfigurationItem(
                AUTH_TOKEN_KEY,
                ConfigurationItemType.SecureString,
                true,
                \"Anthropic authentication token\",
                ANTHROPIC_AUTH_TOKEN,
                null,
                ConfigurationPriority.Critical,
                value => value != null && !string.IsNullOrWhiteSpace(value.ToString())
            );

            _configurationSchema[BASE_URL_KEY] = new ConfigurationItem(
                BASE_URL_KEY,
                ConfigurationItemType.Url,
                false,
                \"Anthropic API base URL\",
                ANTHROPIC_BASE_URL,
                DEFAULT_ANTHROPIC_BASE_URL,
                ConfigurationPriority.Normal,
                value => value == null || Uri.TryCreate(value.ToString(), UriKind.Absolute, out _)
            );
        }

        /// <summary>
        /// 注册清理处理器
        /// </summary>
        private void RegisterCleanupHandlers()
        {
            // 注册应用程序退出时的清理
            AppDomain.CurrentDomain.ProcessExit += (_, _) => {
                try
                {
                    ClearConfigurationAsync().Wait(TimeSpan.FromSeconds(5));
                }
                catch
                {
                    // 忽略清理时的异常
                }
            };

            // 注册未处理异常时的清理
            AppDomain.CurrentDomain.UnhandledException += (_, _) => {
                try
                {
                    ClearConfigurationAsync().Wait(TimeSpan.FromSeconds(2));
                }
                catch
                {
                    // 忽略清理时的异常
                }
            };
        }

        /// <summary>
        /// 备份当前环境变量
        /// </summary>
        private void BackupCurrentEnvironmentVariables()
        {
            _backupEnvironmentVariables.Clear();

            var authToken = Environment.GetEnvironmentVariable(ANTHROPIC_AUTH_TOKEN, EnvironmentVariableTarget.Process);
            if (!string.IsNullOrEmpty(authToken))
            {
                _backupEnvironmentVariables[ANTHROPIC_AUTH_TOKEN] = authToken;
            }

            var baseUrl = Environment.GetEnvironmentVariable(ANTHROPIC_BASE_URL, EnvironmentVariableTarget.Process);
            if (!string.IsNullOrEmpty(baseUrl))
            {
                _backupEnvironmentVariables[ANTHROPIC_BASE_URL] = baseUrl;
            }
        }

        /// <summary>
        /// 执行Claude Code健康检查
        /// </summary>
        /// <returns>健康检查结果</returns>
        private async Task<bool> PerformClaudeCodeHealthCheck()
        {
            try
            {
                using var process = new Process();
                process.StartInfo.FileName = \"claude-code\";
                process.StartInfo.Arguments = \"--version\";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                // 设置超时时间
                process.Start();

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                var completed = await Task.WhenAny(
                    Task.Delay(TimeSpan.FromSeconds(10)), // 10秒超时
                    Task.Run(() => process.WaitForExit())
                );

                if (completed == Task.Delay(TimeSpan.FromSeconds(10)))
                {
                    // 超时
                    try { process.Kill(); } catch { }
                    return false;
                }

                var output = await outputTask;
                var error = await errorTask;

                // 如果进程正常退出并且有版本输出，认为健康
                return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output);
            }
            catch
            {
                // 如果无法执行claude-code命令，返回false
                return false;
            }
        }

        /// <summary>
        /// 更新配置状态
        /// </summary>
        private void UpdateConfigurationState()
        {
            lock (_lockObject)
            {
                var configuredCount = 0;
                var requiredConfigured = true;

                foreach (var schema in _configurationSchema.Values)
                {
                    if (_secureStorage.ContainsKey(schema.Key))
                    {
                        configuredCount++;
                    }
                    else if (schema.IsRequired)
                    {
                        requiredConfigured = false;
                    }
                }

                _state.UpdateConfigurationStats(configuredCount, requiredConfigured);

                if (requiredConfigured && configuredCount > 0)
                {
                    UpdateState(Services.Configuration.ConfigurationState.Configured, \"All required configuration items are set\");
                }
            }
        }

        /// <summary>
        /// 更新状态
        /// </summary>
        /// <param name=\"newState\">新状态</param>
        /// <param name=\"reason\">状态变更原因</param>
        private void UpdateState(Services.Configuration.ConfigurationState newState, string reason)
        {
            var oldState = _state.Current;
            _state.UpdateState(newState, reason);

            var eventArgs = new ConfigurationStateChangedEventArgs(oldState, newState, reason);
            StateChanged?.Invoke(this, eventArgs);
            _stateSubject.Notify(eventArgs);
        }

        /// <summary>
        /// 通知操作完成
        /// </summary>
        /// <param name=\"result\">操作结果</param>
        private async Task NotifyOperationCompleted(ConfigurationResult result)
        {
            lock (_lockObject)
            {
                _operationHistory.Add(result);

                // 限制历史记录数量
                if (_operationHistory.Count > 100)
                {
                    _operationHistory.RemoveAt(0);
                }
            }

            OperationCompleted?.Invoke(this, result);
            await Task.CompletedTask;
        }

        /// <summary>
        /// 检查对象是否已释放
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ConfigurationManager));
        }

        #endregion

        #region IDisposable实现

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    ClearConfigurationAsync().Wait(TimeSpan.FromSeconds(5));
                }
                catch
                {
                    // 忽略清理时的异常
                }

                _secureStorage?.Dispose();
                _stateSubject?.ClearObservers();
                _disposed = true;
            }
        }

        /// <summary>
        /// 析构函数
        /// </summary>
        ~ConfigurationManager()
        {
            Dispose();
        }

        #endregion

        #region 嵌套类

        /// <summary>
        /// 配置状态主题实现
        /// </summary>
        private class ConfigurationStateSubject : SubjectBase<ConfigurationStateChangedEventArgs>
        {
            protected override void OnNotificationError(IObserver<ConfigurationStateChangedEventArgs> observer,
                ConfigurationStateChangedEventArgs data, Exception exception)
            {
                // 可以在这里记录日志
                // 当前忽略观察者通知错误
            }
        }

        #endregion
    }
}