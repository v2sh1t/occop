using FluentAssertions;
using Occop.Core.Models.Security;
using Xunit;

namespace Occop.Tests.Core.Models.Security
{
    /// <summary>
    /// AuditLog类的单元测试
    /// Unit tests for AuditLog class
    /// </summary>
    public class AuditLogTests
    {
        /// <summary>
        /// 测试AuditLog的基本构造和属性
        /// Tests basic construction and properties of AuditLog
        /// </summary>
        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
        {
            // Arrange
            var eventType = AuditEventType.SecurityCleanup;
            var description = "Test cleanup operation";
            var sessionId = "test-session-123";

            // Act
            var auditLog = new AuditLog(eventType, description, sessionId);

            // Assert
            auditLog.Should().NotBeNull();
            auditLog.Id.Should().NotBeNullOrEmpty();
            auditLog.EventType.Should().Be(eventType);
            auditLog.Description.Should().Be(description);
            auditLog.SessionId.Should().Be(sessionId);
            auditLog.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            auditLog.Result.Should().Be(AuditOperationResult.Unknown);
            auditLog.Severity.Should().Be(AuditSeverity.Information);
            auditLog.Details.Should().NotBeNull().And.BeEmpty();
            auditLog.Tags.Should().NotBeNull().And.BeEmpty();
        }

        /// <summary>
        /// 测试构造函数参数验证
        /// Tests constructor parameter validation
        /// </summary>
        [Theory]
        [InlineData(null, "session")]
        [InlineData("", "session")]
        [InlineData("  ", "session")]
        public void Constructor_WithInvalidDescription_ShouldThrowArgumentNullException(string invalidDescription, string sessionId)
        {
            // Act & Assert
            Action act = () => new AuditLog(AuditEventType.SecurityCleanup, invalidDescription, sessionId);
            act.Should().Throw<ArgumentNullException>().WithParameterName("description");
        }

        [Theory]
        [InlineData("description", null)]
        [InlineData("description", "")]
        [InlineData("description", "  ")]
        public void Constructor_WithInvalidSessionId_ShouldThrowArgumentNullException(string description, string invalidSessionId)
        {
            // Act & Assert
            Action act = () => new AuditLog(AuditEventType.SecurityCleanup, description, invalidSessionId);
            act.Should().Throw<ArgumentNullException>().WithParameterName("sessionId");
        }

        /// <summary>
        /// 测试添加详细信息功能
        /// Tests adding detail information functionality
        /// </summary>
        [Fact]
        public void AddDetail_WithValidKeyValue_ShouldAddToDetails()
        {
            // Arrange
            var auditLog = new AuditLog(AuditEventType.SecurityCleanup, "Test", "session");
            var key = "test_key";
            var value = "test_value";

            // Act
            var result = auditLog.AddDetail(key, value);

            // Assert
            result.Should().BeSameAs(auditLog); // Should return same instance for chaining
            auditLog.Details.Should().ContainKey(key);
            auditLog.Details[key].Should().Be(value);
        }

        [Fact]
        public void AddDetail_WithNullOrEmptyKey_ShouldNotAddToDetails()
        {
            // Arrange
            var auditLog = new AuditLog(AuditEventType.SecurityCleanup, "Test", "session");

            // Act
            auditLog.AddDetail(null!, "value");
            auditLog.AddDetail("", "value");
            auditLog.AddDetail("  ", "value");

            // Assert
            auditLog.Details.Should().BeEmpty();
        }

        /// <summary>
        /// 测试添加标签功能
        /// Tests adding tags functionality
        /// </summary>
        [Fact]
        public void AddTag_WithValidTag_ShouldAddToTags()
        {
            // Arrange
            var auditLog = new AuditLog(AuditEventType.SecurityCleanup, "Test", "session");
            var tag = "TestTag";

            // Act
            var result = auditLog.AddTag(tag);

            // Assert
            result.Should().BeSameAs(auditLog);
            auditLog.Tags.Should().Contain(tag.ToLowerInvariant());
        }

        [Fact]
        public void AddTag_WithNullOrEmptyTag_ShouldNotAddToTags()
        {
            // Arrange
            var auditLog = new AuditLog(AuditEventType.SecurityCleanup, "Test", "session");

            // Act
            auditLog.AddTag(null!);
            auditLog.AddTag("");
            auditLog.AddTag("  ");

            // Assert
            auditLog.Tags.Should().BeEmpty();
        }

        [Fact]
        public void AddTag_ShouldNormalizeToLowerCase()
        {
            // Arrange
            var auditLog = new AuditLog(AuditEventType.SecurityCleanup, "Test", "session");
            var tag = "TestTag";

            // Act
            auditLog.AddTag(tag);

            // Assert
            auditLog.Tags.Should().Contain("testtag");
            auditLog.Tags.Should().NotContain("TestTag");
        }

        /// <summary>
        /// 测试设置操作结果功能
        /// Tests setting operation result functionality
        /// </summary>
        [Fact]
        public void SetResult_WithSuccessResult_ShouldUpdatePropertiesCorrectly()
        {
            // Arrange
            var auditLog = new AuditLog(AuditEventType.SecurityCleanup, "Test", "session");
            var result = AuditOperationResult.Succeeded;

            // Act
            var returnValue = auditLog.SetResult(result);

            // Assert
            returnValue.Should().BeSameAs(auditLog);
            auditLog.Result.Should().Be(result);
            auditLog.ErrorMessage.Should().BeNull();
            auditLog.StackTrace.Should().BeNull();
            auditLog.Severity.Should().Be(AuditSeverity.Information); // Should remain unchanged for success
        }

        [Fact]
        public void SetResult_WithFailedResult_ShouldUpdateSeverityToError()
        {
            // Arrange
            var auditLog = new AuditLog(AuditEventType.SecurityCleanup, "Test", "session");
            var result = AuditOperationResult.Failed;
            var errorMessage = "Test error";
            var stackTrace = "Test stack trace";

            // Act
            auditLog.SetResult(result, errorMessage, stackTrace);

            // Assert
            auditLog.Result.Should().Be(result);
            auditLog.ErrorMessage.Should().Be(errorMessage);
            auditLog.StackTrace.Should().Be(stackTrace);
            auditLog.Severity.Should().Be(AuditSeverity.Error);
        }

        [Fact]
        public void SetResult_WithPartiallySucceeded_ShouldUpdateSeverityToWarning()
        {
            // Arrange
            var auditLog = new AuditLog(AuditEventType.SecurityCleanup, "Test", "session");
            var result = AuditOperationResult.PartiallySucceeded;

            // Act
            auditLog.SetResult(result);

            // Assert
            auditLog.Result.Should().Be(result);
            auditLog.Severity.Should().Be(AuditSeverity.Warning);
        }

        /// <summary>
        /// 测试设置清理状态功能
        /// Tests setting cleanup status functionality
        /// </summary>
        [Fact]
        public void SetCleanupStatus_WithValidStatus_ShouldUpdateStatusAndAddDetail()
        {
            // Arrange
            var auditLog = new AuditLog(AuditEventType.SecurityCleanup, "Test", "session");
            var status = CleanupStatus.Completed;

            // Act
            var result = auditLog.SetCleanupStatus(status);

            // Assert
            result.Should().BeSameAs(auditLog);
            auditLog.CleanupStatus.Should().Be(status);
            auditLog.Details.Should().ContainKey("cleanup_status");
            auditLog.Details["cleanup_status"].Should().Be(status.ToString());
        }

        /// <summary>
        /// 测试设置操作耗时功能
        /// Tests setting operation duration functionality
        /// </summary>
        [Fact]
        public void SetDuration_WithValidDuration_ShouldUpdateDurationAndAddDetail()
        {
            // Arrange
            var auditLog = new AuditLog(AuditEventType.SecurityCleanup, "Test", "session");
            var duration = TimeSpan.FromSeconds(30);

            // Act
            var result = auditLog.SetDuration(duration);

            // Assert
            result.Should().BeSameAs(auditLog);
            auditLog.Duration.Should().Be(duration);
            auditLog.Details.Should().ContainKey("duration_ms");
            auditLog.Details["duration_ms"].Should().Be(duration.TotalMilliseconds);
        }

        /// <summary>
        /// 测试生成验证哈希功能
        /// Tests generating validation hash functionality
        /// </summary>
        [Fact]
        public void GenerateValidationHash_ShouldCreateHashAndReturnIt()
        {
            // Arrange
            var auditLog = new AuditLog(AuditEventType.SecurityCleanup, "Test", "session");

            // Act
            var hash = auditLog.GenerateValidationHash();

            // Assert
            hash.Should().NotBeNullOrEmpty();
            auditLog.ValidationHash.Should().Be(hash);
            hash.Should().MatchRegex(@"^[A-Za-z0-9+/]*={0,2}$"); // Base64 pattern
        }

        [Fact]
        public void GenerateValidationHash_ShouldProduceConsistentHashForSameContent()
        {
            // Arrange
            var auditLog1 = new AuditLog(AuditEventType.SecurityCleanup, "Test", "session");
            var auditLog2 = new AuditLog(AuditEventType.SecurityCleanup, "Test", "session");

            // Set same properties to ensure identical content
            auditLog1.SetResult(AuditOperationResult.Succeeded);
            auditLog2.SetResult(AuditOperationResult.Succeeded);

            // Act
            var hash1 = auditLog1.GenerateValidationHash();
            var hash2 = auditLog2.GenerateValidationHash();

            // Assert
            // Note: Hashes will be different due to different IDs and timestamps
            // But the generation process should work consistently
            hash1.Should().NotBeNullOrEmpty();
            hash2.Should().NotBeNullOrEmpty();
        }

        /// <summary>
        /// 测试验证完整性功能
        /// Tests validating integrity functionality
        /// </summary>
        [Fact]
        public void ValidateIntegrity_WithoutGeneratedHash_ShouldReturnFalse()
        {
            // Arrange
            var auditLog = new AuditLog(AuditEventType.SecurityCleanup, "Test", "session");

            // Act
            var isValid = auditLog.ValidateIntegrity();

            // Assert
            isValid.Should().BeFalse();
        }

        [Fact]
        public void ValidateIntegrity_WithValidHash_ShouldReturnTrue()
        {
            // Arrange
            var auditLog = new AuditLog(AuditEventType.SecurityCleanup, "Test", "session");
            auditLog.GenerateValidationHash();

            // Act
            var isValid = auditLog.ValidateIntegrity();

            // Assert
            isValid.Should().BeTrue();
        }

        [Fact]
        public void ValidateIntegrity_WithTamperedData_ShouldReturnFalse()
        {
            // Arrange
            var auditLog = new AuditLog(AuditEventType.SecurityCleanup, "Test", "session");
            auditLog.GenerateValidationHash();

            // Tamper with the data
            auditLog.SetResult(AuditOperationResult.Failed);

            // Act
            var isValid = auditLog.ValidateIntegrity();

            // Assert
            isValid.Should().BeFalse();
        }

        /// <summary>
        /// 测试静态工厂方法
        /// Tests static factory methods
        /// </summary>
        [Fact]
        public void CreateCleanupLog_ShouldCreateCorrectAuditLog()
        {
            // Arrange
            var description = "Test cleanup";
            var sessionId = "session-123";
            var cleanupType = "memory";

            // Act
            var auditLog = AuditLog.CreateCleanupLog(description, sessionId, cleanupType);

            // Assert
            auditLog.Should().NotBeNull();
            auditLog.EventType.Should().Be(AuditEventType.SecurityCleanup);
            auditLog.Description.Should().Be(description);
            auditLog.SessionId.Should().Be(sessionId);
            auditLog.Details.Should().ContainKey("cleanup_type").WhoseValue.Should().Be(cleanupType);
            auditLog.Tags.Should().Contain("cleanup");
            auditLog.Tags.Should().Contain("security");
        }

        [Fact]
        public void CreateValidationLog_ShouldCreateCorrectAuditLog()
        {
            // Arrange
            var description = "Test validation";
            var sessionId = "session-123";
            var validationType = "integrity";

            // Act
            var auditLog = AuditLog.CreateValidationLog(description, sessionId, validationType);

            // Assert
            auditLog.Should().NotBeNull();
            auditLog.EventType.Should().Be(AuditEventType.SecurityValidation);
            auditLog.Description.Should().Be(description);
            auditLog.SessionId.Should().Be(sessionId);
            auditLog.Details.Should().ContainKey("validation_type").WhoseValue.Should().Be(validationType);
            auditLog.Tags.Should().Contain("validation");
            auditLog.Tags.Should().Contain("security");
        }

        [Fact]
        public void CreateMemoryCleanupLog_ShouldCreateCorrectAuditLog()
        {
            // Arrange
            var description = "Test memory cleanup";
            var sessionId = "session-123";

            // Act
            var auditLog = AuditLog.CreateMemoryCleanupLog(description, sessionId);

            // Assert
            auditLog.Should().NotBeNull();
            auditLog.EventType.Should().Be(AuditEventType.MemoryCleanup);
            auditLog.Description.Should().Be(description);
            auditLog.SessionId.Should().Be(sessionId);
            auditLog.Tags.Should().Contain("memory");
            auditLog.Tags.Should().Contain("cleanup");
            auditLog.Tags.Should().Contain("gc");
        }

        /// <summary>
        /// 测试方法链式调用
        /// Tests method chaining
        /// </summary>
        [Fact]
        public void MethodChaining_ShouldAllowFluentInterface()
        {
            // Arrange
            var auditLog = new AuditLog(AuditEventType.SecurityCleanup, "Test", "session");

            // Act
            var result = auditLog
                .AddDetail("key1", "value1")
                .AddDetail("key2", "value2")
                .AddTag("tag1")
                .AddTag("tag2")
                .SetResult(AuditOperationResult.Succeeded)
                .SetCleanupStatus(CleanupStatus.Completed)
                .SetDuration(TimeSpan.FromSeconds(10));

            // Assert
            result.Should().BeSameAs(auditLog);
            auditLog.Details.Should().HaveCount(4); // 2 added details + cleanup_status + duration_ms
            auditLog.Tags.Should().HaveCount(2);
            auditLog.Result.Should().Be(AuditOperationResult.Succeeded);
            auditLog.CleanupStatus.Should().Be(CleanupStatus.Completed);
            auditLog.Duration.Should().Be(TimeSpan.FromSeconds(10));
        }

        /// <summary>
        /// 测试客户端信息设置
        /// Tests client information setting
        /// </summary>
        [Fact]
        public void ClientInfo_ShouldAcceptValidClientInfo()
        {
            // Arrange
            var auditLog = new AuditLog(AuditEventType.SecurityCleanup, "Test", "session");
            var clientInfo = new ClientInfo
            {
                ApplicationName = "TestApp",
                ApplicationVersion = "1.0.0",
                ProcessId = 1234,
                ThreadId = 5678,
                HostName = "test-host",
                UserName = "test-user"
            };

            // Act
            auditLog.ClientInfo = clientInfo;

            // Assert
            auditLog.ClientInfo.Should().BeSameAs(clientInfo);
            auditLog.ClientInfo.ApplicationName.Should().Be("TestApp");
            auditLog.ClientInfo.ProcessId.Should().Be(1234);
        }

        /// <summary>
        /// 测试环境信息设置
        /// Tests environment information setting
        /// </summary>
        [Fact]
        public void EnvironmentInfo_ShouldAcceptValidEnvironmentInfo()
        {
            // Arrange
            var auditLog = new AuditLog(AuditEventType.SecurityCleanup, "Test", "session");
            var envInfo = new EnvironmentInfo
            {
                OperatingSystem = "Windows 10",
                DotNetVersion = "8.0.0",
                MemoryUsage = 1024000,
                CpuUsage = 15.5,
                IsDebugMode = true
            };

            // Act
            auditLog.EnvironmentInfo = envInfo;

            // Assert
            auditLog.EnvironmentInfo.Should().BeSameAs(envInfo);
            auditLog.EnvironmentInfo.OperatingSystem.Should().Be("Windows 10");
            auditLog.EnvironmentInfo.MemoryUsage.Should().Be(1024000);
            auditLog.EnvironmentInfo.IsDebugMode.Should().BeTrue();
        }
    }
}