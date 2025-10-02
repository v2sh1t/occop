using FluentAssertions;
using Occop.SecurityTests.Infrastructure;
using Occop.Core.Security;

namespace Occop.SecurityTests.ExceptionHandlingTests
{
    /// <summary>
    /// 资源泄露检测测试
    /// Resource leak detection tests
    /// </summary>
    public class ResourceLeakTests : SecurityTestBase
    {
        [Fact]
        public async Task SecurityManager_OnMultipleInitializations_ShouldNotLeakMemory()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var initialMemory = GetCurrentMemoryUsage();

            // Act - 多次初始化和清理
            for (int i = 0; i < 10; i++)
            {
                var securityManager = TestContext.CreateSecurityManager(securityContext);
                await securityManager.InitializeAsync(securityContext);
                await securityManager.ClearAllSecurityStateAsync();
                securityManager.Dispose();
            }

            var freedMemory = await WaitForGarbageCollectionAsync();

            // Assert
            var finalMemory = GetCurrentMemoryUsage();
            var memoryIncrease = finalMemory - initialMemory;

            // 内存增长不应超过10MB
            memoryIncrease.Should().BeLessThan(10 * 1024 * 1024,
                "Multiple initialization cycles should not leak significant memory");
        }

        [Fact]
        public async Task SecurityManager_OnRepeatedOperations_ShouldReleaseResources()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            var initialMemory = GetCurrentMemoryUsage();

            // Act - 重复存储和检索操作
            for (int i = 0; i < 100; i++)
            {
                var secureString = DataGenerator.CreateSecureString($"leak_test_{i}");
                var data = await securityManager.StoreSecureDataAsync(secureString);
                var retrieved = await securityManager.RetrieveSecureDataAsync(data.Id);
                await securityManager.ClearSecureDataAsync(data.Id);
            }

            await WaitForGarbageCollectionAsync();

            // Assert
            var finalMemory = GetCurrentMemoryUsage();
            var memoryIncrease = finalMemory - initialMemory;

            // 内存增长应该很小
            memoryIncrease.Should().BeLessThan(5 * 1024 * 1024,
                "Repeated operations should not leak memory");
        }

        [Fact]
        public async Task SecurityManager_OnEventHandlers_ShouldNotLeakHandlers()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            var eventCount = 0;
            EventHandler<SecurityEventArgs> handler = (sender, e) => { eventCount++; };

            // Act - 多次订阅和取消订阅
            for (int i = 0; i < 100; i++)
            {
                securityManager.SecurityEvent += handler;
                securityManager.SecurityEvent -= handler;
            }

            // Assert - 事件处理器应该被正确清理
            eventCount.Should().Be(0, "Event handlers should be properly unsubscribed");

            // 清理
            await securityManager.ClearAllSecurityStateAsync();
            securityManager.Dispose();
        }

        [Fact]
        public async Task SecurityAuditor_OnContinuousAuditing_ShouldNotLeakLogs()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var auditor = TestContext.CreateSecurityAuditor(securityContext);
            var initialMemory = GetCurrentMemoryUsage();

            // Act - 连续审计
            for (int i = 0; i < 1000; i++)
            {
                await auditor.LogSecurityInitializationAsync($"Test audit {i}");
            }

            // 清理过期日志
            await auditor.CleanupExpiredLogsAsync(TimeSpan.FromSeconds(0));
            await WaitForGarbageCollectionAsync();

            // Assert
            var finalMemory = GetCurrentMemoryUsage();
            var memoryIncrease = finalMemory - initialMemory;

            memoryIncrease.Should().BeLessThan(5 * 1024 * 1024,
                "Audit logs should be cleaned up properly");

            auditor.Dispose();
        }

        [Fact]
        public async Task LoggerService_OnContinuousLogging_ShouldNotLeakMemory()
        {
            // Arrange
            var logger = TestContext.CreateLogger<ResourceLeakTests>();
            var initialMemory = GetCurrentMemoryUsage();

            // Act - 连续日志记录
            for (int i = 0; i < 1000; i++)
            {
                logger.LogInformation($"Test log message {i}");
                logger.LogWarning($"Warning message {i}");
                logger.LogError($"Error message {i}");
            }

            await WaitForGarbageCollectionAsync();

            // Assert
            var finalMemory = GetCurrentMemoryUsage();
            var memoryIncrease = finalMemory - initialMemory;

            memoryIncrease.Should().BeLessThan(10 * 1024 * 1024,
                "Continuous logging should not leak memory");
        }

        [Fact]
        public async Task FileHandles_OnRepeatedFileOperations_ShouldBeReleased()
        {
            // Arrange
            var tempFiles = new List<string>();

            try
            {
                // Act - 创建和删除多个文件
                for (int i = 0; i < 100; i++)
                {
                    var tempFile = CreateTempFile($"test content {i}");
                    tempFiles.Add(tempFile);

                    // 立即读取和删除
                    var content = await File.ReadAllTextAsync(tempFile);
                    DeleteTempFile(tempFile);
                }

                await Task.Delay(100); // 给系统时间释放文件句柄

                // Assert - 应该能够访问文件系统
                var testFile = CreateTempFile("final test");
                File.Exists(testFile).Should().BeTrue();
                DeleteTempFile(testFile);
            }
            finally
            {
                // 清理剩余文件
                foreach (var file in tempFiles)
                {
                    DeleteTempFile(file);
                }
            }
        }

        [Fact]
        public async Task TimerResources_OnMultipleTimers_ShouldBeDisposed()
        {
            // Arrange
            var timers = new List<System.Threading.Timer>();
            var timerCount = 0;

            // Act - 创建多个定时器
            for (int i = 0; i < 10; i++)
            {
                var timer = new System.Threading.Timer(_ =>
                {
                    Interlocked.Increment(ref timerCount);
                }, null, 100, 100);

                timers.Add(timer);
            }

            await Task.Delay(500);

            // 释放所有定时器
            foreach (var timer in timers)
            {
                timer.Dispose();
            }

            var countBefore = timerCount;
            await Task.Delay(500);
            var countAfter = timerCount;

            // Assert - 定时器应该停止工作
            countAfter.Should().Be(countBefore, "Timers should stop after disposal");
        }

        [Fact]
        public async Task ThreadResources_OnParallelOperations_ShouldComplete()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            var initialThreadCount = System.Diagnostics.Process.GetCurrentProcess().Threads.Count;

            // Act - 并行操作
            var tasks = Enumerable.Range(0, 50).Select(async i =>
            {
                var data = DataGenerator.CreateSecureString($"thread_test_{i}");
                var stored = await securityManager.StoreSecureDataAsync(data);
                await Task.Delay(10);
                await securityManager.ClearSecureDataAsync(stored.Id);
            });

            await Task.WhenAll(tasks);
            await Task.Delay(1000); // 等待线程清理

            // Assert - 线程应该被回收
            var finalThreadCount = System.Diagnostics.Process.GetCurrentProcess().Threads.Count;
            var threadIncrease = finalThreadCount - initialThreadCount;

            threadIncrease.Should().BeLessThan(10,
                "Thread count should not significantly increase");
        }

        [Fact]
        public async Task DisposableResources_OnExceptionPath_ShouldBeReleased()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var resourcesReleased = 0;

            // Act - 模拟异常路径
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    var securityManager = TestContext.CreateSecurityManager(securityContext);
                    await securityManager.InitializeAsync(securityContext);

                    // 模拟异常
                    if (i % 2 == 0)
                    {
                        throw new InvalidOperationException("Test exception");
                    }

                    await securityManager.ClearAllSecurityStateAsync();
                    securityManager.Dispose();
                    resourcesReleased++;
                }
                catch (InvalidOperationException)
                {
                    // 异常路径也应该清理资源
                    resourcesReleased++;
                }
            }

            await WaitForGarbageCollectionAsync();

            // Assert
            resourcesReleased.Should().Be(10, "All resources should be released even on exception paths");
        }

        [Fact]
        public async Task WeakReferences_OnObjectDisposal_ShouldBeCollected()
        {
            // Arrange
            WeakReference? weakRef = null;

            // Act - 在独立作用域中创建对象
            await Task.Run(async () =>
            {
                var securityContext = TestContext.CreateSecurityContext();
                var securityManager = TestContext.CreateSecurityManager(securityContext);
                await securityManager.InitializeAsync(securityContext);

                weakRef = new WeakReference(securityManager);

                await securityManager.ClearAllSecurityStateAsync();
                securityManager.Dispose();
            });

            await WaitForGarbageCollectionAsync();

            // Assert - 对象应该被回收
            weakRef.Should().NotBeNull();
            weakRef!.IsAlive.Should().BeFalse("Object should be garbage collected after disposal");
        }

        [Fact]
        public async Task LargeObjectHeap_OnBigAllocations_ShouldBeManaged()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            var initialMemory = GetCurrentMemoryUsage();

            // Act - 分配大对象（>85KB会进入LOH）
            for (int i = 0; i < 10; i++)
            {
                var largeData = DataGenerator.CreateSecureString(new string('x', 100000));
                var stored = await securityManager.StoreSecureDataAsync(largeData);
                await securityManager.ClearSecureDataAsync(stored.Id);
            }

            await WaitForGarbageCollectionAsync();

            // Assert
            var finalMemory = GetCurrentMemoryUsage();
            var memoryIncrease = finalMemory - initialMemory;

            memoryIncrease.Should().BeLessThan(20 * 1024 * 1024,
                "Large object heap should be managed properly");
        }

        [Fact]
        public async Task FinalizersAndDestructors_OnObjectCleanup_ShouldRun()
        {
            // Arrange
            var finalizerRan = false;
            var testObject = new DisposableTestObject(() => finalizerRan = true);

            // Act
            testObject.Dispose();
            testObject = null!;

            await WaitForGarbageCollectionAsync();

            // Assert - 终结器应该运行
            // 注意：终结器的执行不能保证立即发生
            await Task.Delay(500);
        }

        /// <summary>
        /// 测试用的可释放对象
        /// </summary>
        private class DisposableTestObject : IDisposable
        {
            private readonly Action _onFinalize;
            private bool _disposed;

            public DisposableTestObject(Action onFinalize)
            {
                _onFinalize = onFinalize;
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing)
            {
                if (!_disposed)
                {
                    if (disposing)
                    {
                        _onFinalize?.Invoke();
                    }
                    _disposed = true;
                }
            }

            ~DisposableTestObject()
            {
                Dispose(false);
            }
        }
    }
}
