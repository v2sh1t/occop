using System.Security;

namespace Occop.Core.Security
{
    /// <summary>
    /// 定义安全管理器的核心接口，提供敏感信息管理和自动清理功能
    /// Defines the core interface for security manager providing sensitive information management and automatic cleanup
    /// </summary>
    public interface ISecurityManager : IDisposable
    {
        /// <summary>
        /// 安全事件发生时触发
        /// Fired when security events occur
        /// </summary>
        event EventHandler<SecurityEventArgs>? SecurityEvent;

        /// <summary>
        /// 清理操作完成时触发
        /// Fired when cleanup operations complete
        /// </summary>
        event EventHandler<CleanupCompletedEventArgs>? CleanupCompleted;

        /// <summary>
        /// 获取安全管理器是否已初始化
        /// Gets whether the security manager is initialized
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// 获取当前安全上下文
        /// Gets the current security context
        /// </summary>
        SecurityContext SecurityContext { get; }

        /// <summary>
        /// 初始化安全管理器
        /// Initializes the security manager
        /// </summary>
        /// <param name="context">安全上下文 Security context</param>
        /// <returns>初始化任务 Initialization task</returns>
        Task InitializeAsync(SecurityContext context);

        /// <summary>
        /// 安全地存储敏感数据
        /// Securely stores sensitive data
        /// </summary>
        /// <param name="data">敏感数据 Sensitive data</param>
        /// <returns>存储操作任务 Storage operation task</returns>
        Task<SecureData> StoreSecureDataAsync(SecureString sensitiveData);

        /// <summary>
        /// 安全地检索敏感数据
        /// Securely retrieves sensitive data
        /// </summary>
        /// <param name="dataId">数据标识 Data identifier</param>
        /// <returns>敏感数据或null Sensitive data or null</returns>
        Task<SecureString?> RetrieveSecureDataAsync(string dataId);

        /// <summary>
        /// 清理指定的敏感数据
        /// Clears specified sensitive data
        /// </summary>
        /// <param name="dataId">数据标识 Data identifier</param>
        /// <returns>清理操作任务 Cleanup operation task</returns>
        Task<bool> ClearSecureDataAsync(string dataId);

        /// <summary>
        /// 清理所有敏感数据和安全状态
        /// Clears all sensitive data and security state
        /// </summary>
        /// <returns>清理操作任务 Cleanup operation task</returns>
        Task<bool> ClearAllSecurityStateAsync();

        /// <summary>
        /// 强制执行垃圾回收和内存清理
        /// Forces garbage collection and memory cleanup
        /// </summary>
        void ForceGarbageCollection();

        /// <summary>
        /// 注册清理触发器
        /// Registers cleanup triggers
        /// </summary>
        /// <param name="triggers">清理触发条件 Cleanup trigger conditions</param>
        void RegisterCleanupTriggers(CleanupTriggers triggers);

        /// <summary>
        /// 验证安全状态完整性
        /// Validates security state integrity
        /// </summary>
        /// <returns>验证结果 Validation result</returns>
        Task<SecurityValidationResult> ValidateSecurityStateAsync();

        /// <summary>
        /// 获取安全状态摘要
        /// Gets security state summary
        /// </summary>
        /// <returns>安全状态摘要 Security state summary</returns>
        SecurityStateSummary GetSecurityStateSummary();
    }

    /// <summary>
    /// 清理完成事件参数
    /// Cleanup completed event arguments
    /// </summary>
    public class CleanupCompletedEventArgs : EventArgs
    {
        /// <summary>
        /// 清理操作类型
        /// Cleanup operation type
        /// </summary>
        public CleanupOperationType OperationType { get; }

        /// <summary>
        /// 清理是否成功
        /// Whether cleanup was successful
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// 清理的项目数量
        /// Number of items cleaned
        /// </summary>
        public int ItemsCleared { get; }

        /// <summary>
        /// 清理耗时
        /// Cleanup duration
        /// </summary>
        public TimeSpan Duration { get; }

        /// <summary>
        /// 错误信息（如果有）
        /// Error message (if any)
        /// </summary>
        public string? ErrorMessage { get; }

        /// <summary>
        /// 初始化清理完成事件参数
        /// Initializes cleanup completed event arguments
        /// </summary>
        public CleanupCompletedEventArgs(
            CleanupOperationType operationType,
            bool isSuccess,
            int itemsCleared,
            TimeSpan duration,
            string? errorMessage = null)
        {
            OperationType = operationType;
            IsSuccess = isSuccess;
            ItemsCleared = itemsCleared;
            Duration = duration;
            ErrorMessage = errorMessage;
        }
    }

    /// <summary>
    /// 清理操作类型
    /// Cleanup operation types
    /// </summary>
    public enum CleanupOperationType
    {
        /// <summary>
        /// 单个数据项清理
        /// Individual data item cleanup
        /// </summary>
        SingleItem,

        /// <summary>
        /// 所有安全状态清理
        /// All security state cleanup
        /// </summary>
        AllSecurityState,

        /// <summary>
        /// 内存清理
        /// Memory cleanup
        /// </summary>
        MemoryCleanup,

        /// <summary>
        /// 自动触发清理
        /// Automatic triggered cleanup
        /// </summary>
        AutomaticCleanup,

        /// <summary>
        /// 应用程序退出清理
        /// Application exit cleanup
        /// </summary>
        ApplicationExit
    }

    /// <summary>
    /// 清理触发条件
    /// Cleanup trigger conditions
    /// </summary>
    public class CleanupTriggers
    {
        /// <summary>
        /// 应用程序退出时清理
        /// Cleanup on application exit
        /// </summary>
        public bool OnApplicationExit { get; set; } = true;

        /// <summary>
        /// 进程异常退出时清理
        /// Cleanup on process abnormal exit
        /// </summary>
        public bool OnProcessAbnormalExit { get; set; } = true;

        /// <summary>
        /// 超时后自动清理
        /// Automatic cleanup after timeout
        /// </summary>
        public bool OnTimeout { get; set; } = false;

        /// <summary>
        /// 自动清理超时时间（如果启用）
        /// Automatic cleanup timeout (if enabled)
        /// </summary>
        public TimeSpan? TimeoutDuration { get; set; }

        /// <summary>
        /// 系统关机时清理
        /// Cleanup on system shutdown
        /// </summary>
        public bool OnSystemShutdown { get; set; } = true;

        /// <summary>
        /// 内存压力时清理
        /// Cleanup on memory pressure
        /// </summary>
        public bool OnMemoryPressure { get; set; } = false;

        /// <summary>
        /// 克隆清理触发器配置
        /// Clones the cleanup trigger configuration
        /// </summary>
        /// <returns>克隆的配置 Cloned configuration</returns>
        public CleanupTriggers Clone()
        {
            return new CleanupTriggers
            {
                OnApplicationExit = OnApplicationExit,
                OnProcessAbnormalExit = OnProcessAbnormalExit,
                OnTimeout = OnTimeout,
                TimeoutDuration = TimeoutDuration,
                OnSystemShutdown = OnSystemShutdown,
                OnMemoryPressure = OnMemoryPressure
            };
        }
    }

    /// <summary>
    /// 安全验证结果
    /// Security validation result
    /// </summary>
    public class SecurityValidationResult
    {
        /// <summary>
        /// 验证是否通过
        /// Whether validation passed
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// 验证的项目数量
        /// Number of items validated
        /// </summary>
        public int ValidatedItems { get; set; }

        /// <summary>
        /// 发现的问题数量
        /// Number of issues found
        /// </summary>
        public int IssuesFound { get; set; }

        /// <summary>
        /// 验证消息列表
        /// List of validation messages
        /// </summary>
        public List<string> ValidationMessages { get; set; } = new();

        /// <summary>
        /// 验证时间戳
        /// Validation timestamp
        /// </summary>
        public DateTime ValidationTimestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// 安全状态摘要
    /// Security state summary
    /// </summary>
    public class SecurityStateSummary
    {
        /// <summary>
        /// 存储的安全数据项数量
        /// Number of stored secure data items
        /// </summary>
        public int SecureDataItemCount { get; set; }

        /// <summary>
        /// 活跃的清理触发器数量
        /// Number of active cleanup triggers
        /// </summary>
        public int ActiveCleanupTriggers { get; set; }

        /// <summary>
        /// 最后清理时间
        /// Last cleanup time
        /// </summary>
        public DateTime? LastCleanupTime { get; set; }

        /// <summary>
        /// 总清理操作次数
        /// Total cleanup operations count
        /// </summary>
        public int TotalCleanupOperations { get; set; }

        /// <summary>
        /// 安全管理器启动时间
        /// Security manager startup time
        /// </summary>
        public DateTime StartupTime { get; set; }

        /// <summary>
        /// 是否有内存泄露风险
        /// Whether there's memory leak risk
        /// </summary>
        public bool HasMemoryLeakRisk { get; set; }
    }
}