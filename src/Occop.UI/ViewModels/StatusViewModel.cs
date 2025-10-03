using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using Occop.Services.Authentication;
using Occop.UI.Models;
using Occop.UI.Services;
using Occop.Services;

namespace Occop.UI.ViewModels
{
    /// <summary>
    /// ViewModel for status display and management
    /// 状态显示和管理的ViewModel
    /// </summary>
    public partial class StatusViewModel : ObservableObject, IDisposable
    {
        private readonly AuthenticationManager _authenticationManager;
        private readonly INotificationManager _notificationManager;
        private readonly ITrayManager? _trayManager;
        private readonly ILogger<StatusViewModel> _logger;
        private bool _disposed = false;

        [ObservableProperty]
        private StatusModel _authenticationStatus;

        [ObservableProperty]
        private StatusModel _claudeStatus;

        [ObservableProperty]
        private StatusModel _systemCleanupStatus;

        [ObservableProperty]
        private StatusModel _networkStatus;

        [ObservableProperty]
        private ObservableCollection<StatusModel> _allStatuses;

        [ObservableProperty]
        private ObservableCollection<NotificationModel> _recentNotifications;

        [ObservableProperty]
        private string _globalStatusMessage = "系统就绪";

        [ObservableProperty]
        private bool _hasUnreadNotifications = false;

        [ObservableProperty]
        private int _unreadNotificationCount = 0;

        [ObservableProperty]
        private bool _isStatusPanelExpanded = false;

        /// <summary>
        /// Initializes a new instance of the StatusViewModel class
        /// 初始化StatusViewModel类的新实例
        /// </summary>
        /// <param name="authenticationManager">Authentication manager</param>
        /// <param name="notificationManager">Notification manager</param>
        /// <param name="trayManager">Tray manager (optional)</param>
        /// <param name="logger">Logger instance</param>
        public StatusViewModel(
            AuthenticationManager authenticationManager,
            INotificationManager notificationManager,
            ITrayManager? trayManager,
            ILogger<StatusViewModel> logger)
        {
            _authenticationManager = authenticationManager ?? throw new ArgumentNullException(nameof(authenticationManager));
            _notificationManager = notificationManager ?? throw new ArgumentNullException(nameof(notificationManager));
            _trayManager = trayManager;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initialize status models
            _authenticationStatus = new StatusModel(StatusType.Authentication, StatusState.Unknown, "认证状态", "正在检查认证状态...");
            _claudeStatus = new StatusModel(StatusType.AITool, StatusState.Unknown, "Claude Code", "正在检查连接状态...");
            _systemCleanupStatus = new StatusModel(StatusType.SystemCleanup, StatusState.Ready, "系统清理", "准备就绪");
            _networkStatus = new StatusModel(StatusType.Network, StatusState.Unknown, "网络连接", "正在检查网络状态...");

            _allStatuses = new ObservableCollection<StatusModel>
            {
                _authenticationStatus,
                _claudeStatus,
                _systemCleanupStatus,
                _networkStatus
            };

            _recentNotifications = new ObservableCollection<NotificationModel>();

            // Initialize commands
            ToggleStatusPanelCommand = new RelayCommand(ToggleStatusPanel);
            RefreshAllStatusCommand = new AsyncRelayCommand(RefreshAllStatusAsync);
            ClearNotificationsCommand = new RelayCommand(ClearNotifications);
            MarkAllNotificationsReadCommand = new RelayCommand(MarkAllNotificationsRead);

            // Subscribe to events
            _authenticationManager.AuthenticationStateChanged += OnAuthenticationStateChanged;
            _authenticationManager.AuthenticationFailed += OnAuthenticationFailed;
            _authenticationManager.SessionExpired += OnSessionExpired;

            _notificationManager.NotificationAdded += OnNotificationAdded;
            _notificationManager.NotificationRemoved += OnNotificationRemoved;
            _notificationManager.NotificationUpdated += OnNotificationUpdated;

            // Initialize status
            Task.Run(async () =>
            {
                await InitializeStatusAsync();
            });

            _logger.LogInformation("StatusViewModel initialized");
        }

        #region Commands

        public ICommand ToggleStatusPanelCommand { get; }
        public IAsyncRelayCommand RefreshAllStatusCommand { get; }
        public ICommand ClearNotificationsCommand { get; }
        public ICommand MarkAllNotificationsReadCommand { get; }

        #endregion

        #region Command Implementations

        private void ToggleStatusPanel()
        {
            IsStatusPanelExpanded = !IsStatusPanelExpanded;
            _logger.LogDebug("Status panel toggled: {IsExpanded}", IsStatusPanelExpanded);
        }

        private async Task RefreshAllStatusAsync()
        {
            try
            {
                _logger.LogInformation("Refreshing all status information");
                GlobalStatusMessage = "正在刷新状态...";

                await UpdateAuthenticationStatusAsync();
                await UpdateClaudeStatusAsync();
                await UpdateSystemCleanupStatusAsync();
                await UpdateNetworkStatusAsync();

                GlobalStatusMessage = "状态已更新";
                _notificationManager.ShowInfo("状态更新", "所有状态信息已刷新");

                _logger.LogInformation("All status information refreshed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing status information");
                GlobalStatusMessage = "状态刷新失败";
                _notificationManager.ShowError("刷新失败", $"无法刷新状态信息：{ex.Message}");
            }
        }

        private void ClearNotifications()
        {
            try
            {
                _notificationManager.ClearAll();
                _logger.LogInformation("All notifications cleared");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing notifications");
            }
        }

        private void MarkAllNotificationsRead()
        {
            try
            {
                foreach (var notification in RecentNotifications)
                {
                    _notificationManager.MarkAsRead(notification.Id);
                }
                _logger.LogInformation("All notifications marked as read");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notifications as read");
            }
        }

        #endregion

        #region Status Update Methods

        /// <summary>
        /// Initialize all status information
        /// 初始化所有状态信息
        /// </summary>
        private async Task InitializeStatusAsync()
        {
            try
            {
                await UpdateAuthenticationStatusAsync();
                await UpdateClaudeStatusAsync();
                await UpdateSystemCleanupStatusAsync();
                await UpdateNetworkStatusAsync();

                // Update global status
                UpdateGlobalStatus();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing status");
                GlobalStatusMessage = "状态初始化失败";
            }
        }

        /// <summary>
        /// Update authentication status
        /// 更新认证状态
        /// </summary>
        private async Task UpdateAuthenticationStatusAsync()
        {
            try
            {
                if (_authenticationManager.IsAuthenticated)
                {
                    AuthenticationStatus.State = StatusState.Ready;
                    AuthenticationStatus.Message = "已认证";
                    AuthenticationStatus.Details = $"用户：{_authenticationManager.CurrentUserLogin}";
                }
                else
                {
                    AuthenticationStatus.State = StatusState.Offline;
                    AuthenticationStatus.Message = "未认证";
                    AuthenticationStatus.Details = "需要进行GitHub认证";
                }

                AuthenticationStatus.Timestamp = DateTime.Now;
                UpdateTrayStatus();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating authentication status");
                AuthenticationStatus.State = StatusState.Error;
                AuthenticationStatus.Message = "认证状态检查失败";
                AuthenticationStatus.Details = ex.Message;
            }
        }

        /// <summary>
        /// Update Claude tool status
        /// 更新Claude工具状态
        /// </summary>
        private async Task UpdateClaudeStatusAsync()
        {
            try
            {
                // Simulate checking Claude status
                await Task.Delay(500);

                // For now, we'll simulate different states based on authentication
                if (_authenticationManager.IsAuthenticated)
                {
                    ClaudeStatus.State = StatusState.Ready;
                    ClaudeStatus.Message = "已连接";
                    ClaudeStatus.Details = "Claude Code工具可用";
                }
                else
                {
                    ClaudeStatus.State = StatusState.Offline;
                    ClaudeStatus.Message = "未连接";
                    ClaudeStatus.Details = "需要认证后才能使用";
                }

                ClaudeStatus.Timestamp = DateTime.Now;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Claude status");
                ClaudeStatus.State = StatusState.Error;
                ClaudeStatus.Message = "连接检查失败";
                ClaudeStatus.Details = ex.Message;
            }
        }

        /// <summary>
        /// Update system cleanup status
        /// 更新系统清理状态
        /// </summary>
        private async Task UpdateSystemCleanupStatusAsync()
        {
            try
            {
                // Simulate checking system cleanup status
                await Task.Delay(300);

                SystemCleanupStatus.State = StatusState.Ready;
                SystemCleanupStatus.Message = "准备就绪";
                SystemCleanupStatus.Details = "系统清理工具可用";
                SystemCleanupStatus.Timestamp = DateTime.Now;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating system cleanup status");
                SystemCleanupStatus.State = StatusState.Error;
                SystemCleanupStatus.Message = "状态检查失败";
                SystemCleanupStatus.Details = ex.Message;
            }
        }

        /// <summary>
        /// Update network status
        /// 更新网络状态
        /// </summary>
        private async Task UpdateNetworkStatusAsync()
        {
            try
            {
                // Simulate network connectivity check
                await Task.Delay(200);

                NetworkStatus.State = StatusState.Ready;
                NetworkStatus.Message = "已连接";
                NetworkStatus.Details = "网络连接正常";
                NetworkStatus.Timestamp = DateTime.Now;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating network status");
                NetworkStatus.State = StatusState.Error;
                NetworkStatus.Message = "网络检查失败";
                NetworkStatus.Details = ex.Message;
            }
        }

        /// <summary>
        /// Update global status message based on individual statuses
        /// 根据各个状态更新全局状态消息
        /// </summary>
        private void UpdateGlobalStatus()
        {
            try
            {
                var errorCount = 0;
                var warningCount = 0;
                var offlineCount = 0;

                foreach (var status in AllStatuses)
                {
                    switch (status.State)
                    {
                        case StatusState.Error:
                            errorCount++;
                            break;
                        case StatusState.Warning:
                            warningCount++;
                            break;
                        case StatusState.Offline:
                            offlineCount++;
                            break;
                    }
                }

                if (errorCount > 0)
                {
                    GlobalStatusMessage = $"发现 {errorCount} 个错误";
                }
                else if (warningCount > 0)
                {
                    GlobalStatusMessage = $"发现 {warningCount} 个警告";
                }
                else if (offlineCount > 0)
                {
                    GlobalStatusMessage = $"有 {offlineCount} 个服务离线";
                }
                else
                {
                    GlobalStatusMessage = "系统就绪";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating global status");
                GlobalStatusMessage = "状态更新失败";
            }
        }

        /// <summary>
        /// Update tray status based on current application state
        /// 根据当前应用程序状态更新托盘状态
        /// </summary>
        private void UpdateTrayStatus()
        {
            try
            {
                if (_trayManager == null) return;

                TrayStatus status = TrayStatus.Idle;

                if (AuthenticationStatus.State == StatusState.Error || ClaudeStatus.State == StatusState.Error)
                {
                    status = TrayStatus.Error;
                }
                else if (AuthenticationStatus.State == StatusState.Warning || ClaudeStatus.State == StatusState.Warning)
                {
                    status = TrayStatus.Error; // Treat warnings as errors for tray
                }
                else if (AuthenticationStatus.State == StatusState.Working || ClaudeStatus.State == StatusState.Working)
                {
                    status = TrayStatus.Working;
                }
                else if (AuthenticationStatus.State == StatusState.Ready && ClaudeStatus.State == StatusState.Ready)
                {
                    status = TrayStatus.Ready;
                }
                else if (AuthenticationStatus.State == StatusState.Offline || ClaudeStatus.State == StatusState.Offline)
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
            Task.Run(async () =>
            {
                await UpdateAuthenticationStatusAsync();
                await UpdateClaudeStatusAsync(); // Claude status depends on authentication
                UpdateGlobalStatus();

                switch (e.NewState)
                {
                    case AuthenticationState.Authenticated:
                        _notificationManager.ShowSuccess("认证成功", $"欢迎，{_authenticationManager.CurrentUserLogin}！");
                        break;

                    case AuthenticationState.NotAuthenticated:
                        _notificationManager.ShowInfo("未认证", "请进行GitHub认证以使用完整功能");
                        break;

                    case AuthenticationState.LockedOut:
                        _notificationManager.ShowError("账户锁定", "您的账户已被锁定，请联系管理员");
                        break;
                }
            });
        }

        private void OnAuthenticationFailed(object? sender, AuthenticationFailedEventArgs e)
        {
            Task.Run(async () =>
            {
                await UpdateAuthenticationStatusAsync();
                UpdateGlobalStatus();
                _notificationManager.ShowError("认证失败", $"认证过程失败：{e.Reason}");
            });
        }

        private void OnSessionExpired(object? sender, SessionExpiredEventArgs e)
        {
            Task.Run(async () =>
            {
                await UpdateAuthenticationStatusAsync();
                UpdateGlobalStatus();
                _notificationManager.ShowWarning("会话过期", "您的会话已过期，请重新认证");
            });
        }

        private void OnNotificationAdded(object? sender, NotificationModel notification)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                // Add to recent notifications (keep only the latest 10)
                RecentNotifications.Insert(0, notification);
                while (RecentNotifications.Count > 10)
                {
                    RecentNotifications.RemoveAt(RecentNotifications.Count - 1);
                }

                UpdateNotificationCounts();
            });
        }

        private void OnNotificationRemoved(object? sender, string notificationId)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                var notification = RecentNotifications.FirstOrDefault(n => n.Id == notificationId);
                if (notification != null)
                {
                    RecentNotifications.Remove(notification);
                }

                UpdateNotificationCounts();
            });
        }

        private void OnNotificationUpdated(object? sender, NotificationModel notification)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                UpdateNotificationCounts();
            });
        }

        private void UpdateNotificationCounts()
        {
            UnreadNotificationCount = _notificationManager.UnreadCount;
            HasUnreadNotifications = UnreadNotificationCount > 0;
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

                    _notificationManager.NotificationAdded -= OnNotificationAdded;
                    _notificationManager.NotificationRemoved -= OnNotificationRemoved;
                    _notificationManager.NotificationUpdated -= OnNotificationUpdated;
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