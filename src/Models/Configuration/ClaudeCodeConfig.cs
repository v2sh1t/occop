using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text.RegularExpressions;
using Occop.Models.Configuration;

namespace Occop.Models.Configuration
{
    /// <summary>
    /// Claude Code专用配置常量
    /// </summary>
    public static class ClaudeCodeConfigConstants
    {
        /// <summary>
        /// Claude Code认证令牌环境变量名
        /// </summary>
        public const string AuthTokenEnvironmentVariable = "ANTHROPIC_AUTH_TOKEN";

        /// <summary>
        /// Claude Code API基础URL环境变量名
        /// </summary>
        public const string BaseUrlEnvironmentVariable = "ANTHROPIC_BASE_URL";

        /// <summary>
        /// 默认Claude Code API基础URL
        /// </summary>
        public const string DefaultBaseUrl = "https://api.anthropic.com";

        /// <summary>
        /// Claude Code认证令牌最小长度
        /// </summary>
        public const int MinTokenLength = 50;

        /// <summary>
        /// Claude Code认证令牌最大长度
        /// </summary>
        public const int MaxTokenLength = 200;

        /// <summary>
        /// Claude Code认证令牌前缀
        /// </summary>
        public const string TokenPrefix = "sk-ant-";
    }

    /// <summary>
    /// Claude Code配置验证器
    /// </summary>
    public static class ClaudeCodeConfigValidator
    {
        /// <summary>
        /// 验证认证令牌格式
        /// </summary>
        /// <param name="value">令牌值</param>
        /// <returns>是否有效</returns>
        public static bool ValidateAuthToken(object? value)
        {
            if (value == null)
                return false;

            string token = value switch
            {
                string str => str,
                SecureString secure => ConvertSecureStringToString(secure),
                _ => string.Empty
            };

            if (string.IsNullOrWhiteSpace(token))
                return false;

            // 检查长度
            if (token.Length < ClaudeCodeConfigConstants.MinTokenLength ||
                token.Length > ClaudeCodeConfigConstants.MaxTokenLength)
                return false;

            // 检查前缀
            if (!token.StartsWith(ClaudeCodeConfigConstants.TokenPrefix, StringComparison.OrdinalIgnoreCase))
                return false;

            // 检查格式：sk-ant-xxx
            var tokenPattern = @"^sk-ant-[a-zA-Z0-9\-_]+$";
            return Regex.IsMatch(token, tokenPattern);
        }

        /// <summary>
        /// 验证API基础URL格式
        /// </summary>
        /// <param name="value">URL值</param>
        /// <returns>是否有效</returns>
        public static bool ValidateBaseUrl(object? value)
        {
            if (value == null)
                return true; // 基础URL是可选的

            if (value is not string url || string.IsNullOrWhiteSpace(url))
                return false;

            // 检查是否为有效的HTTP/HTTPS URL
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return false;

            // 必须是HTTP或HTTPS协议
            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
        }

        /// <summary>
        /// 将SecureString转换为普通字符串（仅用于验证）
        /// </summary>
        /// <param name="secureString">安全字符串</param>
        /// <returns>普通字符串</returns>
        private static string ConvertSecureStringToString(SecureString secureString)
        {
            IntPtr unmanagedString = IntPtr.Zero;
            try
            {
                unmanagedString = System.Runtime.InteropServices.Marshal.SecureStringToGlobalAllocUnicode(secureString);
                return System.Runtime.InteropServices.Marshal.PtrToStringUni(unmanagedString) ?? string.Empty;
            }
            finally
            {
                if (unmanagedString != IntPtr.Zero)
                {
                    System.Runtime.InteropServices.Marshal.ZeroFreeGlobalAllocUnicode(unmanagedString);
                }
            }
        }
    }

    /// <summary>
    /// Claude Code专用配置定义
    /// </summary>
    public class ClaudeCodeConfig
    {
        /// <summary>
        /// 所有Claude Code配置项的定义
        /// </summary>
        private static readonly Dictionary<string, ConfigurationItem> _configurationItems;

        /// <summary>
        /// 静态构造函数，初始化配置项定义
        /// </summary>
        static ClaudeCodeConfig()
        {
            _configurationItems = new Dictionary<string, ConfigurationItem>
            {
                // Claude Code认证令牌配置项
                [ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable] = new ConfigurationItem(
                    key: ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable,
                    type: ConfigurationItemType.SecureString,
                    isRequired: true,
                    description: "Claude Code API认证令牌，用于访问Anthropic API",
                    environmentVariableName: ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable,
                    defaultValue: null,
                    priority: ConfigurationPriority.Critical,
                    validator: ClaudeCodeConfigValidator.ValidateAuthToken
                ),

                // Claude Code API基础URL配置项
                [ClaudeCodeConfigConstants.BaseUrlEnvironmentVariable] = new ConfigurationItem(
                    key: ClaudeCodeConfigConstants.BaseUrlEnvironmentVariable,
                    type: ConfigurationItemType.Url,
                    isRequired: false,
                    description: "Claude Code API基础URL，默认为官方API地址",
                    environmentVariableName: ClaudeCodeConfigConstants.BaseUrlEnvironmentVariable,
                    defaultValue: ClaudeCodeConfigConstants.DefaultBaseUrl,
                    priority: ConfigurationPriority.Normal,
                    validator: ClaudeCodeConfigValidator.ValidateBaseUrl
                )
            };
        }

        /// <summary>
        /// 获取所有Claude Code配置项定义
        /// </summary>
        /// <returns>配置项定义字典</returns>
        public static IReadOnlyDictionary<string, ConfigurationItem> GetConfigurationItems()
        {
            return _configurationItems.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        /// <summary>
        /// 获取指定的配置项定义
        /// </summary>
        /// <param name="key">配置项键名</param>
        /// <returns>配置项定义，如果不存在返回null</returns>
        public static ConfigurationItem? GetConfigurationItem(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;

            return _configurationItems.TryGetValue(key, out var item) ? item : null;
        }

        /// <summary>
        /// 获取必需的配置项列表
        /// </summary>
        /// <returns>必需的配置项键名列表</returns>
        public static IEnumerable<string> GetRequiredConfigurationKeys()
        {
            return _configurationItems.Where(kvp => kvp.Value.IsRequired).Select(kvp => kvp.Key);
        }

        /// <summary>
        /// 获取敏感配置项列表
        /// </summary>
        /// <returns>敏感配置项键名列表</returns>
        public static IEnumerable<string> GetSensitiveConfigurationKeys()
        {
            return _configurationItems.Where(kvp => kvp.Value.IsSensitive).Select(kvp => kvp.Key);
        }

        /// <summary>
        /// 获取环境变量映射
        /// </summary>
        /// <returns>配置键到环境变量名的映射</returns>
        public static IReadOnlyDictionary<string, string> GetEnvironmentVariableMapping()
        {
            return _configurationItems
                .Where(kvp => !string.IsNullOrEmpty(kvp.Value.EnvironmentVariableName))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.EnvironmentVariableName!);
        }

        /// <summary>
        /// 验证配置项值
        /// </summary>
        /// <param name="key">配置项键名</param>
        /// <param name="value">配置项值</param>
        /// <returns>验证结果</returns>
        public static bool ValidateConfigurationValue(string key, object? value)
        {
            var configItem = GetConfigurationItem(key);
            return configItem?.ValidateValue(value) ?? false;
        }

        /// <summary>
        /// 获取配置项摘要信息
        /// </summary>
        /// <returns>配置项摘要信息列表</returns>
        public static IEnumerable<Dictionary<string, object>> GetConfigurationSummary()
        {
            return _configurationItems.Values.Select(item => item.GetSummary());
        }

        /// <summary>
        /// 检查是否为Claude Code相关的环境变量
        /// </summary>
        /// <param name="environmentVariableName">环境变量名</param>
        /// <returns>是否为Claude Code相关的环境变量</returns>
        public static bool IsClaudeCodeEnvironmentVariable(string environmentVariableName)
        {
            if (string.IsNullOrEmpty(environmentVariableName))
                return false;

            return _configurationItems.Values.Any(item =>
                string.Equals(item.EnvironmentVariableName, environmentVariableName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 获取默认配置值
        /// </summary>
        /// <returns>默认配置值字典</returns>
        public static Dictionary<string, object?> GetDefaultValues()
        {
            return _configurationItems.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.DefaultValue
            );
        }

        /// <summary>
        /// 获取配置优先级排序的键名列表
        /// </summary>
        /// <returns>按优先级排序的配置键名列表</returns>
        public static IEnumerable<string> GetConfigurationKeysByPriority()
        {
            return _configurationItems
                .OrderByDescending(kvp => kvp.Value.Priority)
                .ThenBy(kvp => kvp.Key)
                .Select(kvp => kvp.Key);
        }
    }
}