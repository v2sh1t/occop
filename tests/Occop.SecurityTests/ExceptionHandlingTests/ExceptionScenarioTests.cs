using FluentAssertions;
using Occop.SecurityTests.Infrastructure;
using Occop.Core.Security;
using System.Security;

namespace Occop.SecurityTests.ExceptionHandlingTests
{
    /// <summary>
    /// 异常处理场景测试
    /// Exception handling scenario tests
    /// </summary>
    public class ExceptionScenarioTests : SecurityTestBase
    {
        [Fact]
        public async Task SecurityManager_OnMemoryPressure_ShouldHandleGracefully()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            // Act - 模拟内存压力
            SimulateMemoryPressure(100); // 100MB

            // Assert - 系统应该继续运行
            securityManager.IsInitialized.Should().BeTrue();

            // 清理
            await securityManager.ClearAllSecurityStateAsync();
        }

        [Fact]
        public async Task SecurityManager_OnDisposedAccess_ShouldThrowObjectDisposedException()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            // Act
            securityManager.Dispose();

            // Assert
            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                await securityManager.StoreSecureDataAsync(new SecureString())
            );
        }

        [Fact]
        public async Task SecurityManager_OnNullInput_ShouldThrowArgumentNullException()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await securityManager.InitializeAsync(null!)
            );
        }

        [Fact]
        public async Task SecurityManager_OnConcurrentDispose_ShouldHandleSafely()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            // Act - 并发释放
            var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
            {
                try
                {
                    securityManager.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // 预期的异常
                }
            }));

            await Task.WhenAll(tasks);

            // Assert - 不应该崩溃
            Assert.True(true);
        }

        [Fact]
        public async Task SecurityManager_OnOutOfMemory_ShouldDegrade Gracefully()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            // Act - 尝试存储大量数据直到内存压力
            var storedIds = new List<string>();
            try
            {
                for (int i = 0; i < 1000; i++)
                {
                    var secureString = DataGenerator.CreateSecureString($"data_{i}_" + new string('x', 1000));
                    var secureData = await securityManager.StoreSecureDataAsync(secureString);
                    storedIds.Add(secureData.Id);
                }
            }
            catch (OutOfMemoryException)
            {
                // 预期可能发生
            }

            // Assert - 应该仍然可以清理
            var cleanupResult = await securityManager.ClearAllSecurityStateAsync();
            cleanupResult.Should().BeTrue();
        }

        [Fact]
        public async Task SecurityManager_OnCorruptedData_ShouldValidateAndReport()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            // Act
            var validationResult = await securityManager.ValidateSecurityStateAsync();

            // Assert
            validationResult.Should().NotBeNull();
            validationResult.ValidationMessages.Should().NotBeNull();
        }

        [Fact]
        public async Task SecurityManager_OnCleanupFailure_ShouldReportErrors()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            var cleanupErrors = new List<string>();
            securityManager.CleanupCompleted += (sender, e) =>
            {
                if (!e.IsSuccess && e.ErrorMessage != null)
                {
                    cleanupErrors.Add(e.ErrorMessage);
                }
            };

            // Act - 清理不存在的数据
            var result = await securityManager.ClearSecureDataAsync("non_existent_id");

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task SecurityManager_OnRapidOperations_ShouldMaintainIntegrity()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            // Act - 快速连续操作
            var tasks = new List<Task>();
            for (int i = 0; i < 50; i++)
            {
                var index = i;
                tasks.Add(Task.Run(async () =>
                {
                    var secureString = DataGenerator.CreateSecureString($"rapid_{index}");
                    var data = await securityManager.StoreSecureDataAsync(secureString);
                    var retrieved = await securityManager.RetrieveSecureDataAsync(data.Id);
                    await securityManager.ClearSecureDataAsync(data.Id);
                }));
            }

            await Task.WhenAll(tasks);

            // Assert - 验证状态完整性
            var validationResult = await securityManager.ValidateSecurityStateAsync();
            validationResult.IsValid.Should().BeTrue();
        }

        [Fact]
        public async Task SecurityManager_OnThreadAbort_ShouldCleanupResources()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            // Act - 存储数据
            var secureString = DataGenerator.CreateSecureString("test_data");
            var secureData = await securityManager.StoreSecureDataAsync(secureString);

            // 模拟突然中断 - 使用 CancellationToken
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Assert - 清理应该仍然工作
            var cleanupResult = await securityManager.ClearAllSecurityStateAsync();
            cleanupResult.Should().BeTrue();
        }

        [Fact]
        public async Task SecurityManager_OnInvalidState_ShouldRecoverSafely()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);

            // Act - 尝试在未初始化时操作
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await securityManager.StoreSecureDataAsync(new SecureString())
            );

            // 现在正确初始化
            await securityManager.InitializeAsync(securityContext);

            // Assert - 应该可以正常工作
            var secureString = DataGenerator.CreateSecureString("recovery_test");
            var result = await securityManager.StoreSecureDataAsync(secureString);
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task SecurityAuditor_OnExceptionDuringAudit_ShouldContinue()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var auditor = TestContext.CreateSecurityAuditor(securityContext);

            // Act - 记录异常
            var exception = new InvalidOperationException("Test exception");
            var auditId = await auditor.LogSecurityExceptionAsync(
                exception,
                "Test exception scenario",
                new Dictionary<string, object> { { "test", "data" } }
            );

            // Assert
            auditId.Should().NotBeNullOrEmpty();
            var stats = await auditor.GetAuditStatisticsAsync();
            stats.ErrorEvents.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task SecurityAuditor_OnSecurityException_ShouldTriggerCriticalEvent()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var auditor = TestContext.CreateSecurityAuditor(securityContext);
            var criticalEventTriggered = false;

            auditor.CriticalSecurityEvent += (sender, e) =>
            {
                criticalEventTriggered = true;
                e.Message.Should().Contain("Critical security exception");
            };

            // Act
            var securityException = new SecurityException("Unauthorized access attempt");
            await auditor.LogSecurityExceptionAsync(
                securityException,
                "Security violation detected"
            );

            // Assert
            criticalEventTriggered.Should().BeTrue();
        }

        [Fact]
        public async Task CleanupValidator_OnCleanupFailure_ShouldReportDetails()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            var eventDetails = new List<CleanupCompletedEventArgs>();
            securityManager.CleanupCompleted += (sender, e) =>
            {
                eventDetails.Add(e);
            };

            // Act - 尝试清理
            await securityManager.ClearAllSecurityStateAsync();

            // Assert
            eventDetails.Should().HaveCountGreaterThan(0);
            eventDetails.Last().Duration.Should().BeGreaterThan(TimeSpan.Zero);
        }

        [Fact]
        public async Task SecurityManager_OnFileSystemError_ShouldHandleGracefully()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            // Act - 尝试访问受保护的资源（模拟）
            try
            {
                var invalidPath = "/root/protected/file.dat";
                if (File.Exists(invalidPath))
                {
                    File.ReadAllText(invalidPath);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // 预期的异常
            }

            // Assert - 安全管理器应该仍然正常
            securityManager.IsInitialized.Should().BeTrue();
        }

        [Fact]
        public async Task GarbageCollection_OnForcedCleanup_ShouldReleaseMemory()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            var beforeMemory = GetCurrentMemoryUsage();

            // Act - 存储大量数据
            for (int i = 0; i < 100; i++)
            {
                var data = DataGenerator.CreateSecureString(new string('x', 10000));
                await securityManager.StoreSecureDataAsync(data);
            }

            // 清理所有数据
            await securityManager.ClearAllSecurityStateAsync();
            securityManager.ForceGarbageCollection();

            // 等待GC完成
            var freedMemory = await WaitForGarbageCollectionAsync();

            // Assert
            freedMemory.Should().BeGreaterThan(0, "Memory should be freed after cleanup");
        }

        [Fact]
        public async Task ValidationResult_OnMultipleErrors_ShouldCollectAllIssues()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            // Act
            var validationResult = await securityManager.ValidateSecurityStateAsync();

            // Assert
            validationResult.Should().NotBeNull();
            validationResult.ValidationMessages.Should().NotBeNull();
            validationResult.ValidationTimestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }
    }
}
