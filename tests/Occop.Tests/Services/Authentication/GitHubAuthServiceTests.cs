using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Occop.Services.Authentication;
using Occop.Services.Authentication.Models;
using Xunit;

namespace Occop.Tests.Services.Authentication
{
    /// <summary>
    /// Tests for GitHubAuthService class
    /// GitHubAuthService类的测试
    /// </summary>
    public class GitHubAuthServiceTests : IDisposable
    {
        private readonly Mock<OAuthDeviceFlow> _mockDeviceFlow;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<ILogger<GitHubAuthService>> _mockLogger;
        private readonly GitHubAuthService _authService;

        public GitHubAuthServiceTests()
        {
            _mockDeviceFlow = new Mock<OAuthDeviceFlow>(
                Mock.Of<HttpClient>(),
                Mock.Of<ILogger<OAuthDeviceFlow>>());
            _mockConfiguration = new Mock<IConfiguration>();
            _mockLogger = new Mock<ILogger<GitHubAuthService>>();

            // Setup default configuration
            _mockConfiguration.Setup(c => c["GitHub:ClientId"]).Returns("test_client_id");
            _mockConfiguration.Setup(c => c["GitHub:DefaultScopes"]).Returns("user:email,read:user");

            _authService = new GitHubAuthService(
                _mockDeviceFlow.Object,
                _mockConfiguration.Object,
                _mockLogger.Object);
        }

        [Fact]
        public void Constructor_WhenDeviceFlowIsNull_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new GitHubAuthService(null!, _mockConfiguration.Object, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WhenConfigurationIsNull_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new GitHubAuthService(_mockDeviceFlow.Object, null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WhenLoggerIsNull_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new GitHubAuthService(_mockDeviceFlow.Object, _mockConfiguration.Object, null!));
        }

        [Fact]
        public void IsAuthenticated_WhenNotAuthenticated_ShouldReturnFalse()
        {
            // Act & Assert
            _authService.IsAuthenticated.Should().BeFalse();
            _authService.CurrentUserLogin.Should().BeNull();
            _authService.AuthorizedScopes.Should().BeNull();
        }

        [Fact]
        public async Task StartAuthenticationAsync_WhenClientIdNotConfigured_ShouldThrowInvalidOperationException()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["GitHub:ClientId"]).Returns((string?)null);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _authService.StartAuthenticationAsync());

            exception.Message.Should().Contain("GitHub client ID not configured");
        }

        [Fact]
        public async Task StartAuthenticationAsync_WhenValidConfiguration_ShouldReturnDeviceAuthorizationResult()
        {
            // Arrange
            var expectedDeviceCode = new DeviceCodeResponse
            {
                DeviceCode = "device_code_123",
                UserCode = "USER-CODE",
                VerificationUri = "https://github.com/login/device",
                ExpiresIn = 900,
                Interval = 5
            };

            _mockDeviceFlow
                .Setup(df => df.RequestDeviceCodeAsync(
                    "test_client_id",
                    "user:email,read:user",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedDeviceCode);

            // Act
            var result = await _authService.StartAuthenticationAsync();

            // Assert
            result.Should().NotBeNull();
            result.DeviceCode.Should().Be("device_code_123");
            result.UserCode.Should().Be("USER-CODE");
            result.VerificationUri.Should().Be("https://github.com/login/device");
            result.ExpiresIn.Should().Be(900);
            result.Interval.Should().Be(5);
        }

        [Fact]
        public async Task StartAuthenticationAsync_WhenCustomScopes_ShouldUseCustomScopes()
        {
            // Arrange
            var expectedDeviceCode = new DeviceCodeResponse
            {
                DeviceCode = "device_code_123",
                UserCode = "USER-CODE",
                VerificationUri = "https://github.com/login/device",
                ExpiresIn = 900,
                Interval = 5
            };

            _mockDeviceFlow
                .Setup(df => df.RequestDeviceCodeAsync(
                    "test_client_id",
                    "repo,user",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedDeviceCode);

            // Act
            var result = await _authService.StartAuthenticationAsync("repo,user");

            // Assert
            result.Should().NotBeNull();
            _mockDeviceFlow.Verify(df => df.RequestDeviceCodeAsync(
                "test_client_id",
                "repo,user",
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task StartAuthenticationAsync_WhenException_ShouldPropagateException()
        {
            // Arrange
            _mockDeviceFlow
                .Setup(df => df.RequestDeviceCodeAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Network error"));

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _authService.StartAuthenticationAsync());
        }

        [Fact]
        public async Task CompleteAuthenticationAsync_WhenDeviceCodeIsNull_ShouldThrowArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _authService.CompleteAuthenticationAsync(null!));
        }

        [Fact]
        public async Task CompleteAuthenticationAsync_WhenDeviceCodeIsEmpty_ShouldThrowArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _authService.CompleteAuthenticationAsync(""));
        }

        [Fact]
        public async Task CompleteAuthenticationAsync_WhenSuccessfulAuthentication_ShouldReturnSuccess()
        {
            // Arrange
            var tokenResponse = new AccessTokenResponse
            {
                AccessToken = "gho_token_123",
                TokenType = "bearer",
                Scope = "user:email,read:user"
            };

            _mockDeviceFlow
                .Setup(df => df.PollForAccessTokenAsync(
                    "test_client_id",
                    "device_code_123",
                    5,
                    300,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(tokenResponse);

            _mockDeviceFlow
                .Setup(df => df.ValidateAccessTokenAsync(
                    "gho_token_123",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Setup user info mock - we need to mock HttpClient for this
            // For simplicity, we'll assume the token validation includes user validation
            var mockConfig = new Mock<IConfigurationSection>();
            mockConfig.Setup(x => x.Get<string[]>()).Returns(new[] { "testuser" });
            _mockConfiguration.Setup(c => c.GetSection("GitHub:AllowedUsers")).Returns(mockConfig.Object);

            // Act
            var result = await _authService.CompleteAuthenticationAsync("device_code_123");

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.ErrorMessage.Should().BeNull();

            // Note: The user validation part would need more complex mocking
            // This is simplified for the test structure
        }

        [Fact]
        public async Task CompleteAuthenticationAsync_WhenTokenValidationFails_ShouldReturnFailure()
        {
            // Arrange
            var tokenResponse = new AccessTokenResponse
            {
                AccessToken = "invalid_token",
                TokenType = "bearer",
                Scope = "user:email"
            };

            _mockDeviceFlow
                .Setup(df => df.PollForAccessTokenAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(tokenResponse);

            _mockDeviceFlow
                .Setup(df => df.ValidateAccessTokenAsync(
                    "invalid_token",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await _authService.CompleteAuthenticationAsync("device_code_123");

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("获取的访问令牌无效");
        }

        [Fact]
        public async Task CompleteAuthenticationAsync_WhenAccessDenied_ShouldReturnFailure()
        {
            // Arrange
            var tokenResponse = new AccessTokenResponse
            {
                Error = "access_denied",
                ErrorDescription = "User denied the request"
            };

            _mockDeviceFlow
                .Setup(df => df.PollForAccessTokenAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(tokenResponse);

            // Act
            var result = await _authService.CompleteAuthenticationAsync("device_code_123");

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("用户拒绝了授权请求");
        }

        [Fact]
        public async Task CompleteAuthenticationAsync_WhenCancelled_ShouldThrowOperationCancelledException()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel();

            _mockDeviceFlow
                .Setup(df => df.PollForAccessTokenAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                _authService.CompleteAuthenticationAsync("device_code_123", cancellationToken: cts.Token));
        }

        [Fact]
        public async Task CompleteAuthenticationAsync_WhenTimeout_ShouldThrowTimeoutException()
        {
            // Arrange
            _mockDeviceFlow
                .Setup(df => df.PollForAccessTokenAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new TimeoutException("Polling timeout"));

            // Act & Assert
            await Assert.ThrowsAsync<TimeoutException>(() =>
                _authService.CompleteAuthenticationAsync("device_code_123"));
        }

        [Fact]
        public void SignOut_WhenCalled_ShouldClearAuthenticationState()
        {
            // Act
            _authService.SignOut();

            // Assert
            _authService.IsAuthenticated.Should().BeFalse();
            _authService.CurrentUserLogin.Should().BeNull();
            _authService.AuthorizedScopes.Should().BeNull();
        }

        [Fact]
        public void GetAccessToken_WhenNotAuthenticated_ShouldReturnNull()
        {
            // Act
            var token = _authService.GetAccessToken();

            // Assert
            token.Should().BeNull();
        }

        [Fact]
        public void AuthenticationStatusChanged_WhenStatusChanges_ShouldFireEvent()
        {
            // Arrange
            AuthenticationStatusChangedEventArgs? receivedEventArgs = null;
            _authService.AuthenticationStatusChanged += (sender, args) =>
            {
                receivedEventArgs = args;
            };

            // Act
            _authService.SignOut(); // This should trigger the event

            // Assert
            receivedEventArgs.Should().NotBeNull();
            receivedEventArgs!.Status.Should().Be(AuthenticationStatus.SignedOut);
            receivedEventArgs.Message.Should().Be("已成功登出");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task StartAuthenticationAsync_WhenClientIdMissing_ShouldThrowInvalidOperationException(string? clientId)
        {
            // Arrange
            _mockConfiguration.Setup(c => c["GitHub:ClientId"]).Returns(clientId);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _authService.StartAuthenticationAsync());

            exception.Message.Should().Contain("GitHub client ID not configured");
        }

        [Fact]
        public async Task StartAuthenticationAsync_WhenNoCustomScopesAndNoDefaultScopes_ShouldUseHardcodedDefault()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["GitHub:DefaultScopes"]).Returns((string?)null);

            var expectedDeviceCode = new DeviceCodeResponse
            {
                DeviceCode = "device_code_123",
                UserCode = "USER-CODE",
                VerificationUri = "https://github.com/login/device",
                ExpiresIn = 900,
                Interval = 5
            };

            _mockDeviceFlow
                .Setup(df => df.RequestDeviceCodeAsync(
                    "test_client_id",
                    "user:email",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedDeviceCode);

            // Act
            var result = await _authService.StartAuthenticationAsync();

            // Assert
            result.Should().NotBeNull();
            _mockDeviceFlow.Verify(df => df.RequestDeviceCodeAsync(
                "test_client_id",
                "user:email",
                It.IsAny<CancellationToken>()), Times.Once);
        }

        public void Dispose()
        {
            _authService?.Dispose();
        }
    }
}