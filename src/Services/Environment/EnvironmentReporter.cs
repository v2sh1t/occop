using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Occop.Core.Models.Environment;
using Occop.Core.Services.Environment;

namespace Occop.Core.Services.Environment
{
    /// <summary>
    /// 环境检测报告生成器
    /// </summary>
    public class EnvironmentReporter
    {
        private readonly ClaudeCodeDetector _claudeCodeDetector;
        private readonly List<EnvironmentReport> _reportHistory;

        /// <summary>
        /// 报告生成事件
        /// </summary>
        public event EventHandler<ReportGeneratedEventArgs>? ReportGenerated;

        /// <summary>
        /// 初始化环境报告生成器
        /// </summary>
        /// <param name="claudeCodeDetector">Claude Code检测器</param>
        public EnvironmentReporter(ClaudeCodeDetector? claudeCodeDetector = null)
        {
            _claudeCodeDetector = claudeCodeDetector ?? new ClaudeCodeDetector();
            _reportHistory = new List<EnvironmentReport>();
        }

        /// <summary>
        /// 生成完整的环境检测报告
        /// </summary>
        /// <param name="detectionResult">检测结果</param>
        /// <param name="includeDetailedAnalysis">是否包含详细分析</param>
        /// <returns>环境报告</returns>
        public async Task<EnvironmentReport> GenerateReportAsync(DetectionResult detectionResult, bool includeDetailedAnalysis = true)
        {
            if (detectionResult == null)
                throw new ArgumentNullException(nameof(detectionResult));

            try
            {
                // 创建环境报告
                var report = new EnvironmentReport(detectionResult);

                // 如果包含详细分析，增强Claude Code信息
                if (includeDetailedAnalysis && report.ClaudeCode != null)
                {
                    await EnhanceClaudeCodeAnalysisAsync(report);
                }

                // 生成额外的环境分析
                await PerformEnvironmentAnalysisAsync(report);

                // 添加到历史记录
                _reportHistory.Add(report);

                // 触发报告生成事件
                OnReportGenerated(new ReportGeneratedEventArgs(report));

                return report;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"生成环境报告时发生错误: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 生成Claude Code专项报告
        /// </summary>
        /// <param name="forceRefresh">是否强制刷新检测</param>
        /// <returns>Claude Code专项报告</returns>
        public async Task<ClaudeCodeReport> GenerateClaudeCodeReportAsync(bool forceRefresh = false)
        {
            try
            {
                var claudeCodeInfo = await _claudeCodeDetector.DetectClaudeCodeAsync(forceRefresh);

                var report = new ClaudeCodeReport
                {
                    GeneratedAt = DateTime.UtcNow,
                    ClaudeCodeInfo = claudeCodeInfo,
                    InstallationAnalysis = await AnalyzeInstallationAsync(claudeCodeInfo),
                    UsageRecommendations = GenerateUsageRecommendations(claudeCodeInfo),
                    TroubleshootingSteps = GenerateTroubleshootingSteps(claudeCodeInfo),
                    SecurityAssessment = await AssessClaudeCodeSecurityAsync(claudeCodeInfo),
                    PerformanceAnalysis = AnalyzePerformance(claudeCodeInfo),
                    UpdateAvailable = await CheckForUpdatesAsync(claudeCodeInfo)
                };

                return report;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"生成Claude Code专项报告时发生错误: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 生成环境比较报告
        /// </summary>
        /// <param name="previousReport">之前的报告</param>
        /// <param name="currentReport">当前的报告</param>
        /// <returns>比较报告</returns>
        public EnvironmentComparisonReport GenerateComparisonReport(EnvironmentReport previousReport, EnvironmentReport currentReport)
        {
            if (previousReport == null)
                throw new ArgumentNullException(nameof(previousReport));
            if (currentReport == null)
                throw new ArgumentNullException(nameof(currentReport));

            try
            {
                var comparison = new EnvironmentComparisonReport
                {
                    GeneratedAt = DateTime.UtcNow,
                    PreviousReport = previousReport,
                    CurrentReport = currentReport,
                    Changes = IdentifyChanges(previousReport, currentReport),
                    PerformanceChanges = ComparePerformance(previousReport, currentReport),
                    NewIssues = IdentifyNewIssues(previousReport, currentReport),
                    ResolvedIssues = IdentifyResolvedIssues(previousReport, currentReport),
                    Summary = GenerateChangeSummary(previousReport, currentReport)
                };

                return comparison;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"生成环境比较报告时发生错误: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 导出报告到文件
        /// </summary>
        /// <param name="report">环境报告</param>
        /// <param name="outputPath">输出路径</param>
        /// <param name="format">导出格式</param>
        /// <returns>导出的文件路径</returns>
        public async Task<string> ExportReportAsync(EnvironmentReport report, string outputPath, ReportFormat format = ReportFormat.Html)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));
            if (string.IsNullOrEmpty(outputPath))
                throw new ArgumentNullException(nameof(outputPath));

            try
            {
                var content = format switch
                {
                    ReportFormat.Html => report.GenerateHtmlReport(),
                    ReportFormat.Text => report.GenerateTextReport(),
                    ReportFormat.Json => SerializeToJson(report),
                    ReportFormat.Xml => SerializeToXml(report),
                    _ => throw new ArgumentException($"不支持的报告格式: {format}")
                };

                var fileName = GenerateFileName(report, format);
                var fullPath = Path.Combine(outputPath, fileName);

                // 确保目录存在
                Directory.CreateDirectory(outputPath);

                await File.WriteAllTextAsync(fullPath, content);

                return fullPath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"导出报告时发生错误: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 获取报告历史
        /// </summary>
        /// <param name="maxCount">最大数量</param>
        /// <returns>报告历史列表</returns>
        public List<EnvironmentReport> GetReportHistory(int maxCount = 10)
        {
            return _reportHistory
                .OrderByDescending(r => r.GeneratedAt)
                .Take(maxCount)
                .ToList();
        }

        /// <summary>
        /// 清除报告历史
        /// </summary>
        /// <param name="olderThan">清除指定时间之前的报告</param>
        public void ClearReportHistory(DateTime? olderThan = null)
        {
            if (olderThan.HasValue)
            {
                _reportHistory.RemoveAll(r => r.GeneratedAt < olderThan.Value);
            }
            else
            {
                _reportHistory.Clear();
            }
        }

        /// <summary>
        /// 增强Claude Code分析
        /// </summary>
        /// <param name="report">环境报告</param>
        private async Task EnhanceClaudeCodeAnalysisAsync(EnvironmentReport report)
        {
            if (report.ClaudeCode == null || string.IsNullOrEmpty(report.ClaudeCode.ExecutablePath))
                return;

            try
            {
                // 验证可用性
                var isAvailable = await _claudeCodeDetector.IsClaudeCodeAvailableAsync(report.ClaudeCode.ExecutablePath);
                if (!isAvailable)
                {
                    report.Issues.Add(new Issue
                    {
                        Severity = IssueSeverity.Error,
                        Category = "Claude Code",
                        Title = "Claude Code CLI不可用",
                        Description = "检测到Claude Code CLI但无法正常执行",
                        Impact = "可能导致AI编程功能无法使用",
                        Resolution = "检查安装完整性，必要时重新安装"
                    });
                }

                // 验证版本兼容性
                if (!string.IsNullOrEmpty(report.ClaudeCode.Version))
                {
                    var isCompatible = _claudeCodeDetector.IsVersionCompatible(report.ClaudeCode.Version);
                    if (!isCompatible)
                    {
                        report.Recommendations.Add(new Recommendation
                        {
                            Priority = RecommendationPriority.High,
                            Category = "Claude Code",
                            Title = "升级Claude Code版本",
                            Description = $"当前版本 {report.ClaudeCode.Version} 过低",
                            Action = "运行更新命令或重新安装最新版本",
                            Impact = "旧版本可能缺少重要功能或存在安全问题"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                report.Issues.Add(new Issue
                {
                    Severity = IssueSeverity.Warning,
                    Category = "Claude Code",
                    Title = "Claude Code分析失败",
                    Description = $"无法完成Claude Code详细分析: {ex.Message}",
                    Impact = "可能影响报告准确性",
                    Resolution = "稍后重试或手动验证Claude Code状态"
                });
            }
        }

        /// <summary>
        /// 执行环境分析
        /// </summary>
        /// <param name="report">环境报告</param>
        private async Task PerformEnvironmentAnalysisAsync(EnvironmentReport report)
        {
            try
            {
                // 分析环境完整性
                AnalyzeEnvironmentCompleteness(report);

                // 分析性能影响
                AnalyzePerformanceImpact(report);

                // 分析安全风险
                AnalyzeSecurityRisks(report);

                // 生成优化建议
                GenerateOptimizationRecommendations(report);

                // 预测潜在问题
                await PredictPotentialIssuesAsync(report);
            }
            catch (Exception ex)
            {
                report.Issues.Add(new Issue
                {
                    Severity = IssueSeverity.Warning,
                    Category = "Environment Analysis",
                    Title = "环境分析失败",
                    Description = $"环境分析过程中发生错误: {ex.Message}",
                    Impact = "可能影响报告的完整性",
                    Resolution = "稍后重试分析"
                });
            }
        }

        /// <summary>
        /// 分析环境完整性
        /// </summary>
        /// <param name="report">环境报告</param>
        private void AnalyzeEnvironmentCompleteness(EnvironmentReport report)
        {
            var completenessScore = 0;
            var totalPossibleScore = 100;

            // Shell环境检查 (40分)
            if (report.DetectionResult.HasShellEnvironment)
            {
                completenessScore += 40;

                var shellCount = report.DetectionResult.GetAvailableShells().Count;
                if (shellCount > 1)
                {
                    completenessScore += 10; // 多个Shell环境额外加分
                }
            }

            // Claude Code检查 (40分)
            if (report.DetectionResult.HasClaudeCode)
            {
                completenessScore += 40;

                if (report.ClaudeCode?.Compatibility == CompatibilityLevel.FullyCompatible)
                {
                    completenessScore += 10; // 完全兼容额外加分
                }
            }

            // 无错误检查 (20分)
            if (!report.DetectionResult.Errors.Any())
            {
                completenessScore += 20;
            }
            else
            {
                completenessScore += Math.Max(0, 20 - report.DetectionResult.Errors.Count * 5);
            }

            var completenessPercentage = (double)completenessScore / totalPossibleScore * 100;

            report.Metadata["environment_completeness_score"] = completenessScore;
            report.Metadata["environment_completeness_percentage"] = completenessPercentage;

            if (completenessPercentage < 70)
            {
                report.Recommendations.Add(new Recommendation
                {
                    Priority = RecommendationPriority.Medium,
                    Category = "Environment Completeness",
                    Title = "改善环境完整性",
                    Description = $"环境完整性得分为 {completenessPercentage:F1}%，建议完善环境配置",
                    Action = "安装缺失的组件，解决检测错误",
                    Impact = "提高开发效率和系统稳定性"
                });
            }
        }

        /// <summary>
        /// 分析性能影响
        /// </summary>
        /// <param name="report">环境报告</param>
        private void AnalyzePerformanceImpact(EnvironmentReport report)
        {
            var detectionTime = report.DetectionResult.TotalDurationMs;

            if (detectionTime > 5000)
            {
                report.Performance.Bottlenecks.Add("环境检测耗时过长");

                report.Recommendations.Add(new Recommendation
                {
                    Priority = RecommendationPriority.Medium,
                    Category = "Performance",
                    Title = "优化环境检测性能",
                    Description = $"检测耗时 {detectionTime}ms，超过预期",
                    Action = "清理PATH环境变量，移除无效路径",
                    Impact = "减少系统启动和响应时间"
                });
            }

            // 分析Claude Code性能
            if (report.ClaudeCode?.Performance != null)
            {
                var perf = report.ClaudeCode.Performance;
                if (perf.StartupTimeMs > 3000)
                {
                    report.Issues.Add(new Issue
                    {
                        Severity = IssueSeverity.Warning,
                        Category = "Performance",
                        Title = "Claude Code启动较慢",
                        Description = $"启动时间 {perf.StartupTimeMs}ms 超过预期",
                        Impact = "可能影响用户体验",
                        Resolution = "检查系统资源使用情况，考虑升级硬件"
                    });
                }
            }
        }

        /// <summary>
        /// 分析安全风险
        /// </summary>
        /// <param name="report">环境报告</param>
        private void AnalyzeSecurityRisks(EnvironmentReport report)
        {
            // 检查PATH安全性
            var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
            var pathEntries = pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

            foreach (var path in pathEntries)
            {
                if (path.Contains(" ") && !path.StartsWith("\""))
                {
                    report.Security.SecurityRisks.Add($"PATH条目包含空格且未引号保护: {path}");
                }

                if (path.Equals(".", StringComparison.OrdinalIgnoreCase) ||
                    path.Equals("", StringComparison.OrdinalIgnoreCase))
                {
                    report.Security.SecurityRisks.Add("PATH包含当前目录，存在安全风险");
                }
            }

            // 检查Claude Code配置安全性
            if (report.ClaudeCode != null && report.ClaudeCode.EnvironmentVariables.ContainsKey("CLAUDE_API_KEY"))
            {
                report.Security.Recommendations.Add("确保API密钥安全存储，避免在环境变量中暴露");
            }
        }

        /// <summary>
        /// 生成优化建议
        /// </summary>
        /// <param name="report">环境报告</param>
        private void GenerateOptimizationRecommendations(EnvironmentReport report)
        {
            // Shell优化建议
            if (report.DetectionResult.HasShellEnvironment)
            {
                var shells = report.DetectionResult.GetAvailableShells();
                if (shells.Count > 1)
                {
                    var recommended = shells.FirstOrDefault(s => s.Type == EnvironmentType.PowerShellCore);
                    if (recommended != null)
                    {
                        report.Recommendations.Add(new Recommendation
                        {
                            Priority = RecommendationPriority.Low,
                            Category = "Shell Optimization",
                            Title = "使用PowerShell Core",
                            Description = "检测到多个Shell环境，建议优先使用PowerShell Core",
                            Action = "将PowerShell Core设置为默认Shell",
                            Impact = "获得更好的性能和兼容性"
                        });
                    }
                }
            }

            // Claude Code优化建议
            if (report.ClaudeCode?.Status == DetectionStatus.Detected)
            {
                if (report.ClaudeCode.AuthStatus == AuthenticationStatus.NotAuthenticated)
                {
                    report.Recommendations.Add(new Recommendation
                    {
                        Priority = RecommendationPriority.High,
                        Category = "Claude Code",
                        Title = "配置Claude Code认证",
                        Description = "Claude Code未进行身份认证",
                        Action = "运行 'claude auth login' 命令进行登录",
                        Impact = "启用完整的AI编程功能"
                    });
                }
            }
        }

        /// <summary>
        /// 预测潜在问题
        /// </summary>
        /// <param name="report">环境报告</param>
        private async Task PredictPotentialIssuesAsync(EnvironmentReport report)
        {
            await Task.Run(() =>
            {
                // 基于历史报告预测问题
                if (_reportHistory.Count > 1)
                {
                    var previousReport = _reportHistory[_reportHistory.Count - 2];

                    // 检查环境退化
                    if (previousReport.Summary.DetectedEnvironments > report.Summary.DetectedEnvironments)
                    {
                        report.Issues.Add(new Issue
                        {
                            Severity = IssueSeverity.Warning,
                            Category = "Environment Degradation",
                            Title = "环境退化检测",
                            Description = "检测到的环境数量减少，可能存在环境问题",
                            Impact = "可能导致功能缺失",
                            Resolution = "检查环境变更，重新安装缺失组件"
                        });
                    }

                    // 检查性能下降
                    if (previousReport.Summary.DetectionDuration < report.Summary.DetectionDuration * 0.5)
                    {
                        report.Issues.Add(new Issue
                        {
                            Severity = IssueSeverity.Info,
                            Category = "Performance Trend",
                            Title = "性能下降趋势",
                            Description = "检测时间显著增加，可能存在性能问题",
                            Impact = "系统响应速度可能受影响",
                            Resolution = "监控系统资源使用情况，优化环境配置"
                        });
                    }
                }
            });
        }

        // 辅助方法

        private async Task<InstallationAnalysis> AnalyzeInstallationAsync(ClaudeCodeInfo claudeCodeInfo)
        {
            return await Task.FromResult(new InstallationAnalysis
            {
                InstallationType = DetermineInstallationType(claudeCodeInfo),
                InstallationPath = claudeCodeInfo.InstallPath ?? "未知",
                InstallationHealth = DetermineInstallationHealth(claudeCodeInfo),
                RequiredUpdates = new List<string>(),
                MissingDependencies = new List<string>()
            });
        }

        private string DetermineInstallationType(ClaudeCodeInfo claudeCodeInfo)
        {
            if (claudeCodeInfo.InstallPath?.Contains("npm") == true)
                return "NPM全局安装";
            if (claudeCodeInfo.InstallPath?.Contains("Program Files") == true)
                return "系统安装";
            return "本地安装";
        }

        private string DetermineInstallationHealth(ClaudeCodeInfo claudeCodeInfo)
        {
            if (claudeCodeInfo.Status == DetectionStatus.Detected &&
                claudeCodeInfo.Compatibility == CompatibilityLevel.FullyCompatible)
                return "健康";
            if (claudeCodeInfo.Status == DetectionStatus.IncompatibleVersion)
                return "需要更新";
            if (claudeCodeInfo.Status == DetectionStatus.Failed)
                return "有问题";
            return "未知";
        }

        private List<string> GenerateUsageRecommendations(ClaudeCodeInfo claudeCodeInfo)
        {
            var recommendations = new List<string>();

            if (claudeCodeInfo.Status == DetectionStatus.Detected)
            {
                recommendations.Add("定期更新Claude Code CLI到最新版本");

                if (claudeCodeInfo.AuthStatus == AuthenticationStatus.NotAuthenticated)
                {
                    recommendations.Add("运行身份认证以启用完整功能");
                }

                if (claudeCodeInfo.SupportedFeatures.Contains(ClaudeCodeFeature.ProjectManagement))
                {
                    recommendations.Add("使用项目管理功能组织代码");
                }
            }

            return recommendations;
        }

        private List<string> GenerateTroubleshootingSteps(ClaudeCodeInfo claudeCodeInfo)
        {
            var steps = new List<string>();

            if (claudeCodeInfo.Status == DetectionStatus.Failed)
            {
                steps.Add("检查Claude Code CLI是否正确安装");
                steps.Add("验证PATH环境变量包含Claude Code路径");
                steps.Add("尝试重新安装Claude Code CLI");
            }

            if (claudeCodeInfo.Status == DetectionStatus.IncompatibleVersion)
            {
                steps.Add("升级Claude Code CLI到最新版本");
                steps.Add("检查系统兼容性要求");
            }

            return steps;
        }

        private async Task<SecurityAssessment> AssessClaudeCodeSecurityAsync(ClaudeCodeInfo claudeCodeInfo)
        {
            return await Task.FromResult(new SecurityAssessment
            {
                TrustLevel = claudeCodeInfo.Status == DetectionStatus.Detected ? "高" : "未知",
                SecurityRisks = new List<string>(),
                Recommendations = new List<string>()
            });
        }

        private string AnalyzePerformance(ClaudeCodeInfo claudeCodeInfo)
        {
            if (claudeCodeInfo.Performance == null)
                return "无性能数据";

            var perf = claudeCodeInfo.Performance;
            if (perf.StartupTimeMs < 1000)
                return "优秀";
            if (perf.StartupTimeMs < 3000)
                return "良好";
            return "需要优化";
        }

        private async Task<bool> CheckForUpdatesAsync(ClaudeCodeInfo claudeCodeInfo)
        {
            // 简化实现，实际应该检查远程版本
            return await Task.FromResult(false);
        }

        private List<EnvironmentChange> IdentifyChanges(EnvironmentReport previous, EnvironmentReport current)
        {
            var changes = new List<EnvironmentChange>();

            // 比较检测到的环境数量
            if (previous.Summary.DetectedEnvironments != current.Summary.DetectedEnvironments)
            {
                changes.Add(new EnvironmentChange
                {
                    Type = "Environment Count",
                    Description = $"检测到的环境数量从 {previous.Summary.DetectedEnvironments} 变为 {current.Summary.DetectedEnvironments}",
                    Impact = previous.Summary.DetectedEnvironments > current.Summary.DetectedEnvironments ? "负面" : "正面"
                });
            }

            return changes;
        }

        private PerformanceChanges ComparePerformance(EnvironmentReport previous, EnvironmentReport current)
        {
            return new PerformanceChanges
            {
                DetectionTimeChange = current.Summary.DetectionDuration - previous.Summary.DetectionDuration,
                PerformanceImpact = current.Summary.DetectionDuration > previous.Summary.DetectionDuration ? "下降" : "提升"
            };
        }

        private List<Issue> IdentifyNewIssues(EnvironmentReport previous, EnvironmentReport current)
        {
            return current.Issues.Where(currentIssue =>
                !previous.Issues.Any(prevIssue => prevIssue.Title == currentIssue.Title))
                .ToList();
        }

        private List<Issue> IdentifyResolvedIssues(EnvironmentReport previous, EnvironmentReport current)
        {
            return previous.Issues.Where(prevIssue =>
                !current.Issues.Any(currentIssue => currentIssue.Title == prevIssue.Title))
                .ToList();
        }

        private string GenerateChangeSummary(EnvironmentReport previous, EnvironmentReport current)
        {
            var improvements = 0;
            var regressions = 0;

            if (current.Summary.DetectedEnvironments > previous.Summary.DetectedEnvironments)
                improvements++;
            else if (current.Summary.DetectedEnvironments < previous.Summary.DetectedEnvironments)
                regressions++;

            if (current.Issues.Count < previous.Issues.Count)
                improvements++;
            else if (current.Issues.Count > previous.Issues.Count)
                regressions++;

            if (improvements > regressions)
                return "环境状况有所改善";
            if (regressions > improvements)
                return "环境状况有所恶化";
            return "环境状况基本稳定";
        }

        private string SerializeToJson(EnvironmentReport report)
        {
            // 简化的JSON序列化，实际应该使用JSON库
            return $"{{\"GeneratedAt\":\"{report.GeneratedAt:yyyy-MM-dd HH:mm:ss}\",\"Status\":\"{report.Summary.OverallStatus}\"}}";
        }

        private string SerializeToXml(EnvironmentReport report)
        {
            // 简化的XML序列化，实际应该使用XML库
            return $"<EnvironmentReport><GeneratedAt>{report.GeneratedAt:yyyy-MM-dd HH:mm:ss}</GeneratedAt><Status>{report.Summary.OverallStatus}</Status></EnvironmentReport>";
        }

        private string GenerateFileName(EnvironmentReport report, ReportFormat format)
        {
            var timestamp = report.GeneratedAt.ToString("yyyyMMdd_HHmmss");
            var extension = format switch
            {
                ReportFormat.Html => "html",
                ReportFormat.Text => "txt",
                ReportFormat.Json => "json",
                ReportFormat.Xml => "xml",
                _ => "txt"
            };

            return $"EnvironmentReport_{timestamp}.{extension}";
        }

        private void OnReportGenerated(ReportGeneratedEventArgs e)
        {
            ReportGenerated?.Invoke(this, e);
        }
    }

    /// <summary>
    /// 报告生成事件参数
    /// </summary>
    public class ReportGeneratedEventArgs : EventArgs
    {
        /// <summary>
        /// 生成的报告
        /// </summary>
        public EnvironmentReport Report { get; }

        /// <summary>
        /// 初始化事件参数
        /// </summary>
        /// <param name="report">环境报告</param>
        public ReportGeneratedEventArgs(EnvironmentReport report)
        {
            Report = report ?? throw new ArgumentNullException(nameof(report));
        }
    }

    /// <summary>
    /// Claude Code专项报告
    /// </summary>
    public class ClaudeCodeReport
    {
        /// <summary>
        /// 生成时间
        /// </summary>
        public DateTime GeneratedAt { get; set; }

        /// <summary>
        /// Claude Code信息
        /// </summary>
        public ClaudeCodeInfo? ClaudeCodeInfo { get; set; }

        /// <summary>
        /// 安装分析
        /// </summary>
        public InstallationAnalysis? InstallationAnalysis { get; set; }

        /// <summary>
        /// 使用建议
        /// </summary>
        public List<string> UsageRecommendations { get; set; } = new List<string>();

        /// <summary>
        /// 故障排除步骤
        /// </summary>
        public List<string> TroubleshootingSteps { get; set; } = new List<string>();

        /// <summary>
        /// 安全评估
        /// </summary>
        public SecurityAssessment? SecurityAssessment { get; set; }

        /// <summary>
        /// 性能分析
        /// </summary>
        public string PerformanceAnalysis { get; set; } = "";

        /// <summary>
        /// 是否有可用更新
        /// </summary>
        public bool UpdateAvailable { get; set; }
    }

    /// <summary>
    /// 安装分析
    /// </summary>
    public class InstallationAnalysis
    {
        /// <summary>
        /// 安装类型
        /// </summary>
        public string InstallationType { get; set; } = "";

        /// <summary>
        /// 安装路径
        /// </summary>
        public string InstallationPath { get; set; } = "";

        /// <summary>
        /// 安装健康状况
        /// </summary>
        public string InstallationHealth { get; set; } = "";

        /// <summary>
        /// 需要的更新
        /// </summary>
        public List<string> RequiredUpdates { get; set; } = new List<string>();

        /// <summary>
        /// 缺失的依赖
        /// </summary>
        public List<string> MissingDependencies { get; set; } = new List<string>();
    }

    /// <summary>
    /// 环境比较报告
    /// </summary>
    public class EnvironmentComparisonReport
    {
        /// <summary>
        /// 生成时间
        /// </summary>
        public DateTime GeneratedAt { get; set; }

        /// <summary>
        /// 之前的报告
        /// </summary>
        public EnvironmentReport? PreviousReport { get; set; }

        /// <summary>
        /// 当前的报告
        /// </summary>
        public EnvironmentReport? CurrentReport { get; set; }

        /// <summary>
        /// 变化列表
        /// </summary>
        public List<EnvironmentChange> Changes { get; set; } = new List<EnvironmentChange>();

        /// <summary>
        /// 性能变化
        /// </summary>
        public PerformanceChanges? PerformanceChanges { get; set; }

        /// <summary>
        /// 新出现的问题
        /// </summary>
        public List<Issue> NewIssues { get; set; } = new List<Issue>();

        /// <summary>
        /// 已解决的问题
        /// </summary>
        public List<Issue> ResolvedIssues { get; set; } = new List<Issue>();

        /// <summary>
        /// 变化摘要
        /// </summary>
        public string Summary { get; set; } = "";
    }

    /// <summary>
    /// 环境变化
    /// </summary>
    public class EnvironmentChange
    {
        /// <summary>
        /// 变化类型
        /// </summary>
        public string Type { get; set; } = "";

        /// <summary>
        /// 变化描述
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// 影响评估
        /// </summary>
        public string Impact { get; set; } = "";
    }

    /// <summary>
    /// 性能变化
    /// </summary>
    public class PerformanceChanges
    {
        /// <summary>
        /// 检测时间变化
        /// </summary>
        public long DetectionTimeChange { get; set; }

        /// <summary>
        /// 性能影响
        /// </summary>
        public string PerformanceImpact { get; set; } = "";
    }

    /// <summary>
    /// 报告格式枚举
    /// </summary>
    public enum ReportFormat
    {
        /// <summary>
        /// HTML格式
        /// </summary>
        Html,

        /// <summary>
        /// 纯文本格式
        /// </summary>
        Text,

        /// <summary>
        /// JSON格式
        /// </summary>
        Json,

        /// <summary>
        /// XML格式
        /// </summary>
        Xml
    }
}