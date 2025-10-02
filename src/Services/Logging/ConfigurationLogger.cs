using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Occop.Models.Configuration;
using Occop.Services.Configuration;

namespace Occop.Services.Logging
{
    /// <summary>
    /// 日志级别枚举
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// 调试信息
        /// </summary>
        Debug = 0,

        /// <summary>
        /// 一般信息
        /// </summary>
        Information = 1,

        /// <summary>
        /// 警告
        /// </summary>
        Warning = 2,

        /// <summary>
        /// 错误
        /// </summary>
        Error = 3,

        /// <summary>
        /// 致命错误
        /// </summary>
        Fatal = 4
    }

    /// <summary>
    /// 日志条目
    /// </summary>
    public class LogEntry
    {
        /// <summary>
        /// 日志ID
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// 日志级别
        /// </summary>
        public LogLevel Level { get; }

        /// <summary>
        /// 操作类型
        /// </summary>
        public ConfigurationOperation Operation { get; }

        /// <summary>
        /// 操作是否成功
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// 日志消息
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 详细信息（已过滤敏感信息）
        /// </summary>
        public string? Details { get; }

        /// <summary>
        /// 是否包含敏感信息（原始）
        /// </summary>
        public bool IsSensitive { get; }

        /// <summary>
        /// 异常信息
        /// </summary>
        public string? ExceptionInfo { get; }

        /// <summary>
        /// 用户标识（如果有）
        /// </summary>
        public string? UserId { get; }

        /// <summary>
        /// 会话标识（如果有）
        /// </summary>
        public string? SessionId { get; }

        /// <summary>
        /// 机器名称
        /// </summary>
        public string MachineName { get; }

        /// <summary>
        /// 进程ID
        /// </summary>
        public int ProcessId { get; }

        /// <summary>
        /// 线程ID
        /// </summary>
        public int ThreadId { get; }

        /// <summary>
        /// 初始化日志条目
        /// </summary>
        public LogEntry(
            LogLevel level,
            ConfigurationOperation operation,
            bool isSuccess,
            string message,
            string? details = null,
            bool isSensitive = false,
            Exception? exception = null,
            string? userId = null,
            string? sessionId = null)
        {
            Id = Guid.NewGuid().ToString("N");
            Timestamp = DateTime.UtcNow;
            Level = level;
            Operation = operation;
            IsSuccess = isSuccess;
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Details = details;
            IsSensitive = isSensitive;
            ExceptionInfo = exception?.ToString();
            UserId = userId;
            SessionId = sessionId;
            MachineName = Environment.MachineName;
            ProcessId = Environment.ProcessId;
            ThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary>
        /// 转换为JSON字符串
        /// </summary>
        /// <returns>JSON字符串</returns>
        public string ToJson()
        {
            var logObject = new
            {
                Id,
                Timestamp = Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                Level = Level.ToString(),
                Operation = Operation.ToString(),
                IsSuccess,
                Message,
                Details,
                IsSensitive,
                ExceptionInfo,
                UserId,
                SessionId,
                MachineName,
                ProcessId,
                ThreadId
            };

            return JsonSerializer.Serialize(logObject, new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }

        /// <summary>
        /// 转换为可读字符串
        /// </summary>
        /// <returns>可读字符串</returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append($"[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] ");
            sb.Append($"{Level.ToString().ToUpper()} ");
            sb.Append($"[{Operation}] ");
            sb.Append($"{(IsSuccess ? "SUCCESS" : "FAILURE")} ");
            sb.Append($"- {Message}");

            if (!string.IsNullOrEmpty(Details))
            {
                sb.Append($" | Details: {Details}");
            }

            if (IsSensitive)
            {
                sb.Append(" | [SENSITIVE]");
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// 敏感信息过滤器
    /// </summary>
    public static class SensitiveDataFilter
    {
        private static readonly List<Regex> _sensitivePatterns = new()
        {
            // Anthropic API令牌
            new Regex(@"sk-ant-[a-zA-Z0-9\-_]+", RegexOptions.IgnoreCase),
            // 通用密钥模式
            new Regex(@"(api[_-]?key|token|secret|password|passwd)\s*[:=]\s*[\"']?([^\s\"']+)", RegexOptions.IgnoreCase),
            // 环境变量中的敏感信息
            new Regex(@"ANTHROPIC_AUTH_TOKEN\s*=\s*[\"']?([^\s\"']+)", RegexOptions.IgnoreCase),
            // 可能的其他敏感模式
            new Regex(@"Bearer\s+[a-zA-Z0-9\-_.]+", RegexOptions.IgnoreCase),
            new Regex(@"Basic\s+[a-zA-Z0-9+/]+=*", RegexOptions.IgnoreCase)
        };

        /// <summary>
        /// 过滤敏感信息
        /// </summary>
        /// <param name="text">原始文本</param>
        /// <returns>过滤后的文本</returns>
        public static string FilterSensitiveData(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var result = text;

            foreach (var pattern in _sensitivePatterns)
            {
                result = pattern.Replace(result, match =>
                {
                    // 保留前缀和后缀，中间用*替代
                    var originalValue = match.Value;
                    if (originalValue.Length <= 8)
                    {
                        return "[FILTERED]";
                    }

                    var prefix = originalValue.Substring(0, 4);
                    var suffix = originalValue.Substring(originalValue.Length - 4);
                    var maskedLength = Math.Max(4, originalValue.Length - 8);

                    return $"{prefix}{'*'.ToString().PadLeft(maskedLength, '*')}{suffix}";
                });
            }

            return result;
        }

        /// <summary>
        /// 检查文本是否包含敏感信息
        /// </summary>
        /// <param name="text">要检查的文本</param>
        /// <returns>是否包含敏感信息</returns>
        public static bool ContainsSensitiveData(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            return _sensitivePatterns.Any(pattern => pattern.IsMatch(text));
        }
    }

    /// <summary>
    /// 配置日志记录器
    /// 负责记录配置操作日志，过滤敏感信息
    /// </summary>
    public class ConfigurationLogger : IDisposable
    {
        private readonly string _logDirectory;
        private readonly string _logFileName;
        private readonly string _logFilePath;
        private readonly object _lockObject;
        private readonly List<LogEntry> _memoryBuffer;
        private readonly Timer _flushTimer;
        private readonly SemaphoreSlim _semaphore;

        private bool _disposed;
        private LogLevel _minimumLogLevel;
        private int _maxBufferSize;
        private int _flushIntervalMs;
        private bool _enableFileLogging;
        private bool _enableConsoleLogging;

        /// <summary>
        /// 最小日志级别
        /// </summary>
        public LogLevel MinimumLogLevel
        {
            get => _minimumLogLevel;
            set => _minimumLogLevel = value;
        }

        /// <summary>
        /// 是否启用文件日志
        /// </summary>
        public bool EnableFileLogging
        {
            get => _enableFileLogging;
            set => _enableFileLogging = value;
        }

        /// <summary>
        /// 是否启用控制台日志
        /// </summary>
        public bool EnableConsoleLogging
        {
            get => _enableConsoleLogging;
            set => _enableConsoleLogging = value;
        }

        /// <summary>
        /// 内存缓冲区大小
        /// </summary>
        public int BufferSize => _memoryBuffer.Count;

        /// <summary>
        /// 日志事件
        /// </summary>
        public event EventHandler<LogEntry>? LogEntryCreated;

        /// <summary>
        /// 初始化配置日志记录器
        /// </summary>
        /// <param name="logDirectory">日志目录</param>
        /// <param name="minimumLogLevel">最小日志级别</param>
        /// <param name="maxBufferSize">最大缓冲区大小</param>
        /// <param name="flushIntervalMs">刷新间隔（毫秒）</param>
        public ConfigurationLogger(
            string? logDirectory = null,
            LogLevel minimumLogLevel = LogLevel.Information,
            int maxBufferSize = 1000,
            int flushIntervalMs = 5000)
        {
            _logDirectory = logDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Occop", "Logs");
            _logFileName = $"configuration-{DateTime.UtcNow:yyyyMMdd}.log";
            _logFilePath = Path.Combine(_logDirectory, _logFileName);
            _lockObject = new object();
            _memoryBuffer = new List<LogEntry>();
            _semaphore = new SemaphoreSlim(1, 1);

            _minimumLogLevel = minimumLogLevel;
            _maxBufferSize = maxBufferSize;
            _flushIntervalMs = flushIntervalMs;
            _enableFileLogging = true;
            _enableConsoleLogging = false;

            // 确保日志目录存在
            EnsureLogDirectoryExists();

            // 启动定期刷新定时器
            _flushTimer = new Timer(OnFlushTimerCallback, null, _flushIntervalMs, _flushIntervalMs);
        }

        /// <summary>
        /// 记录操作日志
        /// </summary>
        /// <param name="operation">操作类型</param>
        /// <param name="isSuccess">是否成功</param>
        /// <param name="message">日志消息</param>
        /// <param name="details">详细信息</param>
        /// <param name="isSensitive">是否包含敏感信息</param>
        /// <param name="exception">异常信息</param>
        /// <param name="userId">用户标识</param>
        /// <param name="sessionId">会话标识</param>
        /// <returns>日志记录任务</returns>
        public async Task LogOperationAsync(
            ConfigurationOperation operation,
            bool isSuccess,
            string message,
            string? details = null,
            bool isSensitive = false,
            Exception? exception = null,
            string? userId = null,
            string? sessionId = null)
        {
            ThrowIfDisposed();

            var level = DetermineLogLevel(isSuccess, exception);

            if (level < _minimumLogLevel)
                return;

            // 过滤敏感信息
            var filteredMessage = isSensitive ? SensitiveDataFilter.FilterSensitiveData(message) : message;
            var filteredDetails = isSensitive ? SensitiveDataFilter.FilterSensitiveData(details) : details;

            // 检查是否包含敏感信息（如果未明确指定）
            if (!isSensitive)
            {
                isSensitive = SensitiveDataFilter.ContainsSensitiveData(message) ||
                              SensitiveDataFilter.ContainsSensitiveData(details);

                if (isSensitive)
                {
                    filteredMessage = SensitiveDataFilter.FilterSensitiveData(message);
                    filteredDetails = SensitiveDataFilter.FilterSensitiveData(details);
                }
            }

            var logEntry = new LogEntry(
                level,
                operation,
                isSuccess,
                filteredMessage,
                filteredDetails,
                isSensitive,
                exception,
                userId,
                sessionId);

            await AddLogEntryAsync(logEntry);
        }

        /// <summary>
        /// 记录信息日志
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="details">详细信息</param>
        /// <returns>日志记录任务</returns>
        public async Task LogInformationAsync(string message, string? details = null)
        {
            await LogOperationAsync(ConfigurationOperation.Validate, true, message, details);
        }

        /// <summary>
        /// 记录警告日志
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="details">详细信息</param>
        /// <returns>日志记录任务</returns>
        public async Task LogWarningAsync(string message, string? details = null)
        {
            var logEntry = new LogEntry(LogLevel.Warning, ConfigurationOperation.Validate, true, message, details);
            await AddLogEntryAsync(logEntry);
        }

        /// <summary>
        /// 记录错误日志
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="details">详细信息</param>
        /// <param name="exception">异常信息</param>
        /// <returns>日志记录任务</returns>
        public async Task LogErrorAsync(string message, string? details = null, Exception? exception = null)
        {
            var logEntry = new LogEntry(LogLevel.Error, ConfigurationOperation.Validate, false, message, details, false, exception);
            await AddLogEntryAsync(logEntry);
        }

        /// <summary>
        /// 获取最近的日志条目
        /// </summary>
        /// <param name="count">条目数量</param>
        /// <returns>日志条目列表</returns>
        public List<LogEntry> GetRecentEntries(int count = 100)
        {
            ThrowIfDisposed();

            lock (_lockObject)
            {
                return _memoryBuffer
                    .OrderByDescending(entry => entry.Timestamp)
                    .Take(count)
                    .ToList();
            }
        }

        /// <summary>
        /// 获取指定操作的日志条目
        /// </summary>
        /// <param name="operation">操作类型</param>
        /// <param name="count">条目数量</param>
        /// <returns>日志条目列表</returns>
        public List<LogEntry> GetEntriesByOperation(ConfigurationOperation operation, int count = 100)
        {
            ThrowIfDisposed();

            lock (_lockObject)
            {
                return _memoryBuffer
                    .Where(entry => entry.Operation == operation)
                    .OrderByDescending(entry => entry.Timestamp)
                    .Take(count)
                    .ToList();
            }
        }

        /// <summary>
        /// 获取日志统计信息
        /// </summary>
        /// <returns>统计信息</returns>
        public Dictionary<string, object> GetLogStatistics()
        {
            ThrowIfDisposed();

            lock (_lockObject)
            {
                var totalEntries = _memoryBuffer.Count;
                var successCount = _memoryBuffer.Count(e => e.IsSuccess);
                var failureCount = totalEntries - successCount;

                var levelCounts = _memoryBuffer
                    .GroupBy(e => e.Level)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count());

                var operationCounts = _memoryBuffer
                    .GroupBy(e => e.Operation)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count());

                var recentActivity = _memoryBuffer
                    .Where(e => e.Timestamp >= DateTime.UtcNow.AddHours(-1))
                    .Count();

                return new Dictionary<string, object>
                {
                    { "TotalEntries", totalEntries },
                    { "SuccessCount", successCount },
                    { "FailureCount", failureCount },
                    { "SuccessRate", totalEntries > 0 ? (double)successCount / totalEntries * 100 : 0 },
                    { "BufferSize", totalEntries },
                    { "MaxBufferSize", _maxBufferSize },
                    { "BufferUtilization", _maxBufferSize > 0 ? (double)totalEntries / _maxBufferSize * 100 : 0 },
                    { "RecentActivity", recentActivity },
                    { "LevelCounts", levelCounts },
                    { "OperationCounts", operationCounts },
                    { "EnableFileLogging", _enableFileLogging },
                    { "EnableConsoleLogging", _enableConsoleLogging },
                    { "MinimumLogLevel", _minimumLogLevel.ToString() },
                    { "LogFilePath", _logFilePath },
                    { "Timestamp", DateTime.UtcNow }
                };
            }
        }

        /// <summary>
        /// 强制刷新日志到文件
        /// </summary>
        /// <returns>刷新任务</returns>
        public async Task FlushAsync()
        {
            ThrowIfDisposed();

            await _semaphore.WaitAsync();
            try
            {
                await FlushLogsToFileAsync();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// 清理旧日志条目
        /// </summary>
        /// <param name="retentionDays">保留天数</param>
        /// <returns>清理的条目数量</returns>
        public int CleanupOldEntries(int retentionDays = 7)
        {
            ThrowIfDisposed();

            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
            int removedCount = 0;

            lock (_lockObject)
            {
                var entriesToRemove = _memoryBuffer
                    .Where(entry => entry.Timestamp < cutoffDate)
                    .ToList();

                foreach (var entry in entriesToRemove)
                {
                    _memoryBuffer.Remove(entry);
                    removedCount++;
                }
            }

            return removedCount;
        }

        /// <summary>
        /// 添加日志条目
        /// </summary>
        /// <param name="logEntry">日志条目</param>
        private async Task AddLogEntryAsync(LogEntry logEntry)
        {
            lock (_lockObject)
            {
                _memoryBuffer.Add(logEntry);

                // 如果缓冲区满了，移除最旧的条目
                while (_memoryBuffer.Count > _maxBufferSize)
                {
                    _memoryBuffer.RemoveAt(0);
                }
            }

            // 触发事件
            LogEntryCreated?.Invoke(this, logEntry);

            // 控制台输出
            if (_enableConsoleLogging)
            {
                WriteToConsole(logEntry);
            }

            // 如果是高级别日志，立即刷新到文件
            if (logEntry.Level >= LogLevel.Error)
            {
                await _semaphore.WaitAsync();
                try
                {
                    await FlushLogsToFileAsync();
                }
                finally
                {
                    _semaphore.Release();
                }
            }
        }

        /// <summary>
        /// 确定日志级别
        /// </summary>
        /// <param name="isSuccess">是否成功</param>
        /// <param name="exception">异常信息</param>
        /// <returns>日志级别</returns>
        private static LogLevel DetermineLogLevel(bool isSuccess, Exception? exception)
        {
            if (exception != null)
                return LogLevel.Error;

            return isSuccess ? LogLevel.Information : LogLevel.Warning;
        }

        /// <summary>
        /// 确保日志目录存在
        /// </summary>
        private void EnsureLogDirectoryExists()
        {
            try
            {
                if (!Directory.Exists(_logDirectory))
                {
                    Directory.CreateDirectory(_logDirectory);
                }
            }
            catch (Exception ex)
            {
                _enableFileLogging = false;
                Console.WriteLine($"Failed to create log directory: {ex.Message}");
            }
        }

        /// <summary>
        /// 写入控制台
        /// </summary>
        /// <param name="logEntry">日志条目</param>
        private static void WriteToConsole(LogEntry logEntry)
        {
            var originalColor = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = logEntry.Level switch
                {
                    LogLevel.Debug => ConsoleColor.Gray,
                    LogLevel.Information => ConsoleColor.White,
                    LogLevel.Warning => ConsoleColor.Yellow,
                    LogLevel.Error => ConsoleColor.Red,
                    LogLevel.Fatal => ConsoleColor.Magenta,
                    _ => ConsoleColor.White
                };

                Console.WriteLine(logEntry.ToString());
            }
            finally
            {
                Console.ForegroundColor = originalColor;
            }
        }

        /// <summary>
        /// 刷新日志到文件
        /// </summary>
        private async Task FlushLogsToFileAsync()
        {
            if (!_enableFileLogging)
                return;

            try
            {
                List<LogEntry> entriesToFlush;

                lock (_lockObject)
                {
                    entriesToFlush = new List<LogEntry>(_memoryBuffer);
                }

                if (entriesToFlush.Count == 0)
                    return;

                var logLines = entriesToFlush.Select(entry => entry.ToJson());
                await File.AppendAllLinesAsync(_logFilePath, logLines, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to flush logs to file: {ex.Message}");
            }
        }

        /// <summary>
        /// 定时器回调
        /// </summary>
        private async void OnFlushTimerCallback(object? state)
        {
            if (_disposed)
                return;

            await _semaphore.WaitAsync();
            try
            {
                await FlushLogsToFileAsync();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// 检查对象是否已释放
        /// </summary>
        /// <exception cref="ObjectDisposedException">对象已释放时抛出</exception>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ConfigurationLogger));
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _flushTimer?.Dispose();

                // 最后一次刷新日志
                try
                {
                    var flushTask = FlushAsync();
                    flushTask.Wait(TimeSpan.FromSeconds(5));
                }
                catch
                {
                    // 忽略释放时的异常
                }

                _semaphore?.Dispose();
                _disposed = true;
            }
        }
    }
}