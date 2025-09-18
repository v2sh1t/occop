using System;
using System.Collections.Generic;
using System.Linq;
using Occop.Core.Services.Environment;

namespace Occop.Core.Models.Environment
{
    /// <summary>
    /// 检测结果
    /// </summary>
    public class DetectionResult
    {
        /// <summary>
        /// 检测开始时间
        /// </summary>
        public DateTime StartTime { get; }

        /// <summary>
        /// 检测完成时间
        /// </summary>
        public DateTime CompletionTime { get; internal set; }

        /// <summary>
        /// 检测总耗时（毫秒）
        /// </summary>
        public long TotalDurationMs => (long)(CompletionTime - StartTime).TotalMilliseconds;

        /// <summary>
        /// 检测到的环境信息列表
        /// </summary>
        public Dictionary<EnvironmentType, EnvironmentInfo> DetectedEnvironments { get; }

        /// <summary>
        /// 检测过程中的错误信息
        /// </summary>
        public List<DetectionError> Errors { get; }

        /// <summary>
        /// 推荐的Shell环境
        /// </summary>
        public EnvironmentInfo? RecommendedShell { get; internal set; }

        /// <summary>
        /// 是否检测成功（至少检测到一个环境）
        /// </summary>
        public bool IsSuccess => DetectedEnvironments.Any(e => e.Value.Status == DetectionStatus.Detected);

        /// <summary>
        /// 检测到的环境数量
        /// </summary>
        public int DetectedCount => DetectedEnvironments.Count(e => e.Value.Status == DetectionStatus.Detected);

        /// <summary>
        /// 是否有Shell环境可用
        /// </summary>
        public bool HasShellEnvironment => DetectedEnvironments.Any(e =>
            (e.Key == EnvironmentType.PowerShell51 || e.Key == EnvironmentType.PowerShellCore || e.Key == EnvironmentType.GitBash)
            && e.Value.Status == DetectionStatus.Detected);

        /// <summary>
        /// 是否检测到Claude Code CLI
        /// </summary>
        public bool HasClaudeCode => DetectedEnvironments.ContainsKey(EnvironmentType.ClaudeCode)
            && DetectedEnvironments[EnvironmentType.ClaudeCode].Status == DetectionStatus.Detected;

        /// <summary>
        /// 初始化检测结果
        /// </summary>
        public DetectionResult()
        {
            StartTime = DateTime.UtcNow;
            CompletionTime = StartTime;
            DetectedEnvironments = new Dictionary<EnvironmentType, EnvironmentInfo>();
            Errors = new List<DetectionError>();
        }

        /// <summary>
        /// 添加环境检测结果
        /// </summary>
        /// <param name="environmentType">环境类型</param>
        /// <param name="environmentInfo">环境信息</param>
        public void AddEnvironment(EnvironmentType environmentType, EnvironmentInfo environmentInfo)
        {
            if (environmentInfo == null)
                throw new ArgumentNullException(nameof(environmentInfo));

            DetectedEnvironments[environmentType] = environmentInfo;
        }

        /// <summary>
        /// 添加检测错误
        /// </summary>
        /// <param name="environmentType">环境类型</param>
        /// <param name="errorMessage">错误消息</param>
        /// <param name="exception">异常信息</param>
        public void AddError(EnvironmentType environmentType, string errorMessage, Exception? exception = null)
        {
            if (string.IsNullOrEmpty(errorMessage))
                throw new ArgumentNullException(nameof(errorMessage));

            Errors.Add(new DetectionError(environmentType, errorMessage, exception));
        }

        /// <summary>
        /// 获取指定类型的环境信息
        /// </summary>
        /// <param name="environmentType">环境类型</param>
        /// <returns>环境信息</returns>
        public EnvironmentInfo? GetEnvironment(EnvironmentType environmentType)
        {
            return DetectedEnvironments.TryGetValue(environmentType, out var environment) ? environment : null;
        }

        /// <summary>
        /// 获取所有可用的Shell环境
        /// </summary>
        /// <returns>可用的Shell环境列表</returns>
        public List<EnvironmentInfo> GetAvailableShells()
        {
            return DetectedEnvironments
                .Where(e => (e.Key == EnvironmentType.PowerShell51 ||
                           e.Key == EnvironmentType.PowerShellCore ||
                           e.Key == EnvironmentType.GitBash) &&
                           e.Value.Status == DetectionStatus.Detected)
                .Select(e => e.Value)
                .OrderByDescending(e => e.Priority)
                .ToList();
        }

        /// <summary>
        /// 生成检测报告摘要
        /// </summary>
        /// <returns>检测报告摘要</returns>
        public string GenerateSummary()
        {
            var summary = $"环境检测完成，耗时 {TotalDurationMs}ms\n";
            summary += $"检测到 {DetectedCount} 个环境：\n";

            foreach (var env in DetectedEnvironments.Where(e => e.Value.Status == DetectionStatus.Detected))
            {
                summary += $"  - {env.Key}: {env.Value.Version} ({env.Value.InstallPath})\n";
            }

            if (RecommendedShell != null)
            {
                summary += $"推荐Shell: {RecommendedShell.Type} {RecommendedShell.Version}\n";
            }

            if (Errors.Any())
            {
                summary += $"\n检测错误 ({Errors.Count})：\n";
                foreach (var error in Errors)
                {
                    summary += $"  - {error.EnvironmentType}: {error.ErrorMessage}\n";
                }
            }

            return summary;
        }

        /// <summary>
        /// 标记检测完成
        /// </summary>
        internal void MarkCompleted()
        {
            CompletionTime = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// 检测错误信息
    /// </summary>
    public class DetectionError
    {
        /// <summary>
        /// 环境类型
        /// </summary>
        public EnvironmentType EnvironmentType { get; }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// 异常信息
        /// </summary>
        public Exception? Exception { get; }

        /// <summary>
        /// 错误发生时间
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// 初始化检测错误
        /// </summary>
        /// <param name="environmentType">环境类型</param>
        /// <param name="errorMessage">错误消息</param>
        /// <param name="exception">异常信息</param>
        public DetectionError(EnvironmentType environmentType, string errorMessage, Exception? exception = null)
        {
            EnvironmentType = environmentType;
            ErrorMessage = errorMessage ?? throw new ArgumentNullException(nameof(errorMessage));
            Exception = exception;
            Timestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// 返回错误的字符串表示
        /// </summary>
        /// <returns>错误字符串</returns>
        public override string ToString()
        {
            var result = $"[{EnvironmentType}] {ErrorMessage}";
            if (Exception != null)
            {
                result += $" - {Exception.Message}";
            }
            return result;
        }
    }
}