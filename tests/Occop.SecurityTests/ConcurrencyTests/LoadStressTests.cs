using FluentAssertions;
using Occop.SecurityTests.Infrastructure;
using Occop.Core.Security;
using System.Diagnostics;

namespace Occop.SecurityTests.ConcurrencyTests
{
    /// <summary>
    /// 负载和压力测试
    /// Load and stress tests
    /// </summary>
    public class LoadStressTests : SecurityTestBase
    {
        [Fact]
        public async Task SecurityManager_UnderSustainedLoad_ShouldMaintainStability()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            var duration = TimeSpan.FromSeconds(10);
            var stopwatch = Stopwatch.StartNew();
            var operationCount = 0;
            var errorCount = 0;

            // Act - 持续负载
            var loadTasks = Enumerable.Range(0, 10).Select(_ => Task.Run(async () =>
            {
                while (stopwatch.Elapsed < duration)
                {
                    try
                    {
                        var data = await securityManager.StoreSecureDataAsync(
                            DataGenerator.CreateSecureString($"load_test_{Guid.NewGuid()}")
                        );
                        await securityManager.RetrieveSecureDataAsync(data.Id);
                        await securityManager.ClearSecureDataAsync(data.Id);

                        Interlocked.Increment(ref operationCount);
                    }
                    catch
                    {
                        Interlocked.Increment(ref errorCount);
                    }

                    await Task.Delay(10);
                }
            }));

            await Task.WhenAll(loadTasks);
            stopwatch.Stop();

            // Assert
            operationCount.Should().BeGreaterThan(0, "Should complete operations under load");
            errorCount.Should().Be(0, "Should not have errors under sustained load");

            var opsPerSecond = operationCount / stopwatch.Elapsed.TotalSeconds;
            opsPerSecond.Should().BeGreaterThan(10, "Should maintain reasonable throughput");
        }

        [Fact]
        public async Task SecurityManager_UnderSpikeLoad_ShouldRecoverGracefully()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            // Act - 突发负载
            var spikeTasks = Enumerable.Range(0, 200).Select(i => Task.Run(async () =>
            {
                var data = await securityManager.StoreSecureDataAsync(
                    DataGenerator.CreateSecureString($"spike_{i}")
                );
                return data.Id;
            }));

            var spikeResults = await Task.WhenAll(spikeTasks);

            // Assert - 系统应该处理突发负载
            spikeResults.Should().HaveCount(200);
            spikeResults.All(id => !string.IsNullOrEmpty(id)).Should().BeTrue();

            // 验证系统仍然可用
            var testData = await securityManager.StoreSecureDataAsync(
                DataGenerator.CreateSecureString("after_spike")
            );
            testData.Should().NotBeNull();
        }

        [Fact]
        public async Task SecurityManager_WithLargePayloads_ShouldHandleEfficiently()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            var largePayloadSize = 50000; // 50KB
            var stopwatch = Stopwatch.StartNew();

            // Act - 处理大负载
            var tasks = Enumerable.Range(0, 20).Select(i => Task.Run(async () =>
            {
                var largeData = DataGenerator.CreateSecureString(new string('x', largePayloadSize));
                var stored = await securityManager.StoreSecureDataAsync(largeData);
                await securityManager.RetrieveSecureDataAsync(stored.Id);
                await securityManager.ClearSecureDataAsync(stored.Id);
            }));

            await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Assert
            var avgTimePerOperation = stopwatch.ElapsedMilliseconds / 20.0;
            avgTimePerOperation.Should().BeLessThan(500,
                "Large payload operations should complete in reasonable time");
        }

        [Fact]
        public async Task SecurityManager_UnderMemoryPressure_ShouldAdaptGracefully()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            var initialMemory = GetCurrentMemoryUsage();

            // Act - 施加内存压力
            SimulateMemoryPressure(200); // 200MB

            // 在压力下继续操作
            var tasks = Enumerable.Range(0, 50).Select(i => Task.Run(async () =>
            {
                var data = await securityManager.StoreSecureDataAsync(
                    DataGenerator.CreateSecureString($"pressure_{i}")
                );
                await securityManager.ClearSecureDataAsync(data.Id);
            }));

            await Task.WhenAll(tasks);

            // 清理并强制GC
            await securityManager.ClearAllSecurityStateAsync();
            var freedMemory = await WaitForGarbageCollectionAsync();

            // Assert
            freedMemory.Should().BeGreaterThan(0, "Should release memory under pressure");
        }

        [Fact]
        public async Task SecurityAuditor_UnderHighAuditVolume_ShouldNotLoseEvents()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var auditor = TestContext.CreateSecurityAuditor(securityContext);

            var totalEvents = 0;
            auditor.AuditEvent += (sender, e) =>
            {
                Interlocked.Increment(ref totalEvents);
            };

            var eventCount = 1000;

            // Act - 高频率审计
            var tasks = Enumerable.Range(0, eventCount).Select(i => Task.Run(async () =>
            {
                await auditor.LogSecurityInitializationAsync($"High volume event {i}");
            }));

            await Task.WhenAll(tasks);
            await Task.Delay(500); // 等待事件处理

            // Assert
            totalEvents.Should().Be(eventCount, "All audit events should be captured");
            auditor.TotalAuditLogs.Should().Be(eventCount);
        }

        [Fact]
        public async Task SecurityManager_WithRapidCreateDestroy_ShouldNotLeakResources()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var initialMemory = GetCurrentMemoryUsage();

            // Act - 快速创建和销毁
            for (int i = 0; i < 50; i++)
            {
                var manager = TestContext.CreateSecurityManager(securityContext);
                await manager.InitializeAsync(securityContext);

                var data = await manager.StoreSecureDataAsync(
                    DataGenerator.CreateSecureString($"rapid_{i}")
                );

                await manager.ClearAllSecurityStateAsync();
                manager.Dispose();
            }

            var freedMemory = await WaitForGarbageCollectionAsync();

            // Assert
            var finalMemory = GetCurrentMemoryUsage();
            var memoryIncrease = finalMemory - initialMemory;

            memoryIncrease.Should().BeLessThan(10 * 1024 * 1024,
                "Rapid create/destroy should not leak significant memory");
        }

        [Fact]
        public async Task ConcurrentOperations_WithDifferentTypes_ShouldScaleWell()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            var stopwatch = Stopwatch.StartNew();

            // Act - 混合操作类型
            var storeTasks = Enumerable.Range(0, 50).Select(i =>
                securityManager.StoreSecureDataAsync(DataGenerator.CreateSecureString($"store_{i}"))
            );

            var storeResults = await Task.WhenAll(storeTasks);

            var retrieveTasks = storeResults.Select(data =>
                securityManager.RetrieveSecureDataAsync(data.Id)
            );

            await Task.WhenAll(retrieveTasks);

            var clearTasks = storeResults.Select(data =>
                securityManager.ClearSecureDataAsync(data.Id)
            );

            await Task.WhenAll(clearTasks);

            stopwatch.Stop();

            // Assert
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000,
                "Mixed operations should complete within reasonable time");
        }

        [Fact]
        public async Task SystemResources_UnderPeakLoad_ShouldRemainWithinLimits()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            var process = Process.GetCurrentProcess();
            var initialThreadCount = process.Threads.Count;
            var initialMemory = process.WorkingSet64;

            // Act - 峰值负载
            var tasks = Enumerable.Range(0, 100).Select(_ => Task.Run(async () =>
            {
                for (int i = 0; i < 10; i++)
                {
                    var data = await securityManager.StoreSecureDataAsync(
                        DataGenerator.CreateSecureString($"peak_{Guid.NewGuid()}")
                    );
                    await securityManager.ClearSecureDataAsync(data.Id);
                }
            }));

            await Task.WhenAll(tasks);

            process.Refresh();
            var peakThreadCount = process.Threads.Count;
            var peakMemory = process.WorkingSet64;

            // 清理
            await securityManager.ClearAllSecurityStateAsync();
            await Task.Delay(1000);

            process.Refresh();
            var finalThreadCount = process.Threads.Count;

            // Assert
            var threadIncrease = peakThreadCount - initialThreadCount;
            threadIncrease.Should().BeLessThan(50,
                "Thread count should not explode under load");

            var memoryIncrease = (peakMemory - initialMemory) / (1024.0 * 1024.0); // MB
            memoryIncrease.Should().BeLessThan(100,
                "Memory usage should remain reasonable under peak load");
        }

        [Fact]
        public async Task Throughput_UnderOptimalConditions_ShouldMeetBaseline()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            var operationCount = 500;
            var stopwatch = Stopwatch.StartNew();

            // Act - 顺序操作（最佳条件）
            for (int i = 0; i < operationCount; i++)
            {
                var data = await securityManager.StoreSecureDataAsync(
                    DataGenerator.CreateSecureString($"throughput_{i}")
                );
                await securityManager.ClearSecureDataAsync(data.Id);
            }

            stopwatch.Stop();

            // Assert
            var opsPerSecond = operationCount / stopwatch.Elapsed.TotalSeconds;
            opsPerSecond.Should().BeGreaterThan(50,
                "Should achieve baseline throughput under optimal conditions");
        }

        [Fact]
        public async Task ResponseTime_UnderNormalLoad_ShouldBeConsistent()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            var responseTimes = new List<long>();

            // Act - 测量响应时间
            for (int i = 0; i < 100; i++)
            {
                var sw = Stopwatch.StartNew();

                var data = await securityManager.StoreSecureDataAsync(
                    DataGenerator.CreateSecureString($"response_{i}")
                );
                await securityManager.RetrieveSecureDataAsync(data.Id);
                await securityManager.ClearSecureDataAsync(data.Id);

                sw.Stop();
                responseTimes.Add(sw.ElapsedMilliseconds);
            }

            // Assert
            var avgResponseTime = responseTimes.Average();
            var maxResponseTime = responseTimes.Max();
            var p95ResponseTime = responseTimes.OrderBy(t => t).ElementAt((int)(responseTimes.Count * 0.95));

            avgResponseTime.Should().BeLessThan(50, "Average response time should be acceptable");
            p95ResponseTime.Should().BeLessThan(100, "95th percentile should be reasonable");
        }

        [Fact]
        public async Task ErrorRate_UnderStress_ShouldRemainLow()
        {
            // Arrange
            var securityContext = TestContext.CreateSecurityContext();
            var securityManager = TestContext.CreateSecurityManager(securityContext);
            await securityManager.InitializeAsync(securityContext);

            var successCount = 0;
            var errorCount = 0;
            var totalOperations = 200;

            // Act - 压力操作
            var tasks = Enumerable.Range(0, totalOperations).Select(i => Task.Run(async () =>
            {
                try
                {
                    var data = await securityManager.StoreSecureDataAsync(
                        DataGenerator.CreateSecureString($"stress_{i}")
                    );
                    await securityManager.RetrieveSecureDataAsync(data.Id);
                    await securityManager.ClearSecureDataAsync(data.Id);

                    Interlocked.Increment(ref successCount);
                }
                catch
                {
                    Interlocked.Increment(ref errorCount);
                }
            }));

            await Task.WhenAll(tasks);

            // Assert
            var errorRate = (double)errorCount / totalOperations;
            errorRate.Should().BeLessThan(0.01, "Error rate should be less than 1% under stress");
            successCount.Should().Be(totalOperations, "Most operations should succeed");
        }
    }
}
