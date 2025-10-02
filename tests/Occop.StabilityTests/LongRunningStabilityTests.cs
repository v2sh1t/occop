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
