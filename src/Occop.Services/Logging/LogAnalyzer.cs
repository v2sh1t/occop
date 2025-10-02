using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Occop.Core.Logging;

namespace Occop.Services.Logging
{
    /// <summary>
    /// 日志分析器，提供日志查询、统计和分析功能
    /// Log analyzer providing log querying, statistics and analysis capabilities
    /// </summary>
    public class LogAnalyzer
    {
        private readonly string _logDirectory;
        private readonly ILogger? _logger;

        /// <summary>
        /// 初始化日志分析器
        /// Initializes log analyzer
        /// </summary>
        /// <param name="logDirectory">日志目录 Log directory</param>
        /// <param name="logger">日志记录器 Logger</param>
        public LogAnalyzer(string logDirectory, ILogger? logger = null)
        {
            if (string.IsNullOrWhiteSpace(logDirectory))
                throw new ArgumentNullException(nameof(logDirectory));

            _logDirectory = logDirectory;
            _logger = logger;
        }

        /// <summary>
        /// 搜索日志条目
        /// Searches log entries
        /// </summary>
        /// <param name="query">查询条件 Query criteria</param>
        /// <returns>搜索结果 Search results</returns>
        public async Task<LogSearchResult> SearchLogsAsync(LogSearchQuery query)
        {
            var result = new LogSearchResult
            {
                Query = query,
                SearchTime = DateTime.UtcNow
            };

            try
            {
                var logFiles = GetLogFiles(query.IncludeArchived);
                var entries = new List<LogEntry>();

                foreach (var logFile in logFiles)
                {
                    var fileEntries = await ParseLogFileAsync(logFile, query);
                    entries.AddRange(fileEntries);

                    if (query.MaxResults.HasValue && entries.Count >= query.MaxResults.Value)
                    {
                        entries = entries.Take(query.MaxResults.Value).ToList();
                        break;
                    }
                }

                // 应用排序
                // Apply sorting
                entries = query.SortDescending
                    ? entries.OrderByDescending(e => e.Timestamp).ToList()
                    : entries.OrderBy(e => e.Timestamp).ToList();

                result.Entries = entries;
                result.TotalCount = entries.Count;
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                _logger?.LogError(ex, "Failed to search logs");
            }

            return result;
        }

        /// <summary>
        /// 分析日志统计信息
        /// Analyzes log statistics
        /// </summary>
        /// <param name="timeRange">时间范围 Time range</param>
        /// <param name="includeArchived">是否包含归档日志 Whether to include archived logs</param>
        /// <returns>统计信息 Statistics</returns>
        public async Task<LogAnalysisStatistics> AnalyzeLogsAsync(TimeSpan? timeRange = null, bool includeArchived = false)
        {
            var statistics = new LogAnalysisStatistics
            {
                AnalysisTime = DateTime.UtcNow,
                TimeRange = timeRange
            };

            try
            {
                var cutoffTime = timeRange.HasValue ? DateTime.UtcNow.Subtract(timeRange.Value) : DateTime.MinValue;
                var logFiles = GetLogFiles(includeArchived);

                foreach (var logFile in logFiles)
                {
                    var entries = await ParseLogFileAsync(logFile, new LogSearchQuery
                    {
                        StartTime = cutoffTime
                    });

                    foreach (var entry in entries)
                    {
                        statistics.TotalEntries++;

                        // 统计日志级别
                        // Count log levels
                        switch (entry.Level?.ToUpperInvariant())
                        {
                            case "DEBUG":
                            case "TRACE":
                                statistics.DebugCount++;
                                break;
                            case "INFO":
                            case "INFORMATION":
                                statistics.InfoCount++;
                                break;
                            case "WARN":
                            case "WARNING":
                                statistics.WarningCount++;
                                break;
                            case "ERROR":
                                statistics.ErrorCount++;
                                break;
                            case "FATAL":
                            case "CRITICAL":
                                statistics.CriticalCount++;
                                break;
                        }

                        // 统计类别
                        // Count categories
                        if (!string.IsNullOrEmpty(entry.Category))
                        {
                            if (!statistics.CategoryCounts.ContainsKey(entry.Category))
                            {
                                statistics.CategoryCounts[entry.Category] = 0;
                            }
                            statistics.CategoryCounts[entry.Category]++;
                        }

                        // 检测异常
                        // Detect exceptions
                        if (entry.Message?.Contains("Exception", StringComparison.OrdinalIgnoreCase) == true ||
                            entry.Exception != null)
                        {
                            statistics.ExceptionCount++;
                        }
                    }
                }

                statistics.Success = true;
            }
            catch (Exception ex)
            {
                statistics.ErrorMessage = ex.Message;
                _logger?.LogError(ex, "Failed to analyze logs");
            }

            await Task.CompletedTask;
            return statistics;
        }

        /// <summary>
        /// 查找错误模式
        /// Finds error patterns
        /// </summary>
        /// <param name="timeRange">时间范围 Time range</param>
        /// <param name="minOccurrences">最小出现次数 Minimum occurrences</param>
        /// <returns>错误模式列表 Error pattern list</returns>
        public async Task<List<ErrorPattern>> FindErrorPatternsAsync(TimeSpan? timeRange = null, int minOccurrences = 3)
        {
            var patterns = new Dictionary<string, ErrorPattern>();

            try
            {
                var searchQuery = new LogSearchQuery
                {
                    MinLevel = "ERROR",
                    StartTime = timeRange.HasValue ? DateTime.UtcNow.Subtract(timeRange.Value) : DateTime.MinValue
                };

                var searchResult = await SearchLogsAsync(searchQuery);

                foreach (var entry in searchResult.Entries)
                {
                    // 提取错误消息的关键部分
                    // Extract key parts of error message
                    var pattern = ExtractErrorPattern(entry.Message ?? string.Empty);

                    if (!patterns.ContainsKey(pattern))
                    {
                        patterns[pattern] = new ErrorPattern
                        {
                            Pattern = pattern,
                            FirstOccurrence = entry.Timestamp,
                            LastOccurrence = entry.Timestamp,
                            Occurrences = 0,
                            Examples = new List<LogEntry>()
                        };
                    }

                    var errorPattern = patterns[pattern];
                    errorPattern.Occurrences++;
                    errorPattern.LastOccurrence = entry.Timestamp;

                    if (errorPattern.Examples.Count < 5)
                    {
                        errorPattern.Examples.Add(entry);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to find error patterns");
            }

            return patterns.Values
                .Where(p => p.Occurrences >= minOccurrences)
                .OrderByDescending(p => p.Occurrences)
                .ToList();
        }

        /// <summary>
        /// 生成日志报告
        /// Generates log report
        /// </summary>
        /// <param name="timeRange">时间范围 Time range</param>
        /// <returns>日志报告 Log report</returns>
        public async Task<LogReport> GenerateReportAsync(TimeSpan? timeRange = null)
        {
            var report = new LogReport
            {
                GenerationTime = DateTime.UtcNow,
                TimeRange = timeRange ?? TimeSpan.FromHours(24)
            };

            try
            {
                // 收集统计信息
                // Collect statistics
                report.Statistics = await AnalyzeLogsAsync(timeRange, false);

                // 查找错误模式
                // Find error patterns
                report.ErrorPatterns = await FindErrorPatternsAsync(timeRange, 2);

                // 查找关键错误
                // Find critical errors
                var criticalQuery = new LogSearchQuery
                {
                    MinLevel = "CRITICAL",
                    StartTime = timeRange.HasValue ? DateTime.UtcNow.Subtract(timeRange.Value) : DateTime.MinValue,
                    MaxResults = 10
                };
                var criticalResult = await SearchLogsAsync(criticalQuery);
                report.CriticalErrors = criticalResult.Entries;

                report.Success = true;
            }
            catch (Exception ex)
            {
                report.ErrorMessage = ex.Message;
                _logger?.LogError(ex, "Failed to generate log report");
            }

            return report;
        }

        /// <summary>
        /// 获取日志文件列表
        /// Gets log file list
        /// </summary>
        /// <param name="includeArchived">是否包含归档文件 Whether to include archived files</param>
        /// <returns>日志文件列表 Log file list</returns>
        private List<string> GetLogFiles(bool includeArchived)
        {
            var files = new List<string>();

            if (!Directory.Exists(_logDirectory))
                return files;

            // 获取当前日志文件
            // Get current log files
            files.AddRange(Directory.GetFiles(_logDirectory, "*.log", SearchOption.TopDirectoryOnly));

            if (includeArchived)
            {
                var archiveDirectory = Path.Combine(_logDirectory, "archive");
                if (Directory.Exists(archiveDirectory))
                {
                    files.AddRange(Directory.GetFiles(archiveDirectory, "*.log", SearchOption.AllDirectories));
                }
            }

            return files.OrderBy(f => new FileInfo(f).CreationTimeUtc).ToList();
        }

        /// <summary>
        /// 解析日志文件
        /// Parses log file
        /// </summary>
        /// <param name="filePath">文件路径 File path</param>
        /// <param name="query">查询条件 Query criteria</param>
        /// <returns>日志条目列表 Log entry list</returns>
        private async Task<List<LogEntry>> ParseLogFileAsync(string filePath, LogSearchQuery query)
        {
            var entries = new List<LogEntry>();

            try
            {
                using var reader = new StreamReader(filePath);
                string? line;

                while ((line = await reader.ReadLineAsync()) != null)
                {
                    var entry = ParseLogLine(line);
                    if (entry != null && MatchesQuery(entry, query))
                    {
                        entries.Add(entry);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, $"Failed to parse log file: {filePath}");
            }

            return entries;
        }

        /// <summary>
        /// 解析日志行
        /// Parses log line
        /// </summary>
        /// <param name="line">日志行 Log line</param>
        /// <returns>日志条目 Log entry</returns>
        private LogEntry? ParseLogLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return null;

            try
            {
                // NLog格式: 2024-01-01 12:00:00.000|EventId|LEVEL|Logger|Message Exception
                // NLog format: 2024-01-01 12:00:00.000|EventId|LEVEL|Logger|Message Exception
                var parts = line.Split('|');
                if (parts.Length < 5)
                    return null;

                var entry = new LogEntry
                {
                    RawLine = line,
                    Timestamp = DateTime.TryParse(parts[0], out var timestamp) ? timestamp : DateTime.UtcNow,
                    Level = parts[2].Trim(),
                    Category = parts[3].Trim(),
                    Message = parts[4].Trim()
                };

                // 检查是否有异常信息
                // Check if there's exception information
                if (parts.Length > 5)
                {
                    entry.Exception = string.Join("|", parts.Skip(5));
                }

                return entry;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 检查日志条目是否匹配查询条件
        /// Checks if log entry matches query criteria
        /// </summary>
        /// <param name="entry">日志条目 Log entry</param>
        /// <param name="query">查询条件 Query criteria</param>
        /// <returns>是否匹配 Whether matches</returns>
        private bool MatchesQuery(LogEntry entry, LogSearchQuery query)
        {
            // 时间范围过滤
            // Time range filtering
            if (query.StartTime.HasValue && entry.Timestamp < query.StartTime.Value)
                return false;

            if (query.EndTime.HasValue && entry.Timestamp > query.EndTime.Value)
                return false;

            // 日志级别过滤
            // Log level filtering
            if (!string.IsNullOrEmpty(query.MinLevel))
            {
                var minLevel = GetLogLevelValue(query.MinLevel);
                var entryLevel = GetLogLevelValue(entry.Level ?? "INFO");
                if (entryLevel < minLevel)
                    return false;
            }

            // 类别过滤
            // Category filtering
            if (!string.IsNullOrEmpty(query.Category) &&
                !entry.Category?.Contains(query.Category, StringComparison.OrdinalIgnoreCase) == true)
                return false;

            // 文本搜索
            // Text search
            if (!string.IsNullOrEmpty(query.SearchText))
            {
                var searchIn = $"{entry.Message} {entry.Exception}";
                if (!searchIn.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // 正则表达式匹配
            // Regex matching
            if (!string.IsNullOrEmpty(query.RegexPattern))
            {
                try
                {
                    var regex = new Regex(query.RegexPattern, RegexOptions.IgnoreCase);
                    if (!regex.IsMatch(entry.Message ?? string.Empty))
                        return false;
                }
                catch
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 获取日志级别数值
        /// Gets log level value
        /// </summary>
        /// <param name="level">级别名称 Level name</param>
        /// <returns>级别数值 Level value</returns>
        private int GetLogLevelValue(string level)
        {
            return level.ToUpperInvariant() switch
            {
                "TRACE" or "DEBUG" => 0,
                "INFO" or "INFORMATION" => 1,
                "WARN" or "WARNING" => 2,
                "ERROR" => 3,
                "FATAL" or "CRITICAL" => 4,
                _ => 1
            };
        }

        /// <summary>
        /// 提取错误模式
        /// Extracts error pattern
        /// </summary>
        /// <param name="message">错误消息 Error message</param>
        /// <returns>错误模式 Error pattern</returns>
        private string ExtractErrorPattern(string message)
        {
            // 移除具体的数值、路径和时间戳
            // Remove specific numbers, paths and timestamps
            var pattern = Regex.Replace(message, @"\d+", "#");
            pattern = Regex.Replace(pattern, @"[A-Z]:\\[^\s]+", "<path>");
            pattern = Regex.Replace(pattern, @"/[^\s]+", "<path>");
            pattern = Regex.Replace(pattern, @"\d{4}-\d{2}-\d{2}", "<date>");
            pattern = Regex.Replace(pattern, @"\d{2}:\d{2}:\d{2}", "<time>");

            // 限制长度
            // Limit length
            if (pattern.Length > 200)
            {
                pattern = pattern.Substring(0, 200) + "...";
            }

            return pattern;
        }
    }

    /// <summary>
    /// 日志搜索查询
    /// Log search query
    /// </summary>
    public class LogSearchQuery
    {
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? MinLevel { get; set; }
        public string? Category { get; set; }
        public string? SearchText { get; set; }
        public string? RegexPattern { get; set; }
        public bool IncludeArchived { get; set; }
        public int? MaxResults { get; set; }
        public bool SortDescending { get; set; } = true;
    }

    /// <summary>
    /// 日志条目
    /// Log entry
    /// </summary>
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string? Level { get; set; }
        public string? Category { get; set; }
        public string? Message { get; set; }
        public string? Exception { get; set; }
        public string RawLine { get; set; } = string.Empty;
    }

    /// <summary>
    /// 日志搜索结果
    /// Log search result
    /// </summary>
    public class LogSearchResult
    {
        public LogSearchQuery Query { get; set; } = new LogSearchQuery();
        public DateTime SearchTime { get; set; }
        public List<LogEntry> Entries { get; set; } = new List<LogEntry>();
        public int TotalCount { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// 日志分析统计
    /// Log analysis statistics
    /// </summary>
    public class LogAnalysisStatistics
    {
        public DateTime AnalysisTime { get; set; }
        public TimeSpan? TimeRange { get; set; }
        public int TotalEntries { get; set; }
        public int DebugCount { get; set; }
        public int InfoCount { get; set; }
        public int WarningCount { get; set; }
        public int ErrorCount { get; set; }
        public int CriticalCount { get; set; }
        public int ExceptionCount { get; set; }
        public Dictionary<string, int> CategoryCounts { get; set; } = new Dictionary<string, int>();
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }

        public double ErrorRate => TotalEntries > 0 ? (double)(ErrorCount + CriticalCount) / TotalEntries : 0.0;
        public double WarningRate => TotalEntries > 0 ? (double)WarningCount / TotalEntries : 0.0;
    }

    /// <summary>
    /// 错误模式
    /// Error pattern
    /// </summary>
    public class ErrorPattern
    {
        public string Pattern { get; set; } = string.Empty;
        public int Occurrences { get; set; }
        public DateTime FirstOccurrence { get; set; }
        public DateTime LastOccurrence { get; set; }
        public List<LogEntry> Examples { get; set; } = new List<LogEntry>();
    }

    /// <summary>
    /// 日志报告
    /// Log report
    /// </summary>
    public class LogReport
    {
        public DateTime GenerationTime { get; set; }
        public TimeSpan TimeRange { get; set; }
        public LogAnalysisStatistics? Statistics { get; set; }
        public List<ErrorPattern> ErrorPatterns { get; set; } = new List<ErrorPattern>();
        public List<LogEntry> CriticalErrors { get; set; } = new List<LogEntry>();
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
