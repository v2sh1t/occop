using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Occop.Services.Authentication;
using Occop.Services.Authentication;
using Occop.Services.Authentication.Models;
using System.Windows.Input;

namespace Occop.UI.ViewModels
{
    /// <summary>
    /// ViewModel for authentication interface
    /// 认证界面的ViewModel
    /// </summary>
    public partial class AuthenticationViewModel : ObservableObject, IDisposable
    {
        private readonly AuthenticationManager _authenticationManager;
        private readonly ILogger<AuthenticationViewModel> _logger;
        private bool _disposed = false;

        [ObservableProperty]
        private bool _isAuthenticating = false;

        [ObservableProperty]
        private bool _isAuthenticated = false;

        [ObservableProperty]
        private string? _deviceCode;

        [ObservableProperty]
        private string? _userCode;

        [ObservableProperty]
        private string? _verificationUrl;

        [ObservableProperty]
        private string? _statusMessage;

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        private bool _hasError = false;

        [ObservableProperty]
        private string? _currentUserLogin;

        [ObservableProperty]
        private int _authenticationProgress = 0;

        [ObservableProperty]
        private string _authenticationProgressText = "准备开始认证...";

        private CancellationTokenSource? _authenticationCancellationTokenSource;

        public AuthenticationViewModel(AuthenticationManager authenticationManager, ILogger<AuthenticationViewModel> logger)
        {
            _authenticationManager = authenticationManager ?? throw new ArgumentNullException(nameof(authenticationManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Subscribe to authentication events
            _authenticationManager.AuthenticationStateChanged += OnAuthenticationStateChanged;
            _authenticationManager.AuthenticationFailed += OnAuthenticationFailed;
            _authenticationManager.SessionExpired += OnSessionExpired;

            // Initialize commands
            StartAuthenticationCommand = new AsyncRelayCommand(StartAuthenticationAsync, CanStartAuthentication);
            CancelAuthenticationCommand = new AsyncRelayCommand(CancelAuthenticationAsync, CanCancelAuthentication);
            CopyDeviceCodeCommand = new RelayCommand<string>(CopyDeviceCode, CanCopyDeviceCode);
            CopyUserCodeCommand = new RelayCommand<string>(CopyUserCode, CanCopyUserCode);
            CopyVerificationUrlCommand = new RelayCommand<string>(CopyVerificationUrl, CanCopyVerificationUrl);
            OpenVerificationUrlCommand = new RelayCommand<string>(OpenVerificationUrl, CanOpenVerificationUrl);
            SignOutCommand = new AsyncRelayCommand(SignOutAsync, CanSignOut);

            // Initialize state
            UpdateAuthenticationState();
        }

        #region Commands

        public IAsyncRelayCommand StartAuthenticationCommand { get; }
        public IAsyncRelayCommand CancelAuthenticationCommand { get; }
        public IRelayCommand<string> CopyDeviceCodeCommand { get; }
        public IRelayCommand<string> CopyUserCodeCommand { get; }
        public IRelayCommand<string> CopyVerificationUrlCommand { get; }
        public IRelayCommand<string> OpenVerificationUrlCommand { get; }
        public IAsyncRelayCommand SignOutCommand { get; }

        #endregion

        #region Command Implementation

        private bool CanStartAuthentication() => !IsAuthenticating && !IsAuthenticated;

        private async Task StartAuthenticationAsync()
        {
            try
            {
                _logger.LogInformation("Starting GitHub OAuth authentication");

                ClearErrorState();
                IsAuthenticating = true;
                AuthenticationProgress = 0;
                AuthenticationProgressText = "正在初始化认证...";

                _authenticationCancellationTokenSource = new CancellationTokenSource();
                var cancellationToken = _authenticationCancellationTokenSource.Token;

                // Step 1: Request device code
                AuthenticationProgress = 10;
                AuthenticationProgressText = "正在请求设备码...";

                var deviceCodeResult = await _authenticationManager.StartAuthenticationAsync(cancellationToken);

                if (deviceCodeResult != null)
                {
                    DeviceCode = deviceCodeResult.DeviceCode;
                    UserCode = deviceCodeResult.UserCode;
                    VerificationUrl = deviceCodeResult.VerificationUri;

                    AuthenticationProgress = 30;
                    AuthenticationProgressText = "请在浏览器中完成认证...";
                    StatusMessage = $"请访问 {VerificationUrl} 并输入代码：{UserCode}";

                    // Step 2: Complete authentication
                    AuthenticationProgress = 50;
                    AuthenticationProgressText = "等待用户授权...";

                    var authResult = await _authenticationManager.CompleteAuthenticationAsync(deviceCodeResult, cancellationToken);

                    if (authResult.IsSuccess)
                    {
                        AuthenticationProgress = 100;
                        AuthenticationProgressText = "认证成功！";
                        StatusMessage = "认证成功！您现在可以使用应用程序。";
                    }
                    else
                    {
                        SetErrorState($"认证失败：{authResult.ErrorMessage}");
                    }
                }
                else
                {
                    SetErrorState("无法获取设备码，请检查网络连接并重试。");
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Authentication was cancelled by user");
                StatusMessage = "认证已被取消。";
                ClearDeviceCodeInfo();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during authentication");
                SetErrorState($"认证过程中发生错误：{ex.Message}");
            }
            finally
            {
                IsAuthenticating = false;
                _authenticationCancellationTokenSource?.Dispose();
                _authenticationCancellationTokenSource = null;

                // Update command states
                StartAuthenticationCommand.NotifyCanExecuteChanged();
                CancelAuthenticationCommand.NotifyCanExecuteChanged();
                SignOutCommand.NotifyCanExecuteChanged();
            }
        }

        private bool CanCancelAuthentication() => IsAuthenticating;

        private async Task CancelAuthenticationAsync()
        {
            try
            {
                _logger.LogInformation("Cancelling authentication");

                _authenticationCancellationTokenSource?.Cancel();
                StatusMessage = "正在取消认证...";

                await Task.Delay(500); // Give some time for cancellation to process

                ClearDeviceCodeInfo();
                StatusMessage = "认证已取消。";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling authentication");
                SetErrorState($"取消认证时发生错误：{ex.Message}");
            }
        }

        private bool CanCopyDeviceCode(string? deviceCode) => !string.IsNullOrEmpty(deviceCode);

        private void CopyDeviceCode(string? deviceCode)
        {
            if (!string.IsNullOrEmpty(deviceCode))
            {
                try
                {
                    System.Windows.Clipboard.SetText(deviceCode);
                    StatusMessage = "设备码已复制到剪贴板。";
                    _logger.LogDebug("Device code copied to clipboard");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error copying device code to clipboard");
                    SetErrorState("无法复制设备码到剪贴板。");
                }
            }
        }

        private bool CanCopyUserCode(string? userCode) => !string.IsNullOrEmpty(userCode);

        private void CopyUserCode(string? userCode)
        {
            if (!string.IsNullOrEmpty(userCode))
            {
                try
                {
                    System.Windows.Clipboard.SetText(userCode);
                    StatusMessage = "用户码已复制到剪贴板。";
                    _logger.LogDebug("User code copied to clipboard");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error copying user code to clipboard");
                    SetErrorState("无法复制用户码到剪贴板。");
                }
            }
        }

        private bool CanCopyVerificationUrl(string? verificationUrl) => !string.IsNullOrEmpty(verificationUrl);

        private void CopyVerificationUrl(string? verificationUrl)
        {
            if (!string.IsNullOrEmpty(verificationUrl))
            {
                try
                {
                    System.Windows.Clipboard.SetText(verificationUrl);
                    StatusMessage = "验证URL已复制到剪贴板。";
                    _logger.LogDebug("Verification URL copied to clipboard");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error copying verification URL to clipboard");
                    SetErrorState("无法复制验证URL到剪贴板。");
                }
            }
        }

        private bool CanOpenVerificationUrl(string? verificationUrl) => !string.IsNullOrEmpty(verificationUrl);

        private void OpenVerificationUrl(string? verificationUrl)
        {
            if (!string.IsNullOrEmpty(verificationUrl))
            {
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = verificationUrl,
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(psi);
                    StatusMessage = "已在浏览器中打开验证页面。";
                    _logger.LogDebug("Verification URL opened in browser");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error opening verification URL in browser");
                    SetErrorState("无法在浏览器中打开验证页面。");
                }
            }
        }

        private bool CanSignOut() => IsAuthenticated && !IsAuthenticating;

        private async Task SignOutAsync()
        {
            try
            {
                _logger.LogInformation("Signing out user");

                StatusMessage = "正在注销...";

                // Since AuthenticationManager doesn't have SignOutAsync, we'll clear the state directly
                ClearDeviceCodeInfo();
                CurrentUserLogin = null;
                IsAuthenticated = false;
                StatusMessage = "已成功注销。";

                _logger.LogInformation("User signed out successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during sign out");
                SetErrorState($"注销时发生错误：{ex.Message}");
            }
        }

        #endregion

        #region Event Handlers

        private void OnAuthenticationStateChanged(object? sender, AuthenticationStateChangedEventArgs e)
        {
            // Ensure UI updates happen on UI thread
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                UpdateAuthenticationState();

                switch (e.NewState)
                {
                    case AuthenticationState.Authenticated:
                        IsAuthenticated = true;
                        IsAuthenticating = false;
                        CurrentUserLogin = _authenticationManager.CurrentUserLogin;
                        AuthenticationProgress = 100;
                        AuthenticationProgressText = "认证成功！";
                        StatusMessage = $"欢迎，{CurrentUserLogin}！认证成功。";
                        ClearErrorState();
                        break;

                    case AuthenticationState.NotAuthenticated:
                        IsAuthenticated = false;
                        IsAuthenticating = false;
                        CurrentUserLogin = null;
                        AuthenticationProgress = 0;
                        AuthenticationProgressText = "未认证";
                        break;

                    case AuthenticationState.Authenticating:
                        IsAuthenticated = false;
                        IsAuthenticating = true;
                        break;

                    case AuthenticationState.LockedOut:
                        IsAuthenticated = false;
                        IsAuthenticating = false;
                        SetErrorState("账户已被锁定，请稍后再试。");
                        break;
                }

                // Update command states
                StartAuthenticationCommand.NotifyCanExecuteChanged();
                CancelAuthenticationCommand.NotifyCanExecuteChanged();
                SignOutCommand.NotifyCanExecuteChanged();
            });
        }

        private void OnAuthenticationFailed(object? sender, AuthenticationFailedEventArgs e)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                SetErrorState($"认证失败：{e.Reason}");
                IsAuthenticating = false;

                // Clear sensitive information
                ClearDeviceCodeInfo();

                // Update command states
                StartAuthenticationCommand.NotifyCanExecuteChanged();
                CancelAuthenticationCommand.NotifyCanExecuteChanged();
            });
        }

        private void OnSessionExpired(object? sender, SessionExpiredEventArgs e)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                IsAuthenticated = false;
                CurrentUserLogin = null;
                StatusMessage = "会话已过期，请重新认证。";

                // Update command states
                StartAuthenticationCommand.NotifyCanExecuteChanged();
                SignOutCommand.NotifyCanExecuteChanged();
            });
        }

        #endregion

        #region Helper Methods

        private void UpdateAuthenticationState()
        {
            IsAuthenticated = _authenticationManager.IsAuthenticated;
            CurrentUserLogin = _authenticationManager.CurrentUserLogin;

            if (IsAuthenticated)
            {
                StatusMessage = $"已认证用户：{CurrentUserLogin}";
                ClearErrorState();
            }
        }

        private void SetErrorState(string errorMessage)
        {
            ErrorMessage = errorMessage;
            HasError = true;
            _logger.LogError("Authentication error: {ErrorMessage}", errorMessage);
        }

        private void ClearErrorState()
        {
            ErrorMessage = null;
            HasError = false;
        }

        private void ClearDeviceCodeInfo()
        {
            DeviceCode = null;
            UserCode = null;
            VerificationUrl = null;
            AuthenticationProgress = 0;
            AuthenticationProgressText = "准备开始认证...";
        }

        #endregion

        #region IDisposable

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Unsubscribe from events
                    _authenticationManager.AuthenticationStateChanged -= OnAuthenticationStateChanged;
                    _authenticationManager.AuthenticationFailed -= OnAuthenticationFailed;
                    _authenticationManager.SessionExpired -= OnSessionExpired;

                    // Cancel any ongoing authentication
                    _authenticationCancellationTokenSource?.Cancel();
                    _authenticationCancellationTokenSource?.Dispose();
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