using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Occop.UI.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace Occop.UI
{
    /// <summary>
    /// LoginWindow.xaml 的交互逻辑
    /// 登录窗口，专门用于GitHub OAuth认证流程
    /// </summary>
    public partial class LoginWindow : Window
    {
        private readonly ILogger<LoginWindow> _logger;
        private readonly AuthenticationViewModel _viewModel;

        public LoginWindow(ILogger<LoginWindow> logger, AuthenticationViewModel viewModel)
        {
            InitializeComponent();

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            // 设置DataContext
            DataContext = _viewModel;
            AuthenticationView.DataContext = _viewModel;

            // 订阅认证状态变化事件
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            _logger.LogInformation("LoginWindow initialized");
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AuthenticationViewModel.IsAuthenticated))
            {
                // 当认证成功时，触发窗口关闭事件
                if (_viewModel.IsAuthenticated)
                {
                    _logger.LogInformation("Authentication successful, preparing to close login window");

                    // 设置对话框结果为成功
                    DialogResult = true;

                    // 延迟关闭以让用户看到成功消息
                    var timer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(2)
                    };
                    timer.Tick += (s, args) =>
                    {
                        timer.Stop();
                        Close();
                    };
                    timer.Start();
                }
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            try
            {
                // 取消订阅事件
                if (_viewModel != null)
                {
                    _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                }

                _logger.LogInformation("LoginWindow closing");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during LoginWindow closing");
            }

            base.OnClosing(e);
        }

        /// <summary>
        /// 显示登录对话框
        /// </summary>
        /// <param name="owner">父窗口</param>
        /// <returns>认证是否成功</returns>
        public bool ShowLoginDialog(Window? owner = null)
        {
            try
            {
                Owner = owner;
                WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen;

                _logger.LogInformation("Showing login dialog");

                return ShowDialog() == true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing login dialog");
                return false;
            }
        }
    }
}