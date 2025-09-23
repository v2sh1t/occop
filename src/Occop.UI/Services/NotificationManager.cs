using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Occop.UI.Models;
using Occop.Services;

namespace Occop.UI.Services
{
    /// <summary>
    /// Interface for notification management
    /// 通知管理接口
    /// </summary>
    public interface INotificationManager : IDisposable
    {
        /// <summary>
        /// Event raised when a new notification is added
        /// 添加新通知时触发的事件
        /// </summary>
        event EventHandler<NotificationModel>? NotificationAdded;

        /// <summary>
        /// Event raised when a notification is removed
        /// 移除通知时触发的事件
        /// </summary>
        event EventHandler<string>? NotificationRemoved;

        /// <summary>
        /// Event raised when a notification is updated
        /// 更新通知时触发的事件
        /// </summary>
        event EventHandler<NotificationModel>? NotificationUpdated;

        /// <summary>
        /// Gets the collection of active notifications
        /// 获取活动通知集合
        /// </summary>
        ObservableCollection<NotificationModel> Notifications { get; }

        /// <summary>
        /// Gets the count of unread notifications
        /// 获取未读通知数量
        /// </summary>
        int UnreadCount { get; }

        /// <summary>
        /// Show an information notification
        /// 显示信息通知
        /// </summary>
        /// <param name="title">The notification title</param>
        /// <param name="message">The notification message</param>
        /// <param name="duration">The display duration</param>
        /// <param name="action">Optional action to perform when clicked</param>
        /// <returns>The notification ID</returns>
        string ShowInfo(string title, string message, TimeSpan? duration = null, Action? action = null);

        /// <summary>
        /// Show a success notification
        /// 显示成功通知
        /// </summary>
        /// <param name="title">The notification title</param>
        /// <param name="message">The notification message</param>
        /// <param name="duration">The display duration</param>
        /// <param name="action">Optional action to perform when clicked</param>
        /// <returns>The notification ID</returns>
        string ShowSuccess(string title, string message, TimeSpan? duration = null, Action? action = null);

        /// <summary>
        /// Show a warning notification
        /// 显示警告通知
        /// </summary>
        /// <param name="title">The notification title</param>
        /// <param name="message">The notification message</param>
        /// <param name="duration">The display duration</param>
        /// <param name="action">Optional action to perform when clicked</param>
        /// <returns>The notification ID</returns>
        string ShowWarning(string title, string message, TimeSpan? duration = null, Action? action = null);

        /// <summary>
        /// Show an error notification
        /// 显示错误通知
        /// </summary>
        /// <param name="title">The notification title</param>
        /// <param name="message">The notification message</param>
        /// <param name="duration">The display duration</param>
        /// <param name="action">Optional action to perform when clicked</param>
        /// <returns>The notification ID</returns>
        string ShowError(string title, string message, TimeSpan? duration = null, Action? action = null);

        /// <summary>
        /// Show a custom notification
        /// 显示自定义通知
        /// </summary>
        /// <param name="notification">The notification to show</param>
        /// <returns>The notification ID</returns>
        string ShowNotification(NotificationModel notification);

        /// <summary>
        /// Remove a notification by ID
        /// 根据ID移除通知
        /// </summary>
        /// <param name="notificationId">The notification ID</param>
        /// <returns>True if the notification was removed, false otherwise</returns>
        bool RemoveNotification(string notificationId);

        /// <summary>
        /// Mark a notification as read
        /// 将通知标记为已读
        /// </summary>
        /// <param name="notificationId">The notification ID</param>
        /// <returns>True if the notification was marked as read, false otherwise</returns>
        bool MarkAsRead(string notificationId);

        /// <summary>
        /// Clear all notifications
        /// 清除所有通知
        /// </summary>
        void ClearAll();

        /// <summary>
        /// Clear all read notifications
        /// 清除所有已读通知
        /// </summary>
        void ClearRead();
    }

    /// <summary>
    /// Implementation of notification management service
    /// 通知管理服务的实现
    /// </summary>
    public class NotificationManager : INotificationManager, INotifyPropertyChanged
    {
        private readonly ILogger<NotificationManager> _logger;
        private readonly ITrayManager? _trayManager;
        private readonly Dispatcher _dispatcher;
        private readonly Timer _cleanupTimer;
        private readonly ConcurrentDictionary<string, NotificationModel> _notificationMap;
        private bool _disposed = false;

        /// <summary>
        /// Default notification duration
        /// 默认通知持续时间
        /// </summary>
        public static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Maximum number of notifications to keep
        /// 保留的最大通知数量
        /// </summary>
        public static readonly int MaxNotifications = 50;

        private ObservableCollection<NotificationModel> _notifications;
        private int _unreadCount;

        public event EventHandler<NotificationModel>? NotificationAdded;
        public event EventHandler<string>? NotificationRemoved;
        public event EventHandler<NotificationModel>? NotificationUpdated;
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Gets the collection of active notifications
        /// 获取活动通知集合
        /// </summary>
        public ObservableCollection<NotificationModel> Notifications
        {
            get => _notifications;
            private set => SetProperty(ref _notifications, value);
        }

        /// <summary>
        /// Gets the count of unread notifications
        /// 获取未读通知数量
        /// </summary>
        public int UnreadCount
        {
            get => _unreadCount;
            private set => SetProperty(ref _unreadCount, value);
        }

        /// <summary>
        /// Initializes a new instance of the NotificationManager class
        /// 初始化NotificationManager类的新实例
        /// </summary>
        /// <param name="logger">The logger instance</param>
        /// <param name="trayManager">Optional tray manager for system notifications</param>
        public NotificationManager(ILogger<NotificationManager> logger, ITrayManager? trayManager = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _trayManager = trayManager;
            _dispatcher = Dispatcher.CurrentDispatcher;
            _notificationMap = new ConcurrentDictionary<string, NotificationModel>();
            _notifications = new ObservableCollection<NotificationModel>();
            _unreadCount = 0;

            // Setup cleanup timer to remove expired notifications
            _cleanupTimer = new Timer(CleanupExpiredNotifications, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

            _logger.LogInformation("NotificationManager initialized");
        }

        /// <summary>
        /// Show an information notification
        /// 显示信息通知
        /// </summary>
        public string ShowInfo(string title, string message, TimeSpan? duration = null, Action? action = null)
        {
            var notification = new NotificationModel(NotificationType.Info, title, message,
                NotificationPriority.Normal, duration ?? DefaultDuration)
            {
                Action = action,
                IsActionable = action != null
            };

            return ShowNotification(notification);
        }

        /// <summary>
        /// Show a success notification
        /// 显示成功通知
        /// </summary>
        public string ShowSuccess(string title, string message, TimeSpan? duration = null, Action? action = null)
        {
            var notification = new NotificationModel(NotificationType.Success, title, message,
                NotificationPriority.Normal, duration ?? DefaultDuration)
            {
                Action = action,
                IsActionable = action != null
            };

            return ShowNotification(notification);
        }

        /// <summary>
        /// Show a warning notification
        /// 显示警告通知
        /// </summary>
        public string ShowWarning(string title, string message, TimeSpan? duration = null, Action? action = null)
        {
            var notification = new NotificationModel(NotificationType.Warning, title, message,
                NotificationPriority.High, duration ?? TimeSpan.FromSeconds(8))
            {
                Action = action,
                IsActionable = action != null
            };

            return ShowNotification(notification);
        }

        /// <summary>
        /// Show an error notification
        /// 显示错误通知
        /// </summary>
        public string ShowError(string title, string message, TimeSpan? duration = null, Action? action = null)
        {
            var notification = new NotificationModel(NotificationType.Error, title, message,
                NotificationPriority.Critical, duration ?? TimeSpan.FromSeconds(10))
            {
                Action = action,
                IsActionable = action != null
            };

            return ShowNotification(notification);
        }

        /// <summary>
        /// Show a custom notification
        /// 显示自定义通知
        /// </summary>
        public string ShowNotification(NotificationModel notification)
        {
            if (notification == null)
                throw new ArgumentNullException(nameof(notification));

            try
            {
                // Add to tracking dictionary
                _notificationMap.TryAdd(notification.Id, notification);

                // Subscribe to property changes
                notification.PropertyChanged += OnNotificationPropertyChanged;

                // Add to UI collection on UI thread
                _dispatcher.Invoke(() =>
                {
                    // Insert at the beginning for newest-first order
                    Notifications.Insert(0, notification);

                    // Limit the number of notifications
                    while (Notifications.Count > MaxNotifications)
                    {
                        var oldest = Notifications.LastOrDefault();
                        if (oldest != null)
                        {
                            RemoveNotificationInternal(oldest.Id, false);
                        }
                    }

                    UpdateUnreadCount();
                });

                // Show system tray notification for high priority items
                if (notification.Priority >= NotificationPriority.High && _trayManager != null)
                {
                    var trayIcon = notification.Type switch
                    {
                        NotificationType.Success => System.Windows.Forms.ToolTipIcon.Info,
                        NotificationType.Warning => System.Windows.Forms.ToolTipIcon.Warning,
                        NotificationType.Error => System.Windows.Forms.ToolTipIcon.Error,
                        _ => System.Windows.Forms.ToolTipIcon.Info
                    };

                    _trayManager.ShowBalloonTip(notification.Title, notification.Message, trayIcon);
                }

                // Raise event
                NotificationAdded?.Invoke(this, notification);

                _logger.LogDebug("Notification added: {Id} - {Title}", notification.Id, notification.Title);
                return notification.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding notification: {Title}", notification.Title);
                return string.Empty;
            }
        }

        /// <summary>
        /// Remove a notification by ID
        /// 根据ID移除通知
        /// </summary>
        public bool RemoveNotification(string notificationId)
        {
            return RemoveNotificationInternal(notificationId, true);
        }

        /// <summary>
        /// Mark a notification as read
        /// 将通知标记为已读
        /// </summary>
        public bool MarkAsRead(string notificationId)
        {
            if (string.IsNullOrEmpty(notificationId))
                return false;

            try
            {
                if (_notificationMap.TryGetValue(notificationId, out var notification))
                {
                    notification.IsRead = true;
                    NotificationUpdated?.Invoke(this, notification);
                    UpdateUnreadCount();
                    _logger.LogDebug("Notification marked as read: {Id}", notificationId);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification as read: {Id}", notificationId);
                return false;
            }
        }

        /// <summary>
        /// Clear all notifications
        /// 清除所有通知
        /// </summary>
        public void ClearAll()
        {
            try
            {
                _dispatcher.Invoke(() =>
                {
                    var notificationIds = Notifications.Select(n => n.Id).ToList();

                    foreach (var notification in Notifications.ToList())
                    {
                        notification.PropertyChanged -= OnNotificationPropertyChanged;
                    }

                    Notifications.Clear();
                    _notificationMap.Clear();
                    UpdateUnreadCount();

                    foreach (var id in notificationIds)
                    {
                        NotificationRemoved?.Invoke(this, id);
                    }
                });

                _logger.LogInformation("All notifications cleared");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all notifications");
            }
        }

        /// <summary>
        /// Clear all read notifications
        /// 清除所有已读通知
        /// </summary>
        public void ClearRead()
        {
            try
            {
                _dispatcher.Invoke(() =>
                {
                    var readNotifications = Notifications.Where(n => n.IsRead).ToList();
                    var removedIds = new List<string>();

                    foreach (var notification in readNotifications)
                    {
                        notification.PropertyChanged -= OnNotificationPropertyChanged;
                        Notifications.Remove(notification);
                        _notificationMap.TryRemove(notification.Id, out _);
                        removedIds.Add(notification.Id);
                    }

                    UpdateUnreadCount();

                    foreach (var id in removedIds)
                    {
                        NotificationRemoved?.Invoke(this, id);
                    }
                });

                _logger.LogInformation("Read notifications cleared");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing read notifications");
            }
        }

        #region Private Methods

        private bool RemoveNotificationInternal(string notificationId, bool raiseEvent)
        {
            if (string.IsNullOrEmpty(notificationId))
                return false;

            try
            {
                if (_notificationMap.TryRemove(notificationId, out var notification))
                {
                    _dispatcher.Invoke(() =>
                    {
                        notification.PropertyChanged -= OnNotificationPropertyChanged;
                        Notifications.Remove(notification);
                        UpdateUnreadCount();
                    });

                    if (raiseEvent)
                    {
                        NotificationRemoved?.Invoke(this, notificationId);
                    }

                    _logger.LogDebug("Notification removed: {Id}", notificationId);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing notification: {Id}", notificationId);
                return false;
            }
        }

        private void CleanupExpiredNotifications(object? state)
        {
            try
            {
                var now = DateTime.Now;
                var expiredNotifications = _notificationMap.Values
                    .Where(n => n.Duration.HasValue && now - n.Timestamp > n.Duration.Value)
                    .ToList();

                foreach (var notification in expiredNotifications)
                {
                    RemoveNotificationInternal(notification.Id, true);
                }

                if (expiredNotifications.Count > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} expired notifications", expiredNotifications.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during notification cleanup");
            }
        }

        private void OnNotificationPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is NotificationModel notification)
            {
                if (e.PropertyName == nameof(NotificationModel.IsRead))
                {
                    UpdateUnreadCount();
                }

                NotificationUpdated?.Invoke(this, notification);
            }
        }

        private void UpdateUnreadCount()
        {
            var newCount = Notifications.Count(n => !n.IsRead);
            UnreadCount = newCount;
        }

        #endregion

        #region INotifyPropertyChanged

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion

        #region IDisposable

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _cleanupTimer?.Dispose();

                    // Unsubscribe from all notification events
                    foreach (var notification in _notificationMap.Values)
                    {
                        notification.PropertyChanged -= OnNotificationPropertyChanged;
                    }

                    _notificationMap.Clear();

                    _dispatcher.Invoke(() =>
                    {
                        Notifications.Clear();
                    });
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