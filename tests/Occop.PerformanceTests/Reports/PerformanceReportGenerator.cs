using System.Text;
using Occop.Core.Performance;

namespace Occop.PerformanceTests.Reports
{
    /// <summary>
    /// 性能报告格式
    /// Performance report format
    /// </summary>
    public enum ReportFormat
    {
        /// <summary>文本 Text</summary>
        Text,
        /// <summary>Markdown</summary>
        Markdown,
        /// <summary>HTML</summary>
        Html,
        /// <summary>JSON</summary>
        Json
    }

    /// <summary>
    /// 性能报告生成器
    /// Performance report generator
    /// </summary>
    public class PerformanceReportGenerator
    {
        private readonly IPerformanceMonitor _monitor;
        private readonly IMemoryAnalyzer _analyzer;

        /// <summary>
        /// 初始化性能报告生成器
        /// Initializes performance report generator
        /// </summary>
        public PerformanceReportGenerator(IPerformanceMonitor monitor, IMemoryAnalyzer analyzer)
        {
            _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
        }

        /// <summary>
        /// 生成性能报告
        /// Generate performance report
        /// </summary>
        public string GenerateReport(ReportFormat format = ReportFormat.Markdown)
        {
            return format switch
            {
                ReportFormat.Text => GenerateTextReport(),
                ReportFormat.Markdown => GenerateMarkdownReport(),
                ReportFormat.Html => GenerateHtmlReport(),
                ReportFormat.Json => GenerateJsonReport(),
                _ => throw new ArgumentException($"Unsupported format: {format}", nameof(format))
            };
        }

        /// <summary>
        /// 保存报告到文件
        /// Save report to file
        /// </summary>
        public async Task SaveReportAsync(string filePath, ReportFormat format = ReportFormat.Markdown)
        {
            var report = GenerateReport(format);
            await File.WriteAllTextAsync(filePath, report);
        }

        /// <summary>
        /// 生成文本报告
        /// Generate text report
        /// </summary>
        private string GenerateTextReport()
        {
            var sb = new StringBuilder();
            var stats = _monitor.GetAllStatistics();
            var memorySnapshot = _monitor.GetMemorySnapshot();

            sb.AppendLine("=".PadRight(80, '='));
            sb.AppendLine("PERFORMANCE REPORT");
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("=".PadRight(80, '='));
            sb.AppendLine();

            // 内存概览
            sb.AppendLine("MEMORY OVERVIEW");
            sb.AppendLine("-".PadRight(80, '-'));
            sb.AppendLine($"Working Set:     {memorySnapshot.WorkingSetMB:F2} MB");
            sb.AppendLine($"Private Memory:  {memorySnapshot.PrivateMemoryMB:F2} MB");
            sb.AppendLine($"Managed Heap:    {memorySnapshot.ManagedHeapMB:F2} MB");
            sb.AppendLine($"Gen0 Collections: {memorySnapshot.Gen0CollectionCount}");
            sb.AppendLine($"Gen1 Collections: {memorySnapshot.Gen1CollectionCount}");
            sb.AppendLine($"Gen2 Collections: {memorySnapshot.Gen2CollectionCount}");
            sb.AppendLine();

            // 操作统计
            sb.AppendLine("OPERATION STATISTICS");
            sb.AppendLine("-".PadRight(80, '-'));

            if (stats.Count == 0)
            {
                sb.AppendLine("No operation statistics available.");
            }
            else
            {
                foreach (var stat in stats.Values.OrderByDescending(s => s.TotalExecutions))
                {
                    sb.AppendLine($"\nOperation: {stat.OperationName}");
                    sb.AppendLine($"  Category:          {stat.Category}");
                    sb.AppendLine($"  Total Executions:  {stat.TotalExecutions:N0}");
                    sb.AppendLine($"  Success Rate:      {stat.SuccessRate:F1}%");
                    sb.AppendLine($"  Avg Duration:      {stat.AverageDurationMs:F2} ms");
                    sb.AppendLine($"  Min Duration:      {stat.MinDurationMs} ms");
                    sb.AppendLine($"  Max Duration:      {stat.MaxDurationMs} ms");
                    sb.AppendLine($"  Recent Avg:        {stat.RecentAverageDurationMs:F2} ms");
                }
            }

            sb.AppendLine();
            sb.AppendLine("=".PadRight(80, '='));

            return sb.ToString();
        }

        /// <summary>
        /// 生成Markdown报告
        /// Generate markdown report
        /// </summary>
        private string GenerateMarkdownReport()
        {
            var sb = new StringBuilder();
            var stats = _monitor.GetAllStatistics();
            var memorySnapshot = _monitor.GetMemorySnapshot();
            var memoryAnalysis = _analyzer.Analyze(memorySnapshot);

            sb.AppendLine("# Performance Report");
            sb.AppendLine();
            sb.AppendLine($"**Generated:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            // 内存概览
            sb.AppendLine("## Memory Overview");
            sb.AppendLine();
            sb.AppendLine("| Metric | Value |");
            sb.AppendLine("|--------|-------|");
            sb.AppendLine($"| Working Set | {memorySnapshot.WorkingSetMB:F2} MB |");
            sb.AppendLine($"| Private Memory | {memorySnapshot.PrivateMemoryMB:F2} MB |");
            sb.AppendLine($"| Managed Heap | {memorySnapshot.ManagedHeapMB:F2} MB |");
            sb.AppendLine($"| Gen0 Collections | {memorySnapshot.Gen0CollectionCount} |");
            sb.AppendLine($"| Gen1 Collections | {memorySnapshot.Gen1CollectionCount} |");
            sb.AppendLine($"| Gen2 Collections | {memorySnapshot.Gen2CollectionCount} |");
            sb.AppendLine();

            // 内存问题
            if (memoryAnalysis.Issues.Any())
            {
                sb.AppendLine("### Memory Issues");
                sb.AppendLine();
                foreach (var issue in memoryAnalysis.Issues)
                {
                    var icon = issue.Severity switch
                    {
                        MemoryIssueSeverity.Critical => "🔴",
                        MemoryIssueSeverity.Warning => "⚠️",
                        _ => "ℹ️"
                    };
                    sb.AppendLine($"{icon} **{issue.Severity}**: {issue.Description}");
                    sb.AppendLine($"   - *Recommendation:* {issue.Recommendation}");
                    sb.AppendLine();
                }
            }

            // 操作统计
            sb.AppendLine("## Operation Statistics");
            sb.AppendLine();

            if (stats.Count == 0)
            {
                sb.AppendLine("*No operation statistics available.*");
            }
            else
            {
                sb.AppendLine("| Operation | Category | Executions | Success Rate | Avg (ms) | Min (ms) | Max (ms) | Recent Avg (ms) |");
                sb.AppendLine("|-----------|----------|------------|--------------|----------|----------|----------|-----------------|");

                foreach (var stat in stats.Values.OrderByDescending(s => s.TotalExecutions))
                {
                    sb.AppendLine($"| {stat.OperationName} | {stat.Category} | {stat.TotalExecutions:N0} | {stat.SuccessRate:F1}% | {stat.AverageDurationMs:F2} | {stat.MinDurationMs} | {stat.MaxDurationMs} | {stat.RecentAverageDurationMs:F2} |");
                }
            }

            sb.AppendLine();

            // 性能降级检测
            sb.AppendLine("## Performance Degradation Detection");
            sb.AppendLine();

            var degradations = new List<string>();
            foreach (var stat in stats.Values)
            {
                if (_monitor.DetectDegradation(stat.OperationName, 20.0))
                {
                    var percentage = (stat.RecentAverageDurationMs - stat.AverageDurationMs) / stat.AverageDurationMs * 100;
                    degradations.Add($"⚠️ **{stat.OperationName}**: {percentage:F1}% slower than average");
                }
            }

            if (degradations.Any())
            {
                foreach (var degradation in degradations)
                {
                    sb.AppendLine(degradation);
                }
            }
            else
            {
                sb.AppendLine("✅ No performance degradation detected.");
            }

            sb.AppendLine();

            return sb.ToString();
        }

        /// <summary>
        /// 生成HTML报告
        /// Generate HTML report
        /// </summary>
        private string GenerateHtmlReport()
        {
            var sb = new StringBuilder();
            var stats = _monitor.GetAllStatistics();
            var memorySnapshot = _monitor.GetMemorySnapshot();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("    <meta charset=\"utf-8\">");
            sb.AppendLine("    <title>Performance Report</title>");
            sb.AppendLine("    <style>");
            sb.AppendLine("        body { font-family: Arial, sans-serif; margin: 20px; }");
            sb.AppendLine("        h1 { color: #333; }");
            sb.AppendLine("        h2 { color: #666; border-bottom: 2px solid #ddd; padding-bottom: 5px; }");
            sb.AppendLine("        table { border-collapse: collapse; width: 100%; margin: 20px 0; }");
            sb.AppendLine("        th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
            sb.AppendLine("        th { background-color: #f2f2f2; }");
            sb.AppendLine("        tr:hover { background-color: #f5f5f5; }");
            sb.AppendLine("        .metric { display: inline-block; margin: 10px; padding: 10px; background: #f9f9f9; border-radius: 5px; }");
            sb.AppendLine("        .warning { color: #ff9800; }");
            sb.AppendLine("        .critical { color: #f44336; }");
            sb.AppendLine("        .info { color: #2196f3; }");
            sb.AppendLine("    </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            sb.AppendLine($"    <h1>Performance Report</h1>");
            sb.AppendLine($"    <p><strong>Generated:</strong> {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");

            sb.AppendLine("    <h2>Memory Overview</h2>");
            sb.AppendLine("    <div>");
            sb.AppendLine($"        <div class=\"metric\"><strong>Working Set:</strong> {memorySnapshot.WorkingSetMB:F2} MB</div>");
            sb.AppendLine($"        <div class=\"metric\"><strong>Private Memory:</strong> {memorySnapshot.PrivateMemoryMB:F2} MB</div>");
            sb.AppendLine($"        <div class=\"metric\"><strong>Managed Heap:</strong> {memorySnapshot.ManagedHeapMB:F2} MB</div>");
            sb.AppendLine($"        <div class=\"metric\"><strong>Gen0:</strong> {memorySnapshot.Gen0CollectionCount}</div>");
            sb.AppendLine($"        <div class=\"metric\"><strong>Gen1:</strong> {memorySnapshot.Gen1CollectionCount}</div>");
            sb.AppendLine($"        <div class=\"metric\"><strong>Gen2:</strong> {memorySnapshot.Gen2CollectionCount}</div>");
            sb.AppendLine("    </div>");

            sb.AppendLine("    <h2>Operation Statistics</h2>");

            if (stats.Count == 0)
            {
                sb.AppendLine("    <p><em>No operation statistics available.</em></p>");
            }
            else
            {
                sb.AppendLine("    <table>");
                sb.AppendLine("        <thead>");
                sb.AppendLine("            <tr>");
                sb.AppendLine("                <th>Operation</th>");
                sb.AppendLine("                <th>Category</th>");
                sb.AppendLine("                <th>Executions</th>");
                sb.AppendLine("                <th>Success Rate</th>");
                sb.AppendLine("                <th>Avg (ms)</th>");
                sb.AppendLine("                <th>Min (ms)</th>");
                sb.AppendLine("                <th>Max (ms)</th>");
                sb.AppendLine("                <th>Recent Avg (ms)</th>");
                sb.AppendLine("            </tr>");
                sb.AppendLine("        </thead>");
                sb.AppendLine("        <tbody>");

                foreach (var stat in stats.Values.OrderByDescending(s => s.TotalExecutions))
                {
                    sb.AppendLine("            <tr>");
                    sb.AppendLine($"                <td>{stat.OperationName}</td>");
                    sb.AppendLine($"                <td>{stat.Category}</td>");
                    sb.AppendLine($"                <td>{stat.TotalExecutions:N0}</td>");
                    sb.AppendLine($"                <td>{stat.SuccessRate:F1}%</td>");
                    sb.AppendLine($"                <td>{stat.AverageDurationMs:F2}</td>");
                    sb.AppendLine($"                <td>{stat.MinDurationMs}</td>");
                    sb.AppendLine($"                <td>{stat.MaxDurationMs}</td>");
                    sb.AppendLine($"                <td>{stat.RecentAverageDurationMs:F2}</td>");
                    sb.AppendLine("            </tr>");
                }

                sb.AppendLine("        </tbody>");
                sb.AppendLine("    </table>");
            }

            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        /// <summary>
        /// 生成JSON报告
        /// Generate JSON report
        /// </summary>
        private string GenerateJsonReport()
        {
            var stats = _monitor.GetAllStatistics();
            var memorySnapshot = _monitor.GetMemorySnapshot();

            var report = new
            {
                generatedAt = DateTime.Now,
                memory = new
                {
                    workingSetMB = memorySnapshot.WorkingSetMB,
                    privateMemoryMB = memorySnapshot.PrivateMemoryMB,
                    managedHeapMB = memorySnapshot.ManagedHeapMB,
                    gen0Collections = memorySnapshot.Gen0CollectionCount,
                    gen1Collections = memorySnapshot.Gen1CollectionCount,
                    gen2Collections = memorySnapshot.Gen2CollectionCount
                },
                operations = stats.Values.Select(s => new
                {
                    name = s.OperationName,
                    category = s.Category,
                    totalExecutions = s.TotalExecutions,
                    successCount = s.SuccessCount,
                    failureCount = s.FailureCount,
                    successRate = s.SuccessRate,
                    averageDurationMs = s.AverageDurationMs,
                    minDurationMs = s.MinDurationMs,
                    maxDurationMs = s.MaxDurationMs,
                    recentAverageDurationMs = s.RecentAverageDurationMs,
                    lastExecutionTime = s.LastExecutionTime,
                    firstExecutionTime = s.FirstExecutionTime
                }).ToList()
            };

            return System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
    }
}
