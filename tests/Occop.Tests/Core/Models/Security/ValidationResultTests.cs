using FluentAssertions;
using Occop.Core.Models.Security;
using Xunit;

namespace Occop.Tests.Core.Models.Security
{
    /// <summary>
    /// ValidationResult类的单元测试
    /// Unit tests for ValidationResult class
    /// </summary>
    public class ValidationResultTests
    {
        /// <summary>
        /// 测试ValidationResult的基本构造和属性
        /// Tests basic construction and properties of ValidationResult
        /// </summary>
        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
        {
            // Arrange
            var validationType = ValidationType.CleanupValidation;
            var targetResource = "test-resource";
            var sessionId = "session-123";

            // Act
            var validationResult = new ValidationResult(validationType, targetResource, sessionId);

            // Assert
            validationResult.Should().NotBeNull();
            validationResult.Id.Should().NotBeNullOrEmpty();
            validationResult.ValidationType.Should().Be(validationType);
            validationResult.TargetResource.Should().Be(targetResource);
            validationResult.SessionId.Should().Be(sessionId);
            validationResult.StartTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            validationResult.EndTime.Should().Be(validationResult.StartTime);
            validationResult.IsValid.Should().BeFalse(); // Default
            validationResult.Confidence.Should().Be(0.0); // Default
            validationResult.Rules.Should().NotBeNull().And.BeEmpty();
            validationResult.Messages.Should().NotBeNull().And.BeEmpty();
            validationResult.Context.Should().NotBeNull().And.BeEmpty();
            validationResult.Tags.Should().NotBeNull().And.Contain("validation");
            validationResult.Tags.Should().Contain(validationType.ToString().ToLowerInvariant());
        }

        /// <summary>
        /// 测试构造函数参数验证
        /// Tests constructor parameter validation
        /// </summary>
        [Fact]
        public void Constructor_WithNullTargetResource_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Action act = () => new ValidationResult(ValidationType.CleanupValidation, null!, "session");
            act.Should().Throw<ArgumentNullException>().WithParameterName("targetResource");
        }

        [Fact]
        public void Constructor_WithNullSessionId_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Action act = () => new ValidationResult(ValidationType.CleanupValidation, "resource", null!);
            act.Should().Throw<ArgumentNullException>().WithParameterName("sessionId");
        }

        /// <summary>
        /// 测试开始验证功能
        /// Tests starting validation functionality
        /// </summary>
        [Fact]
        public void Start_ShouldUpdateStartAndEndTimes()
        {
            // Arrange
            var validationResult = new ValidationResult(ValidationType.CleanupValidation, "resource", "session");
            var originalStartTime = validationResult.StartTime;

            // Wait a small amount to ensure time difference
            await Task.Delay(10);

            // Act
            var result = validationResult.Start();

            // Assert
            result.Should().BeSameAs(validationResult);
            validationResult.StartTime.Should().BeAfter(originalStartTime);
            validationResult.EndTime.Should().Be(validationResult.StartTime);
        }

        /// <summary>
        /// 测试完成验证功能
        /// Tests completing validation functionality
        /// </summary>
        [Fact]
        public void Complete_WithValidParameters_ShouldUpdateResultAndConfidence()
        {
            // Arrange
            var validationResult = new ValidationResult(ValidationType.CleanupValidation, "resource", "session");
            var isValid = true;
            var confidence = 0.85;

            // Act
            var result = validationResult.Complete(isValid, confidence);

            // Assert
            result.Should().BeSameAs(validationResult);
            validationResult.IsValid.Should().Be(isValid);
            validationResult.Confidence.Should().Be(confidence);
            validationResult.EndTime.Should().BeAfter(validationResult.StartTime);
            validationResult.Duration.Should().BeGreaterOrEqualTo(TimeSpan.Zero);
            validationResult.Checksum.Should().NotBeNullOrEmpty();
        }

        [Theory]
        [InlineData(-0.1, 0.0)] // Below minimum
        [InlineData(1.1, 1.0)]  // Above maximum
        [InlineData(0.5, 0.5)]  // Valid value
        [InlineData(0.0, 0.0)]  // Minimum
        [InlineData(1.0, 1.0)]  // Maximum
        public void Complete_ShouldClampConfidenceValue(double inputConfidence, double expectedConfidence)
        {
            // Arrange
            var validationResult = new ValidationResult(ValidationType.CleanupValidation, "resource", "session");

            // Act
            validationResult.Complete(true, inputConfidence);

            // Assert
            validationResult.Confidence.Should().Be(expectedConfidence);
        }

        /// <summary>
        /// 测试添加验证规则功能
        /// Tests adding validation rules functionality
        /// </summary>
        [Fact]
        public void AddRule_WithValidRule_ShouldAddToRules()
        {
            // Arrange
            var validationResult = new ValidationResult(ValidationType.CleanupValidation, "resource", "session");
            var rule = new ValidationRule("test-rule", "Test rule description");

            // Act
            var result = validationResult.AddRule(rule);

            // Assert
            result.Should().BeSameAs(validationResult);
            validationResult.Rules.Should().Contain(rule);
            validationResult.Rules.Should().HaveCount(1);
        }

        [Fact]
        public void AddRule_WithNullRule_ShouldNotAddToRules()
        {
            // Arrange
            var validationResult = new ValidationResult(ValidationType.CleanupValidation, "resource", "session");

            // Act
            validationResult.AddRule(null!);

            // Assert
            validationResult.Rules.Should().BeEmpty();
        }

        /// <summary>
        /// 测试添加验证消息功能
        /// Tests adding validation messages functionality
        /// </summary>
        [Fact]
        public void AddMessage_WithValidMessage_ShouldAddToMessages()
        {
            // Arrange
            var validationResult = new ValidationResult(ValidationType.CleanupValidation, "resource", "session");
            var message = new ValidationMessage(ValidationMessageType.Information, "Test message");

            // Act
            var result = validationResult.AddMessage(message);

            // Assert
            result.Should().BeSameAs(validationResult);
            validationResult.Messages.Should().Contain(message);
            validationResult.Messages.Should().HaveCount(1);
        }

        [Fact]
        public void AddMessage_WithNullMessage_ShouldNotAddToMessages()
        {
            // Arrange
            var validationResult = new ValidationResult(ValidationType.CleanupValidation, "resource", "session");

            // Act
            validationResult.AddMessage(null!);

            // Assert
            validationResult.Messages.Should().BeEmpty();
        }

        /// <summary>
        /// 测试便捷消息添加方法
        /// Tests convenience message adding methods
        /// </summary>
        [Fact]
        public void AddInfo_ShouldAddInformationMessage()
        {
            // Arrange
            var validationResult = new ValidationResult(ValidationType.CleanupValidation, "resource", "session");
            var message = "Information message";
            var source = "test-source";

            // Act
            var result = validationResult.AddInfo(message, source);

            // Assert
            result.Should().BeSameAs(validationResult);
            validationResult.Messages.Should().HaveCount(1);
            var addedMessage = validationResult.Messages.First();
            addedMessage.Type.Should().Be(ValidationMessageType.Information);
            addedMessage.Message.Should().Be(message);
            addedMessage.Source.Should().Be(source);
        }

        [Fact]
        public void AddWarning_ShouldAddWarningMessage()
        {
            // Arrange
            var validationResult = new ValidationResult(ValidationType.CleanupValidation, "resource", "session");
            var message = "Warning message";

            // Act
            var result = validationResult.AddWarning(message);

            // Assert
            result.Should().BeSameAs(validationResult);
            validationResult.Messages.Should().HaveCount(1);
            var addedMessage = validationResult.Messages.First();
            addedMessage.Type.Should().Be(ValidationMessageType.Warning);
            addedMessage.Message.Should().Be(message);
        }

        [Fact]
        public void AddError_ShouldAddErrorMessage()
        {
            // Arrange
            var validationResult = new ValidationResult(ValidationType.CleanupValidation, "resource", "session");
            var message = "Error message";

            // Act
            var result = validationResult.AddError(message);

            // Assert
            result.Should().BeSameAs(validationResult);
            validationResult.Messages.Should().HaveCount(1);
            var addedMessage = validationResult.Messages.First();
            addedMessage.Type.Should().Be(ValidationMessageType.Error);
            addedMessage.Message.Should().Be(message);
        }

        [Fact]
        public void AddCritical_ShouldAddCriticalMessage()
        {
            // Arrange
            var validationResult = new ValidationResult(ValidationType.CleanupValidation, "resource", "session");
            var message = "Critical message";

            // Act
            var result = validationResult.AddCritical(message);

            // Assert
            result.Should().BeSameAs(validationResult);
            validationResult.Messages.Should().HaveCount(1);
            var addedMessage = validationResult.Messages.First();
            addedMessage.Type.Should().Be(ValidationMessageType.Critical);
            addedMessage.Message.Should().Be(message);
        }

        /// <summary>
        /// 测试添加上下文功能
        /// Tests adding context functionality
        /// </summary>
        [Fact]
        public void AddContext_WithValidKeyValue_ShouldAddToContext()
        {
            // Arrange
            var validationResult = new ValidationResult(ValidationType.CleanupValidation, "resource", "session");
            var key = "test_key";
            var value = "test_value";

            // Act
            var result = validationResult.AddContext(key, value);

            // Assert
            result.Should().BeSameAs(validationResult);
            validationResult.Context.Should().ContainKey(key);
            validationResult.Context[key].Should().Be(value);
        }

        [Fact]
        public void AddContext_WithNullOrEmptyKey_ShouldNotAddToContext()
        {
            // Arrange
            var validationResult = new ValidationResult(ValidationType.CleanupValidation, "resource", "session");

            // Act
            validationResult.AddContext(null!, "value");
            validationResult.AddContext("", "value");
            validationResult.AddContext("  ", "value");

            // Assert
            validationResult.Context.Should().BeEmpty();
        }

        /// <summary>
        /// 测试添加标签功能
        /// Tests adding tags functionality
        /// </summary>
        [Fact]
        public void AddTag_WithValidTag_ShouldAddToTags()
        {
            // Arrange
            var validationResult = new ValidationResult(ValidationType.CleanupValidation, "resource", "session");
            var tag = "TestTag";
            var initialTagCount = validationResult.Tags.Count;

            // Act
            var result = validationResult.AddTag(tag);

            // Assert
            result.Should().BeSameAs(validationResult);
            validationResult.Tags.Should().Contain(tag.ToLowerInvariant());
            validationResult.Tags.Should().HaveCount(initialTagCount + 1);
        }

        [Fact]
        public void AddTag_WithNullOrEmptyTag_ShouldNotAddToTags()
        {
            // Arrange
            var validationResult = new ValidationResult(ValidationType.CleanupValidation, "resource", "session");
            var initialTagCount = validationResult.Tags.Count;

            // Act
            validationResult.AddTag(null!);
            validationResult.AddTag("");
            validationResult.AddTag("  ");

            // Assert
            validationResult.Tags.Should().HaveCount(initialTagCount);
        }

        /// <summary>
        /// 测试校验和生成和验证
        /// Tests checksum generation and validation
        /// </summary>
        [Fact]
        public void Complete_ShouldGenerateChecksum()
        {
            // Arrange
            var validationResult = new ValidationResult(ValidationType.CleanupValidation, "resource", "session");

            // Act
            validationResult.Complete(true);

            // Assert
            validationResult.Checksum.Should().NotBeNullOrEmpty();
            validationResult.Checksum.Should().MatchRegex(@"^[A-Za-z0-9+/]*={0,2}$"); // Base64 pattern
        }

        [Fact]
        public void ValidateIntegrity_WithValidChecksum_ShouldReturnTrue()
        {
            // Arrange
            var validationResult = new ValidationResult(ValidationType.CleanupValidation, "resource", "session");
            validationResult.Complete(true);

            // Act
            var isValid = validationResult.ValidateIntegrity();

            // Assert
            isValid.Should().BeTrue();
        }

        [Fact]
        public void ValidateIntegrity_WithoutChecksum_ShouldReturnFalse()
        {
            // Arrange
            var validationResult = new ValidationResult(ValidationType.CleanupValidation, "resource", "session");

            // Act
            var isValid = validationResult.ValidateIntegrity();

            // Assert
            isValid.Should().BeFalse();
        }

        /// <summary>
        /// 测试转换为SecurityValidationResult
        /// Tests converting to SecurityValidationResult
        /// </summary>
        [Fact]
        public void ToSecurityValidationResult_ShouldConvertCorrectly()
        {
            // Arrange
            var validationResult = new ValidationResult(ValidationType.CleanupValidation, "resource", "session");
            validationResult.AddInfo("Test message")
                           .AddWarning("Warning message")
                           .Complete(true, 0.8);

            // Act
            var securityValidationResult = validationResult.ToSecurityValidationResult();

            // Assert
            securityValidationResult.Should().NotBeNull();
            securityValidationResult.IsValid.Should().Be(validationResult.IsValid);
            securityValidationResult.ValidatedItems.Should().Be(validationResult.ValidatedItems);
            securityValidationResult.IssuesFound.Should().Be(validationResult.IssuesFound);
            securityValidationResult.ValidationMessages.Should().HaveCount(validationResult.Messages.Count);
            securityValidationResult.ValidationTimestamp.Should().Be(validationResult.EndTime);
        }

        /// <summary>
        /// 测试静态工厂方法
        /// Tests static factory methods
        /// </summary>
        [Fact]
        public void CreateCleanupValidation_ShouldCreateCorrectValidationResult()
        {
            // Arrange
            var targetResource = "test-resource";
            var sessionId = "session-123";

            // Act
            var validationResult = ValidationResult.CreateCleanupValidation(targetResource, sessionId);

            // Assert
            validationResult.Should().NotBeNull();
            validationResult.ValidationType.Should().Be(ValidationType.CleanupValidation);
            validationResult.TargetResource.Should().Be(targetResource);
            validationResult.SessionId.Should().Be(sessionId);
            validationResult.Tags.Should().Contain("cleanup");
            validationResult.Tags.Should().Contain("security");
        }

        [Fact]
        public void CreateMemoryValidation_ShouldCreateCorrectValidationResult()
        {
            // Arrange
            var sessionId = "session-123";

            // Act
            var validationResult = ValidationResult.CreateMemoryValidation(sessionId);

            // Assert
            validationResult.Should().NotBeNull();
            validationResult.ValidationType.Should().Be(ValidationType.MemoryLeakValidation);
            validationResult.TargetResource.Should().Be("memory");
            validationResult.SessionId.Should().Be(sessionId);
            validationResult.Tags.Should().Contain("memory");
            validationResult.Tags.Should().Contain("leak-detection");
        }

        [Fact]
        public void CreateIdempotencyValidation_ShouldCreateCorrectValidationResult()
        {
            // Arrange
            var operationId = "op-123";
            var sessionId = "session-123";

            // Act
            var validationResult = ValidationResult.CreateIdempotencyValidation(operationId, sessionId);

            // Assert
            validationResult.Should().NotBeNull();
            validationResult.ValidationType.Should().Be(ValidationType.IdempotencyValidation);
            validationResult.TargetResource.Should().Be(operationId);
            validationResult.SessionId.Should().Be(sessionId);
            validationResult.Tags.Should().Contain("idempotency");
            validationResult.Tags.Should().Contain("operation");
        }

        /// <summary>
        /// 测试方法链式调用
        /// Tests method chaining
        /// </summary>
        [Fact]
        public void MethodChaining_ShouldAllowFluentInterface()
        {
            // Arrange
            var validationResult = new ValidationResult(ValidationType.CleanupValidation, "resource", "session");

            // Act
            var result = validationResult
                .Start()
                .AddContext("key1", "value1")
                .AddContext("key2", "value2")
                .AddTag("tag1")
                .AddTag("tag2")
                .AddInfo("Info message")
                .AddWarning("Warning message")
                .Complete(true, 0.9);

            // Assert
            result.Should().BeSameAs(validationResult);
            validationResult.Context.Should().HaveCount(2);
            validationResult.Tags.Should().Contain("tag1");
            validationResult.Tags.Should().Contain("tag2");
            validationResult.Messages.Should().HaveCount(2);
            validationResult.IsValid.Should().BeTrue();
            validationResult.Confidence.Should().Be(0.9);
        }

        /// <summary>
        /// 测试Duration属性计算
        /// Tests Duration property calculation
        /// </summary>
        [Fact]
        public async Task Duration_ShouldCalculateCorrectly()
        {
            // Arrange
            var validationResult = new ValidationResult(ValidationType.CleanupValidation, "resource", "session");

            // Act
            validationResult.Start();
            await Task.Delay(50); // Wait a bit
            validationResult.Complete(true);

            // Assert
            validationResult.Duration.Should().BeGreaterThan(TimeSpan.Zero);
            validationResult.Duration.Should().BeLessThan(TimeSpan.FromSeconds(1)); // Should be much less than 1 second
        }
    }

    /// <summary>
    /// ValidationRule类的单元测试
    /// Unit tests for ValidationRule class
    /// </summary>
    public class ValidationRuleTests
    {
        /// <summary>
        /// 测试ValidationRule的基本构造和属性
        /// Tests basic construction and properties of ValidationRule
        /// </summary>
        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
        {
            // Arrange
            var name = "test-rule";
            var description = "Test rule description";
            var priority = ValidationRulePriority.High;

            // Act
            var rule = new ValidationRule(name, description, priority);

            // Assert
            rule.Should().NotBeNull();
            rule.Name.Should().Be(name);
            rule.Description.Should().Be(description);
            rule.Priority.Should().Be(priority);
            rule.Result.Should().Be(ValidationRuleResult.NotExecuted);
            rule.ExecutionTime.Should().Be(TimeSpan.Zero);
            rule.ErrorMessage.Should().BeNull();
        }

        [Fact]
        public void Constructor_WithoutPriority_ShouldDefaultToNormal()
        {
            // Arrange
            var name = "test-rule";
            var description = "Test rule description";

            // Act
            var rule = new ValidationRule(name, description);

            // Assert
            rule.Priority.Should().Be(ValidationRulePriority.Normal);
        }

        [Fact]
        public void Constructor_WithNullName_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Action act = () => new ValidationRule(null!, "description");
            act.Should().Throw<ArgumentNullException>().WithParameterName("name");
        }

        [Fact]
        public void Constructor_WithNullDescription_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Action act = () => new ValidationRule("name", null!);
            act.Should().Throw<ArgumentNullException>().WithParameterName("description");
        }
    }

    /// <summary>
    /// ValidationMessage类的单元测试
    /// Unit tests for ValidationMessage class
    /// </summary>
    public class ValidationMessageTests
    {
        /// <summary>
        /// 测试ValidationMessage的基本构造和属性
        /// Tests basic construction and properties of ValidationMessage
        /// </summary>
        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
        {
            // Arrange
            var type = ValidationMessageType.Warning;
            var message = "Test warning message";
            var source = "test-source";

            // Act
            var validationMessage = new ValidationMessage(type, message, source);

            // Assert
            validationMessage.Should().NotBeNull();
            validationMessage.Type.Should().Be(type);
            validationMessage.Message.Should().Be(message);
            validationMessage.Source.Should().Be(source);
            validationMessage.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            validationMessage.RuleName.Should().BeNull();
        }

        [Fact]
        public void Constructor_WithoutSource_ShouldInitializeWithNullSource()
        {
            // Arrange
            var type = ValidationMessageType.Information;
            var message = "Test message";

            // Act
            var validationMessage = new ValidationMessage(type, message);

            // Assert
            validationMessage.Source.Should().BeNull();
        }

        [Fact]
        public void Constructor_WithNullMessage_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Action act = () => new ValidationMessage(ValidationMessageType.Information, null!);
            act.Should().Throw<ArgumentNullException>().WithParameterName("message");
        }
    }

    /// <summary>
    /// SensitiveDataItem类的单元测试
    /// Unit tests for SensitiveDataItem class
    /// </summary>
    public class SensitiveDataItemTests
    {
        /// <summary>
        /// 测试SensitiveDataItem的基本构造和属性
        /// Tests basic construction and properties of SensitiveDataItem
        /// </summary>
        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
        {
            // Arrange
            var dataType = "API_KEY";
            var location = "Environment.Variables";
            var riskLevel = RiskLevel.High;

            // Act
            var sensitiveDataItem = new SensitiveDataItem(dataType, location, riskLevel);

            // Assert
            sensitiveDataItem.Should().NotBeNull();
            sensitiveDataItem.DataType.Should().Be(dataType);
            sensitiveDataItem.Location.Should().Be(location);
            sensitiveDataItem.RiskLevel.Should().Be(riskLevel);
            sensitiveDataItem.IsCleaned.Should().BeFalse();
            sensitiveDataItem.CleanupTimestamp.Should().BeNull();
        }

        [Fact]
        public void Constructor_WithNullDataType_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Action act = () => new SensitiveDataItem(null!, "location", RiskLevel.Medium);
            act.Should().Throw<ArgumentNullException>().WithParameterName("dataType");
        }

        [Fact]
        public void Constructor_WithNullLocation_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Action act = () => new SensitiveDataItem("dataType", null!, RiskLevel.Medium);
            act.Should().Throw<ArgumentNullException>().WithParameterName("location");
        }
    }
}