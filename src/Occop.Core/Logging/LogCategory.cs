namespace Occop.Core.Logging
{
    /// <summary>
    /// 日志分类，用于组织和过滤日志
    /// Log categories for organizing and filtering logs
    /// </summary>
    public enum LogCategory
    {
        /// <summary>
        /// 应用程序常规日志
        /// General application logs
        /// </summary>
        Application,

        /// <summary>
        /// 安全相关日志
        /// Security-related logs
        /// </summary>
        Security,

        /// <summary>
        /// 认证和授权日志
        /// Authentication and authorization logs
        /// </summary>
        Authentication,

        /// <summary>
        /// 数据访问日志
        /// Data access logs
        /// </summary>
        DataAccess,

        /// <summary>
        /// 用户界面日志
        /// User interface logs
        /// </summary>
        UI,

        /// <summary>
        /// 性能监控日志
        /// Performance monitoring logs
        /// </summary>
        Performance,

        /// <summary>
        /// API调用日志
        /// API call logs
        /// </summary>
        API,

        /// <summary>
        /// 配置管理日志
        /// Configuration management logs
        /// </summary>
        Configuration,

        /// <summary>
        /// 清理操作日志
        /// Cleanup operation logs
        /// </summary>
        Cleanup,

        /// <summary>
        /// 验证操作日志
        /// Validation operation logs
        /// </summary>
        Validation,

        /// <summary>
        /// 系统操作日志
        /// System operation logs
        /// </summary>
        System,

        /// <summary>
        /// 网络通信日志
        /// Network communication logs
        /// </summary>
        Network,

        /// <summary>
        /// 第三方集成日志
        /// Third-party integration logs
        /// </summary>
        Integration,

        /// <summary>
        /// 调试日志
        /// Debug logs
        /// </summary>
        Debug
    }

    /// <summary>
    /// 日志操作类型
    /// Log operation types
    /// </summary>
    public enum LogOperationType
    {
        /// <summary>
        /// 创建操作
        /// Create operation
        /// </summary>
        Create,

        /// <summary>
        /// 读取操作
        /// Read operation
        /// </summary>
        Read,

        /// <summary>
        /// 更新操作
        /// Update operation
        /// </summary>
        Update,

        /// <summary>
        /// 删除操作
        /// Delete operation
        /// </summary>
        Delete,

        /// <summary>
        /// 执行操作
        /// Execute operation
        /// </summary>
        Execute,

        /// <summary>
        /// 验证操作
        /// Validate operation
        /// </summary>
        Validate,

        /// <summary>
        /// 清理操作
        /// Cleanup operation
        /// </summary>
        Cleanup,

        /// <summary>
        /// 初始化操作
        /// Initialize operation
        /// </summary>
        Initialize,

        /// <summary>
        /// 关闭操作
        /// Shutdown operation
        /// </summary>
        Shutdown
    }

    /// <summary>
    /// 日志上下文，用于关联相关的日志条目
    /// Log context for correlating related log entries
    /// </summary>
    public class LogContext
    {
        /// <summary>
        /// 相关性ID，用于追踪一组相关的操作
        /// Correlation ID for tracking a group of related operations
        /// </summary>
        public string CorrelationId { get; set; }

        /// <summary>
        /// 用户ID
        /// User ID
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// 会话ID
        /// Session ID
        /// </summary>
        public string SessionId { get; set; }

        /// <summary>
        /// 操作类型
        /// Operation type
        /// </summary>
        public LogOperationType? OperationType { get; set; }

        /// <summary>
        /// 模块名称
        /// Module name
        /// </summary>
        public string? ModuleName { get; set; }

        /// <summary>
        /// 组件名称
        /// Component name
        /// </summary>
        public string? ComponentName { get; set; }

        /// <summary>
        /// 自定义属性
        /// Custom properties
        /// </summary>
        public Dictionary<string, object> Properties { get; set; }

        /// <summary>
        /// 初始化日志上下文
        /// Initializes log context
        /// </summary>
        public LogContext()
        {
            CorrelationId = Guid.NewGuid().ToString("N");
            SessionId = string.Empty;
            Properties = new Dictionary<string, object>();
        }

        /// <summary>
        /// 初始化日志上下文
        /// Initializes log context
        /// </summary>
        /// <param name="sessionId">会话ID Session ID</param>
        public LogContext(string sessionId) : this()
        {
            SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        }

        /// <summary>
        /// 添加自定义属性
        /// Adds custom property
        /// </summary>
        /// <param name="key">键 Key</param>
        /// <param name="value">值 Value</param>
        /// <returns>当前实例 Current instance</returns>
        public LogContext AddProperty(string key, object value)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                Properties[key] = value;
            }
            return this;
        }

        /// <summary>
        /// 设置操作类型
        /// Sets operation type
        /// </summary>
        /// <param name="operationType">操作类型 Operation type</param>
        /// <returns>当前实例 Current instance</returns>
        public LogContext WithOperationType(LogOperationType operationType)
        {
            OperationType = operationType;
            return this;
        }

        /// <summary>
        /// 设置模块名称
        /// Sets module name
        /// </summary>
        /// <param name="moduleName">模块名称 Module name</param>
        /// <returns>当前实例 Current instance</returns>
        public LogContext WithModule(string moduleName)
        {
            ModuleName = moduleName;
            return this;
        }

        /// <summary>
        /// 设置组件名称
        /// Sets component name
        /// </summary>
        /// <param name="componentName">组件名称 Component name</param>
        /// <returns>当前实例 Current instance</returns>
        public LogContext WithComponent(string componentName)
        {
            ComponentName = componentName;
            return this;
        }
    }
}
