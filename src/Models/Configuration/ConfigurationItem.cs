using System;
using System.Security;
using System.Collections.Generic;

namespace Occop.Models.Configuration
{
    /// <summary>
    /// 配置项类型枚举
    /// </summary>
    public enum ConfigurationItemType
    {
        /// <summary>
        /// 普通字符串
        /// </summary>
        String,

        /// <summary>
        /// 安全字符串（敏感信息）
        /// </summary>
        SecureString,

        /// <summary>
        /// 布尔值
        /// </summary>
        Boolean,

        /// <summary>
        /// 整数
        /// </summary>
        Integer,

        /// <summary>
        /// URL
        /// </summary>
        Url
    }

    /// <summary>
    /// 配置项优先级枚举
    /// </summary>
    public enum ConfigurationPriority
    {
        /// <summary>
        /// 低优先级
        /// </summary>
        Low = 1,

        /// <summary>
        /// 正常优先级
        /// </summary>
        Normal = 2,

        /// <summary>
        /// 高优先级
        /// </summary>
        High = 3,

        /// <summary>
        /// 关键优先级
        /// </summary>
        Critical = 4
    }

    /// <summary>
    /// 配置项定义
    /// 定义配置项的元数据和验证规则
    /// </summary>
    public class ConfigurationItem
    {
        /// <summary>
        /// 配置项键名
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// 配置项类型
        /// </summary>
        public ConfigurationItemType Type { get; }

        /// <summary>
        /// 是否必需
        /// </summary>
        public bool IsRequired { get; }

        /// <summary>
        /// 是否敏感信息
        /// </summary>
        public bool IsSensitive { get; }

        /// <summary>
        /// 默认值
        /// </summary>
        public object? DefaultValue { get; }

        /// <summary>
        /// 配置项描述
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// 环境变量名称（如果适用）
        /// </summary>
        public string? EnvironmentVariableName { get; }

        /// <summary>
        /// 配置项优先级
        /// </summary>
        public ConfigurationPriority Priority { get; }

        /// <summary>
        /// 验证函数
        /// </summary>
        public Func<object?, bool>? Validator { get; }

        /// <summary>
        /// 创建时间戳
        /// </summary>
        public DateTime CreatedAt { get; }

        /// <summary>
        /// 最后修改时间戳
        /// </summary>
        public DateTime LastModifiedAt { get; private set; }

        /// <summary>
        /// 初始化配置项
        /// </summary>
        /// <param name="key">配置项键名</param>
        /// <param name="type">配置项类型</param>
        /// <param name="isRequired">是否必需</param>
        /// <param name="description">配置项描述</param>
        /// <param name="environmentVariableName">环境变量名称</param>
        /// <param name="defaultValue">默认值</param>
        /// <param name="priority">优先级</param>
        /// <param name="validator">验证函数</param>
        public ConfigurationItem(
            string key,
            ConfigurationItemType type,
            bool isRequired,
            string description,
            string? environmentVariableName = null,
            object? defaultValue = null,
            ConfigurationPriority priority = ConfigurationPriority.Normal,
            Func<object?, bool>? validator = null)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Type = type;
            IsRequired = isRequired;
            IsSensitive = type == ConfigurationItemType.SecureString;
            Description = description ?? throw new ArgumentNullException(nameof(description));
            EnvironmentVariableName = environmentVariableName;
            DefaultValue = defaultValue;
            Priority = priority;
            Validator = validator;
            CreatedAt = DateTime.UtcNow;
            LastModifiedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// 更新最后修改时间
        /// </summary>
        internal void UpdateLastModified()
        {
            LastModifiedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// 验证值是否有效
        /// </summary>
        /// <param name="value">要验证的值</param>
        /// <returns>验证结果</returns>
        public bool ValidateValue(object? value)
        {
            // 检查必需项
            if (IsRequired && value == null)
                return false;

            // 类型验证
            if (value != null && !IsValidType(value))
                return false;

            // 自定义验证器
            if (Validator != null && !Validator(value))
                return false;

            return true;
        }

        /// <summary>
        /// 检查值是否符合类型要求
        /// </summary>
        /// <param name="value">要检查的值</param>
        /// <returns>是否符合类型要求</returns>
        private bool IsValidType(object value)
        {
            return Type switch
            {
                ConfigurationItemType.String => value is string,
                ConfigurationItemType.SecureString => value is SecureString,
                ConfigurationItemType.Boolean => value is bool,
                ConfigurationItemType.Integer => value is int,
                ConfigurationItemType.Url => value is string url && Uri.TryCreate(url, UriKind.Absolute, out _),
                _ => false
            };
        }

        /// <summary>
        /// 获取配置项摘要信息（不包含敏感数据）
        /// </summary>
        /// <returns>摘要信息</returns>
        public Dictionary<string, object> GetSummary()
        {
            return new Dictionary<string, object>
            {
                { "Key", Key },
                { "Type", Type.ToString() },
                { "IsRequired", IsRequired },
                { "IsSensitive", IsSensitive },
                { "Description", Description },
                { "EnvironmentVariableName", EnvironmentVariableName ?? "N/A" },
                { "Priority", Priority.ToString() },
                { "CreatedAt", CreatedAt },
                { "LastModifiedAt", LastModifiedAt },
                { "HasDefaultValue", DefaultValue != null },
                { "HasValidator", Validator != null }
            };
        }
    }
}