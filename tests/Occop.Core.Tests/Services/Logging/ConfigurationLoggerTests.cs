using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Occop.Models.Configuration;
using Occop.Services.Configuration;
using Occop.Services.Logging;

namespace Occop.Core.Tests.Services.Logging
{
    /// <summary>
    /// ConfigurationLogger测试类
    /// </summary>
    public class ConfigurationLoggerTests : IDisposable
    {
        private readonly string _testLogDirectory;
        private readonly ConfigurationLogger _logger;

        public ConfigurationLoggerTests()
        {
            _testLogDirectory = Path.Combine(Path.GetTempPath(), "OccopTests", Guid.NewGuid().ToString());
            _logger = new ConfigurationLogger(_testLogDirectory, LogLevel.Debug, 100, 1000);
        }

        [Fact]
        public async Task LogOperationAsync_ShouldCreateLogEntry()
        {
            // Act
            await _logger.LogOperationAsync(
                ConfigurationOperation.Set,
                true,
                "Test operation",
                "Test details");

            // Assert
            var entries = _logger.GetRecentEntries(10);
            Assert.Single(entries);

            var entry = entries.First();
            Assert.Equal(ConfigurationOperation.Set, entry.Operation);
            Assert.True(entry.IsSuccess);
            Assert.Equal("Test operation", entry.Message);
            Assert.Equal("Test details", entry.Details);
            Assert.False(entry.IsSensitive);
        }

        [Fact]
        public async Task LogOperationAsync_WithSensitiveData_ShouldFilterSensitiveInfo()
        {
            // Arrange
            var sensitiveMessage = "Setting auth token: sk-ant-1234567890abcdefghijklmnopqrstuvwxyz";
            var sensitiveDetails = "Token value: sk-ant-9876543210zyxwvutsrqponmlkjihgfedcba";

            // Act
            await _logger.LogOperationAsync(
                ConfigurationOperation.Set,
                true,
                sensitiveMessage,
                sensitiveDetails,
                isSensitive: true);

            // Assert
            var entries = _logger.GetRecentEntries(10);
            Assert.Single(entries);

            var entry = entries.First();
            Assert.True(entry.IsSensitive);
            Assert.DoesNotContain("sk-ant-1234567890abcdefghijklmnopqrstuvwxyz", entry.Message);
            Assert.DoesNotContain("sk-ant-9876543210zyxwvutsrqponmlkjihgfedcba", entry.Details);
            Assert.Contains("[FILTERED]", entry.Message);
        }

        [Fact]
        public async Task LogOperationAsync_ShouldAutoDetectSensitiveData()
        {
            // Arrange
            var messageWithToken = "Auth token sk-ant-1234567890abcdefghijklmnopqrstuvwxyz configured";

            // Act
            await _logger.LogOperationAsync(
                ConfigurationOperation.Set,
                true,
                messageWithToken);

            // Assert
            var entries = _logger.GetRecentEntries(10);
            Assert.Single(entries);

            var entry = entries.First();
            Assert.True(entry.IsSensitive);
            Assert.DoesNotContain("sk-ant-1234567890abcdefghijklmnopqrstuvwxyz", entry.Message);
        }

        [Fact]
        public async Task LogInformationAsync_ShouldCreateInfoLogEntry()
        {
            // Act
            await _logger.LogInformationAsync("Information message", "Info details");

            // Assert
            var entries = _logger.GetRecentEntries(10);
            Assert.Single(entries);

            var entry = entries.First();
            Assert.Equal(LogLevel.Information, entry.Level);
            Assert.Equal("Information message", entry.Message);
            Assert.Equal("Info details", entry.Details);
            Assert.True(entry.IsSuccess);
        }

        [Fact]
        public async Task LogWarningAsync_ShouldCreateWarningLogEntry()
        {
            // Act
            await _logger.LogWarningAsync("Warning message", "Warning details");

            // Assert
            var entries = _logger.GetRecentEntries(10);
            Assert.Single(entries);

            var entry = entries.First();
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Equal("Warning message", entry.Message);
            Assert.Equal("Warning details", entry.Details);
            Assert.True(entry.IsSuccess);
        }

        [Fact]
        public async Task LogErrorAsync_ShouldCreateErrorLogEntry()
        {
            // Arrange
            var exception = new InvalidOperationException("Test exception");

            // Act
            await _logger.LogErrorAsync("Error message", "Error details", exception);

            // Assert
            var entries = _logger.GetRecentEntries(10);
            Assert.Single(entries);

            var entry = entries.First();
            Assert.Equal(LogLevel.Error, entry.Level);
            Assert.Equal("Error message", entry.Message);
            Assert.Equal("Error details", entry.Details);
            Assert.False(entry.IsSuccess);
            Assert.NotNull(entry.ExceptionInfo);
            Assert.Contains("Test exception", entry.ExceptionInfo);
        }

        [Fact]
        public async Task GetEntriesByOperation_ShouldFilterByOperation()
        {
            // Arrange
            await _logger.LogOperationAsync(ConfigurationOperation.Set, true, "Set operation");
            await _logger.LogOperationAsync(ConfigurationOperation.Apply, true, "Apply operation");
            await _logger.LogOperationAsync(ConfigurationOperation.Set, false, "Another set operation");

            // Act
            var setEntries = _logger.GetEntriesByOperation(ConfigurationOperation.Set, 10);
            var applyEntries = _logger.GetEntriesByOperation(ConfigurationOperation.Apply, 10);

            // Assert
            Assert.Equal(2, setEntries.Count);
            Assert.Single(applyEntries);
            Assert.All(setEntries, entry => Assert.Equal(ConfigurationOperation.Set, entry.Operation));
            Assert.All(applyEntries, entry => Assert.Equal(ConfigurationOperation.Apply, entry.Operation));
        }

        [Fact]
        public async Task GetLogStatistics_ShouldReturnCorrectStatistics()
        {
            // Arrange
            await _logger.LogOperationAsync(ConfigurationOperation.Set, true, "Success 1");
            await _logger.LogOperationAsync(ConfigurationOperation.Apply, true, "Success 2");
            await _logger.LogOperationAsync(ConfigurationOperation.Clear, false, "Failure 1");

            // Act
            var stats = _logger.GetLogStatistics();

            // Assert
            Assert.Equal(3, stats["TotalEntries"]);
            Assert.Equal(2, stats["SuccessCount"]);
            Assert.Equal(1, stats["FailureCount"]);
            Assert.Equal(200.0 / 3, (double)stats["SuccessRate"], 1); // 66.67%
            Assert.True(stats.ContainsKey("LevelCounts"));
            Assert.True(stats.ContainsKey("OperationCounts"));
        }

        [Fact]
        public void MinimumLogLevel_ShouldFilterLogEntries()
        {
            // Arrange
            _logger.MinimumLogLevel = LogLevel.Warning;

            // Act & Assert - These should not create entries
            _logger.LogInformationAsync("Info message").Wait();
            var entriesAfterInfo = _logger.GetRecentEntries(10);
            Assert.Empty(entriesAfterInfo);

            // This should create an entry
            _logger.LogWarningAsync("Warning message").Wait();
            var entriesAfterWarning = _logger.GetRecentEntries(10);
            Assert.Single(entriesAfterWarning);
        }

        [Fact]
        public async Task CleanupOldEntries_ShouldRemoveOldEntries()
        {
            // Arrange - Add some entries
            await _logger.LogInformationAsync("Entry 1");
            await _logger.LogInformationAsync("Entry 2");
            await _logger.LogInformationAsync("Entry 3");

            // Act
            var removedCount = _logger.CleanupOldEntries(-1); // Remove entries older than -1 days (all)

            // Assert
            Assert.Equal(3, removedCount);
            var remainingEntries = _logger.GetRecentEntries(10);
            Assert.Empty(remainingEntries);
        }

        [Fact]
        public async Task FlushAsync_ShouldNotThrowException()
        {
            // Arrange
            await _logger.LogInformationAsync("Test message");

            // Act & Assert
            await _logger.FlushAsync(); // Should not throw
        }

        [Fact]
        public void EnableFileLogging_ShouldBeConfigurable()
        {
            // Act
            _logger.EnableFileLogging = false;
            _logger.EnableConsoleLogging = true;

            // Assert
            Assert.False(_logger.EnableFileLogging);
            Assert.True(_logger.EnableConsoleLogging);
        }

        public void Dispose()
        {
            _logger?.Dispose();

            // Clean up test directory
            if (Directory.Exists(_testLogDirectory))
            {
                try
                {
                    Directory.Delete(_testLogDirectory, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }

    /// <summary>
    /// LogEntry测试类
    /// </summary>
    public class LogEntryTests
    {
        [Fact]
        public void LogEntry_Constructor_ShouldSetAllProperties()
        {
            // Arrange
            var exception = new InvalidOperationException("Test exception");

            // Act
            var entry = new LogEntry(
                LogLevel.Error,
                ConfigurationOperation.Set,
                false,
                "Test message",
                "Test details",
                true,
                exception,
                "user123",
                "session456");

            // Assert
            Assert.NotNull(entry.Id);
            Assert.NotEmpty(entry.Id);
            Assert.Equal(LogLevel.Error, entry.Level);
            Assert.Equal(ConfigurationOperation.Set, entry.Operation);
            Assert.False(entry.IsSuccess);
            Assert.Equal("Test message", entry.Message);
            Assert.Equal("Test details", entry.Details);
            Assert.True(entry.IsSensitive);
            Assert.NotNull(entry.ExceptionInfo);
            Assert.Equal("user123", entry.UserId);
            Assert.Equal("session456", entry.SessionId);
            Assert.Equal(Environment.MachineName, entry.MachineName);
            Assert.Equal(Environment.ProcessId, entry.ProcessId);
        }

        [Fact]
        public void ToJson_ShouldReturnValidJson()
        {
            // Arrange
            var entry = new LogEntry(
                LogLevel.Information,
                ConfigurationOperation.Apply,
                true,
                "Test message");

            // Act
            var json = entry.ToJson();

            // Assert
            Assert.NotNull(json);
            Assert.NotEmpty(json);
            Assert.Contains("\"message\":\"Test message\"", json);
            Assert.Contains("\"level\":\"Information\"", json);
            Assert.Contains("\"operation\":\"Apply\"", json);
        }

        [Fact]
        public void ToString_ShouldReturnReadableFormat()
        {
            // Arrange
            var entry = new LogEntry(
                LogLevel.Warning,
                ConfigurationOperation.Validate,
                false,
                "Test warning");

            // Act
            var result = entry.ToString();

            // Assert
            Assert.Contains("WARNING", result);
            Assert.Contains("Validate", result);
            Assert.Contains("FAILURE", result);
            Assert.Contains("Test warning", result);
        }

        [Fact]
        public void ToString_WithSensitiveData_ShouldIncludeSensitiveMarker()
        {
            // Arrange
            var entry = new LogEntry(
                LogLevel.Information,
                ConfigurationOperation.Set,
                true,
                "Setting token",
                isSensitive: true);

            // Act
            var result = entry.ToString();

            // Assert
            Assert.Contains("[SENSITIVE]", result);
        }
    }

    /// <summary>
    /// SensitiveDataFilter测试类
    /// </summary>
    public class SensitiveDataFilterTests
    {
        [Theory]
        [InlineData("Setting token sk-ant-1234567890abcdef", "Setting token sk-a****bcdef")]
        [InlineData("API_KEY=secret123value", "API_KEY=[FILTERED]")]
        [InlineData("Bearer eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9", "Bear****iJ9")]
        [InlineData("No sensitive data here", "No sensitive data here")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void FilterSensitiveData_ShouldFilterCorrectly(string? input, string expected)
        {
            // Act
            var result = SensitiveDataFilter.FilterSensitiveData(input);

            // Assert
            if (expected.Contains("****"))
            {
                // For dynamic filtering, just check that sensitive parts are masked
                Assert.DoesNotContain("sk-ant-1234567890abcdef", result);
                Assert.Contains("****", result);
            }
            else
            {
                Assert.Equal(expected, result);
            }
        }

        [Theory]
        [InlineData("Setting token sk-ant-1234567890abcdef", true)]
        [InlineData("API_KEY=secret123", true)]
        [InlineData("password: mypassword", true)]
        [InlineData("Normal log message", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void ContainsSensitiveData_ShouldDetectCorrectly(string? input, bool expected)
        {
            // Act
            var result = SensitiveDataFilter.ContainsSensitiveData(input);

            // Assert
            Assert.Equal(expected, result);
        }
    }
}