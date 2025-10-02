using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Occop.Core.Patterns;
using Occop.Core.Patterns.Observer;
using Occop.Core.Common;

namespace Occop.Core.Managers
{
    /// <summary>
    /// 配置变更事件类型枚举
    /// </summary>
    public enum ConfigurationEventType
    {
        /// <summary>
        /// 配置加载
        /// </summary>
        ConfigurationLoaded,

        /// <summary>
        /// 配置保存
        /// </summary>
        ConfigurationSaved,

        /// <summary>
        /// 配置值变更
        /// </summary>
        ConfigurationChanged,

        /// <summary>
        /// 配置重置
        /// </summary>
        ConfigurationReset,

        /// <summary>
        /// 配置验证失败
        /// </summary>
        ConfigurationValidationFailed,

        /// <summary>
        /// 配置文件丢失
        /// </summary>
        ConfigurationFileMissing
    }

    /// <summary>
    /// 配置变更事件数据
    /// </summary>
    public class ConfigurationEventData
    {
        /// <summary>
        /// 事件类型
        /// </summary>
        public ConfigurationEventType EventType { get; }

        /// <summary>
        /// 事件发生时间
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// 配置键（如果适用）
        /// </summary>
        public string? Key { get; }

        /// <summary>
        /// 旧值（如果适用）
        /// </summary>
        public object? OldValue { get; }

        /// <summary>
        /// 新值（如果适用）
        /// </summary>
        public object? NewValue { get; }

        /// <summary>
        /// 事件消息
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 异常信息（如果有）
        /// </summary>
        public Exception? Exception { get; }

        /// <summary>
        /// 初始化配置变更事件数据
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <param name="message">事件消息</param>
        /// <param name="key">配置键</param>
        /// <param name="oldValue">旧值</param>
        /// <param name="newValue">新值</param>
        /// <param name="exception">异常信息</param>
        public ConfigurationEventData(
            ConfigurationEventType eventType,
            string message,
            string? key = null,
            object? oldValue = null,
            object? newValue = null,
            Exception? exception = null)
        {
            EventType = eventType;
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Key = key;
            OldValue = oldValue;
            NewValue = newValue;
            Exception = exception;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// 配置管理器接口
    /// </summary>
    public interface IConfigurationManager
    {
        /// <summary>
        /// 配置文件路径
        /// </summary>
        string ConfigurationFilePath { get; }

        /// <summary>
        /// 配置是否已加载
        /// </summary>
        bool IsLoaded { get; }

        /// <summary>
        /// 所有配置键
        /// </summary>
        IEnumerable<string> Keys { get; }

        /// <summary>
        /// 加载配置
        /// </summary>
        /// <returns>加载任务</returns>
        Task LoadAsync();

        /// <summary>
        /// 保存配置
        /// </summary>
        /// <returns>保存任务</returns>
        Task SaveAsync();

        /// <summary>
        /// 获取配置值
        /// </summary>
        /// <typeparam name="T">值类型</typeparam>
        /// <param name="key">配置键</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>配置值</returns>
        T GetValue<T>(string key, T defaultValue = default!);

        /// <summary>
        /// 设置配置值
        /// </summary>
        /// <typeparam name="T">值类型</typeparam>
        /// <param name="key">配置键</param>
        /// <param name="value">配置值</param>
        void SetValue<T>(string key, T value);

        /// <summary>
        /// 检查配置键是否存在
        /// </summary>
        /// <param name="key">配置键</param>
        /// <returns>是否存在</returns>
        bool HasKey(string key);

        /// <summary>
        /// 移除配置键
        /// </summary>
        /// <param name="key">配置键</param>
        /// <returns>是否移除成功</returns>
        bool RemoveKey(string key);

        /// <summary>
        /// 重置配置到默认值
        /// </summary>
        void Reset();

        /// <summary>
        /// 验证配置
        /// </summary>
        /// <returns>验证结果</returns>
        bool ValidateConfiguration();

        /// <summary>
        /// 注册配置变更观察者
        /// </summary>
        /// <param name="observer">观察者</param>
        void RegisterConfigurationObserver(IObserver<ConfigurationEventData> observer);

        /// <summary>
        /// 注销配置变更观察者
        /// </summary>
        /// <param name="observer">观察者</param>
        void UnregisterConfigurationObserver(IObserver<ConfigurationEventData> observer);
    }

    /// <summary>
    /// 配置项定义
    /// </summary>
    public class ConfigurationItem
    {
        /// <summary>
        /// 配置键
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// 默认值
        /// </summary>
        public object? DefaultValue { get; }

        /// <summary>
        /// 值类型
        /// </summary>
        public Type ValueType { get; }

        /// <summary>
        /// 是否必需
        /// </summary>
        public bool IsRequired { get; }

        /// <summary>
        /// 描述
        /// </summary>
        public string? Description { get; }

        /// <summary>
        /// 验证函数
        /// </summary>
        public Func<object?, bool>? Validator { get; }

        /// <summary>
        /// 初始化配置项
        /// </summary>
        /// <param name="key">配置键</param>
        /// <param name="valueType">值类型</param>
        /// <param name="defaultValue">默认值</param>
        /// <param name="isRequired">是否必需</param>
        /// <param name="description">描述</param>
        /// <param name="validator">验证函数</param>
        public ConfigurationItem(
            string key,
            Type valueType,
            object? defaultValue = null,
            bool isRequired = false,
            string? description = null,
            Func<object?, bool>? validator = null)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            ValueType = valueType ?? throw new ArgumentNullException(nameof(valueType));
            DefaultValue = defaultValue;
            IsRequired = isRequired;
            Description = description;
            Validator = validator;
        }
    }

    /// <summary>
    /// 配置管理器的单例实现
    /// </summary>
    public sealed class ConfigurationManager : Singleton<ConfigurationManager>, IConfigurationManager, ISingletonInitializer, IDisposable, INotifyPropertyChangedEx
    {
        private readonly SubjectBase<ConfigurationEventData> _configurationEventSubject;
        private readonly Dictionary<string, object?> _configurationData;
        private readonly Dictionary<string, ConfigurationItem> _configurationSchema;
        private readonly object _lockObject = new object();
        private string _configurationFilePath;
        private bool _isLoaded;
        private bool _disposed;

        /// <summary>
        /// 属性变更事件
        /// </summary>
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// 私有构造函数，确保单例模式
        /// </summary>
        public ConfigurationManager()
        {
            _configurationEventSubject = new ConfigurationEventSubject();
            _configurationData = new Dictionary<string, object?>();
            _configurationSchema = new Dictionary<string, ConfigurationItem>();
            _configurationFilePath = GetDefaultConfigurationPath();
            InitializeDefaultSchema();
        }

        /// <summary>
        /// 单例初始化
        /// </summary>
        public void Initialize()
        {
            // 在这里可以进行初始化操作
            NotifyConfigurationEvent(ConfigurationEventType.ConfigurationLoaded, "Configuration manager initialized");
        }

        #region 属性实现

        /// <summary>
        /// 配置文件路径
        /// </summary>
        public string ConfigurationFilePath
        {
            get
            {
                lock (_lockObject)
                {
                    return _configurationFilePath;
                }
            }
            private set
            {
                lock (_lockObject)
                {
                    if (_configurationFilePath != value)
                    {
                        _configurationFilePath = value;
                        OnPropertyChanged();
                    }
                }
            }
        }

        /// <summary>
        /// 配置是否已加载
        /// </summary>
        public bool IsLoaded
        {
            get
            {
                lock (_lockObject)
                {
                    return _isLoaded;
                }
            }
            private set
            {
                lock (_lockObject)
                {
                    if (_isLoaded != value)
                    {
                        _isLoaded = value;
                        OnPropertyChanged();
                    }
                }
            }
        }

        /// <summary>
        /// 所有配置键
        /// </summary>
        public IEnumerable<string> Keys
        {
            get
            {
                lock (_lockObject)
                {
                    return new List<string>(_configurationData.Keys);
                }
            }
        }

        #endregion

        #region 配置操作

        /// <summary>
        /// 加载配置
        /// </summary>
        /// <returns>加载任务</returns>
        public async Task LoadAsync()
        {
            try
            {
                lock (_lockObject)
                {
                    _configurationData.Clear();
                }

                if (File.Exists(_configurationFilePath))
                {
                    var jsonContent = await File.ReadAllTextAsync(_configurationFilePath);
                    var configurationDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonContent);

                    if (configurationDict != null)
                    {
                        lock (_lockObject)
                        {
                            foreach (var kvp in configurationDict)
                            {
                                _configurationData[kvp.Key] = DeserializeValue(kvp.Value, kvp.Key);
                            }
                        }
                    }

                    NotifyConfigurationEvent(ConfigurationEventType.ConfigurationLoaded, "Configuration loaded from file");
                }
                else
                {
                    LoadDefaultValues();
                    NotifyConfigurationEvent(ConfigurationEventType.ConfigurationFileMissing, "Configuration file not found, using defaults");
                }

                IsLoaded = true;
            }
            catch (Exception ex)
            {
                NotifyConfigurationEvent(ConfigurationEventType.ConfigurationValidationFailed, "Failed to load configuration", exception: ex);
                LoadDefaultValues();
                IsLoaded = false;
            }
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        /// <returns>保存任务</returns>
        public async Task SaveAsync()
        {
            try
            {
                var configurationDict = new Dictionary<string, object?>();

                lock (_lockObject)
                {
                    foreach (var kvp in _configurationData)
                    {
                        configurationDict[kvp.Key] = kvp.Value;
                    }
                }

                var jsonContent = JsonSerializer.Serialize(configurationDict, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                var directory = Path.GetDirectoryName(_configurationFilePath);
                if (directory != null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllTextAsync(_configurationFilePath, jsonContent);

                NotifyConfigurationEvent(ConfigurationEventType.ConfigurationSaved, "Configuration saved to file");
            }
            catch (Exception ex)
            {
                NotifyConfigurationEvent(ConfigurationEventType.ConfigurationValidationFailed, "Failed to save configuration", exception: ex);
                throw;
            }
        }

        /// <summary>
        /// 获取配置值
        /// </summary>
        /// <typeparam name="T">值类型</typeparam>
        /// <param name="key">配置键</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>配置值</returns>
        public T GetValue<T>(string key, T defaultValue = default!)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            lock (_lockObject)
            {
                if (_configurationData.TryGetValue(key, out var value))
                {
                    try
                    {
                        if (value is T directValue)
                            return directValue;

                        if (value is JsonElement jsonElement)
                            return JsonSerializer.Deserialize<T>(jsonElement.GetRawText()) ?? defaultValue;

                        return (T)Convert.ChangeType(value, typeof(T)) ?? defaultValue;
                    }
                    catch
                    {
                        return defaultValue;
                    }
                }

                return defaultValue;
            }
        }

        /// <summary>
        /// 设置配置值
        /// </summary>
        /// <typeparam name="T">值类型</typeparam>
        /// <param name="key">配置键</param>
        /// <param name="value">配置值</param>
        public void SetValue<T>(string key, T value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            object? oldValue;
            lock (_lockObject)
            {
                _configurationData.TryGetValue(key, out oldValue);

                // 验证值
                if (_configurationSchema.TryGetValue(key, out var schema) && schema.Validator != null)
                {
                    if (!schema.Validator(value))
                    {
                        throw new ArgumentException($"Value for key '{key}' failed validation", nameof(value));
                    }
                }

                _configurationData[key] = value;
            }

            NotifyConfigurationEvent(ConfigurationEventType.ConfigurationChanged,
                $"Configuration value changed for key '{key}'",
                key, oldValue, value);
        }

        /// <summary>
        /// 检查配置键是否存在
        /// </summary>
        /// <param name="key">配置键</param>
        /// <returns>是否存在</returns>
        public bool HasKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            lock (_lockObject)
            {
                return _configurationData.ContainsKey(key);
            }
        }

        /// <summary>
        /// 移除配置键
        /// </summary>
        /// <param name="key">配置键</param>
        /// <returns>是否移除成功</returns>
        public bool RemoveKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            object? oldValue = null;
            bool removed;

            lock (_lockObject)
            {
                _configurationData.TryGetValue(key, out oldValue);
                removed = _configurationData.Remove(key);
            }

            if (removed)
            {
                NotifyConfigurationEvent(ConfigurationEventType.ConfigurationChanged,
                    $"Configuration key '{key}' removed",
                    key, oldValue, null);
            }

            return removed;
        }

        /// <summary>
        /// 重置配置到默认值
        /// </summary>
        public void Reset()
        {
            lock (_lockObject)
            {
                _configurationData.Clear();
            }

            LoadDefaultValues();
            NotifyConfigurationEvent(ConfigurationEventType.ConfigurationReset, "Configuration reset to defaults");
        }

        /// <summary>
        /// 验证配置
        /// </summary>
        /// <returns>验证结果</returns>
        public bool ValidateConfiguration()
        {
            try
            {
                lock (_lockObject)
                {
                    foreach (var schema in _configurationSchema.Values)
                    {
                        if (schema.IsRequired && !_configurationData.ContainsKey(schema.Key))
                        {
                            NotifyConfigurationEvent(ConfigurationEventType.ConfigurationValidationFailed,
                                $"Required configuration key '{schema.Key}' is missing");
                            return false;
                        }

                        if (_configurationData.TryGetValue(schema.Key, out var value) && schema.Validator != null)
                        {
                            if (!schema.Validator(value))
                            {
                                NotifyConfigurationEvent(ConfigurationEventType.ConfigurationValidationFailed,
                                    $"Configuration value for key '{schema.Key}' failed validation");
                                return false;
                            }
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                NotifyConfigurationEvent(ConfigurationEventType.ConfigurationValidationFailed,
                    "Configuration validation failed", exception: ex);
                return false;
            }
        }

        #endregion

        #region 观察者模式

        /// <summary>
        /// 注册配置变更观察者
        /// </summary>
        /// <param name="observer">观察者</param>
        public void RegisterConfigurationObserver(IObserver<ConfigurationEventData> observer)
        {
            _configurationEventSubject.Attach(observer);
        }

        /// <summary>
        /// 注销配置变更观察者
        /// </summary>
        /// <param name="observer">观察者</param>
        public void UnregisterConfigurationObserver(IObserver<ConfigurationEventData> observer)
        {
            _configurationEventSubject.Detach(observer);
        }

        /// <summary>
        /// 通知配置事件
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <param name="message">事件消息</param>
        /// <param name="key">配置键</param>
        /// <param name="oldValue">旧值</param>
        /// <param name="newValue">新值</param>
        /// <param name="exception">异常信息</param>
        private void NotifyConfigurationEvent(
            ConfigurationEventType eventType,
            string message,
            string? key = null,
            object? oldValue = null,
            object? newValue = null,
            Exception? exception = null)
        {
            var eventData = new ConfigurationEventData(eventType, message, key, oldValue, newValue, exception);
            _configurationEventSubject.Notify(eventData);
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 获取默认配置文件路径
        /// </summary>
        /// <returns>配置文件路径</returns>
        private static string GetDefaultConfigurationPath()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appDataPath, "Occop", "config.json");
        }

        /// <summary>
        /// 初始化默认配置架构
        /// </summary>
        private void InitializeDefaultSchema()
        {
            // GitHub OAuth相关配置
            AddConfigurationItem("GitHubClientId", typeof(string), null, true, "GitHub OAuth Client ID");
            AddConfigurationItem("GitHubApiBaseUrl", typeof(string), "https://api.github.com", false, "GitHub API Base URL");
            AddConfigurationItem("GitHubDeviceFlowUrl", typeof(string), "https://github.com/login/device/code", false, "GitHub Device Flow URL");

            // 应用程序配置
            AddConfigurationItem("LogLevel", typeof(string), "Information", false, "Application log level");
            AddConfigurationItem("AutoSave", typeof(bool), true, false, "Auto-save configuration changes");
            AddConfigurationItem("Theme", typeof(string), "Light", false, "Application theme");

            // 安全配置
            AddConfigurationItem("TokenExpirationHours", typeof(int), 24, false, "Token expiration time in hours",
                value => value is int hours && hours > 0 && hours <= 168); // 1-168 hours (1 week max)

            AddConfigurationItem("MaxRetryAttempts", typeof(int), 3, false, "Maximum retry attempts",
                value => value is int attempts && attempts >= 0 && attempts <= 10);
        }

        /// <summary>
        /// 添加配置项定义
        /// </summary>
        /// <param name="key">配置键</param>
        /// <param name="valueType">值类型</param>
        /// <param name="defaultValue">默认值</param>
        /// <param name="isRequired">是否必需</param>
        /// <param name="description">描述</param>
        /// <param name="validator">验证函数</param>
        private void AddConfigurationItem(string key, Type valueType, object? defaultValue = null,
            bool isRequired = false, string? description = null, Func<object?, bool>? validator = null)
        {
            _configurationSchema[key] = new ConfigurationItem(key, valueType, defaultValue, isRequired, description, validator);
        }

        /// <summary>
        /// 加载默认值
        /// </summary>
        private void LoadDefaultValues()
        {
            lock (_lockObject)
            {
                foreach (var schema in _configurationSchema.Values)
                {
                    if (schema.DefaultValue != null)
                    {
                        _configurationData[schema.Key] = schema.DefaultValue;
                    }
                }
            }
        }

        /// <summary>
        /// 反序列化JSON值
        /// </summary>
        /// <param name="jsonElement">JSON元素</param>
        /// <param name="key">配置键</param>
        /// <returns>反序列化的值</returns>
        private object? DeserializeValue(JsonElement jsonElement, string key)
        {
            if (_configurationSchema.TryGetValue(key, out var schema))
            {
                return JsonSerializer.Deserialize(jsonElement.GetRawText(), schema.ValueType);
            }

            return jsonElement.ValueKind switch
            {
                JsonValueKind.String => jsonElement.GetString(),
                JsonValueKind.Number => jsonElement.TryGetInt64(out var longValue) ? longValue : jsonElement.GetDouble(),
                JsonValueKind.True or JsonValueKind.False => jsonElement.GetBoolean(),
                JsonValueKind.Null => null,
                _ => jsonElement.GetRawText()
            };
        }

        #endregion

        #region INotifyPropertyChanged实现

        /// <summary>
        /// 触发属性变更事件
        /// </summary>
        /// <param name="propertyName">属性名称</param>
        public void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 设置属性值并在值变更时触发通知
        /// </summary>
        /// <typeparam name="T">属性类型</typeparam>
        /// <param name="field">字段引用</param>
        /// <param name="value">新值</param>
        /// <param name="propertyName">属性名称</param>
        /// <returns>如果值发生了变更返回true，否则返回false</returns>
        public bool SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// 设置属性值并在值变更时触发通知，支持自定义变更后操作
        /// </summary>
        /// <typeparam name="T">属性类型</typeparam>
        /// <param name="field">字段引用</param>
        /// <param name="value">新值</param>
        /// <param name="onChanged">值变更后的回调操作</param>
        /// <param name="propertyName">属性名称</param>
        /// <returns>如果值发生了变更返回true，否则返回false</returns>
        public bool SetProperty<T>(ref T field, T value, Action onChanged, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            onChanged?.Invoke();
            return true;
        }

        #endregion

        #region 资源清理

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _configurationEventSubject.ClearObservers();
                _disposed = true;
            }
        }

        #endregion

        /// <summary>
        /// 配置事件主题的具体实现
        /// </summary>
        private class ConfigurationEventSubject : SubjectBase<ConfigurationEventData>
        {
            protected override void OnNotificationError(IObserver<ConfigurationEventData> observer, ConfigurationEventData data, Exception exception)
            {
                // 在实际项目中，这里可以记录日志
                // 当前为框架实现，暂时忽略错误
            }
        }
    }
}