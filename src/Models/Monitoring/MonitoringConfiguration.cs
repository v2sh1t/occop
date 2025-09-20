using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Occop.Models.Monitoring
{
    /// <summary>
    /// 监控配置管理类
    /// 统一管理所有监控相关的配置参数，支持配置的持久化和动态更新
    /// </summary>
    public class MonitoringConfiguration
    {
        #region 基础配置

        /// <summary>
        /// 配置版本号
        /// </summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// 配置创建时间
        /// </summary>
        public DateTime CreatedTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 配置最后修改时间
        /// </summary>
        public DateTime LastModifiedTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 配置文件路径
        /// </summary>
        [JsonIgnore]
        public string ConfigFilePath { get; set; }

        /// <summary>
        /// 是否启用监控
        /// </summary>
        public bool IsMonitoringEnabled { get; set; } = true;

        #endregion

        #region 进程监控配置

        /// <summary>
        /// 进程监控配置
        /// </summary>
        public ProcessMonitoringConfig ProcessMonitoring { get; set; } = new();

        /// <summary>
        /// 是否启用WMI事件监听
        /// </summary>
        public bool EnableWmiEventListening { get; set; } = true;

        /// <summary>
        /// WMI监听器配置
        /// </summary>
        public WmiListenerConfig WmiListener { get; set; } = new();

        /// <summary>
        /// 是否启用定时轮询（兜底机制）
        /// </summary>
        public bool EnablePollingFallback { get; set; } = true;

        /// <summary>
        /// 轮询间隔（毫秒）
        /// </summary>
        public int PollingIntervalMs { get; set; } = 5000;

        #endregion

        #region 性能和资源配置

        /// <summary>
        /// 内存使用警报阈值（MB）
        /// </summary>
        public long MemoryWarningThresholdMB { get; set; } = 500;

        /// <summary>
        /// 内存使用严重警报阈值（MB）
        /// </summary>
        public long MemoryCriticalThresholdMB { get; set; } = 1000;

        /// <summary>
        /// CPU使用率警报阈值（百分比）
        /// </summary>
        public double CpuWarningThreshold { get; set; } = 80.0;

        /// <summary>
        /// CPU使用率严重警报阈值（百分比）
        /// </summary>
        public double CpuCriticalThreshold { get; set; } = 95.0;

        /// <summary>
        /// 句柄数量警报阈值
        /// </summary>
        public int HandleWarningThreshold { get; set; } = 1000;

        /// <summary>
        /// 线程数量警报阈值
        /// </summary>
        public int ThreadWarningThreshold { get; set; } = 100;

        /// <summary>
        /// 监控系统内存限制（MB）
        /// </summary>
        public long MonitoringSystemMemoryLimitMB { get; set; } = 10;

        #endregion

        #region 持久化配置

        /// <summary>
        /// 是否启用状态持久化
        /// </summary>
        public bool EnableStatePersistence { get; set; } = true;

        /// <summary>
        /// 状态文件保存路径
        /// </summary>
        public string StateFilePath { get; set; } = "monitoring_state.json";

        /// <summary>
        /// 自动保存状态间隔（分钟）
        /// </summary>
        public int AutoSaveIntervalMinutes { get; set; } = 5;

        /// <summary>
        /// 是否启用事件历史持久化
        /// </summary>
        public bool EnableEventHistoryPersistence { get; set; } = true;

        /// <summary>
        /// 事件历史文件保存路径
        /// </summary>
        public string EventHistoryFilePath { get; set; } = "monitoring_events.json";

        /// <summary>
        /// 事件历史保留天数
        /// </summary>
        public int EventHistoryRetentionDays { get; set; } = 7;

        #endregion

        #region 日志配置

        /// <summary>
        /// 是否启用详细日志
        /// </summary>
        public bool EnableVerboseLogging { get; set; } = false;

        /// <summary>
        /// 日志级别
        /// </summary>
        public string LogLevel { get; set; } = "Information";

        /// <summary>
        /// 日志文件路径
        /// </summary>
        public string LogFilePath { get; set; } = "monitoring.log";

        /// <summary>
        /// 日志文件最大大小（MB）
        /// </summary>
        public int LogFileMaxSizeMB { get; set; } = 50;

        /// <summary>
        /// 保留的日志文件数量
        /// </summary>
        public int LogFileRetainCount { get; set; } = 5;

        #endregion

        #region 健康检查配置

        /// <summary>
        /// 是否启用健康检查
        /// </summary>
        public bool EnableHealthCheck { get; set; } = true;

        /// <summary>
        /// 健康检查间隔（分钟）
        /// </summary>
        public int HealthCheckIntervalMinutes { get; set; } = 10;

        /// <summary>
        /// 健康检查超时时间（秒）
        /// </summary>
        public int HealthCheckTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// 启用的健康检查项目
        /// </summary>
        public List<string> EnabledHealthChecks { get; set; } = new()
        {
            "MemoryUsage",
            "ProcessCount",
            "EventRate",
            "WmiConnection",
            "StatePersistence"
        };

        #endregion

        #region 通知配置

        /// <summary>
        /// 是否启用通知
        /// </summary>
        public bool EnableNotifications { get; set; } = false;

        /// <summary>
        /// 通知配置
        /// </summary>
        public Dictionary<string, object> NotificationSettings { get; set; } = new();

        /// <summary>
        /// 需要发送通知的事件类型
        /// </summary>
        public List<MonitoringEventType> NotificationEventTypes { get; set; } = new()
        {
            MonitoringEventType.Error,
            MonitoringEventType.ProcessKilled,
            MonitoringEventType.PerformanceAlert
        };

        #endregion

        #region 高级配置

        /// <summary>
        /// 自定义配置项
        /// </summary>
        public Dictionary<string, object> CustomSettings { get; set; } = new();

        /// <summary>
        /// 实验性功能开关
        /// </summary>
        public Dictionary<string, bool> ExperimentalFeatures { get; set; } = new();

        /// <summary>
        /// 调试模式
        /// </summary>
        public bool DebugMode { get; set; } = false;

        /// <summary>
        /// 监控启动延迟（秒）
        /// </summary>
        public int StartupDelaySeconds { get; set; } = 0;

        /// <summary>
        /// 优雅关闭超时（秒）
        /// </summary>
        public int GracefulShutdownTimeoutSeconds { get; set; } = 30;

        #endregion

        #region 构造函数

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public MonitoringConfiguration()
        {
            InitializeDefaults();
        }

        /// <summary>
        /// 带配置文件路径的构造函数
        /// </summary>
        /// <param name="configFilePath">配置文件路径</param>
        public MonitoringConfiguration(string configFilePath) : this()
        {
            ConfigFilePath = configFilePath;
        }

        #endregion

        #region 初始化方法

        /// <summary>
        /// 初始化默认配置
        /// </summary>
        private void InitializeDefaults()
        {
            // 设置默认的自定义配置
            CustomSettings["MaxConcurrentProcesses"] = 50;
            CustomSettings["EventBatchSize"] = 100;
            CustomSettings["BackgroundTaskInterval"] = 60;

            // 设置实验性功能
            ExperimentalFeatures["EnableProcessTreeMonitoring"] = false;
            ExperimentalFeatures["EnablePerformancePrediction"] = false;
            ExperimentalFeatures["EnableAutoProcessRecovery"] = false;
        }

        #endregion

        #region 配置验证

        /// <summary>
        /// 验证配置的有效性
        /// </summary>
        /// <returns>验证结果</returns>
        public ConfigurationValidationResult Validate()
        {
            var result = new ConfigurationValidationResult();

            try
            {
                // 验证基础配置
                if (PollingIntervalMs < 1000)
                {
                    result.AddWarning("轮询间隔过短可能影响性能，建议设置为1000ms以上");
                }

                if (ProcessMonitoring.MaxMonitoredProcesses <= 0)
                {
                    result.AddError("最大监控进程数量必须大于0");
                }

                // 验证内存配置
                if (MemoryWarningThresholdMB >= MemoryCriticalThresholdMB)
                {
                    result.AddError("内存警报阈值必须小于严重警报阈值");
                }

                if (MonitoringSystemMemoryLimitMB <= 0)
                {
                    result.AddError("监控系统内存限制必须大于0");
                }

                // 验证CPU配置
                if (CpuWarningThreshold >= CpuCriticalThreshold)
                {
                    result.AddError("CPU警报阈值必须小于严重警报阈值");
                }

                if (CpuWarningThreshold < 0 || CpuWarningThreshold > 100)
                {
                    result.AddError("CPU警报阈值必须在0-100之间");
                }

                // 验证文件路径
                if (EnableStatePersistence && string.IsNullOrWhiteSpace(StateFilePath))
                {
                    result.AddError("启用状态持久化时，状态文件路径不能为空");
                }

                if (EnableEventHistoryPersistence && string.IsNullOrWhiteSpace(EventHistoryFilePath))
                {
                    result.AddError("启用事件历史持久化时，事件历史文件路径不能为空");
                }

                // 验证时间间隔
                if (AutoSaveIntervalMinutes <= 0)
                {
                    result.AddError("自动保存间隔必须大于0");
                }

                if (HealthCheckIntervalMinutes <= 0)
                {
                    result.AddError("健康检查间隔必须大于0");
                }

                // 验证保留期限
                if (EventHistoryRetentionDays <= 0)
                {
                    result.AddWarning("事件历史保留天数设置过短，可能导致历史数据丢失");
                }

                result.IsValid = result.Errors.Count == 0;
            }
            catch (Exception ex)
            {
                result.AddError($"配置验证过程中发生异常: {ex.Message}");
                result.IsValid = false;
            }

            return result;
        }

        #endregion

        #region 配置持久化

        /// <summary>
        /// 保存配置到文件
        /// </summary>
        /// <param name="filePath">文件路径（可选，默认使用ConfigFilePath）</param>
        /// <returns>保存结果</returns>
        public MonitoringResult SaveToFile(string filePath = null)
        {
            try
            {
                var targetPath = filePath ?? ConfigFilePath;
                if (string.IsNullOrWhiteSpace(targetPath))
                {
                    return MonitoringResult.Failure("未指定配置文件路径");
                }

                // 更新修改时间
                LastModifiedTime = DateTime.UtcNow;

                // 序列化配置
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                var json = JsonSerializer.Serialize(this, options);

                // 确保目录存在
                var directory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 写入文件
                File.WriteAllText(targetPath, json);

                ConfigFilePath = targetPath;
                return MonitoringResult.Success($"配置已保存到: {targetPath}");
            }
            catch (Exception ex)
            {
                return MonitoringResult.Failure($"保存配置失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 从文件加载配置
        /// </summary>
        /// <param name="filePath">配置文件路径</param>
        /// <returns>配置对象</returns>
        public static MonitoringConfiguration LoadFromFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"配置文件不存在: {filePath}");
                }

                var json = File.ReadAllText(filePath);

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                };

                var config = JsonSerializer.Deserialize<MonitoringConfiguration>(json, options);
                config.ConfigFilePath = filePath;

                return config;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"加载配置文件失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 创建默认配置并保存
        /// </summary>
        /// <param name="filePath">保存路径</param>
        /// <returns>配置对象</returns>
        public static MonitoringConfiguration CreateDefault(string filePath = "monitoring_config.json")
        {
            var config = new MonitoringConfiguration(filePath);
            config.SaveToFile();
            return config;
        }

        #endregion

        #region 配置更新

        /// <summary>
        /// 更新配置项
        /// </summary>
        /// <param name="key">配置键</param>
        /// <param name="value">配置值</param>
        /// <returns>更新结果</returns>
        public MonitoringResult UpdateSetting(string key, object value)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    return MonitoringResult.Failure("配置键不能为空");
                }

                // 使用反射更新属性
                var property = GetType().GetProperty(key);
                if (property != null && property.CanWrite)
                {
                    var convertedValue = Convert.ChangeType(value, property.PropertyType);
                    property.SetValue(this, convertedValue);
                    LastModifiedTime = DateTime.UtcNow;
                    return MonitoringResult.Success($"配置项 {key} 已更新");
                }

                // 检查是否为自定义配置
                if (key.StartsWith("Custom."))
                {
                    var customKey = key.Substring(7);
                    CustomSettings[customKey] = value;
                    LastModifiedTime = DateTime.UtcNow;
                    return MonitoringResult.Success($"自定义配置项 {customKey} 已更新");
                }

                return MonitoringResult.Failure($"未找到配置项: {key}");
            }
            catch (Exception ex)
            {
                return MonitoringResult.Failure($"更新配置项失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 获取配置项值
        /// </summary>
        /// <typeparam name="T">返回类型</typeparam>
        /// <param name="key">配置键</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>配置值</returns>
        public T GetSetting<T>(string key, T defaultValue = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    return defaultValue;
                }

                // 检查属性
                var property = GetType().GetProperty(key);
                if (property != null && property.CanRead)
                {
                    var value = property.GetValue(this);
                    return value != null ? (T)Convert.ChangeType(value, typeof(T)) : defaultValue;
                }

                // 检查自定义配置
                if (key.StartsWith("Custom."))
                {
                    var customKey = key.Substring(7);
                    if (CustomSettings.ContainsKey(customKey))
                    {
                        return (T)Convert.ChangeType(CustomSettings[customKey], typeof(T));
                    }
                }

                return defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// 重置为默认配置
        /// </summary>
        public void ResetToDefaults()
        {
            var defaultConfig = new MonitoringConfiguration();

            // 保留配置文件路径和基本信息
            var originalPath = ConfigFilePath;
            var originalCreatedTime = CreatedTime;

            // 复制所有属性
            foreach (var property in GetType().GetProperties())
            {
                if (property.CanWrite && property.Name != nameof(ConfigFilePath) && property.Name != nameof(CreatedTime))
                {
                    property.SetValue(this, property.GetValue(defaultConfig));
                }
            }

            ConfigFilePath = originalPath;
            CreatedTime = originalCreatedTime;
            LastModifiedTime = DateTime.UtcNow;
        }

        #endregion

        #region 配置比较和合并

        /// <summary>
        /// 比较配置差异
        /// </summary>
        /// <param name="other">另一个配置</param>
        /// <returns>差异列表</returns>
        public List<ConfigurationDifference> CompareWith(MonitoringConfiguration other)
        {
            var differences = new List<ConfigurationDifference>();

            if (other == null)
            {
                differences.Add(new ConfigurationDifference("Configuration", "Current", "null", ConfigurationChangeType.Removed));
                return differences;
            }

            foreach (var property in GetType().GetProperties())
            {
                if (property.CanRead && property.Name != nameof(LastModifiedTime))
                {
                    var currentValue = property.GetValue(this);
                    var otherValue = property.GetValue(other);

                    if (!Equals(currentValue, otherValue))
                    {
                        differences.Add(new ConfigurationDifference(
                            property.Name,
                            currentValue?.ToString(),
                            otherValue?.ToString(),
                            ConfigurationChangeType.Modified));
                    }
                }
            }

            return differences;
        }

        /// <summary>
        /// 合并另一个配置
        /// </summary>
        /// <param name="other">要合并的配置</param>
        /// <param name="overwriteExisting">是否覆盖现有值</param>
        /// <returns>合并结果</returns>
        public MonitoringResult MergeWith(MonitoringConfiguration other, bool overwriteExisting = false)
        {
            try
            {
                if (other == null)
                {
                    return MonitoringResult.Failure("要合并的配置不能为空");
                }

                var mergedCount = 0;

                foreach (var property in GetType().GetProperties())
                {
                    if (property.CanWrite && property.Name != nameof(ConfigFilePath) && property.Name != nameof(CreatedTime))
                    {
                        var currentValue = property.GetValue(this);
                        var otherValue = property.GetValue(other);

                        if (otherValue != null && (overwriteExisting || currentValue == null || currentValue.Equals(GetDefaultValue(property.PropertyType))))
                        {
                            property.SetValue(this, otherValue);
                            mergedCount++;
                        }
                    }
                }

                LastModifiedTime = DateTime.UtcNow;
                return MonitoringResult.Success($"已合并 {mergedCount} 个配置项");
            }
            catch (Exception ex)
            {
                return MonitoringResult.Failure($"配置合并失败: {ex.Message}", ex);
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取类型的默认值
        /// </summary>
        /// <param name="type">类型</param>
        /// <returns>默认值</returns>
        private static object GetDefaultValue(Type type)
        {
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }

        /// <summary>
        /// 获取配置摘要
        /// </summary>
        /// <returns>配置摘要</returns>
        public string GetSummary()
        {
            return $"监控配置 v{Version} - 监控: {(IsMonitoringEnabled ? "启用" : "禁用")}, " +
                   $"WMI: {(EnableWmiEventListening ? "启用" : "禁用")}, " +
                   $"轮询: {PollingIntervalMs}ms, " +
                   $"进程数限制: {ProcessMonitoring.MaxMonitoredProcesses}";
        }

        #endregion

        #region 重写方法

        /// <summary>
        /// 获取字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString()
        {
            return GetSummary();
        }

        #endregion
    }

    #region 辅助类

    /// <summary>
    /// 配置验证结果
    /// </summary>
    public class ConfigurationValidationResult
    {
        /// <summary>
        /// 是否有效
        /// </summary>
        public bool IsValid { get; set; } = true;

        /// <summary>
        /// 错误列表
        /// </summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// 警告列表
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// 添加错误
        /// </summary>
        /// <param name="error">错误信息</param>
        public void AddError(string error)
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                Errors.Add(error);
                IsValid = false;
            }
        }

        /// <summary>
        /// 添加警告
        /// </summary>
        /// <param name="warning">警告信息</param>
        public void AddWarning(string warning)
        {
            if (!string.IsNullOrWhiteSpace(warning))
            {
                Warnings.Add(warning);
            }
        }

        /// <summary>
        /// 获取摘要
        /// </summary>
        /// <returns>验证结果摘要</returns>
        public string GetSummary()
        {
            return $"验证结果: {(IsValid ? "有效" : "无效")} - 错误: {Errors.Count}, 警告: {Warnings.Count}";
        }
    }

    /// <summary>
    /// 配置差异
    /// </summary>
    public class ConfigurationDifference
    {
        /// <summary>
        /// 属性名称
        /// </summary>
        public string PropertyName { get; set; }

        /// <summary>
        /// 当前值
        /// </summary>
        public string CurrentValue { get; set; }

        /// <summary>
        /// 另一个值
        /// </summary>
        public string OtherValue { get; set; }

        /// <summary>
        /// 变更类型
        /// </summary>
        public ConfigurationChangeType ChangeType { get; set; }

        public ConfigurationDifference(string propertyName, string currentValue, string otherValue, ConfigurationChangeType changeType)
        {
            PropertyName = propertyName;
            CurrentValue = currentValue;
            OtherValue = otherValue;
            ChangeType = changeType;
        }

        public override string ToString()
        {
            return $"{PropertyName}: {CurrentValue} -> {OtherValue} ({ChangeType})";
        }
    }

    /// <summary>
    /// 配置变更类型
    /// </summary>
    public enum ConfigurationChangeType
    {
        /// <summary>
        /// 新增
        /// </summary>
        Added,

        /// <summary>
        /// 修改
        /// </summary>
        Modified,

        /// <summary>
        /// 移除
        /// </summary>
        Removed
    }

    #endregion
}