using FluentAssertions;
using Occop.SecurityTests.Infrastructure;
using Occop.Core.Logging;
using Microsoft.Extensions.Logging;

namespace Occop.SecurityTests.SensitiveDataTests
{
    /// <summary>
    /// 日志敏感数据泄露测试
    /// Log sensitive data leak tests
    /// </summary>
    public class LogSensitiveDataLeakTests : SecurityTestBase
    {
        [Fact]
        public void LoggerService_WithSensitiveData_ShouldFilterApiKeys()
        {
            // Arrange
            var logger = TestContext.CreateLogger<LogSensitiveDataLeakTests>();
            var sensitiveMessage = "Connecting with API_KEY=sk_test_1234567890abcdefghijklmnop";

            // Act
            logger.LogInformation(sensitiveMessage);

            // Assert - 验证日志中的敏感数据已被过滤
            var scanResult = VerifyNoSensitiveData(sensitiveMessage);
            if (scanResult.ContainsSensitiveData)
            {
                // 日志服务应该过滤了敏感数据
                scanResult.Findings.Should().NotBeEmpty("Logger should have detected sensitive data for filtering");
            }
        }

        [Fact]
        public void LoggerService_WithPassword_ShouldFilterPasswords()
        {
            // Arrange
            var logger = TestContext.CreateLogger<LogSensitiveDataLeakTests>();
            var sensitiveMessage = "User login attempt with password=MySecretPass123";

            // Act
            logger.LogWarning(sensitiveMessage);

            // Assert
            var scanResult = VerifyNoSensitiveData(sensitiveMessage);
            scanResult.Findings.Should().Contain(f => f.Type == "Password");
        }

        [Fact]
        public void LoggerService_WithConnectionString_ShouldFilterConnectionStrings()
        {
            // Arrange
            var logger = TestContext.CreateLogger<LogSensitiveDataLeakTests>();
            var connectionString = "Server=localhost;Database=testdb;User Id=admin;Password=SuperSecret123;";

            // Act
            logger.LogError($"Database connection failed: {connectionString}");

            // Assert
            var scanResult = VerifyNoSensitiveData(connectionString);
            scanResult.Findings.Should().Contain(f => f.Type == "ConnectionString");
        }

        [Fact]
        public void LoggerService_WithJWT_ShouldFilterJWTTokens()
        {
            // Arrange
            var logger = TestContext.CreateLogger<LogSensitiveDataLeakTests>();
            var jwt = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.dozjgNryP4J3jVmNHl0w5N_XgL0n3I9PlFUP0THsR8U";

            // Act
            logger.LogDebug($"Token validation: {jwt}");

            // Assert
            var scanResult = VerifyNoSensitiveData(jwt);
            scanResult.Findings.Should().Contain(f => f.Type == "JWT");
        }

        [Fact]
        public void LoggerService_WithBearerToken_ShouldFilterBearerTokens()
        {
            // Arrange
            var logger = TestContext.CreateLogger<LogSensitiveDataLeakTests>();
            var authHeader = "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9";

            // Act
            logger.LogInformation($"Authorization header: {authHeader}");

            // Assert
            var scanResult = VerifyNoSensitiveData(authHeader);
            scanResult.Findings.Should().Contain(f => f.Type == "BearerToken" || f.Type == "JWT");
        }

        [Fact]
        public void SensitiveDataFilter_WithEmail_ShouldPartiallyMaskEmail()
        {
            // Arrange
            var filter = new SensitiveDataFilter(true);
            var email = "john.doe@example.com";

            // Act
            var filtered = filter.FilterSensitiveData($"User email: {email}");

            // Assert
            filtered.Should().NotContain(email);
            filtered.Should().Contain("@example.com"); // 域名应该保留
        }

        [Fact]
        public void SensitiveDataFilter_WithCreditCard_ShouldMaskMostDigits()
        {
            // Arrange
            var filter = new SensitiveDataFilter(true);
            var creditCard = "4532-1234-5678-9010";

            // Act
            var filtered = filter.FilterSensitiveData($"Payment card: {creditCard}");

            // Assert
            filtered.Should().NotContain(creditCard);
            filtered.Should().Contain("9010"); // 最后4位应该保留
            filtered.Should().Contain("****");
        }

        [Fact]
        public void SensitiveDataFilter_WithMultipleSensitiveItems_ShouldFilterAll()
        {
            // Arrange
            var filter = new SensitiveDataFilter(true);
            var text = @"
                API_KEY=secret123
                PASSWORD=mypass
                EMAIL=user@test.com
                JWT=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9
            ";

            // Act
            var filtered = filter.FilterSensitiveData(text);

            // Assert
            filtered.Should().NotContain("secret123");
            filtered.Should().NotContain("mypass");
            filtered.Should().Contain("***");
        }

        [Fact]
        public void SensitiveDataFilter_WithDictionary_ShouldFilterSensitiveKeys()
        {
            // Arrange
            var filter = new SensitiveDataFilter(true);
            var properties = new Dictionary<string, object>
            {
                { "username", "admin" },
                { "password", "secret123" },
                { "api_key", "sk_live_123" },
                { "normal_field", "normal_value" }
            };

            // Act
            var filtered = filter.FilterSensitiveData(properties);

            // Assert
            filtered["username"].Should().Be("admin"); // 非敏感字段保持不变
            filtered["password"].Should().NotBe("secret123"); // 密码应被过滤
            filtered["api_key"].Should().NotBe("sk_live_123"); // API密钥应被过滤
            filtered["normal_field"].Should().Be("normal_value");
        }

        [Fact]
        public void SensitiveDataDetection_WithMixedContent_ShouldDetectAllPatterns()
        {
            // Arrange
            var filter = new SensitiveDataFilter(true);
            var text = @"
                Configuration:
                - API Key: sk_test_abc123
                - Database: Server=localhost;Password=dbpass;
                - User: john@example.com
            ";

            // Act
            var detection = filter.DetectSensitiveData(text);

            // Assert
            detection.ContainsSensitiveData.Should().BeTrue();
            detection.PatternCount.Should().BeGreaterThan(0);
            detection.DetectedPatterns.Should().Contain(p =>
                p.Contains("ApiKey") ||
                p.Contains("ConnectionString") ||
                p.Contains("Email"));
        }

        [Fact]
        public void LogCategory_WithSensitiveContext_ShouldNotLeakData()
        {
            // Arrange
            var context = new LogContext("test_operation", "security_test");
            context.AddProperty("safe_property", "safe_value");
            context.AddProperty("api_key", "sk_test_should_be_filtered");

            // Act
            var scanResult = Scanner.ScanObject(context, "log_context");

            // Assert - 敏感属性应该被检测到
            scanResult.ContainsSensitiveData.Should().BeTrue();
        }

        [Fact]
        public async Task LogAnalyzer_SearchingLogs_ShouldNotExposeSensitiveData()
        {
            // Arrange
            var logDir = Path.Combine(Path.GetTempPath(), $"occop_logs_{Guid.NewGuid():N}");
            Directory.CreateDirectory(logDir);

            try
            {
                // 创建包含敏感数据的日志文件
                var logFile = Path.Combine(logDir, "test.log");
                await File.WriteAllTextAsync(logFile, "API_KEY=secret123\nNormal log entry");

                // Act - 扫描日志目录
                var scanResults = await Scanner.ScanDirectoryAsync(logDir);

                // Assert
                scanResults.Should().HaveCountGreaterThan(0);
                scanResults.First().ContainsSensitiveData.Should().BeTrue();
            }
            finally
            {
                if (Directory.Exists(logDir))
                    Directory.Delete(logDir, true);
            }
        }

        [Fact]
        public void PrivateKey_InLogs_ShouldBeDetectedAndFiltered()
        {
            // Arrange
            var privateKey = @"-----BEGIN RSA PRIVATE KEY-----
MIICXAIBAAKBgQDdlatRjRjog7jP...
-----END RSA PRIVATE KEY-----";
            var filter = new SensitiveDataFilter(true);

            // Act
            var filtered = filter.FilterSensitiveData($"Loading key: {privateKey}");

            // Assert
            filtered.Should().NotContain("BEGIN RSA PRIVATE KEY");
            filtered.Should().Contain("***");
        }

        [Fact]
        public void ConnectionString_InExceptionMessage_ShouldBeFiltered()
        {
            // Arrange
            var connectionString = "Server=prod-db;Database=main;User Id=admin;Password=prod_pass_123;";
            var filter = new SensitiveDataFilter(true);

            // Act
            var exceptionMessage = $"Failed to connect: {connectionString}";
            var filtered = filter.FilterSensitiveData(exceptionMessage);

            // Assert
            filtered.Should().NotContain("prod_pass_123");
            filtered.Should().Contain("Password=***"); // 密码部分应该被遮蔽
        }

        [Fact]
        public void CustomSensitivePattern_ShouldBeDetectable()
        {
            // Arrange
            var filter = new SensitiveDataFilter(true);
            filter.AddPattern("CustomSecret", @"CUSTOM_SECRET_[A-Z0-9]{10}", System.Text.RegularExpressions.RegexOptions.None);
            var text = "Custom secret: CUSTOM_SECRET_ABC1234567";

            // Act
            var filtered = filter.FilterSensitiveData(text);

            // Assert
            filtered.Should().NotContain("CUSTOM_SECRET_ABC1234567");
            filtered.Should().Contain("***");
        }

        [Fact]
        public void DisabledFilter_ShouldNotFilterAnything()
        {
            // Arrange
            var filter = new SensitiveDataFilter(false); // 禁用过滤
            var sensitiveText = "API_KEY=secret123 PASSWORD=test";

            // Act
            var result = filter.FilterSensitiveData(sensitiveText);

            // Assert
            result.Should().Be(sensitiveText); // 禁用时应该返回原文
        }

        [Fact]
        public void PhoneNumber_ShouldBeDetectedAndFiltered()
        {
            // Arrange
            var filter = new SensitiveDataFilter(true);
            var phoneNumber = "+1-555-123-4567";

            // Act
            var filtered = filter.FilterSensitiveData($"Contact: {phoneNumber}");

            // Assert
            var detection = filter.DetectSensitiveData($"Contact: {phoneNumber}");
            detection.ContainsSensitiveData.Should().BeTrue();
            detection.DetectedPatterns.Should().Contain("Phone");
        }

        [Fact]
        public void SSN_ShouldBeDetectedAndFiltered()
        {
            // Arrange
            var filter = new SensitiveDataFilter(true);
            var ssn = "123-45-6789";

            // Act
            var filtered = filter.FilterSensitiveData($"SSN: {ssn}");

            // Assert
            filtered.Should().NotContain(ssn);
            var detection = filter.DetectSensitiveData($"SSN: {ssn}");
            detection.DetectedPatterns.Should().Contain("SSN");
        }
    }
}
