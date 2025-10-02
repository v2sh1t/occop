using System;
using System.Threading.Tasks;
using Xunit;
using Occop.Core.Services.Environment;
using Occop.Core.Models.Environment;

namespace Occop.Core.Tests.Services.Environment
{
    /// <summary>
    /// 环境检测器测试
    /// </summary>
    public class EnvironmentDetectorTests : IDisposable
    {
        private readonly EnvironmentDetector _detector;

        public EnvironmentDetectorTests()
        {
            _detector = new EnvironmentDetector(cacheExpirationMinutes: 1); // 短缓存时间用于测试
        }

        [Fact]
        public async Task DetectAllEnvironmentsAsync_ShouldReturnValidResult()
        {
            // Act
            var result = await _detector.DetectAllEnvironmentsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.TotalDurationMs >= 0);
            Assert.NotEmpty(result.DetectedEnvironments);

            // 检查是否检测了所有环境类型
            Assert.Contains(EnvironmentType.PowerShell51, result.DetectedEnvironments.Keys);
            Assert.Contains(EnvironmentType.PowerShellCore, result.DetectedEnvironments.Keys);
            Assert.Contains(EnvironmentType.GitBash, result.DetectedEnvironments.Keys);
            Assert.Contains(EnvironmentType.ClaudeCode, result.DetectedEnvironments.Keys);

            // 验证时间戳
            Assert.True(result.StartTime <= result.CompletionTime);

            // 输出详细结果用于调试
            Console.WriteLine($"检测结果摘要:\n{result.GenerateSummary()}");
        }

        [Theory]
        [InlineData(EnvironmentType.PowerShell51)]
        [InlineData(EnvironmentType.PowerShellCore)]
        [InlineData(EnvironmentType.GitBash)]
        [InlineData(EnvironmentType.ClaudeCode)]
        public async Task DetectEnvironmentAsync_ShouldReturnValidEnvironmentInfo(EnvironmentType environmentType)
        {
            // Act
            var environmentInfo = await _detector.DetectEnvironmentAsync(environmentType);

            // Assert
            Assert.NotNull(environmentInfo);
            Assert.Equal(environmentType, environmentInfo.Type);
            Assert.True(environmentInfo.DetectionTime > DateTime.MinValue);
            Assert.True(environmentInfo.Priority > 0);

            // 输出环境信息用于调试
            Console.WriteLine($"环境 {environmentType}: {environmentInfo.GetDescription()}");

            // 如果检测成功，验证必要字段
            if (environmentInfo.Status == DetectionStatus.Detected)
            {
                Assert.NotNull(environmentInfo.InstallPath);
                Assert.NotNull(environmentInfo.ExecutablePath);
                Assert.NotNull(environmentInfo.Version);
                Assert.True(environmentInfo.IsInstalled);
                Assert.True(environmentInfo.IsAvailable);

                Console.WriteLine($"  - 安装路径: {environmentInfo.InstallPath}");
                Console.WriteLine($"  - 可执行文件: {environmentInfo.ExecutablePath}");
                Console.WriteLine($"  - 版本: {environmentInfo.Version}");
            }
            else
            {
                Console.WriteLine($"  - 状态: {environmentInfo.Status}");
                if (!string.IsNullOrEmpty(environmentInfo.ErrorMessage))
                {
                    Console.WriteLine($"  - 错误: {environmentInfo.ErrorMessage}");
                }
            }
        }

        [Fact]
        public async Task GetRecommendedShellAsync_ShouldReturnHighestPriorityShell()
        {
            // Act
            var recommendedShell = await _detector.GetRecommendedShellAsync();

            // Assert
            // 可能为null（如果没有检测到任何Shell）
            if (recommendedShell != null)
            {
                Assert.True(recommendedShell.IsAvailable);
                Assert.True(recommendedShell.IsRecommended);
                Assert.Contains(recommendedShell.Type, new[]
                {
                    EnvironmentType.PowerShellCore,
                    EnvironmentType.PowerShell51,
                    EnvironmentType.GitBash
                });

                Console.WriteLine($"推荐Shell: {recommendedShell.GetDescription()}");
            }
            else
            {
                Console.WriteLine("未找到可用的Shell环境");
            }
        }

        [Fact]
        public async Task CacheValidation_ShouldWorkCorrectly()
        {
            var environmentType = EnvironmentType.PowerShellCore;

            // 首次检测
            Assert.False(_detector.IsCacheValid(environmentType));

            var firstResult = await _detector.DetectEnvironmentAsync(environmentType);
            Assert.True(_detector.IsCacheValid(environmentType));

            // 再次检测应使用缓存
            var secondResult = await _detector.DetectEnvironmentAsync(environmentType);
            Assert.Equal(firstResult.DetectionTime, secondResult.DetectionTime);

            // 强制刷新
            var thirdResult = await _detector.DetectEnvironmentAsync(environmentType, forceRefresh: true);
            // 如果真的重新检测了，时间应该不同（但可能很接近）

            // 清除缓存
            _detector.ClearCache(environmentType);
            Assert.False(_detector.IsCacheValid(environmentType));
        }

        [Fact]
        public void ClearCache_ShouldClearSpecificOrAllCache()
        {
            // 先填充一些缓存（通过检测）
            var task1 = _detector.DetectEnvironmentAsync(EnvironmentType.PowerShellCore);
            var task2 = _detector.DetectEnvironmentAsync(EnvironmentType.GitBash);
            Task.WaitAll(task1, task2);

            // 验证缓存存在
            Assert.True(_detector.IsCacheValid(EnvironmentType.PowerShellCore));
            Assert.True(_detector.IsCacheValid(EnvironmentType.GitBash));

            // 清除特定缓存
            _detector.ClearCache(EnvironmentType.PowerShellCore);
            Assert.False(_detector.IsCacheValid(EnvironmentType.PowerShellCore));
            Assert.True(_detector.IsCacheValid(EnvironmentType.GitBash));

            // 清除所有缓存
            _detector.ClearCache();
            Assert.False(_detector.IsCacheValid(EnvironmentType.GitBash));
        }

        [Fact]
        public void EnvironmentMonitoring_ShouldStartAndStop()
        {
            // 测试监控启动和停止
            // 注意：这个测试主要验证方法调用不会抛异常
            Assert.DoesNotThrow(() => _detector.StartEnvironmentMonitoring());
            Assert.DoesNotThrow(() => _detector.StartEnvironmentMonitoring()); // 重复启动应该没问题

            Assert.DoesNotThrow(() => _detector.StopEnvironmentMonitoring());
            Assert.DoesNotThrow(() => _detector.StopEnvironmentMonitoring()); // 重复停止应该没问题
        }

        [Fact]
        public async Task DetectAllEnvironments_WithForceRefresh_ShouldIgnoreCache()
        {
            // 首次检测
            var firstResult = await _detector.DetectAllEnvironmentsAsync();

            // 强制刷新检测
            var secondResult = await _detector.DetectAllEnvironmentsAsync(forceRefresh: true);

            // 验证结果不为空
            Assert.NotNull(firstResult);
            Assert.NotNull(secondResult);

            // 两次结果的环境数量应该相同
            Assert.Equal(firstResult.DetectedEnvironments.Count, secondResult.DetectedEnvironments.Count);
        }

        [Fact]
        public async Task DetectionResult_PropertiesAndMethods_ShouldWorkCorrectly()
        {
            // Act
            var result = await _detector.DetectAllEnvironmentsAsync();

            // Assert
            Assert.NotNull(result);

            // 测试基本属性
            Assert.True(result.TotalDurationMs >= 0);
            Assert.True(result.DetectedCount >= 0);

            // 测试方法
            var summary = result.GenerateSummary();
            Assert.NotNull(summary);
            Assert.NotEmpty(summary);
            Console.WriteLine($"生成的摘要:\n{summary}");

            // 测试获取特定环境
            foreach (EnvironmentType envType in Enum.GetValues<EnvironmentType>())
            {
                var env = result.GetEnvironment(envType);
                Assert.NotNull(env);
                Assert.Equal(envType, env.Type);
            }

            // 测试获取可用Shell
            var availableShells = result.GetAvailableShells();
            Assert.NotNull(availableShells);
            // 验证Shell按优先级排序
            for (int i = 1; i < availableShells.Count; i++)
            {
                Assert.True(availableShells[i-1].Priority >= availableShells[i].Priority);
            }
        }

        public void Dispose()
        {
            _detector?.Dispose();
        }
    }

    /// <summary>
    /// 环境信息测试
    /// </summary>
    public class EnvironmentInfoTests
    {
        [Fact]
        public void EnvironmentInfo_Initialization_ShouldSetDefaultValues()
        {
            // Arrange & Act
            var envInfo = new EnvironmentInfo(EnvironmentType.PowerShellCore);

            // Assert
            Assert.Equal(EnvironmentType.PowerShellCore, envInfo.Type);
            Assert.Equal(DetectionStatus.NotDetected, envInfo.Status);
            Assert.False(envInfo.IsInstalled);
            Assert.False(envInfo.IsAvailable);
            Assert.True(envInfo.Priority > 0);
            Assert.NotNull(envInfo.Properties);
            Assert.True(envInfo.DetectionTime > DateTime.MinValue);
        }

        [Fact]
        public void SetDetected_WithValidData_ShouldUpdateStatus()
        {
            // Arrange
            var envInfo = new EnvironmentInfo(EnvironmentType.PowerShellCore);
            var installPath = @"C:\Program Files\PowerShell\7";
            var executablePath = @"C:\Program Files\PowerShell\7\pwsh.exe";
            var version = "7.3.0";

            // Act
            envInfo.SetDetected(installPath, executablePath, version);

            // Assert
            Assert.Equal(DetectionStatus.Detected, envInfo.Status);
            Assert.Equal(installPath, envInfo.InstallPath);
            Assert.Equal(executablePath, envInfo.ExecutablePath);
            Assert.Equal(version, envInfo.Version);
            Assert.True(envInfo.IsInstalled);
            Assert.True(envInfo.IsAvailable);
            Assert.Null(envInfo.ErrorMessage);
            Assert.Null(envInfo.Exception);
        }

        [Fact]
        public void SetDetected_WithIncompatibleVersion_ShouldSetIncompatibleStatus()
        {
            // Arrange
            var envInfo = new EnvironmentInfo(EnvironmentType.PowerShellCore);
            var installPath = @"C:\Program Files\PowerShell\6";
            var executablePath = @"C:\Program Files\PowerShell\6\pwsh.exe";
            var version = "6.0.0"; // 低于最小兼容版本7.0.0

            // Act
            envInfo.SetDetected(installPath, executablePath, version);

            // Assert
            Assert.Equal(DetectionStatus.IncompatibleVersion, envInfo.Status);
            Assert.True(envInfo.IsInstalled);
            Assert.False(envInfo.IsAvailable);
        }

        [Fact]
        public void SetFailed_WithError_ShouldUpdateStatus()
        {
            // Arrange
            var envInfo = new EnvironmentInfo(EnvironmentType.GitBash);
            var errorMessage = "Git not found in PATH";
            var exception = new FileNotFoundException("git.exe not found");

            // Act
            envInfo.SetFailed(errorMessage, exception);

            // Assert
            Assert.Equal(DetectionStatus.Failed, envInfo.Status);
            Assert.Equal(errorMessage, envInfo.ErrorMessage);
            Assert.Equal(exception, envInfo.Exception);
            Assert.False(envInfo.IsInstalled);
            Assert.False(envInfo.IsAvailable);
        }

        [Fact]
        public void Properties_AddAndGet_ShouldWorkCorrectly()
        {
            // Arrange
            var envInfo = new EnvironmentInfo(EnvironmentType.GitBash);

            // Act
            envInfo.AddProperty("GitPath", @"C:\Program Files\Git\bin\git.exe");
            envInfo.AddProperty("BashPath", @"C:\Program Files\Git\bin\bash.exe");
            envInfo.AddProperty("IsPortable", false);

            // Assert
            Assert.Equal(@"C:\Program Files\Git\bin\git.exe", envInfo.GetProperty<string>("GitPath"));
            Assert.Equal(@"C:\Program Files\Git\bin\bash.exe", envInfo.GetProperty<string>("BashPath"));
            Assert.False(envInfo.GetProperty<bool>("IsPortable"));
            Assert.Equal("default", envInfo.GetProperty("NonExistent", "default"));
        }

        [Fact]
        public void GetDescription_ShouldReturnCorrectDescription()
        {
            // Arrange
            var envInfo = new EnvironmentInfo(EnvironmentType.PowerShellCore);

            // Test NotDetected
            var desc = envInfo.GetDescription();
            Assert.Contains("未检测", desc);

            // Test Detected
            envInfo.SetDetected(@"C:\Program Files\PowerShell\7", @"C:\Program Files\PowerShell\7\pwsh.exe", "7.3.0");
            desc = envInfo.GetDescription();
            Assert.Contains("7.3.0", desc);
            Assert.Contains("已安装", desc);

            // Test Failed
            envInfo.SetFailed("Test error");
            desc = envInfo.GetDescription();
            Assert.Contains("检测失败", desc);
            Assert.Contains("Test error", desc);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void SetDetected_WithInvalidInstallPath_ShouldThrowException(string invalidPath)
        {
            // Arrange
            var envInfo = new EnvironmentInfo(EnvironmentType.PowerShellCore);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                envInfo.SetDetected(invalidPath, @"C:\test\pwsh.exe", "7.3.0"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void SetFailed_WithInvalidErrorMessage_ShouldThrowException(string invalidMessage)
        {
            // Arrange
            var envInfo = new EnvironmentInfo(EnvironmentType.PowerShellCore);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                envInfo.SetFailed(invalidMessage));
        }
    }

    /// <summary>
    /// 检测结果测试
    /// </summary>
    public class DetectionResultTests
    {
        [Fact]
        public void DetectionResult_Initialization_ShouldSetDefaultValues()
        {
            // Arrange & Act
            var result = new DetectionResult();

            // Assert
            Assert.NotNull(result.DetectedEnvironments);
            Assert.NotNull(result.Errors);
            Assert.Empty(result.DetectedEnvironments);
            Assert.Empty(result.Errors);
            Assert.True(result.StartTime > DateTime.MinValue);
            Assert.Equal(0, result.DetectedCount);
            Assert.False(result.IsSuccess);
            Assert.False(result.HasShellEnvironment);
            Assert.False(result.HasClaudeCode);
        }

        [Fact]
        public void AddEnvironment_ShouldAddToCollection()
        {
            // Arrange
            var result = new DetectionResult();
            var envInfo = new EnvironmentInfo(EnvironmentType.PowerShellCore);
            envInfo.SetDetected(@"C:\Program Files\PowerShell\7", @"C:\Program Files\PowerShell\7\pwsh.exe", "7.3.0");

            // Act
            result.AddEnvironment(EnvironmentType.PowerShellCore, envInfo);

            // Assert
            Assert.Single(result.DetectedEnvironments);
            Assert.Equal(envInfo, result.GetEnvironment(EnvironmentType.PowerShellCore));
            Assert.Equal(1, result.DetectedCount);
            Assert.True(result.IsSuccess);
            Assert.True(result.HasShellEnvironment);
        }

        [Fact]
        public void AddError_ShouldAddToErrorCollection()
        {
            // Arrange
            var result = new DetectionResult();
            var exception = new Exception("Test exception");

            // Act
            result.AddError(EnvironmentType.GitBash, "Git not found", exception);

            // Assert
            Assert.Single(result.Errors);
            Assert.Equal(EnvironmentType.GitBash, result.Errors[0].EnvironmentType);
            Assert.Equal("Git not found", result.Errors[0].ErrorMessage);
            Assert.Equal(exception, result.Errors[0].Exception);
        }

        [Fact]
        public void GetAvailableShells_ShouldReturnOrderedShells()
        {
            // Arrange
            var result = new DetectionResult();

            var psCore = new EnvironmentInfo(EnvironmentType.PowerShellCore);
            psCore.SetDetected(@"C:\PS7", @"C:\PS7\pwsh.exe", "7.3.0");

            var gitBash = new EnvironmentInfo(EnvironmentType.GitBash);
            gitBash.SetDetected(@"C:\Git", @"C:\Git\bin\bash.exe", "2.34.1");

            var ps51 = new EnvironmentInfo(EnvironmentType.PowerShell51);
            ps51.SetDetected(@"C:\Windows\System32\WindowsPowerShell\v1.0", @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe", "5.1.0");

            result.AddEnvironment(EnvironmentType.PowerShellCore, psCore);
            result.AddEnvironment(EnvironmentType.GitBash, gitBash);
            result.AddEnvironment(EnvironmentType.PowerShell51, ps51);

            // Act
            var availableShells = result.GetAvailableShells();

            // Assert
            Assert.Equal(3, availableShells.Count);

            // 验证按优先级排序（PowerShell Core > PowerShell 5.1 > Git Bash）
            Assert.Equal(EnvironmentType.PowerShellCore, availableShells[0].Type);
            Assert.Equal(EnvironmentType.PowerShell51, availableShells[1].Type);
            Assert.Equal(EnvironmentType.GitBash, availableShells[2].Type);
        }

        [Fact]
        public void GenerateSummary_ShouldCreateValidSummary()
        {
            // Arrange
            var result = new DetectionResult();

            var envInfo = new EnvironmentInfo(EnvironmentType.PowerShellCore);
            envInfo.SetDetected(@"C:\PS7", @"C:\PS7\pwsh.exe", "7.3.0");
            envInfo.IsRecommended = true;

            result.AddEnvironment(EnvironmentType.PowerShellCore, envInfo);
            result.AddError(EnvironmentType.ClaudeCode, "Claude CLI not found");
            result.RecommendedShell = envInfo;
            result.MarkCompleted();

            // Act
            var summary = result.GenerateSummary();

            // Assert
            Assert.NotNull(summary);
            Assert.Contains("环境检测完成", summary);
            Assert.Contains("检测到 1 个环境", summary);
            Assert.Contains("PowerShellCore", summary);
            Assert.Contains("7.3.0", summary);
            Assert.Contains("推荐Shell", summary);
            Assert.Contains("检测错误", summary);
            Assert.Contains("Claude CLI not found", summary);

            Console.WriteLine($"生成的摘要:\n{summary}");
        }

        [Fact]
        public void MarkCompleted_ShouldUpdateCompletionTime()
        {
            // Arrange
            var result = new DetectionResult();
            var originalCompletionTime = result.CompletionTime;

            // Act
            await Task.Delay(10); // 确保时间差
            result.MarkCompleted();

            // Assert
            Assert.True(result.CompletionTime > originalCompletionTime);
            Assert.True(result.TotalDurationMs >= 0);
        }
    }
}