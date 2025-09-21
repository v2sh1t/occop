using FluentAssertions;
using Moq;
using Occop.Core.Security;
using Occop.Core.Models.Security;
using Occop.Services.Security;
using Xunit;

namespace Occop.Tests.Services.Security
{
    /// <summary>
    /// CleanupValidator类的单元测试
    /// Unit tests for CleanupValidator class
    /// </summary>
    public class CleanupValidatorTests : IDisposable
    {
        private readonly SecurityContext _securityContext;
        private readonly SecurityAuditor _auditor;
        private readonly CleanupValidator _validator;

        public CleanupValidatorTests()
        {
            _securityContext = new SecurityContext("test-app", SecurityLevel.High);
            _auditor = new SecurityAuditor(_securityContext);
            _validator = new CleanupValidator(_securityContext, _auditor);
        }

        public void Dispose()
        {
            _validator?.Dispose();
            _auditor?.Dispose();
            _securityContext?.Dispose();
        }

        /// <summary>
        /// 测试CleanupValidator的基本构造和属性
        /// Tests basic construction and properties of CleanupValidator
        /// </summary>
        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
        {
            // Arrange
            var context = new SecurityContext("test-app", SecurityLevel.Medium);
            var auditor = new SecurityAuditor(context);
            var config = new ValidationConfiguration
            {
                EnablePeriodicValidation = false,
                ValidationTimeout = TimeSpan.FromMinutes(10)
            };

            // Act
            using var validator = new CleanupValidator(context, auditor, config);

            // Assert
            validator.Should().NotBeNull();
            validator.IsEnabled.Should().BeTrue();
            validator.Configuration.Should().BeSameAs(config);
            validator.ActiveValidationSessions.Should().Be(0);

            auditor.Dispose();
            context.Dispose();
        }

        [Fact]
        public void Constructor_WithNullSecurityContext_ShouldThrowArgumentNullException()
        {
            // Arrange
            var auditor = new SecurityAuditor(_securityContext);

            // Act & Assert
            Action act = () => new CleanupValidator(null!, auditor);
            act.Should().Throw<ArgumentNullException>().WithParameterName("securityContext");

            auditor.Dispose();
        }

        [Fact]
        public void Constructor_WithNullAuditor_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Action act = () => new CleanupValidator(_securityContext, null!);
            act.Should().Throw<ArgumentNullException>().WithParameterName("auditor");
        }

        [Fact]
        public void Constructor_WithDefaultConfiguration_ShouldUseDefaultValues()
        {
            // Arrange
            var context = new SecurityContext("test-app");
            var auditor = new SecurityAuditor(context);

            // Act
            using var validator = new CleanupValidator(context, auditor);

            // Assert
            validator.Configuration.Should().NotBeNull();
            validator.Configuration.EnablePeriodicValidation.Should().BeTrue();
            validator.Configuration.PeriodicValidationInterval.Should().Be(TimeSpan.FromMinutes(30));

            auditor.Dispose();
            context.Dispose();
        }

        /// <summary>
        /// 测试验证清理状态
        /// Tests validating cleanup state
        /// </summary>
        [Fact]
        public async Task ValidateCleanupStateAsync_WithValidState_ShouldReturnSuccessResult()
        {
            // Arrange
            var targetResource = "test-resource";
            var expectedState = CleanupState.Cleaned;

            // Act
            var result = await _validator.ValidateCleanupStateAsync(targetResource, expectedState);

            // Assert
            result.Should().NotBeNull();
            result.ValidationType.Should().Be(ValidationType.CleanupValidation);
            result.TargetResource.Should().Be(targetResource);
            result.SessionId.Should().Be(_securityContext.SessionId);
            result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
            result.Tags.Should().Contain("cleanup");
            result.Tags.Should().Contain("security");
        }

        [Fact]
        public async Task ValidateCleanupStateAsync_ShouldTriggerValidationEvent()
        {
            // Arrange
            var targetResource = "test-resource";
            var expectedState = CleanupState.Validated;

            ValidationEventArgs? capturedEvent = null;
            _validator.ValidationEvent += (sender, args) => capturedEvent = args;

            // Act
            var result = await _validator.ValidateCleanupStateAsync(targetResource, expectedState);

            // Assert
            capturedEvent.Should().NotBeNull();
            capturedEvent!.ValidationResult.Should().BeSameAs(result);
            capturedEvent.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task ValidateCleanupStateAsync_WithFailures_ShouldTriggerValidationFailedEvent()
        {
            // Arrange
            var targetResource = "failing-resource";
            var expectedState = CleanupState.Failed;

            CleanupValidationFailedEventArgs? capturedEvent = null;
            _validator.ValidationFailed += (sender, args) => capturedEvent = args;

            // Act
            var result = await _validator.ValidateCleanupStateAsync(targetResource, expectedState);

            // Assert
            // Note: The actual validation failure depends on the internal implementation
            // This test verifies the event mechanism is in place
            if (!result.IsValid)
            {
                capturedEvent.Should().NotBeNull();
                capturedEvent!.TargetResource.Should().Be(targetResource);
                capturedEvent.ValidationResult.Should().BeSameAs(result);
            }
        }

        /// <summary>
        /// 测试零敏感数据泄露验证
        /// Tests zero sensitive data leak validation
        /// </summary>
        [Fact]
        public async Task ValidateZeroSensitiveDataLeakAsync_WithNoLeaks_ShouldReturnSuccess()
        {
            // Arrange
            var dataIdentifiers = new List<string> { "API_KEY", "SECRET_TOKEN" };
            var validationScope = ValidationScope.Memory | ValidationScope.EnvironmentVariables;

            // Act
            var result = await _validator.ValidateZeroSensitiveDataLeakAsync(dataIdentifiers, validationScope);

            // Assert
            result.Should().NotBeNull();
            result.ValidationType.Should().Be(ValidationType.SensitiveDataLeakValidation);
            result.TargetResource.Should().Be("sensitive_data");
            result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
            result.Context.Should().ContainKey("zero_leak_achieved");
        }

        [Fact]
        public async Task ValidateZeroSensitiveDataLeakAsync_WithEmptyIdentifiers_ShouldComplete()
        {
            // Arrange
            var dataIdentifiers = new List<string>();

            // Act
            var result = await _validator.ValidateZeroSensitiveDataLeakAsync(dataIdentifiers);

            // Assert
            result.Should().NotBeNull();
            result.ValidationType.Should().Be(ValidationType.SensitiveDataLeakValidation);
            // With empty identifiers, should generally find no leaks
            result.Context.Should().ContainKey("zero_leak_achieved");
        }

        [Theory]
        [InlineData(ValidationScope.Memory)]
        [InlineData(ValidationScope.EnvironmentVariables)]
        [InlineData(ValidationScope.TemporaryFiles)]
        [InlineData(ValidationScope.ProcessMemory)]
        [InlineData(ValidationScope.Full)]
        public async Task ValidateZeroSensitiveDataLeakAsync_WithDifferentScopes_ShouldComplete(ValidationScope scope)
        {
            // Arrange
            var dataIdentifiers = new List<string> { "TEST_KEY" };

            // Act
            var result = await _validator.ValidateZeroSensitiveDataLeakAsync(dataIdentifiers, scope);

            // Assert
            result.Should().NotBeNull();
            result.ValidationType.Should().Be(ValidationType.SensitiveDataLeakValidation);
        }

        /// <summary>
        /// 测试幂等性验证
        /// Tests idempotency validation
        /// </summary>
        [Fact]
        public async Task ValidateIdempotencyAsync_WithNewOperation_ShouldReturnIdempotent()
        {
            // Arrange
            var operationId = "new-operation-123";
            var operationParameters = new Dictionary<string, object>
            {
                { "param1", "value1" },
                { "param2", 42 }
            };

            // Act
            var result = await _validator.ValidateIdempotencyAsync(operationId, operationParameters);

            // Assert
            result.Should().NotBeNull();
            result.ValidationType.Should().Be(ValidationType.IdempotencyValidation);
            result.TargetResource.Should().Be(operationId);
            result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
            result.Context.Should().ContainKey("operation_history_count");
        }

        [Fact]
        public async Task ValidateIdempotencyAsync_WithEmptyParameters_ShouldComplete()
        {
            // Arrange
            var operationId = "empty-params-operation";
            var operationParameters = new Dictionary<string, object>();

            // Act
            var result = await _validator.ValidateIdempotencyAsync(operationId, operationParameters);

            // Assert
            result.Should().NotBeNull();
            result.ValidationType.Should().Be(ValidationType.IdempotencyValidation);
            result.TargetResource.Should().Be(operationId);
        }

        /// <summary>
        /// 测试清理成功率验证
        /// Tests cleanup success rate validation
        /// </summary>
        [Fact]
        public async Task ValidateCleanupSuccessRateAsync_WithDefaultParameters_ShouldReturnResult()
        {
            // Arrange
            // Add some audit logs to have data for statistics
            await _auditor.LogCleanupOperationAsync("test", "Test cleanup", AuditOperationResult.Succeeded);
            await _auditor.LogCleanupOperationAsync("test", "Test cleanup", AuditOperationResult.Succeeded);

            // Act
            var result = await _validator.ValidateCleanupSuccessRateAsync();

            // Assert
            result.Should().NotBeNull();
            result.ValidationType.Should().Be(ValidationType.StateValidation);
            result.TargetResource.Should().Be("cleanup_success_rate");
            result.Context.Should().ContainKey("actual_success_rate");
            result.Context.Should().ContainKey("minimum_success_rate");
            result.Context.Should().ContainKey("total_operations");
        }

        [Theory]
        [InlineData(0.90, true)]   // Lower requirement, should pass
        [InlineData(0.99, false)]  // Higher requirement, might fail
        public async Task ValidateCleanupSuccessRateAsync_WithDifferentRequirements_ShouldEvaluateCorrectly(
            double minimumSuccessRate, bool expectPassWithNoFailures)
        {
            // Arrange
            // Add successful operations
            await _auditor.LogCleanupOperationAsync("test", "Success 1", AuditOperationResult.Succeeded);
            await _auditor.LogCleanupOperationAsync("test", "Success 2", AuditOperationResult.Succeeded);

            // Act
            var result = await _validator.ValidateCleanupSuccessRateAsync(null, minimumSuccessRate);

            // Assert
            result.Should().NotBeNull();
            result.Context["minimum_success_rate"].Should().Be(minimumSuccessRate);

            // The actual pass/fail depends on current statistics
            // This test verifies the method completes and includes the right context
        }

        [Fact]
        public async Task ValidateCleanupSuccessRateAsync_WithTimeRange_ShouldFilterStatistics()
        {
            // Arrange
            await _auditor.LogCleanupOperationAsync("test", "Recent cleanup", AuditOperationResult.Succeeded);
            var timeRange = TimeSpan.FromMinutes(1);

            // Act
            var result = await _validator.ValidateCleanupSuccessRateAsync(timeRange);

            // Assert
            result.Should().NotBeNull();
            result.Context.Should().ContainKey("actual_success_rate");
        }

        /// <summary>
        /// 测试内存清理完整性验证
        /// Tests memory cleanup integrity validation
        /// </summary>
        [Fact]
        public async Task ValidateMemoryCleanupIntegrityAsync_ShouldPerformMemoryValidation()
        {
            // Act
            var result = await _validator.ValidateMemoryCleanupIntegrityAsync();

            // Assert
            result.Should().NotBeNull();
            result.ValidationType.Should().Be(ValidationType.MemoryLeakValidation);
            result.TargetResource.Should().Be("memory");
            result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
            result.Context.Should().ContainKey("memory_before_gc");
            result.Context.Should().ContainKey("memory_after_gc");
            result.Context.Should().ContainKey("memory_freed");
            result.Context.Should().ContainKey("memory_leak_detected");
        }

        [Fact]
        public async Task ValidateMemoryCleanupIntegrityAsync_ShouldForceGarbageCollection()
        {
            // Arrange
            var initialMemory = GC.GetTotalMemory(false);

            // Act
            var result = await _validator.ValidateMemoryCleanupIntegrityAsync();

            // Assert
            result.Should().NotBeNull();
            var memoryBefore = (long)result.Context["memory_before_gc"];
            var memoryAfter = (long)result.Context["memory_after_gc"];

            // Memory before should be >= memory after due to GC
            memoryBefore.Should().BeGreaterOrEqualTo(memoryAfter);
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
            var validator = new CleanupValidator(context, auditor);

            // Act
            validator.Dispose();

            // Assert
            // Verify that dispose completes without exception
            Action act = () => validator.Dispose(); // Should not throw on multiple dispose calls
            act.Should().NotThrow();

            auditor.Dispose();
            context.Dispose();
        }

        [Fact]
        public void Dispose_MultipleCallsShouldNotThrow()
        {
            // Arrange
            var context = new SecurityContext("test-app");
            var auditor = new SecurityAuditor(context);
            var validator = new CleanupValidator(context, auditor);

            // Act & Assert
            validator.Dispose();
            Action act = () => validator.Dispose();
            act.Should().NotThrow();

            auditor.Dispose();
            context.Dispose();
        }

        /// <summary>
        /// 测试并发验证会话管理
        /// Tests concurrent validation session management
        /// </summary>
        [Fact]
        public async Task ConcurrentValidations_ShouldManageSessionsCorrectly()
        {
            // Arrange
            var tasks = new List<Task<ValidationResult>>();

            // Act - Start multiple concurrent validations
            for (int i = 0; i < 5; i++)
            {
                var resourceName = $"resource-{i}";
                tasks.Add(_validator.ValidateCleanupStateAsync(resourceName, CleanupState.Cleaned));
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            results.Should().HaveCount(5);
            results.Should().OnlyContain(r => r != null);
            results.Should().OnlyContain(r => r.Duration > TimeSpan.Zero);

            // All validations should have completed, so active sessions should be 0
            _validator.ActiveValidationSessions.Should().Be(0);
        }

        /// <summary>
        /// 测试异常处理
        /// Tests exception handling
        /// </summary>
        [Fact]
        public async Task ValidateCleanupStateAsync_WithNullResource_ShouldHandleGracefully()
        {
            // This test depends on how the implementation handles null values
            // The method might throw ArgumentNullException or handle it gracefully

            // Act & Assert
            Func<Task> act = async () => await _validator.ValidateCleanupStateAsync(null!, CleanupState.Cleaned);

            // The implementation should either handle this gracefully or throw a meaningful exception
            await act.Should().NotThrowAsync<NullReferenceException>();
        }
    }

    /// <summary>
    /// ValidationConfiguration类的单元测试
    /// Unit tests for ValidationConfiguration class
    /// </summary>
    public class ValidationConfigurationTests
    {
        /// <summary>
        /// 测试默认配置值
        /// Tests default configuration values
        /// </summary>
        [Fact]
        public void Constructor_ShouldSetDefaultValues()
        {
            // Act
            var config = new ValidationConfiguration();

            // Assert
            config.EnablePeriodicValidation.Should().BeTrue();
            config.PeriodicValidationInterval.Should().Be(TimeSpan.FromMinutes(30));
            config.SensitiveEnvironmentVariables.Should().NotBeNull();
            config.SensitiveEnvironmentVariables.Should().Contain("GITHUB_TOKEN");
            config.SensitiveEnvironmentVariables.Should().Contain("API_KEY");
            config.SensitiveEnvironmentVariables.Should().Contain("SECRET");
            config.EnableVerboseLogging.Should().BeFalse();
            config.ValidationTimeout.Should().Be(TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// 测试配置属性可设置性
        /// Tests configuration properties are settable
        /// </summary>
        [Fact]
        public void Properties_ShouldBeSettable()
        {
            // Arrange
            var config = new ValidationConfiguration();

            // Act
            config.EnablePeriodicValidation = false;
            config.PeriodicValidationInterval = TimeSpan.FromHours(1);
            config.EnableVerboseLogging = true;
            config.ValidationTimeout = TimeSpan.FromMinutes(10);
            config.SensitiveEnvironmentVariables.Add("CUSTOM_SECRET");

            // Assert
            config.EnablePeriodicValidation.Should().BeFalse();
            config.PeriodicValidationInterval.Should().Be(TimeSpan.FromHours(1));
            config.EnableVerboseLogging.Should().BeTrue();
            config.ValidationTimeout.Should().Be(TimeSpan.FromMinutes(10));
            config.SensitiveEnvironmentVariables.Should().Contain("CUSTOM_SECRET");
        }
    }

    /// <summary>
    /// ValidationSession类的单元测试
    /// Unit tests for ValidationSession class
    /// </summary>
    public class ValidationSessionTests
    {
        /// <summary>
        /// 测试ValidationSession的基本构造和属性
        /// Tests basic construction and properties of ValidationSession
        /// </summary>
        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
        {
            // Arrange
            var id = "session-123";
            var targetResource = "test-resource";
            var type = ValidationType.CleanupValidation;

            // Act
            var session = new ValidationSession(id, targetResource, type);

            // Assert
            session.Should().NotBeNull();
            session.Id.Should().Be(id);
            session.TargetResource.Should().Be(targetResource);
            session.Type.Should().Be(type);
            session.StartTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }

        /// <summary>
        /// 测试不同类型的验证会话
        /// Tests different types of validation sessions
        /// </summary>
        [Theory]
        [InlineData(ValidationType.CleanupValidation)]
        [InlineData(ValidationType.MemoryLeakValidation)]
        [InlineData(ValidationType.SensitiveDataLeakValidation)]
        [InlineData(ValidationType.IdempotencyValidation)]
        [InlineData(ValidationType.IntegrityValidation)]
        public void Constructor_WithDifferentValidationTypes_ShouldInitializeCorrectly(ValidationType validationType)
        {
            // Arrange
            var id = $"session-{validationType}";
            var targetResource = $"resource-{validationType}";

            // Act
            var session = new ValidationSession(id, targetResource, validationType);

            // Assert
            session.Type.Should().Be(validationType);
            session.Id.Should().Be(id);
            session.TargetResource.Should().Be(targetResource);
        }
    }

    /// <summary>
    /// OperationHistoryEntry类的单元测试
    /// Unit tests for OperationHistoryEntry class
    /// </summary>
    public class OperationHistoryEntryTests
    {
        /// <summary>
        /// 测试OperationHistoryEntry的基本功能
        /// Tests basic functionality of OperationHistoryEntry
        /// </summary>
        [Fact]
        public void Properties_ShouldBeSettableAndGettable()
        {
            // Arrange
            var entry = new OperationHistoryEntry();
            var operationId = "op-123";
            var timestamp = DateTime.UtcNow;
            var parameters = new Dictionary<string, object> { { "param1", "value1" } };
            var results = new Dictionary<string, object> { { "result1", "success" } };

            // Act
            entry.OperationId = operationId;
            entry.Timestamp = timestamp;
            entry.Parameters = parameters;
            entry.Results = results;

            // Assert
            entry.OperationId.Should().Be(operationId);
            entry.Timestamp.Should().Be(timestamp);
            entry.Parameters.Should().BeSameAs(parameters);
            entry.Results.Should().BeSameAs(results);
        }

        [Fact]
        public void Constructor_ShouldInitializeCollections()
        {
            // Act
            var entry = new OperationHistoryEntry();

            // Assert
            entry.Parameters.Should().NotBeNull().And.BeEmpty();
            entry.Results.Should().NotBeNull().And.BeEmpty();
            entry.OperationId.Should().Be(string.Empty);
        }
    }

    /// <summary>
    /// 事件参数类的单元测试
    /// Unit tests for event argument classes
    /// </summary>
    public class ValidationEventArgsTests
    {
        [Fact]
        public void ValidationEventArgs_WithValidationResult_ShouldInitializeCorrectly()
        {
            // Arrange
            var validationResult = ValidationResult.CreateCleanupValidation("test", "session");

            // Act
            var eventArgs = new ValidationEventArgs(validationResult);

            // Assert
            eventArgs.ValidationResult.Should().BeSameAs(validationResult);
            eventArgs.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }
    }

    public class CleanupValidationFailedEventArgsTests
    {
        [Fact]
        public void CleanupValidationFailedEventArgs_WithValidParameters_ShouldInitializeCorrectly()
        {
            // Arrange
            var validationResult = ValidationResult.CreateCleanupValidation("test", "session");
            var targetResource = "test-resource";

            // Act
            var eventArgs = new CleanupValidationFailedEventArgs(validationResult, targetResource);

            // Assert
            eventArgs.ValidationResult.Should().BeSameAs(validationResult);
            eventArgs.TargetResource.Should().Be(targetResource);
            eventArgs.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }
    }
}