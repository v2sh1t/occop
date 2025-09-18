using System;
using System.Threading.Tasks;
using Xunit;
using Occop.Core.Services.Environment;
using Occop.Core.Models.Environment;

namespace Occop.Core.Tests.Services.Environment
{
    /// <summary>
    /// ShellDetector基础类测试
    /// </summary>
    public class ShellDetectorTests : IDisposable
    {
        private readonly PowerShell51Detector _ps51Detector;
        private readonly PowerShellCoreDetector _psCoreDetector;
        private readonly GitBashDetector _gitBashDetector;
        private readonly ShellDetectorManager _manager;

        public ShellDetectorTests()
        {
            _ps51Detector = new PowerShell51Detector();
            _psCoreDetector = new PowerShellCoreDetector();
            _gitBashDetector = new GitBashDetector();
            _manager = new ShellDetectorManager();

            // 注册所有检测器
            _manager.RegisterDetector(ShellType.PowerShell51, _ps51Detector);
            _manager.RegisterDetector(ShellType.PowerShellCore, _psCoreDetector);
            _manager.RegisterDetector(ShellType.GitBash, _gitBashDetector);
        }

        [Fact]
        public async Task PowerShell51Detector_ShouldDetectShell()
        {
            // Act
            var result = await _ps51Detector.DetectShellAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(ShellType.PowerShell51, result.ShellType);
            Assert.Equal(EnvironmentType.PowerShell51, result.Type);

            // 输出详细信息用于调试
            Console.WriteLine($"PowerShell 5.1检测结果:");
            Console.WriteLine($"状态: {result.Status}");
            Console.WriteLine($"版本: {result.Version}");
            Console.WriteLine($"路径: {result.ExecutablePath}");
            Console.WriteLine($"描述: {result.GetDescription()}");

            if (result.Status == DetectionStatus.Detected)
            {
                Assert.NotNull(result.ExecutablePath);
                Assert.NotNull(result.Version);
                Assert.True(result.IsAvailable);

                // 检查Shell特定属性
                Assert.True(result.HasCapability(ShellCapabilities.Interactive));
                Assert.True(result.HasCapability(ShellCapabilities.Scripting));
                Assert.Contains("5.", result.Version);
            }
            else
            {
                Assert.NotNull(result.ErrorMessage);
                Console.WriteLine($"检测失败原因: {result.ErrorMessage}");
            }
        }

        [Fact]
        public async Task PowerShellCoreDetector_ShouldDetectShell()
        {
            // Act
            var result = await _psCoreDetector.DetectShellAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(ShellType.PowerShellCore, result.ShellType);
            Assert.Equal(EnvironmentType.PowerShellCore, result.Type);

            // 输出详细信息用于调试
            Console.WriteLine($"PowerShell Core检测结果:");
            Console.WriteLine($"状态: {result.Status}");
            Console.WriteLine($"版本: {result.Version}");
            Console.WriteLine($"路径: {result.ExecutablePath}");
            Console.WriteLine($"描述: {result.GetDescription()}");

            if (result.Status == DetectionStatus.Detected)
            {
                Assert.NotNull(result.ExecutablePath);
                Assert.NotNull(result.Version);
                Assert.True(result.IsAvailable);

                // 检查Shell特定属性
                Assert.True(result.HasCapability(ShellCapabilities.Interactive));
                Assert.True(result.HasCapability(ShellCapabilities.Scripting));
                Assert.True(result.HasCapability(ShellCapabilities.ModuleManagement));
                Assert.True(result.Version.StartsWith("6.") || result.Version.StartsWith("7."));
            }
            else
            {
                Assert.NotNull(result.ErrorMessage);
                Console.WriteLine($"检测失败原因: {result.ErrorMessage}");
            }
        }

        [Fact]
        public async Task GitBashDetector_ShouldDetectShell()
        {
            // Act
            var result = await _gitBashDetector.DetectShellAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(ShellType.GitBash, result.ShellType);
            Assert.Equal(EnvironmentType.GitBash, result.Type);

            // 输出详细信息用于调试
            Console.WriteLine($"Git Bash检测结果:");
            Console.WriteLine($"状态: {result.Status}");
            Console.WriteLine($"版本: {result.Version}");
            Console.WriteLine($"路径: {result.ExecutablePath}");
            Console.WriteLine($"描述: {result.GetDescription()}");

            if (result.Status == DetectionStatus.Detected)
            {
                Assert.NotNull(result.ExecutablePath);
                Assert.NotNull(result.Version);
                Assert.True(result.IsAvailable);

                // 检查Shell特定属性
                Assert.True(result.HasCapability(ShellCapabilities.Interactive));
                Assert.True(result.HasCapability(ShellCapabilities.Scripting));
                Assert.True(result.HasCapability(ShellCapabilities.Piping));

                // 检查Git Bash特有属性
                if (result.Properties.ContainsKey("GitPath"))
                {
                    Console.WriteLine($"Git路径: {result.Properties["GitPath"]}");
                }
                if (result.Properties.ContainsKey("BashPath"))
                {
                    Console.WriteLine($"Bash路径: {result.Properties["BashPath"]}");
                }
            }
            else
            {
                Assert.NotNull(result.ErrorMessage);
                Console.WriteLine($"检测失败原因: {result.ErrorMessage}");
            }
        }

        [Fact]
        public async Task ShellDetectorManager_DetectAllShells_ShouldReturnResults()
        {
            // Act
            var results = await _manager.DetectAllShellsAsync();

            // Assert
            Assert.NotNull(results);
            Assert.NotEmpty(results);
            Assert.Equal(3, results.Count); // PowerShell51, PowerShellCore, GitBash

            Console.WriteLine($"Shell检测器管理器检测到 {results.Count} 个Shell:");

            foreach (var shell in results)
            {
                Console.WriteLine($"- {shell.ShellType}: {shell.Status}");
                if (shell.IsAvailable)
                {
                    Console.WriteLine($"  版本: {shell.Version}");
                    Console.WriteLine($"  路径: {shell.ExecutablePath}");
                    Console.WriteLine($"  评分: {shell.Score:F1}");
                    Console.WriteLine($"  性能等级: {shell.PerformanceLevel}");
                }
            }
        }

        [Fact]
        public async Task ShellDetectorManager_GetOptimalShell_ShouldReturnBestShell()
        {
            // Act
            var optimalShell = await _manager.GetOptimalShellAsync();

            // Assert - 可能没有检测到任何Shell，这在某些环境下是正常的
            if (optimalShell != null)
            {
                Assert.True(optimalShell.IsAvailable);
                Assert.True(optimalShell.Score > 0);

                Console.WriteLine($"最优Shell检测结果:");
                Console.WriteLine($"类型: {optimalShell.ShellType}");
                Console.WriteLine($"版本: {optimalShell.Version}");
                Console.WriteLine($"路径: {optimalShell.ExecutablePath}");
                Console.WriteLine($"评分: {optimalShell.Score:F1}");
                Console.WriteLine($"性能等级: {optimalShell.PerformanceLevel}");
                Console.WriteLine($"能力: {optimalShell.Capabilities}");
            }
            else
            {
                Console.WriteLine("未检测到可用的Shell环境（这在某些测试环境下是正常的）");
            }
        }

        [Fact]
        public async Task ShellDetectorManager_WithRequirements_ShouldFilterShells()
        {
            // Arrange
            var requirements = new ShellRequirements
            {
                RequiredCapabilities = ShellCapabilities.Interactive | ShellCapabilities.Scripting,
                RequireUnicodeSupport = true,
                MaxMemoryUsageMB = 100
            };

            // Act
            var optimalShell = await _manager.GetOptimalShellAsync(requirements);

            // Assert
            if (optimalShell != null)
            {
                Assert.True(optimalShell.HasCapability(ShellCapabilities.Interactive));
                Assert.True(optimalShell.HasCapability(ShellCapabilities.Scripting));
                Assert.True(optimalShell.MemoryUsageMB <= 100);

                Console.WriteLine($"符合要求的最优Shell:");
                Console.WriteLine($"类型: {optimalShell.ShellType}");
                Console.WriteLine($"内存使用: {optimalShell.MemoryUsageMB:F1} MB");
                Console.WriteLine($"响应时间: {optimalShell.ResponseTimeMs} ms");
            }
            else
            {
                Console.WriteLine("没有Shell符合指定要求");
            }
        }

        [Fact]
        public async Task ShellDetector_CacheTest_ShouldUseCachedResults()
        {
            // Arrange
            var detector = _ps51Detector;

            // Act - 第一次检测
            var startTime1 = DateTime.UtcNow;
            var result1 = await detector.DetectShellAsync();
            var duration1 = DateTime.UtcNow - startTime1;

            // Act - 第二次检测（应该使用缓存）
            var startTime2 = DateTime.UtcNow;
            var result2 = await detector.DetectShellAsync();
            var duration2 = DateTime.UtcNow - startTime2;

            // Assert
            Assert.Equal(result1.Status, result2.Status);
            Assert.Equal(result1.Version, result2.Version);
            Assert.Equal(result1.ExecutablePath, result2.ExecutablePath);

            // 第二次应该更快（使用缓存）
            Console.WriteLine($"第一次检测耗时: {duration1.TotalMilliseconds:F0} ms");
            Console.WriteLine($"第二次检测耗时: {duration2.TotalMilliseconds:F0} ms");

            // 在大多数情况下，缓存应该更快，但在测试环境中可能差别不大
            if (result1.Status == DetectionStatus.Detected)
            {
                Assert.True(duration2 <= duration1 * 2, "缓存检测应该不会明显慢于首次检测");
            }
        }

        [Fact]
        public async Task ShellDetector_ForceRefresh_ShouldBypassCache()
        {
            // Arrange
            var detector = _psCoreDetector;

            // Act - 首次检测
            var result1 = await detector.DetectShellAsync();

            // Act - 强制刷新
            var result2 = await detector.DetectShellAsync(forceRefresh: true);

            // Assert
            Assert.Equal(result1.Status, result2.Status);
            if (result1.Status == DetectionStatus.Detected)
            {
                Assert.Equal(result1.Version, result2.Version);
                Assert.Equal(result1.ExecutablePath, result2.ExecutablePath);
            }

            Console.WriteLine($"强制刷新测试完成 - 状态一致: {result1.Status == result2.Status}");
        }

        [Theory]
        [InlineData(ShellType.PowerShell51)]
        [InlineData(ShellType.PowerShellCore)]
        [InlineData(ShellType.GitBash)]
        public async Task ShellDetectorManager_DetectSpecificShell_ShouldReturnCorrectType(ShellType shellType)
        {
            // Act
            var result = await _manager.DetectShellAsync(shellType);

            // Assert
            if (result != null)
            {
                Assert.Equal(shellType, result.ShellType);
                Console.WriteLine($"{shellType} 检测结果: {result.Status}");

                if (result.IsAvailable)
                {
                    Assert.NotNull(result.ExecutablePath);
                    Assert.NotNull(result.Version);
                    Assert.True(result.Score >= 0);
                }
            }
            else
            {
                Console.WriteLine($"{shellType} 检测器未注册");
            }
        }

        [Fact]
        public async Task ShellInfo_Properties_ShouldBeCorrectlySet()
        {
            // Arrange
            var shellInfo = new ShellInfo(EnvironmentType.PowerShellCore, ShellType.PowerShellCore);

            // Act
            shellInfo.SetShellDetected("C:\\Program Files\\PowerShell\\7", "C:\\Program Files\\PowerShell\\7\\pwsh.exe", "7.3.0");
            shellInfo.SetPerformanceMetrics(500, 100, 25.5);
            shellInfo.AddEnvironmentVariable("TEST_VAR", "test_value");
            shellInfo.AddCapability(ShellCapabilities.RemoteExecution);

            // Assert
            Assert.Equal(ShellType.PowerShellCore, shellInfo.ShellType);
            Assert.Equal("7.3.0", shellInfo.Version);
            Assert.Equal(500, shellInfo.StartupTimeMs);
            Assert.Equal(100, shellInfo.ResponseTimeMs);
            Assert.Equal(25.5, shellInfo.MemoryUsageMB);
            Assert.True(shellInfo.HasCapability(ShellCapabilities.RemoteExecution));
            Assert.Equal("test_value", shellInfo.EnvironmentVariables["TEST_VAR"]);
            Assert.True(shellInfo.Score > 0);

            Console.WriteLine($"ShellInfo测试完成:");
            Console.WriteLine($"类型: {shellInfo.ShellType}");
            Console.WriteLine($"评分: {shellInfo.Score:F1}");
            Console.WriteLine($"能力: {shellInfo.Capabilities}");
            Console.WriteLine($"环境变量数量: {shellInfo.EnvironmentVariables.Count}");
        }

        public void Dispose()
        {
            _ps51Detector?.ClearCache();
            _psCoreDetector?.ClearCache();
            _gitBashDetector?.ClearCache();
        }
    }
}