using Microsoft.Extensions.Logging;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Occop.Services
{
    /// <summary>
    /// Implementation of system tray management
    /// 系统托盘管理实现
    /// </summary>
    public class TrayManager : ITrayManager
    {
        private readonly ILogger<TrayManager> _logger;
        private NotifyIcon? _notifyIcon;
        private ContextMenuStrip? _contextMenu;
        private IntPtr _mainWindowHandle;
        private bool _disposed = false;

        // Icon resources for different states
        private Icon? _idleIcon;
        private Icon? _readyIcon;
        private Icon? _workingIcon;
        private Icon? _errorIcon;
        private Icon? _disconnectedIcon;

        public TrayManager(ILogger<TrayManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Events

        public event EventHandler? ShowMainWindowRequested;
        public event EventHandler? ExitApplicationRequested;
        public event EventHandler? OpenSettingsRequested;

        #endregion

        #region Properties

        public bool IsVisible => _notifyIcon?.Visible ?? false;

        #endregion

        #region Public Methods

        public void Initialize(IntPtr mainWindowHandle)
        {
            try
            {
                _mainWindowHandle = mainWindowHandle;

                // Initialize NotifyIcon
                _notifyIcon = new NotifyIcon
                {
                    Text = "Occop - 工具管理中心",
                    Visible = false
                };

                // Load icons
                LoadIcons();

                // Set default icon
                SetIcon(_idleIcon ?? CreateDefaultIcon());

                // Set up event handlers
                _notifyIcon.MouseDoubleClick += OnTrayIconDoubleClick;
                _notifyIcon.MouseClick += OnTrayIconClick;

                // Create default context menu
                CreateDefaultContextMenu();

                _logger.LogInformation("TrayManager initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize TrayManager");
                throw;
            }
        }

        public void Show()
        {
            try
            {
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = true;
                    _logger.LogDebug("Tray icon shown");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show tray icon");
            }
        }

        public void Hide()
        {
            try
            {
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _logger.LogDebug("Tray icon hidden");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to hide tray icon");
            }
        }

        public void SetIcon(Icon icon)
        {
            try
            {
                if (_notifyIcon != null && icon != null)
                {
                    _notifyIcon.Icon = icon;
                    _logger.LogDebug("Tray icon updated");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set tray icon");
            }
        }

        public void SetTooltipText(string text)
        {
            try
            {
                if (_notifyIcon != null)
                {
                    // NotifyIcon.Text has a 63 character limit
                    _notifyIcon.Text = text.Length > 63 ? text.Substring(0, 60) + "..." : text;
                    _logger.LogDebug("Tray tooltip updated: {Text}", text);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set tooltip text");
            }
        }

        public void ShowBalloonTip(string title, string text, ToolTipIcon icon = ToolTipIcon.Info, int timeout = 3000)
        {
            try
            {
                if (_notifyIcon != null)
                {
                    _notifyIcon.ShowBalloonTip(timeout, title, text, icon);
                    _logger.LogDebug("Balloon tip shown: {Title} - {Text}", title, text);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show balloon tip");
            }
        }

        public void UpdateStatus(TrayStatus status)
        {
            try
            {
                Icon iconToUse = status switch
                {
                    TrayStatus.Idle => _idleIcon,
                    TrayStatus.Ready => _readyIcon,
                    TrayStatus.Working => _workingIcon,
                    TrayStatus.Error => _errorIcon,
                    TrayStatus.Disconnected => _disconnectedIcon,
                    _ => _idleIcon
                } ?? CreateDefaultIcon();

                SetIcon(iconToUse);

                string tooltipText = status switch
                {
                    TrayStatus.Idle => "Occop - 空闲",
                    TrayStatus.Ready => "Occop - 就绪",
                    TrayStatus.Working => "Occop - 工作中...",
                    TrayStatus.Error => "Occop - 错误",
                    TrayStatus.Disconnected => "Occop - 未连接",
                    _ => "Occop - 工具管理中心"
                };

                SetTooltipText(tooltipText);

                _logger.LogDebug("Tray status updated to: {Status}", status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update tray status");
            }
        }

        public void SetContextMenuItems(params TrayMenuItem[] menuItems)
        {
            try
            {
                if (_contextMenu != null)
                {
                    _contextMenu.Items.Clear();

                    foreach (var menuItem in menuItems)
                    {
                        if (menuItem.IsSeparator)
                        {
                            _contextMenu.Items.Add(new ToolStripSeparator());
                        }
                        else
                        {
                            var item = new ToolStripMenuItem(menuItem.Text)
                            {
                                Enabled = menuItem.IsEnabled
                            };

                            if (menuItem.Action != null)
                            {
                                item.Click += (sender, e) => menuItem.Action.Invoke();
                            }

                            _contextMenu.Items.Add(item);
                        }
                    }

                    _logger.LogDebug("Context menu updated with {Count} items", menuItems.Length);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set context menu items");
            }
        }

        #endregion

        #region Private Methods

        private void LoadIcons()
        {
            try
            {
                // Try to load icons from resources or create default ones
                _idleIcon = LoadIconFromResource("idle.ico") ?? CreateDefaultIcon();
                _readyIcon = LoadIconFromResource("ready.ico") ?? CreateReadyIcon();
                _workingIcon = LoadIconFromResource("working.ico") ?? CreateWorkingIcon();
                _errorIcon = LoadIconFromResource("error.ico") ?? CreateErrorIcon();
                _disconnectedIcon = LoadIconFromResource("disconnected.ico") ?? CreateDisconnectedIcon();

                _logger.LogDebug("Icons loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load some icons, using defaults");

                // Ensure we have at least default icons
                _idleIcon ??= CreateDefaultIcon();
                _readyIcon ??= CreateReadyIcon();
                _workingIcon ??= CreateWorkingIcon();
                _errorIcon ??= CreateErrorIcon();
                _disconnectedIcon ??= CreateDisconnectedIcon();
            }
        }

        private Icon? LoadIconFromResource(string iconName)
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var resourceName = $"Occop.UI.Resources.Icons.{iconName}";

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    return new Icon(stream);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not load icon from resource: {IconName}", iconName);
            }

            return null;
        }

        private Icon CreateDefaultIcon()
        {
            // Create a simple 16x16 gray circle icon
            var bitmap = new Bitmap(16, 16);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.FillEllipse(Brushes.Gray, 2, 2, 12, 12);
            }
            return Icon.FromHandle(bitmap.GetHicon());
        }

        private Icon CreateReadyIcon()
        {
            // Create a simple 16x16 green circle icon
            var bitmap = new Bitmap(16, 16);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.FillEllipse(Brushes.Green, 2, 2, 12, 12);
            }
            return Icon.FromHandle(bitmap.GetHicon());
        }

        private Icon CreateWorkingIcon()
        {
            // Create a simple 16x16 blue circle icon
            var bitmap = new Bitmap(16, 16);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.FillEllipse(Brushes.Blue, 2, 2, 12, 12);
            }
            return Icon.FromHandle(bitmap.GetHicon());
        }

        private Icon CreateErrorIcon()
        {
            // Create a simple 16x16 red circle icon
            var bitmap = new Bitmap(16, 16);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.FillEllipse(Brushes.Red, 2, 2, 12, 12);
            }
            return Icon.FromHandle(bitmap.GetHicon());
        }

        private Icon CreateDisconnectedIcon()
        {
            // Create a simple 16x16 dark gray circle icon
            var bitmap = new Bitmap(16, 16);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.FillEllipse(Brushes.DarkGray, 2, 2, 12, 12);
            }
            return Icon.FromHandle(bitmap.GetHicon());
        }

        private void CreateDefaultContextMenu()
        {
            try
            {
                _contextMenu = new ContextMenuStrip();

                var showItem = new ToolStripMenuItem("显示主窗口");
                showItem.Click += (sender, e) => OnShowMainWindow();
                _contextMenu.Items.Add(showItem);

                _contextMenu.Items.Add(new ToolStripSeparator());

                var settingsItem = new ToolStripMenuItem("设置");
                settingsItem.Click += (sender, e) => OnOpenSettings();
                _contextMenu.Items.Add(settingsItem);

                _contextMenu.Items.Add(new ToolStripSeparator());

                var exitItem = new ToolStripMenuItem("退出");
                exitItem.Click += (sender, e) => OnExitApplication();
                _contextMenu.Items.Add(exitItem);

                if (_notifyIcon != null)
                {
                    _notifyIcon.ContextMenuStrip = _contextMenu;
                }

                _logger.LogDebug("Default context menu created");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create default context menu");
            }
        }

        private void OnTrayIconDoubleClick(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                OnShowMainWindow();
            }
        }

        private void OnTrayIconClick(object? sender, MouseEventArgs e)
        {
            // Handle single click if needed
            // Currently, context menu handles right-click automatically
        }

        private void OnShowMainWindow()
        {
            try
            {
                ShowMainWindowRequested?.Invoke(this, EventArgs.Empty);
                _logger.LogDebug("Show main window requested from tray");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling show main window request");
            }
        }

        private void OnOpenSettings()
        {
            try
            {
                OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
                _logger.LogDebug("Open settings requested from tray");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling open settings request");
            }
        }

        private void OnExitApplication()
        {
            try
            {
                ExitApplicationRequested?.Invoke(this, EventArgs.Empty);
                _logger.LogDebug("Exit application requested from tray");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling exit application request");
            }
        }

        #endregion

        #region IDisposable

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    try
                    {
                        // Hide and dispose of the notify icon
                        if (_notifyIcon != null)
                        {
                            _notifyIcon.Visible = false;
                            _notifyIcon.Dispose();
                            _notifyIcon = null;
                        }

                        // Dispose of context menu
                        _contextMenu?.Dispose();
                        _contextMenu = null;

                        // Dispose of icons
                        _idleIcon?.Dispose();
                        _readyIcon?.Dispose();
                        _workingIcon?.Dispose();
                        _errorIcon?.Dispose();
                        _disconnectedIcon?.Dispose();

                        _logger.LogDebug("TrayManager disposed successfully");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during TrayManager disposal");
                    }
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