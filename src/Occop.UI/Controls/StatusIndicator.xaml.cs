using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Occop.UI.Models;

namespace Occop.UI.Controls
{
    /// <summary>
    /// StatusIndicator user control for displaying application status
    /// 用于显示应用程序状态的StatusIndicator用户控件
    /// </summary>
    public partial class StatusIndicator : UserControl
    {
        #region Dependency Properties

        /// <summary>
        /// Status dependency property
        /// 状态依赖属性
        /// </summary>
        public static readonly DependencyProperty StatusProperty =
            DependencyProperty.Register(nameof(Status), typeof(StatusModel), typeof(StatusIndicator),
                new PropertyMetadata(null, OnStatusChanged));

        /// <summary>
        /// ShowTitle dependency property
        /// 显示标题依赖属性
        /// </summary>
        public static readonly DependencyProperty ShowTitleProperty =
            DependencyProperty.Register(nameof(ShowTitle), typeof(bool), typeof(StatusIndicator),
                new PropertyMetadata(true));

        /// <summary>
        /// ShowMessage dependency property
        /// 显示消息依赖属性
        /// </summary>
        public static readonly DependencyProperty ShowMessageProperty =
            DependencyProperty.Register(nameof(ShowMessage), typeof(bool), typeof(StatusIndicator),
                new PropertyMetadata(true));

        /// <summary>
        /// ShowDetails dependency property
        /// 显示详情依赖属性
        /// </summary>
        public static readonly DependencyProperty ShowDetailsProperty =
            DependencyProperty.Register(nameof(ShowDetails), typeof(bool), typeof(StatusIndicator),
                new PropertyMetadata(false));

        /// <summary>
        /// ShowTimestamp dependency property
        /// 显示时间戳依赖属性
        /// </summary>
        public static readonly DependencyProperty ShowTimestampProperty =
            DependencyProperty.Register(nameof(ShowTimestamp), typeof(bool), typeof(StatusIndicator),
                new PropertyMetadata(false));

        /// <summary>
        /// CompactMode dependency property
        /// 紧凑模式依赖属性
        /// </summary>
        public static readonly DependencyProperty CompactModeProperty =
            DependencyProperty.Register(nameof(CompactMode), typeof(bool), typeof(StatusIndicator),
                new PropertyMetadata(false, OnCompactModeChanged));

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the status to display
        /// 获取或设置要显示的状态
        /// </summary>
        public StatusModel Status
        {
            get => (StatusModel)GetValue(StatusProperty);
            set => SetValue(StatusProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to show the title
        /// 获取或设置是否显示标题
        /// </summary>
        public bool ShowTitle
        {
            get => (bool)GetValue(ShowTitleProperty);
            set => SetValue(ShowTitleProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to show the message
        /// 获取或设置是否显示消息
        /// </summary>
        public bool ShowMessage
        {
            get => (bool)GetValue(ShowMessageProperty);
            set => SetValue(ShowMessageProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to show the details
        /// 获取或设置是否显示详情
        /// </summary>
        public bool ShowDetails
        {
            get => (bool)GetValue(ShowDetailsProperty);
            set => SetValue(ShowDetailsProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to show the timestamp
        /// 获取或设置是否显示时间戳
        /// </summary>
        public bool ShowTimestamp
        {
            get => (bool)GetValue(ShowTimestampProperty);
            set => SetValue(ShowTimestampProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to use compact mode
        /// 获取或设置是否使用紧凑模式
        /// </summary>
        public bool CompactMode
        {
            get => (bool)GetValue(CompactModeProperty);
            set => SetValue(CompactModeProperty, value);
        }

        #endregion

        #region Static Converters

        /// <summary>
        /// Converter to invert boolean to visibility
        /// 将布尔值转换为相反的可见性的转换器
        /// </summary>
        public static readonly IValueConverter InvertedBooleanToVisibilityConverter = new InvertedBooleanToVisibilityConverter();

        #endregion

        /// <summary>
        /// Initializes a new instance of the StatusIndicator class
        /// 初始化StatusIndicator类的新实例
        /// </summary>
        public StatusIndicator()
        {
            InitializeComponent();
            DataContext = new StatusIndicatorViewModel();
        }

        #region Event Handlers

        private static void OnStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is StatusIndicator statusIndicator && statusIndicator.DataContext is StatusIndicatorViewModel viewModel)
            {
                viewModel.UpdateStatus(e.NewValue as StatusModel);
            }
        }

        private static void OnCompactModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is StatusIndicator statusIndicator && statusIndicator.DataContext is StatusIndicatorViewModel viewModel)
            {
                viewModel.CompactMode = (bool)e.NewValue;
            }
        }

        #endregion
    }

    /// <summary>
    /// ViewModel for StatusIndicator control
    /// StatusIndicator控件的ViewModel
    /// </summary>
    public class StatusIndicatorViewModel : ViewModelBase
    {
        private StatusModel? _status;
        private string _title = string.Empty;
        private string _message = string.Empty;
        private string _details = string.Empty;
        private string _timestampText = string.Empty;
        private Brush _statusColor = Brushes.Gray;
        private Brush _borderBrush = Brushes.Transparent;
        private Thickness _borderThickness = new Thickness(0);
        private bool _isLoading = false;
        private bool _compactMode = false;
        private string _actionButtonText = string.Empty;
        private string _secondaryButtonText = string.Empty;

        /// <summary>
        /// Gets or sets the status model
        /// 获取或设置状态模型
        /// </summary>
        public StatusModel? Status
        {
            get => _status;
            private set => SetProperty(ref _status, value);
        }

        /// <summary>
        /// Gets or sets the title text
        /// 获取或设置标题文本
        /// </summary>
        public string Title
        {
            get => _title;
            private set => SetProperty(ref _title, value);
        }

        /// <summary>
        /// Gets or sets the message text
        /// 获取或设置消息文本
        /// </summary>
        public string Message
        {
            get => _message;
            private set => SetProperty(ref _message, value);
        }

        /// <summary>
        /// Gets or sets the details text
        /// 获取或设置详情文本
        /// </summary>
        public string Details
        {
            get => _details;
            private set => SetProperty(ref _details, value);
        }

        /// <summary>
        /// Gets or sets the timestamp text
        /// 获取或设置时间戳文本
        /// </summary>
        public string TimestampText
        {
            get => _timestampText;
            private set => SetProperty(ref _timestampText, value);
        }

        /// <summary>
        /// Gets or sets the status color
        /// 获取或设置状态颜色
        /// </summary>
        public Brush StatusColor
        {
            get => _statusColor;
            private set => SetProperty(ref _statusColor, value);
        }

        /// <summary>
        /// Gets or sets the border brush
        /// 获取或设置边框画刷
        /// </summary>
        public Brush BorderBrush
        {
            get => _borderBrush;
            private set => SetProperty(ref _borderBrush, value);
        }

        /// <summary>
        /// Gets or sets the border thickness
        /// 获取或设置边框厚度
        /// </summary>
        public Thickness BorderThickness
        {
            get => _borderThickness;
            private set => SetProperty(ref _borderThickness, value);
        }

        /// <summary>
        /// Gets or sets whether the control is in loading state
        /// 获取或设置控件是否处于加载状态
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        /// <summary>
        /// Gets or sets whether the control is in compact mode
        /// 获取或设置控件是否处于紧凑模式
        /// </summary>
        public bool CompactMode
        {
            get => _compactMode;
            set
            {
                if (SetProperty(ref _compactMode, value))
                {
                    UpdateVisibility();
                }
            }
        }

        /// <summary>
        /// Gets or sets the action button text
        /// 获取或设置操作按钮文本
        /// </summary>
        public string ActionButtonText
        {
            get => _actionButtonText;
            private set => SetProperty(ref _actionButtonText, value);
        }

        /// <summary>
        /// Gets or sets the secondary button text
        /// 获取或设置次要按钮文本
        /// </summary>
        public string SecondaryButtonText
        {
            get => _secondaryButtonText;
            private set => SetProperty(ref _secondaryButtonText, value);
        }

        #region Visibility Properties

        public Visibility TitleVisibility => ShowTitle && !string.IsNullOrEmpty(Title) ? Visibility.Visible : Visibility.Collapsed;
        public Visibility MessageVisibility => ShowMessage && !string.IsNullOrEmpty(Message) ? Visibility.Visible : Visibility.Collapsed;
        public Visibility DetailsVisibility => ShowDetails && !string.IsNullOrEmpty(Details) && !CompactMode ? Visibility.Visible : Visibility.Collapsed;
        public Visibility TimestampVisibility => ShowTimestamp && !string.IsNullOrEmpty(TimestampText) && !CompactMode ? Visibility.Visible : Visibility.Collapsed;
        public Visibility LoadingIconVisibility => IsLoading ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ActionButtonVisibility => !string.IsNullOrEmpty(ActionButtonText) ? Visibility.Visible : Visibility.Collapsed;
        public Visibility SecondaryButtonVisibility => !string.IsNullOrEmpty(SecondaryButtonText) ? Visibility.Visible : Visibility.Collapsed;

        private bool ShowTitle => true; // This would be bound to the parent control property
        private bool ShowMessage => true; // This would be bound to the parent control property
        private bool ShowDetails => false; // This would be bound to the parent control property
        private bool ShowTimestamp => false; // This would be bound to the parent control property

        #endregion

        /// <summary>
        /// Update the status information
        /// 更新状态信息
        /// </summary>
        /// <param name="status">The new status</param>
        public void UpdateStatus(StatusModel? status)
        {
            Status = status;

            if (status == null)
            {
                Title = string.Empty;
                Message = string.Empty;
                Details = string.Empty;
                TimestampText = string.Empty;
                StatusColor = Brushes.Gray;
                IsLoading = false;
                UpdateBorder(StatusState.Unknown);
                UpdateVisibility();
                return;
            }

            Title = status.Title;
            Message = status.Message;
            Details = status.Details;
            TimestampText = FormatTimestamp(status.Timestamp);
            IsLoading = status.State == StatusState.Loading || status.State == StatusState.Working;

            // Update status color based on state
            StatusColor = GetStatusColor(status.State);

            // Update border based on state
            UpdateBorder(status.State);

            // Update action buttons based on status type and state
            UpdateActionButtons(status);

            UpdateVisibility();
        }

        private void UpdateVisibility()
        {
            OnPropertyChanged(nameof(TitleVisibility));
            OnPropertyChanged(nameof(MessageVisibility));
            OnPropertyChanged(nameof(DetailsVisibility));
            OnPropertyChanged(nameof(TimestampVisibility));
            OnPropertyChanged(nameof(LoadingIconVisibility));
            OnPropertyChanged(nameof(ActionButtonVisibility));
            OnPropertyChanged(nameof(SecondaryButtonVisibility));
        }

        private Brush GetStatusColor(StatusState state)
        {
            return state switch
            {
                StatusState.Unknown => Brushes.Gray,
                StatusState.Idle => new SolidColorBrush(Color.FromRgb(0x95, 0xA5, 0xA6)), // Gray
                StatusState.Ready => new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60)), // Green
                StatusState.Working => new SolidColorBrush(Color.FromRgb(0x34, 0x98, 0xDB)), // Blue
                StatusState.Loading => new SolidColorBrush(Color.FromRgb(0x34, 0x98, 0xDB)), // Blue
                StatusState.Success => new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60)), // Green
                StatusState.Warning => new SolidColorBrush(Color.FromRgb(0xF3, 0x9C, 0x12)), // Orange
                StatusState.Error => new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)), // Red
                StatusState.Offline => new SolidColorBrush(Color.FromRgb(0x7F, 0x8C, 0x8D)), // Gray
                _ => Brushes.Gray
            };
        }

        private void UpdateBorder(StatusState state)
        {
            switch (state)
            {
                case StatusState.Warning:
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0xF3, 0x9C, 0x12));
                    BorderThickness = new Thickness(1);
                    break;
                case StatusState.Error:
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C));
                    BorderThickness = new Thickness(1);
                    break;
                case StatusState.Success:
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60));
                    BorderThickness = new Thickness(1);
                    break;
                default:
                    BorderBrush = Brushes.Transparent;
                    BorderThickness = new Thickness(0);
                    break;
            }
        }

        private void UpdateActionButtons(StatusModel status)
        {
            ActionButtonText = string.Empty;
            SecondaryButtonText = string.Empty;

            // Set action buttons based on status type and state
            switch (status.Type)
            {
                case StatusType.Authentication:
                    if (status.State == StatusState.Error || status.State == StatusState.Offline)
                    {
                        ActionButtonText = "重新连接";
                    }
                    break;

                case StatusType.AITool:
                    if (status.State == StatusState.Error)
                    {
                        ActionButtonText = "重试";
                        SecondaryButtonText = "详情";
                    }
                    else if (status.State == StatusState.Offline)
                    {
                        ActionButtonText = "连接";
                    }
                    break;

                case StatusType.SystemCleanup:
                    if (status.State == StatusState.Ready)
                    {
                        ActionButtonText = "开始清理";
                    }
                    else if (status.State == StatusState.Error)
                    {
                        ActionButtonText = "重试";
                        SecondaryButtonText = "查看日志";
                    }
                    break;
            }
        }

        private string FormatTimestamp(DateTime timestamp)
        {
            var now = DateTime.Now;
            var diff = now - timestamp;

            if (diff.TotalMinutes < 1)
            {
                return "刚刚";
            }
            else if (diff.TotalHours < 1)
            {
                return $"{(int)diff.TotalMinutes}分钟前";
            }
            else if (diff.TotalDays < 1)
            {
                return $"{(int)diff.TotalHours}小时前";
            }
            else if (diff.TotalDays < 7)
            {
                return $"{(int)diff.TotalDays}天前";
            }
            else
            {
                return timestamp.ToString("MM/dd HH:mm");
            }
        }
    }

    /// <summary>
    /// Base class for ViewModels with property change notification
    /// 具有属性更改通知的ViewModel基类
    /// </summary>
    public abstract class ViewModelBase : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    /// <summary>
    /// Converter to invert boolean to visibility
    /// 将布尔值转换为相反的可见性的转换器
    /// </summary>
    public class InvertedBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility == Visibility.Collapsed;
            }
            return false;
        }
    }
}