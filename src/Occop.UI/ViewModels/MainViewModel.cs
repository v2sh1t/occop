using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Occop.Core.Authentication;
using Occop.Services;
using System.Windows.Input;
using System.ComponentModel;
using System.Windows.Media;
using System.Reflection;

namespace Occop.UI.ViewModels
{
    /// <summary>
    /// ViewModel for main window interface
    /// 主窗口界面的ViewModel
    /// </summary>
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        private readonly AuthenticationManager _authenticationManager;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MainViewModel> _logger;
        private readonly ITrayManager? _trayManager;
        private bool _disposed = false;

        [ObservableProperty]
        private string _userDisplayName = "未登录";

        [ObservableProperty]
        private string _authenticationStatusText = "未认证";

        [ObservableProperty]
        private string _authenticationDetails = "需要进行GitHub认证以使用此应用程序";

        [ObservableProperty]
        private string _authenticationButtonText = "登录";

        [ObservableProperty]
        private string _claudeStatusText = "离线";

        [ObservableProperty]
        private Brush _claudeStatusColor = Brushes.Gray;

        [ObservableProperty]
        private string _otherToolsStatusText = "未配置";

        [ObservableProperty]
        private Brush _otherToolsStatusColor = Brushes.Gray;

        [ObservableProperty]
        private string _cleanupStatusText = "准备就绪";

        [ObservableProperty]
        private string _lastCleanupInfo = "尚未执行过清理";

        [ObservableProperty]
        private string _statusMessage = "就绪";

        [ObservableProperty]
        private string _applicationVersion = "";

        [ObservableProperty]
        private StatusViewModel? _statusViewModel;

        public MainViewModel(
            AuthenticationManager authenticationManager,
            IServiceProvider serviceProvider,
            ILogger<MainViewModel> logger)
        {
            _authenticationManager = authenticationManager ?? throw new ArgumentNullException(nameof(authenticationManager));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Try to get TrayManager (optional dependency)
            _trayManager = _serviceProvider.GetService<ITrayManager>();

            // Subscribe to authentication events
            _authenticationManager.AuthenticationStateChanged += OnAuthenticationStateChanged;
            _authenticationManager.AuthenticationFailed += OnAuthenticationFailed;
            _authenticationManager.SessionExpired += OnSessionExpired;

            // Initialize commands
            ManageAuthenticationCommand = new AsyncRelayCommand(ManageAuthenticationAsync);
            ManageToolsCommand = new AsyncRelayCommand(ManageToolsAsync);
            StartCleanupCommand = new AsyncRelayCommand(StartCleanupAsync);
            OpenSettingsCommand = new AsyncRelayCommand(OpenSettingsAsync);

            // Initialize state
            InitializeApplicationInfo();
            UpdateAuthenticationState();
            UpdateToolsStatus();
            UpdateCleanupStatus();
            UpdateTrayStatus();

            _logger.LogInformation("MainViewModel initialized");
        }

        #region Commands

        public IAsyncRelayCommand ManageAuthenticationCommand { get; }
        public IAsyncRelayCommand ManageToolsCommand { get; }
        public IAsyncRelayCommand StartCleanupCommand { get; }
        public IAsyncRelayCommand OpenSettingsCommand { get; }

        #endregion

        #region Command Implementation

        private async Task ManageAuthenticationAsync()
        {
            try
            {
                _logger.LogInformation("Managing authentication");

                if (_authenticationManager.IsAuthenticated)
                {
                    // Show sign out confirmation
                    var result = System.Windows.MessageBox.Show(
                        "确定要注销当前用户吗？",
                        "确认注销",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Question);

                    if (result == System.Windows.MessageBoxResult.Yes)
                    {
                        await SignOutAsync();
                    }
                }
                else
                {
                    // Show login window
                    await ShowLoginWindowAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error managing authentication");
                StatusMessage = $"认证管理错误：{ex.Message}";
            }
        }

        private async Task ManageToolsAsync()
        {
            try
            {
                _logger.LogInformation("Managing tools");
                StatusMessage = "工具管理功能即将推出...";

                // TODO: Implement tools management window
                await Task.Delay(100); // Placeholder
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error managing tools");
                StatusMessage = $"工具管理错误：{ex.Message}";
            }
        }

        private async Task StartCleanupAsync()
        {
            try
            {
                _logger.LogInformation("Starting cleanup");
                StatusMessage = "系统清理功能即将推出...";

                // TODO: Implement cleanup functionality
                await Task.Delay(100); // Placeholder
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting cleanup");
                StatusMessage = $"清理错误：{ex.Message}";
            }
        }

        private async Task OpenSettingsAsync()
        {
            try
            {
                _logger.LogInformation("Opening settings");

                var settingsWindow = _serviceProvider.GetRequiredService<SettingsWindow>();
                var currentWindow = System.Windows.Application.Current.MainWindow;

                var settingsResult = settingsWindow.ShowSettingsDialog(currentWindow);

                if (settingsResult)
                {
                    _logger.LogInformation("Settings saved successfully");
                    StatusMessage = "设置已保存";
                }
                else
                {
                    _logger.LogInformation("Settings cancelled or closed");
                    StatusMessage = "设置已取消";
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening settings");
                StatusMessage = $"设置错误：{ex.Message}";
            }
        }

        #endregion

        #region Private Methods

        private async Task ShowLoginWindowAsync()
        {
            try
            {
                var loginWindow = _serviceProvider.GetRequiredService<LoginWindow>();
                var currentWindow = System.Windows.Application.Current.MainWindow;

                var loginResult = loginWindow.ShowLoginDialog(currentWindow);

                if (loginResult)
                {
                    _logger.LogInformation("Login successful");
                    StatusMessage = "登录成功！";
                    UpdateAuthenticationState();
                }
                else
                {
                    _logger.LogInformation("Login cancelled or failed");
                    StatusMessage = "登录已取消或失败";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing login window");
                StatusMessage = $"登录窗口错误：{ex.Message}";
            }
        }

        private async Task SignOutAsync()
        {
            try
            {
                _logger.LogInformation("Signing out user");
                StatusMessage = "正在注销...";

                // Clear authentication state
                // Note: AuthenticationManager doesn't have a SignOut method, so we simulate it
                UserDisplayName = "未登录";
                AuthenticationStatusText = "未认证";
                AuthenticationDetails = "需要进行GitHub认证以使用此应用程序";
                AuthenticationButtonText = "登录";

                StatusMessage = "已成功注销";
                _logger.LogInformation("User signed out successfully");

                await Task.Delay(100); // Placeholder for actual sign out logic
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during sign out");
                StatusMessage = $"注销错误：{ex.Message}";
            }
        }

        private void InitializeApplicationInfo()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                ApplicationVersion = $"版本 {version?.ToString(3) ?? "1.0.0"}";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not get application version");
                ApplicationVersion = "版本 1.0.0";
            }
        }

        private void UpdateAuthenticationState()
        {
            if (_authenticationManager.IsAuthenticated)
            {
                UserDisplayName = _authenticationManager.CurrentUserLogin ?? "已认证用户";
                AuthenticationStatusText = "已认证";
                AuthenticationDetails = $"当前用户：{UserDisplayName}";
                AuthenticationButtonText = "注销";
            }
            else
            {
                UserDisplayName = "未登录";
                AuthenticationStatusText = "未认证";
                AuthenticationDetails = "需要进行GitHub认证以使用此应用程序";
                AuthenticationButtonText = "登录";
            }
        }

        private void UpdateToolsStatus()
        {
            // TODO: Implement actual tools status checking
            ClaudeStatusText = "未连接";
            ClaudeStatusColor = Brushes.Orange;

            OtherToolsStatusText = "未配置";
            OtherToolsStatusColor = Brushes.Gray;
        }

        private void UpdateCleanupStatus()
        {
            // TODO: Implement actual cleanup status checking
            CleanupStatusText = "准备就绪";
            LastCleanupInfo = "尚未执行过清理操作";
        }

        private void UpdateTrayStatus()
        {
            try
            {
                if (_trayManager == null) return;

                // Determine tray status based on current application state
                TrayStatus status = TrayStatus.Idle;

                if (_authenticationManager.IsAuthenticated)
                {
                    // Check if tools are connected/working
                    if (ClaudeStatusText == "已连接" || ClaudeStatusText == "工作中")
                    {
                        status = TrayStatus.Working;
                    }
                    else if (ClaudeStatusText == "离线" || ClaudeStatusText == "未连接")
                    {
                        status = TrayStatus.Disconnected;
                    }
                    else
                    {
                        status = TrayStatus.Ready;
                    }
                }
                else
                {
                    status = TrayStatus.Disconnected;
                }

                _trayManager.UpdateStatus(status);
                _logger.LogDebug("Tray status updated to: {Status}", status);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update tray status");
            }
        }

        #endregion

        #region Event Handlers

        private void OnAuthenticationStateChanged(object? sender, AuthenticationStateChangedEventArgs e)
        {
            // Ensure UI updates happen on UI thread
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                UpdateAuthenticationState();
                UpdateTrayStatus();

                switch (e.NewState)
                {
                    case AuthenticationState.Authenticated:
                        StatusMessage = $"欢迎，{UserDisplayName}！";
                        _trayManager?.ShowBalloonTip("Occop", $"用户 {UserDisplayName} 已登录", System.Windows.Forms.ToolTipIcon.Info);
                        break;

                    case AuthenticationState.NotAuthenticated:
                        StatusMessage = "尚未认证";
                        break;

                    case AuthenticationState.Authenticating:
                        StatusMessage = "正在进行认证...";
                        break;

                    case AuthenticationState.LockedOut:
                        StatusMessage = "账户已被锁定";
                        _trayManager?.ShowBalloonTip("Occop", "账户已被锁定", System.Windows.Forms.ToolTipIcon.Warning);
                        break;
                }

                // Update command states
                ManageAuthenticationCommand.NotifyCanExecuteChanged();
            });
        }

        private void OnAuthenticationFailed(object? sender, AuthenticationFailedEventArgs e)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                StatusMessage = $"认证失败：{e.Reason}";
                UpdateAuthenticationState();
                UpdateTrayStatus();
                _trayManager?.ShowBalloonTip("Occop", $"认证失败：{e.Reason}", System.Windows.Forms.ToolTipIcon.Error);
            });
        }

        private void OnSessionExpired(object? sender, SessionExpiredEventArgs e)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                StatusMessage = "会话已过期，请重新认证";
                UpdateAuthenticationState();
                UpdateTrayStatus();
                _trayManager?.ShowBalloonTip("Occop", "会话已过期，请重新认证", System.Windows.Forms.ToolTipIcon.Warning);
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
                    // Unsubscribe from events
                    _authenticationManager.AuthenticationStateChanged -= OnAuthenticationStateChanged;
                    _authenticationManager.AuthenticationFailed -= OnAuthenticationFailed;
                    _authenticationManager.SessionExpired -= OnSessionExpired;
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