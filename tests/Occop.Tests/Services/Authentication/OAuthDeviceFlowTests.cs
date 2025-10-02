using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Occop.Services.Authentication;
using Occop.Services.Authentication.Models;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Occop.Tests.Services.Authentication
{
    /// <summary>
    /// Tests for OAuthDeviceFlow class
    /// OAuthDeviceFlow类的测试
    /// </summary>
    public class OAuthDeviceFlowTests
    {
        private readonly Mock<ILogger<OAuthDeviceFlow>> _mockLogger;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly OAuthDeviceFlow _deviceFlow;

        public OAuthDeviceFlowTests()
        {
            _mockLogger = new Mock<ILogger<OAuthDeviceFlow>>();
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
            _deviceFlow = new OAuthDeviceFlow(_httpClient, _mockLogger.Object);
        }

        [Fact]
        public void Constructor_WhenHttpClientIsNull_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new OAuthDeviceFlow(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WhenLoggerIsNull_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new OAuthDeviceFlow(_httpClient, null!));
        }

        [Fact]
        public async Task RequestDeviceCodeAsync_WhenClientIdIsNull_ShouldThrowArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _deviceFlow.RequestDeviceCodeAsync(null!));
        }

        [Fact]
        public async Task RequestDeviceCodeAsync_WhenClientIdIsEmpty_ShouldThrowArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _deviceFlow.RequestDeviceCodeAsync(""));
        }

        [Fact]
        public async Task RequestDeviceCodeAsync_WhenValidRequest_ShouldReturnDeviceCodeResponse()
        {
            // Arrange
            var expectedResponse = new DeviceCodeResponse
            {
                DeviceCode = "device_code_123",
                UserCode = "USER-CODE",
                VerificationUri = "https://github.com/login/device",
                ExpiresIn = 900,
                Interval = 5
            };

            var responseJson = JsonSerializer.Serialize(expectedResponse);
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Post &&
                        req.RequestUri!.ToString() == "https://github.com/login/device/code"),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            // Act
            var result = await _deviceFlow.RequestDeviceCodeAsync("test_client_id", "user:email");

            // Assert
            result.Should().NotBeNull();
            result.DeviceCode.Should().Be("device_code_123");
            result.UserCode.Should().Be("USER-CODE");
            result.VerificationUri.Should().Be("https://github.com/login/device");
            result.ExpiresIn.Should().Be(900);
            result.Interval.Should().Be(5);
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public async Task RequestDeviceCodeAsync_WhenHttpRequestFails_ShouldThrowInvalidOperationException()
        {
            // Arrange
            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Network error"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _deviceFlow.RequestDeviceCodeAsync("test_client_id"));

            exception.Message.Should().Contain("Failed to request device code due to network error");
        }

        [Fact]
        public async Task RequestDeviceCodeAsync_WhenInvalidResponse_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var invalidResponse = new DeviceCodeResponse
            {
                DeviceCode = "", // Invalid - empty device code
                UserCode = "USER-CODE",
                VerificationUri = "https://github.com/login/device",
                ExpiresIn = 900,
                Interval = 5
            };

            var responseJson = JsonSerializer.Serialize(invalidResponse);
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _deviceFlow.RequestDeviceCodeAsync("test_client_id"));

            exception.Message.Should().Contain("Received invalid device code response from GitHub");
        }

        [Fact]
        public async Task PollForAccessTokenAsync_WhenClientIdIsNull_ShouldThrowArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _deviceFlow.PollForAccessTokenAsync(null!, "device_code"));
        }

        [Fact]
        public async Task PollForAccessTokenAsync_WhenDeviceCodeIsNull_ShouldThrowArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _deviceFlow.PollForAccessTokenAsync("client_id", null!));
        }

        [Fact]
        public async Task PollForAccessTokenAsync_WhenSuccessResponse_ShouldReturnAccessToken()
        {
            // Arrange
            var successResponse = new AccessTokenResponse
            {
                AccessToken = "gho_16C7e42F292c6912E7710c838347Ae178B4a",
                TokenType = "bearer",
                Scope = "user:email,read:user"
            };

            var responseJson = JsonSerializer.Serialize(successResponse);
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Post &&
                        req.RequestUri!.ToString() == "https://github.com/login/oauth/access_token"),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            // Act
            var result = await _deviceFlow.PollForAccessTokenAsync("client_id", "device_code");

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.AccessToken.Should().Be("gho_16C7e42F292c6912E7710c838347Ae178B4a");
            result.TokenType.Should().Be("bearer");
            result.Scope.Should().Be("user:email,read:user");
        }

        [Fact]
        public async Task PollForAccessTokenAsync_WhenAuthorizationPending_ShouldRetryAndEventuallySucceed()
        {
            // Arrange
            var pendingResponse = new AccessTokenResponse
            {
                Error = "authorization_pending",
                ErrorDescription = "Authorization is pending"
            };

            var successResponse = new AccessTokenResponse
            {
                AccessToken = "gho_16C7e42F292c6912E7710c838347Ae178B4a",
                TokenType = "bearer",
                Scope = "user:email"
            };

            var pendingJson = JsonSerializer.Serialize(pendingResponse);
            var successJson = JsonSerializer.Serialize(successResponse);

            _mockHttpMessageHandler
                .Protected()
                .SetupSequence<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(pendingJson, Encoding.UTF8, "application/json")
                })
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(successJson, Encoding.UTF8, "application/json")
                });

            // Act
            var result = await _deviceFlow.PollForAccessTokenAsync(
                "client_id",
                "device_code",
                intervalSeconds: 1, // Short interval for test
                timeoutSeconds: 10);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.AccessToken.Should().Be("gho_16C7e42F292c6912E7710c838347Ae178B4a");
        }

        [Fact]
        public async Task PollForAccessTokenAsync_WhenSlowDown_ShouldIncreaseInterval()
        {
            // Arrange
            var slowDownResponse = new AccessTokenResponse
            {
                Error = "slow_down",
                ErrorDescription = "Polling too fast"
            };

            var successResponse = new AccessTokenResponse
            {
                AccessToken = "token",
                TokenType = "bearer"
            };

            var slowDownJson = JsonSerializer.Serialize(slowDownResponse);
            var successJson = JsonSerializer.Serialize(successResponse);

            _mockHttpMessageHandler
                .Protected()
                .SetupSequence<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(slowDownJson, Encoding.UTF8, "application/json")
                })
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(successJson, Encoding.UTF8, "application/json")
                });

            // Act
            var result = await _deviceFlow.PollForAccessTokenAsync(
                "client_id",
                "device_code",
                intervalSeconds: 1,
                timeoutSeconds: 15);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
        }

        [Fact]
        public async Task PollForAccessTokenAsync_WhenAccessDenied_ShouldReturnError()
        {
            // Arrange
            var deniedResponse = new AccessTokenResponse
            {
                Error = "access_denied",
                ErrorDescription = "User denied the request"
            };

            var responseJson = JsonSerializer.Serialize(deniedResponse);
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            // Act
            var result = await _deviceFlow.PollForAccessTokenAsync("client_id", "device_code");

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeFalse();
            result.IsAccessDenied.Should().BeTrue();
            result.Error.Should().Be("access_denied");
        }

        [Fact]
        public async Task PollForAccessTokenAsync_WhenExpiredToken_ShouldReturnError()
        {
            // Arrange
            var expiredResponse = new AccessTokenResponse
            {
                Error = "expired_token",
                ErrorDescription = "The device code has expired"
            };

            var responseJson = JsonSerializer.Serialize(expiredResponse);
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            // Act
            var result = await _deviceFlow.PollForAccessTokenAsync("client_id", "device_code");

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeFalse();
            result.IsExpiredToken.Should().BeTrue();
            result.Error.Should().Be("expired_token");
        }

        [Fact]
        public async Task PollForAccessTokenAsync_WhenCancelled_ShouldThrowOperationCancelledException()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                _deviceFlow.PollForAccessTokenAsync("client_id", "device_code", cancellationToken: cts.Token));
        }

        [Fact]
        public async Task PollForAccessTokenAsync_WhenTimeout_ShouldThrowTimeoutException()
        {
            // Arrange
            var pendingResponse = new AccessTokenResponse
            {
                Error = "authorization_pending"
            };

            var responseJson = JsonSerializer.Serialize(pendingResponse);
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            // Act & Assert
            await Assert.ThrowsAsync<TimeoutException>(() =>
                _deviceFlow.PollForAccessTokenAsync(
                    "client_id",
                    "device_code",
                    intervalSeconds: 1,
                    timeoutSeconds: 2)); // Very short timeout
        }

        [Fact]
        public async Task ValidateAccessTokenAsync_WhenValidToken_ShouldReturnTrue()
        {
            // Arrange
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"login\":\"testuser\"}", Encoding.UTF8, "application/json")
            };

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Get &&
                        req.RequestUri!.ToString() == "https://api.github.com/user" &&
                        req.Headers.Authorization!.Parameter == "test_token"),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            // Act
            var result = await _deviceFlow.ValidateAccessTokenAsync("test_token");

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task ValidateAccessTokenAsync_WhenInvalidToken_ShouldReturnFalse()
        {
            // Arrange
            var httpResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized);

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            // Act
            var result = await _deviceFlow.ValidateAccessTokenAsync("invalid_token");

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task ValidateAccessTokenAsync_WhenNullOrEmptyToken_ShouldReturnFalse()
        {
            // Act & Assert
            var resultNull = await _deviceFlow.ValidateAccessTokenAsync(null!);
            var resultEmpty = await _deviceFlow.ValidateAccessTokenAsync("");
            var resultWhitespace = await _deviceFlow.ValidateAccessTokenAsync("   ");

            resultNull.Should().BeFalse();
            resultEmpty.Should().BeFalse();
            resultWhitespace.Should().BeFalse();
        }

        [Fact]
        public async Task ValidateAccessTokenAsync_WhenExceptionThrown_ShouldReturnFalse()
        {
            // Arrange
            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Network error"));

            // Act
            var result = await _deviceFlow.ValidateAccessTokenAsync("test_token");

            // Assert
            result.Should().BeFalse();
        }
    }
}