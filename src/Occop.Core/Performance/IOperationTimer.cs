using System;
using System.Collections.Generic;

namespace Occop.Core.Performance
{
    /// <summary>
    /// 操作计时器接口
    /// Operation timer interface
    /// </summary>
    public interface IOperationTimer : IDisposable
    {
        /// <summary>
        /// 操作名称
        /// Operation name
        /// </summary>
        string OperationName { get; }

        /// <summary>
        /// 操作分类
        /// Operation category
        /// </summary>
        string Category { get; }

        /// <summary>
        /// 开始时间
        /// Start time
        /// </summary>
        DateTime StartTime { get; }

        /// <summary>
        /// 已用时间（毫秒）
        /// Elapsed time in milliseconds
        /// </summary>
        long ElapsedMilliseconds { get; }

        /// <summary>
        /// 是否正在运行
        /// Whether running
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// 元数据
        /// Metadata
        /// </summary>
        Dictionary<string, object> Metadata { get; }

        /// <summary>
        /// 添加元数据
        /// Add metadata
        /// </summary>
        /// <param name="key">键 Key</param>
        /// <param name="value">值 Value</param>
        void AddMetadata(string key, object value);

        /// <summary>
        /// 停止计时并记录成功
        /// Stop timer and record as success
        /// </summary>
        void Stop();

        /// <summary>
        /// 停止计时并记录失败
        /// Stop timer and record as failure
        /// </summary>
        /// <param name="error">错误信息 Error message</param>
        void Fail(string? error = null);

        /// <summary>
        /// 记录检查点
        /// Record checkpoint
        /// </summary>
        /// <param name="checkpointName">检查点名称 Checkpoint name</param>
        void Checkpoint(string checkpointName);

        /// <summary>
        /// 获取所有检查点
        /// Get all checkpoints
        /// </summary>
        /// <returns>检查点列表 List of checkpoints</returns>
        List<OperationCheckpoint> GetCheckpoints();
    }

    /// <summary>
    /// 操作检查点
    /// Operation checkpoint
    /// </summary>
    public class OperationCheckpoint
    {
        /// <summary>
        /// 检查点名称
        /// Checkpoint name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 时间戳
        /// Timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 从开始的耗时（毫秒）
        /// Elapsed time from start in milliseconds
        /// </summary>
        public long ElapsedMs { get; set; }
    }
}
