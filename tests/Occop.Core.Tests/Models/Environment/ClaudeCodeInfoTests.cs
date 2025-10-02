using System;
using System.Linq;
using Xunit;
using Occop.Core.Models.Environment;
using Occop.Core.Services.Environment;

namespace Occop.Core.Tests.Models.Environment
{
    /// <summary>
    /// Claude Code 信息测试
    /// </summary>
    public class ClaudeCodeInfoTests
    {
        [Fact]
        public void ClaudeCodeInfo_Initialization_ShouldSetDefaultValues()
        {
            // Arrange & Act
            var info = new ClaudeCodeInfo();

            // Assert
            Assert.Equal(DetectionStatus.NotDetected, info.Status);
            Assert.Equal(CompatibilityLevel.Unknown, info.Compatibility);
            Assert.NotNull(info.SupportedFeatures);
            Assert.Empty(info.SupportedFeatures);
            Assert.Equal(AuthenticationStatus.Unknown, info.AuthStatus);
            Assert.Equal(DateTime.MinValue, info.LastDetectionTime);
            Assert.NotNull(info.EnvironmentVariables);
            Assert.Empty(info.EnvironmentVariables);
            Assert.False(info.IsRecommendedVersion);
        }

        [Fact]
        public void SetDetected_WithValidData_ShouldUpdateStatusAndEvaluateCompatibility()
        {
            // Arrange
            var info = new ClaudeCodeInfo();
            var version = "1.2.5";
            var executablePath = @"C:\Users\Test\AppData\Roaming\npm\claude.exe";
            var installPath = @"C:\Users\Test\AppData\Roaming\npm";

            // Act
            info.SetDetected(version, executablePath, installPath);

            // Assert
            Assert.Equal(DetectionStatus.Detected, info.Status);
            Assert.Equal(version, info.Version);
            Assert.Equal(executablePath, info.ExecutablePath);
            Assert.Equal(installPath, info.InstallPath);
            Assert.NotNull(info.ParsedVersion);
            Assert.Equal(new Version(1, 2, 5), info.ParsedVersion);
            Assert.Equal(CompatibilityLevel.FullyCompatible, info.Compatibility);
            Assert.True(info.IsRecommendedVersion);
            Assert.True(info.LastDetectionTime > DateTime.MinValue);
            Assert.Null(info.ErrorMessage);
            Assert.Null(info.Exception);

            // 验证支持的功能
            Assert.Contains(ClaudeCodeFeature.BasicChat, info.SupportedFeatures);
            Assert.Contains(ClaudeCodeFeature.FileOperations, info.SupportedFeatures);
            Assert.Contains(ClaudeCodeFeature.ProjectManagement, info.SupportedFeatures);
            Assert.Contains(ClaudeCodeFeature.GitIntegration, info.SupportedFeatures);
            Assert.Contains(ClaudeCodeFeature.AdvancedCodeAnalysis, info.SupportedFeatures);
            Assert.Contains(ClaudeCodeFeature.MultiFileEditing, info.SupportedFeatures);
        }

        [Fact]
        public void SetDetected_WithLowVersion_ShouldSetBasicCompatibility()
        {
            // Arrange
            var info = new ClaudeCodeInfo();
            var version = "1.0.5";
            var executablePath = @"C:\claude.exe";

            // Act
            info.SetDetected(version, executablePath);

            // Assert
            Assert.Equal(DetectionStatus.Detected, info.Status);
            Assert.Equal(CompatibilityLevel.BasicCompatible, info.Compatibility);
            Assert.False(info.IsRecommendedVersion);

            // 验证基础功能支持
            Assert.Contains(ClaudeCodeFeature.BasicChat, info.SupportedFeatures);
            Assert.Contains(ClaudeCodeFeature.FileOperations, info.SupportedFeatures);
            Assert.DoesNotContain(ClaudeCodeFeature.AdvancedCodeAnalysis, info.SupportedFeatures);
        }

        [Fact]
        public void SetFailed_WithError_ShouldUpdateStatusAndErrorInfo()
        {
            // Arrange
            var info = new ClaudeCodeInfo();
            var errorMessage = "Claude CLI not found in PATH";
            var exception = new System.IO.FileNotFoundException("claude.exe not found");

            // Act
            info.SetFailed(errorMessage, exception);

            // Assert
            Assert.Equal(DetectionStatus.Failed, info.Status);
            Assert.Equal(errorMessage, info.ErrorMessage);
            Assert.Equal(exception, info.Exception);
            Assert.True(info.LastDetectionTime > DateTime.MinValue);
        }

        [Fact]
        public void SetIncompatible_WithOldVersion_ShouldSetIncompatibleStatus()
        {
            // Arrange
            var info = new ClaudeCodeInfo();
            var version = "0.9.0";
            var reason = "Version too old";

            // Act
            info.SetIncompatible(version, reason);

            // Assert
            Assert.Equal(DetectionStatus.IncompatibleVersion, info.Status);
            Assert.Equal(version, info.Version);
            Assert.Equal(reason, info.ErrorMessage);
            Assert.Equal(CompatibilityLevel.Incompatible, info.Compatibility);
            Assert.NotNull(info.ParsedVersion);
            Assert.Equal(new Version(0, 9, 0), info.ParsedVersion);
        }

        [Fact]
        public void GetCompatibilityDescription_ShouldReturnCorrectDescriptions()
        {
            // Arrange
            var info = new ClaudeCodeInfo();

            // Test FullyCompatible
            info.SetDetected("1.3.0", @"C:\claude.exe");
            Assert.Contains("完全兼容", info.GetCompatibilityDescription());

            // Test BasicCompatible
            info.SetDetected("1.1.0", @"C:\claude.exe");
            Assert.Contains("基本兼容", info.GetCompatibilityDescription());

            // Test Incompatible
            info.SetIncompatible("0.8.0", "Version too old");
            Assert.Contains("版本过低", info.GetCompatibilityDescription());

            // Test Unknown
            var newInfo = new ClaudeCodeInfo();
            Assert.Contains("未知兼容性", newInfo.GetCompatibilityDescription());
        }

        [Fact]
        public void GetFeatureSummary_ShouldReturnCorrectSummary()
        {
            // Arrange
            var info = new ClaudeCodeInfo();

            // Test empty features
            Assert.Equal("无支持的功能", info.GetFeatureSummary());

            // Test with features
            info.SetDetected("1.2.0", @"C:\claude.exe");
            var summary = info.GetFeatureSummary();
            Assert.Contains("基础对话", summary);
            Assert.Contains("文件操作", summary);
            Assert.Contains("项目管理", summary);
        }

        [Fact]
        public void ToString_ShouldReturnCorrectStringRepresentation()
        {
            // Arrange
            var info = new ClaudeCodeInfo();

            // Test NotDetected
            Assert.Equal("Claude Code CLI 未检测到", info.ToString());

            // Test Failed
            info.SetFailed("Not found");
            Assert.Contains("检测失败", info.ToString());

            // Test Detected
            info.SetDetected("1.2.0", @"C:\claude.exe");
            var str = info.ToString();
            Assert.Contains("Claude Code CLI 1.2.0", str);
            Assert.Contains("完全兼容", str);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void SetDetected_WithInvalidVersion_ShouldThrowException(string invalidVersion)
        {
            // Arrange
            var info = new ClaudeCodeInfo();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                info.SetDetected(invalidVersion, @"C:\claude.exe"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void SetDetected_WithInvalidExecutablePath_ShouldThrowException(string invalidPath)
        {
            // Arrange
            var info = new ClaudeCodeInfo();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                info.SetDetected("1.0.0", invalidPath));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void SetFailed_WithInvalidErrorMessage_ShouldThrowException(string invalidMessage)
        {
            // Arrange
            var info = new ClaudeCodeInfo();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                info.SetFailed(invalidMessage));
        }

        [Theory]
        [InlineData("1.0.0", 1, 0, 0)]
        [InlineData("1.2.5", 1, 2, 5)]
        [InlineData("2.1.0.45", 2, 1, 0)]
        [InlineData("v1.3.2", 1, 3, 2)]
        public void TryParseVersion_WithValidVersions_ShouldParseCorrectly(string versionString, int major, int minor, int build)
        {
            // Arrange
            var info = new ClaudeCodeInfo();

            // Act
            info.SetDetected(versionString, @"C:\claude.exe");

            // Assert
            Assert.NotNull(info.ParsedVersion);
            Assert.Equal(major, info.ParsedVersion.Major);
            Assert.Equal(minor, info.ParsedVersion.Minor);
            Assert.Equal(build, info.ParsedVersion.Build);
        }

        [Theory]
        [InlineData("invalid")]
        [InlineData("abc.def.ghi")]
        [InlineData("")]
        public void TryParseVersion_WithInvalidVersions_ShouldHandleGracefully(string invalidVersion)
        {
            // Arrange
            var info = new ClaudeCodeInfo();

            // Act
            info.SetDetected(invalidVersion, @"C:\claude.exe");

            // Assert
            // 应该仍然设置为检测成功，但版本解析失败
            Assert.Equal(DetectionStatus.Detected, info.Status);
            Assert.Equal(CompatibilityLevel.Unknown, info.Compatibility);
        }
    }

    /// <summary>
    /// 性能指标测试
    /// </summary>
    public class PerformanceMetricsTests
    {
        [Fact]
        public void PerformanceMetrics_Initialization_ShouldSetDefaultValues()
        {
            // Arrange & Act
            var metrics = new PerformanceMetrics();

            // Assert
            Assert.Equal(0, metrics.StartupTimeMs);
            Assert.Equal(0, metrics.ResponseTimeMs);
            Assert.Equal(0, metrics.MemoryUsageMB);
            Assert.Equal(0, metrics.CpuUsagePercent);
            Assert.True(metrics.LastMeasuredTime > DateTime.MinValue);
        }

        [Fact]
        public void ToString_ShouldReturnFormattedString()
        {
            // Arrange
            var metrics = new PerformanceMetrics
            {
                StartupTimeMs = 1500,
                ResponseTimeMs = 250,
                MemoryUsageMB = 128.5,
                CpuUsagePercent = 15.3
            };

            // Act
            var result = metrics.ToString();

            // Assert
            Assert.Contains("启动: 1500ms", result);
            Assert.Contains("响应: 250ms", result);
            Assert.Contains("内存: 128.5MB", result);
            Assert.Contains("CPU: 15.3%", result);
        }
    }

    /// <summary>
    /// 枚举测试
    /// </summary>
    public class EnumsTests
    {
        [Fact]
        public void CompatibilityLevel_ShouldHaveExpectedValues()
        {
            // Arrange & Act & Assert
            Assert.True(Enum.IsDefined(typeof(CompatibilityLevel), CompatibilityLevel.Unknown));
            Assert.True(Enum.IsDefined(typeof(CompatibilityLevel), CompatibilityLevel.Incompatible));
            Assert.True(Enum.IsDefined(typeof(CompatibilityLevel), CompatibilityLevel.BasicCompatible));
            Assert.True(Enum.IsDefined(typeof(CompatibilityLevel), CompatibilityLevel.FullyCompatible));
        }

        [Fact]
        public void ClaudeCodeFeature_ShouldHaveExpectedValues()
        {
            // Arrange & Act & Assert
            var features = Enum.GetValues<ClaudeCodeFeature>();
            Assert.Contains(ClaudeCodeFeature.BasicChat, features);
            Assert.Contains(ClaudeCodeFeature.FileOperations, features);
            Assert.Contains(ClaudeCodeFeature.ProjectManagement, features);
            Assert.Contains(ClaudeCodeFeature.GitIntegration, features);
            Assert.Contains(ClaudeCodeFeature.AdvancedCodeAnalysis, features);
            Assert.Contains(ClaudeCodeFeature.MultiFileEditing, features);
            Assert.Contains(ClaudeCodeFeature.RealTimeCollaboration, features);
            Assert.Contains(ClaudeCodeFeature.ExtensionSupport, features);
        }

        [Fact]
        public void AuthenticationStatus_ShouldHaveExpectedValues()
        {
            // Arrange & Act & Assert
            var statuses = Enum.GetValues<AuthenticationStatus>();
            Assert.Contains(AuthenticationStatus.Unknown, statuses);
            Assert.Contains(AuthenticationStatus.NotAuthenticated, statuses);
            Assert.Contains(AuthenticationStatus.Authenticated, statuses);
            Assert.Contains(AuthenticationStatus.Expired, statuses);
            Assert.Contains(AuthenticationStatus.Error, statuses);
        }
    }
}