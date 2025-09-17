using FluentAssertions;
using Occop.Services.Authentication.Models;
using System.Text.Json;
using Xunit;

namespace Occop.Tests.Services.Authentication
{
    /// <summary>
    /// Tests for AccessTokenResponse model
    /// AccessTokenResponse模型的测试
    /// </summary>
    public class AccessTokenResponseTests
    {
        [Fact]
        public void AccessTokenResponse_WhenSuccess_ShouldBeValid()
        {
            // Arrange
            var response = new AccessTokenResponse
            {
                AccessToken = "gho_16C7e42F292c6912E7710c838347Ae178B4a",
                TokenType = "bearer",
                Scope = "user:email,read:user"
            };

            // Act & Assert
            response.IsSuccess.Should().BeTrue();
            response.IsValid.Should().BeTrue();
            response.IsAuthorizationPending.Should().BeFalse();
            response.IsSlowDown.Should().BeFalse();
            response.IsExpiredToken.Should().BeFalse();
            response.IsAccessDenied.Should().BeFalse();
        }

        [Fact]
        public void AccessTokenResponse_WhenAuthorizationPending_ShouldIndicateCorrectStatus()
        {
            // Arrange
            var response = new AccessTokenResponse
            {
                Error = "authorization_pending",
                ErrorDescription = "The authorization request is still pending"
            };

            // Act & Assert
            response.IsSuccess.Should().BeFalse();
            response.IsAuthorizationPending.Should().BeTrue();
            response.IsSlowDown.Should().BeFalse();
            response.IsExpiredToken.Should().BeFalse();
            response.IsAccessDenied.Should().BeFalse();
        }

        [Fact]
        public void AccessTokenResponse_WhenSlowDown_ShouldIndicateCorrectStatus()
        {
            // Arrange
            var response = new AccessTokenResponse
            {
                Error = "slow_down",
                ErrorDescription = "The polling rate is too high"
            };

            // Act & Assert
            response.IsSuccess.Should().BeFalse();
            response.IsAuthorizationPending.Should().BeFalse();
            response.IsSlowDown.Should().BeTrue();
            response.IsExpiredToken.Should().BeFalse();
            response.IsAccessDenied.Should().BeFalse();
        }

        [Fact]
        public void AccessTokenResponse_WhenExpiredToken_ShouldIndicateCorrectStatus()
        {
            // Arrange
            var response = new AccessTokenResponse
            {
                Error = "expired_token",
                ErrorDescription = "The device code has expired"
            };

            // Act & Assert
            response.IsSuccess.Should().BeFalse();
            response.IsAuthorizationPending.Should().BeFalse();
            response.IsSlowDown.Should().BeFalse();
            response.IsExpiredToken.Should().BeTrue();
            response.IsAccessDenied.Should().BeFalse();
        }

        [Fact]
        public void AccessTokenResponse_WhenAccessDenied_ShouldIndicateCorrectStatus()
        {
            // Arrange
            var response = new AccessTokenResponse
            {
                Error = "access_denied",
                ErrorDescription = "The user denied the authorization request"
            };

            // Act & Assert
            response.IsSuccess.Should().BeFalse();
            response.IsAuthorizationPending.Should().BeFalse();
            response.IsSlowDown.Should().BeFalse();
            response.IsExpiredToken.Should().BeFalse();
            response.IsAccessDenied.Should().BeTrue();
        }

        [Theory]
        [InlineData("authorization_pending", "等待用户授权中...")]
        [InlineData("slow_down", "轮询频率过高，请降低频率")]
        [InlineData("expired_token", "设备码已过期，请重新启动授权流程")]
        [InlineData("unsupported_grant_type", "设备码无效或格式错误")]
        [InlineData("access_denied", "用户拒绝了授权请求")]
        public void GetUserFriendlyErrorMessage_ShouldReturnCorrectMessage(string error, string expectedMessage)
        {
            // Arrange
            var response = new AccessTokenResponse
            {
                Error = error
            };

            // Act
            var message = response.GetUserFriendlyErrorMessage();

            // Assert
            message.Should().Be(expectedMessage);
        }

        [Fact]
        public void GetUserFriendlyErrorMessage_WhenSuccess_ShouldReturnEmpty()
        {
            // Arrange
            var response = new AccessTokenResponse
            {
                AccessToken = "token",
                TokenType = "bearer"
            };

            // Act
            var message = response.GetUserFriendlyErrorMessage();

            // Assert
            message.Should().BeEmpty();
        }

        [Fact]
        public void GetUserFriendlyErrorMessage_WhenUnknownError_ShouldReturnErrorDescription()
        {
            // Arrange
            var response = new AccessTokenResponse
            {
                Error = "unknown_error",
                ErrorDescription = "Custom error description"
            };

            // Act
            var message = response.GetUserFriendlyErrorMessage();

            // Assert
            message.Should().Be("Custom error description");
        }

        [Fact]
        public void GetScopes_WhenScopeEmpty_ShouldReturnEmptyArray()
        {
            // Arrange
            var response = new AccessTokenResponse
            {
                Scope = ""
            };

            // Act
            var scopes = response.GetScopes();

            // Assert
            scopes.Should().BeEmpty();
        }

        [Fact]
        public void GetScopes_WhenScopeNull_ShouldReturnEmptyArray()
        {
            // Arrange
            var response = new AccessTokenResponse
            {
                Scope = null!
            };

            // Act
            var scopes = response.GetScopes();

            // Assert
            scopes.Should().BeEmpty();
        }

        [Fact]
        public void GetScopes_WhenMultipleScopes_ShouldReturnCorrectArray()
        {
            // Arrange
            var response = new AccessTokenResponse
            {
                Scope = "user:email,read:user,repo"
            };

            // Act
            var scopes = response.GetScopes();

            // Assert
            scopes.Should().BeEquivalentTo(new[] { "user:email", "read:user", "repo" });
        }

        [Fact]
        public void GetScopes_WhenScopesWithSpaces_ShouldTrimSpaces()
        {
            // Arrange
            var response = new AccessTokenResponse
            {
                Scope = " user:email , read:user , repo "
            };

            // Act
            var scopes = response.GetScopes();

            // Assert
            scopes.Should().BeEquivalentTo(new[] { "user:email", "read:user", "repo" });
        }

        [Fact]
        public void JsonDeserialization_SuccessResponse_ShouldWorkCorrectly()
        {
            // Arrange
            var json = """
            {
                "access_token": "gho_16C7e42F292c6912E7710c838347Ae178B4a",
                "token_type": "bearer",
                "scope": "user:email,read:user"
            }
            """;

            // Act
            var response = JsonSerializer.Deserialize<AccessTokenResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            // Assert
            response.Should().NotBeNull();
            response!.AccessToken.Should().Be("gho_16C7e42F292c6912E7710c838347Ae178B4a");
            response.TokenType.Should().Be("bearer");
            response.Scope.Should().Be("user:email,read:user");
            response.IsSuccess.Should().BeTrue();
        }

        [Fact]
        public void JsonDeserialization_ErrorResponse_ShouldWorkCorrectly()
        {
            // Arrange
            var json = """
            {
                "error": "authorization_pending",
                "error_description": "The authorization request is still pending",
                "error_uri": "https://docs.github.com/apps/building-oauth-apps/authorizing-oauth-apps/#error-codes-for-the-device-flow"
            }
            """;

            // Act
            var response = JsonSerializer.Deserialize<AccessTokenResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            // Assert
            response.Should().NotBeNull();
            response!.Error.Should().Be("authorization_pending");
            response.ErrorDescription.Should().Be("The authorization request is still pending");
            response.ErrorUri.Should().Be("https://docs.github.com/apps/building-oauth-apps/authorizing-oauth-apps/#error-codes-for-the-device-flow");
            response.IsSuccess.Should().BeFalse();
            response.IsAuthorizationPending.Should().BeTrue();
        }

        [Theory]
        [InlineData("AUTHORIZATION_PENDING")]
        [InlineData("Authorization_Pending")]
        [InlineData("authorization_pending")]
        public void ErrorStatusChecks_ShouldBeCaseInsensitive(string errorValue)
        {
            // Arrange
            var response = new AccessTokenResponse
            {
                Error = errorValue
            };

            // Act & Assert
            response.IsAuthorizationPending.Should().BeTrue();
        }
    }
}