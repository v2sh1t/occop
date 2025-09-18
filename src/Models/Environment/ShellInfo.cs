using System;
using System.Collections.Generic;
using System.Linq;
using Occop.Core.Services.Environment;

namespace Occop.Core.Models.Environment
{
    /// <summary>
    /// Shell类型枚举
    /// </summary>
    public enum ShellType
    {
        /// <summary>
        /// PowerShell 5.1（Windows PowerShell）
        /// </summary>
        PowerShell51,

        /// <summary>
        /// PowerShell Core 7+（跨平台PowerShell）
        /// </summary>
        PowerShellCore,

        /// <summary>
        /// Git Bash（基于MinTTY的Bash Shell）
        /// </summary>
        GitBash,

        /// <summary>
        /// 命令提示符（传统Windows Shell）
        /// </summary>
        CommandPrompt,

        /// <summary>
        /// Windows Subsystem for Linux
        /// </summary>
        WSL,

        /// <summary>
        /// 其他Shell类型
        /// </summary>
        Other
    }

    /// <summary>
    /// Shell能力枚举
    /// </summary>
    [Flags]
    public enum ShellCapabilities
    {
        /// <summary>
        /// 无特殊能力
        /// </summary>
        None = 0,

        /// <summary>
        /// 支持交互模式
        /// </summary>
        Interactive = 1,

        /// <summary>
        /// 支持脚本执行
        /// </summary>
        Scripting = 2,

        /// <summary>
        /// 支持管道操作
        /// </summary>
        Piping = 4,

        /// <summary>
        /// 支持任务控制（作业管理）
        /// </summary>
        JobControl = 8,

        /// <summary>
        /// 支持命令历史
        /// </summary>
        History = 16,

        /// <summary>
        /// 支持自动补全
        /// </summary>
        AutoCompletion = 32,

        /// <summary>
        /// 支持语法高亮
        /// </summary>
        SyntaxHighlighting = 64,

        /// <summary>
        /// 支持模块/包管理
        /// </summary>
        ModuleManagement = 128,

        /// <summary>
        /// 支持远程执行
        /// </summary>
        RemoteExecution = 256,

        /// <summary>
        /// 支持Unicode/UTF-8
        /// </summary>
        UnicodeSupport = 512
    }

    /// <summary>
    /// Shell性能等级枚举
    /// </summary>
    public enum ShellPerformanceLevel
    {
        /// <summary>
        /// 未知性能
        /// </summary>
        Unknown,

        /// <summary>
        /// 低性能
        /// </summary>
        Low,

        /// <summary>
        /// 中等性能
        /// </summary>
        Medium,

        /// <summary>
        /// 高性能
        /// </summary>
        High,

        /// <summary>
        /// 极高性能
        /// </summary>
        VeryHigh
    }

    /// <summary>
    /// Shell信息类，扩展了基础环境信息以支持Shell特定的属性和功能
    /// </summary>
    public class ShellInfo : EnvironmentInfo
    {
        /// <summary>
        /// Shell类型
        /// </summary>
        public ShellType ShellType { get; internal set; }

        /// <summary>
        /// Shell能力标志
        /// </summary>
        public ShellCapabilities Capabilities { get; internal set; }

        /// <summary>
        /// 启动参数（用于特定配置）
        /// </summary>
        public string[] StartupParameters { get; internal set; }

        /// <summary>
        /// 默认启动参数
        /// </summary>
        public string[] DefaultParameters { get; internal set; }

        /// <summary>
        /// 是否支持交互模式
        /// </summary>
        public bool SupportsInteractiveMode => Capabilities.HasFlag(ShellCapabilities.Interactive);

        /// <summary>
        /// 是否支持脚本执行
        /// </summary>
        public bool SupportsScripting => Capabilities.HasFlag(ShellCapabilities.Scripting);

        /// <summary>
        /// 配置文件路径
        /// </summary>
        public string? ConfigurationPath { get; internal set; }

        /// <summary>
        /// 模块路径（PowerShell模块，Bash插件等）
        /// </summary>
        public string? ModulePath { get; internal set; }

        /// <summary>
        /// 历史文件路径
        /// </summary>
        public string? HistoryPath { get; internal set; }

        /// <summary>
        /// Shell环境变量
        /// </summary>
        public Dictionary<string, string> EnvironmentVariables { get; }

        /// <summary>
        /// 启动时间（毫秒）
        /// </summary>
        public int StartupTimeMs { get; internal set; }

        /// <summary>
        /// 响应时间（毫秒）
        /// </summary>
        public int ResponseTimeMs { get; internal set; }

        /// <summary>
        /// 性能等级
        /// </summary>
        public ShellPerformanceLevel PerformanceLevel { get; internal set; }

        /// <summary>
        /// 内存使用量（MB）
        /// </summary>
        public double MemoryUsageMB { get; internal set; }

        /// <summary>
        /// Shell评分（综合各项指标）
        /// </summary>
        public double Score { get; internal set; }

        /// <summary>
        /// 支持的编码格式
        /// </summary>
        public string[] SupportedEncodings { get; internal set; }

        /// <summary>
        /// 默认编码格式
        /// </summary>
        public string DefaultEncoding { get; internal set; }

        /// <summary>
        /// 是否为Windows原生Shell
        /// </summary>
        public bool IsNativeWindows { get; internal set; }

        /// <summary>
        /// 是否支持ANSI颜色
        /// </summary>
        public bool SupportsAnsiColors { get; internal set; }

        /// <summary>
        /// 窗口标题设置命令
        /// </summary>
        public string? WindowTitleCommand { get; internal set; }

        /// <summary>
        /// 清屏命令
        /// </summary>
        public string? ClearCommand { get; internal set; }

        /// <summary>
        /// 初始化Shell信息
        /// </summary>
        /// <param name="environmentType">环境类型</param>
        /// <param name="shellType">Shell类型</param>
        public ShellInfo(EnvironmentType environmentType, ShellType shellType) : base(environmentType)
        {
            ShellType = shellType;
            Capabilities = GetDefaultCapabilities(shellType);
            StartupParameters = Array.Empty<string>();
            DefaultParameters = GetDefaultParameters(shellType);
            EnvironmentVariables = new Dictionary<string, string>();
            SupportedEncodings = GetDefaultEncodings(shellType);
            DefaultEncoding = GetDefaultEncoding(shellType);
            PerformanceLevel = ShellPerformanceLevel.Unknown;
            IsNativeWindows = GetIsNativeWindows(shellType);
            SupportsAnsiColors = GetSupportsAnsiColors(shellType);
            WindowTitleCommand = GetWindowTitleCommand(shellType);
            ClearCommand = GetClearCommand(shellType);
        }

        /// <summary>
        /// 设置Shell检测成功的信息
        /// </summary>
        /// <param name="installPath">安装路径</param>
        /// <param name="executablePath">可执行文件路径</param>
        /// <param name="version">版本信息</param>
        /// <param name="configPath">配置文件路径</param>
        public void SetShellDetected(string installPath, string executablePath, string version, string? configPath = null)
        {
            SetDetected(installPath, executablePath, version);
            ConfigurationPath = configPath;
        }

        /// <summary>
        /// 设置性能指标
        /// </summary>
        /// <param name="startupTimeMs">启动时间（毫秒）</param>
        /// <param name="responseTimeMs">响应时间（毫秒）</param>
        /// <param name="memoryUsageMB">内存使用量（MB）</param>
        public void SetPerformanceMetrics(int startupTimeMs, int responseTimeMs, double memoryUsageMB)
        {
            StartupTimeMs = startupTimeMs;
            ResponseTimeMs = responseTimeMs;
            MemoryUsageMB = memoryUsageMB;
            PerformanceLevel = CalculatePerformanceLevel(startupTimeMs, responseTimeMs, memoryUsageMB);
            Score = CalculateShellScore();
        }

        /// <summary>
        /// 添加Shell环境变量
        /// </summary>
        /// <param name="name">变量名</param>
        /// <param name="value">变量值</param>
        public void AddEnvironmentVariable(string name, string value)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            EnvironmentVariables[name] = value ?? string.Empty;
        }

        /// <summary>
        /// 设置启动参数
        /// </summary>
        /// <param name="parameters">启动参数数组</param>
        public void SetStartupParameters(params string[] parameters)
        {
            StartupParameters = parameters ?? Array.Empty<string>();
        }

        /// <summary>
        /// 检查是否具有特定能力
        /// </summary>
        /// <param name="capability">要检查的能力</param>
        /// <returns>是否具有该能力</returns>
        public bool HasCapability(ShellCapabilities capability)
        {
            return Capabilities.HasFlag(capability);
        }

        /// <summary>
        /// 添加Shell能力
        /// </summary>
        /// <param name="capability">要添加的能力</param>
        public void AddCapability(ShellCapabilities capability)
        {
            Capabilities |= capability;
        }

        /// <summary>
        /// 移除Shell能力
        /// </summary>
        /// <param name="capability">要移除的能力</param>
        public void RemoveCapability(ShellCapabilities capability)
        {
            Capabilities &= ~capability;
        }

        /// <summary>
        /// 获取Shell的命令行语法说明
        /// </summary>
        /// <returns>命令行语法说明</returns>
        public string GetCommandLineSyntax()
        {
            return ShellType switch
            {
                ShellType.PowerShell51 or ShellType.PowerShellCore =>
                    $\"{ExecutablePath}\" {string.Join(\" \", DefaultParameters)} -Command \"<命令>\",
                ShellType.GitBash =>
                    $\"{ExecutablePath}\" {string.Join(\" \", DefaultParameters)} -c \"<命令>\",
                ShellType.CommandPrompt =>
                    $\"{ExecutablePath}\" /c \"<命令>\",
                _ => $\"{ExecutablePath}\" <参数>\"
            };
        }

        /// <summary>
        /// 获取详细的Shell描述
        /// </summary>
        /// <returns>详细描述</returns>
        public new string GetDescription()
        {
            var baseDescription = base.GetDescription();
            if (Status != DetectionStatus.Detected)
                return baseDescription;

            var capabilities = Capabilities.ToString().Replace(\", \", \" | \");
            var performance = PerformanceLevel != ShellPerformanceLevel.Unknown ? $\" (性能: {PerformanceLevel})\" : \"\";

            return $\"{baseDescription} - {capabilities}{performance}\";
        }

        #region 私有辅助方法

        /// <summary>
        /// 获取Shell类型的默认能力
        /// </summary>
        /// <param name="shellType">Shell类型</param>
        /// <returns>默认能力</returns>
        private static ShellCapabilities GetDefaultCapabilities(ShellType shellType)
        {
            return shellType switch
            {
                ShellType.PowerShell51 or ShellType.PowerShellCore =>
                    ShellCapabilities.Interactive | ShellCapabilities.Scripting | ShellCapabilities.Piping |
                    ShellCapabilities.JobControl | ShellCapabilities.History | ShellCapabilities.AutoCompletion |
                    ShellCapabilities.ModuleManagement | ShellCapabilities.RemoteExecution |
                    ShellCapabilities.UnicodeSupport,

                ShellType.GitBash =>
                    ShellCapabilities.Interactive | ShellCapabilities.Scripting | ShellCapabilities.Piping |
                    ShellCapabilities.JobControl | ShellCapabilities.History | ShellCapabilities.AutoCompletion |
                    ShellCapabilities.UnicodeSupport,

                ShellType.CommandPrompt =>
                    ShellCapabilities.Interactive | ShellCapabilities.Scripting | ShellCapabilities.Piping |
                    ShellCapabilities.History,

                _ => ShellCapabilities.Interactive | ShellCapabilities.Scripting
            };
        }

        /// <summary>
        /// 获取Shell类型的默认参数
        /// </summary>
        /// <param name="shellType">Shell类型</param>
        /// <returns>默认参数</returns>
        private static string[] GetDefaultParameters(ShellType shellType)
        {
            return shellType switch
            {
                ShellType.PowerShell51 or ShellType.PowerShellCore => new[] { \"-NoProfile\", \"-NoLogo\" },
                ShellType.GitBash => new[] { \"--login\", \"-i\" },
                ShellType.CommandPrompt => Array.Empty<string>(),
                _ => Array.Empty<string>()
            };
        }

        /// <summary>
        /// 获取Shell类型支持的默认编码格式
        /// </summary>
        /// <param name="shellType">Shell类型</param>
        /// <returns>支持的编码格式</returns>
        private static string[] GetDefaultEncodings(ShellType shellType)
        {
            return shellType switch
            {
                ShellType.PowerShell51 => new[] { \"UTF-8\", \"UTF-16\", \"GBK\", \"ASCII\" },
                ShellType.PowerShellCore => new[] { \"UTF-8\", \"UTF-16\", \"ASCII\" },
                ShellType.GitBash => new[] { \"UTF-8\", \"ASCII\" },
                ShellType.CommandPrompt => new[] { \"GBK\", \"UTF-8\", \"ASCII\" },
                _ => new[] { \"UTF-8\", \"ASCII\" }
            };
        }

        /// <summary>
        /// 获取Shell类型的默认编码
        /// </summary>
        /// <param name="shellType">Shell类型</param>
        /// <returns>默认编码</returns>
        private static string GetDefaultEncoding(ShellType shellType)
        {
            return shellType switch
            {
                ShellType.PowerShell51 => \"UTF-16\",
                ShellType.PowerShellCore or ShellType.GitBash => \"UTF-8\",
                ShellType.CommandPrompt => \"GBK\",
                _ => \"UTF-8\"
            };
        }

        /// <summary>
        /// 获取Shell是否为Windows原生
        /// </summary>
        /// <param name="shellType">Shell类型</param>
        /// <returns>是否为Windows原生</returns>
        private static bool GetIsNativeWindows(ShellType shellType)
        {
            return shellType == ShellType.PowerShell51 || shellType == ShellType.CommandPrompt;
        }

        /// <summary>
        /// 获取Shell是否支持ANSI颜色
        /// </summary>
        /// <param name="shellType">Shell类型</param>
        /// <returns>是否支持ANSI颜色</returns>
        private static bool GetSupportsAnsiColors(ShellType shellType)
        {
            return shellType switch
            {
                ShellType.PowerShellCore or ShellType.GitBash => true,
                ShellType.PowerShell51 => false, // 默认不支持，需要配置
                ShellType.CommandPrompt => false,
                _ => false
            };
        }

        /// <summary>
        /// 获取窗口标题设置命令
        /// </summary>
        /// <param name="shellType">Shell类型</param>
        /// <returns>窗口标题设置命令</returns>
        private static string? GetWindowTitleCommand(ShellType shellType)
        {
            return shellType switch
            {
                ShellType.PowerShell51 or ShellType.PowerShellCore => \"$Host.UI.RawUI.WindowTitle = '{0}'\",
                ShellType.GitBash => \"echo -ne \\\"\\033]0;{0}\\007\\\"\",
                ShellType.CommandPrompt => \"title {0}\",
                _ => null
            };
        }

        /// <summary>
        /// 获取清屏命令
        /// </summary>
        /// <param name="shellType">Shell类型</param>
        /// <returns>清屏命令</returns>
        private static string? GetClearCommand(ShellType shellType)
        {
            return shellType switch
            {
                ShellType.PowerShell51 or ShellType.PowerShellCore => \"Clear-Host\",
                ShellType.GitBash => \"clear\",
                ShellType.CommandPrompt => \"cls\",
                _ => \"clear\"
            };
        }

        /// <summary>
        /// 计算性能等级
        /// </summary>
        /// <param name="startupTimeMs">启动时间</param>
        /// <param name="responseTimeMs">响应时间</param>
        /// <param name="memoryUsageMB">内存使用量</param>
        /// <returns>性能等级</returns>
        private static ShellPerformanceLevel CalculatePerformanceLevel(int startupTimeMs, int responseTimeMs, double memoryUsageMB)
        {
            var startupScore = startupTimeMs switch
            {
                <= 100 => 5,
                <= 300 => 4,
                <= 800 => 3,
                <= 1500 => 2,
                _ => 1
            };

            var responseScore = responseTimeMs switch
            {
                <= 50 => 5,
                <= 100 => 4,
                <= 200 => 3,
                <= 500 => 2,
                _ => 1
            };

            var memoryScore = memoryUsageMB switch
            {
                <= 10 => 5,
                <= 25 => 4,
                <= 50 => 3,
                <= 100 => 2,
                _ => 1
            };

            var avgScore = (startupScore + responseScore + memoryScore) / 3.0;

            return avgScore switch
            {
                >= 4.5 => ShellPerformanceLevel.VeryHigh,
                >= 3.5 => ShellPerformanceLevel.High,
                >= 2.5 => ShellPerformanceLevel.Medium,
                >= 1.5 => ShellPerformanceLevel.Low,
                _ => ShellPerformanceLevel.Unknown
            };
        }

        /// <summary>
        /// 计算Shell综合评分
        /// </summary>
        /// <returns>综合评分（0-100）</returns>
        private double CalculateShellScore()
        {
            if (Status != DetectionStatus.Detected)
                return 0;

            double score = Priority; // 基础优先级分数

            // 能力加分
            var capabilityCount = Enum.GetValues<ShellCapabilities>()
                .Count(cap => cap != ShellCapabilities.None && HasCapability(cap));
            score += capabilityCount * 2;

            // 性能加分
            score += PerformanceLevel switch
            {
                ShellPerformanceLevel.VeryHigh => 20,
                ShellPerformanceLevel.High => 15,
                ShellPerformanceLevel.Medium => 10,
                ShellPerformanceLevel.Low => 5,
                _ => 0
            };

            // 兼容性加分
            if (IsVersionCompatible())
                score += 10;

            // Unicode支持加分
            if (SupportsAnsiColors)
                score += 5;

            return Math.Min(100, Math.Max(0, score));
        }

        /// <summary>
        /// 检查版本是否兼容
        /// </summary>
        /// <returns>是否兼容</returns>
        private bool IsVersionCompatible()
        {
            return ParsedVersion != null && MinimumCompatibleVersion != null &&
                   ParsedVersion >= MinimumCompatibleVersion;
        }

        #endregion
    }
}