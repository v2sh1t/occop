using FluentAssertions;
using Microsoft.Extensions.Logging;
using Occop.Core.Authentication;
using Occop.Core.Performance;
using Occop.Core.Security;
using Occop.IntegrationTests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Occop.StabilityTests;

/// <summary>
/// 长时间运行的稳定性测试
/// 这些测试旨在检测内存泄漏、性能降级和长期稳定性问题
/// </summary>
public class LongRunningStabilityTests : IClassFixture<IntegrationTestContext>
{
    private readonly IntegrationTestContext _context;
    private readonly ITestOutputHelper _output;
    private readonly ILogger<LongRunningStabilityTests> _logger;

    public LongRunningStabilityTests(IntegrationTestContext context, ITestOutputHelper output)
    {
        _context = context;
        _output = output;
        _logger = context.LoggerFactory.CreateLogger<LongRunningStabilityTests>();
    }

    /// <summary>
    /// 24小时稳定性测试 - 持续运行检测内存泄漏和性能降级
    /// 注意: 此测试默认跳过,仅在CI或专门的稳定性测试环境中运行
    /// </summary>
    [Fact(Skip = "长时间运行测试 - 仅在稳定性测试环境中运行")]
    [Trait("Category", "Stability")]
    [Trait("Duration", "24Hours")]
    public async Task System_Should_RemainStable_During24HourOperation()
    {
        // 测试配置
        var testDuration = TimeSpan.FromHours(24);
        var operationInterval = TimeSpan.FromSeconds(10);
        var memoryCheckInterval = TimeSpan.FromMinutes(30);

        var startTime = DateTime.Now;
        var endTime = startTime.Add(testDuration);

        _logger.LogInformation("开始24小时稳定性测试");
        _logger.LogInformation("开始时间: {StartTime}", startTime);
        _logger.LogInformation("预计结束时间: {EndTime}", endTime);

        var monitor = new PerformanceMonitor(_context.LoggerFactory.CreateLogger<PerformanceMonitor>());
        var memoryAnalyzer = new MemoryAnalyzer(_context.LoggerFactory.CreateLogger<MemoryAnalyzer>());
        var memorySnapshots = new List<MemorySnapshot>();

        var initialMemory = GC.GetTotalMemory(true);
        memorySnapshots.Add(monitor.GetMemorySnapshot());

        var operationCount = 0;
        var errorCount = 0;
        var lastMemoryCheck = DateTime.Now;

        using var cts = new CancellationTokenSource(testDuration);

        try
        {
            while (DateTime.Now < endTime && !cts.Token.IsCancellationRequested)
            {
                // 执行典型操作
                try
                {
                    await PerformTypicalOperationsAsync(monitor);
                    operationCount++;

                    // 定期检查内存
                    if (DateTime.Now - lastMemoryCheck >= memoryCheckInterval)
                    {
                        var snapshot = monitor.GetMemorySnapshot();
                        memorySnapshots.Add(snapshot);
                        lastMemoryCheck = DateTime.Now;

                        // 检测内存泄漏
                        if (memorySnapshots.Count >= 3)
                        {
                            var hasLeak = memoryAnalyzer.DetectMemoryLeak(memorySnapshots, threshold: 5.0);
                            if (hasLeak)
                            {
                                _logger.LogWarning("检测到可能的内存泄漏");
                                var trendReport = memoryAnalyzer.GenerateTrendReport(memorySnapshots);
                                _logger.LogWarning("内存增长趋势: {TrendMBPerHour} MB/小时",
                                    trendReport.ManagedHeapTrendMBPerHour);
                            }
                        }

                        // 检测性能降级
                        var hasDegradation = monitor.DetectDegradation("TypicalOperation", threshold: 20.0);
                        if (hasDegradation)
                        {
                            _logger.LogWarning("检测到性能降级");
                        }

                        // 记录进度
                        var elapsed = DateTime.Now - startTime;
                        var remaining = endTime - DateTime.Now;
                        _logger.LogInformation(
                            "进度: {Elapsed}/{Total} ({Percentage:F2}%), 操作数: {Operations}, 错误: {Errors}",
                            elapsed, testDuration, (elapsed.TotalMilliseconds / testDuration.TotalMilliseconds * 100),
                            operationCount, errorCount);
                    }
                }
                catch (Exception ex)
                {
                    errorCount++;
                    _logger.LogError(ex, "操作失败");

                    // 错误率过高则失败
                    if (errorCount > operationCount * 0.05) // 5%错误率
                    {
                        throw new Exception($"错误率过高: {errorCount}/{operationCount}");
                    }
                }

                await Task.Delay(operationInterval, cts.Token);
            }

            // 最终验证
            var finalMemory = GC.GetTotalMemory(true);
            var memoryIncrease = finalMemory - initialMemory;
            var memoryIncreasePercentage = (memoryIncrease / (double)initialMemory) * 100;

            _logger.LogInformation("测试完成");
            _logger.LogInformation("总操作数: {Operations}", operationCount);
            _logger.LogInformation("总错误数: {Errors}", errorCount);
            _logger.LogInformation("内存增长: {Increase} bytes ({Percentage:F2}%)",
                memoryIncrease, memoryIncreasePercentage);

            // 断言
            memoryIncreasePercentage.Should().BeLessThan(20, "24小时内存增长不应超过20%");
            errorCount.Should().BeLessThan(operationCount / 100, "错误率不应超过1%");

            var finalAnalysis = memoryAnalyzer.Analyze(memorySnapshots.Last());
            finalAnalysis.Issues.Should().NotContain(i => i.Severity == "Critical", "不应有严重的内存问题");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("测试被取消");
        }
    }

    /// <summary>
    /// 1小时压力测试 - 持续高负载
    /// </summary>
    [Fact(Skip = "长时间运行测试 - 仅在稳定性测试环境中运行")]
    [Trait("Category", "Stability")]
    [Trait("Duration", "1Hour")]
    public async Task System_Should_HandleContinuousLoad_For1Hour()
    {
        var testDuration = TimeSpan.FromHours(1);
        var concurrentOperations = 10;
        var startTime = DateTime.Now;

        _logger.LogInformation("开始1小时压力测试，并发度: {Concurrency}", concurrentOperations);

        var monitor = new PerformanceMonitor(_context.LoggerFactory.CreateLogger<PerformanceMonitor>());
        var operationCounts = new int[concurrentOperations];
        var errorCounts = new int[concurrentOperations];

        using var cts = new CancellationTokenSource(testDuration);

        var tasks = Enumerable.Range(0, concurrentOperations).Select(async i =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    await PerformTypicalOperationsAsync(monitor);
                    Interlocked.Increment(ref operationCounts[i]);
                }
                catch
                {
                    Interlocked.Increment(ref errorCounts[i]);
                }

                await Task.Delay(100, cts.Token);
            }
        }).ToArray();

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            // 预期的超时
        }

        var totalOperations = operationCounts.Sum();
        var totalErrors = errorCounts.Sum();
        var elapsed = DateTime.Now - startTime;

        _logger.LogInformation("压力测试完成");
        _logger.LogInformation("耗时: {Elapsed}", elapsed);
        _logger.LogInformation("总操作数: {Operations}", totalOperations);
        _logger.LogInformation("总错误数: {Errors}", totalErrors);
        _logger.LogInformation("吞吐量: {Throughput:F2} 操作/秒", totalOperations / elapsed.TotalSeconds);

        // 断言
        totalOperations.Should().BeGreaterThan(0);
        var errorRate = totalErrors / (double)totalOperations;
        errorRate.Should().BeLessThan(0.01, "错误率应低于1%");
    }

    /// <summary>
    /// 内存泄漏检测测试 - 重复操作检测泄漏
    /// </summary>
    [Fact]
    [Trait("Category", "Stability")]
    [Trait("Duration", "Short")]
    public async Task RepeatedOperations_ShouldNot_CauseMemoryLeak()
    {
        var iterations = 1000;
        var monitor = new PerformanceMonitor(_context.LoggerFactory.CreateLogger<PerformanceMonitor>());
        var memoryAnalyzer = new MemoryAnalyzer(_context.LoggerFactory.CreateLogger<MemoryAnalyzer>());
        var snapshots = new List<MemorySnapshot>();

        // 预热
        for (int i = 0; i < 10; i++)
        {
            await PerformTypicalOperationsAsync(monitor);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var initialMemory = GC.GetTotalMemory(false);
        snapshots.Add(monitor.GetMemorySnapshot());

        // 重复操作
        for (int i = 0; i < iterations; i++)
        {
            await PerformTypicalOperationsAsync(monitor);

            // 每100次迭代记录一次快照
            if (i % 100 == 0)
            {
                GC.Collect();
                snapshots.Add(monitor.GetMemorySnapshot());
            }
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(false);
        var memoryIncrease = finalMemory - initialMemory;

        _logger.LogInformation("初始内存: {Initial} bytes", initialMemory);
        _logger.LogInformation("最终内存: {Final} bytes", finalMemory);
        _logger.LogInformation("内存增长: {Increase} bytes", memoryIncrease);

        // 检测内存泄漏
        var hasLeak = memoryAnalyzer.DetectMemoryLeak(snapshots, threshold: 5.0);
        hasLeak.Should().BeFalse("不应检测到内存泄漏");

        // 内存增长不应超过初始内存的50%
        var increasePercentage = (memoryIncrease / (double)initialMemory) * 100;
        increasePercentage.Should().BeLessThan(50, "内存增长不应超过初始内存的50%");
    }

    /// <summary>
    /// 性能退化检测测试
    /// </summary>
    [Fact]
    [Trait("Category", "Stability")]
    [Trait("Duration", "Short")]
    public async Task ContinuousOperations_ShouldNot_ShowPerformanceDegradation()
    {
        var iterations = 500;
        var monitor = new PerformanceMonitor(_context.LoggerFactory.CreateLogger<PerformanceMonitor>());

        // 预热
        for (int i = 0; i < 10; i++)
        {
            await PerformTypicalOperationsAsync(monitor);
        }

        // 记录基准性能
        var baselineTimings = new List<double>();
        for (int i = 0; i < 50; i++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await PerformTypicalOperationsAsync(monitor);
            sw.Stop();
            baselineTimings.Add(sw.Elapsed.TotalMilliseconds);
        }

        var baselineAverage = baselineTimings.Average();

        // 执行大量操作
        for (int i = 0; i < iterations; i++)
        {
            await PerformTypicalOperationsAsync(monitor);
        }

        // 测量后期性能
        var laterTimings = new List<double>();
        for (int i = 0; i < 50; i++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await PerformTypicalOperationsAsync(monitor);
            sw.Stop();
            laterTimings.Add(sw.Elapsed.TotalMilliseconds);
        }

        var laterAverage = laterTimings.Average();

        _logger.LogInformation("基准平均耗时: {Baseline:F2} ms", baselineAverage);
        _logger.LogInformation("后期平均耗时: {Later:F2} ms", laterAverage);

        var degradation = ((laterAverage - baselineAverage) / baselineAverage) * 100;
        _logger.LogInformation("性能变化: {Degradation:F2}%", degradation);

        // 性能不应降低超过20%
        degradation.Should().BeLessThan(20, "性能退化不应超过20%");
    }

    /// <summary>
    /// 资源清理测试 - 验证资源正确释放
    /// </summary>
    [Fact]
    [Trait("Category", "Stability")]
    [Trait("Duration", "Short")]
    public async Task RepeatedResourceAllocation_ShouldClean_ProperlyAsync()
    {
        var iterations = 500;
        var monitor = new PerformanceMonitor(_context.LoggerFactory.CreateLogger<PerformanceMonitor>());

        var initialHandleCount = System.Diagnostics.Process.GetCurrentProcess().HandleCount;
        var initialThreadCount = System.Diagnostics.Process.GetCurrentProcess().Threads.Count;

        _logger.LogInformation("初始句柄数: {Handles}, 初始线程数: {Threads}",
            initialHandleCount, initialThreadCount);

        for (int i = 0; i < iterations; i++)
        {
            var authManager = new AuthenticationManager(
                _context.LoggerFactory.CreateLogger<AuthenticationManager>()
            );

            await authManager.InitializeAsync();
            authManager.Dispose();

            if (i % 100 == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        await Task.Delay(1000); // 等待清理完成

        var finalHandleCount = System.Diagnostics.Process.GetCurrentProcess().HandleCount;
        var finalThreadCount = System.Diagnostics.Process.GetCurrentProcess().Threads.Count;

        _logger.LogInformation("最终句柄数: {Handles}, 最终线程数: {Threads}",
            finalHandleCount, finalThreadCount);

        var handleIncrease = finalHandleCount - initialHandleCount;
        var threadIncrease = finalThreadCount - initialThreadCount;

        _logger.LogInformation("句柄增长: {Handles}, 线程增长: {Threads}",
            handleIncrease, threadIncrease);

        // 句柄和线程数不应显著增长
        handleIncrease.Should().BeLessThan(50, "句柄泄漏检测");
        threadIncrease.Should().BeLessThan(10, "线程泄漏检测");
    }

    /// <summary>
    /// 异常恢复测试 - 验证系统从异常中恢复
    /// </summary>
    [Fact]
    [Trait("Category", "Stability")]
    [Trait("Duration", "Short")]
    public async Task System_Should_RecoverFrom_Exceptions()
    {
        var successCount = 0;
        var failureCount = 0;
        var recoveryCount = 0;

        var monitor = new PerformanceMonitor(_context.LoggerFactory.CreateLogger<PerformanceMonitor>());

        for (int i = 0; i < 100; i++)
        {
            try
            {
                if (i % 10 == 5) // 模拟周期性故障
                {
                    throw new InvalidOperationException("模拟故障");
                }

                await PerformTypicalOperationsAsync(monitor);

                if (failureCount > 0 && successCount > failureCount)
                {
                    recoveryCount++;
                }

                successCount++;
            }
            catch (InvalidOperationException)
            {
                failureCount++;

                // 等待后重试
                await Task.Delay(100);
            }
        }

        _logger.LogInformation("成功: {Success}, 失败: {Failure}, 恢复: {Recovery}",
            successCount, failureCount, recoveryCount);

        successCount.Should().BeGreaterThan(failureCount, "系统应该能够从故障中恢复");
        recoveryCount.Should().BeGreaterThan(0, "应该观察到至少一次恢复");
    }

    /// <summary>
    /// 并发压力测试 - 验证系统在高并发下的稳定性
    /// </summary>
    [Fact]
    [Trait("Category", "Stability")]
    [Trait("Duration", "Medium")]
    public async Task System_Should_HandleHighConcurrency_Stably()
    {
        var concurrentTasks = 50;
        var operationsPerTask = 20;
        var monitor = new PerformanceMonitor(_context.LoggerFactory.CreateLogger<PerformanceMonitor>());

        var successCounts = new int[concurrentTasks];
        var failureCounts = new int[concurrentTasks];

        _logger.LogInformation("开始并发压力测试: {Tasks} 任务, 每任务 {Operations} 操作",
            concurrentTasks, operationsPerTask);

        var startTime = DateTime.Now;

        var tasks = Enumerable.Range(0, concurrentTasks).Select(async taskId =>
        {
            for (int i = 0; i < operationsPerTask; i++)
            {
                try
                {
                    await PerformTypicalOperationsAsync(monitor);
                    Interlocked.Increment(ref successCounts[taskId]);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "任务 {TaskId} 操作失败", taskId);
                    Interlocked.Increment(ref failureCounts[taskId]);
                }
            }
        }).ToArray();

        await Task.WhenAll(tasks);

        var elapsed = DateTime.Now - startTime;
        var totalSuccess = successCounts.Sum();
        var totalFailure = failureCounts.Sum();
        var totalOperations = totalSuccess + totalFailure;

        _logger.LogInformation("并发测试完成");
        _logger.LogInformation("耗时: {Elapsed}", elapsed);
        _logger.LogInformation("总操作: {Total}, 成功: {Success}, 失败: {Failure}",
            totalOperations, totalSuccess, totalFailure);
        _logger.LogInformation("吞吐量: {Throughput:F2} 操作/秒",
            totalOperations / elapsed.TotalSeconds);

        // 验证
        totalSuccess.Should().BeGreaterThan(0, "应该有成功的操作");
        var errorRate = totalFailure / (double)totalOperations;
        errorRate.Should().BeLessThan(0.05, "错误率应低于5%");
    }

    /// <summary>
    /// 数据一致性测试 - 验证并发操作的数据一致性
    /// </summary>
    [Fact]
    [Trait("Category", "Stability")]
    [Trait("Duration", "Short")]
    public async Task ConcurrentOperations_Should_MaintainDataConsistency()
    {
        var securityManager = _context.GetService<ISecurityManager>();
        await securityManager.InitializeAsync();

        var concurrentWrites = 20;
        var dataIds = new System.Collections.Concurrent.ConcurrentBag<string>();

        // 并发写入
        var writeTasks = Enumerable.Range(0, concurrentWrites).Select(async i =>
        {
            var data = System.Text.Encoding.UTF8.GetBytes($"Test data {i}");
            var id = await securityManager.StoreSecureDataAsync(data);
            dataIds.Add(id);
        }).ToArray();

        await Task.WhenAll(writeTasks);

        // 验证所有数据可读取
        var readTasks = dataIds.Select(async id =>
        {
            var data = await securityManager.RetrieveSecureDataAsync(id);
            return data;
        }).ToArray();

        var results = await Task.WhenAll(readTasks);

        // 清理
        var cleanupTasks = dataIds.Select(id => securityManager.ClearSecureDataAsync(id)).ToArray();
        await Task.WhenAll(cleanupTasks);

        await securityManager.DisposeAsync();

        // 验证
        results.Should().NotContainNulls("所有数据应该可读取");
        results.Should().HaveCount(concurrentWrites, "应该读取所有写入的数据");
    }

    /// <summary>
    /// 大数据处理测试 - 验证系统处理大量数据的能力
    /// </summary>
    [Fact]
    [Trait("Category", "Stability")]
    [Trait("Duration", "Medium")]
    public async Task System_Should_HandleLargeData_Efficiently()
    {
        var securityManager = _context.GetService<ISecurityManager>();
        await securityManager.InitializeAsync();

        var dataSizes = new[] { 1024, 10 * 1024, 100 * 1024, 1024 * 1024 }; // 1KB, 10KB, 100KB, 1MB
        var monitor = new PerformanceMonitor(_context.LoggerFactory.CreateLogger<PerformanceMonitor>());

        foreach (var size in dataSizes)
        {
            var data = new byte[size];
            new Random().NextBytes(data);

            string? dataId = null;

            try
            {
                // 存储
                using (var storeTimer = monitor.BeginOperation($"Store_{size}"))
                {
                    dataId = await securityManager.StoreSecureDataAsync(data);
                    storeTimer.MarkSuccess();
                }

                // 检索
                byte[]? retrieved = null;
                using (var retrieveTimer = monitor.BeginOperation($"Retrieve_{size}"))
                {
                    retrieved = await securityManager.RetrieveSecureDataAsync(dataId);
                    retrieveTimer.MarkSuccess();
                }

                // 验证
                retrieved.Should().NotBeNull();
                retrieved.Should().HaveCount(size);

                var storeStats = monitor.GetOperationStats($"Store_{size}");
                var retrieveStats = monitor.GetOperationStats($"Retrieve_{size}");

                _logger.LogInformation(
                    "大小: {Size} bytes - 存储: {StoreTime:F2}ms, 检索: {RetrieveTime:F2}ms",
                    size, storeStats.AverageDuration, retrieveStats.AverageDuration);

                // 性能断言 - 1MB数据应在合理时间内处理
                if (size == 1024 * 1024)
                {
                    storeStats.AverageDuration.Should().BeLessThan(1000, "1MB数据存储应在1秒内完成");
                    retrieveStats.AverageDuration.Should().BeLessThan(1000, "1MB数据检索应在1秒内完成");
                }
            }
            finally
            {
                if (dataId != null)
                {
                    await securityManager.ClearSecureDataAsync(dataId);
                }
            }
        }

        await securityManager.DisposeAsync();
    }

    /// <summary>
    /// 执行典型操作
    /// </summary>
    private async Task PerformTypicalOperationsAsync(PerformanceMonitor monitor)
    {
        using var timer = monitor.BeginOperation("TypicalOperation");

        // 模拟认证
        var authManager = new AuthenticationManager(
            _context.LoggerFactory.CreateLogger<AuthenticationManager>()
        );

        await authManager.InitializeAsync();

        // 模拟安全操作
        var securityManager = _context.GetService<ISecurityManager>();
        await securityManager.InitializeAsync();

        var testData = System.Text.Encoding.UTF8.GetBytes("Test data for stability testing");
        var dataId = await securityManager.StoreSecureDataAsync(testData);

        var retrieved = await securityManager.RetrieveSecureDataAsync(dataId);
        retrieved.Should().NotBeNull();

        await securityManager.ClearSecureDataAsync(dataId);

        // 清理
        authManager.Dispose();
        await securityManager.DisposeAsync();

        timer.MarkSuccess();
    }
}
