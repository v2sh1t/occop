using System;
using System.Collections.Generic;
using System.Linq;

namespace Occop.Models.Security
{
    /// <summary>
    /// 清理结果状态
    /// </summary>
    public enum CleanupResultStatus
    {
        /// <summary>
        /// 成功完成
        /// </summary>
        Success,

        /// <summary>
        /// 执行失败
        /// </summary>
        Failed,

        /// <summary>
        /// 部分成功（某些子操作失败）
        /// </summary>
        PartialSuccess,

        /// <summary>
        /// 被取消
        /// </summary>
        Canceled,

        /// <summary>
        /// 超时
        /// </summary>
        Timeout,

        /// <summary>
        /// 跳过执行（如操作不适用）
        /// </summary>
        Skipped
    }

    /// <summary>
    /// 清理结果项
    /// 用于记录单个清理目标的执行结果
    /// </summary>
    public class CleanupResultItem
    {
        /// <summary>
        /// 清理目标（如文件路径、进程ID、环境变量名等）
        /// </summary>
        public string Target { get; set; }

        /// <summary>
        /// 清理结果状态
        /// </summary>
        public CleanupResultStatus Status { get; set; }

        /// <summary>
        /// 结果消息
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 错误信息（如果有）
        /// </summary>
        public string ErrorDetails { get; set; }

        /// <summary>
        /// 执行时长
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// 额外数据
        /// </summary>
        public Dictionary<string, object> Data { get; set; }

        /// <summary>
        /// 初始化清理结果项
        /// </summary>
        /// <param name="target">清理目标</param>
        /// <param name="status">状态</param>
        /// <param name="message">消息</param>
        /// <param name="duration">执行时长</param>
        public CleanupResultItem(string target, CleanupResultStatus status, string message = null, TimeSpan duration = default)
        {
            Target = target;
            Status = status;
            Message = message;
            Duration = duration;
            Data = new Dictionary<string, object>();
        }

        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess => Status == CleanupResultStatus.Success;

        /// <summary>
        /// 添加数据
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        public void AddData(string key, object value)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                Data[key] = value;
            }
        }

        /// <summary>
        /// 获取对象字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString()
        {
            return $"{Target}: {Status} ({Duration.TotalMilliseconds:F0}ms) - {Message}";
        }
    }

    /// <summary>
    /// 清理结果模型
    /// 记录清理操作的完整执行结果，包括成功、失败的详细信息
    /// 支持多目标清理和统计分析
    /// </summary>
    public class CleanupResult
    {
        #region 基本属性

        /// <summary>
        /// 结果唯一标识符
        /// </summary>
        public Guid ResultId { get; }

        /// <summary>
        /// 相关的清理操作ID
        /// </summary>
        public Guid OperationId { get; }

        /// <summary>
        /// 操作名称
        /// </summary>
        public string OperationName { get; }

        /// <summary>
        /// 操作类型
        /// </summary>
        public CleanupOperationType OperationType { get; }

        /// <summary>
        /// 整体执行状态
        /// </summary>
        public CleanupResultStatus Status { get; private set; }

        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess => Status == CleanupResultStatus.Success;

        /// <summary>
        /// 是否部分成功
        /// </summary>
        public bool IsPartialSuccess => Status == CleanupResultStatus.PartialSuccess;

        /// <summary>
        /// 是否失败
        /// </summary>
        public bool IsFailure => Status == CleanupResultStatus.Failed;

        /// <summary>
        /// 是否被取消
        /// </summary>
        public bool IsCanceled => Status == CleanupResultStatus.Canceled;

        /// <summary>
        /// 是否超时
        /// </summary>
        public bool IsTimeout => Status == CleanupResultStatus.Timeout;

        #endregion

        #region 时间信息

        /// <summary>
        /// 开始执行时间
        /// </summary>
        public DateTime StartTime { get; }

        /// <summary>
        /// 结束时间
        /// </summary>
        public DateTime EndTime { get; private set; }

        /// <summary>
        /// 执行时长
        /// </summary>
        public TimeSpan Duration => EndTime - StartTime;

        #endregion

        #region 结果信息

        /// <summary>
        /// 结果消息
        /// </summary>
        public string Message { get; private set; }

        /// <summary>
        /// 详细描述
        /// </summary>
        public string Details { get; private set; }

        /// <summary>
        /// 异常信息（如果有）
        /// </summary>
        public Exception Exception { get; private set; }

        /// <summary>
        /// 错误代码（如果有）
        /// </summary>
        public string ErrorCode { get; private set; }

        #endregion

        #region 清理统计

        /// <summary>
        /// 清理结果项列表
        /// </summary>
        public List<CleanupResultItem> Items { get; }

        /// <summary>
        /// 总目标数量
        /// </summary>
        public int TotalTargets => Items.Count;

        /// <summary>
        /// 成功清理的目标数量
        /// </summary>
        public int SuccessfulTargets => Items.Count(item => item.IsSuccess);

        /// <summary>
        /// 失败的目标数量
        /// </summary>
        public int FailedTargets => Items.Count(item => !item.IsSuccess);

        /// <summary>
        /// 成功率（百分比）
        /// </summary>
        public double SuccessRate => TotalTargets > 0 ? (double)SuccessfulTargets / TotalTargets * 100 : 0;

        #endregion

        #region 性能信息

        /// <summary>
        /// 清理的数据量（字节）
        /// </summary>
        public long CleanedDataSize { get; private set; }

        /// <summary>
        /// 删除的文件数量
        /// </summary>
        public int DeletedFileCount { get; private set; }

        /// <summary>
        /// 清理的环境变量数量
        /// </summary>
        public int ClearedEnvironmentVariables { get; private set; }

        /// <summary>
        /// 终止的进程数量
        /// </summary>
        public int TerminatedProcesses { get; private set; }

        /// <summary>
        /// 释放的内存大小（字节）
        /// </summary>
        public long ReleasedMemorySize { get; private set; }

        #endregion

        #region 额外数据

        /// <summary>
        /// 额外的结果数据
        /// </summary>
        public Dictionary<string, object> Data { get; }

        /// <summary>
        /// 结果标签
        /// </summary>
        public HashSet<string> Tags { get; }

        /// <summary>
        /// 警告信息列表
        /// </summary>
        public List<string> Warnings { get; }

        #endregion

        #region 构造函数

        /// <summary>
        /// 私有构造函数，通过静态工厂方法创建实例
        /// </summary>
        /// <param name="operation">相关的清理操作</param>
        /// <param name="status">执行状态</param>
        /// <param name="message">结果消息</param>
        private CleanupResult(CleanupOperation operation, CleanupResultStatus status, string message)
        {
            ResultId = Guid.NewGuid();
            OperationId = operation.OperationId;
            OperationName = operation.Name;
            OperationType = operation.Type;
            Status = status;
            Message = message;
            StartTime = DateTime.UtcNow;
            EndTime = DateTime.UtcNow;

            Items = new List<CleanupResultItem>();
            Data = new Dictionary<string, object>();
            Tags = new HashSet<string>();
            Warnings = new List<string>();
        }

        #endregion

        #region 静态工厂方法

        /// <summary>
        /// 创建成功结果
        /// </summary>
        /// <param name="operation">清理操作</param>
        /// <param name="message">成功消息</param>
        /// <returns>清理结果</returns>
        public static CleanupResult CreateSuccess(CleanupOperation operation, string message = "清理操作执行成功")
        {
            var result = new CleanupResult(operation, CleanupResultStatus.Success, message);
            result.EndTime = DateTime.UtcNow;
            return result;
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        /// <param name="operation">清理操作</param>
        /// <param name="message">失败消息</param>
        /// <param name="exception">异常信息</param>
        /// <param name="errorCode">错误代码</param>
        /// <returns>清理结果</returns>
        public static CleanupResult CreateFailure(CleanupOperation operation, string message, Exception exception = null, string errorCode = null)
        {
            var result = new CleanupResult(operation, CleanupResultStatus.Failed, message)
            {
                Exception = exception,
                ErrorCode = errorCode,
                EndTime = DateTime.UtcNow
            };

            if (exception != null)
            {
                result.Details = exception.ToString();
            }

            return result;
        }

        /// <summary>
        /// 创建部分成功结果
        /// </summary>
        /// <param name="operation">清理操作</param>
        /// <param name="message">结果消息</param>
        /// <returns>清理结果</returns>
        public static CleanupResult CreatePartialSuccess(CleanupOperation operation, string message = "清理操作部分成功")
        {
            var result = new CleanupResult(operation, CleanupResultStatus.PartialSuccess, message);
            result.EndTime = DateTime.UtcNow;
            return result;
        }

        /// <summary>
        /// 创建取消结果
        /// </summary>
        /// <param name="operation">清理操作</param>
        /// <param name="message">取消消息</param>
        /// <returns>清理结果</returns>
        public static CleanupResult CreateCanceled(CleanupOperation operation, string message = "清理操作被取消")
        {
            var result = new CleanupResult(operation, CleanupResultStatus.Canceled, message);
            result.EndTime = DateTime.UtcNow;
            return result;
        }

        /// <summary>
        /// 创建超时结果
        /// </summary>
        /// <param name="operation">清理操作</param>
        /// <param name="message">超时消息</param>
        /// <returns>清理结果</returns>
        public static CleanupResult CreateTimeout(CleanupOperation operation, string message = "清理操作执行超时")
        {
            var result = new CleanupResult(operation, CleanupResultStatus.Timeout, message);
            result.EndTime = DateTime.UtcNow;
            return result;
        }

        /// <summary>
        /// 创建跳过结果
        /// </summary>
        /// <param name="operation">清理操作</param>
        /// <param name="message">跳过消息</param>
        /// <returns>清理结果</returns>
        public static CleanupResult CreateSkipped(CleanupOperation operation, string message = "清理操作被跳过")
        {
            var result = new CleanupResult(operation, CleanupResultStatus.Skipped, message);
            result.EndTime = DateTime.UtcNow;
            return result;
        }

        #endregion

        #region 结果项管理

        /// <summary>
        /// 添加成功的清理结果项
        /// </summary>
        /// <param name="target">清理目标</param>
        /// <param name="message">成功消息</param>
        /// <param name="duration">执行时长</param>
        public void AddSuccessItem(string target, string message = "清理成功", TimeSpan duration = default)
        {
            Items.Add(new CleanupResultItem(target, CleanupResultStatus.Success, message, duration));
            UpdateOverallStatus();
        }

        /// <summary>
        /// 添加失败的清理结果项
        /// </summary>
        /// <param name="target">清理目标</param>
        /// <param name="message">失败消息</param>
        /// <param name="errorDetails">错误详情</param>
        /// <param name="duration">执行时长</param>
        public void AddFailureItem(string target, string message, string errorDetails = null, TimeSpan duration = default)
        {
            var item = new CleanupResultItem(target, CleanupResultStatus.Failed, message, duration)
            {
                ErrorDetails = errorDetails
            };
            Items.Add(item);
            UpdateOverallStatus();
        }

        /// <summary>
        /// 添加跳过的清理结果项
        /// </summary>
        /// <param name="target">清理目标</param>
        /// <param name="reason">跳过原因</param>
        public void AddSkippedItem(string target, string reason = "目标不存在或不适用")
        {
            Items.Add(new CleanupResultItem(target, CleanupResultStatus.Skipped, reason));
            UpdateOverallStatus();
        }

        /// <summary>
        /// 批量添加结果项
        /// </summary>
        /// <param name="items">结果项列表</param>
        public void AddItems(IEnumerable<CleanupResultItem> items)
        {
            if (items != null)
            {
                Items.AddRange(items);
                UpdateOverallStatus();
            }
        }

        #endregion

        #region 统计和性能更新

        /// <summary>
        /// 更新清理统计信息
        /// </summary>
        /// <param name="cleanedDataSize">清理的数据量</param>
        /// <param name="deletedFileCount">删除的文件数量</param>
        /// <param name="clearedEnvVars">清理的环境变量数量</param>
        /// <param name="terminatedProcesses">终止的进程数量</param>
        /// <param name="releasedMemory">释放的内存大小</param>
        public void UpdateStatistics(long cleanedDataSize = 0, int deletedFileCount = 0,
            int clearedEnvVars = 0, int terminatedProcesses = 0, long releasedMemory = 0)
        {
            CleanedDataSize += cleanedDataSize;
            DeletedFileCount += deletedFileCount;
            ClearedEnvironmentVariables += clearedEnvVars;
            TerminatedProcesses += terminatedProcesses;
            ReleasedMemorySize += releasedMemory;
        }

        /// <summary>
        /// 添加警告信息
        /// </summary>
        /// <param name="warning">警告消息</param>
        public void AddWarning(string warning)
        {
            if (!string.IsNullOrWhiteSpace(warning) && !Warnings.Contains(warning))
            {
                Warnings.Add(warning);
            }
        }

        /// <summary>
        /// 添加数据项
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        public void AddData(string key, object value)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                Data[key] = value;
            }
        }

        /// <summary>
        /// 获取数据项
        /// </summary>
        /// <typeparam name="T">返回类型</typeparam>
        /// <param name="key">键</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>数据值</returns>
        public T GetData<T>(string key, T defaultValue = default)
        {
            if (string.IsNullOrWhiteSpace(key) || !Data.ContainsKey(key))
                return defaultValue;

            try
            {
                return (T)Data[key];
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

        #endregion

        #region 结果分析

        /// <summary>
        /// 获取失败的目标列表
        /// </summary>
        /// <returns>失败目标列表</returns>
        public IEnumerable<string> GetFailedTargets()
        {
            return Items.Where(item => !item.IsSuccess).Select(item => item.Target);
        }

        /// <summary>
        /// 获取成功的目标列表
        /// </summary>
        /// <returns>成功目标列表</returns>
        public IEnumerable<string> GetSuccessfulTargets()
        {
            return Items.Where(item => item.IsSuccess).Select(item => item.Target);
        }

        /// <summary>
        /// 获取指定状态的结果项
        /// </summary>
        /// <param name="status">状态</param>
        /// <returns>结果项列表</returns>
        public IEnumerable<CleanupResultItem> GetItemsByStatus(CleanupResultStatus status)
        {
            return Items.Where(item => item.Status == status);
        }

        /// <summary>
        /// 获取执行摘要
        /// </summary>
        /// <returns>摘要信息</returns>
        public string GetSummary()
        {
            var success = SuccessfulTargets;
            var failed = FailedTargets;
            var total = TotalTargets;
            var duration = Duration.TotalMilliseconds;

            var summary = $"{OperationName} - {Status}";
            if (total > 0)
            {
                summary += $" ({success}/{total} 成功, {SuccessRate:F1}%)";
            }
            summary += $" - {duration:F0}ms";

            if (failed > 0)
            {
                summary += $" - {failed} 个目标失败";
            }

            if (Warnings.Count > 0)
            {
                summary += $" - {Warnings.Count} 个警告";
            }

            return summary;
        }

        /// <summary>
        /// 获取详细报告
        /// </summary>
        /// <returns>详细报告</returns>
        public string GetDetailedReport()
        {
            var report = new System.Text.StringBuilder();

            // 基本信息
            report.AppendLine($"清理操作报告 - {OperationName}");
            report.AppendLine($"操作ID: {OperationId}");
            report.AppendLine($"操作类型: {OperationType}");
            report.AppendLine($"执行状态: {Status}");
            report.AppendLine($"开始时间: {StartTime:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"结束时间: {EndTime:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"执行时长: {Duration.TotalMilliseconds:F0}ms");
            report.AppendLine();

            // 统计信息
            if (TotalTargets > 0)
            {
                report.AppendLine("执行统计:");
                report.AppendLine($"  总目标数: {TotalTargets}");
                report.AppendLine($"  成功数: {SuccessfulTargets}");
                report.AppendLine($"  失败数: {FailedTargets}");
                report.AppendLine($"  成功率: {SuccessRate:F1}%");
                report.AppendLine();
            }

            // 性能统计
            if (CleanedDataSize > 0 || DeletedFileCount > 0 || ClearedEnvironmentVariables > 0 ||
                TerminatedProcesses > 0 || ReleasedMemorySize > 0)
            {
                report.AppendLine("性能统计:");
                if (CleanedDataSize > 0)
                    report.AppendLine($"  清理数据量: {CleanedDataSize:N0} 字节");
                if (DeletedFileCount > 0)
                    report.AppendLine($"  删除文件数: {DeletedFileCount}");
                if (ClearedEnvironmentVariables > 0)
                    report.AppendLine($"  清理环境变量数: {ClearedEnvironmentVariables}");
                if (TerminatedProcesses > 0)
                    report.AppendLine($"  终止进程数: {TerminatedProcesses}");
                if (ReleasedMemorySize > 0)
                    report.AppendLine($"  释放内存: {ReleasedMemorySize:N0} 字节");
                report.AppendLine();
            }

            // 失败详情
            var failedItems = GetItemsByStatus(CleanupResultStatus.Failed).ToList();
            if (failedItems.Any())
            {
                report.AppendLine("失败详情:");
                foreach (var item in failedItems)
                {
                    report.AppendLine($"  ❌ {item.Target}: {item.Message}");
                    if (!string.IsNullOrWhiteSpace(item.ErrorDetails))
                    {
                        report.AppendLine($"     错误: {item.ErrorDetails}");
                    }
                }
                report.AppendLine();
            }

            // 警告信息
            if (Warnings.Count > 0)
            {
                report.AppendLine("警告信息:");
                foreach (var warning in Warnings)
                {
                    report.AppendLine($"  ⚠️ {warning}");
                }
                report.AppendLine();
            }

            // 异常信息
            if (Exception != null)
            {
                report.AppendLine("异常信息:");
                report.AppendLine($"  类型: {Exception.GetType().Name}");
                report.AppendLine($"  消息: {Exception.Message}");
                if (!string.IsNullOrWhiteSpace(Exception.StackTrace))
                {
                    report.AppendLine($"  堆栈跟踪: {Exception.StackTrace}");
                }
            }

            return report.ToString();
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 根据结果项更新整体状态
        /// </summary>
        private void UpdateOverallStatus()
        {
            if (Items.Count == 0)
                return;

            var hasSuccess = Items.Any(item => item.Status == CleanupResultStatus.Success);
            var hasFailure = Items.Any(item => item.Status == CleanupResultStatus.Failed);
            var allSkipped = Items.All(item => item.Status == CleanupResultStatus.Skipped);

            if (allSkipped)
            {
                Status = CleanupResultStatus.Skipped;
            }
            else if (hasSuccess && hasFailure)
            {
                Status = CleanupResultStatus.PartialSuccess;
            }
            else if (hasSuccess && !hasFailure)
            {
                Status = CleanupResultStatus.Success;
            }
            else if (hasFailure)
            {
                Status = CleanupResultStatus.Failed;
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
            return ResultId.GetHashCode();
        }

        /// <summary>
        /// 比较对象相等性
        /// </summary>
        /// <param name="obj">比较对象</param>
        /// <returns>是否相等</returns>
        public override bool Equals(object obj)
        {
            return obj is CleanupResult other && ResultId == other.ResultId;
        }

        #endregion
    }

    #region 扩展方法

    /// <summary>
    /// 清理结果扩展方法
    /// </summary>
    public static class CleanupResultExtensions
    {
        /// <summary>
        /// 合并多个清理结果
        /// </summary>
        /// <param name="results">结果列表</param>
        /// <param name="combinedOperationName">合并操作名称</param>
        /// <returns>合并后的结果</returns>
        public static CleanupResult CombineResults(this IEnumerable<CleanupResult> results, string combinedOperationName = "合并清理操作")
        {
            var resultList = results?.ToList() ?? new List<CleanupResult>();
            if (!resultList.Any())
            {
                throw new ArgumentException("结果列表不能为空", nameof(results));
            }

            // 创建一个虚拟操作用于合并结果
            var firstResult = resultList.First();
            var dummyOperation = new CleanupOperation(combinedOperationName, firstResult.OperationType, CleanupTriggerReason.Manual);

            var combinedResult = CleanupResult.CreateSuccess(dummyOperation, "合并清理结果");

            // 合并所有结果项
            foreach (var result in resultList)
            {
                combinedResult.AddItems(result.Items);

                // 合并统计信息
                combinedResult.UpdateStatistics(
                    result.CleanedDataSize,
                    result.DeletedFileCount,
                    result.ClearedEnvironmentVariables,
                    result.TerminatedProcesses,
                    result.ReleasedMemorySize
                );

                // 合并警告
                foreach (var warning in result.Warnings)
                {
                    combinedResult.AddWarning(warning);
                }

                // 合并标签
                foreach (var tag in result.Tags)
                {
                    combinedResult.AddTag(tag);
                }
            }

            return combinedResult;
        }

        /// <summary>
        /// 检查结果是否需要重试
        /// </summary>
        /// <param name="result">清理结果</param>
        /// <returns>是否需要重试</returns>
        public static bool ShouldRetry(this CleanupResult result)
        {
            return result.IsFailure && !result.IsCanceled && result.FailedTargets > 0;
        }

        /// <summary>
        /// 获取重试目标列表
        /// </summary>
        /// <param name="result">清理结果</param>
        /// <returns>需要重试的目标列表</returns>
        public static IEnumerable<string> GetRetryTargets(this CleanupResult result)
        {
            return result.GetFailedTargets().Where(target =>
                !string.IsNullOrWhiteSpace(target) &&
                !target.Contains("PERMANENT_FAILURE")); // 排除永久失败的目标
        }
    }

    #endregion
}