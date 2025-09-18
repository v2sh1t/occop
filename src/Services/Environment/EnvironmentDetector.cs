using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using Occop.Core.Models.Environment;

namespace Occop.Core.Services.Environment
{
    /// <summary>
    /// 环境检测器实现
    /// </summary>
    public class EnvironmentDetector : IEnvironmentDetector, IDisposable
    {
        private readonly ConcurrentDictionary<EnvironmentType, CacheEntry> _cache;
        private readonly TimeSpan _cacheExpiration;
        private readonly object _monitoringLock = new object();
        private bool _isMonitoring;
        private bool _disposed;

        /// <summary>
        /// 环境变化事件
        /// </summary>
        public event EventHandler<EnvironmentChangedEventArgs>? EnvironmentChanged;

        /// <summary>
        /// 缓存条目
        /// </summary>
        private class CacheEntry
        {
            public EnvironmentInfo EnvironmentInfo { get; }
            public DateTime CacheTime { get; }
            public bool IsValid(TimeSpan expiration) => DateTime.UtcNow - CacheTime < expiration;

            public CacheEntry(EnvironmentInfo environmentInfo)
            {
                EnvironmentInfo = environmentInfo;
                CacheTime = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// 初始化环境检测器
        /// </summary>
        /// <param name="cacheExpirationMinutes">缓存过期时间（分钟）</param>
        public EnvironmentDetector(int cacheExpirationMinutes = 30)
        {
            _cache = new ConcurrentDictionary<EnvironmentType, CacheEntry>();
            _cacheExpiration = TimeSpan.FromMinutes(cacheExpirationMinutes);
        }

        /// <summary>
        /// 检测所有支持的环境
        /// </summary>
        /// <param name="forceRefresh">是否强制刷新缓存</param>
        /// <returns>检测结果</returns>
        public async Task<DetectionResult> DetectAllEnvironmentsAsync(bool forceRefresh = false)
        {
            var result = new DetectionResult();

            try
            {
                var detectionTasks = new List<Task<EnvironmentInfo>>();

                // 并行检测所有环境类型
                foreach (EnvironmentType envType in Enum.GetValues<EnvironmentType>())
                {
                    detectionTasks.Add(DetectEnvironmentAsync(envType, forceRefresh));
                }

                var environments = await Task.WhenAll(detectionTasks);

                // 添加检测结果
                foreach (var env in environments)
                {
                    result.AddEnvironment(env.Type, env);

                    // 如果检测失败，添加错误信息
                    if (env.Status == DetectionStatus.Failed && !string.IsNullOrEmpty(env.ErrorMessage))
                    {
                        result.AddError(env.Type, env.ErrorMessage, env.Exception);
                    }
                }

                // 确定推荐的Shell环境
                result.RecommendedShell = await GetRecommendedShellAsync();
            }
            catch (Exception ex)
            {
                result.AddError(EnvironmentType.PowerShellCore, "检测过程中发生未知错误", ex);
            }
            finally
            {
                result.MarkCompleted();
            }

            return result;
        }

        /// <summary>
        /// 检测特定类型的环境
        /// </summary>
        /// <param name="environmentType">环境类型</param>
        /// <param name="forceRefresh">是否强制刷新缓存</param>
        /// <returns>环境信息</returns>
        public async Task<EnvironmentInfo> DetectEnvironmentAsync(EnvironmentType environmentType, bool forceRefresh = false)
        {
            // 检查缓存
            if (!forceRefresh && _cache.TryGetValue(environmentType, out var cachedEntry) && cachedEntry.IsValid(_cacheExpiration))
            {
                return cachedEntry.EnvironmentInfo;
            }

            var environmentInfo = new EnvironmentInfo(environmentType);

            try
            {
                switch (environmentType)
                {
                    case EnvironmentType.PowerShell51:
                        await DetectPowerShell51Async(environmentInfo);
                        break;
                    case EnvironmentType.PowerShellCore:
                        await DetectPowerShellCoreAsync(environmentInfo);
                        break;
                    case EnvironmentType.GitBash:
                        await DetectGitBashAsync(environmentInfo);
                        break;
                    case EnvironmentType.ClaudeCode:
                        await DetectClaudeCodeAsync(environmentInfo);
                        break;
                    default:
                        environmentInfo.SetFailed($"不支持的环境类型: {environmentType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                environmentInfo.SetFailed($"检测 {environmentType} 时发生异常: {ex.Message}", ex);
            }

            // 更新缓存
            _cache.AddOrUpdate(environmentType, new CacheEntry(environmentInfo), (key, oldEntry) => new CacheEntry(environmentInfo));

            return environmentInfo;
        }

        /// <summary>
        /// 获取推荐的Shell环境（基于优先级）
        /// </summary>
        /// <returns>推荐的环境信息</returns>
        public async Task<EnvironmentInfo?> GetRecommendedShellAsync()
        {
            var shells = new[]
            {
                EnvironmentType.PowerShellCore,
                EnvironmentType.PowerShell51,
                EnvironmentType.GitBash
            };

            foreach (var shellType in shells)
            {
                var env = await DetectEnvironmentAsync(shellType);
                if (env.IsAvailable)
                {
                    env.IsRecommended = true;
                    return env;
                }
            }

            return null;
        }

        /// <summary>
        /// 检查缓存是否有效
        /// </summary>
        /// <param name="environmentType">环境类型</param>
        /// <returns>缓存是否有效</returns>
        public bool IsCacheValid(EnvironmentType environmentType)
        {
            return _cache.TryGetValue(environmentType, out var entry) && entry.IsValid(_cacheExpiration);
        }

        /// <summary>
        /// 清除指定环境的缓存
        /// </summary>
        /// <param name="environmentType">环境类型，null表示清除所有缓存</param>
        public void ClearCache(EnvironmentType? environmentType = null)
        {
            if (environmentType.HasValue)
            {
                _cache.TryRemove(environmentType.Value, out _);
            }
            else
            {
                _cache.Clear();
            }
        }

        /// <summary>
        /// 启动环境变化监控
        /// </summary>
        public void StartEnvironmentMonitoring()
        {
            lock (_monitoringLock)
            {
                if (_isMonitoring) return;
                _isMonitoring = true;
                // TODO: 实现文件系统监控和注册表监控
                // 这里可以使用FileSystemWatcher监控PATH变化等
            }
        }

        /// <summary>
        /// 停止环境变化监控
        /// </summary>
        public void StopEnvironmentMonitoring()
        {
            lock (_monitoringLock)
            {
                if (!_isMonitoring) return;
                _isMonitoring = false;
                // TODO: 停止监控
            }
        }

        #region 私有检测方法

        /// <summary>
        /// 检测PowerShell 5.1
        /// </summary>
        /// <param name="environmentInfo">环境信息</param>
        private async Task DetectPowerShell51Async(EnvironmentInfo environmentInfo)
        {
            try
            {
                // 首先尝试从注册表查找PowerShell 5.1
                var ps51Path = GetPowerShell51FromRegistry();
                if (!string.IsNullOrEmpty(ps51Path) && File.Exists(ps51Path))
                {
                    var version = await GetPowerShellVersionAsync(ps51Path);
                    if (!string.IsNullOrEmpty(version))
                    {
                        environmentInfo.SetDetected(Path.GetDirectoryName(ps51Path)!, ps51Path, version);
                        environmentInfo.AddProperty("RegistrySource", true);
                        return;
                    }
                }

                // 尝试从PATH环境变量查找
                var pathResult = await FindExecutableInPathAsync("powershell.exe");
                if (pathResult.found)
                {
                    var version = await GetPowerShellVersionAsync(pathResult.path);
                    if (!string.IsNullOrEmpty(version) && version.StartsWith("5."))
                    {
                        environmentInfo.SetDetected(Path.GetDirectoryName(pathResult.path)!, pathResult.path, version);
                        environmentInfo.AddProperty("PathSource", true);
                        return;
                    }
                }

                environmentInfo.SetFailed("未找到PowerShell 5.1安装");
            }
            catch (Exception ex)
            {
                environmentInfo.SetFailed("检测PowerShell 5.1时发生异常", ex);
            }
        }

        /// <summary>
        /// 检测PowerShell Core
        /// </summary>
        /// <param name="environmentInfo">环境信息</param>
        private async Task DetectPowerShellCoreAsync(EnvironmentInfo environmentInfo)
        {
            try
            {
                // 尝试从PATH环境变量查找pwsh.exe
                var pathResult = await FindExecutableInPathAsync("pwsh.exe");
                if (pathResult.found)
                {
                    var version = await GetPowerShellVersionAsync(pathResult.path);
                    if (!string.IsNullOrEmpty(version))
                    {
                        environmentInfo.SetDetected(Path.GetDirectoryName(pathResult.path)!, pathResult.path, version);
                        environmentInfo.AddProperty("PathSource", true);
                        return;
                    }
                }

                // 尝试常见安装路径
                var commonPaths = new[]
                {
                    @"C:\Program Files\PowerShell\7\pwsh.exe",
                    @"C:\Program Files\PowerShell\6\pwsh.exe",
                    Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), @"Microsoft\powershell\pwsh.exe")
                };

                foreach (var path in commonPaths)
                {
                    if (File.Exists(path))
                    {
                        var version = await GetPowerShellVersionAsync(path);
                        if (!string.IsNullOrEmpty(version))
                        {
                            environmentInfo.SetDetected(Path.GetDirectoryName(path)!, path, version);
                            environmentInfo.AddProperty("CommonPathSource", true);
                            return;
                        }
                    }
                }

                environmentInfo.SetFailed("未找到PowerShell Core安装");
            }
            catch (Exception ex)
            {
                environmentInfo.SetFailed("检测PowerShell Core时发生异常", ex);
            }
        }

        /// <summary>
        /// 检测Git Bash
        /// </summary>
        /// <param name="environmentInfo">环境信息</param>
        private async Task DetectGitBashAsync(EnvironmentInfo environmentInfo)
        {
            try
            {
                // 尝试从PATH环境变量查找git.exe和bash.exe
                var gitResult = await FindExecutableInPathAsync("git.exe");
                if (gitResult.found)
                {
                    var gitDir = Path.GetDirectoryName(gitResult.path);
                    var bashPath = Path.Combine(gitDir!, "..", "bin", "bash.exe");

                    if (File.Exists(bashPath))
                    {
                        var gitVersion = await GetGitVersionAsync(gitResult.path);
                        if (!string.IsNullOrEmpty(gitVersion))
                        {
                            environmentInfo.SetDetected(Path.GetDirectoryName(gitDir!)!, bashPath, gitVersion);
                            environmentInfo.AddProperty("GitPath", gitResult.path);
                            environmentInfo.AddProperty("BashPath", bashPath);
                            return;
                        }
                    }
                }

                // 尝试常见安装路径
                var commonPaths = new[]
                {
                    @"C:\Program Files\Git\bin\bash.exe",
                    @"C:\Program Files (x86)\Git\bin\bash.exe",
                    Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), @"Programs\Git\bin\bash.exe")
                };

                foreach (var bashPath in commonPaths)
                {
                    if (File.Exists(bashPath))
                    {
                        var gitPath = Path.Combine(Path.GetDirectoryName(bashPath)!, "git.exe");
                        if (File.Exists(gitPath))
                        {
                            var gitVersion = await GetGitVersionAsync(gitPath);
                            if (!string.IsNullOrEmpty(gitVersion))
                            {
                                environmentInfo.SetDetected(Path.GetDirectoryName(Path.GetDirectoryName(bashPath)!)!, bashPath, gitVersion);
                                environmentInfo.AddProperty("GitPath", gitPath);
                                environmentInfo.AddProperty("BashPath", bashPath);
                                return;
                            }
                        }
                    }
                }

                environmentInfo.SetFailed("未找到Git Bash安装");
            }
            catch (Exception ex)
            {
                environmentInfo.SetFailed("检测Git Bash时发生异常", ex);
            }
        }

        /// <summary>
        /// 检测Claude Code CLI
        /// </summary>
        /// <param name="environmentInfo">环境信息</param>
        private async Task DetectClaudeCodeAsync(EnvironmentInfo environmentInfo)
        {
            try
            {
                // 尝试从PATH环境变量查找claude.exe
                var pathResult = await FindExecutableInPathAsync("claude.exe");
                if (pathResult.found)
                {
                    var version = await GetClaudeVersionAsync(pathResult.path);
                    if (!string.IsNullOrEmpty(version))
                    {
                        environmentInfo.SetDetected(Path.GetDirectoryName(pathResult.path)!, pathResult.path, version);
                        environmentInfo.AddProperty("PathSource", true);
                        return;
                    }
                }

                // 尝试claude命令（可能是批处理文件或其他形式）
                var claudeResult = await FindExecutableInPathAsync("claude");
                if (claudeResult.found)
                {
                    var version = await GetClaudeVersionAsync(claudeResult.path);
                    if (!string.IsNullOrEmpty(version))
                    {
                        environmentInfo.SetDetected(Path.GetDirectoryName(claudeResult.path)!, claudeResult.path, version);
                        environmentInfo.AddProperty("PathSource", true);
                        return;
                    }
                }

                environmentInfo.SetFailed("未找到Claude Code CLI安装");
            }
            catch (Exception ex)
            {
                environmentInfo.SetFailed("检测Claude Code CLI时发生异常", ex);
            }
        }

        #endregion

        #region 私有辅助方法

        /// <summary>
        /// 从注册表获取PowerShell 5.1路径
        /// </summary>
        /// <returns>PowerShell 5.1路径</returns>
        private string? GetPowerShell51FromRegistry()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\PowerShell\1\ShellIds\Microsoft.PowerShell");
                return key?.GetValue("Path") as string;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 在PATH环境变量中查找可执行文件
        /// </summary>
        /// <param name="fileName">文件名</param>
        /// <returns>查找结果</returns>
        private async Task<(bool found, string path)> FindExecutableInPathAsync(string fileName)
        {
            return await Task.Run(() =>
            {
                var pathEnv = System.Environment.GetEnvironmentVariable("PATH");
                if (string.IsNullOrEmpty(pathEnv)) return (false, string.Empty);

                var paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

                foreach (var path in paths)
                {
                    try
                    {
                        var fullPath = Path.Combine(path, fileName);
                        if (File.Exists(fullPath))
                        {
                            return (true, fullPath);
                        }
                    }
                    catch
                    {
                        // 忽略无效路径
                    }
                }

                return (false, string.Empty);
            });
        }

        /// <summary>
        /// 获取PowerShell版本
        /// </summary>
        /// <param name="powershellPath">PowerShell可执行文件路径</param>
        /// <returns>版本字符串</returns>
        private async Task<string?> GetPowerShellVersionAsync(string powershellPath)
        {
            try
            {
                using var process = new Process();
                process.StartInfo.FileName = powershellPath;
                process.StartInfo.Arguments = "-NoProfile -Command \"$PSVersionTable.PSVersion.ToString()\"";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                return process.ExitCode == 0 ? output.Trim() : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 获取Git版本
        /// </summary>
        /// <param name="gitPath">Git可执行文件路径</param>
        /// <returns>版本字符串</returns>
        private async Task<string?> GetGitVersionAsync(string gitPath)
        {
            try
            {
                using var process = new Process();
                process.StartInfo.FileName = gitPath;
                process.StartInfo.Arguments = "--version";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    // 从"git version 2.34.1.windows.1"中提取版本号
                    var match = System.Text.RegularExpressions.Regex.Match(output, @"git version (\d+\.\d+\.\d+)");
                    return match.Success ? match.Groups[1].Value : output.Trim();
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 获取Claude版本
        /// </summary>
        /// <param name="claudePath">Claude可执行文件路径</param>
        /// <returns>版本字符串</returns>
        private async Task<string?> GetClaudeVersionAsync(string claudePath)
        {
            try
            {
                using var process = new Process();
                process.StartInfo.FileName = claudePath;
                process.StartInfo.Arguments = "--version";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                return process.ExitCode == 0 ? output.Trim() : null;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region 资源释放

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                StopEnvironmentMonitoring();
                _cache.Clear();
                _disposed = true;
            }
        }

        #endregion
    }
}