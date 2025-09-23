using System;
using System.Drawing;

namespace Occop.Services
{
    /// <summary>
    /// Interface for system tray management
    /// 系统托盘管理接口
    /// </summary>
    public interface ITrayManager : IDisposable
    {
        /// <summary>
        /// Event raised when the user wants to show the main window
        /// 用户要求显示主窗口时触发的事件
        /// </summary>
        event EventHandler? ShowMainWindowRequested;

        /// <summary>
        /// Event raised when the user wants to exit the application
        /// 用户要求退出应用程序时触发的事件
        /// </summary>
        event EventHandler? ExitApplicationRequested;

        /// <summary>
        /// Event raised when the user wants to open settings
        /// 用户要求打开设置时触发的事件
        /// </summary>
        event EventHandler? OpenSettingsRequested;

        /// <summary>
        /// Gets a value indicating whether the tray icon is visible
        /// 获取托盘图标是否可见
        /// </summary>
        bool IsVisible { get; }

        /// <summary>
        /// Initialize the tray manager with main window handle
        /// 使用主窗口句柄初始化托盘管理器
        /// </summary>
        /// <param name="mainWindowHandle">The main window handle</param>
        void Initialize(IntPtr mainWindowHandle);

        /// <summary>
        /// Show the tray icon
        /// 显示托盘图标
        /// </summary>
        void Show();

        /// <summary>
        /// Hide the tray icon
        /// 隐藏托盘图标
        /// </summary>
        void Hide();

        /// <summary>
        /// Set the tray icon
        /// 设置托盘图标
        /// </summary>
        /// <param name="icon">The icon to display</param>
        void SetIcon(Icon icon);

        /// <summary>
        /// Set the tray icon tooltip text
        /// 设置托盘图标工具提示文本
        /// </summary>
        /// <param name="text">The tooltip text</param>
        void SetTooltipText(string text);

        /// <summary>
        /// Show a balloon tip notification
        /// 显示气球提示通知
        /// </summary>
        /// <param name="title">The notification title</param>
        /// <param name="text">The notification text</param>
        /// <param name="icon">The notification icon type</param>
        /// <param name="timeout">The timeout in milliseconds</param>
        void ShowBalloonTip(string title, string text, ToolTipIcon icon = ToolTipIcon.Info, int timeout = 3000);

        /// <summary>
        /// Update the tray icon based on application status
        /// 根据应用程序状态更新托盘图标
        /// </summary>
        /// <param name="status">The application status</param>
        void UpdateStatus(TrayStatus status);

        /// <summary>
        /// Set custom context menu items
        /// 设置自定义上下文菜单项
        /// </summary>
        /// <param name="menuItems">The menu items to add</param>
        void SetContextMenuItems(params TrayMenuItem[] menuItems);
    }

    /// <summary>
    /// Represents the status of the application for tray icon display
    /// 表示用于托盘图标显示的应用程序状态
    /// </summary>
    public enum TrayStatus
    {
        /// <summary>Idle state</summary>
        Idle,
        /// <summary>Authenticated and ready</summary>
        Ready,
        /// <summary>Working or processing</summary>
        Working,
        /// <summary>Error state</summary>
        Error,
        /// <summary>Disconnected or offline</summary>
        Disconnected
    }

    /// <summary>
    /// Represents a tray context menu item
    /// 表示托盘上下文菜单项
    /// </summary>
    public class TrayMenuItem
    {
        /// <summary>
        /// The display text of the menu item
        /// 菜单项的显示文本
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Action to execute when the menu item is clicked
        /// 点击菜单项时执行的操作
        /// </summary>
        public Action? Action { get; set; }

        /// <summary>
        /// Whether the menu item is a separator
        /// 菜单项是否为分隔符
        /// </summary>
        public bool IsSeparator { get; set; }

        /// <summary>
        /// Whether the menu item is enabled
        /// 菜单项是否启用
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Creates a regular menu item
        /// 创建常规菜单项
        /// </summary>
        /// <param name="text">The menu text</param>
        /// <param name="action">The action to execute</param>
        /// <param name="isEnabled">Whether the item is enabled</param>
        /// <returns>A new menu item</returns>
        public static TrayMenuItem Create(string text, Action action, bool isEnabled = true)
        {
            return new TrayMenuItem
            {
                Text = text,
                Action = action,
                IsEnabled = isEnabled
            };
        }

        /// <summary>
        /// Creates a separator menu item
        /// 创建分隔符菜单项
        /// </summary>
        /// <returns>A separator menu item</returns>
        public static TrayMenuItem CreateSeparator()
        {
            return new TrayMenuItem { IsSeparator = true };
        }
    }
}