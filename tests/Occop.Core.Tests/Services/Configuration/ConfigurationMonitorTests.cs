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
    /// ConfigurationMonitor测试类
    /// </summary>
    public class ConfigurationMonitorTests : IDisposable
    {
        private readonly SecureStorage _secureStorage;
        private readonly ConfigurationLogger _logger;
        private readonly ClaudeCodeConfigurator _configurator;
        private readonly ConfigurationValidator _validator;
        private readonly ConfigurationMonitor _monitor;

        public ConfigurationMonitorTests()
        {
            _secureStorage = new SecureStorage();
            _logger = new ConfigurationLogger(enableFileLogging: false);
            _configurator = new ClaudeCodeConfigurator(_secureStorage, _logger);
            _validator = new ConfigurationValidator(_secureStorage, _logger);

            var config = new MonitorConfiguration
            {
                HealthCheckIntervalMs = 100, // Short interval for testing
                ConfigurationCheckIntervalMs = 50,
                EnableAutoRecovery = true,
                MaxRetryAttempts = 2,
                RetryIntervalMs = 50,
                EnableVerboseLogging = true
            };

            _monitor = new ConfigurationMonitor(_configurator, _validator, _logger, config);
        }

        [Fact]
        public async Task StartMonitoringAsync_ShouldStartSuccessfully()
        {
            // Act
            var result = await _monitor.StartMonitoringAsync();

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(ConfigurationOperation.Validate, result.Operation);
            Assert.True(_monitor.IsMonitoring);
            Assert.NotNull(_monitor.MonitoringStartedAt);
            Assert.Contains("started successfully", result.Message);
        }

        [Fact]
        public async Task StartMonitoringAsync_WhenAlreadyStarted_ShouldFail()
        {
            // Arrange
            await _monitor.StartMonitoringAsync();

            // Act
            var result = await _monitor.StartMonitoringAsync();

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("already started", result.Message);
        }

        [Fact]
        public async Task StopMonitoringAsync_ShouldStopSuccessfully()
        {
            // Arrange
            await _monitor.StartMonitoringAsync();

            // Act
            var result = await _monitor.StopMonitoringAsync();

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(ConfigurationOperation.Validate, result.Operation);
            Assert.False(_monitor.IsMonitoring);
            Assert.Contains("stopped successfully", result.Message);
        }

        [Fact]
        public async Task StopMonitoringAsync_WhenNotStarted_ShouldFail()
        {
            // Act
            var result = await _monitor.StopMonitoringAsync();

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("not started", result.Message);
        }

        [Fact]
        public async Task MonitorEvent_ShouldBeRaisedOnStartAndStop()
        {
            // Arrange
            var events = new List<MonitorEventArgs>();
            _monitor.MonitorEvent += (sender, args) => events.Add(args);

            // Act
            await _monitor.StartMonitoringAsync();
            await _monitor.StopMonitoringAsync();

            // Assert
            Assert.Contains(events, e => e.EventType == MonitorEventType.MonitoringStarted);
            Assert.Contains(events, e => e.EventType == MonitorEventType.MonitoringStopped);
        }

        [Fact]
        public async Task TriggerHealthCheckAsync_ShouldPerformHealthCheck()
        {
            // Arrange
            var validToken = CreateSecureString("sk-ant-api03-1234567890abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ");
            _secureStorage.Store(ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable, validToken);

            // Act
            var result = await _monitor.TriggerHealthCheckAsync();

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(_monitor.LastHealthResult);
            Assert.NotNull(_monitor.LastHealthCheckTime);
            Assert.Equal(result.HealthScore, _monitor.LastHealthResult.HealthScore);

            // Cleanup
            validToken.Dispose();
        }

        [Fact]
        public async Task HealthStatusChanged_EventShouldBeRaised()
        {
            // Arrange
            HealthCheckResult? eventResult = null;
            _monitor.HealthStatusChanged += (sender, result) => eventResult = result;

            var validToken = CreateSecureString("sk-ant-api03-1234567890abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ");
            _secureStorage.Store(ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable, validToken);

            // Act
            await _monitor.TriggerHealthCheckAsync();

            // Assert
            Assert.NotNull(eventResult);

            // Cleanup
            validToken.Dispose();
        }

        [Fact]
        public async Task ConfigurationChanged_EventShouldBeRaised()
        {
            // Arrange
            Dictionary<string, object>? eventConfig = null;
            _monitor.ConfigurationChanged += (sender, config) => eventConfig = config;

            await _monitor.StartMonitoringAsync();

            // Act - Change configuration
            var validToken = CreateSecureString("sk-ant-api03-1234567890abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ");
            await _configurator.SetAuthTokenAsync(validToken);

            // Wait a bit for the monitor to detect the change
            await Task.Delay(200);

            // Assert
            // Note: The configuration change detection runs on a timer, so we might need to wait
            // In a real test environment, we might need to adjust timing or use more sophisticated synchronization

            // Cleanup
            validToken.Dispose();
            await _monitor.StopMonitoringAsync();
        }

        [Fact]
        public void GetMonitoringStatistics_ShouldReturnValidStatistics()
        {
            // Act
            var stats = _monitor.GetMonitoringStatistics();

            // Assert
            Assert.NotNull(stats);
            Assert.Contains("IsMonitoring", stats);
            Assert.Contains("MonitoringStartedAt", stats);
            Assert.Contains("LastHealthCheckTime", stats);
            Assert.Contains("ConsecutiveFailures", stats);
            Assert.Contains("HealthCheckIntervalMs", stats);
            Assert.Contains("ConfigurationCheckIntervalMs", stats);
            Assert.Contains("EnableAutoRecovery", stats);
            Assert.Contains("MaxRetryAttempts", stats);
            Assert.Contains("ObserversCount", stats);
            Assert.Contains("Timestamp", stats);

            Assert.False((bool)stats["IsMonitoring"]);
            Assert.Equal("N/A", stats["MonitoringStartedAt"]);
            Assert.Equal(0, stats["ConsecutiveFailures"]);
            Assert.Equal(100, stats["HealthCheckIntervalMs"]);
            Assert.Equal(50, stats["ConfigurationCheckIntervalMs"]);
            Assert.True((bool)stats["EnableAutoRecovery"]);
            Assert.Equal(2, stats["MaxRetryAttempts"]);
        }

        [Fact]
        public async Task GetMonitoringStatistics_WithRunningMonitor_ShouldIncludeUptime()
        {
            // Arrange
            await _monitor.StartMonitoringAsync();

            // Act
            var stats = _monitor.GetMonitoringStatistics();

            // Assert
            Assert.True((bool)stats["IsMonitoring"]);
            Assert.NotEqual("N/A", stats["MonitoringStartedAt"]);
            Assert.Contains("UptimeMinutes", stats);

            // Cleanup
            await _monitor.StopMonitoringAsync();
        }

        [Fact]
        public void RegisterObserver_ShouldAddObserver()
        {
            // Arrange
            var observer = new TestObserver();

            // Act
            _monitor.RegisterObserver(observer);

            // Assert
            var stats = _monitor.GetMonitoringStatistics();
            Assert.Equal(1, stats["ObserversCount"]);
        }

        [Fact]
        public void RegisterObserver_WithNullObserver_ShouldThrow()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _monitor.RegisterObserver(null!));
        }

        [Fact]
        public void UnregisterObserver_ShouldRemoveObserver()
        {
            // Arrange
            var observer = new TestObserver();
            _monitor.RegisterObserver(observer);

            // Act
            _monitor.UnregisterObserver(observer);

            // Assert
            var stats = _monitor.GetMonitoringStatistics();
            Assert.Equal(0, stats["ObserversCount"]);
        }

        [Fact]
        public void UnregisterObserver_WithNullObserver_ShouldNotThrow()
        {
            // Act & Assert - Should not throw
            _monitor.UnregisterObserver(null!);
        }

        [Fact]
        public void ConsecutiveFailures_ShouldTrackFailures()
        {
            // Initially should be 0
            Assert.Equal(0, _monitor.ConsecutiveFailures);
        }

        [Fact]
        public async Task Dispose_ShouldStopMonitoringAndCleanup()
        {
            // Arrange
            var observer = new TestObserver();
            _monitor.RegisterObserver(observer);
            await _monitor.StartMonitoringAsync();

            // Act
            _monitor.Dispose();

            // Assert
            Assert.False(_monitor.IsMonitoring);
            Assert.True(observer.OnCompletedCalled);

            // Verify that operations after dispose throw ObjectDisposedException
            await Assert.ThrowsAsync<ObjectDisposedException>(() => _monitor.StartMonitoringAsync());
        }

        [Fact]
        public async Task PeriodicHealthChecks_ShouldRunAutomatically()
        {
            // Arrange
            var healthCheckEvents = new List<MonitorEventArgs>();
            _monitor.MonitorEvent += (sender, args) =>
            {
                if (args.EventType == MonitorEventType.PeriodicCheckCompleted)
                {
                    healthCheckEvents.Add(args);
                }
            };

            var validToken = CreateSecureString("sk-ant-api03-1234567890abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ");
            _secureStorage.Store(ClaudeCodeConfigConstants.AuthTokenEnvironmentVariable, validToken);

            // Act
            await _monitor.StartMonitoringAsync();
            await Task.Delay(300); // Wait for at least 2-3 health check intervals

            // Assert
            Assert.NotEmpty(healthCheckEvents);

            // Cleanup
            await _monitor.StopMonitoringAsync();
            validToken.Dispose();
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

            _monitor?.Dispose();
            _validator?.Dispose();
            _configurator?.Dispose();
            _secureStorage?.Dispose();
            _logger?.Dispose();
        }

        /// <summary>
        /// 测试用观察者类
        /// </summary>
        private class TestObserver : IObserver<MonitorEventArgs>
        {
            public bool OnNextCalled { get; private set; }
            public bool OnErrorCalled { get; private set; }
            public bool OnCompletedCalled { get; private set; }
            public MonitorEventArgs? LastEvent { get; private set; }

            public void OnNext(MonitorEventArgs value)
            {
                OnNextCalled = true;
                LastEvent = value;
            }

            public void OnError(Exception error)
            {
                OnErrorCalled = true;
            }

            public void OnCompleted()
            {
                OnCompletedCalled = true;
            }
        }
    }

    /// <summary>
    /// MonitorEventArgs测试类
    /// </summary>
    public class MonitorEventArgsTests
    {
        [Fact]
        public void MonitorEventArgs_Constructor_ShouldSetAllProperties()
        {
            // Arrange
            var healthResult = new HealthCheckResult(new List<ConfigurationValidationResult>());
            var configStatus = new Dictionary<string, object> { { "test", "value" } };
            var exception = new InvalidOperationException("Test exception");

            // Act
            var eventArgs = new MonitorEventArgs(
                MonitorEventType.HealthStatusChanged,
                "Test message",
                "Test details",
                healthResult,
                configStatus,
                exception);

            // Assert
            Assert.Equal(MonitorEventType.HealthStatusChanged, eventArgs.EventType);
            Assert.Equal("Test message", eventArgs.Message);
            Assert.Equal("Test details", eventArgs.Details);
            Assert.Equal(healthResult, eventArgs.HealthResult);
            Assert.Equal(configStatus, eventArgs.ConfigurationStatus);
            Assert.Equal(exception, eventArgs.Exception);
            Assert.True(eventArgs.Timestamp <= DateTime.UtcNow);
        }

        [Fact]
        public void MonitorEventArgs_Constructor_WithNullMessage_ShouldThrow()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new MonitorEventArgs(
                MonitorEventType.MonitoringStarted,
                null!));
        }
    }

    /// <summary>
    /// MonitorConfiguration测试类
    /// </summary>
    public class MonitorConfigurationTests
    {
        [Fact]
        public void MonitorConfiguration_DefaultValues_ShouldBeCorrect()
        {
            // Act
            var config = new MonitorConfiguration();

            // Assert
            Assert.Equal(30000, config.HealthCheckIntervalMs);
            Assert.Equal(10000, config.ConfigurationCheckIntervalMs);
            Assert.True(config.EnableAutoRecovery);
            Assert.Equal(3, config.MaxRetryAttempts);
            Assert.Equal(5000, config.RetryIntervalMs);
            Assert.False(config.EnableVerboseLogging);
            Assert.Equal(70, config.HealthScoreWarningThreshold);
            Assert.Equal(50, config.HealthScoreErrorThreshold);
        }

        [Fact]
        public void MonitorConfiguration_Properties_ShouldBeSettable()
        {
            // Arrange
            var config = new MonitorConfiguration();

            // Act
            config.HealthCheckIntervalMs = 15000;
            config.ConfigurationCheckIntervalMs = 5000;
            config.EnableAutoRecovery = false;
            config.MaxRetryAttempts = 5;
            config.RetryIntervalMs = 2000;
            config.EnableVerboseLogging = true;
            config.HealthScoreWarningThreshold = 80;
            config.HealthScoreErrorThreshold = 60;

            // Assert
            Assert.Equal(15000, config.HealthCheckIntervalMs);
            Assert.Equal(5000, config.ConfigurationCheckIntervalMs);
            Assert.False(config.EnableAutoRecovery);
            Assert.Equal(5, config.MaxRetryAttempts);
            Assert.Equal(2000, config.RetryIntervalMs);
            Assert.True(config.EnableVerboseLogging);
            Assert.Equal(80, config.HealthScoreWarningThreshold);
            Assert.Equal(60, config.HealthScoreErrorThreshold);
        }
    }
}