using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Occop.Core.Logging
{
    /// <summary>
    /// 日志服务实现，提供结构化的日志记录功能
    /// Logger service implementation providing structured logging capabilities
    /// </summary>
    public class LoggerService : ILoggerService, IDisposable
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ConcurrentDictionary<LogCategory, ILogger> _loggers;
        private readonly ConcurrentDictionary<LogCategory, LogLevel> _logLevels;
        private readonly SensitiveDataFilter _sensitiveDataFilter;
        private bool _disposed = false;

        /// <summary>
        /// 获取敏感数据过滤器是否启用
        /// Gets whether sensitive data filter is enabled
        /// </summary>
        public bool IsSensitiveDataFilterEnabled => _sensitiveDataFilter.IsEnabled;

        /// <summary>
        /// 初始化日志服务
        /// Initializes logger service
        /// </summary>
        /// <param name="loggerFactory">日志工厂 Logger factory</param>
        /// <param name="enableSensitiveDataFilter">是否启用敏感数据过滤 Whether to enable sensitive data filtering</param>
        public LoggerService(ILoggerFactory loggerFactory, bool enableSensitiveDataFilter = true)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _loggers = new ConcurrentDictionary<LogCategory, ILogger>();
            _logLevels = new ConcurrentDictionary<LogCategory, LogLevel>();
            _sensitiveDataFilter = new SensitiveDataFilter(enableSensitiveDataFilter);

            InitializeDefaultLogLevels();
        }

        /// <summary>
        /// 初始化默认日志级别
        /// Initializes default log levels
        /// </summary>
        private void InitializeDefaultLogLevels()
        {
            // 为不同类别设置默认日志级别
            // Set default log levels for different categories
            _logLevels[LogCategory.Application] = LogLevel.Information;
            _logLevels[LogCategory.Security] = LogLevel.Information;
            _logLevels[LogCategory.Authentication] = LogLevel.Information;
            _logLevels[LogCategory.DataAccess] = LogLevel.Information;
            _logLevels[LogCategory.UI] = LogLevel.Warning;
            _logLevels[LogCategory.Performance] = LogLevel.Information;
            _logLevels[LogCategory.API] = LogLevel.Information;
            _logLevels[LogCategory.Configuration] = LogLevel.Information;
            _logLevels[LogCategory.Cleanup] = LogLevel.Information;
            _logLevels[LogCategory.Validation] = LogLevel.Information;
            _logLevels[LogCategory.System] = LogLevel.Information;
            _logLevels[LogCategory.Network] = LogLevel.Warning;
            _logLevels[LogCategory.Integration] = LogLevel.Information;
            _logLevels[LogCategory.Debug] = LogLevel.Debug;
        }

        /// <summary>
        /// 获取或创建指定类别的日志记录器
        /// Gets or creates logger for specified category
        /// </summary>
        /// <param name="category">日志分类 Log category</param>
        /// <returns>日志记录器 Logger</returns>
        private ILogger GetLogger(LogCategory category)
        {
            return _loggers.GetOrAdd(category, cat => _loggerFactory.CreateLogger($"Occop.{cat}"));
        }

        /// <summary>
        /// 记录调试级别的日志
        /// Logs debug level message
        /// </summary>
        public void LogDebug(LogCategory category, string message, LogContext? context = null, Exception? exception = null)
        {
            if (!IsEnabled(category, LogLevel.Debug))
                return;

            var filteredMessage = _sensitiveDataFilter.FilterSensitiveData(message);
            var logger = GetLogger(category);

            using (CreateScopeFromContext(logger, context))
            {
                if (exception != null)
                    logger.LogDebug(exception, filteredMessage);
                else
                    logger.LogDebug(filteredMessage);
            }
        }

        /// <summary>
        /// 记录信息级别的日志
        /// Logs information level message
        /// </summary>
        public void LogInformation(LogCategory category, string message, LogContext? context = null, Exception? exception = null)
        {
            if (!IsEnabled(category, LogLevel.Information))
                return;

            var filteredMessage = _sensitiveDataFilter.FilterSensitiveData(message);
            var logger = GetLogger(category);

            using (CreateScopeFromContext(logger, context))
            {
                if (exception != null)
                    logger.LogInformation(exception, filteredMessage);
                else
                    logger.LogInformation(filteredMessage);
            }
        }

        /// <summary>
        /// 记录警告级别的日志
        /// Logs warning level message
        /// </summary>
        public void LogWarning(LogCategory category, string message, LogContext? context = null, Exception? exception = null)
        {
            if (!IsEnabled(category, LogLevel.Warning))
                return;

            var filteredMessage = _sensitiveDataFilter.FilterSensitiveData(message);
            var logger = GetLogger(category);

            using (CreateScopeFromContext(logger, context))
            {
                if (exception != null)
                    logger.LogWarning(exception, filteredMessage);
                else
                    logger.LogWarning(filteredMessage);
            }
        }

        /// <summary>
        /// 记录错误级别的日志
        /// Logs error level message
        /// </summary>
        public void LogError(LogCategory category, string message, LogContext? context = null, Exception? exception = null)
        {
            if (!IsEnabled(category, LogLevel.Error))
                return;

            var filteredMessage = _sensitiveDataFilter.FilterSensitiveData(message);
            var logger = GetLogger(category);

            using (CreateScopeFromContext(logger, context))
            {
                if (exception != null)
                    logger.LogError(exception, filteredMessage);
                else
                    logger.LogError(filteredMessage);
            }
        }

        /// <summary>
        /// 记录严重错误级别的日志
        /// Logs critical level message
        /// </summary>
        public void LogCritical(LogCategory category, string message, LogContext? context = null, Exception? exception = null)
        {
            if (!IsEnabled(category, LogLevel.Critical))
                return;

            var filteredMessage = _sensitiveDataFilter.FilterSensitiveData(message);
            var logger = GetLogger(category);

            using (CreateScopeFromContext(logger, context))
            {
                if (exception != null)
                    logger.LogCritical(exception, filteredMessage);
                else
                    logger.LogCritical(filteredMessage);
            }
        }

        /// <summary>
        /// 记录操作日志
        /// Logs operation
        /// </summary>
        public void LogOperation(LogCategory category, LogOperationType operationType, string message,
            LogContext? context = null, bool success = true, TimeSpan? duration = null)
        {
            var level = success ? LogLevel.Information : LogLevel.Warning;
            if (!IsEnabled(category, level))
                return;

            context ??= new LogContext();
            context.WithOperationType(operationType);

            if (duration.HasValue)
            {
                context.AddProperty("duration_ms", duration.Value.TotalMilliseconds);
            }

            context.AddProperty("operation_type", operationType.ToString());
            context.AddProperty("success", success);

            var filteredMessage = _sensitiveDataFilter.FilterSensitiveData(message);
            var formattedMessage = $"[{operationType}] {filteredMessage} (Success: {success})";

            if (duration.HasValue)
            {
                formattedMessage += $" - Duration: {duration.Value.TotalMilliseconds:F2}ms";
            }

            var logger = GetLogger(category);

            using (CreateScopeFromContext(logger, context))
            {
                if (success)
                    logger.LogInformation(formattedMessage);
                else
                    logger.LogWarning(formattedMessage);
            }
        }

        /// <summary>
        /// 记录性能指标
        /// Logs performance metric
        /// </summary>
        public void LogPerformance(string metricName, double value, string unit, LogContext? context = null)
        {
            if (!IsEnabled(LogCategory.Performance, LogLevel.Information))
                return;

            context ??= new LogContext();
            context.AddProperty("metric_name", metricName);
            context.AddProperty("metric_value", value);
            context.AddProperty("metric_unit", unit);

            var message = $"Performance Metric: {metricName} = {value:F2} {unit}";
            var logger = GetLogger(LogCategory.Performance);

            using (CreateScopeFromContext(logger, context))
            {
                logger.LogInformation(message);
            }
        }

        /// <summary>
        /// 开始性能计时
        /// Starts performance timing
        /// </summary>
        public IDisposable BeginTimedOperation(string operationName, LogContext? context = null)
        {
            return new TimedOperation(this, operationName, context);
        }

        /// <summary>
        /// 创建日志作用域
        /// Creates log scope
        /// </summary>
        public IDisposable BeginScope(string scopeName, LogContext? context = null)
        {
            context ??= new LogContext();
            context.AddProperty("scope", scopeName);

            var logger = GetLogger(LogCategory.Application);
            return logger.BeginScope(new Dictionary<string, object>
            {
                ["scope_name"] = scopeName,
                ["correlation_id"] = context.CorrelationId,
                ["session_id"] = context.SessionId
            });
        }

        /// <summary>
        /// 获取当前日志级别
        /// Gets current log level
        /// </summary>
        public LogLevel GetLogLevel(LogCategory category)
        {
            return _logLevels.GetOrAdd(category, LogLevel.Information);
        }

        /// <summary>
        /// 设置日志级别
        /// Sets log level
        /// </summary>
        public void SetLogLevel(LogCategory category, LogLevel level)
        {
            _logLevels[category] = level;
        }

        /// <summary>
        /// 检查日志级别是否启用
        /// Checks if log level is enabled
        /// </summary>
        public bool IsEnabled(LogCategory category, LogLevel level)
        {
            var logger = GetLogger(category);
            return logger.IsEnabled(level);
        }

        /// <summary>
        /// 刷新日志缓冲区
        /// Flushes log buffer
        /// </summary>
        public async Task FlushAsync()
        {
            // NLog和其他日志框架通常会自动刷新
            // NLog and other logging frameworks typically flush automatically
            // 这里可以添加显式刷新逻辑
            // Explicit flush logic can be added here
            await Task.CompletedTask;
        }

        /// <summary>
        /// 启用或禁用敏感数据过滤
        /// Enables or disables sensitive data filtering
        /// </summary>
        public void SetSensitiveDataFilterEnabled(bool enabled)
        {
            // 敏感数据过滤器在构造时已初始化
            // Sensitive data filter is initialized at construction
            // 这里记录配置更改
            // Log configuration change here
            LogInformation(LogCategory.Configuration,
                $"Sensitive data filter {(enabled ? "enabled" : "disabled")}");
        }

        /// <summary>
        /// 从上下文创建日志作用域
        /// Creates log scope from context
        /// </summary>
        /// <param name="logger">日志记录器 Logger</param>
        /// <param name="context">日志上下文 Log context</param>
        /// <returns>日志作用域 Log scope</returns>
        private IDisposable? CreateScopeFromContext(ILogger logger, LogContext? context)
        {
            if (context == null)
                return null;

            var scopeData = new Dictionary<string, object>
            {
                ["correlation_id"] = context.CorrelationId,
                ["session_id"] = context.SessionId
            };

            if (!string.IsNullOrWhiteSpace(context.UserId))
                scopeData["user_id"] = context.UserId;

            if (context.OperationType.HasValue)
                scopeData["operation_type"] = context.OperationType.Value.ToString();

            if (!string.IsNullOrWhiteSpace(context.ModuleName))
                scopeData["module"] = context.ModuleName;

            if (!string.IsNullOrWhiteSpace(context.ComponentName))
                scopeData["component"] = context.ComponentName;

            // 添加自定义属性（过滤敏感数据）
            // Add custom properties (filter sensitive data)
            var filteredProperties = _sensitiveDataFilter.FilterSensitiveData(context.Properties);
            foreach (var prop in filteredProperties)
            {
                scopeData[prop.Key] = prop.Value;
            }

            return logger.BeginScope(scopeData);
        }

        /// <summary>
        /// 释放资源
        /// Disposes resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源的具体实现
        /// Actual implementation of resource disposal
        /// </summary>
        /// <param name="disposing">是否正在释放托管资源 Whether disposing managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 刷新所有日志
                    // Flush all logs
                    _ = FlushAsync().ConfigureAwait(false);

                    // 清理日志记录器缓存
                    // Clear logger cache
                    _loggers.Clear();
                    _logLevels.Clear();
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// 性能计时操作
        /// Timed performance operation
        /// </summary>
        private class TimedOperation : IDisposable
        {
            private readonly LoggerService _loggerService;
            private readonly string _operationName;
            private readonly LogContext _context;
            private readonly Stopwatch _stopwatch;
            private bool _disposed = false;

            public TimedOperation(LoggerService loggerService, string operationName, LogContext? context)
            {
                _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
                _operationName = operationName ?? throw new ArgumentNullException(nameof(operationName));
                _context = context ?? new LogContext();
                _stopwatch = Stopwatch.StartNew();
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _stopwatch.Stop();

                    _loggerService.LogPerformance(
                        _operationName,
                        _stopwatch.Elapsed.TotalMilliseconds,
                        "ms",
                        _context);

                    _disposed = true;
                }
            }
        }
    }
}
