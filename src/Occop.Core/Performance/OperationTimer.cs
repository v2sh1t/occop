using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Occop.Core.Performance
{
    /// <summary>
    /// 操作计时器实现
    /// Operation timer implementation
    /// </summary>
    public class OperationTimer : IOperationTimer
    {
        private readonly IPerformanceMonitor _monitor;
        private readonly Stopwatch _stopwatch;
        private readonly List<OperationCheckpoint> _checkpoints;
        private bool _disposed = false;
        private bool _completed = false;

        /// <summary>
        /// 操作名称
        /// Operation name
        /// </summary>
        public string OperationName { get; }

        /// <summary>
        /// 操作分类
        /// Operation category
        /// </summary>
        public string Category { get; }

        /// <summary>
        /// 开始时间
        /// Start time
        /// </summary>
        public DateTime StartTime { get; }

        /// <summary>
        /// 已用时间（毫秒）
        /// Elapsed time in milliseconds
        /// </summary>
        public long ElapsedMilliseconds => _stopwatch.ElapsedMilliseconds;

        /// <summary>
        /// 是否正在运行
        /// Whether running
        /// </summary>
        public bool IsRunning => _stopwatch.IsRunning;

        /// <summary>
        /// 元数据
        /// Metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; }

        /// <summary>
        /// 初始化操作计时器
        /// Initializes operation timer
        /// </summary>
        /// <param name="monitor">性能监控器 Performance monitor</param>
        /// <param name="operationName">操作名称 Operation name</param>
        /// <param name="category">操作分类 Operation category</param>
        public OperationTimer(IPerformanceMonitor monitor, string operationName, string category = "General")
        {
            _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            OperationName = operationName ?? throw new ArgumentNullException(nameof(operationName));
            Category = category ?? "General";
            StartTime = DateTime.UtcNow;
            Metadata = new Dictionary<string, object>();
            _checkpoints = new List<OperationCheckpoint>();

            _stopwatch = Stopwatch.StartNew();
        }

        /// <summary>
        /// 添加元数据
        /// Add metadata
        /// </summary>
        /// <param name="key">键 Key</param>
        /// <param name="value">值 Value</param>
        public void AddMetadata(string key, object value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            Metadata[key] = value;
        }

        /// <summary>
        /// 停止计时并记录成功
        /// Stop timer and record as success
        /// </summary>
        public void Stop()
        {
            if (_completed)
                return;

            _stopwatch.Stop();
            _completed = true;

            _monitor.RecordOperation(OperationName, _stopwatch.ElapsedMilliseconds, true, Metadata);
        }

        /// <summary>
        /// 停止计时并记录失败
        /// Stop timer and record as failure
        /// </summary>
        /// <param name="error">错误信息 Error message</param>
        public void Fail(string? error = null)
        {
            if (_completed)
                return;

            _stopwatch.Stop();
            _completed = true;

            if (!string.IsNullOrEmpty(error))
            {
                Metadata["Error"] = error;
            }

            _monitor.RecordOperation(OperationName, _stopwatch.ElapsedMilliseconds, false, Metadata);
        }

        /// <summary>
        /// 记录检查点
        /// Record checkpoint
        /// </summary>
        /// <param name="checkpointName">检查点名称 Checkpoint name</param>
        public void Checkpoint(string checkpointName)
        {
            if (string.IsNullOrWhiteSpace(checkpointName))
                throw new ArgumentException("Checkpoint name cannot be null or empty", nameof(checkpointName));

            var checkpoint = new OperationCheckpoint
            {
                Name = checkpointName,
                Timestamp = DateTime.UtcNow,
                ElapsedMs = _stopwatch.ElapsedMilliseconds
            };

            _checkpoints.Add(checkpoint);
        }

        /// <summary>
        /// 获取所有检查点
        /// Get all checkpoints
        /// </summary>
        /// <returns>检查点列表 List of checkpoints</returns>
        public List<OperationCheckpoint> GetCheckpoints()
        {
            return new List<OperationCheckpoint>(_checkpoints);
        }

        /// <summary>
        /// 释放资源（自动调用Stop）
        /// Dispose resources (automatically calls Stop)
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            if (!_completed)
            {
                Stop();
            }

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
