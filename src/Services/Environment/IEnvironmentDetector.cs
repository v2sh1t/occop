using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Occop.Core.Services.Environment
{
    /// <summary>
    /// 环境类型枚举
    /// </summary>
    public enum EnvironmentType
    {
        /// <summary>
        /// PowerShell 5.1
        /// </summary>
        PowerShell51,

        /// <summary>
        /// PowerShell Core 7+
        /// </summary>
        PowerShellCore,

        /// <summary>
        /// Git Bash
        /// </summary>
        GitBash,

        /// <summary>
        /// Claude Code CLI
        /// </summary>
        ClaudeCode
    }

    /// <summary>
    /// 检测状态枚举
    /// </summary>
    public enum DetectionStatus
    {
        /// <summary>
        /// 未检测
        /// </summary>
        NotDetected,

        /// <summary>
        /// 检测中
        /// </summary>
        Detecting,

        /// <summary>
        /// 检测成功
        /// </summary>
        Detected,

        /// <summary>
        /// 检测失败
        /// </summary>
        Failed,

        /// <summary>
        /// 已安装但版本不兼容
        /// </summary>
        IncompatibleVersion
    }

    /// <summary>
    /// 环境检测器接口
    /// </summary>
    public interface IEnvironmentDetector
    {
        /// <summary>
        /// 检测所有支持的环境
        /// </summary>
        /// <param name="forceRefresh">是否强制刷新缓存</param>
        /// <returns>检测结果</returns>
        Task<DetectionResult> DetectAllEnvironmentsAsync(bool forceRefresh = false);

        /// <summary>
        /// 检测特定类型的环境
        /// </summary>
        /// <param name="environmentType">环境类型</param>
        /// <param name="forceRefresh">是否强制刷新缓存</param>
        /// <returns>环境信息</returns>
        Task<EnvironmentInfo> DetectEnvironmentAsync(EnvironmentType environmentType, bool forceRefresh = false);

        /// <summary>
        /// 获取推荐的Shell环境（基于优先级）
        /// </summary>
        /// <returns>推荐的环境信息</returns>
        Task<EnvironmentInfo?> GetRecommendedShellAsync();

        /// <summary>
        /// 检查缓存是否有效
        /// </summary>
        /// <param name="environmentType">环境类型</param>
        /// <returns>缓存是否有效</returns>
        bool IsCacheValid(EnvironmentType environmentType);

        /// <summary>
        /// 清除指定环境的缓存
        /// </summary>
        /// <param name="environmentType">环境类型，null表示清除所有缓存</param>
        void ClearCache(EnvironmentType? environmentType = null);

        /// <summary>
        /// 启动环境变化监控
        /// </summary>
        void StartEnvironmentMonitoring();

        /// <summary>
        /// 停止环境变化监控
        /// </summary>
        void StopEnvironmentMonitoring();

        /// <summary>
        /// 环境变化事件
        /// </summary>
        event EventHandler<EnvironmentChangedEventArgs> EnvironmentChanged;
    }

    /// <summary>
    /// 环境变化事件参数
    /// </summary>
    public class EnvironmentChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 环境类型
        /// </summary>
        public EnvironmentType EnvironmentType { get; }

        /// <summary>
        /// 变化类型
        /// </summary>
        public EnvironmentChangeType ChangeType { get; }

        /// <summary>
        /// 之前的环境信息
        /// </summary>
        public EnvironmentInfo? OldEnvironmentInfo { get; }

        /// <summary>
        /// 新的环境信息
        /// </summary>
        public EnvironmentInfo? NewEnvironmentInfo { get; }

        /// <summary>
        /// 变化发生时间
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// 初始化环境变化事件参数
        /// </summary>
        /// <param name="environmentType">环境类型</param>
        /// <param name="changeType">变化类型</param>
        /// <param name="oldEnvironmentInfo">之前的环境信息</param>
        /// <param name="newEnvironmentInfo">新的环境信息</param>
        public EnvironmentChangedEventArgs(
            EnvironmentType environmentType,
            EnvironmentChangeType changeType,
            EnvironmentInfo? oldEnvironmentInfo = null,
            EnvironmentInfo? newEnvironmentInfo = null)
        {
            EnvironmentType = environmentType;
            ChangeType = changeType;
            OldEnvironmentInfo = oldEnvironmentInfo;
            NewEnvironmentInfo = newEnvironmentInfo;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// 环境变化类型枚举
    /// </summary>
    public enum EnvironmentChangeType
    {
        /// <summary>
        /// 环境安装
        /// </summary>
        Installed,

        /// <summary>
        /// 环境卸载
        /// </summary>
        Uninstalled,

        /// <summary>
        /// 版本更新
        /// </summary>
        VersionUpdated,

        /// <summary>
        /// 路径变化
        /// </summary>
        PathChanged,

        /// <summary>
        /// 状态变化
        /// </summary>
        StatusChanged
    }
}