using FluentAssertions;
using Occop.Services.Authentication.Models;
using System.Text.Json;
using Xunit;

namespace Occop.Tests.Services.Authentication
{
    /// <summary>
    /// Tests for DeviceCodeResponse model
    /// DeviceCodeResponse模型的测试
    /// </summary>
    public class DeviceCodeResponseTests
    {
        [Fact]
        public void DeviceCodeResponse_WhenAllFieldsValid_ShouldBeValid()
        {
            // Arrange
            var response = new DeviceCodeResponse
            {
                DeviceCode = "device_code_123",
                UserCode = "USER-CODE",
                VerificationUri = "https://github.com/login/device",
                VerificationUriComplete = "https://github.com/login/device?user_code=USER-CODE",
                ExpiresIn = 900,
                Interval = 5
            };

            // Act & Assert
            response.IsValid.Should().BeTrue();
            response.IsExpired.Should().BeFalse();
        }

        [Fact]
        public void DeviceCodeResponse_WhenRequiredFieldsMissing_ShouldBeInvalid()
        {
            // Arrange
            var response = new DeviceCodeResponse
            {
                DeviceCode = "",
                UserCode = "USER-CODE",
                VerificationUri = "https://github.com/login/device",
                ExpiresIn = 900,
                Interval = 5
            };

            // Act & Assert
            response.IsValid.Should().BeFalse();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void DeviceCodeResponse_WhenExpiresInInvalid_ShouldBeInvalid(int expiresIn)
        {
            // Arrange
            var response = new DeviceCodeResponse
            {
                DeviceCode = "device_code_123",
                UserCode = "USER-CODE",
                VerificationUri = "https://github.com/login/device",
                ExpiresIn = expiresIn,
                Interval = 5
            };

            // Act & Assert
            response.IsValid.Should().BeFalse();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void DeviceCodeResponse_WhenIntervalInvalid_ShouldBeInvalid(int interval)
        {
            // Arrange
            var response = new DeviceCodeResponse
            {
                DeviceCode = "device_code_123",
                UserCode = "USER-CODE",
                VerificationUri = "https://github.com/login/device",
                ExpiresIn = 900,
                Interval = interval
            };

            // Act & Assert
            response.IsValid.Should().BeFalse();
        }

        [Fact]
        public void ExpiresAt_ShouldCalculateCorrectExpirationTime()
        {
            // Arrange
            var beforeCreate = DateTime.UtcNow;
            var response = new DeviceCodeResponse
            {
                ExpiresIn = 900 // 15 minutes
            };
            var afterCreate = DateTime.UtcNow;

            // Act
            var expiresAt = response.ExpiresAt;

            // Assert
            expiresAt.Should().BeAfter(beforeCreate.AddSeconds(900 - 1));
            expiresAt.Should().BeBefore(afterCreate.AddSeconds(900 + 1));
        }

        [Fact]
        public void IsExpired_WhenNotExpired_ShouldReturnFalse()
        {
            // Arrange
            var response = new DeviceCodeResponse
            {
                ExpiresIn = 900 // 15 minutes in the future
            };

            // Act & Assert
            response.IsExpired.Should().BeFalse();
        }

        [Fact]
        public void JsonDeserialization_ShouldWorkCorrectly()
        {
            // Arrange
            var json = """
            {
                "device_code": "device_code_123",
                "user_code": "USER-CODE",
                "verification_uri": "https://github.com/login/device",
                "verification_uri_complete": "https://github.com/login/device?user_code=USER-CODE",
                "expires_in": 900,
                "interval": 5
            }
            """;

            // Act
            var response = JsonSerializer.Deserialize<DeviceCodeResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            // Assert
            response.Should().NotBeNull();
            response!.DeviceCode.Should().Be("device_code_123");
            response.UserCode.Should().Be("USER-CODE");
            response.VerificationUri.Should().Be("https://github.com/login/device");
            response.VerificationUriComplete.Should().Be("https://github.com/login/device?user_code=USER-CODE");
            response.ExpiresIn.Should().Be(900);
            response.Interval.Should().Be(5);
            response.IsValid.Should().BeTrue();
        }

        [Fact]
        public void JsonDeserialization_WhenOptionalFieldMissing_ShouldStillWork()
        {
            // Arrange
            var json = """
            {
                "device_code": "device_code_123",
                "user_code": "USER-CODE",
                "verification_uri": "https://github.com/login/device",
                "expires_in": 900,
                "interval": 5
            }
            """;

            // Act
            var response = JsonSerializer.Deserialize<DeviceCodeResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            // Assert
            response.Should().NotBeNull();
            response!.VerificationUriComplete.Should().BeNull();
            response.IsValid.Should().BeTrue();
        }
    }
}