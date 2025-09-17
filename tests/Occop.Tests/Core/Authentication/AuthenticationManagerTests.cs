using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Occop.Core.Authentication;
using Occop.Core.Security;
using Occop.Services.Authentication;
using System.Security;
using Xunit;

namespace Occop.Tests.Core.Authentication
{
    /// <summary>
    /// Unit tests for AuthenticationManager class
    /// AuthenticationManager类的单元测试
    /// </summary>
    public class AuthenticationManagerTests : IDisposable
    {
        private readonly Mock<GitHubAuthService> _mockGitHubAuthService;
        private readonly Mock<UserWhitelist> _mockUserWhitelist;
        private readonly Mock<SecureTokenManager> _mockSecureTokenManager;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<ILogger<AuthenticationManager>> _mockLogger;
        private AuthenticationManager? _authenticationManager;

        public AuthenticationManagerTests()
        {
            _mockGitHubAuthService = new Mock<GitHubAuthService>(
                Mock.Of<OAuthDeviceFlow>(),
                Mock.Of<IConfiguration>(),
                Mock.Of<ILogger<GitHubAuthService>>());

            _mockUserWhitelist = new Mock<UserWhitelist>(
                Mock.Of<IConfiguration>(),
                Mock.Of<ILogger<UserWhitelist>>());

            _mockSecureTokenManager = new Mock<SecureTokenManager>(
                Mock.Of<IConfiguration>(),
                Mock.Of<ILogger<SecureTokenManager>>(),
                Mock.Of<TokenStorage>());

            _mockConfiguration = new Mock<IConfiguration>();
            _mockLogger = new Mock<ILogger<AuthenticationManager>>();

            SetupDefaultConfiguration();
            SetupDefaultMockBehavior();
        }

        private void SetupDefaultConfiguration()
        {
            _mockConfiguration.Setup(c => c.GetValue<int>("Authentication:MaxFailedAttempts", 3)).Returns(3);
            _mockConfiguration.Setup(c => c.GetValue<int>("Authentication:LockoutDurationMinutes", 15)).Returns(15);
            _mockConfiguration.Setup(c => c.GetValue<int>("Authentication:SessionTimeoutMinutes", 480)).Returns(480);
        }

        private void SetupDefaultMockBehavior()
        {
            _mockSecureTokenManager.Setup(m => m.GetSecurityStatus())
                .Returns(new SecurityStatus { HasValidAccessToken = false, HasValidRefreshToken = false });

            _mockUserWhitelist.Setup(w => w.GetWhitelistInfo())
                .Returns(new WhitelistInfo { Mode = WhitelistMode.Disabled });
        }

        private AuthenticationManager CreateAuthenticationManager()
        {
            return new AuthenticationManager(
                _mockGitHubAuthService.Object,
                _mockUserWhitelist.Object,
                _mockSecureTokenManager.Object,
                _mockConfiguration.Object,
                _mockLogger.Object);
        }

        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            // Arrange & Act
            _authenticationManager = CreateAuthenticationManager();

            // Assert
            Assert.NotNull(_authenticationManager);
            Assert.Equal(AuthenticationState.NotAuthenticated, _authenticationManager.CurrentState);
            Assert.False(_authenticationManager.IsAuthenticated);
            Assert.Null(_authenticationManager.CurrentUserLogin);
        }

        [Fact]
        public void Constructor_WithNullGitHubAuthService_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new AuthenticationManager(null!, _mockUserWhitelist.Object, _mockSecureTokenManager.Object, _mockConfiguration.Object, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullUserWhitelist_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new AuthenticationManager(_mockGitHubAuthService.Object, null!, _mockSecureTokenManager.Object, _mockConfiguration.Object, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullSecureTokenManager_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new AuthenticationManager(_mockGitHubAuthService.Object, _mockUserWhitelist.Object, null!, _mockConfiguration.Object, _mockLogger.Object));
        }

        [Fact]
        public void IsAuthenticated_WithValidToken_ReturnsTrue()
        {
            // Arrange
            _mockSecureTokenManager.Setup(m => m.GetSecurityStatus())
                .Returns(new SecurityStatus { HasValidAccessToken = true });
            _authenticationManager = CreateAuthenticationManager();

            // Use reflection to set internal state for testing
            var stateField = typeof(AuthenticationManager).GetField("_currentState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            stateField?.SetValue(_authenticationManager, AuthenticationState.Authenticated);

            var userField = typeof(AuthenticationManager).GetField("_currentUserLogin", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            userField?.SetValue(_authenticationManager, "testuser");

            var timeField = typeof(AuthenticationManager).GetField("_lastAuthenticationTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            timeField?.SetValue(_authenticationManager, DateTime.UtcNow);

            // Act & Assert
            Assert.True(_authenticationManager.IsAuthenticated);
        }

        [Fact]
        public void IsLockedOut_WithTooManyFailedAttempts_ReturnsTrue()
        {
            // Arrange
            _authenticationManager = CreateAuthenticationManager();

            // Use reflection to set failed attempts
            var attemptsField = typeof(AuthenticationManager).GetField("_failedAuthenticationAttempts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            attemptsField?.SetValue(_authenticationManager, 3);

            var timeField = typeof(AuthenticationManager).GetField("_lastFailedAttemptTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            timeField?.SetValue(_authenticationManager, DateTime.UtcNow);

            // Act & Assert
            Assert.True(_authenticationManager.IsLockedOut);
            Assert.NotNull(_authenticationManager.LockoutExpirationTime);
        }

        [Fact]
        public async Task StartAuthenticationAsync_WhenNotLockedOut_CallsGitHubService()
        {
            // Arrange
            _authenticationManager = CreateAuthenticationManager();
            var expectedResult = new DeviceAuthorizationResult
            {
                DeviceCode = "device123",
                UserCode = "USER123",
                VerificationUri = "https://github.com/login/device"
            };

            _mockGitHubAuthService.Setup(s => s.StartAuthenticationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _authenticationManager.StartAuthenticationAsync();

            // Assert
            Assert.Equal(expectedResult.DeviceCode, result.DeviceCode);
            Assert.Equal(expectedResult.UserCode, result.UserCode);
            Assert.Equal(AuthenticationState.AwaitingUserAuthorization, _authenticationManager.CurrentState);
        }

        [Fact]
        public async Task StartAuthenticationAsync_WhenLockedOut_ThrowsInvalidOperationException()
        {
            // Arrange
            _authenticationManager = CreateAuthenticationManager();

            // Set lockout state
            var attemptsField = typeof(AuthenticationManager).GetField("_failedAuthenticationAttempts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            attemptsField?.SetValue(_authenticationManager, 5);

            var timeField = typeof(AuthenticationManager).GetField("_lastFailedAttemptTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            timeField?.SetValue(_authenticationManager, DateTime.UtcNow);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _authenticationManager.StartAuthenticationAsync());
        }

        [Fact]
        public async Task CompleteAuthenticationAsync_WithSuccessfulResult_UpdatesState()
        {
            // Arrange
            _authenticationManager = CreateAuthenticationManager();
            var successResult = new AuthenticationResult
            {
                Success = true,
                UserLogin = "testuser",
                Scopes = new[] { "user:email" }
            };

            _mockGitHubAuthService.Setup(s => s.CompleteAuthenticationAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(successResult);

            _mockUserWhitelist.Setup(w => w.IsUserAllowed("testuser")).Returns(true);

            var mockSecureString = new Mock<SecureString>();
            _mockGitHubAuthService.Setup(s => s.GetAccessToken()).Returns(mockSecureString.Object);

            // Act
            var result = await _authenticationManager.CompleteAuthenticationAsync("device123");

            // Assert
            Assert.True(result.Success);
            Assert.Equal("testuser", result.UserLogin);
        }

        [Fact]
        public async Task CompleteAuthenticationAsync_WithUnauthorizedUser_ReturnsFailed()
        {
            // Arrange
            _authenticationManager = CreateAuthenticationManager();
            var successResult = new AuthenticationResult
            {
                Success = true,
                UserLogin = "unauthorizeduser"
            };

            _mockGitHubAuthService.Setup(s => s.CompleteAuthenticationAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(successResult);

            _mockUserWhitelist.Setup(w => w.IsUserAllowed("unauthorizeduser")).Returns(false);

            // Act
            var result = await _authenticationManager.CompleteAuthenticationAsync("device123");

            // Assert
            Assert.False(result.Success);
            Assert.Contains("not authorized", result.ErrorMessage!);
            Assert.Equal(AuthenticationState.Forbidden, _authenticationManager.CurrentState);
        }

        [Fact]
        public void SignOut_ClearsAuthenticationState()
        {
            // Arrange
            _authenticationManager = CreateAuthenticationManager();

            // Set authenticated state
            var stateField = typeof(AuthenticationManager).GetField("_currentState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            stateField?.SetValue(_authenticationManager, AuthenticationState.Authenticated);

            var userField = typeof(AuthenticationManager).GetField("_currentUserLogin", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            userField?.SetValue(_authenticationManager, "testuser");

            // Act
            _authenticationManager.SignOut();

            // Assert
            Assert.Equal(AuthenticationState.SignedOut, _authenticationManager.CurrentState);
            Assert.Null(_authenticationManager.CurrentUserLogin);
            _mockGitHubAuthService.Verify(s => s.SignOut(), Times.Once);
            _mockSecureTokenManager.Verify(s => s.ClearAllSecurityState(), Times.Once);
        }

        [Fact]
        public void ValidateAuthentication_WithExpiredSession_ReturnsFalse()
        {
            // Arrange
            _mockSecureTokenManager.Setup(m => m.GetSecurityStatus())
                .Returns(new SecurityStatus { HasValidAccessToken = true });
            _authenticationManager = CreateAuthenticationManager();

            // Set authenticated state with expired session
            var stateField = typeof(AuthenticationManager).GetField("_currentState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            stateField?.SetValue(_authenticationManager, AuthenticationState.Authenticated);

            var userField = typeof(AuthenticationManager).GetField("_currentUserLogin", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            userField?.SetValue(_authenticationManager, "testuser");

            var timeField = typeof(AuthenticationManager).GetField("_lastAuthenticationTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            timeField?.SetValue(_authenticationManager, DateTime.UtcNow.AddHours(-10)); // Expired session

            // Act
            var isValid = _authenticationManager.ValidateAuthentication();

            // Assert
            Assert.False(isValid);
            Assert.Equal(AuthenticationState.SessionExpired, _authenticationManager.CurrentState);
        }

        [Fact]
        public void GetAuthenticationStatus_ReturnsCompleteStatus()
        {
            // Arrange
            _authenticationManager = CreateAuthenticationManager();
            var securityStatus = new SecurityStatus
            {
                HasValidAccessToken = true,
                AccessTokenExpirationTime = DateTime.UtcNow.AddHours(1)
            };
            _mockSecureTokenManager.Setup(m => m.GetSecurityStatus()).Returns(securityStatus);

            var whitelistInfo = new WhitelistInfo { Mode = WhitelistMode.AllowList };
            _mockUserWhitelist.Setup(w => w.GetWhitelistInfo()).Returns(whitelistInfo);

            // Act
            var status = _authenticationManager.GetAuthenticationStatus();

            // Assert
            Assert.NotNull(status);
            Assert.Equal(AuthenticationState.NotAuthenticated, status.CurrentState);
            Assert.False(status.IsAuthenticated);
            Assert.Equal(0, status.FailedAttempts);
            Assert.False(status.IsLockedOut);
            Assert.NotNull(status.SecurityStatus);
            Assert.NotNull(status.WhitelistInfo);
        }

        [Fact]
        public void AuthenticationStateChanged_Event_FiresOnStateChange()
        {
            // Arrange
            _authenticationManager = CreateAuthenticationManager();
            AuthenticationStateChangedEventArgs? firedEventArgs = null;
            _authenticationManager.AuthenticationStateChanged += (sender, e) => firedEventArgs = e;

            // Act
            _authenticationManager.SignOut();

            // Assert
            Assert.NotNull(firedEventArgs);
            Assert.Equal(AuthenticationState.NotAuthenticated, firedEventArgs.OldState);
            Assert.Equal(AuthenticationState.SignedOut, firedEventArgs.NewState);
        }

        [Fact]
        public void AuthenticationFailed_Event_FiresOnFailedAttempt()
        {
            // Arrange
            _authenticationManager = CreateAuthenticationManager();
            AuthenticationFailedEventArgs? firedEventArgs = null;
            _authenticationManager.AuthenticationFailed += (sender, e) => firedEventArgs = e;

            var failedResult = new AuthenticationResult
            {
                Success = false,
                ErrorMessage = "Invalid credentials"
            };

            _mockGitHubAuthService.Setup(s => s.CompleteAuthenticationAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(failedResult);

            // Act
            var result = _authenticationManager.CompleteAuthenticationAsync("device123").Result;

            // Assert
            Assert.NotNull(firedEventArgs);
            Assert.Contains("Invalid credentials", firedEventArgs.Reason);
            Assert.Equal(1, firedEventArgs.AttemptNumber);
        }

        [Fact]
        public void Dispose_UnsubscribesFromEvents()
        {
            // Arrange
            _authenticationManager = CreateAuthenticationManager();

            // Act
            _authenticationManager.Dispose();

            // Assert - Operations after dispose should throw
            Assert.Throws<ObjectDisposedException>(() => _authenticationManager.CurrentState);
        }

        [Fact]
        public void Operations_AfterDispose_ThrowObjectDisposedException()
        {
            // Arrange
            _authenticationManager = CreateAuthenticationManager();
            _authenticationManager.Dispose();

            // Act & Assert
            Assert.Throws<ObjectDisposedException>(() => _authenticationManager.CurrentState);
            Assert.Throws<ObjectDisposedException>(() => _authenticationManager.IsAuthenticated);
            Assert.Throws<ObjectDisposedException>(() => _authenticationManager.ValidateAuthentication());
        }

        public void Dispose()
        {
            _authenticationManager?.Dispose();
        }
    }
}