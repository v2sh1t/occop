using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Occop.Core.Services.Environment;

namespace Occop.Core.Models.Environment
{
    /// <summary>
    /// 环境检测报告
    /// </summary>
    public class EnvironmentReport
    {
        /// <summary>
        /// 报告生成时间
        /// </summary>
        public DateTime GeneratedAt { get; }

        /// <summary>
        /// 报告版本
        /// </summary>
        public string Version { get; }

        /// <summary>
        /// 系统信息
        /// </summary>
        public SystemInfo System { get; set; }

        /// <summary>
        /// 检测结果
        /// </summary>
        public DetectionResult DetectionResult { get; set; }

        /// <summary>
        /// Claude Code 详细信息
        /// </summary>
        public ClaudeCodeInfo? ClaudeCode { get; set; }

        /// <summary>
        /// 环境摘要
        /// </summary>
        public EnvironmentSummary Summary { get; set; }

        /// <summary>
        /// 推荐建议
        /// </summary>
        public List<Recommendation> Recommendations { get; set; }

        /// <summary>
        /// 潜在问题
        /// </summary>
        public List<Issue> Issues { get; set; }

        /// <summary>
        /// 性能评估
        /// </summary>
        public PerformanceAssessment Performance { get; set; }

        /// <summary>
        /// 安全评估
        /// </summary>
        public SecurityAssessment Security { get; set; }

        /// <summary>
        /// 配置建议
        /// </summary>
        public ConfigurationSuggestions Configuration { get; set; }

        /// <summary>
        /// 检测历史
        /// </summary>
        public List<DetectionHistoryEntry> History { get; set; }

        /// <summary>
        /// 报告元数据
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; }

        /// <summary>
        /// 初始化环境报告
        /// </summary>
        /// <param name="detectionResult">检测结果</param>
        public EnvironmentReport(DetectionResult detectionResult)
        {
            GeneratedAt = DateTime.UtcNow;
            Version = "1.0.0";
            DetectionResult = detectionResult ?? throw new ArgumentNullException(nameof(detectionResult));

            System = new SystemInfo();
            Summary = new EnvironmentSummary();
            Recommendations = new List<Recommendation>();
            Issues = new List<Issue>();
            Performance = new PerformanceAssessment();
            Security = new SecurityAssessment();
            Configuration = new ConfigurationSuggestions();
            History = new List<DetectionHistoryEntry>();
            Metadata = new Dictionary<string, object>();

            // 生成报告内容
            GenerateReport();
        }

        /// <summary>
        /// 生成报告内容
        /// </summary>
        private void GenerateReport()
        {
            AnalyzeDetectionResults();
            GenerateSummary();
            GenerateRecommendations();
            IdentifyIssues();
            AssessPerformance();
            AssessSecurity();
            GenerateConfigurationSuggestions();
            AddMetadata();
        }

        /// <summary>
        /// 分析检测结果
        /// </summary>
        private void AnalyzeDetectionResults()
        {
            // 提取Claude Code信息
            if (DetectionResult.DetectedEnvironments.TryGetValue(EnvironmentType.ClaudeCode, out var claudeCodeEnv))
            {
                ClaudeCode = ConvertToClaudeCodeInfo(claudeCodeEnv);
            }

            // 添加检测历史条目
            History.Add(new DetectionHistoryEntry
            {
                Timestamp = DetectionResult.StartTime,
                TotalDetected = DetectionResult.DetectedCount,
                Duration = DetectionResult.TotalDurationMs,
                HasClaudeCode = DetectionResult.HasClaudeCode,
                HasShellEnvironment = DetectionResult.HasShellEnvironment
            });
        }

        /// <summary>
        /// 将EnvironmentInfo转换为ClaudeCodeInfo
        /// </summary>
        /// <param name="environmentInfo">环境信息</param>
        /// <returns>Claude Code信息</returns>
        private ClaudeCodeInfo ConvertToClaudeCodeInfo(EnvironmentInfo environmentInfo)
        {
            var claudeCodeInfo = new ClaudeCodeInfo();

            if (environmentInfo.Status == DetectionStatus.Detected)
            {
                claudeCodeInfo.SetDetected(
                    environmentInfo.Version ?? "未知版本",
                    environmentInfo.ExecutablePath ?? "",
                    environmentInfo.InstallPath);
            }
            else if (environmentInfo.Status == DetectionStatus.Failed)
            {
                claudeCodeInfo.SetFailed(
                    environmentInfo.ErrorMessage ?? "检测失败",
                    environmentInfo.Exception);
            }
            else if (environmentInfo.Status == DetectionStatus.IncompatibleVersion)
            {
                claudeCodeInfo.SetIncompatible(
                    environmentInfo.Version ?? "未知版本",
                    $"版本不兼容，需要 {environmentInfo.MinimumCompatibleVersion}+");
            }

            return claudeCodeInfo;
        }

        /// <summary>
        /// 生成环境摘要
        /// </summary>
        private void GenerateSummary()
        {
            Summary.TotalEnvironments = DetectionResult.DetectedEnvironments.Count;
            Summary.DetectedEnvironments = DetectionResult.DetectedCount;
            Summary.FailedDetections = DetectionResult.Errors.Count;
            Summary.HasRequiredEnvironments = DetectionResult.HasShellEnvironment && DetectionResult.HasClaudeCode;
            Summary.RecommendedShell = DetectionResult.RecommendedShell?.Type.ToString() ?? "无";
            Summary.OverallStatus = DetermineOverallStatus();
            Summary.DetectionDuration = DetectionResult.TotalDurationMs;
        }

        /// <summary>
        /// 确定总体状态
        /// </summary>
        /// <returns>总体状态</returns>
        private EnvironmentStatus DetermineOverallStatus()
        {
            if (!DetectionResult.HasShellEnvironment && !DetectionResult.HasClaudeCode)
                return EnvironmentStatus.Critical;

            if (!DetectionResult.HasShellEnvironment || !DetectionResult.HasClaudeCode)
                return EnvironmentStatus.Warning;

            if (DetectionResult.Errors.Any())
                return EnvironmentStatus.Warning;

            return EnvironmentStatus.Healthy;
        }

        /// <summary>
        /// 生成推荐建议
        /// </summary>
        private void GenerateRecommendations()
        {
            // Shell环境建议
            if (!DetectionResult.HasShellEnvironment)
            {
                Recommendations.Add(new Recommendation
                {
                    Priority = RecommendationPriority.High,
                    Category = "Shell Environment",
                    Title = "安装Shell环境",
                    Description = "建议安装PowerShell Core 7+ 或 PowerShell 5.1+",
                    Action = "访问 https://github.com/PowerShell/PowerShell 下载最新版本",
                    Impact = "没有Shell环境将无法执行命令行操作"
                });
            }

            // Claude Code建议
            if (!DetectionResult.HasClaudeCode)
            {
                Recommendations.Add(new Recommendation
                {
                    Priority = RecommendationPriority.High,
                    Category = "Claude Code",
                    Title = "安装Claude Code CLI",
                    Description = "建议安装最新版本的Claude Code CLI",
                    Action = "运行 npm install -g @anthropic-ai/claude-code 或从官网下载",
                    Impact = "没有Claude Code CLI将无法使用AI编程助手功能"
                });
            }
            else if (ClaudeCode?.Compatibility == CompatibilityLevel.Incompatible)
            {
                Recommendations.Add(new Recommendation
                {
                    Priority = RecommendationPriority.High,
                    Category = "Claude Code",
                    Title = "升级Claude Code CLI",
                    Description = $"当前版本 {ClaudeCode.Version} 不兼容，需要升级",
                    Action = "运行 npm update -g @anthropic-ai/claude-code",
                    Impact = "版本过低可能导致功能缺失或错误"
                });
            }

            // 性能优化建议
            if (DetectionResult.TotalDurationMs > 5000)
            {
                Recommendations.Add(new Recommendation
                {
                    Priority = RecommendationPriority.Medium,
                    Category = "Performance",
                    Title = "优化检测性能",
                    Description = "环境检测耗时较长，建议优化系统配置",
                    Action = "清理PATH环境变量，删除无效路径",
                    Impact = "改善系统响应速度"
                });
            }
        }

        /// <summary>
        /// 识别潜在问题
        /// </summary>
        private void IdentifyIssues()
        {
            // 检测错误
            foreach (var error in DetectionResult.Errors)
            {
                Issues.Add(new Issue
                {
                    Severity = IssueSeverity.Error,
                    Category = error.EnvironmentType.ToString(),
                    Title = $"{error.EnvironmentType} 检测失败",
                    Description = error.ErrorMessage,
                    Impact = "可能导致相关功能无法使用",
                    Resolution = "检查安装状态和配置，确保环境正确安装"
                });
            }

            // 版本兼容性问题
            if (ClaudeCode?.Status == DetectionStatus.IncompatibleVersion)
            {
                Issues.Add(new Issue
                {
                    Severity = IssueSeverity.Warning,
                    Category = "Version Compatibility",
                    Title = "Claude Code版本不兼容",
                    Description = $"当前版本 {ClaudeCode.Version} 可能存在兼容性问题",
                    Impact = "部分功能可能无法正常使用",
                    Resolution = "升级到推荐版本或更高版本"
                });
            }

            // 性能问题
            if (DetectionResult.TotalDurationMs > 10000)
            {
                Issues.Add(new Issue
                {
                    Severity = IssueSeverity.Warning,
                    Category = "Performance",
                    Title = "检测性能较慢",
                    Description = $"环境检测耗时 {DetectionResult.TotalDurationMs}ms，超过预期",
                    Impact = "系统响应速度可能受影响",
                    Resolution = "优化系统配置，清理无效的环境变量"
                });
            }
        }

        /// <summary>
        /// 评估性能
        /// </summary>
        private void AssessPerformance()
        {
            Performance.DetectionSpeed = DetermineDetectionSpeed();
            Performance.SystemLoad = "正常"; // 简化实现
            Performance.ResourceUsage = "低";
            Performance.Bottlenecks = new List<string>();

            if (DetectionResult.TotalDurationMs > 5000)
            {
                Performance.Bottlenecks.Add("PATH环境变量过多");
            }

            if (DetectionResult.DetectedCount < 2)
            {
                Performance.Bottlenecks.Add("可用环境较少");
            }
        }

        /// <summary>
        /// 确定检测速度等级
        /// </summary>
        /// <returns>检测速度</returns>
        private string DetermineDetectionSpeed()
        {
            return DetectionResult.TotalDurationMs switch
            {
                < 1000 => "快速",
                < 3000 => "正常",
                < 5000 => "较慢",
                _ => "缓慢"
            };
        }

        /// <summary>
        /// 评估安全性
        /// </summary>
        private void AssessSecurity()
        {
            Security.TrustLevel = "中等"; // 简化实现
            Security.SecurityRisks = new List<string>();
            Security.Recommendations = new List<string>();

            // 检查可执行文件路径安全性
            foreach (var env in DetectionResult.DetectedEnvironments.Values)
            {
                if (env.Status == DetectionStatus.Detected && !string.IsNullOrEmpty(env.ExecutablePath))
                {
                    if (env.ExecutablePath.Contains(" ") && !env.ExecutablePath.StartsWith("\""))
                    {
                        Security.SecurityRisks.Add($"{env.Type} 可执行文件路径包含空格，可能存在安全风险");
                    }
                }
            }

            if (!Security.SecurityRisks.Any())
            {
                Security.TrustLevel = "高";
            }
        }

        /// <summary>
        /// 生成配置建议
        /// </summary>
        private void GenerateConfigurationSuggestions()
        {
            Configuration.EnvironmentVariables = new Dictionary<string, string>();
            Configuration.PathOptimizations = new List<string>();
            Configuration.SecuritySettings = new List<string>();

            // PATH优化建议
            if (DetectionResult.TotalDurationMs > 3000)
            {
                Configuration.PathOptimizations.Add("清理无效的PATH条目");
                Configuration.PathOptimizations.Add("将常用路径移到PATH开头");
            }

            // 环境变量建议
            if (ClaudeCode?.Status == DetectionStatus.Detected)
            {
                Configuration.EnvironmentVariables["CLAUDE_CODE_PATH"] = ClaudeCode.ExecutablePath ?? "";
            }
        }

        /// <summary>
        /// 添加元数据
        /// </summary>
        private void AddMetadata()
        {
            Metadata["hostname"] = Environment.MachineName;
            Metadata["username"] = Environment.UserName;
            Metadata["os_version"] = Environment.OSVersion.ToString();
            Metadata["processor_count"] = Environment.ProcessorCount;
            Metadata["working_directory"] = Environment.CurrentDirectory;
            Metadata["report_generator"] = "OCCOP Environment Reporter";
        }

        /// <summary>
        /// 生成HTML格式报告
        /// </summary>
        /// <returns>HTML报告</returns>
        public string GenerateHtmlReport()
        {
            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html><head><title>环境检测报告</title>");
            html.AppendLine("<style>");
            html.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; }");
            html.AppendLine(".header { background: #f0f0f0; padding: 20px; border-radius: 5px; }");
            html.AppendLine(".section { margin: 20px 0; padding: 15px; border: 1px solid #ddd; border-radius: 5px; }");
            html.AppendLine(".success { color: green; } .warning { color: orange; } .error { color: red; }");
            html.AppendLine("</style></head><body>");

            // 报告头部
            html.AppendLine($"<div class='header'>");
            html.AppendLine($"<h1>环境检测报告</h1>");
            html.AppendLine($"<p>生成时间: {GeneratedAt:yyyy-MM-dd HH:mm:ss}</p>");
            html.AppendLine($"<p>报告版本: {Version}</p>");
            html.AppendLine($"</div>");

            // 摘要部分
            html.AppendLine($"<div class='section'>");
            html.AppendLine($"<h2>环境摘要</h2>");
            html.AppendLine($"<p>总体状态: <span class='{Summary.OverallStatus.ToString().ToLower()}'>{Summary.OverallStatus}</span></p>");
            html.AppendLine($"<p>检测到的环境: {Summary.DetectedEnvironments}/{Summary.TotalEnvironments}</p>");
            html.AppendLine($"<p>推荐Shell: {Summary.RecommendedShell}</p>");
            html.AppendLine($"<p>检测耗时: {Summary.DetectionDuration}ms</p>");
            html.AppendLine($"</div>");

            // Claude Code部分
            if (ClaudeCode != null)
            {
                html.AppendLine($"<div class='section'>");
                html.AppendLine($"<h2>Claude Code CLI</h2>");
                html.AppendLine($"<p>状态: {ClaudeCode.Status}</p>");
                html.AppendLine($"<p>版本: {ClaudeCode.Version}</p>");
                html.AppendLine($"<p>兼容性: {ClaudeCode.GetCompatibilityDescription()}</p>");
                html.AppendLine($"<p>支持功能: {ClaudeCode.GetFeatureSummary()}</p>");
                html.AppendLine($"</div>");
            }

            // 建议部分
            if (Recommendations.Any())
            {
                html.AppendLine($"<div class='section'>");
                html.AppendLine($"<h2>推荐建议</h2>");
                html.AppendLine($"<ul>");
                foreach (var rec in Recommendations)
                {
                    html.AppendLine($"<li><strong>{rec.Title}</strong>: {rec.Description}</li>");
                }
                html.AppendLine($"</ul>");
                html.AppendLine($"</div>");
            }

            html.AppendLine("</body></html>");
            return html.ToString();
        }

        /// <summary>
        /// 生成文本格式报告
        /// </summary>
        /// <returns>文本报告</returns>
        public string GenerateTextReport()
        {
            var report = new StringBuilder();

            report.AppendLine("=== 环境检测报告 ===");
            report.AppendLine($"生成时间: {GeneratedAt:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"报告版本: {Version}");
            report.AppendLine();

            report.AppendLine("=== 环境摘要 ===");
            report.AppendLine($"总体状态: {Summary.OverallStatus}");
            report.AppendLine($"检测到的环境: {Summary.DetectedEnvironments}/{Summary.TotalEnvironments}");
            report.AppendLine($"推荐Shell: {Summary.RecommendedShell}");
            report.AppendLine($"检测耗时: {Summary.DetectionDuration}ms");
            report.AppendLine();

            if (ClaudeCode != null)
            {
                report.AppendLine("=== Claude Code CLI ===");
                report.AppendLine($"状态: {ClaudeCode.Status}");
                report.AppendLine($"版本: {ClaudeCode.Version}");
                report.AppendLine($"兼容性: {ClaudeCode.GetCompatibilityDescription()}");
                report.AppendLine($"支持功能: {ClaudeCode.GetFeatureSummary()}");
                report.AppendLine();
            }

            if (Recommendations.Any())
            {
                report.AppendLine("=== 推荐建议 ===");
                foreach (var rec in Recommendations)
                {
                    report.AppendLine($"[{rec.Priority}] {rec.Title}");
                    report.AppendLine($"  描述: {rec.Description}");
                    report.AppendLine($"  操作: {rec.Action}");
                    report.AppendLine();
                }
            }

            if (Issues.Any())
            {
                report.AppendLine("=== 发现的问题 ===");
                foreach (var issue in Issues)
                {
                    report.AppendLine($"[{issue.Severity}] {issue.Title}");
                    report.AppendLine($"  描述: {issue.Description}");
                    report.AppendLine($"  解决方案: {issue.Resolution}");
                    report.AppendLine();
                }
            }

            return report.ToString();
        }

        /// <summary>
        /// 返回报告的字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString()
        {
            return $"环境检测报告 - {Summary.OverallStatus} ({Summary.DetectedEnvironments}/{Summary.TotalEnvironments} 环境)";
        }
    }

    /// <summary>
    /// 系统信息
    /// </summary>
    public class SystemInfo
    {
        /// <summary>
        /// 操作系统
        /// </summary>
        public string OperatingSystem { get; set; } = Environment.OSVersion.ToString();

        /// <summary>
        /// 机器名
        /// </summary>
        public string MachineName { get; set; } = Environment.MachineName;

        /// <summary>
        /// 用户名
        /// </summary>
        public string UserName { get; set; } = Environment.UserName;

        /// <summary>
        /// 处理器数量
        /// </summary>
        public int ProcessorCount { get; set; } = Environment.ProcessorCount;

        /// <summary>
        /// 工作目录
        /// </summary>
        public string WorkingDirectory { get; set; } = Environment.CurrentDirectory;

        /// <summary>
        /// 系统启动时间
        /// </summary>
        public DateTime SystemStartTime { get; set; } = DateTime.Now.AddMilliseconds(-Environment.TickCount);
    }

    /// <summary>
    /// 环境摘要
    /// </summary>
    public class EnvironmentSummary
    {
        /// <summary>
        /// 总环境数
        /// </summary>
        public int TotalEnvironments { get; set; }

        /// <summary>
        /// 已检测到的环境数
        /// </summary>
        public int DetectedEnvironments { get; set; }

        /// <summary>
        /// 检测失败数
        /// </summary>
        public int FailedDetections { get; set; }

        /// <summary>
        /// 是否具备必需环境
        /// </summary>
        public bool HasRequiredEnvironments { get; set; }

        /// <summary>
        /// 推荐Shell
        /// </summary>
        public string RecommendedShell { get; set; } = "";

        /// <summary>
        /// 总体状态
        /// </summary>
        public EnvironmentStatus OverallStatus { get; set; }

        /// <summary>
        /// 检测耗时
        /// </summary>
        public long DetectionDuration { get; set; }
    }

    /// <summary>
    /// 推荐建议
    /// </summary>
    public class Recommendation
    {
        /// <summary>
        /// 优先级
        /// </summary>
        public RecommendationPriority Priority { get; set; }

        /// <summary>
        /// 分类
        /// </summary>
        public string Category { get; set; } = "";

        /// <summary>
        /// 标题
        /// </summary>
        public string Title { get; set; } = "";

        /// <summary>
        /// 描述
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// 建议操作
        /// </summary>
        public string Action { get; set; } = "";

        /// <summary>
        /// 影响说明
        /// </summary>
        public string Impact { get; set; } = "";
    }

    /// <summary>
    /// 问题信息
    /// </summary>
    public class Issue
    {
        /// <summary>
        /// 严重程度
        /// </summary>
        public IssueSeverity Severity { get; set; }

        /// <summary>
        /// 分类
        /// </summary>
        public string Category { get; set; } = "";

        /// <summary>
        /// 标题
        /// </summary>
        public string Title { get; set; } = "";

        /// <summary>
        /// 描述
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// 影响
        /// </summary>
        public string Impact { get; set; } = "";

        /// <summary>
        /// 解决方案
        /// </summary>
        public string Resolution { get; set; } = "";
    }

    /// <summary>
    /// 性能评估
    /// </summary>
    public class PerformanceAssessment
    {
        /// <summary>
        /// 检测速度
        /// </summary>
        public string DetectionSpeed { get; set; } = "";

        /// <summary>
        /// 系统负载
        /// </summary>
        public string SystemLoad { get; set; } = "";

        /// <summary>
        /// 资源使用情况
        /// </summary>
        public string ResourceUsage { get; set; } = "";

        /// <summary>
        /// 性能瓶颈
        /// </summary>
        public List<string> Bottlenecks { get; set; } = new List<string>();
    }

    /// <summary>
    /// 安全评估
    /// </summary>
    public class SecurityAssessment
    {
        /// <summary>
        /// 信任级别
        /// </summary>
        public string TrustLevel { get; set; } = "";

        /// <summary>
        /// 安全风险
        /// </summary>
        public List<string> SecurityRisks { get; set; } = new List<string>();

        /// <summary>
        /// 安全建议
        /// </summary>
        public List<string> Recommendations { get; set; } = new List<string>();
    }

    /// <summary>
    /// 配置建议
    /// </summary>
    public class ConfigurationSuggestions
    {
        /// <summary>
        /// 环境变量建议
        /// </summary>
        public Dictionary<string, string> EnvironmentVariables { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// PATH优化建议
        /// </summary>
        public List<string> PathOptimizations { get; set; } = new List<string>();

        /// <summary>
        /// 安全设置建议
        /// </summary>
        public List<string> SecuritySettings { get; set; } = new List<string>();
    }

    /// <summary>
    /// 检测历史条目
    /// </summary>
    public class DetectionHistoryEntry
    {
        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 检测到的总数
        /// </summary>
        public int TotalDetected { get; set; }

        /// <summary>
        /// 检测耗时
        /// </summary>
        public long Duration { get; set; }

        /// <summary>
        /// 是否有Claude Code
        /// </summary>
        public bool HasClaudeCode { get; set; }

        /// <summary>
        /// 是否有Shell环境
        /// </summary>
        public bool HasShellEnvironment { get; set; }
    }

    /// <summary>
    /// 环境状态枚举
    /// </summary>
    public enum EnvironmentStatus
    {
        /// <summary>
        /// 健康
        /// </summary>
        Healthy,

        /// <summary>
        /// 警告
        /// </summary>
        Warning,

        /// <summary>
        /// 严重
        /// </summary>
        Critical
    }

    /// <summary>
    /// 推荐优先级枚举
    /// </summary>
    public enum RecommendationPriority
    {
        /// <summary>
        /// 低优先级
        /// </summary>
        Low,

        /// <summary>
        /// 中等优先级
        /// </summary>
        Medium,

        /// <summary>
        /// 高优先级
        /// </summary>
        High,

        /// <summary>
        /// 关键优先级
        /// </summary>
        Critical
    }

    /// <summary>
    /// 问题严重程度枚举
    /// </summary>
    public enum IssueSeverity
    {
        /// <summary>
        /// 信息
        /// </summary>
        Info,

        /// <summary>
        /// 警告
        /// </summary>
        Warning,

        /// <summary>
        /// 错误
        /// </summary>
        Error,

        /// <summary>
        /// 关键错误
        /// </summary>
        Critical
    }
}