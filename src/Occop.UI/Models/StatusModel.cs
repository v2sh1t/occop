using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Occop.UI.Models
{
    /// <summary>
    /// Enum representing different status types
    /// 表示不同状态类型的枚举
    /// </summary>
    public enum StatusType
    {
        /// <summary>Authentication status</summary>
        Authentication,
        /// <summary>AI tool status</summary>
        AITool,
        /// <summary>System cleanup status</summary>
        SystemCleanup,
        /// <summary>Application status</summary>
        Application,
        /// <summary>Network connectivity status</summary>
        Network
    }

    /// <summary>
    /// Enum representing different status states
    /// 表示不同状态状态的枚举
    /// </summary>
    public enum StatusState
    {
        /// <summary>Unknown or undefined state</summary>
        Unknown,
        /// <summary>Idle state</summary>
        Idle,
        /// <summary>Ready state</summary>
        Ready,
        /// <summary>Working or processing</summary>
        Working,
        /// <summary>Success state</summary>
        Success,
        /// <summary>Warning state</summary>
        Warning,
        /// <summary>Error state</summary>
        Error,
        /// <summary>Offline or disconnected state</summary>
        Offline,
        /// <summary>Loading or initializing state</summary>
        Loading
    }

    /// <summary>
    /// Enum representing different notification types
    /// 表示不同通知类型的枚举
    /// </summary>
    public enum NotificationType
    {
        /// <summary>Information notification</summary>
        Info,
        /// <summary>Success notification</summary>
        Success,
        /// <summary>Warning notification</summary>
        Warning,
        /// <summary>Error notification</summary>
        Error
    }

    /// <summary>
    /// Enum representing notification priority levels
    /// 表示通知优先级级别的枚举
    /// </summary>
    public enum NotificationPriority
    {
        /// <summary>Low priority</summary>
        Low,
        /// <summary>Normal priority</summary>
        Normal,
        /// <summary>High priority</summary>
        High,
        /// <summary>Critical priority</summary>
        Critical
    }

    /// <summary>
    /// Model representing application status information
    /// 表示应用程序状态信息的模型
    /// </summary>
    public class StatusModel : INotifyPropertyChanged
    {
        private StatusType _type;
        private StatusState _state;
        private string _title;
        private string _message;
        private string _details;
        private DateTime _timestamp;
        private object? _additionalData;

        /// <summary>
        /// Gets or sets the status type
        /// 获取或设置状态类型
        /// </summary>
        public StatusType Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        /// <summary>
        /// Gets or sets the status state
        /// 获取或设置状态状态
        /// </summary>
        public StatusState State
        {
            get => _state;
            set => SetProperty(ref _state, value);
        }

        /// <summary>
        /// Gets or sets the status title
        /// 获取或设置状态标题
        /// </summary>
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        /// <summary>
        /// Gets or sets the status message
        /// 获取或设置状态消息
        /// </summary>
        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        /// <summary>
        /// Gets or sets the status details
        /// 获取或设置状态详情
        /// </summary>
        public string Details
        {
            get => _details;
            set => SetProperty(ref _details, value);
        }

        /// <summary>
        /// Gets or sets the status timestamp
        /// 获取或设置状态时间戳
        /// </summary>
        public DateTime Timestamp
        {
            get => _timestamp;
            set => SetProperty(ref _timestamp, value);
        }

        /// <summary>
        /// Gets or sets additional data associated with the status
        /// 获取或设置与状态相关的附加数据
        /// </summary>
        public object? AdditionalData
        {
            get => _additionalData;
            set => SetProperty(ref _additionalData, value);
        }

        /// <summary>
        /// Initializes a new instance of the StatusModel class
        /// 初始化StatusModel类的新实例
        /// </summary>
        public StatusModel()
        {
            _type = StatusType.Application;
            _state = StatusState.Unknown;
            _title = string.Empty;
            _message = string.Empty;
            _details = string.Empty;
            _timestamp = DateTime.Now;
        }

        /// <summary>
        /// Initializes a new instance of the StatusModel class with specified parameters
        /// 使用指定参数初始化StatusModel类的新实例
        /// </summary>
        /// <param name="type">The status type</param>
        /// <param name="state">The status state</param>
        /// <param name="title">The status title</param>
        /// <param name="message">The status message</param>
        /// <param name="details">The status details</param>
        public StatusModel(StatusType type, StatusState state, string title, string message, string details = "")
        {
            _type = type;
            _state = state;
            _title = title ?? string.Empty;
            _message = message ?? string.Empty;
            _details = details ?? string.Empty;
            _timestamp = DateTime.Now;
        }

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
    }

    /// <summary>
    /// Model representing a notification
    /// 表示通知的模型
    /// </summary>
    public class NotificationModel : INotifyPropertyChanged
    {
        private string _id;
        private NotificationType _type;
        private NotificationPriority _priority;
        private string _title;
        private string _message;
        private DateTime _timestamp;
        private TimeSpan? _duration;
        private bool _isRead;
        private bool _isActionable;
        private Action? _action;
        private object? _context;

        /// <summary>
        /// Gets or sets the notification ID
        /// 获取或设置通知ID
        /// </summary>
        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        /// <summary>
        /// Gets or sets the notification type
        /// 获取或设置通知类型
        /// </summary>
        public NotificationType Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        /// <summary>
        /// Gets or sets the notification priority
        /// 获取或设置通知优先级
        /// </summary>
        public NotificationPriority Priority
        {
            get => _priority;
            set => SetProperty(ref _priority, value);
        }

        /// <summary>
        /// Gets or sets the notification title
        /// 获取或设置通知标题
        /// </summary>
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        /// <summary>
        /// Gets or sets the notification message
        /// 获取或设置通知消息
        /// </summary>
        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        /// <summary>
        /// Gets or sets the notification timestamp
        /// 获取或设置通知时间戳
        /// </summary>
        public DateTime Timestamp
        {
            get => _timestamp;
            set => SetProperty(ref _timestamp, value);
        }

        /// <summary>
        /// Gets or sets the notification display duration
        /// 获取或设置通知显示持续时间
        /// </summary>
        public TimeSpan? Duration
        {
            get => _duration;
            set => SetProperty(ref _duration, value);
        }

        /// <summary>
        /// Gets or sets whether the notification has been read
        /// 获取或设置通知是否已读
        /// </summary>
        public bool IsRead
        {
            get => _isRead;
            set => SetProperty(ref _isRead, value);
        }

        /// <summary>
        /// Gets or sets whether the notification is actionable
        /// 获取或设置通知是否可操作
        /// </summary>
        public bool IsActionable
        {
            get => _isActionable;
            set => SetProperty(ref _isActionable, value);
        }

        /// <summary>
        /// Gets or sets the action to perform when notification is clicked
        /// 获取或设置点击通知时执行的操作
        /// </summary>
        public Action? Action
        {
            get => _action;
            set => SetProperty(ref _action, value);
        }

        /// <summary>
        /// Gets or sets additional context data for the notification
        /// 获取或设置通知的附加上下文数据
        /// </summary>
        public object? Context
        {
            get => _context;
            set => SetProperty(ref _context, value);
        }

        /// <summary>
        /// Initializes a new instance of the NotificationModel class
        /// 初始化NotificationModel类的新实例
        /// </summary>
        public NotificationModel()
        {
            _id = Guid.NewGuid().ToString();
            _type = NotificationType.Info;
            _priority = NotificationPriority.Normal;
            _title = string.Empty;
            _message = string.Empty;
            _timestamp = DateTime.Now;
            _isRead = false;
            _isActionable = false;
        }

        /// <summary>
        /// Initializes a new instance of the NotificationModel class with specified parameters
        /// 使用指定参数初始化NotificationModel类的新实例
        /// </summary>
        /// <param name="type">The notification type</param>
        /// <param name="title">The notification title</param>
        /// <param name="message">The notification message</param>
        /// <param name="priority">The notification priority</param>
        /// <param name="duration">The notification duration</param>
        public NotificationModel(NotificationType type, string title, string message,
            NotificationPriority priority = NotificationPriority.Normal, TimeSpan? duration = null)
        {
            _id = Guid.NewGuid().ToString();
            _type = type;
            _title = title ?? string.Empty;
            _message = message ?? string.Empty;
            _priority = priority;
            _duration = duration;
            _timestamp = DateTime.Now;
            _isRead = false;
            _isActionable = false;
        }

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
    }
}