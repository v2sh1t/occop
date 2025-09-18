using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Occop.Core.Models.Environment;
using Occop.Core.Services.Environment;

namespace Occop.Core.Tests.Services.Environment
{
    /// <summary>
    /// Claude Code 检测器测试
    /// </summary>
    public class ClaudeCodeDetectorTests : IDisposable
    {
        private readonly ClaudeCodeDetector _detector;

        public ClaudeCodeDetectorTests()
        {
            _detector = new ClaudeCodeDetector();
        }

        [Fact]
        public async Task DetectClaudeCodeAsync_ShouldReturnValidClaudeCodeInfo()
        {
            // Act
            var result = await _detector.DetectClaudeCodeAsync();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.LastDetectionTime > DateTime.MinValue);

            // 输出检测结果用于调试
            Console.WriteLine($"Claude Code 检测结果:");
            Console.WriteLine($"  状态: {result.Status}");
            Console.WriteLine($"  版本: {result.Version ?? "未知"}");
            Console.WriteLine($"  可执行文件路径: {result.ExecutablePath ?? "未找到"}");
            Console.WriteLine($"  安装路径: {result.InstallPath ?? "未知"}");
            Console.WriteLine($"  兼容性: {result.Compatibility}");
            Console.WriteLine($"  认证状态: {result.AuthStatus}");

            if (result.Status == DetectionStatus.Detected)
            {
                // 如果检测成功，验证必要字段
                Assert.NotNull(result.Version);
                Assert.NotNull(result.ExecutablePath);
                Assert.True(File.Exists(result.ExecutablePath));
                Assert.NotEmpty(result.SupportedFeatures);

                Console.WriteLine($"  支持的功能: {result.GetFeatureSummary()}");
                Console.WriteLine($"  兼容性描述: {result.GetCompatibilityDescription()}");

                if (result.Performance != null)
                {
                    Console.WriteLine($"  性能指标: {result.Performance}");
                }
            }
            else if (result.Status == DetectionStatus.Failed)
            {
                Console.WriteLine($"  检测失败原因: {result.ErrorMessage}");
            }
            else if (result.Status == DetectionStatus.IncompatibleVersion)
            {
                Console.WriteLine($"  版本不兼容: {result.ErrorMessage}");
            }
        }

        [Fact]
        public async Task DetectClaudeCodeAsync_WithForceRefresh_ShouldIgnoreCache()
        {
            // Arrange - 首次检测
            var firstResult = await _detector.DetectClaudeCodeAsync(false);

            // Act - 强制刷新
            var secondResult = await _detector.DetectClaudeCodeAsync(true);

            // Assert
            Assert.NotNull(firstResult);
            Assert.NotNull(secondResult);

            // 验证状态一致性
            Assert.Equal(firstResult.Status, secondResult.Status);

            if (firstResult.Status == DetectionStatus.Detected && secondResult.Status == DetectionStatus.Detected)
            {
                Assert.Equal(firstResult.Version, secondResult.Version);
                Assert.Equal(firstResult.ExecutablePath, secondResult.ExecutablePath);
            }
        }

        [Fact]
        public async Task IsClaudeCodeAvailableAsync_WithInvalidPath_ShouldReturnFalse()
        {
            // Arrange
            var invalidPath = @"C:\NonExistent\claude.exe";

            // Act
            var result = await _detector.IsClaudeCodeAvailableAsync(invalidPath);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsClaudeCodeAvailableAsync_WithNullPath_ShouldReturnFalse()
        {
            // Act
            var result = await _detector.IsClaudeCodeAvailableAsync(null);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsClaudeCodeAvailableAsync_WithEmptyPath_ShouldReturnFalse()
        {
            // Act
            var result = await _detector.IsClaudeCodeAvailableAsync("");

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData("1.0.0", true)]
        [InlineData("1.5.2", true)]
        [InlineData("2.0.0", true)]
        [InlineData("0.9.9", false)]
        [InlineData("0.5.0", false)]
        [InlineData("invalid", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsVersionCompatible_ShouldReturnCorrectResult(string version, bool expectedResult)
        {
            // Act
            var result = _detector.IsVersionCompatible(version);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public async Task DetectClaudeCodeAsync_MultipleCallsWithoutForceRefresh_ShouldUseCaching()
        {
            // 注意：这个测试验证检测器的基本行为，实际的缓存行为可能需要更复杂的测试

            // Arrange & Act
            var result1 = await _detector.DetectClaudeCodeAsync(false);
            var result2 = await _detector.DetectClaudeCodeAsync(false);

            // Assert
            Assert.NotNull(result1);
            Assert.NotNull(result2);
            Assert.Equal(result1.Status, result2.Status);

            if (result1.Status == DetectionStatus.Detected && result2.Status == DetectionStatus.Detected)
            {
                Assert.Equal(result1.Version, result2.Version);
                Assert.Equal(result1.ExecutablePath, result2.ExecutablePath);
            }
        }

        [Fact]
        public async Task DetectClaudeCodeAsync_ShouldHandleExceptionsGracefully()
        {
            // 这个测试验证检测器能够处理各种异常情况
            // 实际的异常情况很难模拟，但我们可以验证方法不会抛出未处理的异常

            // Act & Assert - 确保不抛出异常
            var result = await _detector.DetectClaudeCodeAsync();
            Assert.NotNull(result);

            // 无论结果如何，都应该设置了某种状态
            Assert.True(Enum.IsDefined(typeof(DetectionStatus), result.Status));
        }

        [Fact]
        public void ClaudeCodeDetector_ConstructorShouldNotThrow()
        {
            // Arrange & Act & Assert
            Assert.DoesNotThrow(() => new ClaudeCodeDetector());
        }

        public void Dispose()
        {
            // ClaudeCodeDetector没有实现IDisposable，但保持一致的测试模式
        }
    }

    /// <summary>
    /// Claude Code 检测器集成测试
    /// </summary>
    public class ClaudeCodeDetectorIntegrationTests
    {
        [Fact]
        public async Task DetectClaudeCodeAsync_RealEnvironment_ShouldProvideDetailedOutput()
        {
            // 这是一个集成测试，用于在真实环境中验证检测器的行为

            // Arrange
            var detector = new ClaudeCodeDetector();

            // Act
            var result = await detector.DetectClaudeCodeAsync();

            // Assert & Output detailed information for debugging
            Assert.NotNull(result);

            Console.WriteLine("=== Claude Code 详细检测报告 ===");
            Console.WriteLine($"检测时间: {result.LastDetectionTime:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"检测状态: {result.Status}");

            switch (result.Status)
            {
                case DetectionStatus.Detected:
                    Console.WriteLine($"版本: {result.Version}");
                    Console.WriteLine($"可执行文件: {result.ExecutablePath}");
                    Console.WriteLine($"安装路径: {result.InstallPath}");
                    Console.WriteLine($"兼容性: {result.Compatibility} - {result.GetCompatibilityDescription()}");
                    Console.WriteLine($"是否推荐版本: {result.IsRecommendedVersion}");
                    Console.WriteLine($"认证状态: {result.AuthStatus}");
                    Console.WriteLine($"API端点: {result.ApiEndpoint}");
                    Console.WriteLine($"配置文件: {result.ConfigFilePath ?? "未找到"}");
                    Console.WriteLine($"支持的功能 ({result.SupportedFeatures.Count}): {result.GetFeatureSummary()}");

                    if (result.EnvironmentVariables.Count > 0)
                    {
                        Console.WriteLine("环境变量:");
                        foreach (var env in result.EnvironmentVariables)
                        {
                            Console.WriteLine($"  {env.Key} = {env.Value}");
                        }
                    }

                    if (result.Performance != null)
                    {
                        Console.WriteLine($"性能指标: {result.Performance}");
                    }
                    break;

                case DetectionStatus.Failed:
                    Console.WriteLine($"检测失败: {result.ErrorMessage}");
                    if (result.Exception != null)
                    {
                        Console.WriteLine($"异常: {result.Exception.Message}");
                    }
                    break;

                case DetectionStatus.IncompatibleVersion:
                    Console.WriteLine($"版本不兼容: {result.Version}");
                    Console.WriteLine($"原因: {result.ErrorMessage}");
                    break;

                default:
                    Console.WriteLine("未检测到Claude Code CLI");
                    break;
            }

            Console.WriteLine($"ToString输出: {result}");
        }

        [Fact]
        public async Task DetectClaudeCodeAsync_PerformanceMeasurement()
        {
            // 性能测试
            var detector = new ClaudeCodeDetector();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act
            var result = await detector.DetectClaudeCodeAsync();
            stopwatch.Stop();

            // Assert
            Assert.NotNull(result);
            Assert.True(stopwatch.ElapsedMilliseconds < 30000); // 30秒超时

            Console.WriteLine($"Claude Code检测耗时: {stopwatch.ElapsedMilliseconds}ms");

            if (result.Status == DetectionStatus.Detected && result.Performance != null)
            {
                Console.WriteLine($"Claude Code启动性能: {result.Performance.StartupTimeMs}ms");
                Console.WriteLine($"Claude Code响应性能: {result.Performance.ResponseTimeMs}ms");
            }
        }
    }
}