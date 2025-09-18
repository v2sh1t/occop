using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32;
using Occop.Core.Models.Environment;

namespace Occop.Core.Services.Environment
{
    /// <summary>
    /// PowerShell 5.1检测器
    /// </summary>
    public class PowerShell51Detector : ShellDetector
    {
        /// <summary>
        /// Shell类型
        /// </summary>
        protected override ShellType ShellType => ShellType.PowerShell51;

        /// <summary>
        /// 对应的环境类型
        /// </summary>
        protected override EnvironmentType EnvironmentType => EnvironmentType.PowerShell51;

        /// <summary>
        /// 检测PowerShell 5.1
        /// </summary>
        /// <param name=\"shellInfo\">Shell信息</param>
        /// <returns>检测任务</returns>
        protected override async Task DetectShellInternalAsync(ShellInfo shellInfo)
        {
            // 首先尝试从注册表查找PowerShell 5.1
            var ps51Path = GetPowerShell51FromRegistry();
            if (!string.IsNullOrEmpty(ps51Path) && File.Exists(ps51Path))
            {
                var version = await GetPowerShellVersionAsync(ps51Path);
                if (!string.IsNullOrEmpty(version) && IsPowerShell51Version(version))
                {
                    var installPath = Path.GetDirectoryName(ps51Path)!;
                    var configPath = GetPowerShell51ConfigPath();

                    shellInfo.SetShellDetected(installPath, ps51Path, version, configPath);
                    shellInfo.AddProperty(\"RegistrySource\", true);
                    shellInfo.AddProperty(\"PSEdition\", \"Desktop\");

                    // 设置PowerShell 5.1特有的配置
                    await ConfigurePowerShell51Specific(shellInfo);
                    return;
                }
            }

            // 尝试从PATH环境变量查找
            var pathResult = await FindExecutableInPathAsync(\"powershell.exe\");
            if (pathResult.found)
            {
                var version = await GetPowerShellVersionAsync(pathResult.path);
                if (!string.IsNullOrEmpty(version) && IsPowerShell51Version(version))
                {
                    var installPath = Path.GetDirectoryName(pathResult.path)!;
                    var configPath = GetPowerShell51ConfigPath();

                    shellInfo.SetShellDetected(installPath, pathResult.path, version, configPath);
                    shellInfo.AddProperty(\"PathSource\", true);
                    shellInfo.AddProperty(\"PSEdition\", \"Desktop\");

                    await ConfigurePowerShell51Specific(shellInfo);
                    return;
                }
            }

            shellInfo.SetFailed(\"未找到PowerShell 5.1安装\");
        }

        /// <summary>
        /// 配置PowerShell 5.1特定设置
        /// </summary>
        /// <param name=\"shellInfo\">Shell信息</param>
        /// <returns>配置任务</returns>
        private async Task ConfigurePowerShell51Specific(ShellInfo shellInfo)
        {
            // 设置PowerShell 5.1特有的启动参数
            shellInfo.SetStartupParameters(\"-NoProfile\", \"-NoLogo\", \"-ExecutionPolicy\", \"Bypass\");

            // 添加模块路径
            var modulePath = GetPowerShell51ModulePath();
            if (!string.IsNullOrEmpty(modulePath))
            {
                shellInfo.ModulePath = modulePath;
                shellInfo.AddProperty(\"PSModulePath\", modulePath);
            }

            // 添加历史文件路径
            var historyPath = GetPowerShell51HistoryPath();
            if (!string.IsNullOrEmpty(historyPath))
            {
                shellInfo.HistoryPath = historyPath;
            }

            // 检查执行策略
            var executionPolicy = await GetExecutionPolicyAsync(shellInfo.ExecutablePath!);
            if (!string.IsNullOrEmpty(executionPolicy))
            {
                shellInfo.AddProperty(\"ExecutionPolicy\", executionPolicy);
            }

            // 检查.NET Framework版本
            var dotNetVersion = GetDotNetFrameworkVersion();
            if (!string.IsNullOrEmpty(dotNetVersion))
            {
                shellInfo.AddProperty(\"DotNetVersion\", dotNetVersion);
            }
        }

        /// <summary>
        /// 从注册表获取PowerShell 5.1路径
        /// </summary>
        /// <returns>PowerShell 5.1路径</returns>
        private string? GetPowerShell51FromRegistry()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@\"SOFTWARE\\Microsoft\\PowerShell\\1\\ShellIds\\Microsoft.PowerShell\");
                return key?.GetValue(\"Path\") as string;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 获取PowerShell版本
        /// </summary>
        /// <param name=\"powershellPath\">PowerShell可执行文件路径</param>
        /// <returns>版本字符串</returns>
        private async Task<string?> GetPowerShellVersionAsync(string powershellPath)
        {
            try
            {
                using var process = new Process();
                process.StartInfo.FileName = powershellPath;
                process.StartInfo.Arguments = \"-NoProfile -Command \\\"$PSVersionTable.PSVersion.ToString()\\\"\";
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
        /// 检查版本是否为PowerShell 5.1
        /// </summary>
        /// <param name=\"version\">版本字符串</param>
        /// <returns>是否为PowerShell 5.1</returns>
        private static bool IsPowerShell51Version(string version)
        {
            return version.StartsWith(\"5.\") && !version.StartsWith(\"5.0.\");
        }

        /// <summary>
        /// 获取PowerShell 5.1配置路径
        /// </summary>
        /// <returns>配置路径</returns>
        private static string? GetPowerShell51ConfigPath()
        {
            try
            {
                var documentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
                var ps51ProfilePath = Path.Combine(documentsPath, \"WindowsPowerShell\", \"Microsoft.PowerShell_profile.ps1\");
                return Directory.Exists(Path.GetDirectoryName(ps51ProfilePath)) ? ps51ProfilePath : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 获取PowerShell 5.1模块路径
        /// </summary>
        /// <returns>模块路径</returns>
        private static string? GetPowerShell51ModulePath()
        {
            try
            {
                var documentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
                return Path.Combine(documentsPath, \"WindowsPowerShell\", \"Modules\");
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 获取PowerShell 5.1历史文件路径
        /// </summary>
        /// <returns>历史文件路径</returns>
        private static string? GetPowerShell51HistoryPath()
        {
            try
            {
                var appDataPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appDataPath, \"Microsoft\", \"Windows\", \"PowerShell\", \"PSReadline\", \"ConsoleHost_history.txt\");
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 获取执行策略
        /// </summary>
        /// <param name=\"powershellPath\">PowerShell路径</param>
        /// <returns>执行策略</returns>
        private async Task<string?> GetExecutionPolicyAsync(string powershellPath)
        {
            try
            {
                using var process = new Process();
                process.StartInfo.FileName = powershellPath;
                process.StartInfo.Arguments = \"-NoProfile -Command \\\"Get-ExecutionPolicy\\\"\";
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
        /// 获取.NET Framework版本
        /// </summary>
        /// <returns>.NET Framework版本</returns>
        private static string? GetDotNetFrameworkVersion()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@\"SOFTWARE\\Microsoft\\NET Framework Setup\\NDP\\v4\\Full\");
                var release = key?.GetValue(\"Release\") as int?;

                return release switch
                {
                    >= 533320 => \"4.8.1\",
                    >= 528040 => \"4.8\",
                    >= 461808 => \"4.7.2\",
                    >= 461308 => \"4.7.1\",
                    >= 460798 => \"4.7\",
                    >= 394802 => \"4.6.2\",
                    >= 394254 => \"4.6.1\",
                    >= 393295 => \"4.6\",
                    >= 379893 => \"4.5.2\",
                    >= 378675 => \"4.5.1\",
                    >= 378389 => \"4.5\",
                    _ => \"Unknown\"
                };
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// PowerShell Core检测器
    /// </summary>
    public class PowerShellCoreDetector : ShellDetector
    {
        /// <summary>
        /// Shell类型
        /// </summary>
        protected override ShellType ShellType => ShellType.PowerShellCore;

        /// <summary>
        /// 对应的环境类型
        /// </summary>
        protected override EnvironmentType EnvironmentType => EnvironmentType.PowerShellCore;

        /// <summary>
        /// 检测PowerShell Core
        /// </summary>
        /// <param name=\"shellInfo\">Shell信息</param>
        /// <returns>检测任务</returns>
        protected override async Task DetectShellInternalAsync(ShellInfo shellInfo)
        {
            // 尝试从PATH环境变量查找pwsh.exe
            var pathResult = await FindExecutableInPathAsync(\"pwsh.exe\");
            if (pathResult.found)
            {
                var version = await GetPowerShellCoreVersionAsync(pathResult.path);
                if (!string.IsNullOrEmpty(version) && IsPowerShellCoreVersion(version))
                {
                    var installPath = Path.GetDirectoryName(pathResult.path)!;
                    var configPath = GetPowerShellCoreConfigPath();

                    shellInfo.SetShellDetected(installPath, pathResult.path, version, configPath);
                    shellInfo.AddProperty(\"PathSource\", true);
                    shellInfo.AddProperty(\"PSEdition\", \"Core\");

                    await ConfigurePowerShellCoreSpecific(shellInfo);
                    return;
                }
            }

            // 尝试常见安装路径
            var commonPaths = new[]
            {
                @\"C:\\Program Files\\PowerShell\\7\\pwsh.exe\",
                @\"C:\\Program Files\\PowerShell\\6\\pwsh.exe\",
                Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                    @\"Microsoft\\powershell\\pwsh.exe\")
            };

            foreach (var path in commonPaths)
            {
                if (File.Exists(path))
                {
                    var version = await GetPowerShellCoreVersionAsync(path);
                    if (!string.IsNullOrEmpty(version) && IsPowerShellCoreVersion(version))
                    {
                        var installPath = Path.GetDirectoryName(path)!;
                        var configPath = GetPowerShellCoreConfigPath();

                        shellInfo.SetShellDetected(installPath, path, version, configPath);
                        shellInfo.AddProperty(\"CommonPathSource\", true);
                        shellInfo.AddProperty(\"PSEdition\", \"Core\");

                        await ConfigurePowerShellCoreSpecific(shellInfo);
                        return;
                    }
                }
            }

            shellInfo.SetFailed(\"未找到PowerShell Core安装\");
        }

        /// <summary>
        /// 配置PowerShell Core特定设置
        /// </summary>
        /// <param name=\"shellInfo\">Shell信息</param>
        /// <returns>配置任务</returns>
        private async Task ConfigurePowerShellCoreSpecific(ShellInfo shellInfo)
        {
            // 设置PowerShell Core特有的启动参数
            shellInfo.SetStartupParameters(\"-NoProfile\", \"-NoLogo\");

            // 添加模块路径
            var modulePath = GetPowerShellCoreModulePath();
            if (!string.IsNullOrEmpty(modulePath))
            {
                shellInfo.ModulePath = modulePath;
                shellInfo.AddProperty(\"PSModulePath\", modulePath);
            }

            // 添加历史文件路径
            var historyPath = GetPowerShellCoreHistoryPath();
            if (!string.IsNullOrEmpty(historyPath))
            {
                shellInfo.HistoryPath = historyPath;
            }

            // 检查PSEdition
            var psEdition = await GetPSEditionAsync(shellInfo.ExecutablePath!);
            if (!string.IsNullOrEmpty(psEdition))
            {
                shellInfo.AddProperty(\"PSEdition\", psEdition);
            }

            // 检查.NET版本
            var dotNetVersion = await GetDotNetVersionAsync(shellInfo.ExecutablePath!);
            if (!string.IsNullOrEmpty(dotNetVersion))
            {
                shellInfo.AddProperty(\"DotNetVersion\", dotNetVersion);
            }

            // 检查支持的特性
            await CheckPowerShellCoreFeatures(shellInfo);
        }

        /// <summary>
        /// 获取PowerShell Core版本
        /// </summary>
        /// <param name=\"pwshPath\">pwsh可执行文件路径</param>
        /// <returns>版本字符串</returns>
        private async Task<string?> GetPowerShellCoreVersionAsync(string pwshPath)
        {
            try
            {
                using var process = new Process();
                process.StartInfo.FileName = pwshPath;
                process.StartInfo.Arguments = \"-NoProfile -Command \\\"$PSVersionTable.PSVersion.ToString()\\\"\";
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
        /// 检查版本是否为PowerShell Core
        /// </summary>
        /// <param name=\"version\">版本字符串</param>
        /// <returns>是否为PowerShell Core</returns>
        private static bool IsPowerShellCoreVersion(string version)
        {
            return version.StartsWith(\"6.\") || version.StartsWith(\"7.\");
        }

        /// <summary>
        /// 获取PowerShell Core配置路径
        /// </summary>
        /// <returns>配置路径</returns>
        private static string? GetPowerShellCoreConfigPath()
        {
            try
            {
                var documentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
                var psCoreProfilePath = Path.Combine(documentsPath, \"PowerShell\", \"Microsoft.PowerShell_profile.ps1\");
                return Directory.Exists(Path.GetDirectoryName(psCoreProfilePath)) ? psCoreProfilePath : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 获取PowerShell Core模块路径
        /// </summary>
        /// <returns>模块路径</returns>
        private static string? GetPowerShellCoreModulePath()
        {
            try
            {
                var documentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
                return Path.Combine(documentsPath, \"PowerShell\", \"Modules\");
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 获取PowerShell Core历史文件路径
        /// </summary>
        /// <returns>历史文件路径</returns>
        private static string? GetPowerShellCoreHistoryPath()
        {
            try
            {
                var appDataPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appDataPath, \"Microsoft\", \"Windows\", \"PowerShell\", \"PSReadline\", \"ConsoleHost_history.txt\");
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 获取PSEdition
        /// </summary>
        /// <param name=\"pwshPath\">PowerShell路径</param>
        /// <returns>PSEdition</returns>
        private async Task<string?> GetPSEditionAsync(string pwshPath)
        {
            try
            {
                using var process = new Process();
                process.StartInfo.FileName = pwshPath;
                process.StartInfo.Arguments = \"-NoProfile -Command \\\"$PSVersionTable.PSEdition\\\"\";
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
        /// 获取.NET版本
        /// </summary>
        /// <param name=\"pwshPath\">PowerShell路径</param>
        /// <returns>.NET版本</returns>
        private async Task<string?> GetDotNetVersionAsync(string pwshPath)
        {
            try
            {
                using var process = new Process();
                process.StartInfo.FileName = pwshPath;
                process.StartInfo.Arguments = \"-NoProfile -Command \\\"[System.Runtime.InteropServices.RuntimeInformation]::FrameworkDescription\\\"\";
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
        /// 检查PowerShell Core特性
        /// </summary>
        /// <param name=\"shellInfo\">Shell信息</param>
        /// <returns>检查任务</returns>
        private async Task CheckPowerShellCoreFeatures(ShellInfo shellInfo)
        {
            try
            {
                // 检查是否支持JSON
                var supportsJson = await TestCommandAsync(shellInfo.ExecutablePath!, \"ConvertTo-Json @{test='value'}\");
                if (supportsJson)
                {
                    shellInfo.AddCapability(ShellCapabilities.ModuleManagement);
                }

                // 检查是否支持远程
                var supportsRemoting = await TestCommandAsync(shellInfo.ExecutablePath!, \"Get-Command Invoke-Command -ErrorAction SilentlyContinue\");
                if (supportsRemoting)
                {
                    shellInfo.AddCapability(ShellCapabilities.RemoteExecution);
                }
            }
            catch
            {
                // 特性检查失败不影响基本检测
            }
        }

        /// <summary>
        /// 测试PowerShell命令
        /// </summary>
        /// <param name=\"pwshPath\">PowerShell路径</param>
        /// <param name=\"command\">要测试的命令</param>
        /// <returns>命令是否成功执行</returns>
        private async Task<bool> TestCommandAsync(string pwshPath, string command)
        {
            try
            {
                using var process = new Process();
                process.StartInfo.FileName = pwshPath;
                process.StartInfo.Arguments = $\"-NoProfile -Command \\\"{command}\\\"\";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                process.Start();
                await process.WaitForExitAsync();

                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}