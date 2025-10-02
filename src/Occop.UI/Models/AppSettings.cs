using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Occop.UI.Models
{
    /// <summary>
    /// Enum representing application themes
    /// 表示应用程序主题的枚举
    /// </summary>
    public enum AppTheme
    {
        /// <summary>Light theme</summary>
        Light,
        /// <summary>Dark theme</summary>
        Dark,
        /// <summary>System theme (follows OS)</summary>
        System
    }

    /// <summary>
    /// Enum representing window startup states
    /// 表示窗口启动状态的枚举
    /// </summary>
    public enum WindowStartupState
    {
        /// <summary>Normal window</summary>
        Normal,
        /// <summary>Minimized to taskbar</summary>
        Minimized,
        /// <summary>Minimized to system tray</summary>
        MinimizedToTray
    }

    /// <summary>
    /// Model representing application settings
    /// 表示应用程序设置的模型
    /// </summary>
    public class AppSettings : INotifyPropertyChanged
    {
        #region Appearance Settings

        private AppTheme _theme = AppTheme.System;
        private double _windowOpacity = 1.0;
        private bool _enableAnimations = true;

        /// <summary>
        /// Gets or sets the application theme
        /// 获取或设置应用程序主题
        /// </summary>
        public AppTheme Theme
        {
            get => _theme;
            set => SetProperty(ref _theme, value);
        }

        /// <summary>
        /// Gets or sets the window opacity (0.0 to 1.0)
        /// 获取或设置窗口不透明度（0.0到1.0）
        /// </summary>
        public double WindowOpacity
        {
            get => _windowOpacity;
            set => SetProperty(ref _windowOpacity, Math.Clamp(value, 0.0, 1.0));
        }

        /// <summary>
        /// Gets or sets whether animations are enabled
        /// 获取或设置是否启用动画
        /// </summary>
        public bool EnableAnimations
        {
            get => _enableAnimations;
            set => SetProperty(ref _enableAnimations, value);
        }

        #endregion

        #region Startup Settings

        private bool _startWithWindows = false;
        private WindowStartupState _windowStartupState = WindowStartupState.Normal;
        private bool _checkForUpdatesOnStartup = true;

        /// <summary>
        /// Gets or sets whether the app starts with Windows
        /// 获取或设置应用程序是否随Windows启动
        /// </summary>
        public bool StartWithWindows
        {
            get => _startWithWindows;
            set => SetProperty(ref _startWithWindows, value);
        }

        /// <summary>
        /// Gets or sets the window startup state
        /// 获取或设置窗口启动状态
        /// </summary>
        public WindowStartupState WindowStartupState
        {
            get => _windowStartupState;
            set => SetProperty(ref _windowStartupState, value);
        }

        /// <summary>
        /// Gets or sets whether to check for updates on startup
        /// 获取或设置是否在启动时检查更新
        /// </summary>
        public bool CheckForUpdatesOnStartup
        {
            get => _checkForUpdatesOnStartup;
            set => SetProperty(ref _checkForUpdatesOnStartup, value);
        }

        #endregion

        #region Notification Settings

        private bool _enableNotifications = true;
        private bool _enableSoundNotifications = true;
        private bool _enableTrayNotifications = true;
        private NotificationPriority _minimumNotificationPriority = NotificationPriority.Normal;
        private TimeSpan _notificationDuration = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Gets or sets whether notifications are enabled
        /// 获取或设置是否启用通知
        /// </summary>
        public bool EnableNotifications
        {
            get => _enableNotifications;
            set => SetProperty(ref _enableNotifications, value);
        }

        /// <summary>
        /// Gets or sets whether sound notifications are enabled
        /// 获取或设置是否启用声音通知
        /// </summary>
        public bool EnableSoundNotifications
        {
            get => _enableSoundNotifications;
            set => SetProperty(ref _enableSoundNotifications, value);
        }

        /// <summary>
        /// Gets or sets whether tray balloon notifications are enabled
        /// 获取或设置是否启用托盘气球通知
        /// </summary>
        public bool EnableTrayNotifications
        {
            get => _enableTrayNotifications;
            set => SetProperty(ref _enableTrayNotifications, value);
        }

        /// <summary>
        /// Gets or sets the minimum notification priority to display
        /// 获取或设置要显示的最低通知优先级
        /// </summary>
        public NotificationPriority MinimumNotificationPriority
        {
            get => _minimumNotificationPriority;
            set => SetProperty(ref _minimumNotificationPriority, value);
        }

        /// <summary>
        /// Gets or sets the default notification duration
        /// 获取或设置默认通知持续时间
        /// </summary>
        public TimeSpan NotificationDuration
        {
            get => _notificationDuration;
            set => SetProperty(ref _notificationDuration, value);
        }

        #endregion

        #region System Tray Settings

        private bool _minimizeToTray = true;
        private bool _closeToTray = false;
        private bool _showTrayIcon = true;

        /// <summary>
        /// Gets or sets whether to minimize to system tray
        /// 获取或设置是否最小化到系统托盘
        /// </summary>
        public bool MinimizeToTray
        {
            get => _minimizeToTray;
            set => SetProperty(ref _minimizeToTray, value);
        }

        /// <summary>
        /// Gets or sets whether to close to system tray (instead of exiting)
        /// 获取或设置是否关闭到系统托盘（而不是退出）
        /// </summary>
        public bool CloseToTray
        {
            get => _closeToTray;
            set => SetProperty(ref _closeToTray, value);
        }

        /// <summary>
        /// Gets or sets whether to show the system tray icon
        /// 获取或设置是否显示系统托盘图标
        /// </summary>
        public bool ShowTrayIcon
        {
            get => _showTrayIcon;
            set => SetProperty(ref _showTrayIcon, value);
        }

        #endregion

        #region Authentication Settings

        private int _sessionTimeoutMinutes = 480;
        private int _maxFailedAttempts = 3;
        private int _lockoutDurationMinutes = 15;
        private bool _rememberAuthentication = true;

        /// <summary>
        /// Gets or sets the session timeout in minutes
        /// 获取或设置会话超时时间（分钟）
        /// </summary>
        public int SessionTimeoutMinutes
        {
            get => _sessionTimeoutMinutes;
            set => SetProperty(ref _sessionTimeoutMinutes, Math.Max(1, value));
        }

        /// <summary>
        /// Gets or sets the maximum failed authentication attempts
        /// 获取或设置最大失败认证尝试次数
        /// </summary>
        public int MaxFailedAttempts
        {
            get => _maxFailedAttempts;
            set => SetProperty(ref _maxFailedAttempts, Math.Max(1, value));
        }

        /// <summary>
        /// Gets or sets the lockout duration in minutes
        /// 获取或设置锁定持续时间（分钟）
        /// </summary>
        public int LockoutDurationMinutes
        {
            get => _lockoutDurationMinutes;
            set => SetProperty(ref _lockoutDurationMinutes, Math.Max(1, value));
        }

        /// <summary>
        /// Gets or sets whether to remember authentication between sessions
        /// 获取或设置是否在会话之间记住认证
        /// </summary>
        public bool RememberAuthentication
        {
            get => _rememberAuthentication;
            set => SetProperty(ref _rememberAuthentication, value);
        }

        #endregion

        #region Logging Settings

        private string _logLevel = "Information";
        private bool _enableFileLogging = true;
        private int _logRetentionDays = 30;

        /// <summary>
        /// Gets or sets the logging level
        /// 获取或设置日志级别
        /// </summary>
        public string LogLevel
        {
            get => _logLevel;
            set => SetProperty(ref _logLevel, value ?? "Information");
        }

        /// <summary>
        /// Gets or sets whether file logging is enabled
        /// 获取或设置是否启用文件日志
        /// </summary>
        public bool EnableFileLogging
        {
            get => _enableFileLogging;
            set => SetProperty(ref _enableFileLogging, value);
        }

        /// <summary>
        /// Gets or sets the log retention period in days
        /// 获取或设置日志保留天数
        /// </summary>
        public int LogRetentionDays
        {
            get => _logRetentionDays;
            set => SetProperty(ref _logRetentionDays, Math.Max(1, value));
        }

        #endregion

        #region Advanced Settings

        private bool _enableDebugMode = false;
        private bool _enableTelemetry = false;
        private string _language = "zh-CN";

        /// <summary>
        /// Gets or sets whether debug mode is enabled
        /// 获取或设置是否启用调试模式
        /// </summary>
        public bool EnableDebugMode
        {
            get => _enableDebugMode;
            set => SetProperty(ref _enableDebugMode, value);
        }

        /// <summary>
        /// Gets or sets whether telemetry is enabled
        /// 获取或设置是否启用遥测
        /// </summary>
        public bool EnableTelemetry
        {
            get => _enableTelemetry;
            set => SetProperty(ref _enableTelemetry, value);
        }

        /// <summary>
        /// Gets or sets the application language
        /// 获取或设置应用程序语言
        /// </summary>
        public string Language
        {
            get => _language;
            set => SetProperty(ref _language, value ?? "zh-CN");
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

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

        /// <summary>
        /// Creates a clone of the current settings
        /// 创建当前设置的克隆
        /// </summary>
        /// <returns>A new AppSettings instance with the same values</returns>
        public AppSettings Clone()
        {
            return new AppSettings
            {
                // Appearance
                Theme = this.Theme,
                WindowOpacity = this.WindowOpacity,
                EnableAnimations = this.EnableAnimations,

                // Startup
                StartWithWindows = this.StartWithWindows,
                WindowStartupState = this.WindowStartupState,
                CheckForUpdatesOnStartup = this.CheckForUpdatesOnStartup,

                // Notifications
                EnableNotifications = this.EnableNotifications,
                EnableSoundNotifications = this.EnableSoundNotifications,
                EnableTrayNotifications = this.EnableTrayNotifications,
                MinimumNotificationPriority = this.MinimumNotificationPriority,
                NotificationDuration = this.NotificationDuration,

                // System Tray
                MinimizeToTray = this.MinimizeToTray,
                CloseToTray = this.CloseToTray,
                ShowTrayIcon = this.ShowTrayIcon,

                // Authentication
                SessionTimeoutMinutes = this.SessionTimeoutMinutes,
                MaxFailedAttempts = this.MaxFailedAttempts,
                LockoutDurationMinutes = this.LockoutDurationMinutes,
                RememberAuthentication = this.RememberAuthentication,

                // Logging
                LogLevel = this.LogLevel,
                EnableFileLogging = this.EnableFileLogging,
                LogRetentionDays = this.LogRetentionDays,

                // Advanced
                EnableDebugMode = this.EnableDebugMode,
                EnableTelemetry = this.EnableTelemetry,
                Language = this.Language
            };
        }

        /// <summary>
        /// Copies values from another AppSettings instance
        /// 从另一个AppSettings实例复制值
        /// </summary>
        /// <param name="source">The source settings</param>
        public void CopyFrom(AppSettings source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            // Appearance
            Theme = source.Theme;
            WindowOpacity = source.WindowOpacity;
            EnableAnimations = source.EnableAnimations;

            // Startup
            StartWithWindows = source.StartWithWindows;
            WindowStartupState = source.WindowStartupState;
            CheckForUpdatesOnStartup = source.CheckForUpdatesOnStartup;

            // Notifications
            EnableNotifications = source.EnableNotifications;
            EnableSoundNotifications = source.EnableSoundNotifications;
            EnableTrayNotifications = source.EnableTrayNotifications;
            MinimumNotificationPriority = source.MinimumNotificationPriority;
            NotificationDuration = source.NotificationDuration;

            // System Tray
            MinimizeToTray = source.MinimizeToTray;
            CloseToTray = source.CloseToTray;
            ShowTrayIcon = source.ShowTrayIcon;

            // Authentication
            SessionTimeoutMinutes = source.SessionTimeoutMinutes;
            MaxFailedAttempts = source.MaxFailedAttempts;
            LockoutDurationMinutes = source.LockoutDurationMinutes;
            RememberAuthentication = source.RememberAuthentication;

            // Logging
            LogLevel = source.LogLevel;
            EnableFileLogging = source.EnableFileLogging;
            LogRetentionDays = source.LogRetentionDays;

            // Advanced
            EnableDebugMode = source.EnableDebugMode;
            EnableTelemetry = source.EnableTelemetry;
            Language = source.Language;
        }
    }
}
