using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Occop.UI.Models;

namespace Occop.UI.Services
{
    /// <summary>
    /// Interface for settings management
    /// 设置管理接口
    /// </summary>
    public interface ISettingsManager : IDisposable
    {
        /// <summary>
        /// Event raised when settings are changed
        /// 设置更改时触发的事件
        /// </summary>
        event EventHandler<AppSettings>? SettingsChanged;

        /// <summary>
        /// Gets the current application settings
        /// 获取当前应用程序设置
        /// </summary>
        AppSettings CurrentSettings { get; }

        /// <summary>
        /// Load settings from storage
        /// 从存储加载设置
        /// </summary>
        /// <returns>A task representing the async operation</returns>
        Task<AppSettings> LoadSettingsAsync();

        /// <summary>
        /// Save settings to storage
        /// 保存设置到存储
        /// </summary>
        /// <param name="settings">The settings to save</param>
        /// <returns>A task representing the async operation</returns>
        Task SaveSettingsAsync(AppSettings settings);

        /// <summary>
        /// Reset settings to default values
        /// 重置设置为默认值
        /// </summary>
        /// <returns>A task representing the async operation</returns>
        Task ResetSettingsAsync();

        /// <summary>
        /// Validate settings
        /// 验证设置
        /// </summary>
        /// <param name="settings">The settings to validate</param>
        /// <returns>List of validation errors, empty if valid</returns>
        IReadOnlyList<string> ValidateSettings(AppSettings settings);

        /// <summary>
        /// Apply settings to the application
        /// 将设置应用到应用程序
        /// </summary>
        /// <param name="settings">The settings to apply</param>
        /// <returns>A task representing the async operation</returns>
        Task ApplySettingsAsync(AppSettings settings);

        /// <summary>
        /// Export settings to a file
        /// 导出设置到文件
        /// </summary>
        /// <param name="filePath">The file path to export to</param>
        /// <returns>A task representing the async operation</returns>
        Task ExportSettingsAsync(string filePath);

        /// <summary>
        /// Import settings from a file
        /// 从文件导入设置
        /// </summary>
        /// <param name="filePath">The file path to import from</param>
        /// <returns>A task representing the imported settings</returns>
        Task<AppSettings> ImportSettingsAsync(string filePath);
    }

    /// <summary>
    /// Implementation of settings management service
    /// 设置管理服务的实现
    /// </summary>
    public class SettingsManager : ISettingsManager
    {
        private readonly ILogger<SettingsManager> _logger;
        private readonly string _settingsFilePath;
        private readonly SemaphoreSlim _saveLock = new SemaphoreSlim(1, 1);
        private AppSettings _currentSettings;
        private bool _disposed = false;

        private const string AppName = "Occop";
        private const string SettingsFileName = "settings.json";
        private const string AutoStartRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        public event EventHandler<AppSettings>? SettingsChanged;

        /// <summary>
        /// Gets the current application settings
        /// 获取当前应用程序设置
        /// </summary>
        public AppSettings CurrentSettings
        {
            get => _currentSettings;
            private set
            {
                _currentSettings = value;
                SettingsChanged?.Invoke(this, value);
            }
        }

        /// <summary>
        /// Initializes a new instance of the SettingsManager class
        /// 初始化SettingsManager类的新实例
        /// </summary>
        /// <param name="logger">The logger instance</param>
        public SettingsManager(ILogger<SettingsManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Determine settings file path
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, AppName);
            Directory.CreateDirectory(appFolder);
            _settingsFilePath = Path.Combine(appFolder, SettingsFileName);

            _currentSettings = new AppSettings();
            _logger.LogInformation("SettingsManager initialized. Settings file: {FilePath}", _settingsFilePath);
        }

        /// <summary>
        /// Load settings from storage
        /// 从存储加载设置
        /// </summary>
        public async Task<AppSettings> LoadSettingsAsync()
        {
            try
            {
                if (!File.Exists(_settingsFilePath))
                {
                    _logger.LogInformation("Settings file not found, creating default settings");
                    var defaultSettings = new AppSettings();
                    await SaveSettingsAsync(defaultSettings);
                    CurrentSettings = defaultSettings;
                    return defaultSettings;
                }

                var json = await File.ReadAllTextAsync(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = true
                });

                if (settings == null)
                {
                    _logger.LogWarning("Failed to deserialize settings, using defaults");
                    settings = new AppSettings();
                }

                CurrentSettings = settings;
                _logger.LogInformation("Settings loaded successfully");
                return settings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading settings, using defaults");
                var defaultSettings = new AppSettings();
                CurrentSettings = defaultSettings;
                return defaultSettings;
            }
        }

        /// <summary>
        /// Save settings to storage
        /// 保存设置到存储
        /// </summary>
        public async Task SaveSettingsAsync(AppSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            await _saveLock.WaitAsync();
            try
            {
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(_settingsFilePath, json);
                CurrentSettings = settings;
                _logger.LogInformation("Settings saved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving settings");
                throw;
            }
            finally
            {
                _saveLock.Release();
            }
        }

        /// <summary>
        /// Reset settings to default values
        /// 重置设置为默认值
        /// </summary>
        public async Task ResetSettingsAsync()
        {
            try
            {
                var defaultSettings = new AppSettings();
                await SaveSettingsAsync(defaultSettings);
                _logger.LogInformation("Settings reset to defaults");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting settings");
                throw;
            }
        }

        /// <summary>
        /// Validate settings
        /// 验证设置
        /// </summary>
        public IReadOnlyList<string> ValidateSettings(AppSettings settings)
        {
            if (settings == null)
                return new[] { "设置对象不能为空" };

            var errors = new List<string>();

            // Validate window opacity
            if (settings.WindowOpacity < 0.0 || settings.WindowOpacity > 1.0)
            {
                errors.Add("窗口不透明度必须在0.0到1.0之间");
            }

            // Validate session timeout
            if (settings.SessionTimeoutMinutes < 1)
            {
                errors.Add("会话超时时间必须至少为1分钟");
            }

            // Validate max failed attempts
            if (settings.MaxFailedAttempts < 1)
            {
                errors.Add("最大失败尝试次数必须至少为1");
            }

            // Validate lockout duration
            if (settings.LockoutDurationMinutes < 1)
            {
                errors.Add("锁定持续时间必须至少为1分钟");
            }

            // Validate log retention
            if (settings.LogRetentionDays < 1)
            {
                errors.Add("日志保留天数必须至少为1天");
            }

            // Validate log level
            var validLogLevels = new[] { "Trace", "Debug", "Information", "Warning", "Error", "Critical" };
            if (!validLogLevels.Contains(settings.LogLevel))
            {
                errors.Add($"日志级别必须是以下之一：{string.Join(", ", validLogLevels)}");
            }

            // Validate notification duration
            if (settings.NotificationDuration < TimeSpan.FromSeconds(1))
            {
                errors.Add("通知持续时间必须至少为1秒");
            }

            return errors.AsReadOnly();
        }

        /// <summary>
        /// Apply settings to the application
        /// 将设置应用到应用程序
        /// </summary>
        public async Task ApplySettingsAsync(AppSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            try
            {
                _logger.LogInformation("Applying settings");

                // Apply startup settings
                await ApplyStartupSettingsAsync(settings);

                // Apply theme settings (would require theme manager integration)
                // This is a placeholder - actual implementation would integrate with ResourceDictionary
                _logger.LogDebug("Applying theme: {Theme}", settings.Theme);

                // Apply notification settings (would require notification manager integration)
                _logger.LogDebug("Applying notification settings");

                // Save the applied settings
                await SaveSettingsAsync(settings);

                _logger.LogInformation("Settings applied successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying settings");
                throw;
            }
        }

        /// <summary>
        /// Export settings to a file
        /// 导出设置到文件
        /// </summary>
        public async Task ExportSettingsAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be empty", nameof(filePath));

            try
            {
                var json = JsonSerializer.Serialize(CurrentSettings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(filePath, json);
                _logger.LogInformation("Settings exported to {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting settings to {FilePath}", filePath);
                throw;
            }
        }

        /// <summary>
        /// Import settings from a file
        /// 从文件导入设置
        /// </summary>
        public async Task<AppSettings> ImportSettingsAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be empty", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException("Settings file not found", filePath);

            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (settings == null)
                    throw new InvalidOperationException("Failed to deserialize settings");

                // Validate imported settings
                var errors = ValidateSettings(settings);
                if (errors.Count > 0)
                {
                    throw new InvalidOperationException($"导入的设置无效：{string.Join(", ", errors)}");
                }

                _logger.LogInformation("Settings imported from {FilePath}", filePath);
                return settings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing settings from {FilePath}", filePath);
                throw;
            }
        }

        #region Private Methods

        private async Task ApplyStartupSettingsAsync(AppSettings settings)
        {
            try
            {
                if (settings.StartWithWindows)
                {
                    await SetStartupAsync(true);
                }
                else
                {
                    await SetStartupAsync(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error applying startup settings");
            }
        }

        private Task SetStartupAsync(bool enable)
        {
            return Task.Run(() =>
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(AutoStartRegistryKey, true);
                    if (key == null)
                    {
                        _logger.LogWarning("Could not open registry key for auto-start");
                        return;
                    }

                    if (enable)
                    {
                        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                        if (!string.IsNullOrEmpty(exePath))
                        {
                            key.SetValue(AppName, $"\"{exePath}\"");
                            _logger.LogInformation("Auto-start enabled");
                        }
                    }
                    else
                    {
                        key.DeleteValue(AppName, false);
                        _logger.LogInformation("Auto-start disabled");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error setting auto-start");
                    throw;
                }
            });
        }

        #endregion

        #region IDisposable

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _saveLock?.Dispose();
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
