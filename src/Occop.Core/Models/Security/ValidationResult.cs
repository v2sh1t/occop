using Occop.Core.Security;

namespace Occop.Core.Models.Security
{
    /// <summary>
    /// 安全验证结果模型，扩展基础的SecurityValidationResult
    /// Security validation result model that extends the base SecurityValidationResult
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// 验证结果唯一标识符
        /// Unique identifier for the validation result
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// 验证类型
        /// Validation type
        /// </summary>
        public ValidationType ValidationType { get; set; }

        /// <summary>
        /// 验证是否通过
        /// Whether validation passed
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// 验证置信度（0.0 - 1.0）
        /// Validation confidence (0.0 - 1.0)
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// 验证开始时间
        /// Validation start time
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// 验证结束时间
        /// Validation end time
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// 验证耗时
        /// Validation duration
        /// </summary>
        public TimeSpan Duration => EndTime - StartTime;

        /// <summary>
        /// 验证的目标资源
        /// Target resource being validated
        /// </summary>
        public string TargetResource { get; set; }

        /// <summary>
        /// 验证规则列表
        /// List of validation rules
        /// </summary>
        public List<ValidationRule> Rules { get; set; }

        /// <summary>
        /// 验证消息列表
        /// List of validation messages
        /// </summary>
        public List<ValidationMessage> Messages { get; set; }

        /// <summary>
        /// 验证的项目数量
        /// Number of items validated
        /// </summary>
        public int ValidatedItems { get; set; }

        /// <summary>
        /// 通过验证的项目数量
        /// Number of items that passed validation
        /// </summary>
        public int PassedItems { get; set; }

        /// <summary>
        /// 失败的项目数量
        /// Number of items that failed validation
        /// </summary>
        public int FailedItems { get; set; }

        /// <summary>
        /// 跳过的项目数量
        /// Number of items that were skipped
        /// </summary>
        public int SkippedItems { get; set; }

        /// <summary>
        /// 发现的问题数量
        /// Number of issues found
        /// </summary>
        public int IssuesFound { get; set; }

        /// <summary>
        /// 严重问题数量
        /// Number of critical issues
        /// </summary>
        public int CriticalIssues { get; set; }

        /// <summary>
        /// 警告数量
        /// Number of warnings
        /// </summary>
        public int Warnings { get; set; }

        /// <summary>
        /// 验证方法信息
        /// Validation method information
        /// </summary>
        public ValidationMethodInfo? MethodInfo { get; set; }

        /// <summary>
        /// 验证上下文信息
        /// Validation context information
        /// </summary>
        public Dictionary<string, object> Context { get; set; }

        /// <summary>
        /// 验证结果的校验和（用于完整性检查）
        /// Checksum of the validation result (for integrity checking)
        /// </summary>
        public string? Checksum { get; set; }

        /// <summary>
        /// 关联的审计日志ID
        /// Associated audit log ID
        /// </summary>
        public string? AuditLogId { get; set; }

        /// <summary>
        /// 验证会话ID
        /// Validation session ID
        /// </summary>
        public string SessionId { get; set; }

        /// <summary>
        /// 是否需要重新验证
        /// Whether re-validation is needed
        /// </summary>
        public bool RequiresRevalidation { get; set; }

        /// <summary>
        /// 下次验证建议时间
        /// Recommended next validation time
        /// </summary>
        public DateTime? NextValidationTime { get; set; }

        /// <summary>
        /// 验证结果详细数据
        /// Detailed validation result data
        /// </summary>
        public ValidationResultDetails? Details { get; set; }

        /// <summary>
        /// 标签集合（用于分类和过滤）
        /// Tags collection (for categorization and filtering)
        /// </summary>
        public HashSet<string> Tags { get; set; }

        /// <summary>
        /// 初始化验证结果
        /// Initializes validation result
        /// </summary>
        /// <param name="validationType">验证类型 Validation type</param>
        /// <param name="targetResource">目标资源 Target resource</param>
        /// <param name="sessionId">会话ID Session ID</param>
        public ValidationResult(ValidationType validationType, string targetResource, string sessionId)
        {
            Id = Guid.NewGuid().ToString("N");
            ValidationType = validationType;
            TargetResource = targetResource ?? throw new ArgumentNullException(nameof(targetResource));
            SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
            StartTime = DateTime.UtcNow;
            EndTime = StartTime;
            Confidence = 0.0;

            Rules = new List<ValidationRule>();
            Messages = new List<ValidationMessage>();
            Context = new Dictionary<string, object>();
            Tags = new HashSet<string>();

            // 根据验证类型设置默认标签
            // Set default tags based on validation type
            Tags.Add("validation");
            Tags.Add(validationType.ToString().ToLowerInvariant());
        }

        /// <summary>
        /// 开始验证
        /// Starts validation
        /// </summary>
        /// <returns>当前实例 Current instance</returns>
        public ValidationResult Start()
        {
            StartTime = DateTime.UtcNow;
            EndTime = StartTime;
            return this;
        }

        /// <summary>
        /// 完成验证
        /// Completes validation
        /// </summary>
        /// <param name="isValid">验证结果 Validation result</param>
        /// <param name="confidence">置信度 Confidence</param>
        /// <returns>当前实例 Current instance</returns>
        public ValidationResult Complete(bool isValid, double confidence = 1.0)
        {
            EndTime = DateTime.UtcNow;
            IsValid = isValid;
            Confidence = Math.Max(0.0, Math.Min(1.0, confidence));

            // 计算统计信息
            // Calculate statistics
            CalculateStatistics();

            // 生成校验和
            // Generate checksum
            GenerateChecksum();

            return this;
        }

        /// <summary>
        /// 添加验证规则
        /// Adds validation rule
        /// </summary>
        /// <param name="rule">验证规则 Validation rule</param>
        /// <returns>当前实例 Current instance</returns>
        public ValidationResult AddRule(ValidationRule rule)
        {
            if (rule != null)
            {
                Rules.Add(rule);
            }
            return this;
        }

        /// <summary>
        /// 添加验证消息
        /// Adds validation message
        /// </summary>
        /// <param name="message">验证消息 Validation message</param>
        /// <returns>当前实例 Current instance</returns>
        public ValidationResult AddMessage(ValidationMessage message)
        {
            if (message != null)
            {
                Messages.Add(message);
            }
            return this;
        }

        /// <summary>
        /// 添加信息消息
        /// Adds information message
        /// </summary>
        /// <param name="message">消息内容 Message content</param>
        /// <param name="source">消息来源 Message source</param>
        /// <returns>当前实例 Current instance</returns>
        public ValidationResult AddInfo(string message, string? source = null)
        {
            return AddMessage(new ValidationMessage(ValidationMessageType.Information, message, source));
        }

        /// <summary>
        /// 添加警告消息
        /// Adds warning message
        /// </summary>
        /// <param name="message">消息内容 Message content</param>
        /// <param name="source">消息来源 Message source</param>
        /// <returns>当前实例 Current instance</returns>
        public ValidationResult AddWarning(string message, string? source = null)
        {
            return AddMessage(new ValidationMessage(ValidationMessageType.Warning, message, source));
        }

        /// <summary>
        /// 添加错误消息
        /// Adds error message
        /// </summary>
        /// <param name="message">消息内容 Message content</param>
        /// <param name="source">消息来源 Message source</param>
        /// <returns>当前实例 Current instance</returns>
        public ValidationResult AddError(string message, string? source = null)
        {
            return AddMessage(new ValidationMessage(ValidationMessageType.Error, message, source));
        }

        /// <summary>
        /// 添加关键错误消息
        /// Adds critical error message
        /// </summary>
        /// <param name="message">消息内容 Message content</param>
        /// <param name="source">消息来源 Message source</param>
        /// <returns>当前实例 Current instance</returns>
        public ValidationResult AddCritical(string message, string? source = null)
        {
            return AddMessage(new ValidationMessage(ValidationMessageType.Critical, message, source));
        }

        /// <summary>
        /// 添加上下文信息
        /// Adds context information
        /// </summary>
        /// <param name="key">键 Key</param>
        /// <param name="value">值 Value</param>
        /// <returns>当前实例 Current instance</returns>
        public ValidationResult AddContext(string key, object value)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                Context[key] = value;
            }
            return this;
        }

        /// <summary>
        /// 添加标签
        /// Adds tag
        /// </summary>
        /// <param name="tag">标签 Tag</param>
        /// <returns>当前实例 Current instance</returns>
        public ValidationResult AddTag(string tag)
        {
            if (!string.IsNullOrWhiteSpace(tag))
            {
                Tags.Add(tag.Trim().ToLowerInvariant());
            }
            return this;
        }

        /// <summary>
        /// 计算统计信息
        /// Calculates statistics
        /// </summary>
        private void CalculateStatistics()
        {
            IssuesFound = Messages.Count(m => m.Type == ValidationMessageType.Error || m.Type == ValidationMessageType.Critical);
            CriticalIssues = Messages.Count(m => m.Type == ValidationMessageType.Critical);
            Warnings = Messages.Count(m => m.Type == ValidationMessageType.Warning);

            PassedItems = Rules.Count(r => r.Result == ValidationRuleResult.Passed);
            FailedItems = Rules.Count(r => r.Result == ValidationRuleResult.Failed);
            SkippedItems = Rules.Count(r => r.Result == ValidationRuleResult.Skipped);
            ValidatedItems = PassedItems + FailedItems + SkippedItems;
        }

        /// <summary>
        /// 生成校验和
        /// Generates checksum
        /// </summary>
        private void GenerateChecksum()
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var content = $"{Id}|{ValidationType}|{IsValid}|{StartTime:O}|{EndTime:O}|{ValidatedItems}|{IssuesFound}";
            var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
            Checksum = Convert.ToBase64String(hash);
        }

        /// <summary>
        /// 验证校验和完整性
        /// Validates checksum integrity
        /// </summary>
        /// <returns>是否完整 Whether integrity is intact</returns>
        public bool ValidateIntegrity()
        {
            if (string.IsNullOrEmpty(Checksum))
                return false;

            var originalChecksum = Checksum;
            GenerateChecksum();
            return originalChecksum == Checksum;
        }

        /// <summary>
        /// 转换为基础的SecurityValidationResult
        /// Converts to base SecurityValidationResult
        /// </summary>
        /// <returns>SecurityValidationResult实例 SecurityValidationResult instance</returns>
        public SecurityValidationResult ToSecurityValidationResult()
        {
            return new SecurityValidationResult
            {
                IsValid = IsValid,
                ValidatedItems = ValidatedItems,
                IssuesFound = IssuesFound,
                ValidationMessages = Messages.Select(m => m.Message).ToList(),
                ValidationTimestamp = EndTime
            };
        }

        /// <summary>
        /// 创建清理验证结果
        /// Creates cleanup validation result
        /// </summary>
        /// <param name="targetResource">目标资源 Target resource</param>
        /// <param name="sessionId">会话ID Session ID</param>
        /// <returns>验证结果实例 Validation result instance</returns>
        public static ValidationResult CreateCleanupValidation(string targetResource, string sessionId)
        {
            return new ValidationResult(ValidationType.CleanupValidation, targetResource, sessionId)
                .AddTag("cleanup")
                .AddTag("security");
        }

        /// <summary>
        /// 创建内存清理验证结果
        /// Creates memory cleanup validation result
        /// </summary>
        /// <param name="sessionId">会话ID Session ID</param>
        /// <returns>验证结果实例 Validation result instance</returns>
        public static ValidationResult CreateMemoryValidation(string sessionId)
        {
            return new ValidationResult(ValidationType.MemoryLeakValidation, "memory", sessionId)
                .AddTag("memory")
                .AddTag("leak-detection");
        }

        /// <summary>
        /// 创建幂等性验证结果
        /// Creates idempotency validation result
        /// </summary>
        /// <param name="operationId">操作ID Operation ID</param>
        /// <param name="sessionId">会话ID Session ID</param>
        /// <returns>验证结果实例 Validation result instance</returns>
        public static ValidationResult CreateIdempotencyValidation(string operationId, string sessionId)
        {
            return new ValidationResult(ValidationType.IdempotencyValidation, operationId, sessionId)
                .AddTag("idempotency")
                .AddTag("operation");
        }
    }

    /// <summary>
    /// 验证类型
    /// Validation types
    /// </summary>
    public enum ValidationType
    {
        /// <summary>
        /// 清理验证
        /// Cleanup validation
        /// </summary>
        CleanupValidation,

        /// <summary>
        /// 内存泄露验证
        /// Memory leak validation
        /// </summary>
        MemoryLeakValidation,

        /// <summary>
        /// 敏感信息泄露验证
        /// Sensitive data leak validation
        /// </summary>
        SensitiveDataLeakValidation,

        /// <summary>
        /// 完整性验证
        /// Integrity validation
        /// </summary>
        IntegrityValidation,

        /// <summary>
        /// 幂等性验证
        /// Idempotency validation
        /// </summary>
        IdempotencyValidation,

        /// <summary>
        /// 配置验证
        /// Configuration validation
        /// </summary>
        ConfigurationValidation,

        /// <summary>
        /// 权限验证
        /// Permission validation
        /// </summary>
        PermissionValidation,

        /// <summary>
        /// 状态验证
        /// State validation
        /// </summary>
        StateValidation
    }

    /// <summary>
    /// 验证规则
    /// Validation rule
    /// </summary>
    public class ValidationRule
    {
        /// <summary>
        /// 规则名称
        /// Rule name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 规则描述
        /// Rule description
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 规则优先级
        /// Rule priority
        /// </summary>
        public ValidationRulePriority Priority { get; set; }

        /// <summary>
        /// 规则结果
        /// Rule result
        /// </summary>
        public ValidationRuleResult Result { get; set; }

        /// <summary>
        /// 执行时间
        /// Execution time
        /// </summary>
        public TimeSpan ExecutionTime { get; set; }

        /// <summary>
        /// 错误信息（如果规则失败）
        /// Error message (if rule failed)
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 初始化验证规则
        /// Initializes validation rule
        /// </summary>
        /// <param name="name">规则名称 Rule name</param>
        /// <param name="description">规则描述 Rule description</param>
        /// <param name="priority">规则优先级 Rule priority</param>
        public ValidationRule(string name, string description, ValidationRulePriority priority = ValidationRulePriority.Normal)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            Priority = priority;
            Result = ValidationRuleResult.NotExecuted;
        }
    }

    /// <summary>
    /// 验证规则优先级
    /// Validation rule priority
    /// </summary>
    public enum ValidationRulePriority
    {
        /// <summary>
        /// 低优先级
        /// Low priority
        /// </summary>
        Low,

        /// <summary>
        /// 正常优先级
        /// Normal priority
        /// </summary>
        Normal,

        /// <summary>
        /// 高优先级
        /// High priority
        /// </summary>
        High,

        /// <summary>
        /// 关键优先级
        /// Critical priority
        /// </summary>
        Critical
    }

    /// <summary>
    /// 验证规则结果
    /// Validation rule result
    /// </summary>
    public enum ValidationRuleResult
    {
        /// <summary>
        /// 未执行
        /// Not executed
        /// </summary>
        NotExecuted,

        /// <summary>
        /// 通过
        /// Passed
        /// </summary>
        Passed,

        /// <summary>
        /// 失败
        /// Failed
        /// </summary>
        Failed,

        /// <summary>
        /// 跳过
        /// Skipped
        /// </summary>
        Skipped,

        /// <summary>
        /// 错误
        /// Error
        /// </summary>
        Error
    }

    /// <summary>
    /// 验证消息
    /// Validation message
    /// </summary>
    public class ValidationMessage
    {
        /// <summary>
        /// 消息类型
        /// Message type
        /// </summary>
        public ValidationMessageType Type { get; set; }

        /// <summary>
        /// 消息内容
        /// Message content
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 消息来源
        /// Message source
        /// </summary>
        public string? Source { get; set; }

        /// <summary>
        /// 消息时间戳
        /// Message timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 相关的规则名称
        /// Related rule name
        /// </summary>
        public string? RuleName { get; set; }

        /// <summary>
        /// 初始化验证消息
        /// Initializes validation message
        /// </summary>
        /// <param name="type">消息类型 Message type</param>
        /// <param name="message">消息内容 Message content</param>
        /// <param name="source">消息来源 Message source</param>
        public ValidationMessage(ValidationMessageType type, string message, string? source = null)
        {
            Type = type;
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Source = source;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// 验证消息类型
    /// Validation message types
    /// </summary>
    public enum ValidationMessageType
    {
        /// <summary>
        /// 信息
        /// Information
        /// </summary>
        Information,

        /// <summary>
        /// 警告
        /// Warning
        /// </summary>
        Warning,

        /// <summary>
        /// 错误
        /// Error
        /// </summary>
        Error,

        /// <summary>
        /// 关键错误
        /// Critical error
        /// </summary>
        Critical
    }

    /// <summary>
    /// 验证方法信息
    /// Validation method information
    /// </summary>
    public class ValidationMethodInfo
    {
        /// <summary>
        /// 验证器名称
        /// Validator name
        /// </summary>
        public string ValidatorName { get; set; }

        /// <summary>
        /// 验证器版本
        /// Validator version
        /// </summary>
        public string ValidatorVersion { get; set; }

        /// <summary>
        /// 使用的算法
        /// Algorithm used
        /// </summary>
        public string? Algorithm { get; set; }

        /// <summary>
        /// 方法参数
        /// Method parameters
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; }

        /// <summary>
        /// 初始化验证方法信息
        /// Initializes validation method information
        /// </summary>
        /// <param name="validatorName">验证器名称 Validator name</param>
        /// <param name="validatorVersion">验证器版本 Validator version</param>
        public ValidationMethodInfo(string validatorName, string validatorVersion)
        {
            ValidatorName = validatorName ?? throw new ArgumentNullException(nameof(validatorName));
            ValidatorVersion = validatorVersion ?? throw new ArgumentNullException(nameof(validatorVersion));
            Parameters = new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// 验证结果详细信息
    /// Validation result details
    /// </summary>
    public class ValidationResultDetails
    {
        /// <summary>
        /// 内存使用情况
        /// Memory usage information
        /// </summary>
        public MemoryUsageInfo? MemoryUsage { get; set; }

        /// <summary>
        /// 清理统计信息
        /// Cleanup statistics
        /// </summary>
        public CleanupStatistics? CleanupStats { get; set; }

        /// <summary>
        /// 发现的敏感数据项
        /// Sensitive data items found
        /// </summary>
        public List<SensitiveDataItem> SensitiveDataItems { get; set; }

        /// <summary>
        /// 性能指标
        /// Performance metrics
        /// </summary>
        public Dictionary<string, double> PerformanceMetrics { get; set; }

        /// <summary>
        /// 初始化验证结果详细信息
        /// Initializes validation result details
        /// </summary>
        public ValidationResultDetails()
        {
            SensitiveDataItems = new List<SensitiveDataItem>();
            PerformanceMetrics = new Dictionary<string, double>();
        }
    }

    /// <summary>
    /// 内存使用信息
    /// Memory usage information
    /// </summary>
    public class MemoryUsageInfo
    {
        /// <summary>
        /// 验证前内存使用量（字节）
        /// Memory usage before validation (bytes)
        /// </summary>
        public long BeforeBytes { get; set; }

        /// <summary>
        /// 验证后内存使用量（字节）
        /// Memory usage after validation (bytes)
        /// </summary>
        public long AfterBytes { get; set; }

        /// <summary>
        /// 内存差异（字节）
        /// Memory difference (bytes)
        /// </summary>
        public long DifferenceBytes => AfterBytes - BeforeBytes;

        /// <summary>
        /// GC回收次数
        /// GC collection count
        /// </summary>
        public int GcCollections { get; set; }
    }

    /// <summary>
    /// 清理统计信息
    /// Cleanup statistics
    /// </summary>
    public class CleanupStatistics
    {
        /// <summary>
        /// 清理的项目总数
        /// Total items cleaned
        /// </summary>
        public int TotalItemsCleaned { get; set; }

        /// <summary>
        /// 成功清理的项目数
        /// Successfully cleaned items
        /// </summary>
        public int SuccessfullyCleanedItems { get; set; }

        /// <summary>
        /// 清理失败的项目数
        /// Failed to clean items
        /// </summary>
        public int FailedToCleanItems { get; set; }

        /// <summary>
        /// 释放的内存量（字节）
        /// Memory freed (bytes)
        /// </summary>
        public long MemoryFreed { get; set; }

        /// <summary>
        /// 清理成功率
        /// Cleanup success rate
        /// </summary>
        public double SuccessRate => TotalItemsCleaned > 0 ? (double)SuccessfullyCleanedItems / TotalItemsCleaned : 0.0;
    }

    /// <summary>
    /// 敏感数据项
    /// Sensitive data item
    /// </summary>
    public class SensitiveDataItem
    {
        /// <summary>
        /// 数据类型
        /// Data type
        /// </summary>
        public string DataType { get; set; }

        /// <summary>
        /// 数据位置
        /// Data location
        /// </summary>
        public string Location { get; set; }

        /// <summary>
        /// 风险级别
        /// Risk level
        /// </summary>
        public RiskLevel RiskLevel { get; set; }

        /// <summary>
        /// 是否已清理
        /// Whether cleaned
        /// </summary>
        public bool IsCleaned { get; set; }

        /// <summary>
        /// 清理时间戳
        /// Cleanup timestamp
        /// </summary>
        public DateTime? CleanupTimestamp { get; set; }

        /// <summary>
        /// 初始化敏感数据项
        /// Initializes sensitive data item
        /// </summary>
        /// <param name="dataType">数据类型 Data type</param>
        /// <param name="location">数据位置 Data location</param>
        /// <param name="riskLevel">风险级别 Risk level</param>
        public SensitiveDataItem(string dataType, string location, RiskLevel riskLevel)
        {
            DataType = dataType ?? throw new ArgumentNullException(nameof(dataType));
            Location = location ?? throw new ArgumentNullException(nameof(location));
            RiskLevel = riskLevel;
        }
    }

    /// <summary>
    /// 风险级别
    /// Risk levels
    /// </summary>
    public enum RiskLevel
    {
        /// <summary>
        /// 低风险
        /// Low risk
        /// </summary>
        Low,

        /// <summary>
        /// 中等风险
        /// Medium risk
        /// </summary>
        Medium,

        /// <summary>
        /// 高风险
        /// High risk
        /// </summary>
        High,

        /// <summary>
        /// 关键风险
        /// Critical risk
        /// </summary>
        Critical
    }
}