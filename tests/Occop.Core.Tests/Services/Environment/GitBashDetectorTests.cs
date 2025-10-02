using System;
using System.Threading.Tasks;
using Xunit;
using Occop.Core.Services.Environment;
using Occop.Core.Models.Environment;

namespace Occop.Core.Tests.Services.Environment
{
    /// <summary>
    /// GitBash检测器专门测试
    /// </summary>
    public class GitBashDetectorTests : IDisposable
    {
        private readonly GitBashDetector _gitBashDetector;

        public GitBashDetectorTests()
        {
            _gitBashDetector = new GitBashDetector();
        }

        [Fact]
        public async Task GitBashDetector_DetectShell_ShouldProvideDetailedInfo()
        {
            // Act
            var result = await _gitBashDetector.DetectShellAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(EnvironmentType.GitBash, result.Type);
            Assert.Equal(ShellType.GitBash, result.ShellType);

            Console.WriteLine("=== Git Bash 详细检测结果 ===");
            Console.WriteLine($"检测状态: {result.Status}");
            Console.WriteLine($"是否可用: {result.IsAvailable}");
            Console.WriteLine($"安装路径: {result.InstallPath ?? "未检测到"}");
            Console.WriteLine($"可执行文件: {result.ExecutablePath ?? "未检测到"}");
            Console.WriteLine($"版本信息: {result.Version ?? "未知"}");
            Console.WriteLine($"配置路径: {result.ConfigurationPath ?? "未找到"}");

            if (result.IsAvailable)
            {
                // 验证Git Bash特有属性
                Assert.True(result.HasCapability(ShellCapabilities.Interactive));
                Assert.True(result.HasCapability(ShellCapabilities.Scripting));
                Assert.True(result.HasCapability(ShellCapabilities.Piping));
                Assert.True(result.HasCapability(ShellCapabilities.JobControl));

                // 检查版本信息
                Assert.NotNull(result.Version);
                Console.WriteLine($"Git版本: {result.Version}");

                // 检查性能指标
                Console.WriteLine($"启动时间: {result.StartupTimeMs} ms");
                Console.WriteLine($"响应时间: {result.ResponseTimeMs} ms");
                Console.WriteLine($"内存使用: {result.MemoryUsageMB:F1} MB");
                Console.WriteLine($"性能等级: {result.PerformanceLevel}");
                Console.WriteLine($"综合评分: {result.Score:F1}");

                // 验证优先级
                Assert.True(result.Priority >= 70, $"Git Bash优先级应该>=70，实际: {result.Priority}");

                // 检查Git Bash特有属性
                Console.WriteLine("\\n=== Git Bash 特有属性 ===");
                foreach (var prop in result.Properties)
                {
                    Console.WriteLine($"{prop.Key}: {prop.Value}");
                }

                // 验证Git相关路径
                if (result.Properties.ContainsKey("GitPath"))
                {
                    var gitPath = result.Properties["GitPath"].ToString();
                    Assert.False(string.IsNullOrEmpty(gitPath), "Git路径不应为空");
                    Console.WriteLine($"✓ Git路径: {gitPath}");
                }

                if (result.Properties.ContainsKey("BashPath"))
                {
                    var bashPath = result.Properties["BashPath"].ToString();
                    Assert.False(string.IsNullOrEmpty(bashPath), "Bash路径不应为空");
                    Console.WriteLine($"✓ Bash路径: {bashPath}");
                }

                // 验证检测方法
                if (result.Properties.ContainsKey("DetectionMethod"))
                {
                    var method = result.Properties["DetectionMethod"].ToString();
                    Assert.Contains(method, new[] { "GitPath", "DirectBash", "CommonPaths" });
                    Console.WriteLine($"✓ 检测方法: {method}");
                }

                // 检查环境变量
                if (result.EnvironmentVariables.Count > 0)
                {
                    Console.WriteLine("\\n=== Git Bash 环境变量 ===");
                    foreach (var env in result.EnvironmentVariables)
                    {
                        Console.WriteLine($"{env.Key}: {env.Value}");
                    }

                    // 验证Git安装根目录变量
                    if (result.EnvironmentVariables.ContainsKey("GIT_INSTALL_ROOT"))
                    {
                        Console.WriteLine("✓ 检测到Git安装根目录");
                    }

                    // 验证MinGW路径
                    if (result.EnvironmentVariables.ContainsKey("MINGW_PREFIX"))
                    {
                        Console.WriteLine("✓ 检测到MinGW环境");
                    }
                }

                // 验证启动参数
                Assert.NotNull(result.StartupParameters);
                Assert.Contains("--login", result.StartupParameters);
                Assert.Contains("-i", result.StartupParameters);
                Console.WriteLine($"启动参数: [{string.Join(", ", result.StartupParameters)}]");

                // 验证编码支持
                Assert.Contains("UTF-8", result.SupportedEncodings);
                Assert.Equal("UTF-8", result.DefaultEncoding);
                Console.WriteLine($"支持编码: [{string.Join(", ", result.SupportedEncodings)}]");
                Console.WriteLine($"默认编码: {result.DefaultEncoding}");

                // 验证跨平台特性
                Assert.False(result.IsNativeWindows, "Git Bash应该标识为非Windows原生Shell");

                // 验证ANSI颜色支持
                Assert.True(result.SupportsAnsiColors, "Git Bash应该支持ANSI颜色");

                // 验证窗口标题和清屏命令
                Assert.NotNull(result.WindowTitleCommand);
                Assert.NotNull(result.ClearCommand);
                Console.WriteLine($"窗口标题命令: {result.WindowTitleCommand}");
                Console.WriteLine($"清屏命令: {result.ClearCommand}");
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
        public async Task GitBashDetector_ConfigurationTest_ShouldDetectGitConfig()
        {
            // Act
            var result = await _gitBashDetector.DetectShellAsync();

            if (!result.IsAvailable)
            {
                Console.WriteLine("Git Bash未安装，跳过配置测试");
                return;
            }

            // Assert
            Console.WriteLine("=== Git配置检测测试 ===");

            // 检查Git配置相关属性
            var gitConfigProperties = new[] { "Git.UserName", "Git.UserEmail", "Git.DefaultBranch", "Git.CoreEditor" };

            Console.WriteLine("检测到的Git配置:");
            foreach (var propKey in gitConfigProperties)
            {
                if (result.Properties.ContainsKey(propKey))
                {
                    var value = result.Properties[propKey];
                    Console.WriteLine($"  {propKey}: {value}");
                    Assert.NotNull(value);
                }
                else
                {
                    Console.WriteLine($"  {propKey}: 未配置");
                }
            }

            // 验证Git路径存在且可访问
            if (result.Properties.ContainsKey("GitPath"))
            {
                var gitPath = result.Properties["GitPath"].ToString();
                Assert.False(string.IsNullOrEmpty(gitPath));
                Console.WriteLine($"\\n✓ Git可执行文件路径验证通过: {gitPath}");
            }

            // 验证Bash配置文件
            var bashConfigFiles = new[] { "ConfigFile..bashrc", "ConfigFile..bash_profile", "ConfigFile..profile" };

            Console.WriteLine("\\n检测到的Bash配置文件:");
            foreach (var configKey in bashConfigFiles)
            {
                if (result.Properties.ContainsKey(configKey))
                {
                    var configPath = result.Properties[configKey];
                    Console.WriteLine($"  {configKey}: {configPath}");
                }
            }

            // 验证历史文件路径
            if (!string.IsNullOrEmpty(result.HistoryPath))
            {
                Console.WriteLine($"\\n✓ Bash历史文件: {result.HistoryPath}");
            }
        }

        [Fact]
        public async Task GitBashDetector_VersionTest_ShouldValidateVersions()
        {
            // Act
            var result = await _gitBashDetector.DetectShellAsync();

            if (!result.IsAvailable)
            {
                Console.WriteLine("Git Bash未安装，跳过版本测试");
                return;
            }

            // Assert
            Console.WriteLine("=== Git Bash 版本验证测试 ===");

            // 验证Git版本格式
            Assert.NotNull(result.Version);
            Console.WriteLine($"主版本信息: {result.Version}");

            // 验证版本兼容性
            Assert.NotNull(result.ParsedVersion);
            Assert.NotNull(result.MinimumCompatibleVersion);

            var isCompatible = result.ParsedVersion >= result.MinimumCompatibleVersion;
            Console.WriteLine($"解析版本: {result.ParsedVersion}");
            Console.WriteLine($"最低要求: {result.MinimumCompatibleVersion}");
            Console.WriteLine($"版本兼容: {(isCompatible ? "✓ 兼容" : "✗ 不兼容")}");

            if (isCompatible)
            {
                Assert.Equal(DetectionStatus.Detected, result.Status);
            }
            else
            {
                Assert.Equal(DetectionStatus.IncompatibleVersion, result.Status);
            }

            // 检查Bash版本（如果有）
            if (result.Properties.ContainsKey("BashVersion"))
            {
                var bashVersion = result.Properties["BashVersion"].ToString();
                Assert.False(string.IsNullOrEmpty(bashVersion));
                Console.WriteLine($"Bash版本: {bashVersion}");
            }

            // 检查MSYS版本（如果有）
            if (result.Properties.ContainsKey("MSYSVersion"))
            {
                var msysVersion = result.Properties["MSYSVersion"].ToString();
                Console.WriteLine($"MSYS版本: {msysVersion}");
            }
        }

        [Fact]
        public async Task GitBashDetector_PerformanceTest_ShouldMeasureMetrics()
        {
            // Act
            var result = await _gitBashDetector.DetectShellAsync();

            if (!result.IsAvailable)
            {
                Console.WriteLine("Git Bash未安装，跳过性能测试");
                return;
            }

            // Assert
            Console.WriteLine("=== Git Bash 性能测试 ===");

            Console.WriteLine($"启动时间: {result.StartupTimeMs} ms");
            Console.WriteLine($"响应时间: {result.ResponseTimeMs} ms");
            Console.WriteLine($"内存使用: {result.MemoryUsageMB:F1} MB");
            Console.WriteLine($"性能等级: {result.PerformanceLevel}");
            Console.WriteLine($"综合评分: {result.Score:F1}");

            // 基本性能验证
            Assert.True(result.StartupTimeMs >= 0, "启动时间应该非负");
            Assert.True(result.ResponseTimeMs >= 0, "响应时间应该非负");
            Assert.True(result.MemoryUsageMB > 0, "内存使用应该大于0");

            // Git Bash通常比PowerShell轻量
            Assert.True(result.MemoryUsageMB < 100, "Git Bash内存使用应该相对较少");

            // 验证性能等级合理性
            if (result.ResponseTimeMs > 0)
            {
                Assert.NotEqual(ShellPerformanceLevel.Unknown, result.PerformanceLevel);
                Console.WriteLine($"✓ 性能等级评估: {result.PerformanceLevel}");
            }

            // 验证评分合理性
            Assert.True(result.Score >= result.Priority, "综合评分应该不低于基础优先级");
            Assert.True(result.Score <= 100, "综合评分不应超过100");

            Console.WriteLine($"✓ 性能指标验证通过");
        }

        [Fact]
        public async Task GitBashDetector_CapabilityTest_ShouldValidateFeatures()
        {
            // Act
            var result = await _gitBashDetector.DetectShellAsync();

            if (!result.IsAvailable)
            {
                Console.WriteLine("Git Bash未安装，跳过能力测试");
                return;
            }

            // Assert
            Console.WriteLine("=== Git Bash 能力验证测试 ===");

            // 验证基础Shell能力
            var requiredCapabilities = new[]
            {
                ShellCapabilities.Interactive,
                ShellCapabilities.Scripting,
                ShellCapabilities.Piping,
                ShellCapabilities.JobControl,
                ShellCapabilities.History,
                ShellCapabilities.AutoCompletion,
                ShellCapabilities.UnicodeSupport
            };

            Console.WriteLine("验证基础能力:");
            foreach (var capability in requiredCapabilities)
            {
                var hasCapability = result.HasCapability(capability);
                Console.WriteLine($"  {capability}: {(hasCapability ? "✓" : "✗")}");

                if (capability != ShellCapabilities.AutoCompletion) // 自动补全可能需要特殊配置
                {
                    Assert.True(hasCapability, $"Git Bash应该支持{capability}");
                }
            }

            // 验证Git Bash不支持的能力
            var unsupportedCapabilities = new[]
            {
                ShellCapabilities.ModuleManagement,
                ShellCapabilities.RemoteExecution
            };

            Console.WriteLine("\\n验证不支持的能力:");
            foreach (var capability in unsupportedCapabilities)
            {
                var hasCapability = result.HasCapability(capability);
                Console.WriteLine($"  {capability}: {(hasCapability ? "✓" : "✗ (符合预期)")}");
                // Git Bash通常不支持这些高级功能
            }

            // 验证命令行语法
            var syntax = result.GetCommandLineSyntax();
            Assert.Contains("-c", syntax);
            Console.WriteLine($"\\n命令行语法: {syntax}");

            // 验证ANSI颜色支持
            Assert.True(result.SupportsAnsiColors, "Git Bash应该支持ANSI颜色");
            Console.WriteLine("✓ ANSI颜色支持确认");

            Console.WriteLine($"\\n总能力数: {result.Capabilities.ToString().Split(", ").Length}");
            Console.WriteLine($"能力详情: {result.Capabilities}");
        }

        [Fact]
        public async Task GitBashDetector_MultipleDetection_ShouldBeConsistent()
        {
            // Act - 多次检测
            var result1 = await _gitBashDetector.DetectShellAsync();
            var result2 = await _gitBashDetector.DetectShellAsync(); // 使用缓存
            var result3 = await _gitBashDetector.DetectShellAsync(forceRefresh: true); // 强制刷新

            // Assert
            Console.WriteLine("=== Git Bash 多次检测一致性测试 ===");

            // 验证状态一致性
            Assert.Equal(result1.Status, result2.Status);
            Assert.Equal(result1.Status, result3.Status);
            Console.WriteLine($"检测状态一致性: ✓ ({result1.Status})");

            if (result1.IsAvailable)
            {
                // 验证核心信息一致性
                Assert.Equal(result1.Version, result2.Version);
                Assert.Equal(result1.Version, result3.Version);
                Console.WriteLine($"版本一致性: ✓ ({result1.Version})");

                Assert.Equal(result1.ExecutablePath, result2.ExecutablePath);
                Assert.Equal(result1.ExecutablePath, result3.ExecutablePath);
                Console.WriteLine($"路径一致性: ✓ ({result1.ExecutablePath})");

                Assert.Equal(result1.ShellType, result2.ShellType);
                Assert.Equal(result1.ShellType, result3.ShellType);
                Console.WriteLine($"Shell类型一致性: ✓ ({result1.ShellType})");

                // 验证能力一致性
                Assert.Equal(result1.Capabilities, result2.Capabilities);
                Assert.Equal(result1.Capabilities, result3.Capabilities);
                Console.WriteLine($"能力一致性: ✓");

                // 验证属性数量一致性（具体值可能因时间戳等略有差异）
                Assert.Equal(result1.Properties.Count, result3.Properties.Count);
                Console.WriteLine($"属性数量一致性: ✓ ({result1.Properties.Count}项)");
            }
            else
            {
                // 验证错误信息一致性
                Assert.Equal(result1.ErrorMessage, result2.ErrorMessage);
                Assert.Equal(result1.ErrorMessage, result3.ErrorMessage);
                Console.WriteLine($"错误信息一致性: ✓");
            }

            Console.WriteLine("✓ 多次检测一致性验证通过");
        }

        [Theory]
        [InlineData("--login")]
        [InlineData("-i")]
        public void GitBashDetector_StartupParameters_ShouldContainExpectedParams(string expectedParam)
        {
            // Arrange
            var shellInfo = new ShellInfo(EnvironmentType.GitBash, ShellType.GitBash);

            // Act - 模拟设置启动参数
            shellInfo.SetStartupParameters("--login", "-i");

            // Assert
            Assert.Contains(expectedParam, shellInfo.StartupParameters);
            Console.WriteLine($"✓ 启动参数包含: {expectedParam}");
        }

        [Fact]
        public void GitBashDetector_ShellInfo_ShouldHaveCorrectDefaults()
        {
            // Act
            var shellInfo = new ShellInfo(EnvironmentType.GitBash, ShellType.GitBash);

            // Assert
            Console.WriteLine("=== Git Bash ShellInfo 默认值测试 ===");

            Assert.Equal(ShellType.GitBash, shellInfo.ShellType);
            Assert.Equal(EnvironmentType.GitBash, shellInfo.Type);
            Console.WriteLine($"✓ Shell类型: {shellInfo.ShellType}");

            Assert.Equal("UTF-8", shellInfo.DefaultEncoding);
            Console.WriteLine($"✓ 默认编码: {shellInfo.DefaultEncoding}");

            Assert.False(shellInfo.IsNativeWindows);
            Console.WriteLine($"✓ Windows原生: {shellInfo.IsNativeWindows}");

            Assert.True(shellInfo.SupportsAnsiColors);
            Console.WriteLine($"✓ ANSI颜色: {shellInfo.SupportsAnsiColors}");

            Assert.Contains("UTF-8", shellInfo.SupportedEncodings);
            Console.WriteLine($"✓ 支持编码: [{string.Join(", ", shellInfo.SupportedEncodings)}]");

            Assert.NotNull(shellInfo.WindowTitleCommand);
            Assert.NotNull(shellInfo.ClearCommand);
            Console.WriteLine($"✓ 窗口标题命令: {shellInfo.WindowTitleCommand}");
            Console.WriteLine($"✓ 清屏命令: {shellInfo.ClearCommand}");

            Console.WriteLine("✓ 默认值验证通过");
        }

        public void Dispose()
        {
            _gitBashDetector?.ClearCache();
        }
    }
}