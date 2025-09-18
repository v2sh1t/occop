using System;
using System.Collections.Generic;
using Occop.Core.Services.Environment;

namespace Occop.Core.Models.Environment
{
    /// <summary>
    /// Claude Code CLI 信息
    /// </summary>
    public class ClaudeCodeInfo
    {
        /// <summary>
        /// 版本号
        /// </summary>
        public string? Version { get; set; }

        /// <summary>
        /// 解析后的版本对象
        /// </summary>
        public Version? ParsedVersion { get; set; }

        /// <summary>
        /// 可执行文件路径
        /// </summary>
        public string? ExecutablePath { get; set; }

        /// <summary>
        /// 安装路径
        /// </summary>
        public string? InstallPath { get; set; }

        /// <summary>
        /// 检测状态
        /// </summary>
        public DetectionStatus Status { get; set; }

        /// <summary>
        /// 是否为推荐版本
        /// </summary>
        public bool IsRecommendedVersion { get; set; }

        /// <summary>
        /// 兼容性等级
        /// </summary>
        public CompatibilityLevel Compatibility { get; set; }

        /// <summary>
        /// 支持的功能列表
        /// </summary>
        public List<ClaudeCodeFeature> SupportedFeatures { get; set; }

        /// <summary>
        /// API 端点信息
        /// </summary>
        public string? ApiEndpoint { get; set; }

        /// <summary>
        /// 用户认证状态
        /// </summary>
        public AuthenticationStatus AuthStatus { get; set; }

        /// <summary>
        /// 最后检测时间
        /// </summary>
        public DateTime LastDetectionTime { get; set; }

        /// <summary>
        /// 检测错误信息
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 异常信息
        /// </summary>
        public Exception? Exception { get; set; }

        /// <summary>
        /// 配置文件路径
        /// </summary>
        public string? ConfigFilePath { get; set; }

        /// <summary>
        /// 环境变量设置
        /// </summary>
        public Dictionary<string, string> EnvironmentVariables { get; set; }

        /// <summary>
        /// 性能指标
        /// </summary>
        public PerformanceMetrics? Performance { get; set; }

        /// <summary>
        /// 初始化 Claude Code 信息
        /// </summary>
        public ClaudeCodeInfo()
        {
            Status = DetectionStatus.NotDetected;
            Compatibility = CompatibilityLevel.Unknown;
            SupportedFeatures = new List<ClaudeCodeFeature>();
            AuthStatus = AuthenticationStatus.Unknown;
            LastDetectionTime = DateTime.MinValue;
            EnvironmentVariables = new Dictionary<string, string>();
            IsRecommendedVersion = false;
        }

        /// <summary>
        /// 设置检测成功状态
        /// </summary>
        /// <param name="version">版本号</param>
        /// <param name="executablePath">可执行文件路径</param>
        /// <param name="installPath">安装路径</param>
        public void SetDetected(string version, string executablePath, string? installPath = null)
        {
            if (string.IsNullOrEmpty(version))
                throw new ArgumentNullException(nameof(version));
            if (string.IsNullOrEmpty(executablePath))
                throw new ArgumentNullException(nameof(executablePath));

            Version = version;
            ExecutablePath = executablePath;
            InstallPath = installPath ?? System.IO.Path.GetDirectoryName(executablePath);
            Status = DetectionStatus.Detected;
            LastDetectionTime = DateTime.UtcNow;
            ErrorMessage = null;
            Exception = null;

            // 尝试解析版本
            ParsedVersion = TryParseVersion(version);

            // 评估兼容性
            EvaluateCompatibility();

            // 检测支持的功能
            DetectSupportedFeatures();
        }

        /// <summary>
        /// 设置检测失败状态
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
            LastDetectionTime = DateTime.UtcNow;
        }

        /// <summary>
        /// 设置版本不兼容状态
        /// </summary>
        /// <param name="version">检测到的版本</param>
        /// <param name="reason">不兼容原因</param>
        public void SetIncompatible(string version, string reason)
        {
            Version = version;
            Status = DetectionStatus.IncompatibleVersion;
            ErrorMessage = reason;
            LastDetectionTime = DateTime.UtcNow;
            Compatibility = CompatibilityLevel.Incompatible;
            ParsedVersion = TryParseVersion(version);
        }

        /// <summary>
        /// 尝试解析版本号
        /// </summary>
        /// <param name="versionString">版本字符串</param>
        /// <returns>解析后的版本对象</returns>
        private Version? TryParseVersion(string versionString)
        {
            if (string.IsNullOrEmpty(versionString))
                return null;

            // 清理版本字符串，提取数字部分
            var cleanVersion = System.Text.RegularExpressions.Regex
                .Match(versionString, @"(\d+)\.(\d+)(?:\.(\d+))?(?:\.(\d+))?")?.Value;

            if (string.IsNullOrEmpty(cleanVersion))
                return null;

            return System.Version.TryParse(cleanVersion, out var version) ? version : null;
        }

        /// <summary>
        /// 评估兼容性等级
        /// </summary>
        private void EvaluateCompatibility()
        {
            if (ParsedVersion == null)
            {
                Compatibility = CompatibilityLevel.Unknown;
                return;
            }

            var minVersion = new Version(1, 0, 0);
            var recommendedVersion = new Version(1, 2, 0);

            if (ParsedVersion < minVersion)
            {
                Compatibility = CompatibilityLevel.Incompatible;
            }
            else if (ParsedVersion >= recommendedVersion)
            {
                Compatibility = CompatibilityLevel.FullyCompatible;
                IsRecommendedVersion = true;
            }
            else
            {
                Compatibility = CompatibilityLevel.BasicCompatible;
            }
        }

        /// <summary>
        /// 检测支持的功能
        /// </summary>
        private void DetectSupportedFeatures()
        {
            SupportedFeatures.Clear();

            if (ParsedVersion == null)
                return;

            // 基础功能
            SupportedFeatures.Add(ClaudeCodeFeature.BasicChat);
            SupportedFeatures.Add(ClaudeCodeFeature.FileOperations);

            // 版本特定功能
            if (ParsedVersion >= new Version(1, 1, 0))
            {
                SupportedFeatures.Add(ClaudeCodeFeature.ProjectManagement);
                SupportedFeatures.Add(ClaudeCodeFeature.GitIntegration);
            }

            if (ParsedVersion >= new Version(1, 2, 0))
            {
                SupportedFeatures.Add(ClaudeCodeFeature.AdvancedCodeAnalysis);
                SupportedFeatures.Add(ClaudeCodeFeature.MultiFileEditing);
            }

            if (ParsedVersion >= new Version(1, 3, 0))
            {
                SupportedFeatures.Add(ClaudeCodeFeature.RealTimeCollaboration);
                SupportedFeatures.Add(ClaudeCodeFeature.ExtensionSupport);
            }
        }

        /// <summary>
        /// 获取兼容性描述
        /// </summary>
        /// <returns>兼容性描述</returns>
        public string GetCompatibilityDescription()
        {
            return Compatibility switch
            {
                CompatibilityLevel.FullyCompatible => "完全兼容 - 支持所有功能",
                CompatibilityLevel.BasicCompatible => "基本兼容 - 支持核心功能",
                CompatibilityLevel.Incompatible => $"版本过低 - 需要升级到 1.0.0 或更高版本",
                CompatibilityLevel.Unknown => "未知兼容性",
                _ => "未知状态"
            };
        }

        /// <summary>
        /// 获取功能支持摘要
        /// </summary>
        /// <returns>功能支持摘要</returns>
        public string GetFeatureSummary()
        {
            if (!SupportedFeatures.Any())
                return "无支持的功能";

            var featureNames = SupportedFeatures.Select(f => GetFeatureName(f));
            return string.Join(", ", featureNames);
        }

        /// <summary>
        /// 获取功能名称
        /// </summary>
        /// <param name="feature">功能枚举</param>
        /// <returns>功能名称</returns>
        private string GetFeatureName(ClaudeCodeFeature feature)
        {
            return feature switch
            {
                ClaudeCodeFeature.BasicChat => "基础对话",
                ClaudeCodeFeature.FileOperations => "文件操作",
                ClaudeCodeFeature.ProjectManagement => "项目管理",
                ClaudeCodeFeature.GitIntegration => "Git集成",
                ClaudeCodeFeature.AdvancedCodeAnalysis => "高级代码分析",
                ClaudeCodeFeature.MultiFileEditing => "多文件编辑",
                ClaudeCodeFeature.RealTimeCollaboration => "实时协作",
                ClaudeCodeFeature.ExtensionSupport => "扩展支持",
                _ => feature.ToString()
            };
        }

        /// <summary>
        /// 返回 Claude Code 信息的字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString()
        {
            if (Status == DetectionStatus.NotDetected)
                return "Claude Code CLI 未检测到";

            if (Status == DetectionStatus.Failed)
                return $"Claude Code CLI 检测失败: {ErrorMessage}";

            return $"Claude Code CLI {Version} ({GetCompatibilityDescription()})";
        }
    }

    /// <summary>
    /// 兼容性等级枚举
    /// </summary>
    public enum CompatibilityLevel
    {
        /// <summary>
        /// 未知兼容性
        /// </summary>
        Unknown,

        /// <summary>
        /// 不兼容
        /// </summary>
        Incompatible,

        /// <summary>
        /// 基本兼容
        /// </summary>
        BasicCompatible,

        /// <summary>
        /// 完全兼容
        /// </summary>
        FullyCompatible
    }

    /// <summary>
    /// Claude Code 功能枚举
    /// </summary>
    public enum ClaudeCodeFeature
    {
        /// <summary>
        /// 基础对话功能
        /// </summary>
        BasicChat,

        /// <summary>
        /// 文件操作功能
        /// </summary>
        FileOperations,

        /// <summary>
        /// 项目管理功能
        /// </summary>
        ProjectManagement,

        /// <summary>
        /// Git 集成功能
        /// </summary>
        GitIntegration,

        /// <summary>
        /// 高级代码分析功能
        /// </summary>
        AdvancedCodeAnalysis,

        /// <summary>
        /// 多文件编辑功能
        /// </summary>
        MultiFileEditing,

        /// <summary>
        /// 实时协作功能
        /// </summary>
        RealTimeCollaboration,

        /// <summary>
        /// 扩展支持功能
        /// </summary>
        ExtensionSupport
    }

    /// <summary>
    /// 认证状态枚举
    /// </summary>
    public enum AuthenticationStatus
    {
        /// <summary>
        /// 未知状态
        /// </summary>
        Unknown,

        /// <summary>
        /// 未认证
        /// </summary>
        NotAuthenticated,

        /// <summary>
        /// 已认证
        /// </summary>
        Authenticated,

        /// <summary>
        /// 认证过期
        /// </summary>
        Expired,

        /// <summary>
        /// 认证错误
        /// </summary>
        Error
    }

    /// <summary>
    /// 性能指标
    /// </summary>
    public class PerformanceMetrics
    {
        /// <summary>
        /// 启动时间（毫秒）
        /// </summary>
        public long StartupTimeMs { get; set; }

        /// <summary>
        /// 响应时间（毫秒）
        /// </summary>
        public long ResponseTimeMs { get; set; }

        /// <summary>
        /// 内存使用量（MB）
        /// </summary>
        public double MemoryUsageMB { get; set; }

        /// <summary>
        /// CPU 使用率（百分比）
        /// </summary>
        public double CpuUsagePercent { get; set; }

        /// <summary>
        /// 最后测量时间
        /// </summary>
        public DateTime LastMeasuredTime { get; set; }

        /// <summary>
        /// 初始化性能指标
        /// </summary>
        public PerformanceMetrics()
        {
            LastMeasuredTime = DateTime.UtcNow;
        }

        /// <summary>
        /// 返回性能指标的字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString()
        {
            return $"启动: {StartupTimeMs}ms, 响应: {ResponseTimeMs}ms, 内存: {MemoryUsageMB:F1}MB, CPU: {CpuUsagePercent:F1}%";
        }
    }
}