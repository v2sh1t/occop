using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Occop.Core.Models.Environment;

namespace Occop.Core.Services.Environment
{
    /// <summary>
    /// Git Bash检测器
    /// </summary>
    public class GitBashDetector : ShellDetector
    {
        /// <summary>
        /// Shell类型
        /// </summary>
        protected override ShellType ShellType => ShellType.GitBash;

        /// <summary>
        /// 对应的环境类型
        /// </summary>
        protected override EnvironmentType EnvironmentType => EnvironmentType.GitBash;

        /// <summary>
        /// 检测Git Bash
        /// </summary>
        /// <param name=\"shellInfo\">Shell信息</param>
        /// <returns>检测任务</returns>
        protected override async Task DetectShellInternalAsync(ShellInfo shellInfo)
        {
            // 策略1: 通过Git安装路径查找Bash
            var gitBasedResult = await DetectViaGitPathAsync();
            if (gitBasedResult.found)
            {
                var gitVersion = await GetGitVersionAsync(gitBasedResult.gitPath);
                var bashVersion = await GetBashVersionAsync(gitBasedResult.bashPath);

                if (!string.IsNullOrEmpty(gitVersion) && !string.IsNullOrEmpty(bashVersion))
                {
                    var installPath = Path.GetDirectoryName(Path.GetDirectoryName(gitBasedResult.bashPath))!;
                    var configPath = GetGitBashConfigPath();

                    shellInfo.SetShellDetected(installPath, gitBasedResult.bashPath, gitVersion, configPath);
                    shellInfo.AddProperty(\"GitPath\", gitBasedResult.gitPath);
                    shellInfo.AddProperty(\"BashPath\", gitBasedResult.bashPath);
                    shellInfo.AddProperty(\"BashVersion\", bashVersion);
                    shellInfo.AddProperty(\"DetectionMethod\", \"GitPath\");

                    await ConfigureGitBashSpecific(shellInfo, gitBasedResult.gitPath);
                    return;
                }
            }

            // 策略2: 直接查找bash.exe
            var directBashResult = await DetectBashDirectlyAsync();
            if (directBashResult.found)
            {
                var bashVersion = await GetBashVersionAsync(directBashResult.bashPath);
                var associatedGitPath = await FindAssociatedGitAsync(directBashResult.bashPath);

                if (!string.IsNullOrEmpty(bashVersion))
                {
                    var installPath = Path.GetDirectoryName(Path.GetDirectoryName(directBashResult.bashPath))!;
                    var configPath = GetGitBashConfigPath();

                    shellInfo.SetShellDetected(installPath, directBashResult.bashPath, bashVersion, configPath);
                    shellInfo.AddProperty(\"BashPath\", directBashResult.bashPath);
                    shellInfo.AddProperty(\"BashVersion\", bashVersion);
                    shellInfo.AddProperty(\"DetectionMethod\", \"DirectBash\");

                    if (!string.IsNullOrEmpty(associatedGitPath))
                    {
                        var gitVersion = await GetGitVersionAsync(associatedGitPath);
                        shellInfo.AddProperty(\"GitPath\", associatedGitPath);
                        shellInfo.AddProperty(\"GitVersion\", gitVersion);
                        await ConfigureGitBashSpecific(shellInfo, associatedGitPath);
                    }

                    return;
                }
            }

            // 策略3: 检查常见安装路径
            var commonPathResult = await DetectViaCommonPathsAsync();
            if (commonPathResult.found)
            {
                var gitVersion = await GetGitVersionAsync(commonPathResult.gitPath);
                var bashVersion = await GetBashVersionAsync(commonPathResult.bashPath);

                if (!string.IsNullOrEmpty(gitVersion) && !string.IsNullOrEmpty(bashVersion))
                {
                    var installPath = Path.GetDirectoryName(Path.GetDirectoryName(commonPathResult.bashPath))!;
                    var configPath = GetGitBashConfigPath();

                    shellInfo.SetShellDetected(installPath, commonPathResult.bashPath, gitVersion, configPath);
                    shellInfo.AddProperty(\"GitPath\", commonPathResult.gitPath);
                    shellInfo.AddProperty(\"BashPath\", commonPathResult.bashPath);
                    shellInfo.AddProperty(\"BashVersion\", bashVersion);
                    shellInfo.AddProperty(\"DetectionMethod\", \"CommonPaths\");

                    await ConfigureGitBashSpecific(shellInfo, commonPathResult.gitPath);
                    return;
                }
            }

            shellInfo.SetFailed(\"未找到Git Bash安装\");
        }

        /// <summary>
        /// 配置Git Bash特定设置
        /// </summary>
        /// <param name=\"shellInfo\">Shell信息</param>
        /// <param name=\"gitPath\">Git可执行文件路径</param>
        /// <returns>配置任务</returns>
        private async Task ConfigureGitBashSpecific(ShellInfo shellInfo, string gitPath)
        {
            // 设置Git Bash特有的启动参数
            shellInfo.SetStartupParameters(\"--login\", \"-i\");

            // 获取Git配置信息
            var gitConfig = await GetGitConfigurationAsync(gitPath);
            foreach (var kvp in gitConfig)
            {
                shellInfo.AddProperty($\"Git.{kvp.Key}\", kvp.Value);
            }

            // 设置环境变量
            var gitInstallDir = Path.GetDirectoryName(Path.GetDirectoryName(gitPath));
            if (!string.IsNullOrEmpty(gitInstallDir))
            {
                shellInfo.AddEnvironmentVariable(\"GIT_INSTALL_ROOT\", gitInstallDir);

                // 检查MinGW路径
                var mingwPath = Path.Combine(gitInstallDir, \"mingw64\");
                if (Directory.Exists(mingwPath))
                {
                    shellInfo.AddEnvironmentVariable(\"MINGW_PREFIX\", mingwPath);
                    shellInfo.AddProperty(\"MinGWPath\", mingwPath);
                }
            }

            // 设置历史文件路径
            var homeDir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            var bashHistoryPath = Path.Combine(homeDir, \".bash_history\");
            if (File.Exists(bashHistoryPath))
            {
                shellInfo.HistoryPath = bashHistoryPath;
            }

            // 检查bash配置文件
            var bashConfigPaths = new[]
            {
                Path.Combine(homeDir, \".bashrc\"),
                Path.Combine(homeDir, \".bash_profile\"),
                Path.Combine(homeDir, \".profile\")
            };

            foreach (var configPath in bashConfigPaths)
            {
                if (File.Exists(configPath))
                {
                    shellInfo.AddProperty($\"ConfigFile.{Path.GetFileName(configPath)}\", configPath);
                }
            }

            // 检查支持的特性
            await CheckGitBashFeatures(shellInfo);
        }

        /// <summary>
        /// 通过Git路径检测
        /// </summary>
        /// <returns>检测结果</returns>
        private async Task<(bool found, string gitPath, string bashPath)> DetectViaGitPathAsync()
        {
            var gitResult = await FindExecutableInPathAsync(\"git.exe\");
            if (!gitResult.found)
                return (false, string.Empty, string.Empty);

            var gitDir = Path.GetDirectoryName(gitResult.path);
            if (string.IsNullOrEmpty(gitDir))
                return (false, string.Empty, string.Empty);

            // 查找bash.exe的常见相对路径
            var bashPaths = new[]
            {
                Path.Combine(gitDir, \"..\", \"bin\", \"bash.exe\"),
                Path.Combine(gitDir, \"..\", \"usr\", \"bin\", \"bash.exe\"),
                Path.Combine(gitDir, \"bash.exe\") // 某些安装可能在同一目录
            };

            foreach (var bashPath in bashPaths)
            {
                try
                {
                    var normalizedPath = Path.GetFullPath(bashPath);
                    if (File.Exists(normalizedPath))
                    {
                        return (true, gitResult.path, normalizedPath);
                    }
                }
                catch
                {
                    continue;
                }
            }

            return (false, string.Empty, string.Empty);
        }

        /// <summary>
        /// 直接检测Bash
        /// </summary>
        /// <returns>检测结果</returns>
        private async Task<(bool found, string bashPath)> DetectBashDirectlyAsync()
        {
            var bashResult = await FindExecutableInPathAsync(\"bash.exe\");
            if (bashResult.found)
            {
                // 验证这是Git Bash而不是其他Bash（如WSL）
                if (await IsGitBashAsync(bashResult.path))
                {
                    return (true, bashResult.path);
                }
            }

            return (false, string.Empty);
        }

        /// <summary>
        /// 通过常见路径检测
        /// </summary>
        /// <returns>检测结果</returns>
        private async Task<(bool found, string gitPath, string bashPath)> DetectViaCommonPathsAsync()
        {
            var commonGitPaths = new[]
            {
                @\"C:\\Program Files\\Git\\cmd\\git.exe\",
                @\"C:\\Program Files (x86)\\Git\\cmd\\git.exe\",
                Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                    @\"Programs\\Git\\cmd\\git.exe\")
            };

            foreach (var gitPath in commonGitPaths)
            {
                if (File.Exists(gitPath))
                {
                    var gitDir = Path.GetDirectoryName(gitPath);
                    var bashPaths = new[]
                    {
                        Path.Combine(Path.GetDirectoryName(gitDir)!, \"bin\", \"bash.exe\"),
                        Path.Combine(Path.GetDirectoryName(gitDir)!, \"usr\", \"bin\", \"bash.exe\")
                    };

                    foreach (var bashPath in bashPaths)
                    {
                        if (File.Exists(bashPath))
                        {
                            return (true, gitPath, bashPath);
                        }
                    }
                }
            }

            return (false, string.Empty, string.Empty);
        }

        /// <summary>
        /// 查找与Bash关联的Git
        /// </summary>
        /// <param name=\"bashPath\">Bash路径</param>
        /// <returns>Git路径</returns>
        private async Task<string?> FindAssociatedGitAsync(string bashPath)
        {
            try
            {
                var gitDir = Path.GetDirectoryName(Path.GetDirectoryName(bashPath));
                if (string.IsNullOrEmpty(gitDir))
                    return null;

                var gitPaths = new[]
                {
                    Path.Combine(gitDir, \"cmd\", \"git.exe\"),
                    Path.Combine(gitDir, \"bin\", \"git.exe\"),
                    Path.Combine(gitDir, \"mingw64\", \"bin\", \"git.exe\")
                };

                foreach (var gitPath in gitPaths)
                {
                    if (File.Exists(gitPath))
                    {
                        return gitPath;
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
        /// 检查是否为Git Bash
        /// </summary>
        /// <param name=\"bashPath\">Bash路径</param>
        /// <returns>是否为Git Bash</returns>
        private async Task<bool> IsGitBashAsync(string bashPath)
        {
            try
            {
                using var process = new Process();
                process.StartInfo.FileName = bashPath;
                process.StartInfo.Arguments = \"-c \\\"echo $TERM; echo $MSYSTEM\\\"\";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                // Git Bash通常会有MSYSTEM环境变量
                return output.Contains(\"MINGW\") || output.Contains(\"MSYS\") || bashPath.Contains(\"Git\");
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取Git版本
        /// </summary>
        /// <param name=\"gitPath\">Git可执行文件路径</param>
        /// <returns>版本字符串</returns>
        private async Task<string?> GetGitVersionAsync(string gitPath)
        {
            if (string.IsNullOrEmpty(gitPath) || !File.Exists(gitPath))
                return null;

            try
            {
                using var process = new Process();
                process.StartInfo.FileName = gitPath;
                process.StartInfo.Arguments = \"--version\";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    // 从\"git version 2.34.1.windows.1\"中提取版本号
                    var match = Regex.Match(output, @\"git version (\\d+\\.\\d+\\.\\d+)\");
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
        /// 获取Bash版本
        /// </summary>
        /// <param name=\"bashPath\">Bash可执行文件路径</param>
        /// <returns>版本字符串</returns>
        private async Task<string?> GetBashVersionAsync(string bashPath)
        {
            if (string.IsNullOrEmpty(bashPath) || !File.Exists(bashPath))
                return null;

            try
            {
                using var process = new Process();
                process.StartInfo.FileName = bashPath;
                process.StartInfo.Arguments = \"--version\";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    // 从\"GNU bash, version 4.4.23(1)-release\"中提取版本号
                    var match = Regex.Match(output, @\"version (\\d+\\.\\d+\\.\\d+)\");
                    return match.Success ? match.Groups[1].Value : output.Split('\\n')[0].Trim();
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 获取Git配置
        /// </summary>
        /// <param name=\"gitPath\">Git路径</param>
        /// <returns>配置字典</returns>
        private async Task<Dictionary<string, string>> GetGitConfigurationAsync(string gitPath)
        {
            var config = new Dictionary<string, string>();

            if (string.IsNullOrEmpty(gitPath) || !File.Exists(gitPath))
                return config;

            try
            {
                // 获取用户名和邮箱
                var userName = await GetGitConfigValueAsync(gitPath, \"user.name\");
                if (!string.IsNullOrEmpty(userName))
                    config[\"UserName\"] = userName;

                var userEmail = await GetGitConfigValueAsync(gitPath, \"user.email\");
                if (!string.IsNullOrEmpty(userEmail))
                    config[\"UserEmail\"] = userEmail;

                // 获取默认分支
                var defaultBranch = await GetGitConfigValueAsync(gitPath, \"init.defaultBranch\");
                if (!string.IsNullOrEmpty(defaultBranch))
                    config[\"DefaultBranch\"] = defaultBranch;

                // 获取核心编辑器
                var coreEditor = await GetGitConfigValueAsync(gitPath, \"core.editor\");
                if (!string.IsNullOrEmpty(coreEditor))
                    config[\"CoreEditor\"] = coreEditor;
            }
            catch
            {
                // 配置获取失败不影响检测
            }

            return config;
        }

        /// <summary>
        /// 获取Git配置值
        /// </summary>
        /// <param name=\"gitPath\">Git路径</param>
        /// <param name=\"configKey\">配置键</param>
        /// <returns>配置值</returns>
        private async Task<string?> GetGitConfigValueAsync(string gitPath, string configKey)
        {
            try
            {
                using var process = new Process();
                process.StartInfo.FileName = gitPath;
                process.StartInfo.Arguments = $\"config --global {configKey}\";
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
        /// 获取Git Bash配置路径
        /// </summary>
        /// <returns>配置路径</returns>
        private static string? GetGitBashConfigPath()
        {
            try
            {
                var homeDir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
                var bashrcPath = Path.Combine(homeDir, \".bashrc\");
                return File.Exists(bashrcPath) ? bashrcPath : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 检查Git Bash特性
        /// </summary>
        /// <param name=\"shellInfo\">Shell信息</param>
        /// <returns>检查任务</returns>
        private async Task CheckGitBashFeatures(ShellInfo shellInfo)
        {
            try
            {
                // 检查是否支持颜色输出
                var supportsColors = await TestBashCommandAsync(shellInfo.ExecutablePath!, \"tput colors\");
                if (supportsColors)
                {
                    shellInfo.AddCapability(ShellCapabilities.SyntaxHighlighting);
                }

                // 检查是否支持readline
                var supportsReadline = await TestBashCommandAsync(shellInfo.ExecutablePath!, \"bind -V\");
                if (supportsReadline)
                {
                    shellInfo.AddCapability(ShellCapabilities.AutoCompletion);
                }

                // 检查是否支持历史展开
                var supportsHistory = await TestBashCommandAsync(shellInfo.ExecutablePath!, \"history 1\");
                if (supportsHistory)
                {
                    shellInfo.AddCapability(ShellCapabilities.History);
                }
            }
            catch
            {
                // 特性检查失败不影响基本检测
            }
        }

        /// <summary>
        /// 测试Bash命令
        /// </summary>
        /// <param name=\"bashPath\">Bash路径</param>
        /// <param name=\"command\">要测试的命令</param>
        /// <returns>命令是否成功执行</returns>
        private async Task<bool> TestBashCommandAsync(string bashPath, string command)
        {
            try
            {
                using var process = new Process();
                process.StartInfo.FileName = bashPath;
                process.StartInfo.Arguments = $\"-c \\\"{command}\\\"\";
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

        /// <summary>
        /// 重写测试参数
        /// </summary>
        /// <returns>测试参数</returns>
        protected override string GetTestArguments()
        {
            return \"-c \\\"echo 'Git Bash Test'\\\"\";
        }

        /// <summary>
        /// 重写配置填充方法
        /// </summary>
        /// <param name=\"shellPath\">Shell路径</param>
        /// <param name=\"config\">配置字典</param>
        /// <returns>填充任务</returns>
        protected override async Task PopulateShellConfigurationAsync(string shellPath, Dictionary<string, string> config)
        {
            await base.PopulateShellConfigurationAsync(shellPath, config);

            // 添加Git Bash特有的配置
            config[\"ShellName\"] = \"Git Bash\";
            config[\"Terminal\"] = \"MinTTY\";

            // 检查MSYS版本
            try
            {
                using var process = new Process();
                process.StartInfo.FileName = shellPath;
                process.StartInfo.Arguments = \"-c \\\"uname -r\\\"\";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    config[\"MSYSVersion\"] = output.Trim();
                }
            }
            catch
            {
                // 忽略获取失败
            }
        }
    }
}