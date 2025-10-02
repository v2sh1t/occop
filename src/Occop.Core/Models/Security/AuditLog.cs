using System.Text.Json.Serialization;

namespace Occop.Core.Models.Security
{
    /// <summary>
    /// 安全审计日志模型，记录所有安全相关的操作和事件
    /// Security audit log model that records all security-related operations and events
    /// </summary>
    public class AuditLog
    {
        /// <summary>
        /// 审计日志唯一标识符
        /// Unique identifier for the audit log
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// 审计事件类型
        /// Type of audit event
        /// </summary>
        public AuditEventType EventType { get; set; }

        /// <summary>
        /// 事件发生时间戳
        /// Timestamp when the event occurred
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 事件严重级别
        /// Severity level of the event
        /// </summary>
        public AuditSeverity Severity { get; set; }

        /// <summary>
        /// 操作用户标识（如果有）
        /// User identifier who performed the operation (if any)
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// 应用程序会话标识
        /// Application session identifier
        /// </summary>
        public string SessionId { get; set; }

        /// <summary>
        /// 事件描述
        /// Event description
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 详细信息（结构化数据）
        /// Detailed information (structured data)
        /// </summary>
        public Dictionary<string, object> Details { get; set; }

        /// <summary>
        /// 操作结果
        /// Operation result
        /// </summary>
        public AuditOperationResult Result { get; set; }

        /// <summary>
        /// 错误信息（如果操作失败）
        /// Error message (if operation failed)
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 错误堆栈跟踪（仅在调试模式下记录）
        /// Error stack trace (recorded only in debug mode)
        /// </summary>
        public string? StackTrace { get; set; }

        /// <summary>
        /// 操作耗时
        /// Operation duration
        /// </summary>
        public TimeSpan? Duration { get; set; }

        /// <summary>
        /// 影响的资源数量
        /// Number of affected resources
        /// </summary>
        public int AffectedResourceCount { get; set; }

        /// <summary>
        /// 资源类型
        /// Resource type
        /// </summary>
        public string? ResourceType { get; set; }

        /// <summary>
        /// 相关的安全操作ID（用于关联多个相关操作）
        /// Related security operation ID (for correlating multiple related operations)
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// 是否包含敏感信息（用于清理验证）
        /// Whether contains sensitive information (for cleanup validation)
        /// </summary>
        public bool ContainsSensitiveData { get; set; }

        /// <summary>
        /// 清理状态（针对清理操作）
        /// Cleanup status (for cleanup operations)
        /// </summary>
        public CleanupStatus? CleanupStatus { get; set; }

        /// <summary>
        /// 验证哈希（用于完整性检查）
        /// Validation hash (for integrity checking)
        /// </summary>
        public string? ValidationHash { get; set; }

        /// <summary>
        /// 幂等性操作标识符（用于防止重复操作）
        /// Idempotency operation identifier (to prevent duplicate operations)
        /// </summary>
        public string? IdempotencyKey { get; set; }

        /// <summary>
        /// 前一次相同操作的时间戳（用于幂等性验证）
        /// Timestamp of previous same operation (for idempotency validation)
        /// </summary>
        public DateTime? PreviousOperationTimestamp { get; set; }

        /// <summary>
        /// 客户端信息
        /// Client information
        /// </summary>
        public ClientInfo? ClientInfo { get; set; }

        /// <summary>
        /// 系统环境信息
        /// System environment information
        /// </summary>
        public EnvironmentInfo? EnvironmentInfo { get; set; }

        /// <summary>
        /// 标签列表（用于分类和过滤）
        /// Tags list (for categorization and filtering)
        /// </summary>
        public HashSet<string> Tags { get; set; }

        /// <summary>
        /// 初始化审计日志
        /// Initializes audit log
        /// </summary>
        /// <param name="eventType">事件类型 Event type</param>
        /// <param name="description">事件描述 Event description</param>
        /// <param name="sessionId">会话标识 Session identifier</param>
        public AuditLog(AuditEventType eventType, string description, string sessionId)
        {
            Id = Guid.NewGuid().ToString("N");
            EventType = eventType;
            Description = description ?? throw new ArgumentNullException(nameof(description));
            SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
            Timestamp = DateTime.UtcNow;
            Details = new Dictionary<string, object>();
            Tags = new HashSet<string>();
            Result = AuditOperationResult.Unknown;
            Severity = AuditSeverity.Information;
        }

        /// <summary>
        /// 添加详细信息
        /// Adds detailed information
        /// </summary>
        /// <param name="key">键 Key</param>
        /// <param name="value">值 Value</param>
        /// <returns>当前实例 Current instance</returns>
        public AuditLog AddDetail(string key, object value)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                Details[key] = value;
            }
            return this;
        }

        /// <summary>
        /// 添加标签
        /// Adds a tag
        /// </summary>
        /// <param name="tag">标签 Tag</param>
        /// <returns>当前实例 Current instance</returns>
        public AuditLog AddTag(string tag)
        {
            if (!string.IsNullOrWhiteSpace(tag))
            {
                Tags.Add(tag.Trim().ToLowerInvariant());
            }
            return this;
        }

        /// <summary>
        /// 设置操作结果
        /// Sets operation result
        /// </summary>
        /// <param name="result">结果 Result</param>
        /// <param name="errorMessage">错误信息 Error message</param>
        /// <param name="stackTrace">堆栈跟踪 Stack trace</param>
        /// <returns>当前实例 Current instance</returns>
        public AuditLog SetResult(AuditOperationResult result, string? errorMessage = null, string? stackTrace = null)
        {
            Result = result;
            ErrorMessage = errorMessage;
            StackTrace = stackTrace;

            // 根据结果调整严重级别
            // Adjust severity based on result
            if (result == AuditOperationResult.Failed || result == AuditOperationResult.PartiallySucceeded)
            {
                Severity = result == AuditOperationResult.Failed ? AuditSeverity.Error : AuditSeverity.Warning;
            }
            else if (result == AuditOperationResult.Succeeded)
            {
                Severity = Severity == AuditSeverity.Information ? AuditSeverity.Information : Severity;
            }

            return this;
        }

        /// <summary>
        /// 设置清理状态
        /// Sets cleanup status
        /// </summary>
        /// <param name="status">清理状态 Cleanup status</param>
        /// <returns>当前实例 Current instance</returns>
        public AuditLog SetCleanupStatus(CleanupStatus status)
        {
            CleanupStatus = status;
            AddDetail("cleanup_status", status.ToString());
            return this;
        }

        /// <summary>
        /// 设置操作耗时
        /// Sets operation duration
        /// </summary>
        /// <param name="duration">耗时 Duration</param>
        /// <returns>当前实例 Current instance</returns>
        public AuditLog SetDuration(TimeSpan duration)
        {
            Duration = duration;
            AddDetail("duration_ms", duration.TotalMilliseconds);
            return this;
        }

        /// <summary>
        /// 生成日志的哈希值用于完整性验证
        /// Generates hash for log integrity verification
        /// </summary>
        /// <returns>哈希值 Hash value</returns>
        public string GenerateValidationHash()
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var content = $"{Id}|{EventType}|{Timestamp:O}|{SessionId}|{Description}|{Result}";
            var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
            ValidationHash = Convert.ToBase64String(hash);
            return ValidationHash;
        }

        /// <summary>
        /// 验证日志完整性
        /// Validates log integrity
        /// </summary>
        /// <returns>是否验证通过 Whether validation passed</returns>
        public bool ValidateIntegrity()
        {
            if (string.IsNullOrEmpty(ValidationHash))
                return false;

            var currentHash = GenerateValidationHash();
            return ValidationHash == currentHash;
        }

        /// <summary>
        /// 创建用于清理操作的审计日志
        /// Creates audit log for cleanup operations
        /// </summary>
        /// <param name="description">操作描述 Operation description</param>
        /// <param name="sessionId">会话ID Session ID</param>
        /// <param name="cleanupType">清理类型 Cleanup type</param>
        /// <returns>审计日志实例 Audit log instance</returns>
        public static AuditLog CreateCleanupLog(string description, string sessionId, string cleanupType)
        {
            return new AuditLog(AuditEventType.SecurityCleanup, description, sessionId)
                .AddDetail("cleanup_type", cleanupType)
                .AddTag("cleanup")
                .AddTag("security");
        }

        /// <summary>
        /// 创建用于验证操作的审计日志
        /// Creates audit log for validation operations
        /// </summary>
        /// <param name="description">操作描述 Operation description</param>
        /// <param name="sessionId">会话ID Session ID</param>
        /// <param name="validationType">验证类型 Validation type</param>
        /// <returns>审计日志实例 Audit log instance</returns>
        public static AuditLog CreateValidationLog(string description, string sessionId, string validationType)
        {
            return new AuditLog(AuditEventType.SecurityValidation, description, sessionId)
                .AddDetail("validation_type", validationType)
                .AddTag("validation")
                .AddTag("security");
        }

        /// <summary>
        /// 创建用于内存清理的审计日志
        /// Creates audit log for memory cleanup operations
        /// </summary>
        /// <param name="description">操作描述 Operation description</param>
        /// <param name="sessionId">会话ID Session ID</param>
        /// <returns>审计日志实例 Audit log instance</returns>
        public static AuditLog CreateMemoryCleanupLog(string description, string sessionId)
        {
            return new AuditLog(AuditEventType.MemoryCleanup, description, sessionId)
                .AddTag("memory")
                .AddTag("cleanup")
                .AddTag("gc");
        }
    }

    /// <summary>
    /// 审计事件类型
    /// Audit event types
    /// </summary>
    public enum AuditEventType
    {
        /// <summary>
        /// 安全初始化
        /// Security initialization
        /// </summary>
        SecurityInitialization,

        /// <summary>
        /// 敏感数据存储
        /// Sensitive data storage
        /// </summary>
        SensitiveDataStorage,

        /// <summary>
        /// 敏感数据检索
        /// Sensitive data retrieval
        /// </summary>
        SensitiveDataRetrieval,

        /// <summary>
        /// 安全清理
        /// Security cleanup
        /// </summary>
        SecurityCleanup,

        /// <summary>
        /// 内存清理
        /// Memory cleanup
        /// </summary>
        MemoryCleanup,

        /// <summary>
        /// 安全验证
        /// Security validation
        /// </summary>
        SecurityValidation,

        /// <summary>
        /// 清理触发器注册
        /// Cleanup trigger registration
        /// </summary>
        CleanupTriggerRegistration,

        /// <summary>
        /// 自动清理执行
        /// Automatic cleanup execution
        /// </summary>
        AutomaticCleanupExecution,

        /// <summary>
        /// 安全异常
        /// Security exception
        /// </summary>
        SecurityException,

        /// <summary>
        /// 配置更改
        /// Configuration change
        /// </summary>
        ConfigurationChange,

        /// <summary>
        /// 访问控制
        /// Access control
        /// </summary>
        AccessControl
    }

    /// <summary>
    /// 审计严重级别
    /// Audit severity levels
    /// </summary>
    public enum AuditSeverity
    {
        /// <summary>
        /// 调试信息
        /// Debug information
        /// </summary>
        Debug = 0,

        /// <summary>
        /// 一般信息
        /// General information
        /// </summary>
        Information = 1,

        /// <summary>
        /// 警告
        /// Warning
        /// </summary>
        Warning = 2,

        /// <summary>
        /// 错误
        /// Error
        /// </summary>
        Error = 3,

        /// <summary>
        /// 严重错误
        /// Critical error
        /// </summary>
        Critical = 4
    }

    /// <summary>
    /// 审计操作结果
    /// Audit operation results
    /// </summary>
    public enum AuditOperationResult
    {
        /// <summary>
        /// 未知结果
        /// Unknown result
        /// </summary>
        Unknown,

        /// <summary>
        /// 成功
        /// Succeeded
        /// </summary>
        Succeeded,

        /// <summary>
        /// 失败
        /// Failed
        /// </summary>
        Failed,

        /// <summary>
        /// 部分成功
        /// Partially succeeded
        /// </summary>
        PartiallySucceeded,

        /// <summary>
        /// 已跳过
        /// Skipped
        /// </summary>
        Skipped,

        /// <summary>
        /// 进行中
        /// In progress
        /// </summary>
        InProgress
    }

    /// <summary>
    /// 清理状态
    /// Cleanup status
    /// </summary>
    public enum CleanupStatus
    {
        /// <summary>
        /// 未开始
        /// Not started
        /// </summary>
        NotStarted,

        /// <summary>
        /// 进行中
        /// In progress
        /// </summary>
        InProgress,

        /// <summary>
        /// 已完成
        /// Completed
        /// </summary>
        Completed,

        /// <summary>
        /// 部分完成
        /// Partially completed
        /// </summary>
        PartiallyCompleted,

        /// <summary>
        /// 失败
        /// Failed
        /// </summary>
        Failed,

        /// <summary>
        /// 验证中
        /// Validating
        /// </summary>
        Validating,

        /// <summary>
        /// 验证通过
        /// Validated
        /// </summary>
        Validated,

        /// <summary>
        /// 验证失败
        /// ValidationFailed
        /// </summary>
        ValidationFailed
    }

    /// <summary>
    /// 客户端信息
    /// Client information
    /// </summary>
    public class ClientInfo
    {
        /// <summary>
        /// 应用程序名称
        /// Application name
        /// </summary>
        public string? ApplicationName { get; set; }

        /// <summary>
        /// 应用程序版本
        /// Application version
        /// </summary>
        public string? ApplicationVersion { get; set; }

        /// <summary>
        /// 进程ID
        /// Process ID
        /// </summary>
        public int ProcessId { get; set; }

        /// <summary>
        /// 线程ID
        /// Thread ID
        /// </summary>
        public int ThreadId { get; set; }

        /// <summary>
        /// 主机名
        /// Host name
        /// </summary>
        public string? HostName { get; set; }

        /// <summary>
        /// 用户名
        /// User name
        /// </summary>
        public string? UserName { get; set; }
    }

    /// <summary>
    /// 环境信息
    /// Environment information
    /// </summary>
    public class EnvironmentInfo
    {
        /// <summary>
        /// 操作系统
        /// Operating system
        /// </summary>
        public string? OperatingSystem { get; set; }

        /// <summary>
        /// .NET 版本
        /// .NET version
        /// </summary>
        public string? DotNetVersion { get; set; }

        /// <summary>
        /// 内存使用量
        /// Memory usage
        /// </summary>
        public long MemoryUsage { get; set; }

        /// <summary>
        /// CPU 使用率
        /// CPU usage
        /// </summary>
        public double CpuUsage { get; set; }

        /// <summary>
        /// 是否在调试模式
        /// Whether in debug mode
        /// </summary>
        public bool IsDebugMode { get; set; }
    }
}