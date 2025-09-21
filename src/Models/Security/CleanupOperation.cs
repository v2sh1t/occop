using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Occop.Models.Security
{
    /// <summary>
    /// 清理操作类型枚举
    /// </summary>
    public enum CleanupOperationType
    {
        /// <summary>
        /// 内存清理 - 清理SecureString等敏感内存数据
        /// </summary>
        MemoryCleanup,

        /// <summary>
        /// 环境变量清理 - 清理临时设置的API密钥环境变量
        /// </summary>
        EnvironmentVariableCleanup,

        /// <summary>
        /// 配置文件清理 - 删除临时创建的配置文件
        /// </summary>
        ConfigurationFileCleanup,

        /// <summary>
        /// 临时文件清理 - 清理运行过程中产生的临时文件
        /// </summary>
        TemporaryFileCleanup,

        /// <summary>
        /// 进程清理 - 终止相关的AI工具进程
        /// </summary>
        ProcessCleanup,

        /// <summary>
        /// 完整清理 - 执行所有类型的清理操作
        /// </summary>
        CompleteCleanup
    }

    /// <summary>
    /// 清理操作优先级
    /// </summary>
    public enum CleanupPriority
    {
        /// <summary>
        /// 低优先级 - 可延迟执行
        /// </summary>
        Low = 0,

        /// <summary>
        /// 正常优先级 - 常规清理操作
        /// </summary>
        Normal = 1,

        /// <summary>
        /// 高优先级 - 重要的安全清理
        /// </summary>
        High = 2,

        /// <summary>
        /// 紧急优先级 - 立即执行的关键清理
        /// </summary>
        Critical = 3
    }

    /// <summary>
    /// 清理触发原因
    /// </summary>
    public enum CleanupTriggerReason
    {
        /// <summary>
        /// 用户手动触发
        /// </summary>
        Manual,

        /// <summary>
        /// 进程正常退出触发
        /// </summary>
        ProcessExit,

        /// <summary>
        /// 进程异常终止触发
        /// </summary>
        ProcessCrash,

        /// <summary>
        /// 应用程序关闭触发
        /// </summary>
        ApplicationShutdown,

        /// <summary>
        /// 系统关机触发
        /// </summary>
        SystemShutdown,

        /// <summary>
        /// 超时触发
        /// </summary>
        Timeout,

        /// <summary>
        /// 异常处理触发
        /// </summary>
        Exception,

        /// <summary>
        /// 定时触发
        /// </summary>
        Scheduled,

        /// <summary>
        /// 强制清理（如调试或测试）
        /// </summary>
        Forced
    }

    /// <summary>
    /// 清理操作状态
    /// </summary>
    public enum CleanupOperationStatus
    {
        /// <summary>
        /// 等待执行
        /// </summary>
        Pending,

        /// <summary>
        /// 正在执行
        /// </summary>
        Running,

        /// <summary>
        /// 执行成功
        /// </summary>
        Completed,

        /// <summary>
        /// 执行失败
        /// </summary>
        Failed,

        /// <summary>
        /// 被取消
        /// </summary>
        Canceled,

        /// <summary>
        /// 超时
        /// </summary>
        Timeout,

        /// <summary>
        /// 部分成功（某些子操作失败）
        /// </summary>
        PartialSuccess
    }

    /// <summary>
    /// 清理操作模型
    /// 定义单个清理操作的属性、行为和执行逻辑
    /// 支持幂等性、优先级和异步执行
    /// </summary>
    public class CleanupOperation
    {
        #region 基本属性

        /// <summary>
        /// 操作唯一标识符
        /// </summary>
        public Guid OperationId { get; }

        /// <summary>
        /// 操作名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 操作描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 清理操作类型
        /// </summary>
        public CleanupOperationType Type { get; set; }

        /// <summary>
        /// 操作优先级
        /// </summary>
        public CleanupPriority Priority { get; set; }

        /// <summary>
        /// 触发原因
        /// </summary>
        public CleanupTriggerReason TriggerReason { get; set; }

        /// <summary>
        /// 当前状态
        /// </summary>
        public CleanupOperationStatus Status { get; private set; }

        #endregion

        #region 时间信息

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; }

        /// <summary>
        /// 开始执行时间
        /// </summary>
        public DateTime? StartedAt { get; private set; }

        /// <summary>
        /// 完成时间
        /// </summary>
        public DateTime? CompletedAt { get; private set; }

        /// <summary>
        /// 执行超时时间
        /// </summary>
        public TimeSpan Timeout { get; set; }

        /// <summary>
        /// 实际执行时长
        /// </summary>
        public TimeSpan? ExecutionDuration => CompletedAt.HasValue && StartedAt.HasValue
            ? CompletedAt.Value - StartedAt.Value
            : null;

        #endregion

        #region 执行相关

        /// <summary>
        /// 执行函数
        /// </summary>
        public Func<CleanupOperation, CancellationToken, Task<CleanupResult>> ExecuteAction { get; set; }

        /// <summary>
        /// 回滚函数（如果支持）
        /// </summary>
        public Func<CleanupOperation, CancellationToken, Task<CleanupResult>> RollbackAction { get; set; }

        /// <summary>
        /// 验证函数（检查清理是否成功）
        /// </summary>
        public Func<CleanupOperation, Task<bool>> VerifyAction { get; set; }

        /// <summary>
        /// 是否支持幂等性（可重复执行）
        /// </summary>
        public bool IsIdempotent { get; set; }

        /// <summary>
        /// 最大重试次数
        /// </summary>
        public int MaxRetryCount { get; set; }

        /// <summary>
        /// 当前重试次数
        /// </summary>
        public int CurrentRetryCount { get; private set; }

        /// <summary>
        /// 重试间隔
        /// </summary>
        public TimeSpan RetryDelay { get; set; }

        #endregion

        #region 上下文数据

        /// <summary>
        /// 操作目标（如文件路径、进程ID等）
        /// </summary>
        public List<string> Targets { get; set; }

        /// <summary>
        /// 操作参数
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; }

        /// <summary>
        /// 操作标签
        /// </summary>
        public HashSet<string> Tags { get; set; }

        /// <summary>
        /// 相关的进程ID（如果适用）
        /// </summary>
        public int? RelatedProcessId { get; set; }

        /// <summary>
        /// 相关的会话ID
        /// </summary>
        public string RelatedSessionId { get; set; }

        #endregion

        #region 结果信息

        /// <summary>
        /// 最后执行结果
        /// </summary>
        public CleanupResult LastResult { get; private set; }

        /// <summary>
        /// 执行历史记录
        /// </summary>
        public List<CleanupResult> ExecutionHistory { get; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMessage { get; private set; }

        /// <summary>
        /// 异常详情
        /// </summary>
        public Exception Exception { get; private set; }

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化清理操作
        /// </summary>
        /// <param name="name">操作名称</param>
        /// <param name="type">操作类型</param>
        /// <param name="triggerReason">触发原因</param>
        /// <param name="priority">优先级</param>
        public CleanupOperation(string name, CleanupOperationType type, CleanupTriggerReason triggerReason, CleanupPriority priority = CleanupPriority.Normal)
        {
            OperationId = Guid.NewGuid();
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Type = type;
            TriggerReason = triggerReason;
            Priority = priority;
            Status = CleanupOperationStatus.Pending;
            CreatedAt = DateTime.UtcNow;

            // 默认设置
            Timeout = TimeSpan.FromSeconds(30); // 默认30秒超时
            IsIdempotent = true; // 默认支持幂等性
            MaxRetryCount = 3; // 默认最多重试3次
            RetryDelay = TimeSpan.FromMilliseconds(500); // 默认重试间隔500ms

            // 初始化集合
            Targets = new List<string>();
            Parameters = new Dictionary<string, object>();
            Tags = new HashSet<string>();
            ExecutionHistory = new List<CleanupResult>();
        }

        #endregion

        #region 执行方法

        /// <summary>
        /// 异步执行清理操作
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>执行结果</returns>
        public async Task<CleanupResult> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            if (ExecuteAction == null)
            {
                var errorResult = CleanupResult.CreateFailure(this, "No execute action defined", new InvalidOperationException("ExecuteAction is null"));
                SetResult(errorResult);
                return errorResult;
            }

            // 检查是否已经在执行中
            if (Status == CleanupOperationStatus.Running)
            {
                var busyResult = CleanupResult.CreateFailure(this, "Operation is already running", new InvalidOperationException("Cannot execute operation that is already running"));
                return busyResult;
            }

            // 如果操作已完成且不支持幂等性，直接返回上次结果
            if (Status == CleanupOperationStatus.Completed && !IsIdempotent && LastResult != null)
            {
                return LastResult;
            }

            var attempt = 0;
            CleanupResult result = null;

            while (attempt <= MaxRetryCount)
            {
                try
                {
                    SetStatus(CleanupOperationStatus.Running);
                    StartedAt = DateTime.UtcNow;
                    CurrentRetryCount = attempt;

                    // 创建超时取消令牌
                    using var timeoutCts = new CancellationTokenSource(Timeout);
                    using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                    // 执行操作
                    result = await ExecuteAction(this, combinedCts.Token);

                    if (result.IsSuccess)
                    {
                        SetStatus(CleanupOperationStatus.Completed);
                        CompletedAt = DateTime.UtcNow;
                        SetResult(result);

                        // 如果有验证函数，执行验证
                        if (VerifyAction != null)
                        {
                            var isVerified = await VerifyAction(this);
                            if (!isVerified)
                            {
                                result = CleanupResult.CreateFailure(this, "Cleanup verification failed", new InvalidOperationException("Cleanup verification failed"));
                                SetResult(result);
                                SetStatus(CleanupOperationStatus.Failed);
                            }
                        }

                        return result;
                    }
                    else if (attempt < MaxRetryCount)
                    {
                        // 重试前等待
                        await Task.Delay(RetryDelay, cancellationToken);
                        attempt++;
                    }
                    else
                    {
                        // 已达到最大重试次数
                        SetStatus(CleanupOperationStatus.Failed);
                        CompletedAt = DateTime.UtcNow;
                        SetResult(result);
                        return result;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    result = CleanupResult.CreateCanceled(this, "Operation was canceled by user");
                    SetStatus(CleanupOperationStatus.Canceled);
                    CompletedAt = DateTime.UtcNow;
                    SetResult(result);
                    return result;
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
                {
                    result = CleanupResult.CreateTimeout(this, $"Operation timed out after {Timeout}");
                    SetStatus(CleanupOperationStatus.Timeout);
                    CompletedAt = DateTime.UtcNow;
                    SetResult(result);
                    return result;
                }
                catch (Exception ex)
                {
                    result = CleanupResult.CreateFailure(this, $"Operation failed: {ex.Message}", ex);

                    if (attempt < MaxRetryCount)
                    {
                        // 记录重试信息
                        ExecutionHistory.Add(result);
                        await Task.Delay(RetryDelay, cancellationToken);
                        attempt++;
                    }
                    else
                    {
                        SetStatus(CleanupOperationStatus.Failed);
                        CompletedAt = DateTime.UtcNow;
                        SetResult(result);
                        return result;
                    }
                }
            }

            return result ?? CleanupResult.CreateFailure(this, "Unexpected execution end", new InvalidOperationException("Execution ended without result"));
        }

        /// <summary>
        /// 异步执行回滚操作
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>回滚结果</returns>
        public async Task<CleanupResult> RollbackAsync(CancellationToken cancellationToken = default)
        {
            if (RollbackAction == null)
            {
                return CleanupResult.CreateFailure(this, "No rollback action defined", new InvalidOperationException("RollbackAction is null"));
            }

            try
            {
                using var timeoutCts = new CancellationTokenSource(Timeout);
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                var result = await RollbackAction(this, combinedCts.Token);
                ExecutionHistory.Add(result);
                return result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return CleanupResult.CreateCanceled(this, "Rollback was canceled by user");
            }
            catch (OperationCanceledException)
            {
                return CleanupResult.CreateTimeout(this, $"Rollback timed out after {Timeout}");
            }
            catch (Exception ex)
            {
                return CleanupResult.CreateFailure(this, $"Rollback failed: {ex.Message}", ex);
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 添加目标
        /// </summary>
        /// <param name="target">目标</param>
        public void AddTarget(string target)
        {
            if (!string.IsNullOrWhiteSpace(target) && !Targets.Contains(target))
            {
                Targets.Add(target);
            }
        }

        /// <summary>
        /// 添加参数
        /// </summary>
        /// <param name="key">参数键</param>
        /// <param name="value">参数值</param>
        public void AddParameter(string key, object value)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                Parameters[key] = value;
            }
        }

        /// <summary>
        /// 获取参数
        /// </summary>
        /// <typeparam name="T">参数类型</typeparam>
        /// <param name="key">参数键</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>参数值</returns>
        public T GetParameter<T>(string key, T defaultValue = default)
        {
            if (string.IsNullOrWhiteSpace(key) || !Parameters.ContainsKey(key))
                return defaultValue;

            try
            {
                return (T)Parameters[key];
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// 添加标签
        /// </summary>
        /// <param name="tag">标签</param>
        public void AddTag(string tag)
        {
            if (!string.IsNullOrWhiteSpace(tag))
            {
                Tags.Add(tag.ToUpperInvariant());
            }
        }

        /// <summary>
        /// 检查是否有指定标签
        /// </summary>
        /// <param name="tag">标签</param>
        /// <returns>是否存在</returns>
        public bool HasTag(string tag)
        {
            return !string.IsNullOrWhiteSpace(tag) && Tags.Contains(tag.ToUpperInvariant());
        }

        /// <summary>
        /// 重置操作状态（用于重新执行）
        /// </summary>
        public void Reset()
        {
            Status = CleanupOperationStatus.Pending;
            StartedAt = null;
            CompletedAt = null;
            CurrentRetryCount = 0;
            ErrorMessage = null;
            Exception = null;
            // 保留历史记录，但清空最后结果
            LastResult = null;
        }

        /// <summary>
        /// 获取操作摘要
        /// </summary>
        /// <returns>摘要字符串</returns>
        public string GetSummary()
        {
            var duration = ExecutionDuration?.TotalMilliseconds.ToString("F0") ?? "N/A";
            var targets = Targets.Count > 0 ? $" (Targets: {Targets.Count})" : "";
            return $"[{Priority}] {Name} - {Status} - {duration}ms{targets}";
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 设置操作状态
        /// </summary>
        /// <param name="status">新状态</param>
        private void SetStatus(CleanupOperationStatus status)
        {
            Status = status;
        }

        /// <summary>
        /// 设置执行结果
        /// </summary>
        /// <param name="result">执行结果</param>
        private void SetResult(CleanupResult result)
        {
            LastResult = result;
            ExecutionHistory.Add(result);

            if (!result.IsSuccess)
            {
                ErrorMessage = result.Message;
                Exception = result.Exception;
            }
        }

        #endregion

        #region 重写方法

        /// <summary>
        /// 获取对象字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString()
        {
            return GetSummary();
        }

        /// <summary>
        /// 计算哈希码
        /// </summary>
        /// <returns>哈希码</returns>
        public override int GetHashCode()
        {
            return OperationId.GetHashCode();
        }

        /// <summary>
        /// 比较对象相等性
        /// </summary>
        /// <param name="obj">比较对象</param>
        /// <returns>是否相等</returns>
        public override bool Equals(object obj)
        {
            return obj is CleanupOperation other && OperationId == other.OperationId;
        }

        #endregion
    }

    #region 静态工厂方法

    /// <summary>
    /// 清理操作工厂类
    /// </summary>
    public static class CleanupOperationFactory
    {
        /// <summary>
        /// 创建内存清理操作
        /// </summary>
        /// <param name="triggerReason">触发原因</param>
        /// <param name="targets">清理目标</param>
        /// <returns>清理操作</returns>
        public static CleanupOperation CreateMemoryCleanup(CleanupTriggerReason triggerReason, params string[] targets)
        {
            var operation = new CleanupOperation("内存清理", CleanupOperationType.MemoryCleanup, triggerReason, CleanupPriority.High)
            {
                Description = "清理内存中的敏感信息，包括SecureString和其他敏感数据",
                Timeout = TimeSpan.FromSeconds(10)
            };

            operation.AddTag("MEMORY");
            operation.AddTag("SENSITIVE");

            foreach (var target in targets ?? Array.Empty<string>())
            {
                operation.AddTarget(target);
            }

            return operation;
        }

        /// <summary>
        /// 创建环境变量清理操作
        /// </summary>
        /// <param name="triggerReason">触发原因</param>
        /// <param name="variableNames">环境变量名称</param>
        /// <returns>清理操作</returns>
        public static CleanupOperation CreateEnvironmentVariableCleanup(CleanupTriggerReason triggerReason, params string[] variableNames)
        {
            var operation = new CleanupOperation("环境变量清理", CleanupOperationType.EnvironmentVariableCleanup, triggerReason, CleanupPriority.Critical)
            {
                Description = "清理临时设置的API密钥环境变量",
                Timeout = TimeSpan.FromSeconds(5)
            };

            operation.AddTag("ENVIRONMENT");
            operation.AddTag("API_KEY");

            foreach (var varName in variableNames ?? Array.Empty<string>())
            {
                operation.AddTarget(varName);
            }

            return operation;
        }

        /// <summary>
        /// 创建配置文件清理操作
        /// </summary>
        /// <param name="triggerReason">触发原因</param>
        /// <param name="filePaths">文件路径</param>
        /// <returns>清理操作</returns>
        public static CleanupOperation CreateConfigFileCleanup(CleanupTriggerReason triggerReason, params string[] filePaths)
        {
            var operation = new CleanupOperation("配置文件清理", CleanupOperationType.ConfigurationFileCleanup, triggerReason, CleanupPriority.High)
            {
                Description = "删除临时创建的配置文件",
                Timeout = TimeSpan.FromSeconds(15)
            };

            operation.AddTag("CONFIG_FILE");
            operation.AddTag("TEMPORARY");

            foreach (var filePath in filePaths ?? Array.Empty<string>())
            {
                operation.AddTarget(filePath);
            }

            return operation;
        }

        /// <summary>
        /// 创建进程清理操作
        /// </summary>
        /// <param name="triggerReason">触发原因</param>
        /// <param name="processIds">进程ID</param>
        /// <returns>清理操作</returns>
        public static CleanupOperation CreateProcessCleanup(CleanupTriggerReason triggerReason, params int[] processIds)
        {
            var operation = new CleanupOperation("进程清理", CleanupOperationType.ProcessCleanup, triggerReason, CleanupPriority.Normal)
            {
                Description = "终止相关的AI工具进程",
                Timeout = TimeSpan.FromSeconds(20)
            };

            operation.AddTag("PROCESS");
            operation.AddTag("AI_TOOL");

            foreach (var pid in processIds ?? Array.Empty<int>())
            {
                operation.AddTarget(pid.ToString());
            }

            return operation;
        }

        /// <summary>
        /// 创建完整清理操作
        /// </summary>
        /// <param name="triggerReason">触发原因</param>
        /// <returns>清理操作</returns>
        public static CleanupOperation CreateCompleteCleanup(CleanupTriggerReason triggerReason)
        {
            var operation = new CleanupOperation("完整清理", CleanupOperationType.CompleteCleanup, triggerReason, CleanupPriority.Critical)
            {
                Description = "执行所有类型的清理操作，确保系统完全清理",
                Timeout = TimeSpan.FromMinutes(2),
                MaxRetryCount = 1 // 完整清理减少重试次数
            };

            operation.AddTag("COMPLETE");
            operation.AddTag("ALL_TYPES");

            return operation;
        }
    }

    #endregion
}