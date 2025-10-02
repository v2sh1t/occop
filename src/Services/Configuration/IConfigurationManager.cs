using System;
using System.Collections.Generic;
using System.Security;
using System.Threading.Tasks;
using Occop.Core.Patterns.Observer;

namespace Occop.Services.Configuration
{
    /// <summary>
    /// Claude Code配置管理器状态枚举
    /// </summary>
    public enum ConfigurationState
    {
        /// <summary>
        /// 未初始化
        /// </summary>
        Uninitialized,

        /// <summary>
        /// 已初始化但未配置
        /// </summary>
        Initialized,

        /// <summary>
        /// 已配置
        /// </summary>
        Configured,

        /// <summary>
        /// 配置已应用（环境变量已设置）
        /// </summary>
        Applied,

        /// <summary>
        /// 配置错误
        /// </summary>
        Error,

        /// <summary>
        /// 已清理
        /// </summary>
        Cleared
    }

    /// <summary>
    /// 配置操作类型枚举
    /// </summary>
    public enum ConfigurationOperation
    {
        /// <summary>
        /// 设置配置
        /// </summary>
        Set,

        /// <summary>
        /// 应用配置（设置环境变量）
        /// </summary>
        Apply,

        /// <summary>
        /// 验证配置
        /// </summary>
        Validate,

        /// <summary>
        /// 清理配置
        /// </summary>
        Clear,

        /// <summary>
        /// 回滚配置
        /// </summary>
        Rollback
    }

    /// <summary>
    /// 配置操作结果
    /// </summary>
    public class ConfigurationResult
    {
        /// <summary>
        /// 操作是否成功
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// 操作类型
        /// </summary>
        public ConfigurationOperation Operation { get; }

        /// <summary>
        /// 结果消息
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 异常信息（如果有）
        /// </summary>
        public Exception? Exception { get; }

        /// <summary>
        /// 操作时间戳
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// 初始化配置操作结果
        /// </summary>
        /// <param name="isSuccess">是否成功</param>
        /// <param name="operation">操作类型</param>
        /// <param name="message">结果消息</param>
        /// <param name="exception">异常信息</param>
        public ConfigurationResult(bool isSuccess, ConfigurationOperation operation, string message, Exception? exception = null)
        {
            IsSuccess = isSuccess;
            Operation = operation;
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Exception = exception;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// 配置状态变更事件数据
    /// </summary>
    public class ConfigurationStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 旧状态
        /// </summary>
        public ConfigurationState OldState { get; }

        /// <summary>
        /// 新状态
        /// </summary>
        public ConfigurationState NewState { get; }

        /// <summary>
        /// 状态变更原因
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// 初始化配置状态变更事件数据
        /// </summary>
        /// <param name="oldState">旧状态</param>
        /// <param name="newState">新状态</param>
        /// <param name="reason">变更原因</param>
        public ConfigurationStateChangedEventArgs(ConfigurationState oldState, ConfigurationState newState, string reason)
        {
            OldState = oldState;
            NewState = newState;
            Reason = reason ?? throw new ArgumentNullException(nameof(reason));
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Claude Code配置管理器接口
    /// 专门用于管理Claude Code的环境变量配置
    /// </summary>
    public interface IConfigurationManager : IDisposable
    {
        /// <summary>
        /// 当前配置状态
        /// </summary>
        ConfigurationState CurrentState { get; }

        /// <summary>
        /// 配置是否已应用（环境变量是否已设置）
        /// </summary>
        bool IsApplied { get; }

        /// <summary>
        /// 配置状态变更事件
        /// </summary>
        event EventHandler<ConfigurationStateChangedEventArgs> StateChanged;

        /// <summary>
        /// 配置操作完成事件
        /// </summary>
        event EventHandler<ConfigurationResult> OperationCompleted;

        /// <summary>
        /// 初始化配置管理器
        /// </summary>
        /// <returns>初始化任务</returns>
        Task<ConfigurationResult> InitializeAsync();

        /// <summary>
        /// 设置Claude Code认证令牌
        /// </summary>
        /// <param name="token">认证令牌（SecureString）</param>
        /// <returns>设置结果</returns>
        Task<ConfigurationResult> SetAuthTokenAsync(SecureString token);

        /// <summary>
        /// 设置Claude Code API基础URL
        /// </summary>
        /// <param name="baseUrl">API基础URL</param>
        /// <returns>设置结果</returns>
        Task<ConfigurationResult> SetBaseUrlAsync(string baseUrl);

        /// <summary>
        /// 应用配置（设置进程级环境变量）
        /// </summary>
        /// <returns>应用结果</returns>
        Task<ConfigurationResult> ApplyConfigurationAsync();

        /// <summary>
        /// 验证当前配置
        /// </summary>
        /// <returns>验证结果</returns>
        Task<ConfigurationResult> ValidateConfigurationAsync();

        /// <summary>
        /// 健康检查（验证Claude Code是否能正常工作）
        /// </summary>
        /// <returns>健康检查结果</returns>
        Task<ConfigurationResult> HealthCheckAsync();

        /// <summary>
        /// 清理配置（清除环境变量和内存中的敏感信息）
        /// </summary>
        /// <returns>清理结果</returns>
        Task<ConfigurationResult> ClearConfigurationAsync();

        /// <summary>
        /// 回滚到上一个工作配置
        /// </summary>
        /// <returns>回滚结果</returns>
        Task<ConfigurationResult> RollbackConfigurationAsync();

        /// <summary>
        /// 获取配置状态详情
        /// </summary>
        /// <returns>配置状态信息</returns>
        Dictionary<string, object> GetStateDetails();

        /// <summary>
        /// 获取配置操作历史（不包含敏感信息）
        /// </summary>
        /// <returns>操作历史</returns>
        IEnumerable<ConfigurationResult> GetOperationHistory();

        /// <summary>
        /// 注册状态观察者
        /// </summary>
        /// <param name="observer">观察者</param>
        void RegisterStateObserver(IObserver<ConfigurationStateChangedEventArgs> observer);

        /// <summary>
        /// 注销状态观察者
        /// </summary>
        /// <param name="observer">观察者</param>
        void UnregisterStateObserver(IObserver<ConfigurationStateChangedEventArgs> observer);
    }
}