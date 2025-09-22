using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Occop.Core.Authentication;

namespace Occop.UI.ViewModels
{
    /// <summary>
    /// ViewModel for login window interface
    /// 登录窗口界面的ViewModel，作为AuthenticationViewModel的包装器
    /// </summary>
    public partial class LoginViewModel : ObservableObject, IDisposable
    {
        private readonly AuthenticationViewModel _authenticationViewModel;
        private readonly ILogger<LoginViewModel> _logger;
        private bool _disposed = false;

        [ObservableProperty]
        private string _windowTitle = "Occop - 用户认证";

        [ObservableProperty]
        private string _welcomeMessage = "欢迎使用 Occop";

        [ObservableProperty]
        private string _subtitle = "请使用您的GitHub账号进行认证";

        public LoginViewModel(AuthenticationViewModel authenticationViewModel, ILogger<LoginViewModel> logger)
        {
            _authenticationViewModel = authenticationViewModel ?? throw new ArgumentNullException(nameof(authenticationViewModel));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _logger.LogInformation("LoginViewModel initialized");
        }

        /// <summary>
        /// 获取包装的AuthenticationViewModel
        /// </summary>
        public AuthenticationViewModel AuthenticationViewModel => _authenticationViewModel;

        /// <summary>
        /// 当前是否正在认证
        /// </summary>
        public bool IsAuthenticating => _authenticationViewModel.IsAuthenticating;

        /// <summary>
        /// 当前是否已认证
        /// </summary>
        public bool IsAuthenticated => _authenticationViewModel.IsAuthenticated;

        /// <summary>
        /// 当前认证用户
        /// </summary>
        public string? CurrentUserLogin => _authenticationViewModel.CurrentUserLogin;

        /// <summary>
        /// 认证状态消息
        /// </summary>
        public string? StatusMessage => _authenticationViewModel.StatusMessage;

        /// <summary>
        /// 错误消息
        /// </summary>
        public string? ErrorMessage => _authenticationViewModel.ErrorMessage;

        /// <summary>
        /// 是否有错误
        /// </summary>
        public bool HasError => _authenticationViewModel.HasError;

        /// <summary>
        /// 开始认证命令
        /// </summary>
        public Microsoft.Toolkit.Mvvm.Input.IAsyncRelayCommand StartAuthenticationCommand => _authenticationViewModel.StartAuthenticationCommand;

        /// <summary>
        /// 取消认证命令
        /// </summary>
        public Microsoft.Toolkit.Mvvm.Input.IAsyncRelayCommand CancelAuthenticationCommand => _authenticationViewModel.CancelAuthenticationCommand;

        /// <summary>
        /// 注销命令
        /// </summary>
        public Microsoft.Toolkit.Mvvm.Input.IAsyncRelayCommand SignOutCommand => _authenticationViewModel.SignOutCommand;

        /// <summary>
        /// 更新窗口标题为自定义消息
        /// </summary>
        /// <param name="message">自定义消息</param>
        public void UpdateWindowTitle(string message)
        {
            WindowTitle = $"Occop - {message}";
            _logger.LogDebug("Updated window title to: {Title}", WindowTitle);
        }

        /// <summary>
        /// 重置窗口标题为默认值
        /// </summary>
        public void ResetWindowTitle()
        {
            WindowTitle = "Occop - 用户认证";
            _logger.LogDebug("Reset window title to default");
        }

        /// <summary>
        /// 更新欢迎消息
        /// </summary>
        /// <param name="message">欢迎消息</param>
        public void UpdateWelcomeMessage(string message)
        {
            WelcomeMessage = message;
            _logger.LogDebug("Updated welcome message to: {Message}", message);
        }

        /// <summary>
        /// 更新副标题
        /// </summary>
        /// <param name="subtitle">副标题</param>
        public void UpdateSubtitle(string subtitle)
        {
            Subtitle = subtitle;
            _logger.LogDebug("Updated subtitle to: {Subtitle}", subtitle);
        }

        #region IDisposable

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Note: We don't dispose the AuthenticationViewModel here
                    // as it may be used elsewhere and is managed by DI container
                    _logger.LogInformation("LoginViewModel disposed");
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