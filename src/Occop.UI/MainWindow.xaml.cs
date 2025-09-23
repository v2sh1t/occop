using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Occop.UI.ViewModels;
using Occop.Services;
using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;

namespace Occop.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// MainWindow.xaml的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ILogger<MainWindow> _logger;
        private readonly MainViewModel _viewModel;
        private readonly ITrayManager _trayManager;

        public MainWindow(ILogger<MainWindow> logger, MainViewModel viewModel, ITrayManager trayManager)
        {
            InitializeComponent();

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _trayManager = trayManager ?? throw new ArgumentNullException(nameof(trayManager));

            // Set the DataContext
            DataContext = _viewModel;

            // Initialize tray manager
            InitializeTrayManager();

            _logger.LogInformation("MainWindow initialized");
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            try
            {
                _logger.LogInformation("MainWindow closing");

                // Don't actually close, just minimize to tray
                e.Cancel = true;
                MinimizeToTray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during MainWindow closing");
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Get window handle and initialize tray after window is created
            var helper = new WindowInteropHelper(this);
            _trayManager.Initialize(helper.Handle);
            _trayManager.Show();
        }

        private void InitializeTrayManager()
        {
            try
            {
                // Subscribe to tray manager events
                _trayManager.ShowMainWindowRequested += OnShowMainWindowRequested;
                _trayManager.ExitApplicationRequested += OnExitApplicationRequested;
                _trayManager.OpenSettingsRequested += OnOpenSettingsRequested;

                // Set initial status
                _trayManager.UpdateStatus(TrayStatus.Idle);

                _logger.LogInformation("Tray manager initialized");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize tray manager");
            }
        }

        private void OnShowMainWindowRequested(object? sender, EventArgs e)
        {
            try
            {
                ShowFromTray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing main window from tray");
            }
        }

        private void OnExitApplicationRequested(object? sender, EventArgs e)
        {
            try
            {
                _logger.LogInformation("Exit requested from tray");

                // Dispose of ViewModel and tray manager
                _viewModel?.Dispose();
                _trayManager?.Dispose();

                // Actually close the application
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during application exit");
            }
        }

        private void OnOpenSettingsRequested(object? sender, EventArgs e)
        {
            try
            {
                // Show main window first
                ShowFromTray();

                // Trigger settings command in ViewModel
                if (_viewModel.OpenSettingsCommand.CanExecute(null))
                {
                    _viewModel.OpenSettingsCommand.Execute(null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening settings from tray");
            }
        }

        private void MinimizeToTray()
        {
            try
            {
                // Hide the window
                Hide();

                // Show notification on first minimize
                _trayManager.ShowBalloonTip(
                    "Occop",
                    "应用程序已最小化到系统托盘",
                    System.Windows.Forms.ToolTipIcon.Info,
                    2000);

                _logger.LogDebug("Window minimized to tray");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error minimizing to tray");
            }
        }

        private void ShowFromTray()
        {
            try
            {
                // Restore window
                Show();
                WindowState = WindowState.Normal;
                Activate();
                Topmost = true;
                Topmost = false;
                Focus();

                _logger.LogDebug("Window restored from tray");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring window from tray");
            }
        }
    }
}