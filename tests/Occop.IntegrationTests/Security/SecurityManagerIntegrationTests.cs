using Xunit;
using FluentAssertions;
using FluentAssertions.Execution;
using Occop.IntegrationTests.Infrastructure;
using Occop.Core.Security;

namespace Occop.IntegrationTests.Security
{
    /// <summary>
    /// 安全管理器集成测试
    /// Integration tests for security manager
    /// </summary>
    public class SecurityManagerIntegrationTests : IntegrationTestBase
    {
        [Fact]
        public async Task SecurityManager_ShouldInitializeSuccessfully()
        {
            // Arrange
            var securityManager = await Helper.InitializeSecurityManagerAsync();

            // Assert
            using (new AssertionScope())
            {
                securityManager.IsInitialized.Should().BeTrue("安全管理器应该已初始化");
                securityManager.SecurityContext.Should().NotBeNull("安全上下文应该存在");
            }

            Logger.LogInformation("✓ 安全管理器初始化验证通过");
        }

        [Fact]
        public async Task StoreAndRetrieveSecureData_ShouldWorkCorrectly()
        {
            // Arrange
            var securityManager = await Helper.InitializeSecurityManagerAsync();
            var testData = DataGenerator.GenerateSecureString();

            // Act
            var storedData = await Helper.StoreSecureDataAsync(securityManager, testData);
            var retrievedData = await Helper.RetrieveSecureDataAsync(securityManager, storedData.Id);

            // Assert
            using (new AssertionScope())
            {
                storedData.Should().NotBeNull("存储的数据不应为空");
                storedData.Id.Should().NotBeNullOrEmpty("数据ID不应为空");
                retrievedData.Should().NotBeNull("应该能够检索数据");
                retrievedData!.Length.Should().Be(testData.Length, "检索的数据长度应该匹配");
            }

            Logger.LogInformation("✓ 存储和检索安全数据验证通过");

            // Cleanup
            testData.Dispose();
            retrievedData?.Dispose();
        }

        [Fact]
        public async Task StoreMultipleSecureData_ShouldMaintainSeparateInstances()
        {
            // Arrange
            var securityManager = await Helper.InitializeSecurityManagerAsync();

            // Act
            var data1 = await Helper.StoreSecureDataAsync(securityManager);
            var data2 = await Helper.StoreSecureDataAsync(securityManager);
            var data3 = await Helper.StoreSecureDataAsync(securityManager);

            // Assert
            using (new AssertionScope())
            {
                data1.Id.Should().NotBe(data2.Id, "每个数据应该有唯一ID");
                data2.Id.Should().NotBe(data3.Id, "每个数据应该有唯一ID");
                data1.Id.Should().NotBe(data3.Id, "每个数据应该有唯一ID");
            }

            // 验证所有数据都可以独立检索
            var retrieved1 = await Helper.RetrieveSecureDataAsync(securityManager, data1.Id);
            var retrieved2 = await Helper.RetrieveSecureDataAsync(securityManager, data2.Id);
            var retrieved3 = await Helper.RetrieveSecureDataAsync(securityManager, data3.Id);

            using (new AssertionScope())
            {
                retrieved1.Should().NotBeNull("数据1应该能检索");
                retrieved2.Should().NotBeNull("数据2应该能检索");
                retrieved3.Should().NotBeNull("数据3应该能检索");
            }

            Logger.LogInformation("✓ 多个安全数据独立性验证通过");

            // Cleanup
            retrieved1?.Dispose();
            retrieved2?.Dispose();
            retrieved3?.Dispose();
        }

        [Fact]
        public async Task ClearSecureData_ShouldRemoveSpecificData()
        {
            // Arrange
            var securityManager = await Helper.InitializeSecurityManagerAsync();
            var data1 = await Helper.StoreSecureDataAsync(securityManager);
            var data2 = await Helper.StoreSecureDataAsync(securityManager);

            // Act - 清理第一个数据
            var cleared = await securityManager.ClearSecureDataAsync(data1.Id);

            // Assert
            cleared.Should().BeTrue("清理操作应该成功");

            var retrieved1 = await Helper.RetrieveSecureDataAsync(securityManager, data1.Id);
            var retrieved2 = await Helper.RetrieveSecureDataAsync(securityManager, data2.Id);

            using (new AssertionScope())
            {
                retrieved1.Should().BeNull("已清理的数据应该无法检索");
                retrieved2.Should().NotBeNull("未清理的数据应该仍可检索");
            }

            Logger.LogInformation("✓ 清理特定安全数据验证通过");

            // Cleanup
            retrieved2?.Dispose();
        }

        [Fact]
        public async Task ClearAllSecurityState_ShouldRemoveAllData()
        {
            // Arrange
            var securityManager = await Helper.InitializeSecurityManagerAsync();

            var data1 = await Helper.StoreSecureDataAsync(securityManager);
            var data2 = await Helper.StoreSecureDataAsync(securityManager);
            var data3 = await Helper.StoreSecureDataAsync(securityManager);

            // Act
            var cleared = await securityManager.ClearAllSecurityStateAsync();

            // Assert
            cleared.Should().BeTrue("清理所有状态应该成功");

            var retrieved1 = await Helper.RetrieveSecureDataAsync(securityManager, data1.Id);
            var retrieved2 = await Helper.RetrieveSecureDataAsync(securityManager, data2.Id);
            var retrieved3 = await Helper.RetrieveSecureDataAsync(securityManager, data3.Id);

            using (new AssertionScope())
            {
                retrieved1.Should().BeNull("所有数据都应该被清理");
                retrieved2.Should().BeNull("所有数据都应该被清理");
                retrieved3.Should().BeNull("所有数据都应该被清理");
            }

            Logger.LogInformation("✓ 清理所有安全状态验证通过");
        }

        [Fact]
        public async Task ValidateSecurityState_AfterInitialization_ShouldBeValid()
        {
            // Arrange
            var securityManager = await Helper.InitializeSecurityManagerAsync();

            // Act & Assert
            await Helper.ValidateSecurityStateAsync(securityManager, shouldBeValid: true);

            Logger.LogInformation("✓ 安全状态验证通过");
        }

        [Fact]
        public async Task GetSecurityStateSummary_ShouldReturnValidSummary()
        {
            // Arrange
            var securityManager = await Helper.InitializeSecurityManagerAsync();
            await Helper.StoreSecureDataAsync(securityManager);
            await Helper.StoreSecureDataAsync(securityManager);

            // Act
            var summary = securityManager.GetSecurityStateSummary();

            // Assert
            using (new AssertionScope())
            {
                summary.Should().NotBeNull("状态摘要不应为空");
                summary.SecureDataItemCount.Should().BeGreaterThanOrEqualTo(0, "数据项数量应该有效");
                summary.StartupTime.Should().BeAfter(DateTime.MinValue, "启动时间应该有效");
                summary.TotalCleanupOperations.Should().BeGreaterThanOrEqualTo(0, "清理操作计数应该有效");
            }

            Logger.LogInformation("✓ 安全状态摘要验证通过: Items={Items}, CleanupOps={Ops}",
                summary.SecureDataItemCount, summary.TotalCleanupOperations);
        }

        [Fact]
        public async Task ForceGarbageCollection_ShouldNotThrow()
        {
            // Arrange
            var securityManager = await Helper.InitializeSecurityManagerAsync();

            // Act & Assert
            var action = () => securityManager.ForceGarbageCollection();
            action.Should().NotThrow("强制垃圾回收不应该抛出异常");

            Logger.LogInformation("✓ 强制垃圾回收验证通过");
        }

        [Fact]
        public async Task RegisterCleanupTriggers_ShouldAcceptConfiguration()
        {
            // Arrange
            var securityManager = await Helper.InitializeSecurityManagerAsync();
            var triggers = DataGenerator.GenerateCleanupTriggers();

            // Act & Assert
            var action = () => securityManager.RegisterCleanupTriggers(triggers);
            action.Should().NotThrow("注册清理触发器不应该抛出异常");

            Logger.LogInformation("✓ 注册清理触发器验证通过");
        }

        [Fact]
        public async Task SecurityEvents_ShouldBeTriggeredDuringOperations()
        {
            // Arrange
            var securityManager = await Helper.InitializeSecurityManagerAsync();
            var eventsReceived = new List<SecurityEventArgs>();

            securityManager.SecurityEvent += (sender, args) =>
            {
                eventsReceived.Add(args);
                Logger.LogInformation("安全事件: {EventType} - {Message}",
                    args.EventType, args.Message);
            };

            // Act
            await Helper.StoreSecureDataAsync(securityManager);
            await securityManager.ClearAllSecurityStateAsync();

            // Assert
            eventsReceived.Should().NotBeEmpty("应该触发安全事件");
            Logger.LogInformation("✓ 安全事件触发验证通过，收到 {Count} 个事件", eventsReceived.Count);
        }

        [Fact]
        public async Task CleanupCompletedEvent_ShouldBeTriggeredOnCleanup()
        {
            // Arrange
            var securityManager = await Helper.InitializeSecurityManagerAsync();
            var cleanupEvents = new List<CleanupCompletedEventArgs>();

            securityManager.CleanupCompleted += (sender, args) =>
            {
                cleanupEvents.Add(args);
                Logger.LogInformation("清理完成: Type={Type}, Success={Success}, Items={Items}",
                    args.OperationType, args.IsSuccess, args.ItemsCleared);
            };

            // Act
            await Helper.StoreSecureDataAsync(securityManager);
            await securityManager.ClearAllSecurityStateAsync();

            // Assert
            cleanupEvents.Should().NotBeEmpty("应该触发清理完成事件");
            cleanupEvents.Should().Contain(e => e.IsSuccess, "至少有一个成功的清理操作");

            Logger.LogInformation("✓ 清理完成事件验证通过，收到 {Count} 个事件", cleanupEvents.Count);
        }

        [Fact]
        public async Task ConcurrentStoreOperations_ShouldBeThreadSafe()
        {
            // Arrange
            var securityManager = await Helper.InitializeSecurityManagerAsync();
            var concurrentOperations = 10;

            // Act
            var tasks = Enumerable.Range(0, concurrentOperations)
                .Select(_ => Helper.StoreSecureDataAsync(securityManager))
                .ToArray();

            var results = await Task.WhenAll(tasks);

            // Assert
            using (new AssertionScope())
            {
                results.Should().HaveCount(concurrentOperations, "所有操作都应该完成");
                results.Should().OnlyContain(r => r != null, "所有结果都应该有效");
                results.Select(r => r.Id).Should().OnlyHaveUniqueItems("所有ID都应该唯一");
            }

            Logger.LogInformation("✓ 并发存储操作线程安全验证通过");
        }

        [Fact]
        public async Task Dispose_ShouldCleanUpAllResources()
        {
            // Arrange
            var securityManager = GetService<ISecurityManager>();
            var context = DataGenerator.GenerateSecurityContext();
            await securityManager.InitializeAsync(context);

            await Helper.StoreSecureDataAsync(securityManager);
            await Helper.StoreSecureDataAsync(securityManager);

            // Act
            securityManager.Dispose();

            // Assert - 验证可以多次调用Dispose
            var action = () => securityManager.Dispose();
            action.Should().NotThrow("多次调用Dispose不应该抛出异常");

            // 验证对象已经被释放
            var throwAction = () => securityManager.GetSecurityStateSummary();
            throwAction.Should().Throw<ObjectDisposedException>("已释放的对象应该抛出ObjectDisposedException");

            Logger.LogInformation("✓ Dispose清理验证通过");
        }
    }
}
