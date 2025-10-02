using Microsoft.Extensions.Logging;

namespace Occop.Core.Logging
{
    /// <summary>
    /// 日志服务接口，提供结构化的日志记录功能
    /// Logger service interface providing structured logging capabilities
    /// </summary>
    public interface ILoggerService
    {
        /// <summary>
        /// 记录调试级别的日志
        /// Logs debug level message
        /// </summary>
        /// <param name="category">日志分类 Log category</param>
        /// <param name="message">日志消息 Log message</param>
        /// <param name="context">日志上下文 Log context</param>
        /// <param name="exception">异常信息 Exception</param>
        void LogDebug(LogCategory category, string message, LogContext? context = null, Exception? exception = null);

        /// <summary>
        /// 记录信息级别的日志
        /// Logs information level message
        /// </summary>
        /// <param name="category">日志分类 Log category</param>
        /// <param name="message">日志消息 Log message</param>
        /// <param name="context">日志上下文 Log context</param>
        /// <param name="exception">异常信息 Exception</param>
        void LogInformation(LogCategory category, string message, LogContext? context = null, Exception? exception = null);

        /// <summary>
        /// 记录警告级别的日志
        /// Logs warning level message
        /// </summary>
        /// <param name="category">日志分类 Log category</param>
        /// <param name="message">日志消息 Log message</param>
        /// <param name="context">日志上下文 Log context</param>
        /// <param name="exception">异常信息 Exception</param>
        void LogWarning(LogCategory category, string message, LogContext? context = null, Exception? exception = null);

        /// <summary>
        /// 记录错误级别的日志
        /// Logs error level message
        /// </summary>
        /// <param name="category">日志分类 Log category</param>
        /// <param name="message">日志消息 Log message</param>
        /// <param name="context">日志上下文 Log context</param>
        /// <param name="exception">异常信息 Exception</param>
        void LogError(LogCategory category, string message, LogContext? context = null, Exception? exception = null);

        /// <summary>
        /// 记录严重错误级别的日志
        /// Logs critical level message
        /// </summary>
        /// <param name="category">日志分类 Log category</param>
        /// <param name="message">日志消息 Log message</param>
        /// <param name="context">日志上下文 Log context</param>
        /// <param name="exception">异常信息 Exception</param>
        void LogCritical(LogCategory category, string message, LogContext? context = null, Exception? exception = null);

        /// <summary>
        /// 记录操作日志
        /// Logs operation
        /// </summary>
        /// <param name="category">日志分类 Log category</param>
        /// <param name="operationType">操作类型 Operation type</param>
        /// <param name="message">日志消息 Log message</param>
        /// <param name="context">日志上下文 Log context</param>
        /// <param name="success">操作是否成功 Whether operation succeeded</param>
        /// <param name="duration">操作耗时 Operation duration</param>
        void LogOperation(LogCategory category, LogOperationType operationType, string message,
            LogContext? context = null, bool success = true, TimeSpan? duration = null);

        /// <summary>
        /// 记录性能指标
        /// Logs performance metric
        /// </summary>
        /// <param name="metricName">指标名称 Metric name</param>
        /// <param name="value">指标值 Metric value</param>
        /// <param name="unit">单位 Unit</param>
        /// <param name="context">日志上下文 Log context</param>
        void LogPerformance(string metricName, double value, string unit, LogContext? context = null);

        /// <summary>
        /// 开始性能计时
        /// Starts performance timing
        /// </summary>
        /// <param name="operationName">操作名称 Operation name</param>
        /// <param name="context">日志上下文 Log context</param>
        /// <returns>性能计时器 Performance timer</returns>
        IDisposable BeginTimedOperation(string operationName, LogContext? context = null);

        /// <summary>
        /// 创建日志作用域
        /// Creates log scope
        /// </summary>
        /// <param name="scopeName">作用域名称 Scope name</param>
        /// <param name="context">日志上下文 Log context</param>
        /// <returns>日志作用域 Log scope</returns>
        IDisposable BeginScope(string scopeName, LogContext? context = null);

        /// <summary>
        /// 获取当前日志级别
        /// Gets current log level
        /// </summary>
        /// <param name="category">日志分类 Log category</param>
        /// <returns>日志级别 Log level</returns>
        LogLevel GetLogLevel(LogCategory category);

        /// <summary>
        /// 设置日志级别
        /// Sets log level
        /// </summary>
        /// <param name="category">日志分类 Log category</param>
        /// <param name="level">日志级别 Log level</param>
        void SetLogLevel(LogCategory category, LogLevel level);

        /// <summary>
        /// 检查日志级别是否启用
        /// Checks if log level is enabled
        /// </summary>
        /// <param name="category">日志分类 Log category</param>
        /// <param name="level">日志级别 Log level</param>
        /// <returns>是否启用 Whether enabled</returns>
        bool IsEnabled(LogCategory category, LogLevel level);

        /// <summary>
        /// 刷新日志缓冲区
        /// Flushes log buffer
        /// </summary>
        Task FlushAsync();

        /// <summary>
        /// 启用或禁用敏感数据过滤
        /// Enables or disables sensitive data filtering
        /// </summary>
        /// <param name="enabled">是否启用 Whether enabled</param>
        void SetSensitiveDataFilterEnabled(bool enabled);

        /// <summary>
        /// 获取敏感数据过滤器是否启用
        /// Gets whether sensitive data filter is enabled
        /// </summary>
        bool IsSensitiveDataFilterEnabled { get; }
    }
}
