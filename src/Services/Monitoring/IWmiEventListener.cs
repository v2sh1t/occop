using System;
using System.Threading;
using System.Threading.Tasks;
using Occop.Models.Monitoring;

namespace Occop.Services.Monitoring
{
    /// <summary>
    /// WMI事件监听器接口
    /// 为Stream B预留的接口，用于实现基于WMI的进程事件监听
    /// </summary>
    public interface IWmiEventListener : IDisposable
    {
        #region 事件定义

        /// <summary>
        /// 进程创建事件
        /// </summary>
        event EventHandler<WmiProcessEventArgs> ProcessCreated;

        /// <summary>
        /// 进程删除事件
        /// </summary>
        event EventHandler<WmiProcessEventArgs> ProcessDeleted;

        /// <summary>
        /// WMI事件监听错误
        /// </summary>
        event EventHandler<WmiEventErrorArgs> EventError;

        #endregion

        #region 属性

        /// <summary>
        /// 获取是否正在监听
        /// </summary>
        bool IsListening { get; }

        /// <summary>
        /// 获取监听配置
        /// </summary>
        WmiListenerConfig Config { get; }

        /// <summary>
        /// 获取监听统计信息
        /// </summary>
        WmiListenerStatistics Statistics { get; }

        #endregion

        #region 方法

        /// <summary>
        /// 启动WMI事件监听
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>启动结果</returns>
        Task<MonitoringResult> StartListeningAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 停止WMI事件监听
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>停止结果</returns>
        Task<MonitoringResult> StopListeningAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 设置监听配置
        /// </summary>
        /// <param name="config">配置信息</param>
        /// <returns>设置结果</returns>
        MonitoringResult SetConfig(WmiListenerConfig config);

        /// <summary>
        /// 检查WMI服务可用性
        /// </summary>
        /// <returns>检查结果</returns>
        Task<MonitoringResult> CheckWmiAvailabilityAsync();

        #endregion
    }

    /// <summary>
    /// WMI监听器配置
    /// </summary>
    public class WmiListenerConfig
    {
        /// <summary>
        /// 是否监听进程创建事件
        /// </summary>
        public bool ListenProcessCreation { get; set; } = true;

        /// <summary>
        /// 是否监听进程删除事件
        /// </summary>
        public bool ListenProcessDeletion { get; set; } = true;

        /// <summary>
        /// WMI查询超时时间（秒）
        /// </summary>
        public int QueryTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// 监听的进程名称过滤器
        /// </summary>
        public string[] ProcessNameFilters { get; set; } = Array.Empty<string>();

        /// <summary>
        /// 是否仅监听AI工具进程
        /// </summary>
        public bool OnlyAIToolProcesses { get; set; } = true;

        /// <summary>
        /// WMI命名空间
        /// </summary>
        public string WmiNamespace { get; set; } = @"root\cimv2";

        /// <summary>
        /// 错误重试次数
        /// </summary>
        public int MaxRetryCount { get; set; } = 3;

        /// <summary>
        /// 重试间隔（毫秒）
        /// </summary>
        public int RetryIntervalMs { get; set; } = 5000;
    }

    /// <summary>
    /// WMI监听器统计信息
    /// </summary>
    public class WmiListenerStatistics
    {
        /// <summary>
        /// 监听开始时间
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// 总运行时间
        /// </summary>
        public TimeSpan TotalRunTime { get; set; }

        /// <summary>
        /// 检测到的进程创建数量
        /// </summary>
        public int ProcessCreationCount { get; set; }

        /// <summary>
        /// 检测到的进程删除数量
        /// </summary>
        public int ProcessDeletionCount { get; set; }

        /// <summary>
        /// WMI事件总数
        /// </summary>
        public int TotalEventCount { get; set; }

        /// <summary>
        /// 错误数量
        /// </summary>
        public int ErrorCount { get; set; }

        /// <summary>
        /// 重连次数
        /// </summary>
        public int ReconnectionCount { get; set; }

        /// <summary>
        /// 最后一次事件时间
        /// </summary>
        public DateTime? LastEventTime { get; set; }

        /// <summary>
        /// 最后一次错误时间
        /// </summary>
        public DateTime? LastErrorTime { get; set; }
    }

    #region 事件参数类

    /// <summary>
    /// WMI进程事件参数
    /// </summary>
    public class WmiProcessEventArgs : EventArgs
    {
        /// <summary>
        /// 进程ID
        /// </summary>
        public int ProcessId { get; }

        /// <summary>
        /// 进程名称
        /// </summary>
        public string ProcessName { get; }

        /// <summary>
        /// 进程路径
        /// </summary>
        public string ProcessPath { get; }

        /// <summary>
        /// 父进程ID
        /// </summary>
        public int? ParentProcessId { get; }

        /// <summary>
        /// 事件时间
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// 命令行参数
        /// </summary>
        public string CommandLine { get; }

        /// <summary>
        /// 事件类型
        /// </summary>
        public WmiProcessEventType EventType { get; }

        public WmiProcessEventArgs(int processId, string processName, WmiProcessEventType eventType,
            string processPath = null, int? parentProcessId = null, string commandLine = null)
        {
            ProcessId = processId;
            ProcessName = processName;
            EventType = eventType;
            ProcessPath = processPath;
            ParentProcessId = parentProcessId;
            CommandLine = commandLine;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// WMI事件错误参数
    /// </summary>
    public class WmiEventErrorArgs : EventArgs
    {
        /// <summary>
        /// 错误异常
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 错误上下文
        /// </summary>
        public string Context { get; }

        /// <summary>
        /// 错误时间
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// 是否可以重试
        /// </summary>
        public bool CanRetry { get; }

        public WmiEventErrorArgs(Exception exception, string context = null, bool canRetry = true)
        {
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
            Message = exception.Message;
            Context = context;
            Timestamp = DateTime.UtcNow;
            CanRetry = canRetry;
        }
    }

    /// <summary>
    /// WMI进程事件类型
    /// </summary>
    public enum WmiProcessEventType
    {
        /// <summary>
        /// 进程创建
        /// </summary>
        ProcessCreated,

        /// <summary>
        /// 进程删除
        /// </summary>
        ProcessDeleted
    }

    #endregion

    /// <summary>
    /// WMI事件监听器实现基类
    /// 为Stream B提供的基础实现框架
    /// </summary>
    public abstract class WmiEventListenerBase : IWmiEventListener
    {
        #region 字段和属性

        protected WmiListenerConfig _config;
        protected WmiListenerStatistics _statistics;
        protected volatile bool _isListening;
        protected volatile bool _disposed;

        /// <summary>
        /// 获取是否正在监听
        /// </summary>
        public bool IsListening => _isListening && !_disposed;

        /// <summary>
        /// 获取监听配置
        /// </summary>
        public WmiListenerConfig Config => _config;

        /// <summary>
        /// 获取监听统计信息
        /// </summary>
        public WmiListenerStatistics Statistics => _statistics;

        #endregion

        #region 事件定义

        /// <summary>
        /// 进程创建事件
        /// </summary>
        public event EventHandler<WmiProcessEventArgs> ProcessCreated;

        /// <summary>
        /// 进程删除事件
        /// </summary>
        public event EventHandler<WmiProcessEventArgs> ProcessDeleted;

        /// <summary>
        /// WMI事件监听错误
        /// </summary>
        public event EventHandler<WmiEventErrorArgs> EventError;

        #endregion

        #region 构造函数

        protected WmiEventListenerBase(WmiListenerConfig config = null)
        {
            _config = config ?? new WmiListenerConfig();
            _statistics = new WmiListenerStatistics();
        }

        #endregion

        #region 抽象方法 (Stream B需要实现)

        /// <summary>
        /// 启动WMI事件监听 (Stream B实现)
        /// </summary>
        public abstract Task<MonitoringResult> StartListeningAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 停止WMI事件监听 (Stream B实现)
        /// </summary>
        public abstract Task<MonitoringResult> StopListeningAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 检查WMI服务可用性 (Stream B实现)
        /// </summary>
        public abstract Task<MonitoringResult> CheckWmiAvailabilityAsync();

        #endregion

        #region 虚方法 (可被Stream B重写)

        /// <summary>
        /// 设置监听配置
        /// </summary>
        public virtual MonitoringResult SetConfig(WmiListenerConfig config)
        {
            if (config == null)
                return MonitoringResult.Failure("配置不能为空");

            if (_isListening)
                return MonitoringResult.Failure("监听器运行中，无法更改配置");

            _config = config;
            return MonitoringResult.Success("配置已更新");
        }

        #endregion

        #region 事件触发方法

        /// <summary>
        /// 触发进程创建事件
        /// </summary>
        protected virtual void OnProcessCreated(WmiProcessEventArgs e)
        {
            _statistics.ProcessCreationCount++;
            _statistics.TotalEventCount++;
            _statistics.LastEventTime = DateTime.UtcNow;
            ProcessCreated?.Invoke(this, e);
        }

        /// <summary>
        /// 触发进程删除事件
        /// </summary>
        protected virtual void OnProcessDeleted(WmiProcessEventArgs e)
        {
            _statistics.ProcessDeletionCount++;
            _statistics.TotalEventCount++;
            _statistics.LastEventTime = DateTime.UtcNow;
            ProcessDeleted?.Invoke(this, e);
        }

        /// <summary>
        /// 触发错误事件
        /// </summary>
        protected virtual void OnEventError(WmiEventErrorArgs e)
        {
            _statistics.ErrorCount++;
            _statistics.LastErrorTime = DateTime.UtcNow;
            EventError?.Invoke(this, e);
        }

        #endregion

        #region IDisposable 实现

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing && _isListening)
            {
                try
                {
                    StopListeningAsync().Wait(TimeSpan.FromSeconds(5));
                }
                catch { /* 忽略清理异常 */ }
            }

            _disposed = true;
        }

        /// <summary>
        /// 析构函数
        /// </summary>
        ~WmiEventListenerBase()
        {
            Dispose(false);
        }

        #endregion
    }
}