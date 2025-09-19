using System;
using System.Linq;
using System.Security;
using Xunit;
using Occop.Models.Configuration;

namespace Occop.Core.Tests.Models.Configuration
{
    /// <summary>
    /// ClaudeCodeConfig测试类
    /// </summary>
    public class ClaudeCodeConfigTests
    {
        [Fact]
        public void GetConfigurationItems_ShouldReturnExpectedItems()
        {
            // Act
            var configItems = ClaudeCodeConfig.GetConfigurationItems();

            // Assert
            Assert.NotNull(configItems);
            Assert.Equal(2, configItems.Count);
            Assert.True(configItems.ContainsKey(ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable));
            Assert.True(configItems.ContainsKey(ClaudeCodeConfigConstants.BaseUrlEnvironmentVariable));
        }

        [Fact]
        public void GetConfigurationItem_WithValidKey_ShouldReturnItem()
        {
            // Act
            var authTokenItem = ClaudeCodeConfig.GetConfigurationItem(ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable);

            // Assert
            Assert.NotNull(authTokenItem);
            Assert.Equal(ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable, authTokenItem.Key);
            Assert.Equal(ConfigurationItemType.SecureString, authTokenItem.Type);
            Assert.True(authTokenItem.IsRequired);
            Assert.True(authTokenItem.IsSensitive);
            Assert.Equal(ConfigurationPriority.Critical, authTokenItem.Priority);
        }

        [Fact]
        public void GetConfigurationItem_WithInvalidKey_ShouldReturnNull()
        {
            // Act
            var result = ClaudeCodeConfig.GetConfigurationItem("INVALID_KEY");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetRequiredConfigurationKeys_ShouldReturnOnlyRequiredKeys()
        {
            // Act
            var requiredKeys = ClaudeCodeConfig.GetRequiredConfigurationKeys().ToList();

            // Assert
            Assert.NotNull(requiredKeys);
            Assert.Single(requiredKeys);
            Assert.Contains(ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable, requiredKeys);
        }

        [Fact]
        public void GetSensitiveConfigurationKeys_ShouldReturnOnlySensitiveKeys()
        {
            // Act
            var sensitiveKeys = ClaudeCodeConfig.GetSensitiveConfigurationKeys().ToList();

            // Assert
            Assert.NotNull(sensitiveKeys);
            Assert.Single(sensitiveKeys);
            Assert.Contains(ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable, sensitiveKeys);
        }

        [Fact]
        public void GetEnvironmentVariableMapping_ShouldReturnCorrectMapping()
        {
            // Act
            var mapping = ClaudeCodeConfig.GetEnvironmentVariableMapping();

            // Assert
            Assert.NotNull(mapping);
            Assert.Equal(2, mapping.Count);
            Assert.Equal(ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable,
                mapping[ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable]);
            Assert.Equal(ClaudeCodeConfigConstants.BaseUrlEnvironmentVariable,
                mapping[ClaudeCodeConfigConstants.BaseUrlEnvironmentVariable]);
        }

        [Theory]
        [InlineData("sk-ant-valid1234567890abcdef", true)]
        [InlineData("sk-ant-short", false)]
        [InlineData("invalid-token", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void ValidateConfigurationValue_AuthToken_ShouldValidateCorrectly(string? token, bool expectedResult)
        {
            // Act
            var result = ClaudeCodeConfig.ValidateConfigurationValue(
                ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable, token);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Theory]
        [InlineData("https://api.anthropic.com", true)]
        [InlineData("http://localhost:8080", true)]
        [InlineData("ftp://invalid.com", false)]
        [InlineData("not-a-url", false)]
        [InlineData("", false)]
        [InlineData(null, true)] // Base URL is optional
        public void ValidateConfigurationValue_BaseUrl_ShouldValidateCorrectly(string? url, bool expectedResult)
        {
            // Act
            var result = ClaudeCodeConfig.ValidateConfigurationValue(
                ClaudeCodeConfigConstants.BaseUrlEnvironmentVariable, url);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public void IsClaudeCodeEnvironmentVariable_WithValidVariables_ShouldReturnTrue()
        {
            // Act & Assert
            Assert.True(ClaudeCodeConfig.IsClaudeCodeEnvironmentVariable(ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable));
            Assert.True(ClaudeCodeConfig.IsClaudeCodeEnvironmentVariable(ClaudeCodeConfigConstants.BaseUrlEnvironmentVariable));
        }

        [Fact]
        public void IsClaudeCodeEnvironmentVariable_WithInvalidVariable_ShouldReturnFalse()
        {
            // Act & Assert
            Assert.False(ClaudeCodeConfig.IsClaudeCodeEnvironmentVariable("INVALID_VAR"));
            Assert.False(ClaudeCodeConfig.IsClaudeCodeEnvironmentVariable(""));
            Assert.False(ClaudeCodeConfig.IsClaudeCodeEnvironmentVariable(null));
        }

        [Fact]
        public void GetDefaultValues_ShouldReturnExpectedDefaults()
        {
            // Act
            var defaults = ClaudeCodeConfig.GetDefaultValues();

            // Assert
            Assert.NotNull(defaults);
            Assert.Equal(2, defaults.Count);
            Assert.Null(defaults[ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable]);
            Assert.Equal(ClaudeCodeConfigConstants.DefaultBaseUrl,
                defaults[ClaudeCodeConfigConstants.BaseUrlEnvironmentVariable]);
        }

        [Fact]
        public void GetConfigurationKeysByPriority_ShouldReturnKeysInCorrectOrder()
        {
            // Act
            var keys = ClaudeCodeConfig.GetConfigurationKeysByPriority().ToList();

            // Assert
            Assert.NotNull(keys);
            Assert.Equal(2, keys.Count);
            // Critical priority should come first
            Assert.Equal(ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable, keys[0]);
            Assert.Equal(ClaudeCodeConfigConstants.BaseUrlEnvironmentVariable, keys[1]);
        }

        [Fact]
        public void GetConfigurationSummary_ShouldReturnValidSummary()
        {
            // Act
            var summary = ClaudeCodeConfig.GetConfigurationSummary().ToList();

            // Assert
            Assert.NotNull(summary);
            Assert.Equal(2, summary.Count);

            var authTokenSummary = summary.First(s => s["Key"].ToString() == ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable);
            Assert.Equal("True", authTokenSummary["IsRequired"].ToString());
            Assert.Equal("True", authTokenSummary["IsSensitive"].ToString());
            Assert.Equal("Critical", authTokenSummary["Priority"].ToString());
        }
    }

    /// <summary>
    /// ClaudeCodeConfigValidator测试类
    /// </summary>
    public class ClaudeCodeConfigValidatorTests
    {
        [Theory]
        [InlineData("sk-ant-api03-1234567890abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ", true)]
        [InlineData("sk-ant-short", false)]
        [InlineData("invalid-prefix-1234567890abcdefghijklmnopqrstuvwxyz", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void ValidateAuthToken_WithStringInput_ShouldValidateCorrectly(string? token, bool expectedResult)
        {
            // Act
            var result = ClaudeCodeConfigValidator.ValidateAuthToken(token);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public void ValidateAuthToken_WithSecureString_ShouldValidateCorrectly()
        {
            // Arrange
            var validToken = "sk-ant-api03-1234567890abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
            var secureString = new SecureString();
            foreach (char c in validToken)
            {
                secureString.AppendChar(c);
            }
            secureString.MakeReadOnly();

            // Act
            var result = ClaudeCodeConfigValidator.ValidateAuthToken(secureString);

            // Assert
            Assert.True(result);

            // Cleanup
            secureString.Dispose();
        }

        [Fact]
        public void ValidateAuthToken_WithInvalidSecureString_ShouldReturnFalse()
        {
            // Arrange
            var invalidToken = "invalid-token";
            var secureString = new SecureString();
            foreach (char c in invalidToken)
            {
                secureString.AppendChar(c);
            }
            secureString.MakeReadOnly();

            // Act
            var result = ClaudeCodeConfigValidator.ValidateAuthToken(secureString);

            // Assert
            Assert.False(result);

            // Cleanup
            secureString.Dispose();
        }

        [Theory]
        [InlineData("https://api.anthropic.com", true)]
        [InlineData("http://localhost:8080", true)]
        [InlineData("https://custom-endpoint.example.com/api", true)]
        [InlineData("ftp://invalid.com", false)]
        [InlineData("not-a-url", false)]
        [InlineData("", false)]
        [InlineData(null, true)] // Base URL is optional
        public void ValidateBaseUrl_ShouldValidateCorrectly(string? url, bool expectedResult)
        {
            // Act
            var result = ClaudeCodeConfigValidator.ValidateBaseUrl(url);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public void ValidateAuthToken_WithInvalidObjectType_ShouldReturnFalse()
        {
            // Act
            var result = ClaudeCodeConfigValidator.ValidateAuthToken(123);

            // Assert
            Assert.False(result);
        }
    }

    /// <summary>
    /// ClaudeCodeConfigConstants测试类
    /// </summary>
    public class ClaudeCodeConfigConstantsTests
    {
        [Fact]
        public void Constants_ShouldHaveExpectedValues()
        {
            // Assert
            Assert.Equal("ANTHROPIC_AUTH_TOKEN", ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable);
            Assert.Equal("ANTHROPIC_BASE_URL", ClaudeCodeConfigConstants.BaseUrlEnvironmentVariable);
            Assert.Equal("https://api.anthropic.com", ClaudeCodeConfigConstants.DefaultBaseUrl);
            Assert.Equal("sk-ant-", ClaudeCodeConfigConstants.TokenPrefix);
            Assert.Equal(50, ClaudeCodeConfigConstants.MinTokenLength);
            Assert.Equal(200, ClaudeCodeConfigConstants.MaxTokenLength);
        }
    }
}