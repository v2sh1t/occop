using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Occop.Core.Models.Environment;
using Occop.Core.Services.Environment;

namespace Occop.Core.Tests.Models.Environment
{
    /// <summary>
    /// 环境报告测试
    /// </summary>
    public class EnvironmentReportTests
    {
        private DetectionResult CreateTestDetectionResult()
        {
            var result = new DetectionResult();

            var psCore = new EnvironmentInfo(EnvironmentType.PowerShellCore);
            psCore.SetDetected(@"C:\Program Files\PowerShell\7", @"C:\Program Files\PowerShell\7\pwsh.exe", "7.3.0");
            psCore.IsRecommended = true;

            var claudeCode = new EnvironmentInfo(EnvironmentType.ClaudeCode);
            claudeCode.SetDetected(@"C:\npm\claude", @"C:\npm\claude\claude.exe", "1.2.0");

            result.AddEnvironment(EnvironmentType.PowerShellCore, psCore);
            result.AddEnvironment(EnvironmentType.ClaudeCode, claudeCode);
            result.RecommendedShell = psCore;
            result.MarkCompleted();

            return result;
        }

        [Fact]
        public void EnvironmentReport_Initialization_ShouldSetDefaultValues()
        {
            // Arrange
            var detectionResult = CreateTestDetectionResult();

            // Act
            var report = new EnvironmentReport(detectionResult);

            // Assert
            Assert.NotNull(report);
            Assert.True(report.GeneratedAt > DateTime.MinValue);
            Assert.Equal("1.0.0", report.Version);
            Assert.Equal(detectionResult, report.DetectionResult);
            Assert.NotNull(report.System);
            Assert.NotNull(report.Summary);
            Assert.NotNull(report.Recommendations);
            Assert.NotNull(report.Issues);
            Assert.NotNull(report.Performance);
            Assert.NotNull(report.Security);
            Assert.NotNull(report.Configuration);
            Assert.NotNull(report.History);
            Assert.NotNull(report.Metadata);
        }

        [Fact]
        public void EnvironmentReport_WithNullDetectionResult_ShouldThrowException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new EnvironmentReport(null!));
        }

        [Fact]
        public void EnvironmentReport_ShouldGenerateCorrectSummary()
        {
            // Arrange
            var detectionResult = CreateTestDetectionResult();

            // Act
            var report = new EnvironmentReport(detectionResult);

            // Assert
            Assert.Equal(2, report.Summary.TotalEnvironments);
            Assert.Equal(2, report.Summary.DetectedEnvironments);
            Assert.Equal(0, report.Summary.FailedDetections);
            Assert.True(report.Summary.HasRequiredEnvironments);
            Assert.Equal("PowerShellCore", report.Summary.RecommendedShell);
            Assert.Equal(EnvironmentStatus.Healthy, report.Summary.OverallStatus);
        }

        [Fact]
        public void EnvironmentReport_ShouldConvertClaudeCodeInfo()
        {
            // Arrange
            var detectionResult = CreateTestDetectionResult();

            // Act
            var report = new EnvironmentReport(detectionResult);

            // Assert
            Assert.NotNull(report.ClaudeCode);
            Assert.Equal(DetectionStatus.Detected, report.ClaudeCode.Status);
            Assert.Equal("1.2.0", report.ClaudeCode.Version);
            Assert.Equal(@"C:\npm\claude\claude.exe", report.ClaudeCode.ExecutablePath);
            Assert.Equal(@"C:\npm\claude", report.ClaudeCode.InstallPath);
        }

        [Fact]
        public void EnvironmentReport_ShouldGenerateRecommendations()
        {
            // Arrange
            var detectionResult = new DetectionResult();
            // 没有任何环境被检测到

            // Act
            var report = new EnvironmentReport(detectionResult);

            // Assert
            Assert.NotEmpty(report.Recommendations);
            Assert.Contains(report.Recommendations, r => r.Title.Contains("Shell环境"));
            Assert.Contains(report.Recommendations, r => r.Title.Contains("Claude Code CLI"));
        }

        [Fact]
        public void EnvironmentReport_ShouldIdentifyIssues()
        {
            // Arrange
            var detectionResult = new DetectionResult();
            detectionResult.AddError(EnvironmentType.ClaudeCode, "Claude CLI not found");

            // Act
            var report = new EnvironmentReport(detectionResult);

            // Assert
            Assert.NotEmpty(report.Issues);
            Assert.Contains(report.Issues, i => i.Title.Contains("ClaudeCode 检测失败"));
        }

        [Fact]
        public void EnvironmentReport_ShouldAddHistoryEntry()
        {
            // Arrange
            var detectionResult = CreateTestDetectionResult();

            // Act
            var report = new EnvironmentReport(detectionResult);

            // Assert
            Assert.Single(report.History);
            var historyEntry = report.History.First();
            Assert.Equal(2, historyEntry.TotalDetected);
            Assert.True(historyEntry.HasClaudeCode);
            Assert.True(historyEntry.HasShellEnvironment);
        }

        [Fact]
        public void EnvironmentReport_ShouldAddMetadata()
        {
            // Arrange
            var detectionResult = CreateTestDetectionResult();

            // Act
            var report = new EnvironmentReport(detectionResult);

            // Assert
            Assert.NotEmpty(report.Metadata);
            Assert.True(report.Metadata.ContainsKey("hostname"));
            Assert.True(report.Metadata.ContainsKey("username"));
            Assert.True(report.Metadata.ContainsKey("os_version"));
            Assert.True(report.Metadata.ContainsKey("processor_count"));
            Assert.True(report.Metadata.ContainsKey("working_directory"));
            Assert.True(report.Metadata.ContainsKey("report_generator"));
        }

        [Fact]
        public void GenerateHtmlReport_ShouldReturnValidHtml()
        {
            // Arrange
            var detectionResult = CreateTestDetectionResult();
            var report = new EnvironmentReport(detectionResult);

            // Act
            var html = report.GenerateHtmlReport();

            // Assert
            Assert.NotNull(html);
            Assert.Contains("<!DOCTYPE html>", html);
            Assert.Contains("<html>", html);
            Assert.Contains("环境检测报告", html);
            Assert.Contains("环境摘要", html);
            Assert.Contains("Claude Code CLI", html);
            Assert.Contains("</html>", html);
        }

        [Fact]
        public void GenerateTextReport_ShouldReturnValidText()
        {
            // Arrange
            var detectionResult = CreateTestDetectionResult();
            var report = new EnvironmentReport(detectionResult);

            // Act
            var text = report.GenerateTextReport();

            // Assert
            Assert.NotNull(text);
            Assert.Contains("=== 环境检测报告 ===", text);
            Assert.Contains("=== 环境摘要 ===", text);
            Assert.Contains("=== Claude Code CLI ===", text);
            Assert.Contains("总体状态: Healthy", text);
        }

        [Fact]
        public void ToString_ShouldReturnCorrectStringRepresentation()
        {
            // Arrange
            var detectionResult = CreateTestDetectionResult();
            var report = new EnvironmentReport(detectionResult);

            // Act
            var result = report.ToString();

            // Assert
            Assert.Contains("环境检测报告", result);
            Assert.Contains("Healthy", result);
            Assert.Contains("2/2", result);
        }
    }

    /// <summary>
    /// 系统信息测试
    /// </summary>
    public class SystemInfoTests
    {
        [Fact]
        public void SystemInfo_Initialization_ShouldSetDefaultValues()
        {
            // Arrange & Act
            var systemInfo = new SystemInfo();

            // Assert
            Assert.NotNull(systemInfo.OperatingSystem);
            Assert.NotNull(systemInfo.MachineName);
            Assert.NotNull(systemInfo.UserName);
            Assert.True(systemInfo.ProcessorCount > 0);
            Assert.NotNull(systemInfo.WorkingDirectory);
            Assert.True(systemInfo.SystemStartTime > DateTime.MinValue);
        }
    }

    /// <summary>
    /// 环境摘要测试
    /// </summary>
    public class EnvironmentSummaryTests
    {
        [Fact]
        public void EnvironmentSummary_Initialization_ShouldSetDefaultValues()
        {
            // Arrange & Act
            var summary = new EnvironmentSummary();

            // Assert
            Assert.Equal(0, summary.TotalEnvironments);
            Assert.Equal(0, summary.DetectedEnvironments);
            Assert.Equal(0, summary.FailedDetections);
            Assert.False(summary.HasRequiredEnvironments);
            Assert.Equal("", summary.RecommendedShell);
            Assert.Equal(EnvironmentStatus.Healthy, summary.OverallStatus);
            Assert.Equal(0, summary.DetectionDuration);
        }
    }

    /// <summary>
    /// 推荐建议测试
    /// </summary>
    public class RecommendationTests
    {
        [Fact]
        public void Recommendation_Initialization_ShouldSetDefaultValues()
        {
            // Arrange & Act
            var recommendation = new Recommendation();

            // Assert
            Assert.Equal(RecommendationPriority.Low, recommendation.Priority);
            Assert.Equal("", recommendation.Category);
            Assert.Equal("", recommendation.Title);
            Assert.Equal("", recommendation.Description);
            Assert.Equal("", recommendation.Action);
            Assert.Equal("", recommendation.Impact);
        }

        [Fact]
        public void Recommendation_PropertiesCanBeSet()
        {
            // Arrange
            var recommendation = new Recommendation();

            // Act
            recommendation.Priority = RecommendationPriority.High;
            recommendation.Category = "Test Category";
            recommendation.Title = "Test Title";
            recommendation.Description = "Test Description";
            recommendation.Action = "Test Action";
            recommendation.Impact = "Test Impact";

            // Assert
            Assert.Equal(RecommendationPriority.High, recommendation.Priority);
            Assert.Equal("Test Category", recommendation.Category);
            Assert.Equal("Test Title", recommendation.Title);
            Assert.Equal("Test Description", recommendation.Description);
            Assert.Equal("Test Action", recommendation.Action);
            Assert.Equal("Test Impact", recommendation.Impact);
        }
    }

    /// <summary>
    /// 问题信息测试
    /// </summary>
    public class IssueTests
    {
        [Fact]
        public void Issue_Initialization_ShouldSetDefaultValues()
        {
            // Arrange & Act
            var issue = new Issue();

            // Assert
            Assert.Equal(IssueSeverity.Info, issue.Severity);
            Assert.Equal("", issue.Category);
            Assert.Equal("", issue.Title);
            Assert.Equal("", issue.Description);
            Assert.Equal("", issue.Impact);
            Assert.Equal("", issue.Resolution);
        }

        [Fact]
        public void Issue_PropertiesCanBeSet()
        {
            // Arrange
            var issue = new Issue();

            // Act
            issue.Severity = IssueSeverity.Critical;
            issue.Category = "Test Category";
            issue.Title = "Test Title";
            issue.Description = "Test Description";
            issue.Impact = "Test Impact";
            issue.Resolution = "Test Resolution";

            // Assert
            Assert.Equal(IssueSeverity.Critical, issue.Severity);
            Assert.Equal("Test Category", issue.Category);
            Assert.Equal("Test Title", issue.Title);
            Assert.Equal("Test Description", issue.Description);
            Assert.Equal("Test Impact", issue.Impact);
            Assert.Equal("Test Resolution", issue.Resolution);
        }
    }

    /// <summary>
    /// 性能评估测试
    /// </summary>
    public class PerformanceAssessmentTests
    {
        [Fact]
        public void PerformanceAssessment_Initialization_ShouldSetDefaultValues()
        {
            // Arrange & Act
            var assessment = new PerformanceAssessment();

            // Assert
            Assert.Equal("", assessment.DetectionSpeed);
            Assert.Equal("", assessment.SystemLoad);
            Assert.Equal("", assessment.ResourceUsage);
            Assert.NotNull(assessment.Bottlenecks);
            Assert.Empty(assessment.Bottlenecks);
        }
    }

    /// <summary>
    /// 安全评估测试
    /// </summary>
    public class SecurityAssessmentTests
    {
        [Fact]
        public void SecurityAssessment_Initialization_ShouldSetDefaultValues()
        {
            // Arrange & Act
            var assessment = new SecurityAssessment();

            // Assert
            Assert.Equal("", assessment.TrustLevel);
            Assert.NotNull(assessment.SecurityRisks);
            Assert.Empty(assessment.SecurityRisks);
            Assert.NotNull(assessment.Recommendations);
            Assert.Empty(assessment.Recommendations);
        }
    }

    /// <summary>
    /// 配置建议测试
    /// </summary>
    public class ConfigurationSuggestionsTests
    {
        [Fact]
        public void ConfigurationSuggestions_Initialization_ShouldSetDefaultValues()
        {
            // Arrange & Act
            var suggestions = new ConfigurationSuggestions();

            // Assert
            Assert.NotNull(suggestions.EnvironmentVariables);
            Assert.Empty(suggestions.EnvironmentVariables);
            Assert.NotNull(suggestions.PathOptimizations);
            Assert.Empty(suggestions.PathOptimizations);
            Assert.NotNull(suggestions.SecuritySettings);
            Assert.Empty(suggestions.SecuritySettings);
        }
    }

    /// <summary>
    /// 检测历史条目测试
    /// </summary>
    public class DetectionHistoryEntryTests
    {
        [Fact]
        public void DetectionHistoryEntry_Initialization_ShouldSetDefaultValues()
        {
            // Arrange & Act
            var entry = new DetectionHistoryEntry();

            // Assert
            Assert.Equal(DateTime.MinValue, entry.Timestamp);
            Assert.Equal(0, entry.TotalDetected);
            Assert.Equal(0, entry.Duration);
            Assert.False(entry.HasClaudeCode);
            Assert.False(entry.HasShellEnvironment);
        }

        [Fact]
        public void DetectionHistoryEntry_PropertiesCanBeSet()
        {
            // Arrange
            var entry = new DetectionHistoryEntry();
            var timestamp = DateTime.UtcNow;

            // Act
            entry.Timestamp = timestamp;
            entry.TotalDetected = 5;
            entry.Duration = 1500;
            entry.HasClaudeCode = true;
            entry.HasShellEnvironment = true;

            // Assert
            Assert.Equal(timestamp, entry.Timestamp);
            Assert.Equal(5, entry.TotalDetected);
            Assert.Equal(1500, entry.Duration);
            Assert.True(entry.HasClaudeCode);
            Assert.True(entry.HasShellEnvironment);
        }
    }

    /// <summary>
    /// 枚举测试
    /// </summary>
    public class EnvironmentReportEnumsTests
    {
        [Fact]
        public void EnvironmentStatus_ShouldHaveExpectedValues()
        {
            // Arrange & Act & Assert
            Assert.True(Enum.IsDefined(typeof(EnvironmentStatus), EnvironmentStatus.Healthy));
            Assert.True(Enum.IsDefined(typeof(EnvironmentStatus), EnvironmentStatus.Warning));
            Assert.True(Enum.IsDefined(typeof(EnvironmentStatus), EnvironmentStatus.Critical));
        }

        [Fact]
        public void RecommendationPriority_ShouldHaveExpectedValues()
        {
            // Arrange & Act & Assert
            var priorities = Enum.GetValues<RecommendationPriority>();
            Assert.Contains(RecommendationPriority.Low, priorities);
            Assert.Contains(RecommendationPriority.Medium, priorities);
            Assert.Contains(RecommendationPriority.High, priorities);
            Assert.Contains(RecommendationPriority.Critical, priorities);
        }

        [Fact]
        public void IssueSeverity_ShouldHaveExpectedValues()
        {
            // Arrange & Act & Assert
            var severities = Enum.GetValues<IssueSeverity>();
            Assert.Contains(IssueSeverity.Info, severities);
            Assert.Contains(IssueSeverity.Warning, severities);
            Assert.Contains(IssueSeverity.Error, severities);
            Assert.Contains(IssueSeverity.Critical, severities);
        }
    }
}