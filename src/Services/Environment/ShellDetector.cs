using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Occop.Core.Models.Environment;

namespace Occop.Core.Services.Environment
{
    /// <summary>
    /// Shell检测器接口
    /// </summary>
    public interface IShellDetector
    {
        /// <summary>
        /// 检测特定Shell类型
        /// </summary>
        /// <param name="forceRefresh">是否强制刷新缓存</param>
        /// <returns>Shell信息</returns>
        Task<ShellInfo> DetectShellAsync(bool forceRefresh = false);

        /// <summary>
        /// 测试Shell响应性
        /// </summary>
        /// <param name="shellPath">Shell可执行文件路径</param>
        /// <returns>响应时间（毫秒），-1表示失败</returns>
        Task<int> TestShellResponsivenessAsync(string shellPath);

        /// <summary>
        /// 获取Shell配置信息
        /// </summary>
        /// <param name="shellPath">Shell可执行文件路径</param>
        /// <returns>配置信息字典</returns>
        Task<Dictionary<string, string>> GetShellConfigurationAsync(string shellPath);
    }

    /// <summary>
    /// Shell检测器基础抽象类，提供Shell检测的公共功能
    /// </summary>
    public abstract class ShellDetector : IShellDetector
    {
        /// <summary>
        /// Shell类型
        /// </summary>
        protected abstract ShellType ShellType { get; }

        /// <summary>
        /// 对应的环境类型
        /// </summary>
        protected abstract EnvironmentType EnvironmentType { get; }

        /// <summary>
        /// 缓存的Shell信息
        /// </summary>
        protected ShellInfo? _cachedShellInfo;

        /// <summary>
        /// 缓存过期时间
        /// </summary>
        protected TimeSpan _cacheExpiration = TimeSpan.FromMinutes(30);

        /// <summary>
        /// 上次检测时间
        /// </summary>
        protected DateTime _lastDetectionTime = DateTime.MinValue;

        /// <summary>
        /// 检测特定Shell类型
        /// </summary>
        /// <param name="forceRefresh">是否强制刷新缓存</param>
        /// <returns>Shell信息</returns>
        public async Task<ShellInfo> DetectShellAsync(bool forceRefresh = false)
        {
            // 检查缓存
            if (!forceRefresh && _cachedShellInfo != null && IsCacheValid())
            {
                return _cachedShellInfo;
            }

            var shellInfo = new ShellInfo(EnvironmentType, ShellType);

            try
            {
                // 调用子类的具体检测实现
                await DetectShellInternalAsync(shellInfo);

                // 如果检测成功，进行响应性测试和配置收集
                if (shellInfo.Status == DetectionStatus.Detected)
                {
                    await EnhanceShellInfoAsync(shellInfo);
                }
            }
            catch (Exception ex)
            {
                shellInfo.SetFailed($\"检测 {ShellType} 时发生异常: {ex.Message}\", ex);
            }

            // 更新缓存
            _cachedShellInfo = shellInfo;
            _lastDetectionTime = DateTime.UtcNow;

            return shellInfo;
        }

        /// <summary>
        /// 测试Shell响应性
        /// </summary>
        /// <param name="shellPath">Shell可执行文件路径</param>
        /// <returns>响应时间（毫秒），-1表示失败</returns>
        public virtual async Task<int> TestShellResponsivenessAsync(string shellPath)
        {
            if (string.IsNullOrEmpty(shellPath) || !File.Exists(shellPath))
                return -1;

            try
            {
                var stopwatch = Stopwatch.StartNew();

                using var process = new Process();
                ConfigureTestProcess(process, shellPath);

                process.Start();

                // 等待Shell启动并响应
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var timeoutTask = Task.Delay(5000); // 5秒超时

                var completedTask = await Task.WhenAny(outputTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    process.Kill();
                    return -1;
                }

                await process.WaitForExitAsync();
                stopwatch.Stop();

                return process.ExitCode == 0 ? (int)stopwatch.ElapsedMilliseconds : -1;
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// 获取Shell配置信息
        /// </summary>
        /// <param name="shellPath">Shell可执行文件路径</param>
        /// <returns>配置信息字典</returns>
        public virtual async Task<Dictionary<string, string>> GetShellConfigurationAsync(string shellPath)
        {
            var config = new Dictionary<string, string>();

            if (string.IsNullOrEmpty(shellPath) || !File.Exists(shellPath))
                return config;

            try
            {
                // 子类可以重写此方法提供特定的配置检测
                await PopulateShellConfigurationAsync(shellPath, config);
            }
            catch
            {
                // 配置获取失败不影响基本检测
            }

            return config;
        }

        #region 抽象和虚拟方法

        /// <summary>
        /// 子类实现的具体Shell检测逻辑
        /// </summary>
        /// <param name="shellInfo">要填充的Shell信息</param>
        /// <returns>检测任务</returns>
        protected abstract Task DetectShellInternalAsync(ShellInfo shellInfo);

        /// <summary>
        /// 配置响应性测试的进程参数
        /// </summary>
        /// <param name="process">进程对象</param>
        /// <param name="shellPath">Shell路径</param>
        protected virtual void ConfigureTestProcess(Process process, string shellPath)
        {
            process.StartInfo.FileName = shellPath;
            process.StartInfo.Arguments = GetTestArguments();
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;
        }

        /// <summary>
        /// 获取响应性测试的参数
        /// </summary>
        /// <returns>测试参数</returns>
        protected virtual string GetTestArguments()
        {
            return ShellType switch
            {
                ShellType.PowerShell51 or ShellType.PowerShellCore => \"-NoProfile -Command \\\"Write-Output 'test'\\\"\",
                ShellType.GitBash => \"-c \\\"echo test\\\"\",
                ShellType.CommandPrompt => \"/c \\\"echo test\\\"\",
                _ => \"--version\"
            };
        }

        /// <summary>
        /// 填充Shell特定的配置信息
        /// </summary>
        /// <param name="shellPath">Shell路径</param>
        /// <param name="config">配置字典</param>
        /// <returns>填充任务</returns>
        protected virtual Task PopulateShellConfigurationAsync(string shellPath, Dictionary<string, string> config)
        {
            // 基础实现 - 子类可以重写
            config[\"ShellPath\"] = shellPath;
            config[\"ShellType\"] = ShellType.ToString();
            return Task.CompletedTask;
        }

        #endregion

        #region 受保护的辅助方法

        /// <summary>
        /// 增强Shell信息（添加性能指标和配置）
        /// </summary>
        /// <param name="shellInfo">Shell信息</param>
        /// <returns>增强任务</returns>
        protected virtual async Task EnhanceShellInfoAsync(ShellInfo shellInfo)
        {
            if (string.IsNullOrEmpty(shellInfo.ExecutablePath))
                return;

            try
            {
                // 测试响应性
                var responseTime = await TestShellResponsivenessAsync(shellInfo.ExecutablePath);

                // 获取配置信息
                var config = await GetShellConfigurationAsync(shellInfo.ExecutablePath);

                // 估算内存使用量（基于文件大小和Shell类型）
                var memoryUsage = EstimateMemoryUsage(shellInfo.ExecutablePath);

                // 设置性能指标
                shellInfo.SetPerformanceMetrics(responseTime > 0 ? responseTime : 1000, responseTime, memoryUsage);

                // 添加配置属性
                foreach (var kvp in config)
                {
                    shellInfo.AddProperty(kvp.Key, kvp.Value);
                }

                // 设置配置路径
                if (config.TryGetValue(\"ConfigPath\", out var configPath))
                {
                    shellInfo.ConfigurationPath = configPath;
                }
            }
            catch
            {
                // 增强信息失败不影响基本检测结果
            }
        }

        /// <summary>
        /// 在PATH环境变量中查找可执行文件
        /// </summary>
        /// <param name="fileName">文件名</param>
        /// <returns>查找结果</returns>
        protected async Task<(bool found, string path)> FindExecutableInPathAsync(string fileName)
        {
            return await Task.Run(() =>
            {
                var pathEnv = System.Environment.GetEnvironmentVariable(\"PATH\");
                if (string.IsNullOrEmpty(pathEnv))
                    return (false, string.Empty);

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
        /// 获取可执行文件版本信息
        /// </summary>
        /// <param name="executablePath">可执行文件路径</param>
        /// <param name="versionArguments">版本参数</param>
        /// <returns>版本字符串</returns>
        protected async Task<string?> GetExecutableVersionAsync(string executablePath, string versionArguments = \"--version\")
        {
            if (string.IsNullOrEmpty(executablePath) || !File.Exists(executablePath))
                return null;

            try
            {
                using var process = new Process();
                process.StartInfo.FileName = executablePath;
                process.StartInfo.Arguments = versionArguments;
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
        /// 估算内存使用量
        /// </summary>
        /// <param name="executablePath">可执行文件路径</param>
        /// <returns>估算的内存使用量（MB）</returns>
        protected virtual double EstimateMemoryUsage(string executablePath)
        {
            try
            {
                var fileInfo = new FileInfo(executablePath);
                var fileSizeMB = fileInfo.Length / (1024.0 * 1024.0);

                // 基于文件大小和Shell类型估算内存使用量
                return ShellType switch
                {
                    ShellType.PowerShell51 => Math.Max(30, fileSizeMB * 3), // PowerShell 5.1通常较重
                    ShellType.PowerShellCore => Math.Max(25, fileSizeMB * 2.5), // PowerShell Core优化较好
                    ShellType.GitBash => Math.Max(15, fileSizeMB * 2), // Git Bash较轻量
                    ShellType.CommandPrompt => Math.Max(5, fileSizeMB * 1.5), // CMD最轻量
                    _ => Math.Max(20, fileSizeMB * 2)
                };
            }
            catch
            {
                // 默认估算值
                return ShellType switch
                {
                    ShellType.PowerShell51 => 35,
                    ShellType.PowerShellCore => 30,
                    ShellType.GitBash => 20,
                    ShellType.CommandPrompt => 10,
                    _ => 25
                };
            }
        }

        /// <summary>
        /// 检查缓存是否有效
        /// </summary>
        /// <returns>缓存是否有效</returns>
        protected bool IsCacheValid()
        {
            return _cachedShellInfo != null &&
                   DateTime.UtcNow - _lastDetectionTime < _cacheExpiration;
        }

        /// <summary>
        /// 清除缓存
        /// </summary>
        public void ClearCache()
        {
            _cachedShellInfo = null;
            _lastDetectionTime = DateTime.MinValue;
        }

        #endregion
    }

    /// <summary>
    /// Shell检测器管理器，协调多个Shell检测器
    /// </summary>
    public class ShellDetectorManager
    {
        private readonly Dictionary<ShellType, IShellDetector> _detectors;

        /// <summary>
        /// 初始化Shell检测器管理器
        /// </summary>
        public ShellDetectorManager()
        {
            _detectors = new Dictionary<ShellType, IShellDetector>();
        }

        /// <summary>
        /// 注册Shell检测器
        /// </summary>
        /// <param name="shellType">Shell类型</param>
        /// <param name="detector">检测器实例</param>
        public void RegisterDetector(ShellType shellType, IShellDetector detector)
        {
            _detectors[shellType] = detector;
        }

        /// <summary>
        /// 检测所有已注册的Shell
        /// </summary>
        /// <param name="forceRefresh">是否强制刷新缓存</param>
        /// <returns>所有Shell信息</returns>
        public async Task<List<ShellInfo>> DetectAllShellsAsync(bool forceRefresh = false)
        {
            var results = new List<ShellInfo>();
            var tasks = new List<Task<ShellInfo>>();

            foreach (var detector in _detectors.Values)
            {
                tasks.Add(detector.DetectShellAsync(forceRefresh));
            }

            var shellInfos = await Task.WhenAll(tasks);
            results.AddRange(shellInfos);

            return results;
        }

        /// <summary>
        /// 检测特定类型的Shell
        /// </summary>
        /// <param name=\"shellType\">Shell类型</param>
        /// <param name=\"forceRefresh\">是否强制刷新缓存</param>
        /// <returns>Shell信息</returns>
        public async Task<ShellInfo?> DetectShellAsync(ShellType shellType, bool forceRefresh = false)
        {
            if (_detectors.TryGetValue(shellType, out var detector))
            {
                return await detector.DetectShellAsync(forceRefresh);
            }

            return null;
        }

        /// <summary>
        /// 获取最优Shell（基于评分）
        /// </summary>
        /// <param name=\"requirements\">要求（可为null）</param>
        /// <returns>最优Shell信息</returns>
        public async Task<ShellInfo?> GetOptimalShellAsync(ShellRequirements? requirements = null)
        {
            var allShells = await DetectAllShellsAsync();
            var availableShells = allShells.Where(s => s.IsAvailable).ToList();

            if (!availableShells.Any())
                return null;

            // 根据要求筛选
            if (requirements != null)
            {
                availableShells = availableShells.Where(s => MeetsRequirements(s, requirements)).ToList();
            }

            // 按评分排序，返回最高分的
            return availableShells.OrderByDescending(s => s.Score).FirstOrDefault();
        }

        /// <summary>
        /// 检查Shell是否满足要求
        /// </summary>
        /// <param name=\"shell\">Shell信息</param>
        /// <param name=\"requirements\">要求</param>
        /// <returns>是否满足要求</returns>
        private static bool MeetsRequirements(ShellInfo shell, ShellRequirements requirements)
        {
            if (requirements.RequiredCapabilities != ShellCapabilities.None &&
                !requirements.RequiredCapabilities.All(cap => shell.HasCapability(cap)))
            {
                return false;
            }

            if (requirements.MinimumPerformanceLevel.HasValue &&
                shell.PerformanceLevel < requirements.MinimumPerformanceLevel.Value)
            {
                return false;
            }

            if (requirements.MaxMemoryUsageMB.HasValue &&
                shell.MemoryUsageMB > requirements.MaxMemoryUsageMB.Value)
            {
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Shell要求规范
    /// </summary>
    public class ShellRequirements
    {
        /// <summary>
        /// 必需的Shell能力
        /// </summary>
        public ShellCapabilities RequiredCapabilities { get; set; } = ShellCapabilities.None;

        /// <summary>
        /// 最低性能等级
        /// </summary>
        public ShellPerformanceLevel? MinimumPerformanceLevel { get; set; }

        /// <summary>
        /// 最大内存使用量（MB）
        /// </summary>
        public double? MaxMemoryUsageMB { get; set; }

        /// <summary>
        /// 首选Shell类型（按优先级排序）
        /// </summary>
        public ShellType[] PreferredShellTypes { get; set; } = Array.Empty<ShellType>();

        /// <summary>
        /// 是否需要Unicode支持
        /// </summary>
        public bool RequireUnicodeSupport { get; set; }

        /// <summary>
        /// 是否需要交互模式
        /// </summary>
        public bool RequireInteractiveMode { get; set; }
    }
}