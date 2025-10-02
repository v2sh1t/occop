using System.Text;

namespace Occop.IntegrationTests.Reports
{
    /// <summary>
    /// 测试报告生成器
    /// Test report generator
    /// </summary>
    public class TestReportGenerator
    {
        /// <summary>
        /// 测试结果
        /// Test result
        /// </summary>
        public class TestResult
        {
            public string TestName { get; set; } = string.Empty;
            public bool Passed { get; set; }
            public TimeSpan Duration { get; set; }
            public string? ErrorMessage { get; set; }
            public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
        }

        /// <summary>
        /// 测试套件结果
        /// Test suite result
        /// </summary>
        public class TestSuiteResult
        {
            public string SuiteName { get; set; } = string.Empty;
            public List<TestResult> Tests { get; set; } = new();
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }

            public int TotalTests => Tests.Count;
            public int PassedTests => Tests.Count(t => t.Passed);
            public int FailedTests => Tests.Count(t => !t.Passed);
            public double PassRate => TotalTests > 0 ? (double)PassedTests / TotalTests * 100 : 0;
            public TimeSpan TotalDuration => EndTime - StartTime;
        }

        /// <summary>
        /// 生成文本格式报告
        /// Generates a text format report
        /// </summary>
        public static string GenerateTextReport(TestSuiteResult suiteResult)
        {
            var sb = new StringBuilder();

            sb.AppendLine("=" .Repeat(80));
            sb.AppendLine($"测试报告 - {suiteResult.SuiteName}");
            sb.AppendLine("=" .Repeat(80));
            sb.AppendLine();

            // 摘要信息
            sb.AppendLine("摘要 Summary");
            sb.AppendLine("-" .Repeat(80));
            sb.AppendLine($"开始时间:     {suiteResult.StartTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"结束时间:     {suiteResult.EndTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"总耗时:       {suiteResult.TotalDuration.TotalSeconds:F2} 秒");
            sb.AppendLine($"总测试数:     {suiteResult.TotalTests}");
            sb.AppendLine($"通过测试:     {suiteResult.PassedTests}");
            sb.AppendLine($"失败测试:     {suiteResult.FailedTests}");
            sb.AppendLine($"通过率:       {suiteResult.PassRate:F2}%");
            sb.AppendLine();

            // 详细结果
            sb.AppendLine("详细结果 Detailed Results");
            sb.AppendLine("-" .Repeat(80));

            foreach (var test in suiteResult.Tests)
            {
                var status = test.Passed ? "✓ PASS" : "✗ FAIL";
                sb.AppendLine($"{status} {test.TestName}");
                sb.AppendLine($"    耗时: {test.Duration.TotalMilliseconds:F0} ms");
                sb.AppendLine($"    执行时间: {test.ExecutedAt:HH:mm:ss}");

                if (!test.Passed && !string.IsNullOrEmpty(test.ErrorMessage))
                {
                    sb.AppendLine($"    错误: {test.ErrorMessage}");
                }

                sb.AppendLine();
            }

            sb.AppendLine("=" .Repeat(80));

            return sb.ToString();
        }

        /// <summary>
        /// 生成Markdown格式报告
        /// Generates a Markdown format report
        /// </summary>
        public static string GenerateMarkdownReport(TestSuiteResult suiteResult)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"# 测试报告 - {suiteResult.SuiteName}");
            sb.AppendLine();

            // 摘要表格
            sb.AppendLine("## 摘要");
            sb.AppendLine();
            sb.AppendLine("| 指标 | 值 |");
            sb.AppendLine("|------|------|");
            sb.AppendLine($"| 开始时间 | {suiteResult.StartTime:yyyy-MM-dd HH:mm:ss} |");
            sb.AppendLine($"| 结束时间 | {suiteResult.EndTime:yyyy-MM-dd HH:mm:ss} |");
            sb.AppendLine($"| 总耗时 | {suiteResult.TotalDuration.TotalSeconds:F2} 秒 |");
            sb.AppendLine($"| 总测试数 | {suiteResult.TotalTests} |");
            sb.AppendLine($"| 通过测试 | {suiteResult.PassedTests} |");
            sb.AppendLine($"| 失败测试 | {suiteResult.FailedTests} |");
            sb.AppendLine($"| 通过率 | {suiteResult.PassRate:F2}% |");
            sb.AppendLine();

            // 测试结果表格
            sb.AppendLine("## 详细结果");
            sb.AppendLine();
            sb.AppendLine("| 状态 | 测试名称 | 耗时 (ms) | 错误信息 |");
            sb.AppendLine("|------|----------|----------|----------|");

            foreach (var test in suiteResult.Tests)
            {
                var status = test.Passed ? "✓" : "✗";
                var error = test.Passed ? "-" : (test.ErrorMessage ?? "未知错误");
                sb.AppendLine($"| {status} | {test.TestName} | {test.Duration.TotalMilliseconds:F0} | {error} |");
            }

            sb.AppendLine();

            return sb.ToString();
        }

        /// <summary>
        /// 生成HTML格式报告
        /// Generates an HTML format report
        /// </summary>
        public static string GenerateHtmlReport(TestSuiteResult suiteResult)
        {
            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine($"    <title>测试报告 - {suiteResult.SuiteName}</title>");
            sb.AppendLine("    <style>");
            sb.AppendLine("        body { font-family: Arial, sans-serif; margin: 20px; }");
            sb.AppendLine("        h1 { color: #333; }");
            sb.AppendLine("        table { border-collapse: collapse; width: 100%; margin: 20px 0; }");
            sb.AppendLine("        th, td { border: 1px solid #ddd; padding: 12px; text-align: left; }");
            sb.AppendLine("        th { background-color: #4CAF50; color: white; }");
            sb.AppendLine("        tr:nth-child(even) { background-color: #f2f2f2; }");
            sb.AppendLine("        .pass { color: green; font-weight: bold; }");
            sb.AppendLine("        .fail { color: red; font-weight: bold; }");
            sb.AppendLine("        .summary { background-color: #f9f9f9; padding: 15px; border-radius: 5px; }");
            sb.AppendLine("    </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            sb.AppendLine($"    <h1>测试报告 - {suiteResult.SuiteName}</h1>");

            // 摘要
            sb.AppendLine("    <div class='summary'>");
            sb.AppendLine("        <h2>摘要</h2>");
            sb.AppendLine("        <table>");
            sb.AppendLine($"            <tr><td>开始时间</td><td>{suiteResult.StartTime:yyyy-MM-dd HH:mm:ss}</td></tr>");
            sb.AppendLine($"            <tr><td>结束时间</td><td>{suiteResult.EndTime:yyyy-MM-dd HH:mm:ss}</td></tr>");
            sb.AppendLine($"            <tr><td>总耗时</td><td>{suiteResult.TotalDuration.TotalSeconds:F2} 秒</td></tr>");
            sb.AppendLine($"            <tr><td>总测试数</td><td>{suiteResult.TotalTests}</td></tr>");
            sb.AppendLine($"            <tr><td>通过测试</td><td class='pass'>{suiteResult.PassedTests}</td></tr>");
            sb.AppendLine($"            <tr><td>失败测试</td><td class='fail'>{suiteResult.FailedTests}</td></tr>");
            sb.AppendLine($"            <tr><td>通过率</td><td>{suiteResult.PassRate:F2}%</td></tr>");
            sb.AppendLine("        </table>");
            sb.AppendLine("    </div>");

            // 详细结果
            sb.AppendLine("    <h2>详细结果</h2>");
            sb.AppendLine("    <table>");
            sb.AppendLine("        <tr><th>状态</th><th>测试名称</th><th>耗时 (ms)</th><th>执行时间</th><th>错误信息</th></tr>");

            foreach (var test in suiteResult.Tests)
            {
                var statusClass = test.Passed ? "pass" : "fail";
                var status = test.Passed ? "✓ PASS" : "✗ FAIL";
                var error = test.Passed ? "-" : (test.ErrorMessage ?? "未知错误");

                sb.AppendLine("        <tr>");
                sb.AppendLine($"            <td class='{statusClass}'>{status}</td>");
                sb.AppendLine($"            <td>{test.TestName}</td>");
                sb.AppendLine($"            <td>{test.Duration.TotalMilliseconds:F0}</td>");
                sb.AppendLine($"            <td>{test.ExecutedAt:HH:mm:ss}</td>");
                sb.AppendLine($"            <td>{error}</td>");
                sb.AppendLine("        </tr>");
            }

            sb.AppendLine("    </table>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        /// <summary>
        /// 保存报告到文件
        /// Saves the report to a file
        /// </summary>
        public static async Task SaveReportAsync(
            string content,
            string filePath,
            CancellationToken cancellationToken = default)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(filePath, content, cancellationToken);
        }
    }

    /// <summary>
    /// 字符串扩展方法
    /// String extension methods
    /// </summary>
    internal static class StringExtensions
    {
        public static string Repeat(this string str, int count)
        {
            if (string.IsNullOrEmpty(str) || count <= 0)
                return string.Empty;

            return string.Concat(Enumerable.Repeat(str, count));
        }
    }
}
