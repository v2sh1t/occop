using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using Occop.UI.Models;
using Occop.UI.Services;

namespace Occop.UI.ViewModels
{
    /// <summary>
    /// ViewModel for settings window
    /// 设置窗口的ViewModel
    /// </summary>
    public partial class SettingsViewModel : ObservableObject, IDisposable
    {
        private readonly ISettingsManager _settingsManager;
        private readonly ILogger<SettingsViewModel> _logger;
        private AppSettings _originalSettings;
        private bool _disposed = false;

        [ObservableProperty]
        private AppSettings _settings;

        [ObservableProperty]
        private bool _hasUnsavedChanges = false;

        [ObservableProperty]
        private string _statusMessage = "就绪";

        [ObservableProperty]
        private bool _isLoading = false;

        [ObservableProperty]
        private ObservableCollection<string> _validationErrors = new ObservableCollection<string>();

        #region Theme Options

        [ObservableProperty]
        private ObservableCollection<string> _themeOptions = new ObservableCollection<string>
        {
            "系统",
            "浅色",
            "深色"
        };

        [ObservableProperty]
        private string _selectedTheme = "系统";

        #endregion

        #region Startup Options

        [ObservableProperty]
        private ObservableCollection<string> _startupStateOptions = new ObservableCollection<string>
        {
            "正常",
            "最小化到任务栏",
            "最小化到托盘"
        };

        [ObservableProperty]
        private string _selectedStartupState = "正常";

        #endregion

        #region Log Level Options

        [ObservableProperty]
        private ObservableCollection<string> _logLevelOptions = new ObservableCollection<string>
        {
            "Trace",
            "Debug",
            "Information",
            "Warning",
            "Error",
            "Critical"
        };

        [ObservableProperty]
        private string _selectedLogLevel = "Information";

        #endregion

        #region Notification Priority Options

        [ObservableProperty]
        private ObservableCollection<string> _priorityOptions = new ObservableCollection<string>
        {
            "低",
            "正常",
            "高",
            "关键"
        };

        [ObservableProperty]
        private string _selectedMinimumPriority = "正常";

        #endregion

        public SettingsViewModel(
            ISettingsManager settingsManager,
            ILogger<SettingsViewModel> logger)
        {
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _settings = new AppSettings();
            _originalSettings = _settings.Clone();

            // Initialize commands
            LoadSettingsCommand = new AsyncRelayCommand(LoadSettingsAsync);
            SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync, CanSaveSettings);
            ResetSettingsCommand = new AsyncRelayCommand(ResetSettingsAsync);
            CancelCommand = new RelayCommand(Cancel);
            ExportSettingsCommand = new AsyncRelayCommand(ExportSettingsAsync);
            ImportSettingsCommand = new AsyncRelayCommand(ImportSettingsAsync);

            // Subscribe to settings changes
            _settingsManager.SettingsChanged += OnSettingsManagerChanged;
            _settings.PropertyChanged += OnSettingsPropertyChanged;

            _logger.LogInformation("SettingsViewModel initialized");
        }

        #region Commands

        public IAsyncRelayCommand LoadSettingsCommand { get; }
        public IAsyncRelayCommand SaveSettingsCommand { get; }
        public IAsyncRelayCommand ResetSettingsCommand { get; }
        public IRelayCommand CancelCommand { get; }
        public IAsyncRelayCommand ExportSettingsCommand { get; }
        public IAsyncRelayCommand ImportSettingsCommand { get; }

        #endregion

        #region Command Implementations

        private async Task LoadSettingsAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "正在加载设置...";
                ValidationErrors.Clear();

                var loadedSettings = await _settingsManager.LoadSettingsAsync();
                Settings = loadedSettings;
                _originalSettings = loadedSettings.Clone();

                // Sync UI selections
                SyncSettingsToUI();

                HasUnsavedChanges = false;
                StatusMessage = "设置加载成功";
                _logger.LogInformation("Settings loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading settings");
                StatusMessage = $"加载设置失败：{ex.Message}";
                ValidationErrors.Add($"加载设置失败：{ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task SaveSettingsAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "正在保存设置...";
                ValidationErrors.Clear();

                // Sync UI selections to settings
                SyncUIToSettings();

                // Validate settings
                var errors = _settingsManager.ValidateSettings(Settings);
                if (errors.Count > 0)
                {
                    foreach (var error in errors)
                    {
                        ValidationErrors.Add(error);
                    }
                    StatusMessage = "设置验证失败";
                    return;
                }

                // Apply and save settings
                await _settingsManager.ApplySettingsAsync(Settings);
                _originalSettings = Settings.Clone();

                HasUnsavedChanges = false;
                StatusMessage = "设置保存成功";
                _logger.LogInformation("Settings saved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving settings");
                StatusMessage = $"保存设置失败：{ex.Message}";
                ValidationErrors.Add($"保存设置失败：{ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private bool CanSaveSettings()
        {
            return HasUnsavedChanges && !IsLoading;
        }

        private async Task ResetSettingsAsync()
        {
            try
            {
                var result = System.Windows.MessageBox.Show(
                    "确定要将所有设置重置为默认值吗？此操作无法撤销。",
                    "确认重置",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (result != System.Windows.MessageBoxResult.Yes)
                    return;

                IsLoading = true;
                StatusMessage = "正在重置设置...";
                ValidationErrors.Clear();

                await _settingsManager.ResetSettingsAsync();
                await LoadSettingsAsync();

                StatusMessage = "设置已重置为默认值";
                _logger.LogInformation("Settings reset to defaults");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting settings");
                StatusMessage = $"重置设置失败：{ex.Message}";
                ValidationErrors.Add($"重置设置失败：{ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void Cancel()
        {
            try
            {
                if (HasUnsavedChanges)
                {
                    var result = System.Windows.MessageBox.Show(
                        "有未保存的更改。确定要放弃这些更改吗？",
                        "确认取消",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Question);

                    if (result != System.Windows.MessageBoxResult.Yes)
                        return;
                }

                // Restore original settings
                Settings.CopyFrom(_originalSettings);
                SyncSettingsToUI();

                HasUnsavedChanges = false;
                StatusMessage = "已取消更改";
                ValidationErrors.Clear();
                _logger.LogInformation("Settings changes cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling settings");
                StatusMessage = $"取消失败：{ex.Message}";
            }
        }

        private async Task ExportSettingsAsync()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "导出设置",
                    Filter = "JSON文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                    DefaultExt = ".json",
                    FileName = $"occop_settings_{DateTime.Now:yyyyMMdd_HHmmss}.json"
                };

                if (dialog.ShowDialog() != true)
                    return;

                IsLoading = true;
                StatusMessage = "正在导出设置...";

                await _settingsManager.ExportSettingsAsync(dialog.FileName);

                StatusMessage = $"设置已导出到：{dialog.FileName}";
                _logger.LogInformation("Settings exported to {FilePath}", dialog.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting settings");
                StatusMessage = $"导出设置失败：{ex.Message}";
                ValidationErrors.Add($"导出设置失败：{ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ImportSettingsAsync()
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "导入设置",
                    Filter = "JSON文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                    DefaultExt = ".json"
                };

                if (dialog.ShowDialog() != true)
                    return;

                IsLoading = true;
                StatusMessage = "正在导入设置...";
                ValidationErrors.Clear();

                var importedSettings = await _settingsManager.ImportSettingsAsync(dialog.FileName);
                Settings = importedSettings;
                SyncSettingsToUI();

                HasUnsavedChanges = true;
                StatusMessage = $"设置已从 {dialog.FileName} 导入，请点击保存以应用";
                _logger.LogInformation("Settings imported from {FilePath}", dialog.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing settings");
                StatusMessage = $"导入设置失败：{ex.Message}";
                ValidationErrors.Add($"导入设置失败：{ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion

        #region Private Methods

        private void SyncSettingsToUI()
        {
            // Theme
            SelectedTheme = Settings.Theme switch
            {
                AppTheme.Light => "浅色",
                AppTheme.Dark => "深色",
                _ => "系统"
            };

            // Startup state
            SelectedStartupState = Settings.WindowStartupState switch
            {
                WindowStartupState.Minimized => "最小化到任务栏",
                WindowStartupState.MinimizedToTray => "最小化到托盘",
                _ => "正常"
            };

            // Log level
            SelectedLogLevel = Settings.LogLevel;

            // Notification priority
            SelectedMinimumPriority = Settings.MinimumNotificationPriority switch
            {
                NotificationPriority.Low => "低",
                NotificationPriority.High => "高",
                NotificationPriority.Critical => "关键",
                _ => "正常"
            };
        }

        private void SyncUIToSettings()
        {
            // Theme
            Settings.Theme = SelectedTheme switch
            {
                "浅色" => AppTheme.Light,
                "深色" => AppTheme.Dark,
                _ => AppTheme.System
            };

            // Startup state
            Settings.WindowStartupState = SelectedStartupState switch
            {
                "最小化到任务栏" => WindowStartupState.Minimized,
                "最小化到托盘" => WindowStartupState.MinimizedToTray,
                _ => WindowStartupState.Normal
            };

            // Log level
            Settings.LogLevel = SelectedLogLevel;

            // Notification priority
            Settings.MinimumNotificationPriority = SelectedMinimumPriority switch
            {
                "低" => NotificationPriority.Low,
                "高" => NotificationPriority.High,
                "关键" => NotificationPriority.Critical,
                _ => NotificationPriority.Normal
            };
        }

        private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            HasUnsavedChanges = true;
            SaveSettingsCommand.NotifyCanExecuteChanged();
        }

        private void OnSettingsManagerChanged(object? sender, AppSettings e)
        {
            // Settings were changed externally, reload
            _logger.LogInformation("Settings changed externally, reloading");
            _ = LoadSettingsAsync();
        }

        #endregion

        #region IDisposable

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _settingsManager.SettingsChanged -= OnSettingsManagerChanged;
                    _settings.PropertyChanged -= OnSettingsPropertyChanged;
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
