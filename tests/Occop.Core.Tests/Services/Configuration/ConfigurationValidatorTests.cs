using System;
using System.Linq;
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
    /// ConfigurationValidator测试类
    /// </summary>
    public class ConfigurationValidatorTests : IDisposable
    {
        private readonly SecureStorage _secureStorage;
        private readonly ConfigurationLogger _logger;
        private readonly ConfigurationValidator _validator;

        public ConfigurationValidatorTests()
        {
            _secureStorage = new SecureStorage();
            _logger = new ConfigurationLogger(enableFileLogging: false);
            _validator = new ConfigurationValidator(_secureStorage, _logger);
        }

        [Fact]
        public async Task ValidateStoredConfigurationAsync_WithValidConfiguration_ShouldReturnHealthy()
        {
            // Arrange
            var validToken = CreateSecureString("sk-ant-api03-1234567890abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ");
            _secureStorage.Store(ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable, validToken);
            _secureStorage.Store(ClaudeCodeConfigConstants.BaseUrlEnvironmentVariable, "https://api.anthropic.com");

            // Act
            var result = await _validator.ValidateStoredConfigurationAsync();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsHealthy);
            Assert.True(result.HealthScore >= 70);
            Assert.NotNull(_validator.LastValidationTime);

            var successfulResults = result.CheckResults.Where(r => r.IsSuccess).ToList();
            Assert.NotEmpty(successfulResults);

            // Cleanup
            validToken.Dispose();
        }

        [Fact]
        public async Task ValidateStoredConfigurationAsync_WithMissingAuthToken_ShouldReturnUnhealthy()
        {
            // Arrange - Only set base URL, missing required auth token

            // Act
            var result = await _validator.ValidateStoredConfigurationAsync();

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsHealthy);
            Assert.True(result.HealthScore < 50);

            var errorResults = result.CheckResults.Where(r => r.ResultType == ValidationResultType.Error).ToList();
            Assert.NotEmpty(errorResults);

            var authTokenError = errorResults.FirstOrDefault(r => r.ConfigurationKey == ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable);
            Assert.NotNull(authTokenError);
            Assert.Contains("not stored", authTokenError.Message);
        }

        [Fact]
        public async Task ValidateStoredConfigurationAsync_WithInvalidAuthToken_ShouldReturnUnhealthy()
        {
            // Arrange
            var invalidToken = CreateSecureString("invalid-token-format");
            _secureStorage.Store(ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable, invalidToken);

            // Act
            var result = await _validator.ValidateStoredConfigurationAsync();

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsHealthy);

            var errorResults = result.CheckResults.Where(r => r.ResultType == ValidationResultType.Error).ToList();
            var authTokenError = errorResults.FirstOrDefault(r => r.ConfigurationKey == ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable);
            Assert.NotNull(authTokenError);
            Assert.Contains("Invalid", authTokenError.Message);

            // Cleanup
            invalidToken.Dispose();
        }

        [Fact]
        public async Task ValidateAppliedConfigurationAsync_WithEnvironmentVariablesSet_ShouldValidate()
        {
            // Arrange
            Environment.SetEnvironmentVariable(ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable,
                "sk-ant-api03-1234567890abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ",
                EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable(ClaudeCodeConfigConstants.BaseUrlEnvironmentVariable,
                "https://api.anthropic.com",
                EnvironmentVariableTarget.Process);

            // Act
            var result = await _validator.ValidateAppliedConfigurationAsync();

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(_validator.LastValidationTime);

            var authTokenResult = result.CheckResults.FirstOrDefault(r => r.ConfigurationKey == ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable);
            Assert.NotNull(authTokenResult);

            var baseUrlResult = result.CheckResults.FirstOrDefault(r => r.ConfigurationKey == ClaudeCodeConfigConstants.BaseUrlEnvironmentVariable);
            Assert.NotNull(baseUrlResult);

            // Claude Code executable test will likely fail in test environment, but that's expected

            // Cleanup
            Environment.SetEnvironmentVariable(ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable, null, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable(ClaudeCodeConfigConstants.BaseUrlEnvironmentVariable, null, EnvironmentVariableTarget.Process);
        }

        [Fact]
        public async Task ValidateAppliedConfigurationAsync_WithMissingEnvironmentVariables_ShouldDetectErrors()
        {
            // Arrange - Ensure environment variables are not set
            Environment.SetEnvironmentVariable(ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable, null, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable(ClaudeCodeConfigConstants.BaseUrlEnvironmentVariable, null, EnvironmentVariableTarget.Process);

            // Act
            var result = await _validator.ValidateAppliedConfigurationAsync();

            // Assert
            Assert.NotNull(result);

            var authTokenResult = result.CheckResults.FirstOrDefault(r => r.ConfigurationKey == ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable);
            Assert.NotNull(authTokenResult);
            Assert.Equal(ValidationResultType.Error, authTokenResult.ResultType);
            Assert.Contains("not set", authTokenResult.Message);

            var baseUrlResult = result.CheckResults.FirstOrDefault(r => r.ConfigurationKey == ClaudeCodeConfigConstants.BaseUrlEnvironmentVariable);
            Assert.NotNull(baseUrlResult);
            Assert.Equal(ValidationResultType.Warning, baseUrlResult.ResultType); // Base URL is optional
        }

        [Fact]
        public async Task PerformHealthCheckAsync_ShouldCombineAllValidations()
        {
            // Arrange
            var validToken = CreateSecureString("sk-ant-api03-1234567890abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ");
            _secureStorage.Store(ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable, validToken);

            Environment.SetEnvironmentVariable(ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable,
                "sk-ant-api03-1234567890abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ",
                EnvironmentVariableTarget.Process);

            // Act
            var result = await _validator.PerformHealthCheckAsync();

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(_validator.LastHealthCheckTime);

            // Should contain results from stored validation, applied validation, API connectivity, and system resources
            Assert.True(result.CheckResults.Count >= 4);

            var systemResourcesResult = result.CheckResults.FirstOrDefault(r => r.ConfigurationKey == "SYSTEM_RESOURCES");
            Assert.NotNull(systemResourcesResult);

            // Cleanup
            validToken.Dispose();
            Environment.SetEnvironmentVariable(ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable, null, EnvironmentVariableTarget.Process);
        }

        [Fact]
        public async Task ValidationCompleted_EventShouldBeRaised()
        {
            // Arrange
            HealthCheckResult? eventResult = null;
            _validator.ValidationCompleted += (sender, result) => eventResult = result;

            var validToken = CreateSecureString("sk-ant-api03-1234567890abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ");
            _secureStorage.Store(ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable, validToken);

            // Act
            var result = await _validator.ValidateStoredConfigurationAsync();

            // Assert
            Assert.NotNull(eventResult);
            Assert.Equal(result.HealthScore, eventResult.HealthScore);
            Assert.Equal(result.IsHealthy, eventResult.IsHealthy);

            // Cleanup
            validToken.Dispose();
        }

        [Fact]
        public void Dispose_ShouldCleanupResources()
        {
            // Arrange
            var validator = new ConfigurationValidator(_secureStorage, _logger);

            // Act & Assert - Should not throw
            validator.Dispose();

            // Verify that operations after dispose throw ObjectDisposedException
            Assert.ThrowsAsync<ObjectDisposedException>(() => validator.ValidateStoredConfigurationAsync());
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
            // Clean up environment variables
            Environment.SetEnvironmentVariable(ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable, null, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable(ClaudeCodeConfigConstants.BaseUrlEnvironmentVariable, null, EnvironmentVariableTarget.Process);

            _validator?.Dispose();
            _secureStorage?.Dispose();
            _logger?.Dispose();
        }
    }

    /// <summary>
    /// ConfigurationValidationResult测试类
    /// </summary>
    public class ConfigurationValidationResultTests
    {
        [Fact]
        public void ConfigurationValidationResult_Constructor_ShouldSetAllProperties()
        {
            // Arrange
            var exception = new InvalidOperationException("Test exception");

            // Act
            var result = new ConfigurationValidationResult(
                ValidationResultType.Error,
                "TEST_KEY",
                "Test message",
                "Test details",
                "Test action",
                exception);

            // Assert
            Assert.Equal(ValidationResultType.Error, result.ResultType);
            Assert.Equal("TEST_KEY", result.ConfigurationKey);
            Assert.Equal("Test message", result.Message);
            Assert.Equal("Test details", result.Details);
            Assert.Equal("Test action", result.RecommendedAction);
            Assert.Equal(exception, result.Exception);
            Assert.False(result.IsSuccess);
            Assert.True(result.IsCritical);
        }

        [Fact]
        public void IsSuccess_ShouldReturnTrueForSuccessType()
        {
            // Arrange
            var result = new ConfigurationValidationResult(
                ValidationResultType.Success,
                "TEST_KEY",
                "Success message");

            // Assert
            Assert.True(result.IsSuccess);
            Assert.False(result.IsCritical);
        }

        [Fact]
        public void IsCritical_ShouldReturnTrueForErrorAndFatal()
        {
            // Arrange
            var errorResult = new ConfigurationValidationResult(
                ValidationResultType.Error,
                "TEST_KEY",
                "Error message");

            var fatalResult = new ConfigurationValidationResult(
                ValidationResultType.Fatal,
                "TEST_KEY",
                "Fatal message");

            var warningResult = new ConfigurationValidationResult(
                ValidationResultType.Warning,
                "TEST_KEY",
                "Warning message");

            // Assert
            Assert.True(errorResult.IsCritical);
            Assert.True(fatalResult.IsCritical);
            Assert.False(warningResult.IsCritical);
        }
    }

    /// <summary>
    /// HealthCheckResult测试类
    /// </summary>
    public class HealthCheckResultTests
    {
        [Fact]
        public void HealthCheckResult_WithAllSuccessResults_ShouldBeHealthy()
        {
            // Arrange
            var results = new[]
            {
                new ConfigurationValidationResult(ValidationResultType.Success, "KEY1", "Success 1"),
                new ConfigurationValidationResult(ValidationResultType.Success, "KEY2", "Success 2"),
                new ConfigurationValidationResult(ValidationResultType.Warning, "KEY3", "Warning 1")
            };

            // Act
            var healthResult = new HealthCheckResult(results);

            // Assert
            Assert.True(healthResult.IsHealthy);
            Assert.True(healthResult.HealthScore > 70); // Should be high with mostly success
            Assert.Equal(3, healthResult.CheckResults.Count);
            Assert.Contains("2 success", healthResult.Summary);
            Assert.Contains("1 warnings", healthResult.Summary);
        }

        [Fact]
        public void HealthCheckResult_WithErrorResults_ShouldBeUnhealthy()
        {
            // Arrange
            var results = new[]
            {
                new ConfigurationValidationResult(ValidationResultType.Success, "KEY1", "Success 1"),
                new ConfigurationValidationResult(ValidationResultType.Error, "KEY2", "Error 1"),
                new ConfigurationValidationResult(ValidationResultType.Fatal, "KEY3", "Fatal 1")
            };

            // Act
            var healthResult = new HealthCheckResult(results);

            // Assert
            Assert.False(healthResult.IsHealthy);
            Assert.Equal(3, healthResult.CheckResults.Count);
            Assert.Contains("1 success", healthResult.Summary);
            Assert.Contains("1 errors", healthResult.Summary);
            Assert.Contains("1 fatal", healthResult.Summary);
        }

        [Fact]
        public void HealthCheckResult_WithEmptyResults_ShouldHaveZeroScore()
        {
            // Arrange
            var results = Array.Empty<ConfigurationValidationResult>();

            // Act
            var healthResult = new HealthCheckResult(results);

            // Assert
            Assert.True(healthResult.IsHealthy); // No errors means healthy
            Assert.Equal(0, healthResult.HealthScore);
            Assert.Empty(healthResult.CheckResults);
        }

        [Fact]
        public void HealthCheckResult_Constructor_ShouldThrowOnNullResults()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new HealthCheckResult(null!));
        }
    }
}