using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Occop.TestRunner;

/// <summary>
/// 测试运行结果
/// </summary>
public class TestRunResult
{
    /// <summary>
    /// 测试类型
    /// </summary>
    public TestType TestType { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    public TestRunStatus Status { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 运行时长
    /// </summary>
    public TimeSpan Duration => EndTime.HasValue ? EndTime.Value - StartTime : TimeSpan.Zero;

    /// <summary>
    /// 测试总数
    /// </summary>
    public int TotalTests { get; set; }

    /// <summary>
    /// 通过的测试数
    /// </summary>
    public int PassedTests { get; set; }

    /// <summary>
    /// 失败的测试数
    /// </summary>
    public int FailedTests { get; set; }

    /// <summary>
    /// 跳过的测试数
    /// </summary>
    public int SkippedTests { get; set; }

    /// <summary>
    /// 输出信息
    /// </summary>
    public string Output { get; set; } = string.Empty;

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 覆盖率百分比
    /// </summary>
    public double? CoveragePercentage { get; set; }

    /// <summary>
    /// 覆盖率报告路径
    /// </summary>
    public string? CoverageReportPath { get; set; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess => Status == TestRunStatus.Completed && FailedTests == 0;
}

/// <summary>
/// 测试调度器 - 管理和调度各类测试的执行
/// </summary>
public class Scheduler
{
    private readonly ILogger<Scheduler> _logger;
    private readonly TestRunConfig _config;
    private readonly Dictionary<TestType, TestRunResult> _results;
    private readonly SemaphoreSlim _semaphore;

    public Scheduler(ILogger<Scheduler> logger, TestRunConfig config)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _results = new Dictionary<TestType, TestRunResult>();
        _semaphore = new SemaphoreSlim(_config.MaxParallelism);
    }

    /// <summary>
    /// 运行所有配置的测试
    /// </summary>
    public async Task<Dictionary<TestType, TestRunResult>> RunAllTestsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("开始运行测试套件...");
        _logger.LogInformation("测试类型: {TestTypes}", _config.TestTypes);
        _logger.LogInformation("并行度: {MaxParallelism}", _config.MaxParallelism);

        var testTypes = GetTestTypesToRun();
        var tasks = new List<Task>();

        foreach (var testType in testTypes)
        {
            if (_config.RunInParallel)
            {
                tasks.Add(RunTestTypeAsync(testType, cancellationToken));
            }
            else
            {
                await RunTestTypeAsync(testType, cancellationToken);
            }
        }

        if (_config.RunInParallel && tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
        }

        _logger.LogInformation("所有测试运行完成");
        LogSummary();

        return _results;
    }

    /// <summary>
    /// 运行特定类型的测试
    /// </summary>
    private async Task RunTestTypeAsync(TestType testType, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            var result = new TestRunResult
            {
                TestType = testType,
                Status = TestRunStatus.Running,
                StartTime = DateTime.Now
            };

            _results[testType] = result;

            _logger.LogInformation("开始运行 {TestType} 测试...", testType);

            try
            {
                var projectPath = GetTestProjectPath(testType);
                if (projectPath == null)
                {
                    _logger.LogWarning("未找到 {TestType} 测试项目，跳过", testType);
                    result.Status = TestRunStatus.Skipped;
                    result.EndTime = DateTime.Now;
                    return;
                }

                // 运行dotnet test
                var (exitCode, output, error) = await RunDotnetTestAsync(projectPath, cancellationToken);

                result.Output = output;
                result.EndTime = DateTime.Now;

                if (exitCode == 0)
                {
                    result.Status = TestRunStatus.Completed;
                    ParseTestResults(result, output);
                    _logger.LogInformation("{TestType} 测试完成: {Passed}/{Total} 通过",
                        testType, result.PassedTests, result.TotalTests);
                }
                else
                {
                    result.Status = TestRunStatus.Failed;
                    result.ErrorMessage = error;
                    ParseTestResults(result, output);
                    _logger.LogError("{TestType} 测试失败: {Failed}/{Total} 失败",
                        testType, result.FailedTests, result.TotalTests);
                }

                // 生成覆盖率报告
                if (_config.GenerateCoverageReport && result.IsSuccess)
                {
                    await GenerateCoverageReportAsync(testType, projectPath, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                result.Status = TestRunStatus.Cancelled;
                result.EndTime = DateTime.Now;
                _logger.LogWarning("{TestType} 测试被取消", testType);
                throw;
            }
            catch (Exception ex)
            {
                result.Status = TestRunStatus.Failed;
                result.ErrorMessage = ex.Message;
                result.EndTime = DateTime.Now;
                _logger.LogError(ex, "{TestType} 测试执行出错", testType);

                if (_config.FailFast)
                {
                    throw;
                }
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// 运行dotnet test命令
    /// </summary>
    private async Task<(int exitCode, string output, string error)> RunDotnetTestAsync(
        string projectPath,
        CancellationToken cancellationToken)
    {
        var arguments = new StringBuilder($"test \"{projectPath}\"");

        arguments.Append($" --verbosity {_config.Verbosity}");
        arguments.Append($" --results-directory \"{_config.OutputDirectory}\"");
        arguments.Append(" --logger \"trx\"");

        if (_config.GenerateCoverageReport)
        {
            arguments.Append(" --collect:\"XPlat Code Coverage\"");
        }

        if (!string.IsNullOrWhiteSpace(_config.Filter))
        {
            arguments.Append($" --filter \"{_config.Filter}\"");
        }

        if (_config.CollectDiagnostics)
        {
            arguments.Append(" --diag \"diagnostics.log\"");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arguments.ToString(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        using var process = new Process { StartInfo = startInfo };

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
                _logger.LogDebug(e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
                _logger.LogDebug(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var registration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(true);
                }
            }
            catch { }
        });

        await process.WaitForExitAsync(cancellationToken);

        return (process.ExitCode, outputBuilder.ToString(), errorBuilder.ToString());
    }

    /// <summary>
    /// 解析测试结果
    /// </summary>
    private void ParseTestResults(TestRunResult result, string output)
    {
        // 解析类似 "Passed: 10, Failed: 0, Skipped: 2, Total: 12" 的输出
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (line.Contains("Passed:") || line.Contains("通过:"))
            {
                var parts = line.Split(',');
                foreach (var part in parts)
                {
                    var trimmed = part.Trim();
                    if (trimmed.StartsWith("Passed:") || trimmed.StartsWith("通过:"))
                    {
                        var value = ExtractNumber(trimmed);
                        if (value.HasValue) result.PassedTests = value.Value;
                    }
                    else if (trimmed.StartsWith("Failed:") || trimmed.StartsWith("失败:"))
                    {
                        var value = ExtractNumber(trimmed);
                        if (value.HasValue) result.FailedTests = value.Value;
                    }
                    else if (trimmed.StartsWith("Skipped:") || trimmed.StartsWith("跳过:"))
                    {
                        var value = ExtractNumber(trimmed);
                        if (value.HasValue) result.SkippedTests = value.Value;
                    }
                    else if (trimmed.StartsWith("Total:") || trimmed.StartsWith("总计:"))
                    {
                        var value = ExtractNumber(trimmed);
                        if (value.HasValue) result.TotalTests = value.Value;
                    }
                }
            }
        }

        // 如果没有解析到总数，计算它
        if (result.TotalTests == 0)
        {
            result.TotalTests = result.PassedTests + result.FailedTests + result.SkippedTests;
        }
    }

    /// <summary>
    /// 从字符串中提取数字
    /// </summary>
    private int? ExtractNumber(string text)
    {
        var numbers = new string(text.Where(char.IsDigit).ToArray());
        return int.TryParse(numbers, out var value) ? value : null;
    }

    /// <summary>
    /// 生成覆盖率报告
    /// </summary>
    private async Task GenerateCoverageReportAsync(
        TestType testType,
        string projectPath,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("正在为 {TestType} 生成覆盖率报告...", testType);

            // 查找覆盖率文件
            var coverageFiles = Directory.GetFiles(_config.OutputDirectory, "coverage.cobertura.xml", SearchOption.AllDirectories);

            if (coverageFiles.Length == 0)
            {
                _logger.LogWarning("未找到覆盖率文件");
                return;
            }

            var reportPath = Path.Combine(_config.OutputDirectory, $"{testType}_Coverage");
            Directory.CreateDirectory(reportPath);

            var formats = string.Join(";", _config.CoverageReportFormats);
            var arguments = $"-reports:\"{coverageFiles[0]}\" -targetdir:\"{reportPath}\" -reporttypes:{formats}";

            var startInfo = new ProcessStartInfo
            {
                FileName = "reportgenerator",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync(cancellationToken);

                if (process.ExitCode == 0)
                {
                    var result = _results[testType];
                    result.CoverageReportPath = reportPath;

                    // 尝试解析覆盖率百分比
                    var summaryFile = Path.Combine(reportPath, "Summary.txt");
                    if (File.Exists(summaryFile))
                    {
                        var summary = await File.ReadAllTextAsync(summaryFile, cancellationToken);
                        var coverageMatch = System.Text.RegularExpressions.Regex.Match(summary, @"Line coverage: ([\d.]+)%");
                        if (coverageMatch.Success && double.TryParse(coverageMatch.Groups[1].Value, out var coverage))
                        {
                            result.CoveragePercentage = coverage;
                        }
                    }

                    _logger.LogInformation("覆盖率报告已生成: {ReportPath}", reportPath);
                }
                else
                {
                    _logger.LogWarning("覆盖率报告生成失败");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成覆盖率报告时出错");
        }
    }

    /// <summary>
    /// 获取要运行的测试类型列表
    /// </summary>
    private List<TestType> GetTestTypesToRun()
    {
        var types = new List<TestType>();

        if (_config.TestTypes.HasFlag(TestType.Unit))
            types.Add(TestType.Unit);

        if (_config.TestTypes.HasFlag(TestType.Integration))
            types.Add(TestType.Integration);

        if (_config.TestTypes.HasFlag(TestType.Performance))
            types.Add(TestType.Performance);

        if (_config.TestTypes.HasFlag(TestType.Security))
            types.Add(TestType.Security);

        if (_config.TestTypes.HasFlag(TestType.Stability))
            types.Add(TestType.Stability);

        // 按优先级排序
        return _config.Priority == TestPriority.Critical
            ? types.OrderByDescending(t => GetTestTypePriority(t)).ToList()
            : types;
    }

    /// <summary>
    /// 获取测试类型的优先级
    /// </summary>
    private int GetTestTypePriority(TestType testType)
    {
        return testType switch
        {
            TestType.Unit => 5,
            TestType.Security => 4,
            TestType.Integration => 3,
            TestType.Performance => 2,
            TestType.Stability => 1,
            _ => 0
        };
    }

    /// <summary>
    /// 获取测试项目路径
    /// </summary>
    private string? GetTestProjectPath(TestType testType)
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var testsDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));

        return testType switch
        {
            TestType.Unit => Path.Combine(testsDir, "Occop.Tests", "Occop.Tests.csproj"),
            TestType.Integration => Path.Combine(testsDir, "Occop.IntegrationTests", "Occop.IntegrationTests.csproj"),
            TestType.Performance => Path.Combine(testsDir, "Occop.PerformanceTests", "Occop.PerformanceTests.csproj"),
            TestType.Security => Path.Combine(testsDir, "Occop.SecurityTests", "Occop.SecurityTests.csproj"),
            TestType.Stability => Path.Combine(testsDir, "Occop.StabilityTests", "Occop.StabilityTests.csproj"),
            _ => null
        };
    }

    /// <summary>
    /// 记录测试摘要
    /// </summary>
    private void LogSummary()
    {
        _logger.LogInformation("========================================");
        _logger.LogInformation("测试运行摘要");
        _logger.LogInformation("========================================");

        var totalPassed = 0;
        var totalFailed = 0;
        var totalSkipped = 0;
        var totalTests = 0;

        foreach (var (testType, result) in _results)
        {
            totalPassed += result.PassedTests;
            totalFailed += result.FailedTests;
            totalSkipped += result.SkippedTests;
            totalTests += result.TotalTests;

            var statusIcon = result.Status switch
            {
                TestRunStatus.Completed when result.FailedTests == 0 => "✓",
                TestRunStatus.Completed => "⚠",
                TestRunStatus.Failed => "✗",
                TestRunStatus.Skipped => "⊘",
                _ => "?"
            };

            _logger.LogInformation("{Icon} {TestType}: {Passed}/{Total} 通过, 耗时 {Duration:mm\\:ss}",
                statusIcon, testType, result.PassedTests, result.TotalTests, result.Duration);

            if (result.CoveragePercentage.HasValue)
            {
                _logger.LogInformation("  覆盖率: {Coverage:F2}%", result.CoveragePercentage.Value);
            }
        }

        _logger.LogInformation("========================================");
        _logger.LogInformation("总计: {Passed}/{Total} 通过, {Failed} 失败, {Skipped} 跳过",
            totalPassed, totalTests, totalFailed, totalSkipped);
        _logger.LogInformation("========================================");
    }

    /// <summary>
    /// 获取整体测试结果
    /// </summary>
    public bool IsAllTestsPassed()
    {
        return _results.Values.All(r => r.IsSuccess || r.Status == TestRunStatus.Skipped);
    }
}
