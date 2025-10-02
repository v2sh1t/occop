using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;
using Occop.Core.Models.Environment;
using Occop.Core.Services.Environment;

namespace Occop.Core.Services.Environment
{
    /// <summary>
    /// Claude Code CLI 检测器
    /// </summary>
    public class ClaudeCodeDetector
    {
        /// <summary>
        /// 支持的Claude Code命令名称
        /// </summary>
        private static readonly string[] ClaudeCodeCommands = { "claude", "claude-code", "claude.exe", "claude-code.exe" };

        /// <summary>
        /// 常见安装路径
        /// </summary>
        private static readonly string[] CommonInstallPaths =
        {
            // NPM 全局安装路径
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "node_modules", ".bin"),

            // Node.js 路径
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "nodejs"),

            // 用户本地路径
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "bin"),

            // 系统路径
            @"C:\Program Files\Claude",
            @"C:\Program Files (x86)\Claude",
            @"C:\Tools\Claude"
        };

        /// <summary>
        /// 检测Claude Code CLI安装状态
        /// </summary>
        /// <param name="forceRefresh">是否强制刷新，忽略缓存</param>
        /// <returns>Claude Code信息</returns>
        public async Task<ClaudeCodeInfo> DetectClaudeCodeAsync(bool forceRefresh = false)
        {
            var claudeCodeInfo = new ClaudeCodeInfo();

            try
            {
                // 1. 首先尝试通过PATH环境变量检测
                var pathResult = await DetectFromPathAsync();
                if (pathResult.Status == DetectionStatus.Detected)
                {
                    return pathResult;
                }

                // 2. 检查常见安装路径
                var installPathResult = await DetectFromCommonPathsAsync();
                if (installPathResult.Status == DetectionStatus.Detected)
                {
                    return installPathResult;
                }

                // 3. 通过注册表检查（Windows）
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    var registryResult = await DetectFromRegistryAsync();
                    if (registryResult.Status == DetectionStatus.Detected)
                    {
                        return registryResult;
                    }
                }

                // 4. 检查NPM全局安装
                var npmResult = await DetectFromNpmAsync();
                if (npmResult.Status == DetectionStatus.Detected)
                {
                    return npmResult;
                }

                // 没有检测到Claude Code
                claudeCodeInfo.SetFailed("未找到Claude Code CLI安装");
                return claudeCodeInfo;
            }
            catch (Exception ex)
            {
                claudeCodeInfo.SetFailed($"检测Claude Code CLI时发生错误: {ex.Message}", ex);
                return claudeCodeInfo;
            }
        }

        /// <summary>
        /// 从PATH环境变量检测Claude Code
        /// </summary>
        /// <returns>检测结果</returns>
        private async Task<ClaudeCodeInfo> DetectFromPathAsync()
        {
            var claudeCodeInfo = new ClaudeCodeInfo();

            try
            {
                foreach (var command in ClaudeCodeCommands)
                {
                    var executablePath = FindExecutableInPath(command);
                    if (!string.IsNullOrEmpty(executablePath) && File.Exists(executablePath))
                    {
                        var version = await GetClaudeCodeVersionAsync(executablePath);
                        if (!string.IsNullOrEmpty(version))
                        {
                            var installPath = Path.GetDirectoryName(executablePath);
                            claudeCodeInfo.SetDetected(version, executablePath, installPath);

                            // 检测额外信息
                            await EnrichClaudeCodeInfoAsync(claudeCodeInfo, executablePath);
                            return claudeCodeInfo;
                        }
                    }
                }

                claudeCodeInfo.SetFailed("PATH中未找到可用的Claude Code CLI");
                return claudeCodeInfo;
            }
            catch (Exception ex)
            {
                claudeCodeInfo.SetFailed($"从PATH检测Claude Code时发生错误: {ex.Message}", ex);
                return claudeCodeInfo;
            }
        }

        /// <summary>
        /// 从常见安装路径检测Claude Code
        /// </summary>
        /// <returns>检测结果</returns>
        private async Task<ClaudeCodeInfo> DetectFromCommonPathsAsync()
        {
            var claudeCodeInfo = new ClaudeCodeInfo();

            try
            {
                foreach (var basePath in CommonInstallPaths)
                {
                    if (!Directory.Exists(basePath)) continue;

                    foreach (var command in ClaudeCodeCommands)
                    {
                        var executablePath = Path.Combine(basePath, command);
                        if (File.Exists(executablePath))
                        {
                            var version = await GetClaudeCodeVersionAsync(executablePath);
                            if (!string.IsNullOrEmpty(version))
                            {
                                claudeCodeInfo.SetDetected(version, executablePath, basePath);

                                // 检测额外信息
                                await EnrichClaudeCodeInfoAsync(claudeCodeInfo, executablePath);
                                return claudeCodeInfo;
                            }
                        }
                    }

                    // 递归搜索子目录
                    var foundPath = await SearchClaudeCodeInDirectoryAsync(basePath, 2);
                    if (!string.IsNullOrEmpty(foundPath))
                    {
                        var version = await GetClaudeCodeVersionAsync(foundPath);
                        if (!string.IsNullOrEmpty(version))
                        {
                            claudeCodeInfo.SetDetected(version, foundPath, Path.GetDirectoryName(foundPath));

                            // 检测额外信息
                            await EnrichClaudeCodeInfoAsync(claudeCodeInfo, foundPath);
                            return claudeCodeInfo;
                        }
                    }
                }

                claudeCodeInfo.SetFailed("常见安装路径中未找到Claude Code CLI");
                return claudeCodeInfo;
            }
            catch (Exception ex)
            {
                claudeCodeInfo.SetFailed($"从常见路径检测Claude Code时发生错误: {ex.Message}", ex);
                return claudeCodeInfo;
            }
        }

        /// <summary>
        /// 从注册表检测Claude Code（Windows）
        /// </summary>
        /// <returns>检测结果</returns>
        private async Task<ClaudeCodeInfo> DetectFromRegistryAsync()
        {
            var claudeCodeInfo = new ClaudeCodeInfo();

            try
            {
                // 检查卸载程序列表
                var uninstallPaths = new[]
                {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                };

                foreach (var uninstallPath in uninstallPaths)
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(uninstallPath))
                    {
                        if (key == null) continue;

                        foreach (var subKeyName in key.GetSubKeyNames())
                        {
                            using (var subKey = key.OpenSubKey(subKeyName))
                            {
                                if (subKey == null) continue;

                                var displayName = subKey.GetValue("DisplayName")?.ToString();
                                if (string.IsNullOrEmpty(displayName)) continue;

                                if (displayName.Contains("Claude", StringComparison.OrdinalIgnoreCase) ||
                                    displayName.Contains("Claude Code", StringComparison.OrdinalIgnoreCase))
                                {
                                    var installLocation = subKey.GetValue("InstallLocation")?.ToString();
                                    if (!string.IsNullOrEmpty(installLocation) && Directory.Exists(installLocation))
                                    {
                                        var foundPath = await SearchClaudeCodeInDirectoryAsync(installLocation);
                                        if (!string.IsNullOrEmpty(foundPath))
                                        {
                                            var version = await GetClaudeCodeVersionAsync(foundPath);
                                            if (!string.IsNullOrEmpty(version))
                                            {
                                                claudeCodeInfo.SetDetected(version, foundPath, installLocation);

                                                // 检测额外信息
                                                await EnrichClaudeCodeInfoAsync(claudeCodeInfo, foundPath);
                                                return claudeCodeInfo;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                claudeCodeInfo.SetFailed("注册表中未找到Claude Code CLI");
                return claudeCodeInfo;
            }
            catch (Exception ex)
            {
                claudeCodeInfo.SetFailed($"从注册表检测Claude Code时发生错误: {ex.Message}", ex);
                return claudeCodeInfo;
            }
        }

        /// <summary>
        /// 通过NPM检测Claude Code
        /// </summary>
        /// <returns>检测结果</returns>
        private async Task<ClaudeCodeInfo> DetectFromNpmAsync()
        {
            var claudeCodeInfo = new ClaudeCodeInfo();

            try
            {
                // 检查NPM是否安装
                var npmPath = FindExecutableInPath("npm") ?? FindExecutableInPath("npm.exe");
                if (string.IsNullOrEmpty(npmPath))
                {
                    claudeCodeInfo.SetFailed("未找到NPM，无法检测NPM安装的Claude Code");
                    return claudeCodeInfo;
                }

                // 检查全局NPM包
                var processInfo = new ProcessStartInfo
                {
                    FileName = npmPath,
                    Arguments = "list -g --depth=0 --json",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    if (process != null)
                    {
                        var output = await process.StandardOutput.ReadToEndAsync();
                        var error = await process.StandardError.ReadToEndAsync();

                        await process.WaitForExitAsync();

                        if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                        {
                            // 解析NPM输出
                            if (output.Contains("claude-code") || output.Contains("@anthropic-ai/claude"))
                            {
                                // 尝试获取Claude Code路径
                                var claudeCodePath = await GetNpmPackagePathAsync(npmPath, "claude-code") ??
                                                   await GetNpmPackagePathAsync(npmPath, "@anthropic-ai/claude");

                                if (!string.IsNullOrEmpty(claudeCodePath))
                                {
                                    var executablePath = Path.Combine(claudeCodePath, "bin", "claude");
                                    if (!File.Exists(executablePath))
                                    {
                                        executablePath = Path.Combine(claudeCodePath, "claude.exe");
                                    }

                                    if (File.Exists(executablePath))
                                    {
                                        var version = await GetClaudeCodeVersionAsync(executablePath);
                                        if (!string.IsNullOrEmpty(version))
                                        {
                                            claudeCodeInfo.SetDetected(version, executablePath, claudeCodePath);

                                            // 检测额外信息
                                            await EnrichClaudeCodeInfoAsync(claudeCodeInfo, executablePath);
                                            return claudeCodeInfo;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                claudeCodeInfo.SetFailed("NPM全局包中未找到Claude Code CLI");
                return claudeCodeInfo;
            }
            catch (Exception ex)
            {
                claudeCodeInfo.SetFailed($"通过NPM检测Claude Code时发生错误: {ex.Message}", ex);
                return claudeCodeInfo;
            }
        }

        /// <summary>
        /// 获取NPM包的安装路径
        /// </summary>
        /// <param name="npmPath">NPM可执行文件路径</param>
        /// <param name="packageName">包名</param>
        /// <returns>包路径</returns>
        private async Task<string?> GetNpmPackagePathAsync(string npmPath, string packageName)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = npmPath,
                    Arguments = $"root -g",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    if (process != null)
                    {
                        var output = await process.StandardOutput.ReadToEndAsync();
                        await process.WaitForExitAsync();

                        if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                        {
                            var globalRoot = output.Trim();
                            var packagePath = Path.Combine(globalRoot, packageName);

                            if (Directory.Exists(packagePath))
                            {
                                return packagePath;
                            }
                        }
                    }
                }
            }
            catch
            {
                // 忽略错误
            }

            return null;
        }

        /// <summary>
        /// 在目录中搜索Claude Code可执行文件
        /// </summary>
        /// <param name="directory">搜索目录</param>
        /// <param name="maxDepth">最大搜索深度</param>
        /// <returns>找到的可执行文件路径</returns>
        private async Task<string?> SearchClaudeCodeInDirectoryAsync(string directory, int maxDepth = 3)
        {
            try
            {
                if (!Directory.Exists(directory) || maxDepth <= 0)
                    return null;

                // 检查当前目录
                foreach (var command in ClaudeCodeCommands)
                {
                    var path = Path.Combine(directory, command);
                    if (File.Exists(path))
                    {
                        return path;
                    }
                }

                // 递归搜索子目录
                foreach (var subDir in Directory.GetDirectories(directory))
                {
                    var result = await SearchClaudeCodeInDirectoryAsync(subDir, maxDepth - 1);
                    if (!string.IsNullOrEmpty(result))
                    {
                        return result;
                    }
                }
            }
            catch
            {
                // 忽略访问权限等错误
            }

            return null;
        }

        /// <summary>
        /// 在PATH环境变量中查找可执行文件
        /// </summary>
        /// <param name="executable">可执行文件名</param>
        /// <returns>完整路径</returns>
        private string? FindExecutableInPath(string executable)
        {
            try
            {
                var path = Environment.GetEnvironmentVariable("PATH");
                if (string.IsNullOrEmpty(path))
                    return null;

                var paths = path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

                foreach (var dir in paths)
                {
                    try
                    {
                        var fullPath = Path.Combine(dir, executable);
                        if (File.Exists(fullPath))
                        {
                            return fullPath;
                        }

                        // Windows下尝试添加.exe扩展名
                        if (Environment.OSVersion.Platform == PlatformID.Win32NT && !executable.EndsWith(".exe"))
                        {
                            fullPath = Path.Combine(dir, executable + ".exe");
                            if (File.Exists(fullPath))
                            {
                                return fullPath;
                            }
                        }
                    }
                    catch
                    {
                        // 忽略无效路径
                    }
                }
            }
            catch
            {
                // 忽略错误
            }

            return null;
        }

        /// <summary>
        /// 获取Claude Code版本信息
        /// </summary>
        /// <param name="executablePath">可执行文件路径</param>
        /// <returns>版本字符串</returns>
        private async Task<string?> GetClaudeCodeVersionAsync(string executablePath)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    if (process != null)
                    {
                        var output = await process.StandardOutput.ReadToEndAsync();
                        var error = await process.StandardError.ReadToEndAsync();

                        await process.WaitForExitAsync();

                        if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                        {
                            // 从输出中提取版本号
                            var versionMatch = Regex.Match(output, @"(\d+\.\d+\.\d+(?:\.\d+)?)");
                            if (versionMatch.Success)
                            {
                                return versionMatch.Groups[1].Value;
                            }

                            // 如果无法提取版本号，返回原始输出的第一行
                            return output.Split('\n')[0].Trim();
                        }
                    }
                }
            }
            catch
            {
                // 忽略错误
            }

            return null;
        }

        /// <summary>
        /// 丰富Claude Code信息
        /// </summary>
        /// <param name="claudeCodeInfo">Claude Code信息</param>
        /// <param name="executablePath">可执行文件路径</param>
        private async Task EnrichClaudeCodeInfoAsync(ClaudeCodeInfo claudeCodeInfo, string executablePath)
        {
            try
            {
                // 检测认证状态
                claudeCodeInfo.AuthStatus = await DetectAuthenticationStatusAsync(executablePath);

                // 检测配置文件
                claudeCodeInfo.ConfigFilePath = DetectConfigFile();

                // 收集环境变量
                CollectRelevantEnvironmentVariables(claudeCodeInfo);

                // 测量性能指标
                claudeCodeInfo.Performance = await MeasurePerformanceAsync(executablePath);

                // 检测API端点
                claudeCodeInfo.ApiEndpoint = await DetectApiEndpointAsync(executablePath);
            }
            catch
            {
                // 忽略非关键错误
            }
        }

        /// <summary>
        /// 检测认证状态
        /// </summary>
        /// <param name="executablePath">可执行文件路径</param>
        /// <returns>认证状态</returns>
        private async Task<AuthenticationStatus> DetectAuthenticationStatusAsync(string executablePath)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = "auth status",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    if (process != null)
                    {
                        var output = await process.StandardOutput.ReadToEndAsync();
                        await process.WaitForExitAsync();

                        if (process.ExitCode == 0)
                        {
                            if (output.Contains("authenticated", StringComparison.OrdinalIgnoreCase) ||
                                output.Contains("logged in", StringComparison.OrdinalIgnoreCase))
                            {
                                return AuthenticationStatus.Authenticated;
                            }
                        }
                        else
                        {
                            if (output.Contains("not authenticated", StringComparison.OrdinalIgnoreCase) ||
                                output.Contains("not logged in", StringComparison.OrdinalIgnoreCase))
                            {
                                return AuthenticationStatus.NotAuthenticated;
                            }
                        }
                    }
                }
            }
            catch
            {
                // 忽略错误
            }

            return AuthenticationStatus.Unknown;
        }

        /// <summary>
        /// 检测配置文件路径
        /// </summary>
        /// <returns>配置文件路径</returns>
        private string? DetectConfigFile()
        {
            try
            {
                var configPaths = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "config.json"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "claude", "config.json"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "claude", "config.json")
                };

                foreach (var path in configPaths)
                {
                    if (File.Exists(path))
                    {
                        return path;
                    }
                }
            }
            catch
            {
                // 忽略错误
            }

            return null;
        }

        /// <summary>
        /// 收集相关环境变量
        /// </summary>
        /// <param name="claudeCodeInfo">Claude Code信息</param>
        private void CollectRelevantEnvironmentVariables(ClaudeCodeInfo claudeCodeInfo)
        {
            try
            {
                var relevantVars = new[]
                {
                    "CLAUDE_API_KEY",
                    "CLAUDE_API_URL",
                    "CLAUDE_CONFIG_PATH",
                    "ANTHROPIC_API_KEY",
                    "NODE_PATH",
                    "NPM_CONFIG_PREFIX"
                };

                foreach (var varName in relevantVars)
                {
                    var value = Environment.GetEnvironmentVariable(varName);
                    if (!string.IsNullOrEmpty(value))
                    {
                        claudeCodeInfo.EnvironmentVariables[varName] = value;
                    }
                }
            }
            catch
            {
                // 忽略错误
            }
        }

        /// <summary>
        /// 测量性能指标
        /// </summary>
        /// <param name="executablePath">可执行文件路径</param>
        /// <returns>性能指标</returns>
        private async Task<PerformanceMetrics?> MeasurePerformanceAsync(string executablePath)
        {
            try
            {
                var metrics = new PerformanceMetrics();
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                var processInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = "--help",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    if (process != null)
                    {
                        await process.WaitForExitAsync();
                        stopwatch.Stop();

                        metrics.StartupTimeMs = stopwatch.ElapsedMilliseconds;
                        metrics.ResponseTimeMs = stopwatch.ElapsedMilliseconds;

                        // 尝试获取内存使用量
                        try
                        {
                            metrics.MemoryUsageMB = process.WorkingSet64 / (1024.0 * 1024.0);
                        }
                        catch
                        {
                            metrics.MemoryUsageMB = 0;
                        }

                        return metrics;
                    }
                }
            }
            catch
            {
                // 忽略错误
            }

            return null;
        }

        /// <summary>
        /// 检测API端点
        /// </summary>
        /// <param name="executablePath">可执行文件路径</param>
        /// <returns>API端点</returns>
        private async Task<string?> DetectApiEndpointAsync(string executablePath)
        {
            try
            {
                // 尝试从配置中读取API端点
                var configPath = DetectConfigFile();
                if (!string.IsNullOrEmpty(configPath) && File.Exists(configPath))
                {
                    var configContent = await File.ReadAllTextAsync(configPath);
                    var apiUrlMatch = Regex.Match(configContent, @"""api_url""\s*:\s*""([^""]+)""");
                    if (apiUrlMatch.Success)
                    {
                        return apiUrlMatch.Groups[1].Value;
                    }
                }

                // 默认API端点
                return "https://api.anthropic.com";
            }
            catch
            {
                return "https://api.anthropic.com";
            }
        }

        /// <summary>
        /// 检查Claude Code是否可用
        /// </summary>
        /// <param name="executablePath">可执行文件路径</param>
        /// <returns>是否可用</returns>
        public async Task<bool> IsClaudeCodeAvailableAsync(string executablePath)
        {
            if (string.IsNullOrEmpty(executablePath) || !File.Exists(executablePath))
                return false;

            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    if (process != null)
                    {
                        await process.WaitForExitAsync();
                        return process.ExitCode == 0;
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        /// <summary>
        /// 验证Claude Code版本兼容性
        /// </summary>
        /// <param name="version">版本字符串</param>
        /// <returns>是否兼容</returns>
        public bool IsVersionCompatible(string version)
        {
            if (string.IsNullOrEmpty(version))
                return false;

            try
            {
                var versionMatch = Regex.Match(version, @"(\d+)\.(\d+)(?:\.(\d+))?");
                if (versionMatch.Success && Version.TryParse(versionMatch.Value, out var parsedVersion))
                {
                    var minVersion = new Version(1, 0, 0);
                    return parsedVersion >= minVersion;
                }
            }
            catch
            {
                // 忽略解析错误
            }

            return false;
        }
    }
}