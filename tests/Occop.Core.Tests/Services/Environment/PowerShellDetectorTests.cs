using System;
using System.Threading.Tasks;
using Xunit;
using Occop.Core.Services.Environment;
using Occop.Core.Models.Environment;

namespace Occop.Core.Tests.Services.Environment
{
    /// <summary>
    /// PowerShell检测器专门测试
    /// </summary>
    public class PowerShellDetectorTests : IDisposable
    {
        private readonly PowerShell51Detector _ps51Detector;
        private readonly PowerShellCoreDetector _psCoreDetector;

        public PowerShellDetectorTests()
        {
            _ps51Detector = new PowerShell51Detector();
            _psCoreDetector = new PowerShellCoreDetector();
        }

        [Fact]
        public async Task PowerShell51Detector_DetectShell_ShouldProvideDetailedInfo()
        {
            // Act
            var result = await _ps51Detector.DetectShellAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(EnvironmentType.PowerShell51, result.Type);
            Assert.Equal(ShellType.PowerShell51, result.ShellType);

            Console.WriteLine("=== PowerShell 5.1 详细检测结果 ===");
            Console.WriteLine($"检测状态: {result.Status}");
            Console.WriteLine($"是否可用: {result.IsAvailable}");
            Console.WriteLine($"安装路径: {result.InstallPath ?? "未检测到"}");
            Console.WriteLine($"可执行文件: {result.ExecutablePath ?? "未检测到"}");
            Console.WriteLine($"版本信息: {result.Version ?? "未知"}");
            Console.WriteLine($"配置路径: {result.ConfigurationPath ?? "未找到"}");

            if (result.IsAvailable)
            {
                // 验证PowerShell 5.1特有属性
                Assert.True(result.HasCapability(ShellCapabilities.Interactive));
                Assert.True(result.HasCapability(ShellCapabilities.Scripting));
                Assert.True(result.HasCapability(ShellCapabilities.ModuleManagement));

                // 检查版本格式
                Assert.NotNull(result.Version);
                Assert.True(result.Version.StartsWith("5."), $"PowerShell 5.1版本应以'5.'开头，实际: {result.Version}");

                // 检查性能指标
                Console.WriteLine($"启动时间: {result.StartupTimeMs} ms");
                Console.WriteLine($"响应时间: {result.ResponseTimeMs} ms");
                Console.WriteLine($"内存使用: {result.MemoryUsageMB:F1} MB");
                Console.WriteLine($"性能等级: {result.PerformanceLevel}");
                Console.WriteLine($"综合评分: {result.Score:F1}");

                // 检查特有属性
                Console.WriteLine("\\n=== PowerShell 5.1 特有属性 ===");
                foreach (var prop in result.Properties)
                {
                    Console.WriteLine($"{prop.Key}: {prop.Value}");
                }

                // 检查环境变量
                if (result.EnvironmentVariables.Count > 0)
                {
                    Console.WriteLine("\\n=== 环境变量 ===");
                    foreach (var env in result.EnvironmentVariables)
                    {
                        Console.WriteLine($"{env.Key}: {env.Value}");
                    }
                }

                // 验证启动参数
                Assert.NotNull(result.StartupParameters);
                Assert.Contains("-NoProfile", result.StartupParameters);
                Console.WriteLine($"启动参数: [{string.Join(", ", result.StartupParameters)}]");

                // 验证编码支持
                Assert.Contains("UTF-8", result.SupportedEncodings);
                Console.WriteLine($"支持编码: [{string.Join(", ", result.SupportedEncodings)}]");
                Console.WriteLine($"默认编码: {result.DefaultEncoding}");

                // 验证Windows原生标识
                Assert.True(result.IsNativeWindows, "PowerShell 5.1应该标识为Windows原生Shell");
            }
            else
            {
                Console.WriteLine($"检测失败原因: {result.ErrorMessage}");
                if (result.Exception != null)
                {
                    Console.WriteLine($"异常详情: {result.Exception.Message}");
                }
            }

            Console.WriteLine($"\\n描述: {result.GetDescription()}");
        }

        [Fact]
        public async Task PowerShellCoreDetector_DetectShell_ShouldProvideDetailedInfo()
        {
            // Act
            var result = await _psCoreDetector.DetectShellAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(EnvironmentType.PowerShellCore, result.Type);
            Assert.Equal(ShellType.PowerShellCore, result.ShellType);

            Console.WriteLine("=== PowerShell Core 详细检测结果 ===");
            Console.WriteLine($"检测状态: {result.Status}");
            Console.WriteLine($"是否可用: {result.IsAvailable}");
            Console.WriteLine($"安装路径: {result.InstallPath ?? "未检测到"}");
            Console.WriteLine($"可执行文件: {result.ExecutablePath ?? "未检测到"}");
            Console.WriteLine($"版本信息: {result.Version ?? "未知"}");
            Console.WriteLine($"配置路径: {result.ConfigurationPath ?? "未找到"}");

            if (result.IsAvailable)
            {
                // 验证PowerShell Core特有属性
                Assert.True(result.HasCapability(ShellCapabilities.Interactive));
                Assert.True(result.HasCapability(ShellCapabilities.Scripting));
                Assert.True(result.HasCapability(ShellCapabilities.ModuleManagement));
                Assert.True(result.HasCapability(ShellCapabilities.UnicodeSupport));

                // 检查版本格式
                Assert.NotNull(result.Version);
                Assert.True(result.Version.StartsWith("6.") || result.Version.StartsWith("7."),
                    $"PowerShell Core版本应以'6.'或'7.'开头，实际: {result.Version}");

                // 检查性能指标
                Console.WriteLine($"启动时间: {result.StartupTimeMs} ms");
                Console.WriteLine($"响应时间: {result.ResponseTimeMs} ms");
                Console.WriteLine($"内存使用: {result.MemoryUsageMB:F1} MB");
                Console.WriteLine($"性能等级: {result.PerformanceLevel}");
                Console.WriteLine($"综合评分: {result.Score:F1}");

                // PowerShell Core应该有更高的优先级
                Assert.True(result.Priority >= 90, $"PowerShell Core优先级应该>=90，实际: {result.Priority}");

                // 检查特有属性
                Console.WriteLine("\\n=== PowerShell Core 特有属性 ===");
                foreach (var prop in result.Properties)
                {
                    Console.WriteLine($"{prop.Key}: {prop.Value}");
                }

                // 验证PSEdition属性
                if (result.Properties.ContainsKey("PSEdition"))
                {
                    Assert.Equal("Core", result.Properties["PSEdition"]);
                    Console.WriteLine("✓ 确认为PowerShell Core版本");
                }

                // 验证启动参数
                Assert.NotNull(result.StartupParameters);
                Assert.Contains("-NoProfile", result.StartupParameters);
                Console.WriteLine($"启动参数: [{string.Join(", ", result.StartupParameters)}]");

                // 验证ANSI颜色支持
                Assert.True(result.SupportsAnsiColors, "PowerShell Core应该支持ANSI颜色");

                // 验证跨平台特性
                Assert.False(result.IsNativeWindows, "PowerShell Core应该标识为跨平台Shell");
            }
            else
            {
                Console.WriteLine($"检测失败原因: {result.ErrorMessage}");
                if (result.Exception != null)
                {
                    Console.WriteLine($"异常详情: {result.Exception.Message}");
                }
            }

            Console.WriteLine($"\\n描述: {result.GetDescription()}");
        }

        [Fact]
        public async Task PowerShellDetectors_Comparison_ShouldShowDifferences()
        {
            // Act
            var ps51Result = await _ps51Detector.DetectShellAsync();
            var psCoreResult = await _psCoreDetector.DetectShellAsync();

            // Assert & Compare
            Console.WriteLine("=== PowerShell版本对比分析 ===");

            Console.WriteLine($"\\nPowerShell 5.1:");
            Console.WriteLine($"  状态: {ps51Result.Status}");
            Console.WriteLine($"  版本: {ps51Result.Version ?? "未检测到"}");
            Console.WriteLine($"  优先级: {ps51Result.Priority}");
            Console.WriteLine($"  评分: {ps51Result.Score:F1}");

            Console.WriteLine($"\\nPowerShell Core:");
            Console.WriteLine($"  状态: {psCoreResult.Status}");
            Console.WriteLine($"  版本: {psCoreResult.Version ?? "未检测到"}");
            Console.WriteLine($"  优先级: {psCoreResult.Priority}");
            Console.WriteLine($"  评分: {psCoreResult.Score:F1}");

            // 如果两者都可用，PowerShell Core应该有更高优先级
            if (ps51Result.IsAvailable && psCoreResult.IsAvailable)
            {
                Assert.True(psCoreResult.Priority > ps51Result.Priority,
                    "PowerShell Core应该比PowerShell 5.1有更高优先级");

                Console.WriteLine("\\n✓ 优先级验证通过：PowerShell Core > PowerShell 5.1");

                // 比较能力差异
                Console.WriteLine("\\n=== 能力对比 ===");
                var ps51Capabilities = ps51Result.Capabilities.ToString().Split(", ");
                var psCoreCapabilities = psCoreResult.Capabilities.ToString().Split(", ");

                Console.WriteLine($"PowerShell 5.1能力: {ps51Result.Capabilities}");
                Console.WriteLine($"PowerShell Core能力: {psCoreResult.Capabilities}");

                // PowerShell Core应该支持更多现代特性
                Assert.True(psCoreResult.HasCapability(ShellCapabilities.UnicodeSupport));
                if (psCoreResult.SupportsAnsiColors)
                {
                    Console.WriteLine("✓ PowerShell Core支持ANSI颜色");
                }
            }

            // 验证模型差异
            Console.WriteLine("\\n=== 架构差异 ===");
            Console.WriteLine($"PowerShell 5.1 - Windows原生: {ps51Result.IsNativeWindows}");
            Console.WriteLine($"PowerShell Core - Windows原生: {psCoreResult.IsNativeWindows}");

            if (ps51Result.IsAvailable)
            {
                Assert.True(ps51Result.IsNativeWindows, "PowerShell 5.1应该是Windows原生");
            }
            if (psCoreResult.IsAvailable)
            {
                Assert.False(psCoreResult.IsNativeWindows, "PowerShell Core应该是跨平台");
            }
        }

        [Fact]
        public async Task PowerShellDetectors_ResponsivenessTest_ShouldMeasurePerformance()
        {
            // Act
            var ps51Result = await _ps51Detector.DetectShellAsync();
            var psCoreResult = await _psCoreDetector.DetectShellAsync();

            Console.WriteLine("=== PowerShell性能测试 ===");

            if (ps51Result.IsAvailable)
            {
                Console.WriteLine($"\\nPowerShell 5.1性能指标:");
                Console.WriteLine($"  启动时间: {ps51Result.StartupTimeMs} ms");
                Console.WriteLine($"  响应时间: {ps51Result.ResponseTimeMs} ms");
                Console.WriteLine($"  内存使用: {ps51Result.MemoryUsageMB:F1} MB");
                Console.WriteLine($"  性能等级: {ps51Result.PerformanceLevel}");

                // 基本性能验证
                Assert.True(ps51Result.StartupTimeMs >= 0, "启动时间应该非负");
                Assert.True(ps51Result.ResponseTimeMs >= 0, "响应时间应该非负");
                Assert.True(ps51Result.MemoryUsageMB > 0, "内存使用应该大于0");

                // 验证性能等级合理性
                if (ps51Result.ResponseTimeMs > 0)
                {
                    Assert.NotEqual(ShellPerformanceLevel.Unknown, ps51Result.PerformanceLevel);
                }
            }

            if (psCoreResult.IsAvailable)
            {
                Console.WriteLine($"\\nPowerShell Core性能指标:");
                Console.WriteLine($"  启动时间: {psCoreResult.StartupTimeMs} ms");
                Console.WriteLine($"  响应时间: {psCoreResult.ResponseTimeMs} ms");
                Console.WriteLine($"  内存使用: {psCoreResult.MemoryUsageMB:F1} MB");
                Console.WriteLine($"  性能等级: {psCoreResult.PerformanceLevel}");

                // 基本性能验证
                Assert.True(psCoreResult.StartupTimeMs >= 0, "启动时间应该非负");
                Assert.True(psCoreResult.ResponseTimeMs >= 0, "响应时间应该非负");
                Assert.True(psCoreResult.MemoryUsageMB > 0, "内存使用应该大于0");

                // 验证性能等级合理性
                if (psCoreResult.ResponseTimeMs > 0)
                {
                    Assert.NotEqual(ShellPerformanceLevel.Unknown, psCoreResult.PerformanceLevel);
                }
            }

            // 性能对比分析
            if (ps51Result.IsAvailable && psCoreResult.IsAvailable)
            {
                Console.WriteLine($"\\n=== 性能对比 ===");

                var ps51Faster = ps51Result.ResponseTimeMs < psCoreResult.ResponseTimeMs;
                var ps51LightWeight = ps51Result.MemoryUsageMB < psCoreResult.MemoryUsageMB;

                Console.WriteLine($"响应速度: {(ps51Faster ? "PowerShell 5.1" : "PowerShell Core")} 更快");
                Console.WriteLine($"内存占用: {(ps51LightWeight ? "PowerShell 5.1" : "PowerShell Core")} 更轻量");

                // 通常PowerShell Core在新版本中有更好的优化
                Console.WriteLine($"综合评分对比: PS5.1={ps51Result.Score:F1}, PSCore={psCoreResult.Score:F1}");
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task PowerShellDetectors_CacheTest_ShouldRespectCacheSettings(bool forceRefresh)
        {
            // Arrange
            var detector = _psCoreDetector;

            // Act
            var startTime = DateTime.UtcNow;
            var result = await detector.DetectShellAsync(forceRefresh);
            var duration = DateTime.UtcNow - startTime;

            // Assert
            Assert.NotNull(result);
            Console.WriteLine($"检测模式: {(forceRefresh ? "强制刷新" : "允许缓存")}");
            Console.WriteLine($"检测耗时: {duration.TotalMilliseconds:F0} ms");
            Console.WriteLine($"检测状态: {result.Status}");

            if (result.IsAvailable)
            {
                Assert.NotNull(result.ExecutablePath);
                Assert.NotNull(result.Version);
            }

            // 多次调用验证缓存行为
            var result2 = await detector.DetectShellAsync(forceRefresh);
            Assert.Equal(result.Status, result2.Status);
            Assert.Equal(result.Version, result2.Version);
        }

        public void Dispose()
        {
            _ps51Detector?.ClearCache();
            _psCoreDetector?.ClearCache();
        }
    }
}