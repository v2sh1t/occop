using System;
using System.Security;
using System.Threading.Tasks;
using Xunit;
using Occop.Models.Configuration;
using Occop.Services.Configuration;
using Occop.Services.Security;
using Occop.Services.Logging;

namespace Occop.Core.Tests.Services.Configuration
{
    /// <summary>
    /// ClaudeCodeConfigurator测试类
    /// </summary>
    public class ClaudeCodeConfiguratorTests : IDisposable
    {
        private readonly SecureStorage _secureStorage;
        private readonly ConfigurationLogger _logger;
        private readonly ClaudeCodeConfigurator _configurator;

        public ClaudeCodeConfiguratorTests()
        {
            _secureStorage = new SecureStorage();
            _logger = new ConfigurationLogger(enableFileLogging: false);
            _configurator = new ClaudeCodeConfigurator(_secureStorage, _logger);
        }

        [Fact]
        public async Task SetAuthTokenAsync_WithValidToken_ShouldSucceed()
        {
            // Arrange
            var validToken = CreateSecureString("sk-ant-api03-1234567890abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ");

            // Act
            var result = await _configurator.SetAuthTokenAsync(validToken);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(ConfigurationOperation.Set, result.Operation);
            Assert.Contains("successfully", result.Message);

            // Verify token is stored
            var storedToken = _secureStorage.GetSecureString(ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable);
            Assert.NotNull(storedToken);

            // Cleanup
            validToken.Dispose();
        }

        [Fact]
        public async Task SetAuthTokenAsync_WithInvalidToken_ShouldFail()
        {
            // Arrange
            var invalidToken = CreateSecureString("invalid-token");

            // Act
            var result = await _configurator.SetAuthTokenAsync(invalidToken);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ConfigurationOperation.Set, result.Operation);
            Assert.Contains("Invalid", result.Message);

            // Cleanup
            invalidToken.Dispose();
        }

        [Fact]
        public async Task SetAuthTokenAsync_WithNullToken_ShouldFail()
        {
            // Act
            var result = await _configurator.SetAuthTokenAsync(null!);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("cannot be null", result.Message);
        }

        [Fact]
        public async Task SetBaseUrlAsync_WithValidUrl_ShouldSucceed()
        {
            // Act
            var result = await _configurator.SetBaseUrlAsync("https://api.custom.com");

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(ConfigurationOperation.Set, result.Operation);
            Assert.Contains("successfully", result.Message);

            // Verify URL is stored
            var storedUrl = _secureStorage.GetString(ClaudeCodeConfigConstants.BaseUrlEnvironmentVariable);
            Assert.Equal("https://api.custom.com", storedUrl);
        }

        [Fact]
        public async Task SetBaseUrlAsync_WithNullUrl_ShouldUseDefault()
        {
            // Act
            var result = await _configurator.SetBaseUrlAsync(null);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Contains(ClaudeCodeConfigConstants.DefaultBaseUrl, result.Message);

            // Verify default URL is stored
            var storedUrl = _secureStorage.GetString(ClaudeCodeConfigConstants.BaseUrlEnvironmentVariable);
            Assert.Equal(ClaudeCodeConfigConstants.DefaultBaseUrl, storedUrl);
        }

        [Fact]
        public async Task SetBaseUrlAsync_WithInvalidUrl_ShouldFail()
        {
            // Act
            var result = await _configurator.SetBaseUrlAsync("not-a-valid-url");

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("Invalid base URL format", result.Message);
        }

        [Fact]
        public async Task ApplyConfigurationAsync_WithValidConfiguration_ShouldSucceed()
        {
            // Arrange
            var validToken = CreateSecureString("sk-ant-api03-1234567890abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ");
            await _configurator.SetAuthTokenAsync(validToken);
            await _configurator.SetBaseUrlAsync("https://api.custom.com");

            // Act
            var result = await _configurator.ApplyConfigurationAsync();

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(ConfigurationOperation.Apply, result.Operation);
            Assert.True(_configurator.IsConfigurationApplied);
            Assert.NotNull(_configurator.ConfigurationAppliedAt);

            // Verify environment variables are set
            var envToken = Environment.GetEnvironmentVariable(ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable);
            var envUrl = Environment.GetEnvironmentVariable(ClaudeCodeConfigConstants.BaseUrlEnvironmentVariable);
            Assert.NotNull(envToken);
            Assert.Equal("https://api.custom.com", envUrl);

            // Cleanup
            validToken.Dispose();
        }

        [Fact]
        public async Task ClearConfigurationAsync_ShouldClearAllConfiguration()
        {
            // Arrange
            var validToken = CreateSecureString("sk-ant-api03-1234567890abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ");
            await _configurator.SetAuthTokenAsync(validToken);
            await _configurator.SetBaseUrlAsync("https://api.custom.com");
            await _configurator.ApplyConfigurationAsync();

            // Act
            var result = await _configurator.ClearConfigurationAsync();

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(ConfigurationOperation.Clear, result.Operation);
            Assert.False(_configurator.IsConfigurationApplied);
            Assert.Null(_configurator.ConfigurationAppliedAt);

            // Verify environment variables are cleared
            var envToken = Environment.GetEnvironmentVariable(ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable);
            var envUrl = Environment.GetEnvironmentVariable(ClaudeCodeConfigConstants.BaseUrlEnvironmentVariable);
            Assert.Null(envToken);
            Assert.Null(envUrl);

            // Verify secure storage is cleared
            Assert.Equal(0, _secureStorage.Count);

            // Cleanup
            validToken.Dispose();
        }

        [Fact]
        public async Task RollbackConfigurationAsync_ShouldRestoreOriginalValues()
        {
            // Arrange - Set original environment value
            Environment.SetEnvironmentVariable(ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable, "original-value", EnvironmentVariableTarget.Process);

            var validToken = CreateSecureString("sk-ant-api03-1234567890abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ");
            await _configurator.SetAuthTokenAsync(validToken);
            await _configurator.ApplyConfigurationAsync();

            // Act
            var result = await _configurator.RollbackConfigurationAsync();

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(ConfigurationOperation.Rollback, result.Operation);
            Assert.False(_configurator.IsConfigurationApplied);

            // Verify original value is restored
            var envToken = Environment.GetEnvironmentVariable(ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable);
            Assert.Equal("original-value", envToken);

            // Cleanup
            validToken.Dispose();
            Environment.SetEnvironmentVariable(ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable, null, EnvironmentVariableTarget.Process);
        }

        [Fact]
        public void GetConfigurationStatus_ShouldReturnCurrentStatus()
        {
            // Act
            var status = _configurator.GetConfigurationStatus();

            // Assert
            Assert.NotNull(status);
            Assert.Contains("IsConfigurationApplied", status);
            Assert.Contains("ConfigurationAppliedAt", status);
            Assert.Contains("HasAuthToken", status);
            Assert.Contains("HasBaseUrl", status);
            Assert.Contains("SecureStorageItemsCount", status);
            Assert.Contains("Timestamp", status);

            Assert.False((bool)status["IsConfigurationApplied"]);
            Assert.Equal("N/A", status["ConfigurationAppliedAt"]);
            Assert.False((bool)status["HasAuthToken"]);
            Assert.False((bool)status["HasBaseUrl"]);
        }

        [Fact]
        public async Task TestClaudeCodeAsync_WithoutConfiguration_ShouldFail()
        {
            // Act
            var result = await _configurator.TestClaudeCodeAsync();

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(ConfigurationOperation.Validate, result.Operation);
            Assert.Contains("not set", result.Message);
        }

        [Fact]
        public async Task TestClaudeCodeAsync_WithConfiguration_ShouldAttemptTest()
        {
            // Arrange
            var validToken = CreateSecureString("sk-ant-api03-1234567890abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ");
            await _configurator.SetAuthTokenAsync(validToken);
            await _configurator.ApplyConfigurationAsync();

            // Act
            var result = await _configurator.TestClaudeCodeAsync();

            // Assert
            Assert.Equal(ConfigurationOperation.Validate, result.Operation);
            // Note: The actual result depends on whether Claude Code is installed
            // In test environment, it will likely fail with process start error

            // Cleanup
            validToken.Dispose();
        }

        [Fact]
        public async Task ConfigurationEvents_ShouldBeRaised()
        {
            // Arrange
            ConfigurationResult? appliedEventResult = null;
            ConfigurationResult? clearedEventResult = null;

            _configurator.ConfigurationApplied += (sender, result) => appliedEventResult = result;
            _configurator.ConfigurationCleared += (sender, result) => clearedEventResult = result;

            var validToken = CreateSecureString("sk-ant-api03-1234567890abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ");
            await _configurator.SetAuthTokenAsync(validToken);

            // Act
            await _configurator.ApplyConfigurationAsync();
            await _configurator.ClearConfigurationAsync();

            // Assert
            Assert.NotNull(appliedEventResult);
            Assert.True(appliedEventResult.IsSuccess);
            Assert.Equal(ConfigurationOperation.Apply, appliedEventResult.Operation);

            Assert.NotNull(clearedEventResult);
            Assert.True(clearedEventResult.IsSuccess);
            Assert.Equal(ConfigurationOperation.Clear, clearedEventResult.Operation);

            // Cleanup
            validToken.Dispose();
        }

        [Fact]
        public void Dispose_ShouldCleanupResources()
        {
            // Arrange
            var configurator = new ClaudeCodeConfigurator(_secureStorage, _logger);

            // Act & Assert - Should not throw
            configurator.Dispose();

            // Verify that operations after dispose throw ObjectDisposedException
            Assert.ThrowsAsync<ObjectDisposedException>(() => configurator.SetAuthTokenAsync(CreateSecureString("test")));
        }

        private static SecureString CreateSecureString(string value)
        {
            var secureString = new SecureString();
            foreach (char c in value)
            {
                secureString.AppendChar(c);
            }
            secureString.MakeReadOnly();
            return secureString;
        }

        public void Dispose()
        {
            // Clear any environment variables that might have been set during tests
            Environment.SetEnvironmentVariable(ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable, null, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable(ClaudeCodeConfigConstants.BaseUrlEnvironmentVariable, null, EnvironmentVariableTarget.Process);

            _configurator?.Dispose();
            _secureStorage?.Dispose();
            _logger?.Dispose();
        }
    }
}