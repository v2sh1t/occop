using System;
using System.Collections.Generic;
using Occop.Core.Services.Environment;

namespace Occop.Core.Models.Environment
{
    /// <summary>
    /// 环境信息
    /// </summary>
    public class EnvironmentInfo
    {
        /// <summary>
        /// 环境类型
        /// </summary>
        public EnvironmentType Type { get; }

        /// <summary>
        /// 检测状态
        /// </summary>
        public DetectionStatus Status { get; internal set; }

        /// <summary>
        /// 是否已安装
        /// </summary>
        public bool IsInstalled => Status == DetectionStatus.Detected || Status == DetectionStatus.IncompatibleVersion;

        /// <summary>
        /// 是否可用（已安装且版本兼容）
        /// </summary>
        public bool IsAvailable => Status == DetectionStatus.Detected;

        /// <summary>
        /// 安装路径
        /// </summary>
        public string? InstallPath { get; internal set; }

        /// <summary>
        /// 可执行文件路径
        /// </summary>
        public string? ExecutablePath { get; internal set; }

        /// <summary>
        /// 版本信息
        /// </summary>
        public string? Version { get; internal set; }

        /// <summary>
        /// 版本详细信息
        /// </summary>
        public Version? ParsedVersion { get; internal set; }

        /// <summary>
        /// 优先级（数值越高优先级越高）
        /// </summary>
        public int Priority { get; internal set; }

        /// <summary>
        /// 最小兼容版本
        /// </summary>
        public Version? MinimumCompatibleVersion { get; internal set; }

        /// <summary>
        /// 检测时间
        /// </summary>
        public DateTime DetectionTime { get; internal set; }

        /// <summary>
        /// 额外的环境属性
        /// </summary>
        public Dictionary<string, object> Properties { get; }

        /// <summary>
        /// 检测错误消息（如果有）
        /// </summary>
        public string? ErrorMessage { get; internal set; }

        /// <summary>
        /// 异常信息（如果有）
        /// </summary>
        public Exception? Exception { get; internal set; }

        /// <summary>
        /// 是否为推荐环境
        /// </summary>
        public bool IsRecommended { get; internal set; }

        /// <summary>
        /// 初始化环境信息
        /// </summary>
        /// <param name="type">环境类型</param>
        public EnvironmentInfo(EnvironmentType type)
        {
            Type = type;
            Status = DetectionStatus.NotDetected;
            DetectionTime = DateTime.UtcNow;
            Properties = new Dictionary<string, object>();
            Priority = GetDefaultPriority(type);
            MinimumCompatibleVersion = GetMinimumVersion(type);
        }

        /// <summary>
        /// 设置检测成功的环境信息
        /// </summary>
        /// <param name="installPath">安装路径</param>
        /// <param name="executablePath">可执行文件路径</param>
        /// <param name="version">版本信息</param>
        public void SetDetected(string installPath, string executablePath, string version)
        {
            if (string.IsNullOrEmpty(installPath))
                throw new ArgumentNullException(nameof(installPath));
            if (string.IsNullOrEmpty(executablePath))
                throw new ArgumentNullException(nameof(executablePath));
            if (string.IsNullOrEmpty(version))
                throw new ArgumentNullException(nameof(version));

            InstallPath = installPath;
            ExecutablePath = executablePath;
            Version = version;
            DetectionTime = DateTime.UtcNow;
            ErrorMessage = null;
            Exception = null;

            // 尝试解析版本号
            if (TryParseVersion(version, out var parsedVersion))
            {
                ParsedVersion = parsedVersion;
                Status = IsVersionCompatible(parsedVersion) ? DetectionStatus.Detected : DetectionStatus.IncompatibleVersion;
            }
            else
            {
                Status = DetectionStatus.Detected; // 无法解析版本时默认为兼容
            }
        }

        /// <summary>
        /// 设置检测失败的环境信息
        /// </summary>
        /// <param name="errorMessage">错误消息</param>
        /// <param name="exception">异常信息</param>
        public void SetFailed(string errorMessage, Exception? exception = null)
        {
            if (string.IsNullOrEmpty(errorMessage))
                throw new ArgumentNullException(nameof(errorMessage));

            Status = DetectionStatus.Failed;
            ErrorMessage = errorMessage;
            Exception = exception;
            DetectionTime = DateTime.UtcNow;
        }

        /// <summary>
        /// 添加环境属性
        /// </summary>
        /// <param name="key">属性键</param>
        /// <param name="value">属性值</param>
        public void AddProperty(string key, object value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            Properties[key] = value;
        }

        /// <summary>
        /// 获取环境属性
        /// </summary>
        /// <typeparam name="T">属性类型</typeparam>
        /// <param name="key">属性键</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>属性值</returns>
        public T GetProperty<T>(string key, T defaultValue = default(T))
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            if (Properties.TryGetValue(key, out var value) && value is T)
            {
                return (T)value;
            }

            return defaultValue;
        }

        /// <summary>
        /// 获取环境描述
        /// </summary>
        /// <returns>环境描述</returns>
        public string GetDescription()
        {
            switch (Status)
            {
                case DetectionStatus.NotDetected:
                    return $"{Type} 未检测";
                case DetectionStatus.Detecting:
                    return $"{Type} 检测中...";
                case DetectionStatus.Detected:
                    return $"{Type} {Version} (已安装)";
                case DetectionStatus.Failed:
                    return $"{Type} 检测失败: {ErrorMessage}";
                case DetectionStatus.IncompatibleVersion:
                    return $"{Type} {Version} (版本不兼容，需要 {MinimumCompatibleVersion}+)";
                default:
                    return $"{Type} 未知状态";
            }
        }

        /// <summary>
        /// 获取默认优先级
        /// </summary>
        /// <param name="type">环境类型</param>
        /// <returns>默认优先级</returns>
        private static int GetDefaultPriority(EnvironmentType type)
        {
            return type switch
            {
                EnvironmentType.PowerShellCore => 100,  // 最高优先级
                EnvironmentType.PowerShell51 => 90,
                EnvironmentType.GitBash => 80,
                EnvironmentType.ClaudeCode => 70,
                _ => 50
            };
        }

        /// <summary>
        /// 获取最小兼容版本
        /// </summary>
        /// <param name="type">环境类型</param>
        /// <returns>最小兼容版本</returns>
        private static Version? GetMinimumVersion(EnvironmentType type)
        {
            return type switch
            {
                EnvironmentType.PowerShell51 => new Version(5, 1, 0),
                EnvironmentType.PowerShellCore => new Version(7, 0, 0),
                EnvironmentType.GitBash => new Version(2, 20, 0),
                EnvironmentType.ClaudeCode => new Version(1, 0, 0),
                _ => null
            };
        }

        /// <summary>
        /// 尝试解析版本号
        /// </summary>
        /// <param name="versionString">版本字符串</param>
        /// <param name="version">解析出的版本</param>
        /// <returns>是否解析成功</returns>
        private static bool TryParseVersion(string versionString, out Version version)
        {
            version = null!;

            if (string.IsNullOrEmpty(versionString))
                return false;

            // 清理版本字符串，提取数字部分
            var cleanVersion = System.Text.RegularExpressions.Regex.Match(versionString, @"(\d+)\.(\d+)(?:\.(\d+))?(?:\.(\d+))?")?.Value;

            if (string.IsNullOrEmpty(cleanVersion))
                return false;

            return Version.TryParse(cleanVersion, out version);
        }

        /// <summary>
        /// 检查版本是否兼容
        /// </summary>
        /// <param name="version">要检查的版本</param>
        /// <returns>是否兼容</returns>
        private bool IsVersionCompatible(Version version)
        {
            return MinimumCompatibleVersion == null || version >= MinimumCompatibleVersion;
        }

        /// <summary>
        /// 返回环境信息的字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString()
        {
            return GetDescription();
        }
    }
}