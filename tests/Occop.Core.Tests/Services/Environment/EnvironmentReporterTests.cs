using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Occop.Core.Models.Environment;
using Occop.Core.Services.Environment;

namespace Occop.Core.Tests.Services.Environment
{
    /// <summary>
    /// 环境报告生成器测试
    /// </summary>
    public class EnvironmentReporterTests : IDisposable
    {
        private readonly EnvironmentReporter _reporter;
        private readonly ClaudeCodeDetector _claudeCodeDetector;

        public EnvironmentReporterTests()
        {
            _claudeCodeDetector = new ClaudeCodeDetector();
            _reporter = new EnvironmentReporter(_claudeCodeDetector);
        }

        private DetectionResult CreateTestDetectionResult()
        {
            var result = new DetectionResult();

            var psCore = new EnvironmentInfo(EnvironmentType.PowerShellCore);
            psCore.SetDetected(@"C:\Program Files\PowerShell\7", @"C:\Program Files\PowerShell\7\pwsh.exe", "7.3.0");
            psCore.IsRecommended = true;

            var claudeCode = new EnvironmentInfo(EnvironmentType.ClaudeCode);
            claudeCode.SetDetected(@"C:\npm\claude", @"C:\npm\claude\claude.exe", "1.2.0");

            var gitBash = new EnvironmentInfo(EnvironmentType.GitBash);
            gitBash.SetFailed("Git not found in PATH");

            result.AddEnvironment(EnvironmentType.PowerShellCore, psCore);
            result.AddEnvironment(EnvironmentType.ClaudeCode, claudeCode);
            result.AddEnvironment(EnvironmentType.GitBash, gitBash);
            result.AddError(EnvironmentType.GitBash, "Git not found in PATH");
            result.RecommendedShell = psCore;
            result.MarkCompleted();

            return result;
        }

        [Fact]
        public void EnvironmentReporter_Initialization_ShouldNotThrow()
        {
            // Arrange & Act & Assert
            Assert.DoesNotThrow(() => new EnvironmentReporter());
            Assert.DoesNotThrow(() => new EnvironmentReporter(_claudeCodeDetector));
        }

        [Fact]
        public async Task GenerateReportAsync_WithValidDetectionResult_ShouldReturnValidReport()
        {
            // Arrange
            var detectionResult = CreateTestDetectionResult();

            // Act
            var report = await _reporter.GenerateReportAsync(detectionResult);

            // Assert
            Assert.NotNull(report);
            Assert.Equal(detectionResult, report.DetectionResult);
            Assert.True(report.GeneratedAt > DateTime.MinValue);
            Assert.NotNull(report.System);
            Assert.NotNull(report.Summary);
            Assert.NotNull(report.ClaudeCode);

            // 验证摘要信息
            Assert.Equal(3, report.Summary.TotalEnvironments);
            Assert.Equal(2, report.Summary.DetectedEnvironments);
            Assert.Equal(1, report.Summary.FailedDetections);
            Assert.True(report.Summary.HasRequiredEnvironments);
            Assert.Equal("PowerShellCore", report.Summary.RecommendedShell);

            // 验证Claude Code信息转换
            Assert.Equal(DetectionStatus.Detected, report.ClaudeCode.Status);
            Assert.Equal("1.2.0", report.ClaudeCode.Version);

            // 验证建议和问题
            Assert.NotEmpty(report.Recommendations);
            Assert.NotEmpty(report.Issues);

            // 验证历史记录
            Assert.Single(report.History);

            Console.WriteLine($"生成的报告摘要: {report}");
        }

        [Fact]
        public async Task GenerateReportAsync_WithNullDetectionResult_ShouldThrowException()
        {
            // Arrange & Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _reporter.GenerateReportAsync(null!));
        }

        [Fact]
        public async Task GenerateReportAsync_WithDetailedAnalysis_ShouldEnhanceClaudeCodeInfo()
        {
            // Arrange
            var detectionResult = CreateTestDetectionResult();

            // Act
            var report = await _reporter.GenerateReportAsync(detectionResult, includeDetailedAnalysis: true);

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.ClaudeCode);

            // 详细分析应该包含更多信息（实际行为取决于Claude Code是否真的可用）
            Console.WriteLine($"Claude Code详细信息:");
            Console.WriteLine($"  状态: {report.ClaudeCode.Status}");
            Console.WriteLine($"  版本: {report.ClaudeCode.Version}");
            Console.WriteLine($"  兼容性: {report.ClaudeCode.GetCompatibilityDescription()}");
        }

        [Fact]
        public async Task GenerateClaudeCodeReportAsync_ShouldReturnValidClaudeCodeReport()
        {
            // Act
            var report = await _reporter.GenerateClaudeCodeReportAsync();

            // Assert
            Assert.NotNull(report);
            Assert.True(report.GeneratedAt > DateTime.MinValue);
            Assert.NotNull(report.ClaudeCodeInfo);
            Assert.NotNull(report.InstallationAnalysis);
            Assert.NotNull(report.UsageRecommendations);
            Assert.NotNull(report.TroubleshootingSteps);
            Assert.NotNull(report.SecurityAssessment);
            Assert.NotNull(report.PerformanceAnalysis);

            Console.WriteLine($"Claude Code专项报告:");
            Console.WriteLine($"  生成时间: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"  检测状态: {report.ClaudeCodeInfo.Status}");
            Console.WriteLine($"  安装类型: {report.InstallationAnalysis.InstallationType}");
            Console.WriteLine($"  性能分析: {report.PerformanceAnalysis}");
            Console.WriteLine($"  是否有更新: {report.UpdateAvailable}");

            if (report.UsageRecommendations.Any())
            {
                Console.WriteLine($"  使用建议:");
                foreach (var recommendation in report.UsageRecommendations)
                {
                    Console.WriteLine($"    - {recommendation}");
                }
            }

            if (report.TroubleshootingSteps.Any())
            {
                Console.WriteLine($"  故障排除步骤:");
                foreach (var step in report.TroubleshootingSteps)
                {
                    Console.WriteLine($"    - {step}");
                }
            }
        }

        [Fact]
        public async Task GenerateClaudeCodeReportAsync_WithForceRefresh_ShouldIgnoreCache()
        {
            // Arrange
            var report1 = await _reporter.GenerateClaudeCodeReportAsync(false);

            // Act
            var report2 = await _reporter.GenerateClaudeCodeReportAsync(true);

            // Assert
            Assert.NotNull(report1);
            Assert.NotNull(report2);
            Assert.True(report2.GeneratedAt >= report1.GeneratedAt);

            // 验证状态一致性
            Assert.Equal(report1.ClaudeCodeInfo.Status, report2.ClaudeCodeInfo.Status);
        }

        [Fact]
        public void GenerateComparisonReport_WithValidReports_ShouldReturnComparison()
        {
            // Arrange
            var detectionResult1 = CreateTestDetectionResult();
            var detectionResult2 = CreateTestDetectionResult();

            var report1 = new EnvironmentReport(detectionResult1);
            var report2 = new EnvironmentReport(detectionResult2);

            // Act
            var comparison = _reporter.GenerateComparisonReport(report1, report2);

            // Assert
            Assert.NotNull(comparison);
            Assert.True(comparison.GeneratedAt > DateTime.MinValue);
            Assert.Equal(report1, comparison.PreviousReport);
            Assert.Equal(report2, comparison.CurrentReport);
            Assert.NotNull(comparison.Changes);
            Assert.NotNull(comparison.PerformanceChanges);
            Assert.NotNull(comparison.NewIssues);
            Assert.NotNull(comparison.ResolvedIssues);
            Assert.NotNull(comparison.Summary);

            Console.WriteLine($"比较报告摘要: {comparison.Summary}");
        }

        [Fact]
        public void GenerateComparisonReport_WithNullPreviousReport_ShouldThrowException()
        {
            // Arrange
            var currentReport = new EnvironmentReport(CreateTestDetectionResult());

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                _reporter.GenerateComparisonReport(null!, currentReport));
        }

        [Fact]
        public void GenerateComparisonReport_WithNullCurrentReport_ShouldThrowException()
        {
            // Arrange
            var previousReport = new EnvironmentReport(CreateTestDetectionResult());

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                _reporter.GenerateComparisonReport(previousReport, null!));
        }

        [Fact]
        public async Task ExportReportAsync_ToHtml_ShouldCreateValidFile()
        {
            // Arrange
            var detectionResult = CreateTestDetectionResult();
            var report = await _reporter.GenerateReportAsync(detectionResult);
            var tempDir = Path.GetTempPath();
            var outputPath = Path.Combine(tempDir, "OccopTests");

            try
            {
                // Act
                var filePath = await _reporter.ExportReportAsync(report, outputPath, ReportFormat.Html);

                // Assert
                Assert.NotNull(filePath);
                Assert.True(File.Exists(filePath));
                Assert.True(filePath.EndsWith(".html"));

                var content = await File.ReadAllTextAsync(filePath);
                Assert.Contains("<!DOCTYPE html>", content);
                Assert.Contains("环境检测报告", content);

                Console.WriteLine($"HTML报告导出到: {filePath}");
                Console.WriteLine($"文件大小: {new FileInfo(filePath).Length} 字节");
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(outputPath))
                {
                    Directory.Delete(outputPath, true);
                }
            }
        }

        [Fact]
        public async Task ExportReportAsync_ToText_ShouldCreateValidFile()
        {
            // Arrange
            var detectionResult = CreateTestDetectionResult();
            var report = await _reporter.GenerateReportAsync(detectionResult);
            var tempDir = Path.GetTempPath();
            var outputPath = Path.Combine(tempDir, "OccopTests");

            try
            {
                // Act
                var filePath = await _reporter.ExportReportAsync(report, outputPath, ReportFormat.Text);

                // Assert
                Assert.NotNull(filePath);
                Assert.True(File.Exists(filePath));
                Assert.True(filePath.EndsWith(".txt"));

                var content = await File.ReadAllTextAsync(filePath);
                Assert.Contains("=== 环境检测报告 ===", content);

                Console.WriteLine($"文本报告导出到: {filePath}");
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(outputPath))
                {
                    Directory.Delete(outputPath, true);
                }
            }
        }

        [Fact]
        public async Task ExportReportAsync_WithNullReport_ShouldThrowException()
        {
            // Arrange
            var outputPath = Path.GetTempPath();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _reporter.ExportReportAsync(null!, outputPath));
        }

        [Fact]
        public async Task ExportReportAsync_WithInvalidOutputPath_ShouldThrowException()
        {
            // Arrange
            var detectionResult = CreateTestDetectionResult();
            var report = await _reporter.GenerateReportAsync(detectionResult);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _reporter.ExportReportAsync(report, null!));

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _reporter.ExportReportAsync(report, ""));
        }

        [Fact]
        public async Task GetReportHistory_ShouldReturnCorrectHistory()
        {
            // Arrange
            var detectionResult = CreateTestDetectionResult();

            // 生成多个报告
            await _reporter.GenerateReportAsync(detectionResult);
            await _reporter.GenerateReportAsync(detectionResult);
            await _reporter.GenerateReportAsync(detectionResult);

            // Act
            var history = _reporter.GetReportHistory();

            // Assert
            Assert.NotNull(history);
            Assert.Equal(3, history.Count);

            // 验证按时间降序排列
            for (int i = 1; i < history.Count; i++)
            {
                Assert.True(history[i - 1].GeneratedAt >= history[i].GeneratedAt);
            }
        }

        [Fact]
        public async Task GetReportHistory_WithMaxCount_ShouldLimitResults()
        {
            // Arrange
            var detectionResult = CreateTestDetectionResult();

            // 生成多个报告
            for (int i = 0; i < 5; i++)
            {
                await _reporter.GenerateReportAsync(detectionResult);
            }

            // Act
            var history = _reporter.GetReportHistory(3);

            // Assert
            Assert.NotNull(history);
            Assert.Equal(3, history.Count);
        }

        [Fact]
        public async Task ClearReportHistory_ShouldClearHistory()
        {
            // Arrange
            var detectionResult = CreateTestDetectionResult();
            await _reporter.GenerateReportAsync(detectionResult);
            await _reporter.GenerateReportAsync(detectionResult);

            // 验证有历史记录
            Assert.Equal(2, _reporter.GetReportHistory().Count);

            // Act
            _reporter.ClearReportHistory();

            // Assert
            Assert.Empty(_reporter.GetReportHistory());
        }

        [Fact]
        public async Task ClearReportHistory_WithOlderThan_ShouldClearSelectively()
        {
            // Arrange
            var detectionResult = CreateTestDetectionResult();
            await _reporter.GenerateReportAsync(detectionResult);

            var cutoffTime = DateTime.UtcNow.AddMinutes(1);

            await _reporter.GenerateReportAsync(detectionResult);

            // Act
            _reporter.ClearReportHistory(cutoffTime);

            // Assert
            var remainingHistory = _reporter.GetReportHistory();
            Assert.Single(remainingHistory);
            Assert.True(remainingHistory[0].GeneratedAt >= cutoffTime);
        }

        [Fact]
        public async Task ReportGenerated_Event_ShouldBeTriggered()
        {
            // Arrange
            var eventTriggered = false;
            EnvironmentReport? generatedReport = null;

            _reporter.ReportGenerated += (sender, args) =>
            {
                eventTriggered = true;
                generatedReport = args.Report;
            };

            var detectionResult = CreateTestDetectionResult();

            // Act
            var report = await _reporter.GenerateReportAsync(detectionResult);

            // Assert
            Assert.True(eventTriggered);
            Assert.NotNull(generatedReport);
            Assert.Equal(report, generatedReport);
        }

        public void Dispose()
        {
            // EnvironmentReporter 没有实现 IDisposable，但保持一致的测试模式
        }
    }

    /// <summary>
    /// 环境报告生成器集成测试
    /// </summary>
    public class EnvironmentReporterIntegrationTests
    {
        [Fact]
        public async Task FullWorkflow_RealEnvironment_ShouldGenerateComprehensiveReport()
        {
            // 这是一个完整的工作流集成测试

            // Arrange
            var detector = new ClaudeCodeDetector();
            var reporter = new EnvironmentReporter(detector);

            // 首先进行真实的环境检测
            var claudeCodeInfo = await detector.DetectClaudeCodeAsync();

            // 创建一个模拟的检测结果（包含真实的Claude Code信息）
            var detectionResult = new DetectionResult();

            var claudeCodeEnv = new EnvironmentInfo(EnvironmentType.ClaudeCode);
            if (claudeCodeInfo.Status == DetectionStatus.Detected)
            {
                claudeCodeEnv.SetDetected(
                    claudeCodeInfo.InstallPath ?? "",
                    claudeCodeInfo.ExecutablePath ?? "",
                    claudeCodeInfo.Version ?? "");
            }
            else
            {
                claudeCodeEnv.SetFailed(claudeCodeInfo.ErrorMessage ?? "检测失败");
            }

            detectionResult.AddEnvironment(EnvironmentType.ClaudeCode, claudeCodeEnv);
            detectionResult.MarkCompleted();

            // Act - 生成完整报告
            var report = await reporter.GenerateReportAsync(detectionResult, includeDetailedAnalysis: true);

            // Assert & Output comprehensive information
            Assert.NotNull(report);

            Console.WriteLine("=== 完整环境检测报告 ===");
            Console.WriteLine($"报告生成时间: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"报告版本: {report.Version}");

            Console.WriteLine("\n=== 系统信息 ===");
            Console.WriteLine($"操作系统: {report.System.OperatingSystem}");
            Console.WriteLine($"机器名: {report.System.MachineName}");
            Console.WriteLine($"用户名: {report.System.UserName}");
            Console.WriteLine($"处理器数量: {report.System.ProcessorCount}");
            Console.WriteLine($"工作目录: {report.System.WorkingDirectory}");

            Console.WriteLine("\n=== 环境摘要 ===");
            Console.WriteLine($"总体状态: {report.Summary.OverallStatus}");
            Console.WriteLine($"检测到的环境: {report.Summary.DetectedEnvironments}/{report.Summary.TotalEnvironments}");
            Console.WriteLine($"检测耗时: {report.Summary.DetectionDuration}ms");
            Console.WriteLine($"具备必需环境: {report.Summary.HasRequiredEnvironments}");

            if (report.ClaudeCode != null)
            {
                Console.WriteLine("\n=== Claude Code CLI ===");
                Console.WriteLine($"状态: {report.ClaudeCode.Status}");
                Console.WriteLine($"版本: {report.ClaudeCode.Version ?? "未知"}");
                Console.WriteLine($"兼容性: {report.ClaudeCode.GetCompatibilityDescription()}");
                Console.WriteLine($"支持功能: {report.ClaudeCode.GetFeatureSummary()}");
            }

            if (report.Recommendations.Any())
            {
                Console.WriteLine("\n=== 推荐建议 ===");
                foreach (var rec in report.Recommendations)
                {
                    Console.WriteLine($"[{rec.Priority}] {rec.Title}");
                    Console.WriteLine($"  描述: {rec.Description}");
                    Console.WriteLine($"  操作: {rec.Action}");
                }
            }

            if (report.Issues.Any())
            {
                Console.WriteLine("\n=== 发现的问题 ===");
                foreach (var issue in report.Issues)
                {
                    Console.WriteLine($"[{issue.Severity}] {issue.Title}");
                    Console.WriteLine($"  描述: {issue.Description}");
                    Console.WriteLine($"  解决方案: {issue.Resolution}");
                }
            }

            Console.WriteLine("\n=== 性能评估 ===");
            Console.WriteLine($"检测速度: {report.Performance.DetectionSpeed}");
            Console.WriteLine($"系统负载: {report.Performance.SystemLoad}");
            Console.WriteLine($"资源使用: {report.Performance.ResourceUsage}");

            if (report.Performance.Bottlenecks.Any())
            {
                Console.WriteLine("性能瓶颈:");
                foreach (var bottleneck in report.Performance.Bottlenecks)
                {
                    Console.WriteLine($"  - {bottleneck}");
                }
            }

            Console.WriteLine("\n=== 安全评估 ===");
            Console.WriteLine($"信任级别: {report.Security.TrustLevel}");

            if (report.Security.SecurityRisks.Any())
            {
                Console.WriteLine("安全风险:");
                foreach (var risk in report.Security.SecurityRisks)
                {
                    Console.WriteLine($"  - {risk}");
                }
            }

            // 测试报告导出
            var tempPath = Path.GetTempPath();
            var exportPath = Path.Combine(tempPath, "OccopIntegrationTest");

            try
            {
                var htmlFile = await reporter.ExportReportAsync(report, exportPath, ReportFormat.Html);
                var textFile = await reporter.ExportReportAsync(report, exportPath, ReportFormat.Text);

                Console.WriteLine($"\n=== 报告导出 ===");
                Console.WriteLine($"HTML报告: {htmlFile}");
                Console.WriteLine($"文本报告: {textFile}");

                Assert.True(File.Exists(htmlFile));
                Assert.True(File.Exists(textFile));
            }
            finally
            {
                if (Directory.Exists(exportPath))
                {
                    Directory.Delete(exportPath, true);
                }
            }
        }
    }
}