using System;
using System.Collections.Generic;

namespace Occop.Models.Monitoring
{
    /// <summary>
    /// WMI进程事件模型
    /// 扩展基础MonitoringEvent，专门用于WMI事件监听器产生的进程事件
    /// </summary>
    public class WMIProcessEvent : MonitoringEvent
    {
        #region WMI特定属性

        /// <summary>
        /// WMI事件类型
        /// </summary>
        public WmiProcessEventType WmiEventType { get; set; }

        /// <summary>
        /// WMI查询语句
        /// </summary>
        public string WmiQuery { get; set; }

        /// <summary>
        /// WMI命名空间
        /// </summary>
        public string WmiNamespace { get; set; }

        /// <summary>
        /// WMI事件到达时间（原始时间戳）
        /// </summary>
        public DateTime WmiEventArrivalTime { get; set; }

        /// <summary>
        /// 目标实例信息（WMI返回的实例数据）
        /// </summary>
        public Dictionary<string, object> TargetInstance { get; set; }

        /// <summary>
        /// 事件处理延迟（从WMI事件到达到处理完成的时间）
        /// </summary>
        public TimeSpan ProcessingDelay { get; set; }

        #endregion

        #region 进程详细信息

        /// <summary>
        /// 进程创建时间（从WMI获取）
        /// </summary>
        public DateTime? WmiProcessCreationDate { get; set; }

        /// <summary>
        /// 进程终止时间（从WMI获取）
        /// </summary>
        public DateTime? WmiProcessTerminationDate { get; set; }

        /// <summary>
        /// 进程会话ID
        /// </summary>
        public uint? SessionId { get; set; }

        /// <summary>
        /// 进程用户名
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// 进程域名
        /// </summary>
        public string Domain { get; set; }

        /// <summary>
        /// 进程完整命令行
        /// </summary>
        public string FullCommandLine { get; set; }

        /// <summary>
        /// 进程可执行文件路径
        /// </summary>
        public string ExecutablePath { get; set; }

        /// <summary>
        /// 进程优先级（从WMI获取）
        /// </summary>
        public uint? WmiPriority { get; set; }

        /// <summary>
        /// 页面文件使用量
        /// </summary>
        public ulong? PageFileUsage { get; set; }

        /// <summary>
        /// 峰值工作集大小
        /// </summary>
        public ulong? PeakWorkingSetSize { get; set; }

        /// <summary>
        /// 进程操作系统PID（额外确认）
        /// </summary>
        public uint? OSProcessId { get; set; }

        #endregion

        #region 父子进程关系

        /// <summary>
        /// 父进程创建时间（用于唯一标识父进程）
        /// </summary>
        public DateTime? ParentProcessCreationDate { get; set; }

        /// <summary>
        /// 子进程列表（在进程退出事件中有用）
        /// </summary>
        public List<uint> ChildProcessIds { get; set; }

        /// <summary>
        /// 进程树深度级别
        /// </summary>
        public int ProcessTreeLevel { get; set; }

        /// <summary>
        /// 是否为根进程（无父进程）
        /// </summary>
        public bool IsRootProcess => !ParentProcessId.HasValue || ParentProcessId == 0;

        #endregion

        #region 事件去重信息

        /// <summary>
        /// 事件唯一标识符（用于去重）
        /// 格式: {EventType}_{ProcessId}_{CreationTime}
        /// </summary>
        public string EventUniqueKey { get; set; }

        /// <summary>
        /// 重复事件计数
        /// </summary>
        public int DuplicateCount { get; set; }

        /// <summary>
        /// 首次检测到事件的时间
        /// </summary>
        public DateTime FirstDetectedTime { get; set; }

        /// <summary>
        /// 最后检测到重复事件的时间
        /// </summary>
        public DateTime? LastDuplicateTime { get; set; }

        #endregion

        #region 构造函数

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public WMIProcessEvent() : base()
        {
            TargetInstance = new Dictionary<string, object>();
            ChildProcessIds = new List<uint>();
            WmiEventArrivalTime = DateTime.UtcNow;
            FirstDetectedTime = DateTime.UtcNow;
            DuplicateCount = 1;
            Source = "WMIEventListener";
            Category = "WMI";
        }

        /// <summary>
        /// 基于WMI事件参数构造
        /// </summary>
        /// <param name="wmiEventArgs">WMI事件参数</param>
        public WMIProcessEvent(WmiProcessEventArgs wmiEventArgs) : this()
        {
            if (wmiEventArgs == null)
                throw new ArgumentNullException(nameof(wmiEventArgs));

            // 设置基础事件信息
            ProcessId = wmiEventArgs.ProcessId;
            ProcessName = wmiEventArgs.ProcessName;
            ParentProcessId = wmiEventArgs.ParentProcessId;
            FullCommandLine = wmiEventArgs.CommandLine;
            ExecutablePath = wmiEventArgs.ProcessPath;
            WmiEventType = wmiEventArgs.EventType;
            WmiEventArrivalTime = wmiEventArgs.Timestamp;

            // 生成事件唯一键
            EventUniqueKey = GenerateUniqueKey(wmiEventArgs.EventType, wmiEventArgs.ProcessId, wmiEventArgs.Timestamp);

            // 设置事件类型和标题
            EventType = ConvertWmiEventType(wmiEventArgs.EventType);
            Title = GenerateEventTitle(wmiEventArgs.EventType, wmiEventArgs.ProcessName, wmiEventArgs.ProcessId);
            Description = GenerateEventDescription(wmiEventArgs);

            // 设置标签
            AddTag("WMI_EVENT");
            AddTag(wmiEventArgs.EventType.ToString().ToUpperInvariant());

            // 计算处理延迟
            ProcessingDelay = DateTime.UtcNow - wmiEventArgs.Timestamp;
        }

        #endregion

        #region 静态工厂方法

        /// <summary>
        /// 创建进程创建WMI事件
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="processName">进程名称</param>
        /// <param name="targetInstance">WMI目标实例数据</param>
        /// <returns>WMI进程事件</returns>
        public static WMIProcessEvent CreateProcessCreated(uint processId, string processName,
            Dictionary<string, object> targetInstance = null)
        {
            var evt = new WMIProcessEvent
            {
                ProcessId = (int)processId,
                OSProcessId = processId,
                ProcessName = processName,
                WmiEventType = WmiProcessEventType.ProcessCreated,
                EventType = MonitoringEventType.ProcessStarted,
                Title = $"WMI检测到进程创建: {processName} [{processId}]",
                Description = $"WMI事件监听器检测到新进程 {processName} (PID: {processId}) 被创建",
                Severity = EventSeverity.Information,
                TargetInstance = targetInstance ?? new Dictionary<string, object>()
            };

            evt.EventUniqueKey = evt.GenerateUniqueKey(WmiProcessEventType.ProcessCreated, (int)processId, evt.Timestamp);
            evt.ExtractWmiInstanceData();
            evt.AddTag("PROCESS_CREATED");

            return evt;
        }

        /// <summary>
        /// 创建进程删除WMI事件
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="processName">进程名称</param>
        /// <param name="targetInstance">WMI目标实例数据</param>
        /// <returns>WMI进程事件</returns>
        public static WMIProcessEvent CreateProcessDeleted(uint processId, string processName,
            Dictionary<string, object> targetInstance = null)
        {
            var evt = new WMIProcessEvent
            {
                ProcessId = (int)processId,
                OSProcessId = processId,
                ProcessName = processName,
                WmiEventType = WmiProcessEventType.ProcessDeleted,
                EventType = MonitoringEventType.ProcessExited,
                Title = $"WMI检测到进程删除: {processName} [{processId}]",
                Description = $"WMI事件监听器检测到进程 {processName} (PID: {processId}) 已终止",
                Severity = EventSeverity.Information,
                TargetInstance = targetInstance ?? new Dictionary<string, object>()
            };

            evt.EventUniqueKey = evt.GenerateUniqueKey(WmiProcessEventType.ProcessDeleted, (int)processId, evt.Timestamp);
            evt.ExtractWmiInstanceData();
            evt.AddTag("PROCESS_DELETED");

            return evt;
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 从WMI目标实例中提取数据
        /// </summary>
        private void ExtractWmiInstanceData()
        {
            if (TargetInstance == null || TargetInstance.Count == 0)
                return;

            try
            {
                // 提取常见的WMI进程属性
                if (TargetInstance.TryGetValue("CreationDate", out var creationDate) && creationDate != null)
                {
                    if (DateTime.TryParse(creationDate.ToString(), out var creationDateTime))
                        WmiProcessCreationDate = creationDateTime;
                }

                if (TargetInstance.TryGetValue("TerminationDate", out var terminationDate) && terminationDate != null)
                {
                    if (DateTime.TryParse(terminationDate.ToString(), out var terminationDateTime))
                        WmiProcessTerminationDate = terminationDateTime;
                }

                if (TargetInstance.TryGetValue("SessionId", out var sessionId) && sessionId != null)
                {
                    if (uint.TryParse(sessionId.ToString(), out var sessionIdValue))
                        SessionId = sessionIdValue;
                }

                if (TargetInstance.TryGetValue("ParentProcessId", out var parentPid) && parentPid != null)
                {
                    if (int.TryParse(parentPid.ToString(), out var parentPidValue))
                        ParentProcessId = parentPidValue;
                }

                if (TargetInstance.TryGetValue("CommandLine", out var cmdLine) && cmdLine != null)
                    FullCommandLine = cmdLine.ToString();

                if (TargetInstance.TryGetValue("ExecutablePath", out var exePath) && exePath != null)
                    ExecutablePath = exePath.ToString();

                if (TargetInstance.TryGetValue("Priority", out var priority) && priority != null)
                {
                    if (uint.TryParse(priority.ToString(), out var priorityValue))
                        WmiPriority = priorityValue;
                }

                if (TargetInstance.TryGetValue("PageFileUsage", out var pageFile) && pageFile != null)
                {
                    if (ulong.TryParse(pageFile.ToString(), out var pageFileValue))
                        PageFileUsage = pageFileValue;
                }

                if (TargetInstance.TryGetValue("PeakWorkingSetSize", out var peakWS) && peakWS != null)
                {
                    if (ulong.TryParse(peakWS.ToString(), out var peakWSValue))
                        PeakWorkingSetSize = peakWSValue;
                }

                // 将所有WMI数据添加到事件数据中
                foreach (var kvp in TargetInstance)
                {
                    AddData($"WMI_{kvp.Key}", kvp.Value);
                }
            }
            catch (Exception ex)
            {
                AddData("WMI_DATA_EXTRACTION_ERROR", ex.Message);
            }
        }

        /// <summary>
        /// 生成事件唯一键
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <param name="processId">进程ID</param>
        /// <param name="timestamp">时间戳</param>
        /// <returns>唯一键</returns>
        private string GenerateUniqueKey(WmiProcessEventType eventType, int processId, DateTime timestamp)
        {
            var timeKey = timestamp.ToString("yyyyMMddHHmmssffff");
            return $"{eventType}_{processId}_{timeKey}";
        }

        /// <summary>
        /// 转换WMI事件类型到监控事件类型
        /// </summary>
        /// <param name="wmiEventType">WMI事件类型</param>
        /// <returns>监控事件类型</returns>
        private static MonitoringEventType ConvertWmiEventType(WmiProcessEventType wmiEventType)
        {
            return wmiEventType switch
            {
                WmiProcessEventType.ProcessCreated => MonitoringEventType.ProcessStarted,
                WmiProcessEventType.ProcessDeleted => MonitoringEventType.ProcessExited,
                _ => MonitoringEventType.Information
            };
        }

        /// <summary>
        /// 生成事件标题
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <param name="processName">进程名称</param>
        /// <param name="processId">进程ID</param>
        /// <returns>事件标题</returns>
        private static string GenerateEventTitle(WmiProcessEventType eventType, string processName, int processId)
        {
            return eventType switch
            {
                WmiProcessEventType.ProcessCreated => $"WMI进程创建: {processName} [{processId}]",
                WmiProcessEventType.ProcessDeleted => $"WMI进程删除: {processName} [{processId}]",
                _ => $"WMI进程事件: {processName} [{processId}]"
            };
        }

        /// <summary>
        /// 生成事件描述
        /// </summary>
        /// <param name="wmiEventArgs">WMI事件参数</param>
        /// <returns>事件描述</returns>
        private static string GenerateEventDescription(WmiProcessEventArgs wmiEventArgs)
        {
            var parentInfo = wmiEventArgs.ParentProcessId.HasValue
                ? $"，父进程PID: {wmiEventArgs.ParentProcessId}"
                : "";

            var pathInfo = !string.IsNullOrEmpty(wmiEventArgs.ProcessPath)
                ? $"，路径: {wmiEventArgs.ProcessPath}"
                : "";

            return wmiEventArgs.EventType switch
            {
                WmiProcessEventType.ProcessCreated =>
                    $"WMI监听器检测到新进程创建 - 名称: {wmiEventArgs.ProcessName}, PID: {wmiEventArgs.ProcessId}{parentInfo}{pathInfo}",
                WmiProcessEventType.ProcessDeleted =>
                    $"WMI监听器检测到进程终止 - 名称: {wmiEventArgs.ProcessName}, PID: {wmiEventArgs.ProcessId}{parentInfo}",
                _ =>
                    $"WMI监听器检测到进程事件 - 名称: {wmiEventArgs.ProcessName}, PID: {wmiEventArgs.ProcessId}{parentInfo}"
            };
        }

        /// <summary>
        /// 标记为重复事件
        /// </summary>
        public void MarkAsDuplicate()
        {
            DuplicateCount++;
            LastDuplicateTime = DateTime.UtcNow;
            AddTag("DUPLICATE");
        }

        /// <summary>
        /// 检查是否为重复事件
        /// </summary>
        /// <returns>是否为重复事件</returns>
        public bool IsDuplicate()
        {
            return DuplicateCount > 1;
        }

        /// <summary>
        /// 设置进程树信息
        /// </summary>
        /// <param name="treeLevel">树深度级别</param>
        /// <param name="parentCreationDate">父进程创建时间</param>
        public void SetProcessTreeInfo(int treeLevel, DateTime? parentCreationDate = null)
        {
            ProcessTreeLevel = treeLevel;
            ParentProcessCreationDate = parentCreationDate;
            AddData("ProcessTreeLevel", treeLevel);

            if (IsRootProcess)
                AddTag("ROOT_PROCESS");
            else
                AddTag($"TREE_LEVEL_{treeLevel}");
        }

        /// <summary>
        /// 添加子进程
        /// </summary>
        /// <param name="childProcessId">子进程ID</param>
        public void AddChildProcess(uint childProcessId)
        {
            if (!ChildProcessIds.Contains(childProcessId))
            {
                ChildProcessIds.Add(childProcessId);
                AddData($"ChildProcess_{ChildProcessIds.Count}", childProcessId);
            }
        }

        /// <summary>
        /// 获取事件摘要（重写基类方法）
        /// </summary>
        /// <returns>摘要字符串</returns>
        public new string GetSummary()
        {
            var duplicateInfo = IsDuplicate() ? $" (重复x{DuplicateCount})" : "";
            var processInfo = ProcessId.HasValue ? $" [PID: {ProcessId}]" : "";
            var delayInfo = ProcessingDelay.TotalMilliseconds > 100 ? $" 延迟:{ProcessingDelay.TotalMilliseconds:F0}ms" : "";

            return $"[WMI-{Severity}] {Title}{processInfo}{duplicateInfo}{delayInfo} - {Timestamp:yyyy-MM-dd HH:mm:ss.fff}";
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

        #endregion
    }
}