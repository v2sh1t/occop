using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Occop.UI.Controls
{
    /// <summary>
    /// Custom control for displaying device code in authentication UI
    /// 用于在认证界面中显示设备码的自定义控件
    /// </summary>
    public class DeviceCodeControl : Control
    {
        static DeviceCodeControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DeviceCodeControl),
                new FrameworkPropertyMetadata(typeof(DeviceCodeControl)));
        }

        #region Dependency Properties

        /// <summary>
        /// Device code to display
        /// 要显示的设备码
        /// </summary>
        public static readonly DependencyProperty DeviceCodeProperty =
            DependencyProperty.Register(
                nameof(DeviceCode),
                typeof(string),
                typeof(DeviceCodeControl),
                new PropertyMetadata(string.Empty, OnDeviceCodeChanged));

        /// <summary>
        /// User code to display (typically shorter than device code)
        /// 要显示的用户码（通常比设备码短）
        /// </summary>
        public static readonly DependencyProperty UserCodeProperty =
            DependencyProperty.Register(
                nameof(UserCode),
                typeof(string),
                typeof(DeviceCodeControl),
                new PropertyMetadata(string.Empty, OnUserCodeChanged));

        /// <summary>
        /// Verification URL for authentication
        /// 认证验证URL
        /// </summary>
        public static readonly DependencyProperty VerificationUrlProperty =
            DependencyProperty.Register(
                nameof(VerificationUrl),
                typeof(string),
                typeof(DeviceCodeControl),
                new PropertyMetadata(string.Empty));

        /// <summary>
        /// Whether the device code is currently visible
        /// 设备码当前是否可见
        /// </summary>
        public static readonly DependencyProperty IsVisibleProperty =
            DependencyProperty.Register(
                nameof(IsVisible),
                typeof(bool),
                typeof(DeviceCodeControl),
                new PropertyMetadata(false, OnVisibilityChanged));

        /// <summary>
        /// Command to copy device code to clipboard
        /// 复制设备码到剪贴板的命令
        /// </summary>
        public static readonly DependencyProperty CopyDeviceCodeCommandProperty =
            DependencyProperty.Register(
                nameof(CopyDeviceCodeCommand),
                typeof(ICommand),
                typeof(DeviceCodeControl),
                new PropertyMetadata(null));

        /// <summary>
        /// Command to copy user code to clipboard
        /// 复制用户码到剪贴板的命令
        /// </summary>
        public static readonly DependencyProperty CopyUserCodeCommandProperty =
            DependencyProperty.Register(
                nameof(CopyUserCodeCommand),
                typeof(ICommand),
                typeof(DeviceCodeControl),
                new PropertyMetadata(null));

        /// <summary>
        /// Command to copy verification URL to clipboard
        /// 复制验证URL到剪贴板的命令
        /// </summary>
        public static readonly DependencyProperty CopyUrlCommandProperty =
            DependencyProperty.Register(
                nameof(CopyUrlCommand),
                typeof(ICommand),
                typeof(DeviceCodeControl),
                new PropertyMetadata(null));

        /// <summary>
        /// Command to open verification URL in browser
        /// 在浏览器中打开验证URL的命令
        /// </summary>
        public static readonly DependencyProperty OpenUrlCommandProperty =
            DependencyProperty.Register(
                nameof(OpenUrlCommand),
                typeof(ICommand),
                typeof(DeviceCodeControl),
                new PropertyMetadata(null));

        /// <summary>
        /// Highlight color for the device code
        /// 设备码的高亮颜色
        /// </summary>
        public static readonly DependencyProperty HighlightBrushProperty =
            DependencyProperty.Register(
                nameof(HighlightBrush),
                typeof(Brush),
                typeof(DeviceCodeControl),
                new PropertyMetadata(Brushes.LightBlue));

        /// <summary>
        /// Font size for the device code
        /// 设备码的字体大小
        /// </summary>
        public static readonly DependencyProperty CodeFontSizeProperty =
            DependencyProperty.Register(
                nameof(CodeFontSize),
                typeof(double),
                typeof(DeviceCodeControl),
                new PropertyMetadata(18.0));

        /// <summary>
        /// Font family for the device code
        /// 设备码的字体
        /// </summary>
        public static readonly DependencyProperty CodeFontFamilyProperty =
            DependencyProperty.Register(
                nameof(CodeFontFamily),
                typeof(FontFamily),
                typeof(DeviceCodeControl),
                new PropertyMetadata(new FontFamily("Consolas, Monaco, 'Courier New', monospace")));

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the device code to display
        /// 获取或设置要显示的设备码
        /// </summary>
        public string DeviceCode
        {
            get => (string)GetValue(DeviceCodeProperty);
            set => SetValue(DeviceCodeProperty, value);
        }

        /// <summary>
        /// Gets or sets the user code to display
        /// 获取或设置要显示的用户码
        /// </summary>
        public string UserCode
        {
            get => (string)GetValue(UserCodeProperty);
            set => SetValue(UserCodeProperty, value);
        }

        /// <summary>
        /// Gets or sets the verification URL
        /// 获取或设置验证URL
        /// </summary>
        public string VerificationUrl
        {
            get => (string)GetValue(VerificationUrlProperty);
            set => SetValue(VerificationUrlProperty, value);
        }

        /// <summary>
        /// Gets or sets whether the device code is visible
        /// 获取或设置设备码是否可见
        /// </summary>
        public bool IsVisible
        {
            get => (bool)GetValue(IsVisibleProperty);
            set => SetValue(IsVisibleProperty, value);
        }

        /// <summary>
        /// Gets or sets the command to copy device code
        /// 获取或设置复制设备码的命令
        /// </summary>
        public ICommand? CopyDeviceCodeCommand
        {
            get => (ICommand?)GetValue(CopyDeviceCodeCommandProperty);
            set => SetValue(CopyDeviceCodeCommandProperty, value);
        }

        /// <summary>
        /// Gets or sets the command to copy user code
        /// 获取或设置复制用户码的命令
        /// </summary>
        public ICommand? CopyUserCodeCommand
        {
            get => (ICommand?)GetValue(CopyUserCodeCommandProperty);
            set => SetValue(CopyUserCodeCommandProperty, value);
        }

        /// <summary>
        /// Gets or sets the command to copy verification URL
        /// 获取或设置复制验证URL的命令
        /// </summary>
        public ICommand? CopyUrlCommand
        {
            get => (ICommand?)GetValue(CopyUrlCommandProperty);
            set => SetValue(CopyUrlCommandProperty, value);
        }

        /// <summary>
        /// Gets or sets the command to open verification URL
        /// 获取或设置打开验证URL的命令
        /// </summary>
        public ICommand? OpenUrlCommand
        {
            get => (ICommand?)GetValue(OpenUrlCommandProperty);
            set => SetValue(OpenUrlCommandProperty, value);
        }

        /// <summary>
        /// Gets or sets the highlight brush for the device code
        /// 获取或设置设备码的高亮画刷
        /// </summary>
        public Brush HighlightBrush
        {
            get => (Brush)GetValue(HighlightBrushProperty);
            set => SetValue(HighlightBrushProperty, value);
        }

        /// <summary>
        /// Gets or sets the font size for the device code
        /// 获取或设置设备码的字体大小
        /// </summary>
        public double CodeFontSize
        {
            get => (double)GetValue(CodeFontSizeProperty);
            set => SetValue(CodeFontSizeProperty, value);
        }

        /// <summary>
        /// Gets or sets the font family for the device code
        /// 获取或设置设备码的字体
        /// </summary>
        public FontFamily CodeFontFamily
        {
            get => (FontFamily)GetValue(CodeFontFamilyProperty);
            set => SetValue(CodeFontFamilyProperty, value);
        }

        #endregion

        #region Events

        /// <summary>
        /// Event fired when device code is copied
        /// 设备码被复制时触发的事件
        /// </summary>
        public event EventHandler<CodeCopiedEventArgs>? DeviceCodeCopied;

        /// <summary>
        /// Event fired when user code is copied
        /// 用户码被复制时触发的事件
        /// </summary>
        public event EventHandler<CodeCopiedEventArgs>? UserCodeCopied;

        /// <summary>
        /// Event fired when verification URL is copied
        /// 验证URL被复制时触发的事件
        /// </summary>
        public event EventHandler<CodeCopiedEventArgs>? UrlCopied;

        /// <summary>
        /// Event fired when verification URL is opened
        /// 验证URL被打开时触发的事件
        /// </summary>
        public event EventHandler<UrlOpenedEventArgs>? UrlOpened;

        #endregion

        #region Event Handling

        private static void OnDeviceCodeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DeviceCodeControl control)
            {
                control.OnDeviceCodeChanged((string)e.OldValue, (string)e.NewValue);
            }
        }

        private static void OnUserCodeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DeviceCodeControl control)
            {
                control.OnUserCodeChanged((string)e.OldValue, (string)e.NewValue);
            }
        }

        private static void OnVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DeviceCodeControl control)
            {
                control.OnVisibilityChanged((bool)e.OldValue, (bool)e.NewValue);
            }
        }

        protected virtual void OnDeviceCodeChanged(string oldValue, string newValue)
        {
            // Update visibility based on code availability
            if (!string.IsNullOrEmpty(newValue) && string.IsNullOrEmpty(oldValue))
            {
                IsVisible = true;
            }
            else if (string.IsNullOrEmpty(newValue))
            {
                IsVisible = false;
            }
        }

        protected virtual void OnUserCodeChanged(string oldValue, string newValue)
        {
            // Similar visibility logic for user code
            if (!string.IsNullOrEmpty(newValue) && string.IsNullOrEmpty(oldValue))
            {
                IsVisible = true;
            }
        }

        protected virtual void OnVisibilityChanged(bool oldValue, bool newValue)
        {
            // Update visual state based on visibility
            VisualStateManager.GoToState(this, newValue ? "Visible" : "Hidden", true);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Formats the device code for display with spacing
        /// 格式化设备码以便显示时有适当间距
        /// </summary>
        /// <param name="code">The code to format</param>
        /// <returns>Formatted code with spacing</returns>
        public static string FormatCode(string code)
        {
            if (string.IsNullOrEmpty(code))
                return string.Empty;

            // Add space every 4 characters for better readability
            var formatted = "";
            for (int i = 0; i < code.Length; i++)
            {
                if (i > 0 && i % 4 == 0)
                    formatted += " ";
                formatted += code[i];
            }
            return formatted;
        }

        /// <summary>
        /// Triggers the device code copied event
        /// 触发设备码已复制事件
        /// </summary>
        public void OnDeviceCodeCopied()
        {
            DeviceCodeCopied?.Invoke(this, new CodeCopiedEventArgs(DeviceCode, "Device Code"));
        }

        /// <summary>
        /// Triggers the user code copied event
        /// 触发用户码已复制事件
        /// </summary>
        public void OnUserCodeCopied()
        {
            UserCodeCopied?.Invoke(this, new CodeCopiedEventArgs(UserCode, "User Code"));
        }

        /// <summary>
        /// Triggers the URL copied event
        /// 触发URL已复制事件
        /// </summary>
        public void OnUrlCopied()
        {
            UrlCopied?.Invoke(this, new CodeCopiedEventArgs(VerificationUrl, "Verification URL"));
        }

        /// <summary>
        /// Triggers the URL opened event
        /// 触发URL已打开事件
        /// </summary>
        public void OnUrlOpened()
        {
            UrlOpened?.Invoke(this, new UrlOpenedEventArgs(VerificationUrl));
        }

        #endregion
    }

    #region Event Args

    /// <summary>
    /// Event arguments for code copied events
    /// 代码已复制事件的参数
    /// </summary>
    public class CodeCopiedEventArgs : EventArgs
    {
        public string Code { get; }
        public string CodeType { get; }

        public CodeCopiedEventArgs(string code, string codeType)
        {
            Code = code;
            CodeType = codeType;
        }
    }

    /// <summary>
    /// Event arguments for URL opened events
    /// URL已打开事件的参数
    /// </summary>
    public class UrlOpenedEventArgs : EventArgs
    {
        public string Url { get; }

        public UrlOpenedEventArgs(string url)
        {
            Url = url;
        }
    }

    #endregion
}