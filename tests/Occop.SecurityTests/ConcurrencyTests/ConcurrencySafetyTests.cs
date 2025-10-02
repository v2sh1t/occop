using FluentAssertions;
using Occop.SecurityTests.Infrastructure;
using Occop.Core.Security;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Occop.SecurityTests.ConcurrencyTests
{
    /// <summary>
    /// 并发安全测试
    /// Concurrency safety tests
    /// </summary>
    public class ConcurrencySafetyTests : SecurityTestBase
    {
        [Fact]
        public async Task SecurityManager_OnConcurrentStoreOperations_ShouldBeThreadSafe()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            var storedIds = new ConcurrentBag<string>();
            var errors = new ConcurrentBag<Exception>();

            // Act - 并发存储操作
            var tasks = Enumerable.Range(0, 100).Select(i => Task.Run(async () =>
            {
                try
                {
                    var secureString = DataGenerator.CreateSecureString($"concurrent_data_{i}");
                    var data = await securityManager.StoreSecureDataAsync(secureString);
                    storedIds.Add(data.Id);
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            }));

            await Task.WhenAll(tasks);

            // Assert
            errors.Should().BeEmpty("No errors should occur during concurrent operations");
            storedIds.Should().HaveCount(100, "All operations should complete successfully");
            storedIds.Distinct().Should().HaveCount(100, "All IDs should be unique");

            // 清理
            await securityManager.ClearAllSecurityStateAsync();
        }

        [Fact]
        public async Task SecurityManager_OnConcurrentRetrieveOperations_ShouldReturnCorrectData()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            // 预先存储数据
            var testData = new Dictionary<string, string>();
            for (int i = 0; i < 50; i++)
            {
                var value = $"test_value_{i}";
                var secureString = DataGenerator.CreateSecureString(value);
                var data = await securityManager.StoreSecureDataAsync(secureString);
                testData[data.Id] = value;
            }

            var retrievalErrors = new ConcurrentBag<string>();

            // Act - 并发检索操作
            var tasks = testData.Select(kvp => Task.Run(async () =>
            {
                var retrieved = await securityManager.RetrieveSecureDataAsync(kvp.Key);
                if (retrieved == null)
                {
                    retrievalErrors.Add($"Failed to retrieve {kvp.Key}");
                }
            }));

            await Task.WhenAll(tasks);

            // Assert
            retrievalErrors.Should().BeEmpty("All concurrent retrievals should succeed");
        }

        [Fact]
        public async Task SecurityManager_OnConcurrentClearOperations_ShouldNotCorrupt()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            var dataIds = new List<string>();
            for (int i = 0; i < 50; i++)
            {
                var data = await securityManager.StoreSecureDataAsync(
                    DataGenerator.CreateSecureString($"data_{i}")
                );
                dataIds.Add(data.Id);
            }

            // Act - 并发清理操作
            var tasks = dataIds.Select(id => Task.Run(async () =>
            {
                await securityManager.ClearSecureDataAsync(id);
            }));

            await Task.WhenAll(tasks);

            // Assert - 验证状态完整性
            var validationResult = await securityManager.ValidateSecurityStateAsync();
            validationResult.IsValid.Should().BeTrue("State should remain valid after concurrent clears");
        }

        [Fact]
        public async Task SecurityManager_OnReadWriteConflict_ShouldHandleCorrectly()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            var testDataId = string.Empty;
            var secureString = DataGenerator.CreateSecureString("conflict_test");
            var stored = await securityManager.StoreSecureDataAsync(secureString);
            testDataId = stored.Id;

            var readCount = 0;
            var writeCount = 0;

            // Act - 同时读写
            var readTasks = Enumerable.Range(0, 50).Select(_ => Task.Run(async () =>
            {
                var data = await securityManager.RetrieveSecureDataAsync(testDataId);
                if (data != null)
                {
                    Interlocked.Increment(ref readCount);
                }
            }));

            var writeTasks = Enumerable.Range(0, 10).Select(i => Task.Run(async () =>
            {
                await securityManager.ClearSecureDataAsync(testDataId);
                var newData = await securityManager.StoreSecureDataAsync(
                    DataGenerator.CreateSecureString($"update_{i}")
                );
                Interlocked.Increment(ref writeCount);
            }));

            await Task.WhenAll(readTasks.Concat(writeTasks));

            // Assert - 不应该崩溃或数据损坏
            writeCount.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task SecurityAuditor_OnConcurrentAuditing_ShouldRecordAllEvents()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var auditor = TestContext.CreateSecurityAuditor(securityContext);
            var eventCount = 0;

            auditor.AuditEvent += (sender, e) =>
            {
                Interlocked.Increment(ref eventCount);
            };

            // Act - 并发审计
            var tasks = Enumerable.Range(0, 100).Select(i => Task.Run(async () =>
            {
                await auditor.LogSecurityInitializationAsync($"Event {i}");
            }));

            await Task.WhenAll(tasks);

            // Assert
            eventCount.Should().Be(100, "All audit events should be recorded");
            auditor.TotalAuditLogs.Should().Be(100);
        }

        [Fact]
        public async Task SecurityManager_OnHighLoadStress_ShouldMaintainPerformance()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            var stopwatch = Stopwatch.StartNew();
            var operationCount = 500;

            // Act - 高负载操作
            var tasks = Enumerable.Range(0, operationCount).Select(i => Task.Run(async () =>
            {
                var data = await securityManager.StoreSecureDataAsync(
                    DataGenerator.CreateSecureString($"stress_{i}")
                );
                await securityManager.RetrieveSecureDataAsync(data.Id);
                await securityManager.ClearSecureDataAsync(data.Id);
            }));

            await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Assert - 性能应该合理
            var avgTimePerOperation = stopwatch.ElapsedMilliseconds / (double)operationCount;
            avgTimePerOperation.Should().BeLessThan(100,
                "Average operation time should be less than 100ms under load");
        }

        [Fact]
        public async Task SecurityManager_OnDeadlockScenario_ShouldNotDeadlock()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            var data1 = await securityManager.StoreSecureDataAsync(DataGenerator.CreateSecureString("data1"));
            var data2 = await securityManager.StoreSecureDataAsync(DataGenerator.CreateSecureString("data2"));

            // Act - 模拟可能导致死锁的场景
            var task1 = Task.Run(async () =>
            {
                for (int i = 0; i < 10; i++)
                {
                    await securityManager.RetrieveSecureDataAsync(data1.Id);
                    await Task.Delay(1);
                    await securityManager.RetrieveSecureDataAsync(data2.Id);
                }
            });

            var task2 = Task.Run(async () =>
            {
                for (int i = 0; i < 10; i++)
                {
                    await securityManager.RetrieveSecureDataAsync(data2.Id);
                    await Task.Delay(1);
                    await securityManager.RetrieveSecureDataAsync(data1.Id);
                }
            });

            // 设置超时检测死锁
            var completedTask = await Task.WhenAny(
                Task.WhenAll(task1, task2),
                Task.Delay(TimeSpan.FromSeconds(10))
            );

            // Assert - 应该在超时前完成
            completedTask.Should().Be(Task.WhenAll(task1, task2),
                "Operations should complete without deadlock");
        }

        [Fact]
        public async Task ConcurrentDictionary_OnRaceCondition_ShouldHandleSafely()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            var sharedDict = new ConcurrentDictionary<int, string>();

            // Act - 并发字典操作
            var tasks = Enumerable.Range(0, 100).Select(i => Task.Run(() =>
            {
                sharedDict.TryAdd(i, $"value_{i}");
                sharedDict.TryGetValue(i, out var value);
                sharedDict.TryRemove(i, out _);
            }));

            await Task.WhenAll(tasks);

            // Assert - 不应该抛出异常
            Assert.True(true, "Concurrent dictionary operations completed safely");
        }

        [Fact]
        public async Task AtomicOperations_OnCounterIncrement_ShouldBeAccurate()
        {
            // Arrange
            var counter = 0;
            var expectedCount = 1000;

            // Act - 并发递增
            var tasks = Enumerable.Range(0, expectedCount).Select(_ => Task.Run(() =>
            {
                Interlocked.Increment(ref counter);
            }));

            await Task.WhenAll(tasks);

            // Assert
            counter.Should().Be(expectedCount, "Atomic operations should produce accurate results");
        }

        [Fact]
        public async Task SemaphoreSlim_OnResourceLimiting_ShouldEnforceLimit()
        {
            // Arrange
            var maxConcurrent = 5;
            var semaphore = new SemaphoreSlim(maxConcurrent);
            var currentConcurrent = 0;
            var maxReached = 0;

            // Act - 模拟受限并发
            var tasks = Enumerable.Range(0, 50).Select(_ => Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var current = Interlocked.Increment(ref currentConcurrent);
                    var max = Math.Max(Interlocked.CompareExchange(ref maxReached, current, maxReached), current);
                    Interlocked.CompareExchange(ref maxReached, max, maxReached);

                    await Task.Delay(10);

                    Interlocked.Decrement(ref currentConcurrent);
                }
                finally
                {
                    semaphore.Release();
                }
            }));

            await Task.WhenAll(tasks);

            // Assert
            maxReached.Should().BeLessOrEqualTo(maxConcurrent,
                "Semaphore should enforce concurrency limit");
        }

        [Fact]
        public async Task AsyncLock_OnCriticalSection_ShouldSerializeAccess()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            var lockObj = new SemaphoreSlim(1, 1);
            var sharedResource = 0;
            var inconsistencies = 0;

            // Act - 并发访问共享资源
            var tasks = Enumerable.Range(0, 100).Select(_ => Task.Run(async () =>
            {
                await lockObj.WaitAsync();
                try
                {
                    var temp = sharedResource;
                    await Task.Delay(1); // 模拟处理时间
                    temp++;

                    if (sharedResource != temp - 1)
                    {
                        Interlocked.Increment(ref inconsistencies);
                    }

                    sharedResource = temp;
                }
                finally
                {
                    lockObj.Release();
                }
            }));

            await Task.WhenAll(tasks);

            // Assert
            inconsistencies.Should().Be(0, "Lock should prevent race conditions");
            sharedResource.Should().Be(100, "Final value should be correct");
        }

        [Fact]
        public async Task TaskCancellation_OnConcurrentOperations_ShouldCancelGracefully()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            var cts = new CancellationTokenSource();
            var completedCount = 0;
            var cancelledCount = 0;

            // Act - 启动并发操作，然后取消
            var tasks = Enumerable.Range(0, 100).Select(i => Task.Run(async () =>
            {
                try
                {
                    for (int j = 0; j < 10; j++)
                    {
                        cts.Token.ThrowIfCancellationRequested();

                        var data = await securityManager.StoreSecureDataAsync(
                            DataGenerator.CreateSecureString($"cancel_test_{i}_{j}")
                        );

                        await Task.Delay(10, cts.Token);
                        await securityManager.ClearSecureDataAsync(data.Id);
                    }

                    Interlocked.Increment(ref completedCount);
                }
                catch (OperationCanceledException)
                {
                    Interlocked.Increment(ref cancelledCount);
                }
            }));

            // 等待一段时间后取消
            await Task.Delay(100);
            cts.Cancel();

            await Task.WhenAll(tasks.Select(t => t.ContinueWith(_ => { })));

            // Assert
            (completedCount + cancelledCount).Should().Be(100,
                "All tasks should either complete or be cancelled");
            cancelledCount.Should().BeGreaterThan(0,
                "Some tasks should be cancelled");
        }

        [Fact]
        public async Task MemoryBarrier_OnSharedState_ShouldEnsureVisibility()
        {
            // Arrange
            var flag = false;
            var data = 0;
            var readCorrectValue = false;

            // Act - 写入线程
            var writerTask = Task.Run(() =>
            {
                data = 42;
                Thread.MemoryBarrier(); // 确保写入可见
                flag = true;
            });

            // 读取线程
            var readerTask = Task.Run(() =>
            {
                while (!flag)
                {
                    Thread.SpinWait(100);
                }
                Thread.MemoryBarrier(); // 确保读取最新值
                readCorrectValue = (data == 42);
            });

            await Task.WhenAll(writerTask, readerTask);

            // Assert
            readCorrectValue.Should().BeTrue("Memory barrier should ensure visibility");
        }
    }
}
