using System.Collections.Concurrent;
using System.Security;
using Occop.Core.Security;
using Occop.Core.Models.Security;

namespace Occop.Services.Security
{
    /// <summary>
    /// 安全审计器服务，负责安全事件的审计、日志记录和分析
    /// Security auditor service responsible for security event auditing, logging and analysis
    /// </summary>
    public class SecurityAuditor : IDisposable
    {
        private readonly object _lockObject = new object();
        private readonly ConcurrentQueue<AuditLog> _auditQueue = new();
        private readonly ConcurrentDictionary<string, AuditLog> _auditLogs = new();
        private readonly Timer? _flushTimer;
        private readonly SecurityContext _securityContext;
        private bool _disposed = false;

        /// <summary>
        /// 审计事件发生时触发
        /// Fired when audit events occur
        /// </summary>
        public event EventHandler<AuditEventArgs>? AuditEvent;

        /// <summary>
        /// 关键安全事件发生时触发
        /// Fired when critical security events occur
        /// </summary>
        public event EventHandler<CriticalSecurityEventArgs>? CriticalSecurityEvent;

        /// <summary>
        /// 获取审计器是否已启用
        /// Gets whether the auditor is enabled
        /// </summary>
        public bool IsEnabled { get; private set; }

        /// <summary>
        /// 获取审计日志总数
        /// Gets total audit log count
        /// </summary>
        public int TotalAuditLogs => _auditLogs.Count;

        /// <summary>
        /// 获取待处理的审计日志数
        /// Gets pending audit log count
        /// </summary>
        public int PendingAuditLogs => _auditQueue.Count;

        /// <summary>
        /// 审计配置
        /// Audit configuration
        /// </summary>
        public AuditConfiguration Configuration { get; private set; }

        /// <summary>
        /// 初始化安全审计器
        /// Initializes security auditor
        /// </summary>
        /// <param name="securityContext">安全上下文 Security context</param>
        /// <param name="configuration">审计配置 Audit configuration</param>
        public SecurityAuditor(SecurityContext securityContext, AuditConfiguration? configuration = null)
        {
            _securityContext = securityContext ?? throw new ArgumentNullException(nameof(securityContext));
            Configuration = configuration ?? new AuditConfiguration();

            IsEnabled = _securityContext.IsAuditLoggingEnabled;

            if (IsEnabled)
            {
                // 启动定时刷新定时器
                // Start the flush timer
                _flushTimer = new Timer(FlushAuditLogs, null, Configuration.FlushInterval, Configuration.FlushInterval);
            }
        }

        /// <summary>
        /// 启用审计器
        /// Enables the auditor
        /// </summary>
        /// <returns>启用任务 Enable task</returns>
        public async Task EnableAsync()
        {
            lock (_lockObject)
            {
                ThrowIfDisposed();
                if (!IsEnabled)
                {
                    IsEnabled = true;
                    LogAuditEvent(AuditEventType.ConfigurationChange, "Security auditor enabled", AuditSeverity.Information);
                }
            }
            await Task.CompletedTask;
        }

        /// <summary>
        /// 禁用审计器
        /// Disables the auditor
        /// </summary>
        /// <returns>禁用任务 Disable task</returns>
        public async Task DisableAsync()
        {
            lock (_lockObject)
            {
                ThrowIfDisposed();
                if (IsEnabled)
                {
                    IsEnabled = false;
                    LogAuditEvent(AuditEventType.ConfigurationChange, "Security auditor disabled", AuditSeverity.Warning);
                }
            }

            // 刷新剩余的审计日志
            // Flush remaining audit logs
            await FlushAuditLogsAsync();
        }

        /// <summary>
        /// 记录安全初始化事件
        /// Logs security initialization event
        /// </summary>
        /// <param name="description">描述 Description</param>
        /// <param name="details">详细信息 Details</param>
        /// <returns>审计日志ID Audit log ID</returns>
        public async Task<string> LogSecurityInitializationAsync(string description, Dictionary<string, object>? details = null)
        {
            var auditLog = AuditLog.CreateValidationLog(description, _securityContext.SessionId, "security_initialization")
                .AddTag("initialization")
                .AddTag("security");

            auditLog.EventType = AuditEventType.SecurityInitialization;

            if (details != null)
            {
                foreach (var detail in details)
                {
                    auditLog.AddDetail(detail.Key, detail.Value);
                }
            }

            await AddAuditLogAsync(auditLog);
            return auditLog.Id;
        }

        /// <summary>
        /// 记录清理操作事件
        /// Logs cleanup operation event
        /// </summary>
        /// <param name="cleanupType">清理类型 Cleanup type</param>
        /// <param name="description">描述 Description</param>
        /// <param name="result">操作结果 Operation result</param>
        /// <param name="itemsAffected">影响的项目数 Items affected</param>
        /// <param name="duration">耗时 Duration</param>
        /// <param name="errorMessage">错误信息 Error message</param>
        /// <returns>审计日志ID Audit log ID</returns>
        public async Task<string> LogCleanupOperationAsync(
            string cleanupType,
            string description,
            AuditOperationResult result,
            int itemsAffected = 0,
            TimeSpan? duration = null,
            string? errorMessage = null)
        {
            var auditLog = AuditLog.CreateCleanupLog(description, _securityContext.SessionId, cleanupType);

            auditLog.SetResult(result, errorMessage)
                .SetDuration(duration ?? TimeSpan.Zero)
                .AddDetail("cleanup_type", cleanupType)
                .AddDetail("items_affected", itemsAffected);

            auditLog.AffectedResourceCount = itemsAffected;
            auditLog.ResourceType = "sensitive_data";

            // 检查清理成功率
            // Check cleanup success rate
            if (result == AuditOperationResult.Failed || (itemsAffected > 0 && result != AuditOperationResult.Succeeded))
            {
                auditLog.Severity = AuditSeverity.Error;
                auditLog.AddTag("cleanup_failure");

                // 触发关键安全事件
                // Trigger critical security event
                await TriggerCriticalSecurityEventAsync("Cleanup operation failed", auditLog);
            }
            else if (result == AuditOperationResult.Succeeded && itemsAffected > 0)
            {
                auditLog.Severity = AuditSeverity.Information;
                auditLog.AddTag("cleanup_success");
            }

            await AddAuditLogAsync(auditLog);
            return auditLog.Id;
        }

        /// <summary>
        /// 记录内存清理事件
        /// Logs memory cleanup event
        /// </summary>
        /// <param name="description">描述 Description</param>
        /// <param name="memoryFreed">释放的内存量 Memory freed</param>
        /// <param name="gcCollections">GC回收次数 GC collections</param>
        /// <param name="duration">耗时 Duration</param>
        /// <returns>审计日志ID Audit log ID</returns>
        public async Task<string> LogMemoryCleanupAsync(
            string description,
            long memoryFreed,
            int gcCollections,
            TimeSpan duration)
        {
            var auditLog = AuditLog.CreateMemoryCleanupLog(description, _securityContext.SessionId)
                .SetDuration(duration)
                .SetResult(AuditOperationResult.Succeeded)
                .AddDetail("memory_freed_bytes", memoryFreed)
                .AddDetail("gc_collections", gcCollections);

            auditLog.AffectedResourceCount = 1;
            auditLog.ResourceType = "memory";

            await AddAuditLogAsync(auditLog);
            return auditLog.Id;
        }

        /// <summary>
        /// 记录敏感信息验证事件
        /// Logs sensitive information validation event
        /// </summary>
        /// <param name="validationType">验证类型 Validation type</param>
        /// <param name="description">描述 Description</param>
        /// <param name="result">验证结果 Validation result</param>
        /// <param name="sensitiveDataFound">发现的敏感数据 Sensitive data found</param>
        /// <param name="isZeroLeak">是否零泄露 Is zero leak</param>
        /// <returns>审计日志ID Audit log ID</returns>
        public async Task<string> LogSensitiveDataValidationAsync(
            string validationType,
            string description,
            ValidationResult result,
            List<SensitiveDataItem>? sensitiveDataFound = null,
            bool isZeroLeak = true)
        {
            var auditLog = AuditLog.CreateValidationLog(description, _securityContext.SessionId, validationType);

            auditLog.SetResult(result.IsValid ? AuditOperationResult.Succeeded : AuditOperationResult.Failed)
                .SetDuration(result.Duration)
                .AddDetail("validation_type", validationType)
                .AddDetail("zero_leak_achieved", isZeroLeak)
                .AddDetail("sensitive_items_found", sensitiveDataFound?.Count ?? 0);

            auditLog.ContainsSensitiveData = sensitiveDataFound?.Any() == true;
            auditLog.AffectedResourceCount = sensitiveDataFound?.Count ?? 0;

            if (!isZeroLeak || !result.IsValid)
            {
                auditLog.Severity = AuditSeverity.Critical;
                auditLog.AddTag("sensitive_leak");

                // 触发关键安全事件
                // Trigger critical security event
                await TriggerCriticalSecurityEventAsync("Sensitive data leak detected", auditLog);
            }
            else
            {
                auditLog.Severity = AuditSeverity.Information;
                auditLog.AddTag("zero_leak_verified");
            }

            await AddAuditLogAsync(auditLog);
            return auditLog.Id;
        }

        /// <summary>
        /// 记录幂等性验证事件
        /// Logs idempotency validation event
        /// </summary>
        /// <param name="operationId">操作ID Operation ID</param>
        /// <param name="description">描述 Description</param>
        /// <param name="isIdempotent">是否幂等 Is idempotent</param>
        /// <param name="previousOperationTime">上次操作时间 Previous operation time</param>
        /// <returns>审计日志ID Audit log ID</returns>
        public async Task<string> LogIdempotencyValidationAsync(
            string operationId,
            string description,
            bool isIdempotent,
            DateTime? previousOperationTime = null)
        {
            var auditLog = AuditLog.CreateValidationLog(description, _securityContext.SessionId, "idempotency")
                .SetResult(isIdempotent ? AuditOperationResult.Succeeded : AuditOperationResult.Failed)
                .AddDetail("operation_id", operationId)
                .AddDetail("is_idempotent", isIdempotent);

            auditLog.IdempotencyKey = operationId;
            auditLog.PreviousOperationTimestamp = previousOperationTime;

            if (!isIdempotent)
            {
                auditLog.Severity = AuditSeverity.Warning;
                auditLog.AddTag("idempotency_violation");
            }
            else
            {
                auditLog.AddTag("idempotency_verified");
            }

            await AddAuditLogAsync(auditLog);
            return auditLog.Id;
        }

        /// <summary>
        /// 记录安全异常事件
        /// Logs security exception event
        /// </summary>
        /// <param name="exception">异常 Exception</param>
        /// <param name="description">描述 Description</param>
        /// <param name="context">上下文 Context</param>
        /// <returns>审计日志ID Audit log ID</returns>
        public async Task<string> LogSecurityExceptionAsync(
            Exception exception,
            string description,
            Dictionary<string, object>? context = null)
        {
            var auditLog = new AuditLog(AuditEventType.SecurityException, description, _securityContext.SessionId)
                .SetResult(AuditOperationResult.Failed, exception.Message, exception.StackTrace)
                .AddDetail("exception_type", exception.GetType().Name)
                .AddDetail("exception_message", exception.Message)
                .AddTag("exception")
                .AddTag("security_error");

            auditLog.Severity = AuditSeverity.Error;

            if (context != null)
            {
                foreach (var item in context)
                {
                    auditLog.AddDetail(item.Key, item.Value);
                }
            }

            // 关键异常需要特别处理
            // Critical exceptions need special handling
            if (exception is SecurityException || exception is UnauthorizedAccessException)
            {
                auditLog.Severity = AuditSeverity.Critical;
                auditLog.AddTag("critical_security_exception");

                await TriggerCriticalSecurityEventAsync("Critical security exception occurred", auditLog);
            }

            await AddAuditLogAsync(auditLog);
            return auditLog.Id;
        }

        /// <summary>
        /// 获取审计日志统计信息
        /// Gets audit log statistics
        /// </summary>
        /// <param name="timeRange">时间范围 Time range</param>
        /// <returns>统计信息 Statistics</returns>
        public async Task<AuditStatistics> GetAuditStatisticsAsync(TimeSpan? timeRange = null)
        {
            await Task.CompletedTask;

            var cutoffTime = timeRange.HasValue ? DateTime.UtcNow.Subtract(timeRange.Value) : DateTime.MinValue;
            var relevantLogs = _auditLogs.Values.Where(log => log.Timestamp >= cutoffTime).ToList();

            return new AuditStatistics
            {
                TotalLogs = relevantLogs.Count,
                CriticalEvents = relevantLogs.Count(l => l.Severity == AuditSeverity.Critical),
                ErrorEvents = relevantLogs.Count(l => l.Severity == AuditSeverity.Error),
                WarningEvents = relevantLogs.Count(l => l.Severity == AuditSeverity.Warning),
                InfoEvents = relevantLogs.Count(l => l.Severity == AuditSeverity.Information),
                CleanupOperations = relevantLogs.Count(l => l.EventType == AuditEventType.SecurityCleanup),
                ValidationOperations = relevantLogs.Count(l => l.EventType == AuditEventType.SecurityValidation),
                SuccessfulOperations = relevantLogs.Count(l => l.Result == AuditOperationResult.Succeeded),
                FailedOperations = relevantLogs.Count(l => l.Result == AuditOperationResult.Failed),
                TimeRange = timeRange ?? TimeSpan.FromTicks(DateTime.UtcNow.Ticks - (relevantLogs.FirstOrDefault()?.Timestamp.Ticks ?? DateTime.UtcNow.Ticks)),
                GeneratedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// 验证审计日志完整性
        /// Validates audit log integrity
        /// </summary>
        /// <returns>验证结果 Validation result</returns>
        public async Task<ValidationResult> ValidateAuditIntegrityAsync()
        {
            var validationResult = ValidationResult.CreateCleanupValidation("audit_logs", _securityContext.SessionId);
            validationResult.Start();

            int validLogs = 0;
            int invalidLogs = 0;
            var corruptedLogs = new List<string>();

            await Task.Run(() =>
            {
                foreach (var log in _auditLogs.Values)
                {
                    if (log.ValidateIntegrity())
                    {
                        validLogs++;
                    }
                    else
                    {
                        invalidLogs++;
                        corruptedLogs.Add(log.Id);
                    }
                }
            });

            validationResult.AddContext("valid_logs", validLogs)
                           .AddContext("invalid_logs", invalidLogs)
                           .AddContext("corrupted_log_ids", corruptedLogs);

            if (invalidLogs > 0)
            {
                validationResult.AddError($"Found {invalidLogs} corrupted audit logs", "integrity_validator")
                               .Complete(false, 0.0);
            }
            else
            {
                validationResult.AddInfo($"All {validLogs} audit logs passed integrity validation", "integrity_validator")
                               .Complete(true, 1.0);
            }

            return validationResult;
        }

        /// <summary>
        /// 清理过期的审计日志
        /// Cleans up expired audit logs
        /// </summary>
        /// <param name="maxAge">最大年龄 Maximum age</param>
        /// <returns>清理的日志数量 Number of logs cleaned</returns>
        public async Task<int> CleanupExpiredLogsAsync(TimeSpan maxAge)
        {
            await Task.CompletedTask;

            var cutoffTime = DateTime.UtcNow.Subtract(maxAge);
            var expiredLogs = _auditLogs.Where(kvp => kvp.Value.Timestamp < cutoffTime).ToList();

            int removedCount = 0;
            foreach (var expiredLog in expiredLogs)
            {
                if (_auditLogs.TryRemove(expiredLog.Key, out _))
                {
                    removedCount++;
                }
            }

            if (removedCount > 0)
            {
                await LogAuditEventAsync(AuditEventType.ConfigurationChange,
                    $"Cleaned up {removedCount} expired audit logs",
                    AuditSeverity.Information);
            }

            return removedCount;
        }

        /// <summary>
        /// 添加审计日志
        /// Adds audit log
        /// </summary>
        /// <param name="auditLog">审计日志 Audit log</param>
        /// <returns>添加任务 Add task</returns>
        private async Task AddAuditLogAsync(AuditLog auditLog)
        {
            if (!IsEnabled || auditLog == null)
                return;

            ThrowIfDisposed();

            // 生成验证哈希
            // Generate validation hash
            auditLog.GenerateValidationHash();

            // 设置客户端和环境信息
            // Set client and environment information
            SetAuditLogMetadata(auditLog);

            // 添加到队列和字典
            // Add to queue and dictionary
            _auditQueue.Enqueue(auditLog);
            _auditLogs[auditLog.Id] = auditLog;

            // 触发审计事件
            // Trigger audit event
            AuditEvent?.Invoke(this, new AuditEventArgs(auditLog));

            await Task.CompletedTask;
        }

        /// <summary>
        /// 记录简单审计事件
        /// Logs simple audit event
        /// </summary>
        /// <param name="eventType">事件类型 Event type</param>
        /// <param name="description">描述 Description</param>
        /// <param name="severity">严重级别 Severity</param>
        /// <returns>审计日志ID Audit log ID</returns>
        private async Task<string> LogAuditEventAsync(AuditEventType eventType, string description, AuditSeverity severity)
        {
            var auditLog = new AuditLog(eventType, description, _securityContext.SessionId)
            {
                Severity = severity
            };

            await AddAuditLogAsync(auditLog);
            return auditLog.Id;
        }

        /// <summary>
        /// 记录简单审计事件（同步版本）
        /// Logs simple audit event (sync version)
        /// </summary>
        /// <param name="eventType">事件类型 Event type</param>
        /// <param name="description">描述 Description</param>
        /// <param name="severity">严重级别 Severity</param>
        private void LogAuditEvent(AuditEventType eventType, string description, AuditSeverity severity)
        {
            _ = Task.Run(async () => await LogAuditEventAsync(eventType, description, severity));
        }

        /// <summary>
        /// 触发关键安全事件
        /// Triggers critical security event
        /// </summary>
        /// <param name="message">消息 Message</param>
        /// <param name="auditLog">审计日志 Audit log</param>
        /// <returns>触发任务 Trigger task</returns>
        private async Task TriggerCriticalSecurityEventAsync(string message, AuditLog auditLog)
        {
            var criticalEvent = new CriticalSecurityEventArgs(message, auditLog, DateTime.UtcNow);
            CriticalSecurityEvent?.Invoke(this, criticalEvent);
            await Task.CompletedTask;
        }

        /// <summary>
        /// 设置审计日志元数据
        /// Sets audit log metadata
        /// </summary>
        /// <param name="auditLog">审计日志 Audit log</param>
        private void SetAuditLogMetadata(AuditLog auditLog)
        {
            auditLog.ClientInfo = new ClientInfo
            {
                ApplicationName = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name,
                ApplicationVersion = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString(),
                ProcessId = Environment.ProcessId,
                ThreadId = Environment.CurrentManagedThreadId,
                HostName = Environment.MachineName,
                UserName = Environment.UserName
            };

            auditLog.EnvironmentInfo = new EnvironmentInfo
            {
                OperatingSystem = Environment.OSVersion.ToString(),
                DotNetVersion = Environment.Version.ToString(),
                MemoryUsage = GC.GetTotalMemory(false),
                IsDebugMode = _securityContext.IsDebugMode
            };

            // 添加相关标签
            // Add relevant tags
            auditLog.AddTag(_securityContext.ApplicationId)
                   .AddTag($"session_{_securityContext.SessionId}")
                   .AddTag($"security_level_{_securityContext.SecurityLevel}");

            if (_securityContext.CurrentUser != null)
            {
                auditLog.UserId = _securityContext.CurrentUser.UserId;
                auditLog.AddTag($"user_{_securityContext.CurrentUser.Username}");
            }
        }

        /// <summary>
        /// 刷新审计日志（定时器回调）
        /// Flushes audit logs (timer callback)
        /// </summary>
        /// <param name="state">状态 State</param>
        private void FlushAuditLogs(object? state)
        {
            _ = Task.Run(async () => await FlushAuditLogsAsync());
        }

        /// <summary>
        /// 异步刷新审计日志
        /// Asynchronously flushes audit logs
        /// </summary>
        /// <returns>刷新任务 Flush task</returns>
        private async Task FlushAuditLogsAsync()
        {
            if (!IsEnabled)
                return;

            // 这里可以添加将日志写入持久化存储的逻辑
            // Here you can add logic to write logs to persistent storage
            // 例如：数据库、文件、远程日志服务等
            // For example: database, files, remote logging service, etc.

            await Task.CompletedTask;
        }

        /// <summary>
        /// 检查对象是否已释放并抛出异常
        /// Checks if object is disposed and throws exception
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SecurityAuditor));
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
                    // 停止定时器
                    // Stop timer
                    _flushTimer?.Dispose();

                    // 刷新剩余的审计日志
                    // Flush remaining audit logs
                    _ = Task.Run(async () => await FlushAuditLogsAsync());

                    // 记录审计器关闭事件
                    // Log auditor shutdown event
                    LogAuditEvent(AuditEventType.ConfigurationChange, "Security auditor disposed", AuditSeverity.Information);

                    // 清理集合
                    // Clear collections
                    _auditLogs.Clear();
                    while (_auditQueue.TryDequeue(out _)) { }
                }

                _disposed = true;
            }
        }
    }

    /// <summary>
    /// 审计事件参数
    /// Audit event arguments
    /// </summary>
    public class AuditEventArgs : EventArgs
    {
        /// <summary>
        /// 审计日志
        /// Audit log
        /// </summary>
        public AuditLog AuditLog { get; }

        /// <summary>
        /// 事件时间戳
        /// Event timestamp
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// 初始化审计事件参数
        /// Initializes audit event arguments
        /// </summary>
        /// <param name="auditLog">审计日志 Audit log</param>
        public AuditEventArgs(AuditLog auditLog)
        {
            AuditLog = auditLog ?? throw new ArgumentNullException(nameof(auditLog));
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// 关键安全事件参数
    /// Critical security event arguments
    /// </summary>
    public class CriticalSecurityEventArgs : EventArgs
    {
        /// <summary>
        /// 事件消息
        /// Event message
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 相关的审计日志
        /// Related audit log
        /// </summary>
        public AuditLog AuditLog { get; }

        /// <summary>
        /// 事件时间戳
        /// Event timestamp
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// 初始化关键安全事件参数
        /// Initializes critical security event arguments
        /// </summary>
        /// <param name="message">事件消息 Event message</param>
        /// <param name="auditLog">审计日志 Audit log</param>
        /// <param name="timestamp">时间戳 Timestamp</param>
        public CriticalSecurityEventArgs(string message, AuditLog auditLog, DateTime timestamp)
        {
            Message = message ?? throw new ArgumentNullException(nameof(message));
            AuditLog = auditLog ?? throw new ArgumentNullException(nameof(auditLog));
            Timestamp = timestamp;
        }
    }

    /// <summary>
    /// 审计配置
    /// Audit configuration
    /// </summary>
    public class AuditConfiguration
    {
        /// <summary>
        /// 刷新间隔
        /// Flush interval
        /// </summary>
        public TimeSpan FlushInterval { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// 最大日志保留时间
        /// Maximum log retention time
        /// </summary>
        public TimeSpan MaxLogRetention { get; set; } = TimeSpan.FromDays(30);

        /// <summary>
        /// 最大内存中日志数量
        /// Maximum in-memory log count
        /// </summary>
        public int MaxInMemoryLogs { get; set; } = 10000;

        /// <summary>
        /// 是否启用详细日志
        /// Whether to enable verbose logging
        /// </summary>
        public bool EnableVerboseLogging { get; set; } = false;

        /// <summary>
        /// 是否记录堆栈跟踪
        /// Whether to log stack traces
        /// </summary>
        public bool LogStackTraces { get; set; } = false;

        /// <summary>
        /// 是否启用性能监控
        /// Whether to enable performance monitoring
        /// </summary>
        public bool EnablePerformanceMonitoring { get; set; } = true;
    }

    /// <summary>
    /// 审计统计信息
    /// Audit statistics
    /// </summary>
    public class AuditStatistics
    {
        /// <summary>
        /// 总日志数
        /// Total log count
        /// </summary>
        public int TotalLogs { get; set; }

        /// <summary>
        /// 关键事件数
        /// Critical event count
        /// </summary>
        public int CriticalEvents { get; set; }

        /// <summary>
        /// 错误事件数
        /// Error event count
        /// </summary>
        public int ErrorEvents { get; set; }

        /// <summary>
        /// 警告事件数
        /// Warning event count
        /// </summary>
        public int WarningEvents { get; set; }

        /// <summary>
        /// 信息事件数
        /// Information event count
        /// </summary>
        public int InfoEvents { get; set; }

        /// <summary>
        /// 清理操作数
        /// Cleanup operation count
        /// </summary>
        public int CleanupOperations { get; set; }

        /// <summary>
        /// 验证操作数
        /// Validation operation count
        /// </summary>
        public int ValidationOperations { get; set; }

        /// <summary>
        /// 成功操作数
        /// Successful operation count
        /// </summary>
        public int SuccessfulOperations { get; set; }

        /// <summary>
        /// 失败操作数
        /// Failed operation count
        /// </summary>
        public int FailedOperations { get; set; }

        /// <summary>
        /// 统计时间范围
        /// Statistics time range
        /// </summary>
        public TimeSpan TimeRange { get; set; }

        /// <summary>
        /// 统计生成时间
        /// Statistics generation time
        /// </summary>
        public DateTime GeneratedAt { get; set; }

        /// <summary>
        /// 成功率
        /// Success rate
        /// </summary>
        public double SuccessRate => TotalLogs > 0 ? (double)SuccessfulOperations / TotalLogs : 0.0;

        /// <summary>
        /// 错误率
        /// Error rate
        /// </summary>
        public double ErrorRate => TotalLogs > 0 ? (double)(ErrorEvents + CriticalEvents) / TotalLogs : 0.0;
    }
}