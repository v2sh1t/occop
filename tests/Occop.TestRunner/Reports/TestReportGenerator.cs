using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Occop.TestRunner.Reports;

/// <summary>
/// 测试报告格式
/// </summary>
public enum ReportFormat
{
    /// <summary>
    /// 纯文本
    /// </summary>
    Text,

    /// <summary>
    /// Markdown
    /// </summary>
    Markdown,

    /// <summary>
    /// HTML
    /// </summary>
    Html,

    /// <summary>
    /// JSON
    /// </summary>
    Json
}

/// <summary>
/// 综合测试报告生成器
/// </summary>
public class TestReportGenerator
{
    private readonly ILogger<TestReportGenerator> _logger;

    public TestReportGenerator(ILogger<TestReportGenerator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 生成测试报告
    /// </summary>
    public async Task<string> GenerateReportAsync(
        Dictionary<TestType, TestRunResult> results,
        ReportFormat format,
        string? outputPath = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("生成 {Format} 格式的测试报告...", format);

        var report = format switch
        {
            ReportFormat.Text => GenerateTextReport(results),
            ReportFormat.Markdown => GenerateMarkdownReport(results),
            ReportFormat.Html => GenerateHtmlReport(results),
            ReportFormat.Json => GenerateJsonReport(results),
            _ => throw new ArgumentException($"不支持的报告格式: {format}")
        };

        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(outputPath, report, cancellationToken);
            _logger.LogInformation("测试报告已保存到: {OutputPath}", outputPath);
        }

        return report;
    }

    /// <summary>
    /// 生成文本格式报告
    /// </summary>
    private string GenerateTextReport(Dictionary<TestType, TestRunResult> results)
    {
        var sb = new StringBuilder();

        sb.AppendLine("===============================================");
        sb.AppendLine("           OCCOP 测试运行报告");
        sb.AppendLine("===============================================");
        sb.AppendLine();
        sb.AppendLine($"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        // 总体摘要
        var totalPassed = results.Values.Sum(r => r.PassedTests);
        var totalFailed = results.Values.Sum(r => r.FailedTests);
        var totalSkipped = results.Values.Sum(r => r.SkippedTests);
        var totalTests = results.Values.Sum(r => r.TotalTests);
        var totalDuration = TimeSpan.FromMilliseconds(results.Values.Sum(r => r.Duration.TotalMilliseconds));

        sb.AppendLine("总体摘要:");
        sb.AppendLine($"  测试总数: {totalTests}");
        sb.AppendLine($"  通过: {totalPassed} ({GetPercentage(totalPassed, totalTests):F2}%)");
        sb.AppendLine($"  失败: {totalFailed} ({GetPercentage(totalFailed, totalTests):F2}%)");
        sb.AppendLine($"  跳过: {totalSkipped} ({GetPercentage(totalSkipped, totalTests):F2}%)");
        sb.AppendLine($"  总耗时: {totalDuration:hh\\:mm\\:ss}");
        sb.AppendLine($"  整体状态: {(totalFailed == 0 ? "✓ 通过" : "✗ 失败")}");
        sb.AppendLine();

        // 各类测试详情
        sb.AppendLine("各类测试详情:");
        sb.AppendLine("-----------------------------------------------");

        foreach (var (testType, result) in results.OrderBy(r => r.Key))
        {
            sb.AppendLine();
            sb.AppendLine($"[{testType}]");
            sb.AppendLine($"  状态: {GetStatusIcon(result.Status)} {result.Status}");
            sb.AppendLine($"  测试数: {result.TotalTests}");
            sb.AppendLine($"  通过: {result.PassedTests}");
            sb.AppendLine($"  失败: {result.FailedTests}");
            sb.AppendLine($"  跳过: {result.SkippedTests}");
            sb.AppendLine($"  耗时: {result.Duration:mm\\:ss\\.fff}");

            if (result.CoveragePercentage.HasValue)
            {
                sb.AppendLine($"  覆盖率: {result.CoveragePercentage:F2}%");
            }

            if (!string.IsNullOrWhiteSpace(result.CoverageReportPath))
            {
                sb.AppendLine($"  覆盖率报告: {result.CoverageReportPath}");
            }

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                sb.AppendLine($"  错误: {result.ErrorMessage}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("===============================================");

        return sb.ToString();
    }

    /// <summary>
    /// 生成Markdown格式报告
    /// </summary>
    private string GenerateMarkdownReport(Dictionary<TestType, TestRunResult> results)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Occop 测试运行报告");
        sb.AppendLine();
        sb.AppendLine($"**生成时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        // 总体摘要
        var totalPassed = results.Values.Sum(r => r.PassedTests);
        var totalFailed = results.Values.Sum(r => r.FailedTests);
        var totalSkipped = results.Values.Sum(r => r.SkippedTests);
        var totalTests = results.Values.Sum(r => r.TotalTests);
        var totalDuration = TimeSpan.FromMilliseconds(results.Values.Sum(r => r.Duration.TotalMilliseconds));

        var overallStatus = totalFailed == 0 ? "✅ 通过" : "❌ 失败";

        sb.AppendLine("## 总体摘要");
        sb.AppendLine();
        sb.AppendLine($"**状态**: {overallStatus}");
        sb.AppendLine();
        sb.AppendLine("| 指标 | 数量 | 百分比 |");
        sb.AppendLine("|------|------|--------|");
        sb.AppendLine($"| 测试总数 | {totalTests} | 100% |");
        sb.AppendLine($"| ✅ 通过 | {totalPassed} | {GetPercentage(totalPassed, totalTests):F2}% |");
        sb.AppendLine($"| ❌ 失败 | {totalFailed} | {GetPercentage(totalFailed, totalTests):F2}% |");
        sb.AppendLine($"| ⊘ 跳过 | {totalSkipped} | {GetPercentage(totalSkipped, totalTests):F2}% |");
        sb.AppendLine($"| ⏱️ 总耗时 | {totalDuration:hh\\:mm\\:ss} | - |");
        sb.AppendLine();

        // 各类测试详情
        sb.AppendLine("## 测试详情");
        sb.AppendLine();
        sb.AppendLine("| 测试类型 | 状态 | 通过/总数 | 失败 | 跳过 | 耗时 | 覆盖率 |");
        sb.AppendLine("|----------|------|-----------|------|------|------|--------|");

        foreach (var (testType, result) in results.OrderBy(r => r.Key))
        {
            var statusIcon = GetStatusIcon(result.Status);
            var passRate = $"{result.PassedTests}/{result.TotalTests}";
            var coverage = result.CoveragePercentage.HasValue ? $"{result.CoveragePercentage:F2}%" : "N/A";

            sb.AppendLine($"| {testType} | {statusIcon} {result.Status} | {passRate} | {result.FailedTests} | {result.SkippedTests} | {result.Duration:mm\\:ss} | {coverage} |");
        }

        sb.AppendLine();

        // 失败详情
        var failedResults = results.Where(r => r.Value.FailedTests > 0 || r.Value.Status == TestRunStatus.Failed).ToList();
        if (failedResults.Any())
        {
            sb.AppendLine("## ❌ 失败详情");
            sb.AppendLine();

            foreach (var (testType, result) in failedResults)
            {
                sb.AppendLine($"### {testType}");
                sb.AppendLine();

                if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    sb.AppendLine("```");
                    sb.AppendLine(result.ErrorMessage);
                    sb.AppendLine("```");
                    sb.AppendLine();
                }
            }
        }

        // 覆盖率报告链接
        var reportsWithCoverage = results.Where(r => !string.IsNullOrWhiteSpace(r.Value.CoverageReportPath)).ToList();
        if (reportsWithCoverage.Any())
        {
            sb.AppendLine("## 📊 覆盖率报告");
            sb.AppendLine();

            foreach (var (testType, result) in reportsWithCoverage)
            {
                sb.AppendLine($"- **{testType}**: [{result.CoveragePercentage:F2}%]({result.CoverageReportPath})");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// 生成HTML格式报告
    /// </summary>
    private string GenerateHtmlReport(Dictionary<TestType, TestRunResult> results)
    {
        var totalPassed = results.Values.Sum(r => r.PassedTests);
        var totalFailed = results.Values.Sum(r => r.FailedTests);
        var totalSkipped = results.Values.Sum(r => r.SkippedTests);
        var totalTests = results.Values.Sum(r => r.TotalTests);
        var totalDuration = TimeSpan.FromMilliseconds(results.Values.Sum(r => r.Duration.TotalMilliseconds));

        var overallStatus = totalFailed == 0 ? "通过" : "失败";
        var statusClass = totalFailed == 0 ? "success" : "failure";

        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"zh-CN\">");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset=\"UTF-8\">");
        sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine("    <title>Occop 测试报告</title>");
        sb.AppendLine("    <style>");
        sb.AppendLine("        body { font-family: Arial, sans-serif; margin: 20px; background: #f5f5f5; }");
        sb.AppendLine("        .container { max-width: 1200px; margin: 0 auto; background: white; padding: 30px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }");
        sb.AppendLine("        h1 { color: #333; border-bottom: 3px solid #4CAF50; padding-bottom: 10px; }");
        sb.AppendLine("        h2 { color: #666; margin-top: 30px; }");
        sb.AppendLine("        .summary { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 20px; margin: 20px 0; }");
        sb.AppendLine("        .summary-card { background: #f9f9f9; padding: 20px; border-radius: 8px; border-left: 4px solid #4CAF50; }");
        sb.AppendLine("        .summary-card.failure { border-left-color: #f44336; }");
        sb.AppendLine("        .summary-card h3 { margin: 0 0 10px 0; color: #666; font-size: 14px; }");
        sb.AppendLine("        .summary-card .value { font-size: 32px; font-weight: bold; color: #333; }");
        sb.AppendLine("        .status.success { color: #4CAF50; }");
        sb.AppendLine("        .status.failure { color: #f44336; }");
        sb.AppendLine("        table { width: 100%; border-collapse: collapse; margin: 20px 0; }");
        sb.AppendLine("        th, td { padding: 12px; text-align: left; border-bottom: 1px solid #ddd; }");
        sb.AppendLine("        th { background-color: #4CAF50; color: white; }");
        sb.AppendLine("        tr:hover { background-color: #f5f5f5; }");
        sb.AppendLine("        .progress-bar { width: 100%; height: 20px; background: #e0e0e0; border-radius: 10px; overflow: hidden; }");
        sb.AppendLine("        .progress-fill { height: 100%; background: #4CAF50; transition: width 0.3s; }");
        sb.AppendLine("        .error-box { background: #ffebee; border-left: 4px solid #f44336; padding: 15px; margin: 10px 0; border-radius: 4px; }");
        sb.AppendLine("        .timestamp { color: #999; font-size: 14px; }");
        sb.AppendLine("    </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("    <div class=\"container\">");
        sb.AppendLine("        <h1>Occop 测试运行报告</h1>");
        sb.AppendLine($"        <p class=\"timestamp\">生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");
        sb.AppendLine();

        // 总体摘要卡片
        sb.AppendLine("        <div class=\"summary\">");
        sb.AppendLine($"            <div class=\"summary-card {statusClass}\">");
        sb.AppendLine("                <h3>整体状态</h3>");
        sb.AppendLine($"                <div class=\"value status {statusClass}\">{overallStatus}</div>");
        sb.AppendLine("            </div>");
        sb.AppendLine("            <div class=\"summary-card\">");
        sb.AppendLine("                <h3>测试总数</h3>");
        sb.AppendLine($"                <div class=\"value\">{totalTests}</div>");
        sb.AppendLine("            </div>");
        sb.AppendLine("            <div class=\"summary-card\">");
        sb.AppendLine("                <h3>通过</h3>");
        sb.AppendLine($"                <div class=\"value status success\">{totalPassed}</div>");
        sb.AppendLine($"                <div>{GetPercentage(totalPassed, totalTests):F2}%</div>");
        sb.AppendLine("            </div>");
        sb.AppendLine("            <div class=\"summary-card failure\">");
        sb.AppendLine("                <h3>失败</h3>");
        sb.AppendLine($"                <div class=\"value status failure\">{totalFailed}</div>");
        sb.AppendLine($"                <div>{GetPercentage(totalFailed, totalTests):F2}%</div>");
        sb.AppendLine("            </div>");
        sb.AppendLine("            <div class=\"summary-card\">");
        sb.AppendLine("                <h3>总耗时</h3>");
        sb.AppendLine($"                <div class=\"value\" style=\"font-size: 24px;\">{totalDuration:hh\\:mm\\:ss}</div>");
        sb.AppendLine("            </div>");
        sb.AppendLine("        </div>");

        // 进度条
        var passPercentage = GetPercentage(totalPassed, totalTests);
        sb.AppendLine("        <div class=\"progress-bar\">");
        sb.AppendLine($"            <div class=\"progress-fill\" style=\"width: {passPercentage}%\"></div>");
        sb.AppendLine("        </div>");
        sb.AppendLine();

        // 详细测试结果表格
        sb.AppendLine("        <h2>测试详情</h2>");
        sb.AppendLine("        <table>");
        sb.AppendLine("            <thead>");
        sb.AppendLine("                <tr>");
        sb.AppendLine("                    <th>测试类型</th>");
        sb.AppendLine("                    <th>状态</th>");
        sb.AppendLine("                    <th>通过</th>");
        sb.AppendLine("                    <th>失败</th>");
        sb.AppendLine("                    <th>跳过</th>");
        sb.AppendLine("                    <th>总数</th>");
        sb.AppendLine("                    <th>耗时</th>");
        sb.AppendLine("                    <th>覆盖率</th>");
        sb.AppendLine("                </tr>");
        sb.AppendLine("            </thead>");
        sb.AppendLine("            <tbody>");

        foreach (var (testType, result) in results.OrderBy(r => r.Key))
        {
            var rowClass = result.Status == TestRunStatus.Failed || result.FailedTests > 0 ? "class=\"failure\"" : "";
            var statusIcon = result.Status switch
            {
                TestRunStatus.Completed when result.FailedTests == 0 => "✓",
                TestRunStatus.Completed => "⚠",
                TestRunStatus.Failed => "✗",
                TestRunStatus.Skipped => "⊘",
                _ => "?"
            };

            sb.AppendLine($"                <tr {rowClass}>");
            sb.AppendLine($"                    <td><strong>{testType}</strong></td>");
            sb.AppendLine($"                    <td>{statusIcon} {result.Status}</td>");
            sb.AppendLine($"                    <td>{result.PassedTests}</td>");
            sb.AppendLine($"                    <td>{result.FailedTests}</td>");
            sb.AppendLine($"                    <td>{result.SkippedTests}</td>");
            sb.AppendLine($"                    <td>{result.TotalTests}</td>");
            sb.AppendLine($"                    <td>{result.Duration:mm\\:ss}</td>");
            sb.AppendLine($"                    <td>{(result.CoveragePercentage.HasValue ? $"{result.CoveragePercentage:F2}%" : "N/A")}</td>");
            sb.AppendLine("                </tr>");
        }

        sb.AppendLine("            </tbody>");
        sb.AppendLine("        </table>");

        // 失败详情
        var failedResults = results.Where(r => r.Value.FailedTests > 0 || r.Value.Status == TestRunStatus.Failed).ToList();
        if (failedResults.Any())
        {
            sb.AppendLine("        <h2>失败详情</h2>");

            foreach (var (testType, result) in failedResults)
            {
                sb.AppendLine("        <div class=\"error-box\">");
                sb.AppendLine($"            <h3>{testType}</h3>");

                if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    sb.AppendLine($"            <pre>{System.Web.HttpUtility.HtmlEncode(result.ErrorMessage)}</pre>");
                }

                sb.AppendLine("        </div>");
            }
        }

        sb.AppendLine("    </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    /// <summary>
    /// 生成JSON格式报告
    /// </summary>
    private string GenerateJsonReport(Dictionary<TestType, TestRunResult> results)
    {
        var report = new
        {
            GeneratedAt = DateTime.Now,
            Summary = new
            {
                TotalTests = results.Values.Sum(r => r.TotalTests),
                PassedTests = results.Values.Sum(r => r.PassedTests),
                FailedTests = results.Values.Sum(r => r.FailedTests),
                SkippedTests = results.Values.Sum(r => r.SkippedTests),
                TotalDuration = TimeSpan.FromMilliseconds(results.Values.Sum(r => r.Duration.TotalMilliseconds)),
                IsSuccess = results.Values.All(r => r.IsSuccess || r.Status == TestRunStatus.Skipped)
            },
            TestResults = results.Select(r => new
            {
                TestType = r.Key.ToString(),
                Status = r.Value.Status.ToString(),
                StartTime = r.Value.StartTime,
                EndTime = r.Value.EndTime,
                Duration = r.Value.Duration,
                TotalTests = r.Value.TotalTests,
                PassedTests = r.Value.PassedTests,
                FailedTests = r.Value.FailedTests,
                SkippedTests = r.Value.SkippedTests,
                CoveragePercentage = r.Value.CoveragePercentage,
                CoverageReportPath = r.Value.CoverageReportPath,
                ErrorMessage = r.Value.ErrorMessage,
                IsSuccess = r.Value.IsSuccess
            }).ToList()
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return JsonSerializer.Serialize(report, options);
    }

    /// <summary>
    /// 计算百分比
    /// </summary>
    private double GetPercentage(int value, int total)
    {
        return total > 0 ? (value * 100.0 / total) : 0;
    }

    /// <summary>
    /// 获取状态图标
    /// </summary>
    private string GetStatusIcon(TestRunStatus status)
    {
        return status switch
        {
            TestRunStatus.Completed => "✓",
            TestRunStatus.Failed => "✗",
            TestRunStatus.Skipped => "⊘",
            TestRunStatus.Running => "⟳",
            TestRunStatus.Pending => "○",
            TestRunStatus.Cancelled => "⊗",
            _ => "?"
        };
    }
}
