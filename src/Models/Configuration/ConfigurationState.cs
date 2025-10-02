using System;
using System.Collections.Generic;
using System.Linq;
using Occop.Services.Configuration;

namespace Occop.Models.Configuration
{
    /// <summary>
    /// 配置状态详细信息
    /// 提供配置管理器的完整状态信息
    /// </summary>
    public class ConfigurationState
    {
        /// <summary>
        /// 当前状态
        /// </summary>
        public Occop.Services.Configuration.ConfigurationState Current { get; private set; }

        /// <summary>
        /// 上一个状态
        /// </summary>
        public Occop.Services.Configuration.ConfigurationState Previous { get; private set; }

        /// <summary>
        /// 状态变更时间
        /// </summary>
        public DateTime StateChangedAt { get; private set; }

        /// <summary>
        /// 已配置的项数量
        /// </summary>
        public int ConfiguredItemsCount { get; private set; }

        /// <summary>
        /// 必需项是否都已配置
        /// </summary>
        public bool AllRequiredConfigured { get; private set; }

        /// <summary>
        /// 环境变量是否已应用
        /// </summary>
        public bool EnvironmentVariablesApplied { get; private set; }

        /// <summary>
        /// 最后验证时间
        /// </summary>
        public DateTime? LastValidatedAt { get; private set; }

        /// <summary>
        /// 最后验证结果
        /// </summary>
        public bool? LastValidationResult { get; private set; }

        /// <summary>
        /// 最后健康检查时间
        /// </summary>
        public DateTime? LastHealthCheckAt { get; private set; }

        /// <summary>
        /// 最后健康检查结果
        /// </summary>
        public bool? LastHealthCheckResult { get; private set; }

        /// <summary>
        /// 配置错误信息
        /// </summary>
        public List<string> ConfigurationErrors { get; private set; }

        /// <summary>
        /// 状态变更历史
        /// </summary>
        public List<StateTransition> StateHistory { get; private set; }

        /// <summary>
        /// 已设置的环境变量名称列表
        /// </summary>
        public List<string> AppliedEnvironmentVariables { get; private set; }

        /// <summary>
        /// 配置项统计信息
        /// </summary>
        public ConfigurationStatistics Statistics { get; private set; }

        /// <summary>
        /// 初始化配置状态
        /// </summary>
        public ConfigurationState()
        {
            Current = Occop.Services.Configuration.ConfigurationState.Uninitialized;
            Previous = Occop.Services.Configuration.ConfigurationState.Uninitialized;
            StateChangedAt = DateTime.UtcNow;
            ConfiguredItemsCount = 0;
            AllRequiredConfigured = false;
            EnvironmentVariablesApplied = false;
            ConfigurationErrors = new List<string>();
            StateHistory = new List<StateTransition>();
            AppliedEnvironmentVariables = new List<string>();
            Statistics = new ConfigurationStatistics();
        }

        /// <summary>
        /// 更新状态
        /// </summary>
        /// <param name="newState">新状态</param>
        /// <param name="reason">状态变更原因</param>
        public void UpdateState(Occop.Services.Configuration.ConfigurationState newState, string reason)
        {
            if (Current != newState)
            {
                var transition = new StateTransition(Current, newState, reason, DateTime.UtcNow);
                StateHistory.Add(transition);

                Previous = Current;
                Current = newState;
                StateChangedAt = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// 更新配置项统计
        /// </summary>
        /// <param name="configuredCount">已配置项数量</param>
        /// <param name="allRequiredConfigured">所有必需项是否已配置</param>
        public void UpdateConfigurationStats(int configuredCount, bool allRequiredConfigured)
        {
            ConfiguredItemsCount = configuredCount;
            AllRequiredConfigured = allRequiredConfigured;
            Statistics.TotalConfiguredItems = configuredCount;
            Statistics.LastUpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// 更新环境变量应用状态
        /// </summary>
        /// <param name="applied">是否已应用</param>
        /// <param name="appliedVariables">已应用的环境变量列表</param>
        public void UpdateEnvironmentVariableStatus(bool applied, IEnumerable<string>? appliedVariables = null)
        {
            EnvironmentVariablesApplied = applied;

            if (appliedVariables != null)
            {
                AppliedEnvironmentVariables.Clear();
                AppliedEnvironmentVariables.AddRange(appliedVariables);
            }
            else if (!applied)
            {
                AppliedEnvironmentVariables.Clear();
            }
        }

        /// <summary>
        /// 更新验证结果
        /// </summary>
        /// <param name="result">验证结果</param>
        public void UpdateValidationResult(bool result)
        {
            LastValidatedAt = DateTime.UtcNow;
            LastValidationResult = result;
            Statistics.TotalValidations++;

            if (result)
                Statistics.SuccessfulValidations++;
        }

        /// <summary>
        /// 更新健康检查结果
        /// </summary>
        /// <param name="result">健康检查结果</param>
        public void UpdateHealthCheckResult(bool result)
        {
            LastHealthCheckAt = DateTime.UtcNow;
            LastHealthCheckResult = result;
            Statistics.TotalHealthChecks++;

            if (result)
                Statistics.SuccessfulHealthChecks++;
        }

        /// <summary>
        /// 添加配置错误
        /// </summary>
        /// <param name="error">错误信息</param>
        public void AddConfigurationError(string error)
        {
            if (!string.IsNullOrEmpty(error) && !ConfigurationErrors.Contains(error))
            {
                ConfigurationErrors.Add(error);
                Statistics.TotalErrors++;
            }
        }

        /// <summary>
        /// 清除配置错误
        /// </summary>
        public void ClearConfigurationErrors()
        {
            ConfigurationErrors.Clear();
        }

        /// <summary>
        /// 获取状态摘要
        /// </summary>
        /// <returns>状态摘要信息</returns>
        public Dictionary<string, object> GetSummary()
        {
            return new Dictionary<string, object>
            {
                { "CurrentState", Current.ToString() },
                { "PreviousState", Previous.ToString() },
                { "StateChangedAt", StateChangedAt },
                { "ConfiguredItemsCount", ConfiguredItemsCount },
                { "AllRequiredConfigured", AllRequiredConfigured },
                { "EnvironmentVariablesApplied", EnvironmentVariablesApplied },
                { "AppliedEnvironmentVariablesCount", AppliedEnvironmentVariables.Count },
                { "LastValidatedAt", LastValidatedAt },
                { "LastValidationResult", LastValidationResult },
                { "LastHealthCheckAt", LastHealthCheckAt },
                { "LastHealthCheckResult", LastHealthCheckResult },
                { "ConfigurationErrorsCount", ConfigurationErrors.Count },
                { "StateTransitionsCount", StateHistory.Count },
                { "Statistics", Statistics.GetSummary() }
            };
        }

        /// <summary>
        /// 检查配置是否健康
        /// </summary>
        /// <returns>是否健康</returns>
        public bool IsHealthy()
        {
            return Current == Occop.Services.Configuration.ConfigurationState.Applied &&
                   AllRequiredConfigured &&
                   EnvironmentVariablesApplied &&
                   ConfigurationErrors.Count == 0 &&
                   LastValidationResult == true &&
                   LastHealthCheckResult == true;
        }
    }

    /// <summary>
    /// 状态转换记录
    /// </summary>
    public class StateTransition
    {
        /// <summary>
        /// 源状态
        /// </summary>
        public Occop.Services.Configuration.ConfigurationState FromState { get; }

        /// <summary>
        /// 目标状态
        /// </summary>
        public Occop.Services.Configuration.ConfigurationState ToState { get; }

        /// <summary>
        /// 转换原因
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// 转换时间
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// 初始化状态转换记录
        /// </summary>
        /// <param name="fromState">源状态</param>
        /// <param name="toState">目标状态</param>
        /// <param name="reason">转换原因</param>
        /// <param name="timestamp">转换时间</param>
        public StateTransition(
            Occop.Services.Configuration.ConfigurationState fromState,
            Occop.Services.Configuration.ConfigurationState toState,
            string reason,
            DateTime timestamp)
        {
            FromState = fromState;
            ToState = toState;
            Reason = reason ?? throw new ArgumentNullException(nameof(reason));
            Timestamp = timestamp;
        }
    }

    /// <summary>
    /// 配置统计信息
    /// </summary>
    public class ConfigurationStatistics
    {
        /// <summary>
        /// 总配置项数量
        /// </summary>
        public int TotalConfiguredItems { get; set; }

        /// <summary>
        /// 总验证次数
        /// </summary>
        public int TotalValidations { get; set; }

        /// <summary>
        /// 成功验证次数
        /// </summary>
        public int SuccessfulValidations { get; set; }

        /// <summary>
        /// 总健康检查次数
        /// </summary>
        public int TotalHealthChecks { get; set; }

        /// <summary>
        /// 成功健康检查次数
        /// </summary>
        public int SuccessfulHealthChecks { get; set; }

        /// <summary>
        /// 总错误数量
        /// </summary>
        public int TotalErrors { get; set; }

        /// <summary>
        /// 最后更新时间
        /// </summary>
        public DateTime LastUpdatedAt { get; set; }

        /// <summary>
        /// 初始化配置统计
        /// </summary>
        public ConfigurationStatistics()
        {
            LastUpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// 获取统计摘要
        /// </summary>
        /// <returns>统计摘要</returns>
        public Dictionary<string, object> GetSummary()
        {
            var validationSuccessRate = TotalValidations > 0 ?
                (double)SuccessfulValidations / TotalValidations * 100 : 0;

            var healthCheckSuccessRate = TotalHealthChecks > 0 ?
                (double)SuccessfulHealthChecks / TotalHealthChecks * 100 : 0;

            return new Dictionary<string, object>
            {
                { "TotalConfiguredItems", TotalConfiguredItems },
                { "TotalValidations", TotalValidations },
                { "SuccessfulValidations", SuccessfulValidations },
                { "ValidationSuccessRate", Math.Round(validationSuccessRate, 2) },
                { "TotalHealthChecks", TotalHealthChecks },
                { "SuccessfulHealthChecks", SuccessfulHealthChecks },
                { "HealthCheckSuccessRate", Math.Round(healthCheckSuccessRate, 2) },
                { "TotalErrors", TotalErrors },
                { "LastUpdatedAt", LastUpdatedAt }
            };
        }
    }
}