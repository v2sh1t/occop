using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Occop.Models.Monitoring
{
    /// <summary>
    /// 进程监控信息模型
    /// 封装被监控进程的基本信息、状态和元数据
    /// </summary>
    public class ProcessInfo
    {
        #region 基本属性

        /// <summary>
        /// 进程ID
        /// </summary>
        public int ProcessId { get; set; }

        /// <summary>
        /// 进程名称
        /// </summary>
        public string ProcessName { get; set; }

        /// <summary>
        /// 进程完整路径
        /// </summary>
        public string FullPath { get; set; }

        /// <summary>
        /// 进程启动时间
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// 进程当前状态
        /// </summary>
        public ProcessState State { get; set; }

        /// <summary>
        /// 父进程ID（如果有）
        /// </summary>
        public int? ParentProcessId { get; set; }

        /// <summary>
        /// 进程优先级
        /// </summary>
        public ProcessPriorityClass? Priority { get; set; }

        #endregion

        #region 监控元数据

        /// <summary>
        /// 添加到监控的时间
        /// </summary>
        public DateTime MonitoringStartTime { get; set; }

        /// <summary>
        /// 最后一次状态更新时间
        /// </summary>
        public DateTime LastUpdateTime { get; set; }

        /// <summary>
        /// 监控标签（用于分类和识别）
        /// </summary>
        public HashSet<string> Tags { get; set; }

        /// <summary>
        /// 自定义元数据
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; }

        /// <summary>
        /// 是否是AI工具进程
        /// </summary>
        public bool IsAIToolProcess { get; set; }

        /// <summary>
        /// AI工具类型（如果是AI工具）
        /// </summary>
        public AIToolType? AIToolType { get; set; }

        #endregion

        #region 性能信息

        /// <summary>
        /// 工作集内存大小（字节）
        /// </summary>
        public long WorkingSetSize { get; set; }

        /// <summary>
        /// 虚拟内存大小（字节）
        /// </summary>
        public long VirtualMemorySize { get; set; }

        /// <summary>
        /// 私有内存大小（字节）
        /// </summary>
        public long PrivateMemorySize { get; set; }

        /// <summary>
        /// CPU使用时间（总计）
        /// </summary>
        public TimeSpan TotalProcessorTime { get; set; }

        /// <summary>
        /// 用户模式CPU时间
        /// </summary>
        public TimeSpan UserProcessorTime { get; set; }

        /// <summary>
        /// 线程数量
        /// </summary>
        public int ThreadCount { get; set; }

        /// <summary>
        /// 句柄数量
        /// </summary>
        public int HandleCount { get; set; }

        #endregion

        #region 状态跟踪

        /// <summary>
        /// 进程是否已退出
        /// </summary>
        public bool HasExited { get; set; }

        /// <summary>
        /// 进程退出时间（如果已退出）
        /// </summary>
        public DateTime? ExitTime { get; set; }

        /// <summary>
        /// 进程退出代码（如果已退出）
        /// </summary>
        public int? ExitCode { get; set; }

        /// <summary>
        /// 是否异常退出
        /// </summary>
        public bool IsAbnormalExit { get; set; }

        /// <summary>
        /// 异常退出原因
        /// </summary>
        public string ExitReason { get; set; }

        #endregion

        #region 子进程管理

        /// <summary>
        /// 子进程ID列表
        /// </summary>
        public List<int> ChildProcessIds { get; set; }

        /// <summary>
        /// 是否监控子进程
        /// </summary>
        public bool MonitorChildProcesses { get; set; }

        /// <summary>
        /// 进程树深度
        /// </summary>
        public int ProcessTreeDepth { get; set; }

        #endregion

        #region 构造函数

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public ProcessInfo()
        {
            Tags = new HashSet<string>();
            Metadata = new Dictionary<string, object>();
            ChildProcessIds = new List<int>();
            State = ProcessState.Unknown;
            MonitoringStartTime = DateTime.UtcNow;
            LastUpdateTime = DateTime.UtcNow;
        }

        /// <summary>
        /// 基于进程ID构造
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="processName">进程名称</param>
        public ProcessInfo(int processId, string processName) : this()
        {
            ProcessId = processId;
            ProcessName = processName;
        }

        /// <summary>
        /// 基于系统Process对象构造
        /// </summary>
        /// <param name="process">系统进程对象</param>
        public ProcessInfo(Process process) : this()
        {
            if (process == null)
                throw new ArgumentNullException(nameof(process));

            UpdateFromProcess(process);
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 从系统Process对象更新信息
        /// </summary>
        /// <param name="process">系统进程对象</param>
        public void UpdateFromProcess(Process process)
        {
            if (process == null)
                throw new ArgumentNullException(nameof(process));

            try
            {
                ProcessId = process.Id;
                ProcessName = process.ProcessName;
                HasExited = process.HasExited;

                if (!HasExited)
                {
                    State = ProcessState.Running;

                    // 安全获取进程信息，某些属性可能在进程退出后无法访问
                    try { StartTime = process.StartTime; } catch { }
                    try { FullPath = process.MainModule?.FileName; } catch { }
                    try { Priority = process.PriorityClass; } catch { }
                    try { WorkingSetSize = process.WorkingSet64; } catch { }
                    try { VirtualMemorySize = process.VirtualMemorySize64; } catch { }
                    try { PrivateMemorySize = process.PrivateMemorySize64; } catch { }
                    try { TotalProcessorTime = process.TotalProcessorTime; } catch { }
                    try { UserProcessorTime = process.UserProcessorTime; } catch { }
                    try { ThreadCount = process.Threads.Count; } catch { }
                    try { HandleCount = process.HandleCount; } catch { }
                }
                else
                {
                    State = ProcessState.Exited;
                    try
                    {
                        ExitTime = process.ExitTime;
                        ExitCode = process.ExitCode;
                    }
                    catch { }
                }

                LastUpdateTime = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                // 进程可能在访问过程中退出，记录状态但不抛出异常
                State = ProcessState.Error;
                ExitReason = $"更新进程信息时出错: {ex.Message}";
                LastUpdateTime = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// 标记进程为AI工具
        /// </summary>
        /// <param name="aiToolType">AI工具类型</param>
        public void MarkAsAITool(AIToolType aiToolType)
        {
            IsAIToolProcess = true;
            AIToolType = aiToolType;
            Tags.Add("AI_TOOL");
            Tags.Add(aiToolType.ToString());
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
        /// 移除标签
        /// </summary>
        /// <param name="tag">标签</param>
        public void RemoveTag(string tag)
        {
            if (!string.IsNullOrWhiteSpace(tag))
            {
                Tags.Remove(tag.ToUpperInvariant());
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
        /// 设置元数据
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        public void SetMetadata(string key, object value)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                Metadata[key] = value;
            }
        }

        /// <summary>
        /// 获取元数据
        /// </summary>
        /// <typeparam name="T">返回类型</typeparam>
        /// <param name="key">键</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>元数据值</returns>
        public T GetMetadata<T>(string key, T defaultValue = default)
        {
            if (string.IsNullOrWhiteSpace(key) || !Metadata.ContainsKey(key))
                return defaultValue;

            try
            {
                return (T)Metadata[key];
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// 添加子进程
        /// </summary>
        /// <param name="childProcessId">子进程ID</param>
        public void AddChildProcess(int childProcessId)
        {
            if (!ChildProcessIds.Contains(childProcessId))
            {
                ChildProcessIds.Add(childProcessId);
            }
        }

        /// <summary>
        /// 移除子进程
        /// </summary>
        /// <param name="childProcessId">子进程ID</param>
        public void RemoveChildProcess(int childProcessId)
        {
            ChildProcessIds.Remove(childProcessId);
        }

        /// <summary>
        /// 计算进程运行时长
        /// </summary>
        /// <returns>运行时长</returns>
        public TimeSpan GetRunningDuration()
        {
            var endTime = HasExited ? (ExitTime ?? DateTime.UtcNow) : DateTime.UtcNow;
            return endTime - StartTime;
        }

        /// <summary>
        /// 计算监控时长
        /// </summary>
        /// <returns>监控时长</returns>
        public TimeSpan GetMonitoringDuration()
        {
            return DateTime.UtcNow - MonitoringStartTime;
        }

        /// <summary>
        /// 获取进程状态描述
        /// </summary>
        /// <returns>状态描述</returns>
        public string GetStateDescription()
        {
            return State switch
            {
                ProcessState.Unknown => "未知状态",
                ProcessState.Starting => "启动中",
                ProcessState.Running => "运行中",
                ProcessState.Suspended => "已暂停",
                ProcessState.Exiting => "退出中",
                ProcessState.Exited => HasExited ? $"已退出 (代码: {ExitCode})" : "已退出",
                ProcessState.Error => $"错误: {ExitReason}",
                _ => State.ToString()
            };
        }

        #endregion

        #region 重写方法

        /// <summary>
        /// 获取对象字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString()
        {
            var status = GetStateDescription();
            var duration = GetRunningDuration();
            return $"[{ProcessId}] {ProcessName} - {status} (运行: {duration:hh\\:mm\\:ss})";
        }

        /// <summary>
        /// 计算哈希码
        /// </summary>
        /// <returns>哈希码</returns>
        public override int GetHashCode()
        {
            return ProcessId.GetHashCode();
        }

        /// <summary>
        /// 比较对象相等性
        /// </summary>
        /// <param name="obj">比较对象</param>
        /// <returns>是否相等</returns>
        public override bool Equals(object obj)
        {
            return obj is ProcessInfo other && ProcessId == other.ProcessId;
        }

        #endregion
    }
}