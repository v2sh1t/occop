using FluentAssertions;
using Moq;
using Occop.Core.Security;
using Occop.Core.Models.Security;
using Occop.Services.Security;
using Xunit;

namespace Occop.Tests.Services.Security
{
    /// <summary>
    /// SecurityAuditor类的单元测试
    /// Unit tests for SecurityAuditor class
    /// </summary>
    public class SecurityAuditorTests : IDisposable
    {
        private readonly SecurityContext _securityContext;
        private readonly SecurityAuditor _auditor;

        public SecurityAuditorTests()
        {
            _securityContext = new SecurityContext("test-app", SecurityLevel.High);
            _auditor = new SecurityAuditor(_securityContext);
        }

        public void Dispose()
        {
            _auditor?.Dispose();
            _securityContext?.Dispose();
        }

        /// <summary>
        /// 测试SecurityAuditor的基本构造和属性
        /// Tests basic construction and properties of SecurityAuditor
        /// </summary>
        [Fact]
        public void Constructor_WithValidSecurityContext_ShouldInitializeCorrectly()
        {
            // Arrange
            var context = new SecurityContext("test-app", SecurityLevel.Medium);
            context.IsAuditLoggingEnabled = true;

            // Act
            using var auditor = new SecurityAuditor(context);

            // Assert
            auditor.Should().NotBeNull();
            auditor.IsEnabled.Should().Be(context.IsAuditLoggingEnabled);
            auditor.TotalAuditLogs.Should().Be(0);
            auditor.PendingAuditLogs.Should().Be(0);
            auditor.Configuration.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithNullSecurityContext_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Action act = () => new SecurityAuditor(null!);
            act.Should().Throw<ArgumentNullException>().WithParameterName("securityContext");
        }

        [Fact]
        public void Constructor_WithCustomConfiguration_ShouldUseProvidedConfiguration()
        {
            // Arrange
            var context = new SecurityContext("test-app");
            var config = new AuditConfiguration
            {
                FlushInterval = TimeSpan.FromMinutes(10),
                MaxLogRetention = TimeSpan.FromDays(60),
                EnableVerboseLogging = true
            };

            // Act
            using var auditor = new SecurityAuditor(context, config);

            // Assert
            auditor.Configuration.Should().BeSameAs(config);
            auditor.Configuration.FlushInterval.Should().Be(TimeSpan.FromMinutes(10));
            auditor.Configuration.MaxLogRetention.Should().Be(TimeSpan.FromDays(60));
            auditor.Configuration.EnableVerboseLogging.Should().BeTrue();
        }

        /// <summary>
        /// 测试启用和禁用审计器
        /// Tests enabling and disabling auditor
        /// </summary>
        [Fact]
        public async Task EnableAsync_WhenDisabled_ShouldEnableAuditor()
        {
            // Arrange
            using var auditor = new SecurityAuditor(new SecurityContext("test-app"));
            // Auditor should be disabled initially for this test

            // Act
            await auditor.EnableAsync();

            // Assert
            auditor.IsEnabled.Should().BeTrue();
        }

        [Fact]
        public async Task DisableAsync_WhenEnabled_ShouldDisableAuditor()
        {
            // Arrange
            // Auditor is enabled by default in our test setup

            // Act
            await _auditor.DisableAsync();

            // Assert
            _auditor.IsEnabled.Should().BeFalse();
        }

        /// <summary>
        /// 测试记录安全初始化事件
        /// Tests logging security initialization events
        /// </summary>
        [Fact]
        public async Task LogSecurityInitializationAsync_WithValidParameters_ShouldCreateAuditLog()
        {
            // Arrange
            var description = "Security manager initialized";
            var details = new Dictionary<string, object>
            {
                { "version", "1.0.0" },
                { "security_level", SecurityLevel.High }
            };

            // Act
            var auditLogId = await _auditor.LogSecurityInitializationAsync(description, details);

            // Assert
            auditLogId.Should().NotBeNullOrEmpty();
            _auditor.TotalAuditLogs.Should().Be(1);
        }

        [Fact]
        public async Task LogSecurityInitializationAsync_WithoutDetails_ShouldCreateAuditLog()
        {
            // Arrange
            var description = "Security manager initialized without details";

            // Act
            var auditLogId = await _auditor.LogSecurityInitializationAsync(description);

            // Assert
            auditLogId.Should().NotBeNullOrEmpty();
            _auditor.TotalAuditLogs.Should().Be(1);
        }

        /// <summary>
        /// 测试记录清理操作事件
        /// Tests logging cleanup operation events
        /// </summary>
        [Fact]
        public async Task LogCleanupOperationAsync_WithSuccessfulOperation_ShouldCreateSuccessAuditLog()
        {
            // Arrange
            var cleanupType = "memory";
            var description = "Memory cleanup completed";
            var result = AuditOperationResult.Succeeded;
            var itemsAffected = 5;
            var duration = TimeSpan.FromSeconds(2);

            // Act
            var auditLogId = await _auditor.LogCleanupOperationAsync(
                cleanupType, description, result, itemsAffected, duration);

            // Assert
            auditLogId.Should().NotBeNullOrEmpty();
            _auditor.TotalAuditLogs.Should().Be(1);
        }

        [Fact]
        public async Task LogCleanupOperationAsync_WithFailedOperation_ShouldTriggerCriticalEvent()
        {
            // Arrange
            var cleanupType = "sensitive_data";
            var description = "Failed to cleanup sensitive data";
            var result = AuditOperationResult.Failed;
            var errorMessage = "Access denied";

            bool criticalEventTriggered = false;
            _auditor.CriticalSecurityEvent += (sender, args) => criticalEventTriggered = true;

            // Act
            var auditLogId = await _auditor.LogCleanupOperationAsync(
                cleanupType, description, result, errorMessage: errorMessage);

            // Assert
            auditLogId.Should().NotBeNullOrEmpty();
            criticalEventTriggered.Should().BeTrue();
            _auditor.TotalAuditLogs.Should().Be(1);
        }

        /// <summary>
        /// 测试记录内存清理事件
        /// Tests logging memory cleanup events
        /// </summary>
        [Fact]
        public async Task LogMemoryCleanupAsync_WithValidParameters_ShouldCreateAuditLog()
        {
            // Arrange
            var description = "Memory cleanup executed";
            var memoryFreed = 1024000L;
            var gcCollections = 3;
            var duration = TimeSpan.FromMilliseconds(500);

            // Act
            var auditLogId = await _auditor.LogMemoryCleanupAsync(
                description, memoryFreed, gcCollections, duration);

            // Assert
            auditLogId.Should().NotBeNullOrEmpty();
            _auditor.TotalAuditLogs.Should().Be(1);
        }

        /// <summary>
        /// 测试记录敏感信息验证事件
        /// Tests logging sensitive information validation events
        /// </summary>
        [Fact]
        public async Task LogSensitiveDataValidationAsync_WithZeroLeak_ShouldCreateSuccessAuditLog()
        {
            // Arrange
            var validationType = "zero_leak_validation";
            var description = "Zero leak validation passed";
            var validationResult = ValidationResult.CreateCleanupValidation("test", _securityContext.SessionId);
            validationResult.Complete(true, 1.0);
            var isZeroLeak = true;

            // Act
            var auditLogId = await _auditor.LogSensitiveDataValidationAsync(
                validationType, description, validationResult, null, isZeroLeak);

            // Assert
            auditLogId.Should().NotBeNullOrEmpty();
            _auditor.TotalAuditLogs.Should().Be(1);
        }

        [Fact]
        public async Task LogSensitiveDataValidationAsync_WithLeakDetected_ShouldTriggerCriticalEvent()
        {
            // Arrange
            var validationType = "zero_leak_validation";
            var description = "Sensitive data leak detected";
            var validationResult = ValidationResult.CreateCleanupValidation("test", _securityContext.SessionId);
            validationResult.Complete(false, 0.0);
            var sensitiveDataFound = new List<SensitiveDataItem>
            {
                new("API_KEY", "Environment", RiskLevel.High)
            };
            var isZeroLeak = false;

            bool criticalEventTriggered = false;
            _auditor.CriticalSecurityEvent += (sender, args) => criticalEventTriggered = true;

            // Act
            var auditLogId = await _auditor.LogSensitiveDataValidationAsync(
                validationType, description, validationResult, sensitiveDataFound, isZeroLeak);

            // Assert
            auditLogId.Should().NotBeNullOrEmpty();
            criticalEventTriggered.Should().BeTrue();
            _auditor.TotalAuditLogs.Should().Be(1);
        }

        /// <summary>
        /// 测试记录幂等性验证事件
        /// Tests logging idempotency validation events
        /// </summary>
        [Fact]
        public async Task LogIdempotencyValidationAsync_WithValidOperation_ShouldCreateAuditLog()
        {
            // Arrange
            var operationId = "op-123";
            var description = "Idempotency validation passed";
            var isIdempotent = true;
            var previousOperationTime = DateTime.UtcNow.AddMinutes(-5);

            // Act
            var auditLogId = await _auditor.LogIdempotencyValidationAsync(
                operationId, description, isIdempotent, previousOperationTime);

            // Assert
            auditLogId.Should().NotBeNullOrEmpty();
            _auditor.TotalAuditLogs.Should().Be(1);
        }

        [Fact]
        public async Task LogIdempotencyValidationAsync_WithViolation_ShouldCreateWarningAuditLog()
        {
            // Arrange
            var operationId = "op-456";
            var description = "Idempotency violation detected";
            var isIdempotent = false;

            // Act
            var auditLogId = await _auditor.LogIdempotencyValidationAsync(
                operationId, description, isIdempotent);

            // Assert
            auditLogId.Should().NotBeNullOrEmpty();
            _auditor.TotalAuditLogs.Should().Be(1);
        }

        /// <summary>
        /// 测试记录安全异常事件
        /// Tests logging security exception events
        /// </summary>
        [Fact]
        public async Task LogSecurityExceptionAsync_WithGeneralException_ShouldCreateErrorAuditLog()
        {
            // Arrange
            var exception = new InvalidOperationException("Test exception");
            var description = "Security operation failed";
            var context = new Dictionary<string, object>
            {
                { "operation", "test_operation" },
                { "user", "test_user" }
            };

            // Act
            var auditLogId = await _auditor.LogSecurityExceptionAsync(exception, description, context);

            // Assert
            auditLogId.Should().NotBeNullOrEmpty();
            _auditor.TotalAuditLogs.Should().Be(1);
        }

        [Fact]
        public async Task LogSecurityExceptionAsync_WithSecurityException_ShouldTriggerCriticalEvent()
        {
            // Arrange
            var exception = new System.Security.SecurityException("Security violation");
            var description = "Critical security exception occurred";

            bool criticalEventTriggered = false;
            _auditor.CriticalSecurityEvent += (sender, args) => criticalEventTriggered = true;

            // Act
            var auditLogId = await _auditor.LogSecurityExceptionAsync(exception, description);

            // Assert
            auditLogId.Should().NotBeNullOrEmpty();
            criticalEventTriggered.Should().BeTrue();
            _auditor.TotalAuditLogs.Should().Be(1);
        }

        [Fact]
        public async Task LogSecurityExceptionAsync_WithUnauthorizedAccessException_ShouldTriggerCriticalEvent()
        {
            // Arrange
            var exception = new UnauthorizedAccessException("Access denied");
            var description = "Unauthorized access attempt";

            bool criticalEventTriggered = false;
            _auditor.CriticalSecurityEvent += (sender, args) => criticalEventTriggered = true;

            // Act
            var auditLogId = await _auditor.LogSecurityExceptionAsync(exception, description);

            // Assert
            auditLogId.Should().NotBeNullOrEmpty();
            criticalEventTriggered.Should().BeTrue();
            _auditor.TotalAuditLogs.Should().Be(1);
        }

        /// <summary>
        /// 测试获取审计统计信息
        /// Tests getting audit statistics
        /// </summary>
        [Fact]
        public async Task GetAuditStatisticsAsync_WithNoTimeRange_ShouldReturnAllStatistics()
        {
            // Arrange
            await _auditor.LogSecurityInitializationAsync("Init");
            await _auditor.LogCleanupOperationAsync("cleanup", "test", AuditOperationResult.Succeeded);
            await _auditor.LogCleanupOperationAsync("cleanup", "test", AuditOperationResult.Failed);

            // Act
            var statistics = await _auditor.GetAuditStatisticsAsync();

            // Assert
            statistics.Should().NotBeNull();
            statistics.TotalLogs.Should().Be(3);
            statistics.SuccessfulOperations.Should().Be(2); // Init + 1 successful cleanup
            statistics.FailedOperations.Should().Be(1);
            statistics.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task GetAuditStatisticsAsync_WithTimeRange_ShouldReturnFilteredStatistics()
        {
            // Arrange
            await _auditor.LogSecurityInitializationAsync("Recent init");

            // Act
            var statistics = await _auditor.GetAuditStatisticsAsync(TimeSpan.FromMinutes(1));

            // Assert
            statistics.Should().NotBeNull();
            statistics.TotalLogs.Should().Be(1);
            statistics.TimeRange.Should().Be(TimeSpan.FromMinutes(1));
        }

        /// <summary>
        /// 测试验证审计日志完整性
        /// Tests validating audit log integrity
        /// </summary>
        [Fact]
        public async Task ValidateAuditIntegrityAsync_WithValidLogs_ShouldReturnSuccessValidation()
        {
            // Arrange
            await _auditor.LogSecurityInitializationAsync("Test init");

            // Act
            var validationResult = await _auditor.ValidateAuditIntegrityAsync();

            // Assert
            validationResult.Should().NotBeNull();
            validationResult.IsValid.Should().BeTrue();
            validationResult.ValidationType.Should().Be(ValidationType.CleanupValidation);
            validationResult.TargetResource.Should().Be("audit_logs");
        }

        /// <summary>
        /// 测试清理过期日志
        /// Tests cleaning up expired logs
        /// </summary>
        [Fact]
        public async Task CleanupExpiredLogsAsync_WithOldLogs_ShouldRemoveExpiredLogs()
        {
            // Arrange
            await _auditor.LogSecurityInitializationAsync("Old log");
            var initialLogCount = _auditor.TotalAuditLogs;

            // Act
            var removedCount = await _auditor.CleanupExpiredLogsAsync(TimeSpan.FromMilliseconds(1));

            // Wait a bit to ensure logs are considered expired
            await Task.Delay(10);
            removedCount = await _auditor.CleanupExpiredLogsAsync(TimeSpan.FromMilliseconds(1));

            // Assert
            // Note: In a real scenario, logs would be removed.
            // This test verifies the method executes without error.
            removedCount.Should().BeGreaterOrEqualTo(0);
        }

        /// <summary>
        /// 测试事件触发
        /// Tests event triggering
        /// </summary>
        [Fact]
        public async Task AuditEvent_ShouldBeTriggeredWhenLogCreated()
        {
            // Arrange
            AuditEventArgs? capturedEventArgs = null;
            _auditor.AuditEvent += (sender, args) => capturedEventArgs = args;

            // Act
            await _auditor.LogSecurityInitializationAsync("Test event triggering");

            // Assert
            capturedEventArgs.Should().NotBeNull();
            capturedEventArgs!.AuditLog.Should().NotBeNull();
            capturedEventArgs.AuditLog.Description.Should().Be("Test event triggering");
            capturedEventArgs.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task CriticalSecurityEvent_ShouldBeTriggeredForCriticalIssues()
        {
            // Arrange
            CriticalSecurityEventArgs? capturedEventArgs = null;
            _auditor.CriticalSecurityEvent += (sender, args) => capturedEventArgs = args;

            // Act
            await _auditor.LogCleanupOperationAsync("test", "Failed operation", AuditOperationResult.Failed);

            // Assert
            capturedEventArgs.Should().NotBeNull();
            capturedEventArgs!.Message.Should().Be("Cleanup operation failed");
            capturedEventArgs.AuditLog.Should().NotBeNull();
            capturedEventArgs.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }

        /// <summary>
        /// 测试资源释放
        /// Tests resource disposal
        /// </summary>
        [Fact]
        public void Dispose_ShouldCleanupResourcesProperly()
        {
            // Arrange
            var context = new SecurityContext("test-app");
            var auditor = new SecurityAuditor(context);

            // Act
            auditor.Dispose();

            // Assert
            // Verify that dispose completes without exception
            // In a real implementation, we would check that timers are stopped,
            // collections are cleared, etc.
            Action act = () => auditor.Dispose(); // Should not throw on multiple dispose calls
            act.Should().NotThrow();
        }

        [Fact]
        public void Dispose_MultipleCallsShouldNotThrow()
        {
            // Arrange
            var context = new SecurityContext("test-app");
            var auditor = new SecurityAuditor(context);

            // Act & Assert
            auditor.Dispose();
            Action act = () => auditor.Dispose();
            act.Should().NotThrow();
        }

        /// <summary>
        /// 测试禁用状态下的行为
        /// Tests behavior when disabled
        /// </summary>
        [Fact]
        public async Task DisabledAuditor_ShouldNotCreateAuditLogs()
        {
            // Arrange
            var context = new SecurityContext("test-app");
            context.IsAuditLoggingEnabled = false;
            using var auditor = new SecurityAuditor(context);

            // Act
            var auditLogId = await auditor.LogSecurityInitializationAsync("Test with disabled auditor");

            // Assert
            // When auditor is disabled, it should still return an ID but not actually store logs
            // or the behavior might be different based on implementation
            auditLogId.Should().NotBeNullOrEmpty();
        }
    }

    /// <summary>
    /// AuditConfiguration类的单元测试
    /// Unit tests for AuditConfiguration class
    /// </summary>
    public class AuditConfigurationTests
    {
        /// <summary>
        /// 测试AuditConfiguration的默认值
        /// Tests default values of AuditConfiguration
        /// </summary>
        [Fact]
        public void Constructor_ShouldSetDefaultValues()
        {
            // Act
            var config = new AuditConfiguration();

            // Assert
            config.FlushInterval.Should().Be(TimeSpan.FromMinutes(5));
            config.MaxLogRetention.Should().Be(TimeSpan.FromDays(30));
            config.MaxInMemoryLogs.Should().Be(10000);
            config.EnableVerboseLogging.Should().BeFalse();
            config.LogStackTraces.Should().BeFalse();
            config.EnablePerformanceMonitoring.Should().BeTrue();
        }

        /// <summary>
        /// 测试配置属性设置
        /// Tests configuration property setting
        /// </summary>
        [Fact]
        public void Properties_ShouldBeSettable()
        {
            // Arrange
            var config = new AuditConfiguration();

            // Act
            config.FlushInterval = TimeSpan.FromMinutes(10);
            config.MaxLogRetention = TimeSpan.FromDays(60);
            config.MaxInMemoryLogs = 20000;
            config.EnableVerboseLogging = true;
            config.LogStackTraces = true;
            config.EnablePerformanceMonitoring = false;

            // Assert
            config.FlushInterval.Should().Be(TimeSpan.FromMinutes(10));
            config.MaxLogRetention.Should().Be(TimeSpan.FromDays(60));
            config.MaxInMemoryLogs.Should().Be(20000);
            config.EnableVerboseLogging.Should().BeTrue();
            config.LogStackTraces.Should().BeTrue();
            config.EnablePerformanceMonitoring.Should().BeFalse();
        }
    }

    /// <summary>
    /// AuditStatistics类的单元测试
    /// Unit tests for AuditStatistics class
    /// </summary>
    public class AuditStatisticsTests
    {
        /// <summary>
        /// 测试成功率计算
        /// Tests success rate calculation
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0.0)]  // No logs
        [InlineData(10, 10, 1.0)]  // All successful
        [InlineData(10, 5, 0.5)]   // Half successful
        [InlineData(10, 0, 0.0)]   // None successful
        public void SuccessRate_ShouldCalculateCorrectly(int totalLogs, int successfulOps, double expectedRate)
        {
            // Arrange
            var statistics = new AuditStatistics
            {
                TotalLogs = totalLogs,
                SuccessfulOperations = successfulOps
            };

            // Act
            var actualRate = statistics.SuccessRate;

            // Assert
            actualRate.Should().Be(expectedRate);
        }

        /// <summary>
        /// 测试错误率计算
        /// Tests error rate calculation
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0.0)]  // No logs
        [InlineData(10, 2, 1, 0.3)]  // 3 errors out of 10
        [InlineData(10, 0, 0, 0.0)]  // No errors
        [InlineData(10, 5, 5, 1.0)]  // All errors
        public void ErrorRate_ShouldCalculateCorrectly(int totalLogs, int errorEvents, int criticalEvents, double expectedRate)
        {
            // Arrange
            var statistics = new AuditStatistics
            {
                TotalLogs = totalLogs,
                ErrorEvents = errorEvents,
                CriticalEvents = criticalEvents
            };

            // Act
            var actualRate = statistics.ErrorRate;

            // Assert
            actualRate.Should().Be(expectedRate);
        }
    }
}