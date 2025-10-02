using FluentAssertions;
using Occop.SecurityTests.Infrastructure;
using Occop.Core.Security;
using Occop.Core.Logging;

namespace Occop.SecurityTests.SensitiveDataTests
{
    /// <summary>
    /// 敏感数据扫描器测试
    /// Sensitive data scanner tests
    /// </summary>
    public class SensitiveDataScannerTests : SecurityTestBase
    {
        [Fact]
        public void ScanText_WithApiKey_ShouldDetectSensitiveData()
        {
            // Arrange
            var text = "API_KEY=sk_test_1234567890abcdefghijklmnop";

            // Act
            var result = Scanner.ScanText(text, "api_config");

            // Assert
            result.Should().NotBeNull();
            result.ContainsSensitiveData.Should().BeTrue();
            result.Findings.Should().HaveCountGreaterThan(0);
            result.CriticalCount.Should().BeGreaterThan(0);
            result.Findings.Should().Contain(f => f.Type == "ApiKey");
        }

        [Fact]
        public void ScanText_WithPassword_ShouldDetectSensitiveData()
        {
            // Arrange
            var text = "password=MySecretPassword123";

            // Act
            var result = Scanner.ScanText(text, "config");

            // Assert
            result.ContainsSensitiveData.Should().BeTrue();
            result.CriticalCount.Should().BeGreaterThan(0);
            result.Findings.Should().Contain(f => f.Type == "Password");
        }

        [Fact]
        public void ScanText_WithCreditCard_ShouldDetectSensitiveData()
        {
            // Arrange
            var text = "Card Number: 4532-1234-5678-9010";

            // Act
            var result = Scanner.ScanText(text, "payment");

            // Assert
            result.ContainsSensitiveData.Should().BeTrue();
            result.HighCount.Should().BeGreaterThan(0);
            result.Findings.Should().Contain(f => f.Type == "CreditCard");
        }

        [Fact]
        public void ScanText_WithJWT_ShouldDetectSensitiveData()
        {
            // Arrange
            var jwt = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";
            var text = $"Authorization: Bearer {jwt}";

            // Act
            var result = Scanner.ScanText(text, "auth_header");

            // Assert
            result.ContainsSensitiveData.Should().BeTrue();
            result.CriticalCount.Should().BeGreaterThan(0);
            result.Findings.Should().Contain(f => f.Type == "JWT" || f.Type == "BearerToken");
        }

        [Fact]
        public void ScanText_WithConnectionString_ShouldDetectSensitiveData()
        {
            // Arrange
            var text = "Server=myserver;Database=mydb;User Id=admin;Password=SuperSecret123;";

            // Act
            var result = Scanner.ScanText(text, "connection_string");

            // Assert
            result.ContainsSensitiveData.Should().BeTrue();
            result.CriticalCount.Should().BeGreaterThan(0);
            result.Findings.Should().Contain(f => f.Type == "ConnectionString");
        }

        [Fact]
        public void ScanText_WithEmail_ShouldDetectSensitiveData()
        {
            // Arrange
            var text = "User email: john.doe@example.com";

            // Act
            var result = Scanner.ScanText(text, "user_info");

            // Assert
            result.ContainsSensitiveData.Should().BeTrue();
            result.MediumCount.Should().BeGreaterThan(0);
            result.Findings.Should().Contain(f => f.Type == "Email");
        }

        [Fact]
        public void ScanText_WithSSN_ShouldDetectSensitiveData()
        {
            // Arrange
            var text = "SSN: 123-45-6789";

            // Act
            var result = Scanner.ScanText(text, "personal_info");

            // Assert
            result.ContainsSensitiveData.Should().BeTrue();
            result.HighCount.Should().BeGreaterThan(0);
            result.Findings.Should().Contain(f => f.Type == "SSN");
        }

        [Fact]
        public void ScanText_WithPrivateKey_ShouldDetectSensitiveData()
        {
            // Arrange
            var privateKey = @"-----BEGIN RSA PRIVATE KEY-----
MIICXAIBAAKBgQC8kGa1pSjbSYZVebtTRBLxBz5H4i2p/llLCrEeQhta5kaQu/Rn
vWmONjn9W4klBaYhNsGCnIr5AaIIRk3cNBvkWaLmLb0D8P6KZ/DT1hQnXOz6Yb8j
6jq5J9cPxKG2YrjkP2aV8i9KJkxBxDZ1LMXMPf2tN6c9qrJL9I8KBgQDYl2fQqJc
-----END RSA PRIVATE KEY-----";
            var text = $"Private Key:\n{privateKey}";

            // Act
            var result = Scanner.ScanText(text, "crypto");

            // Assert
            result.ContainsSensitiveData.Should().BeTrue();
            result.CriticalCount.Should().BeGreaterThan(0);
            result.Findings.Should().Contain(f => f.Type == "PrivateKey");
        }

        [Fact]
        public void ScanText_WithCleanText_ShouldNotDetectSensitiveData()
        {
            // Arrange
            var text = "This is a normal log message with no sensitive information.";

            // Act
            var result = Scanner.ScanText(text, "clean_log");

            // Assert
            result.ContainsSensitiveData.Should().BeFalse();
            result.Findings.Should().BeEmpty();
            result.CriticalCount.Should().Be(0);
        }

        [Fact]
        public async Task ScanFileAsync_WithSensitiveContent_ShouldDetectSensitiveData()
        {
            // Arrange
            var content = "API_KEY=secret123\nPASSWORD=mypass";
            var tempFile = CreateTempFile(content);

            try
            {
                // Act
                var result = await Scanner.ScanFileAsync(tempFile);

                // Assert
                result.ContainsSensitiveData.Should().BeTrue();
                result.Findings.Should().HaveCountGreaterThan(0);
            }
            finally
            {
                DeleteTempFile(tempFile);
            }
        }

        [Fact]
        public async Task ScanFileAsync_WithNonExistentFile_ShouldThrowException()
        {
            // Arrange
            var nonExistentFile = "/path/to/nonexistent/file.txt";

            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(
                async () => await Scanner.ScanFileAsync(nonExistentFile)
            );
        }

        [Fact]
        public void ScanObject_WithSensitiveProperties_ShouldDetectSensitiveData()
        {
            // Arrange
            var obj = new
            {
                Username = "admin",
                Password = "secret123",
                ApiKey = "sk_live_123456789"
            };

            // Act
            var result = Scanner.ScanObject(obj, "config_object");

            // Assert
            result.ContainsSensitiveData.Should().BeTrue();
            result.Findings.Should().HaveCountGreaterThan(0);
        }

        [Fact]
        public void ScanBatch_WithMultipleTexts_ShouldReturnAllResults()
        {
            // Arrange
            var texts = new Dictionary<string, string>
            {
                { "log1", "API_KEY=secret" },
                { "log2", "This is clean" },
                { "log3", "password=test123" }
            };

            // Act
            var results = Scanner.ScanBatch(texts);

            // Assert
            results.Should().HaveCount(3);
            results.Count(r => r.ContainsSensitiveData).Should().Be(2);
        }

        [Fact]
        public void GenerateReport_WithMultipleResults_ShouldCreateCorrectReport()
        {
            // Arrange
            var results = new List<ScanResult>
            {
                Scanner.ScanText("API_KEY=secret", "source1"),
                Scanner.ScanText("Clean text", "source2"),
                Scanner.ScanText("password=test", "source3")
            };

            // Act
            var report = Scanner.GenerateReport(results);

            // Assert
            report.TotalScans.Should().Be(3);
            report.FilesWithSensitiveData.Should().Be(2);
            report.TotalFindings.Should().BeGreaterThan(0);
            report.IsClean.Should().BeFalse();
        }

        [Fact]
        public void AddPattern_WithCustomPattern_ShouldDetectCustomSensitiveData()
        {
            // Arrange
            var customScanner = new SensitiveDataScanner();
            customScanner.AddPattern("CustomToken", @"CUSTOM_TOKEN_[A-Z0-9]{10}", SensitivityLevel.Critical);
            var text = "Token: CUSTOM_TOKEN_ABC1234567";

            // Act
            var result = customScanner.ScanText(text, "custom");

            // Assert
            result.ContainsSensitiveData.Should().BeTrue();
            result.Findings.Should().Contain(f => f.Type == "CustomToken");
        }

        [Fact]
        public void ScanText_WithContext_ShouldIncludeContextInFindings()
        {
            // Arrange
            var text = "Some text before API_KEY=secret123 and some text after";

            // Act
            var result = Scanner.ScanText(text, "with_context");

            // Assert
            result.Findings.Should().HaveCountGreaterThan(0);
            result.Findings.First().Context.Should().NotBeNullOrEmpty();
            result.Findings.First().Context.Should().Contain("before");
            result.Findings.First().Context.Should().Contain("after");
        }

        [Fact]
        public void ScanText_ShouldMaskSensitiveValues()
        {
            // Arrange
            var text = "password=VerySecretPassword123";

            // Act
            var result = Scanner.ScanText(text, "masked");

            // Assert
            result.Findings.Should().HaveCountGreaterThan(0);
            result.Findings.First().MaskedValue.Should().NotContain("VerySecretPassword123");
            result.Findings.First().MaskedValue.Should().Contain("***");
        }
    }
}
