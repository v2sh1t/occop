using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Occop.Models.Monitoring;

namespace Occop.Services.Monitoring
{
    /// <summary>
    /// WMI监控服务
    /// 集成WMI事件监听器与基础进程监控器，提供统一的进程监控服务
    /// 支持进程树管理、事件去重、性能优化等高级功能
    /// </summary>
    public class WMIMonitoringService : IDisposable
    {
        #region 私有字段

        private readonly IProcessMonitor _processMonitor;
        private readonly WMIEventListener _wmiEventListener;
        private readonly WmiListenerConfig _wmiConfig;
        private readonly object _lockObject = new object();

        // 进程树管理
        private readonly ConcurrentDictionary<int, ProcessTreeNode> _processTree;
        private readonly ConcurrentDictionary<int, ProcessTreeNode> _rootProcesses;

        // 事件去重和缓存
        private readonly ConcurrentDictionary<string, DateTime> _recentEvents;
        private readonly Timer _maintenanceTimer;
        private readonly TimeSpan _eventDeduplicationWindow = TimeSpan.FromSeconds(2);

        // 统计和监控
        private WMIMonitoringStatistics _statistics;
        private volatile bool _isRunning;
        private volatile bool _disposed;

        #endregion

        #region 公共属性

        /// <summary>
        /// 获取是否正在运行
        /// </summary>
        public bool IsRunning => _isRunning && !_disposed;

        /// <summary>
        /// 获取WMI监听器统计信息
        /// </summary>
        public WmiListenerStatistics WmiStatistics => _wmiEventListener?.Statistics;

        /// <summary>
        /// 获取基础监控器统计信息
        /// </summary>
        public MonitoringStatistics BaseMonitoringStatistics => _processMonitor?.GetStatistics();

        /// <summary>
        /// 获取WMI监控服务统计信息
        /// </summary>
        public WMIMonitoringStatistics Statistics => _statistics;

        /// <summary>
        /// 获取进程树根节点数量
        /// </summary>
        public int ProcessTreeRootCount => _rootProcesses.Count;

        /// <summary>
        /// 获取进程树总节点数量
        /// </summary>
        public int ProcessTreeTotalCount => _processTree.Count;

        /// <summary>
        /// 获取AI工具进程数量
        /// </summary>
        public int AIToolProcessCount => _processTree.Values.Count(node => node.IsAIToolProcess);

        #endregion

        #region 事件定义

        /// <summary>
        /// WMI进程事件
        /// </summary>
        public event EventHandler<WMIProcessEventArgs> WmiProcessEvent;

        /// <summary>
        /// 进程树变化事件
        /// </summary>
        public event EventHandler<ProcessTreeChangedEventArgs> ProcessTreeChanged;

        /// <summary>
        /// AI工具进程检测事件
        /// </summary>
        public event EventHandler<AIToolProcessEventArgs> AIToolProcessDetected;

        /// <summary>
        /// 监控错误事件
        /// </summary>
        public event EventHandler<MonitoringErrorEventArgs> MonitoringError;

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="processMonitor">基础进程监控器</param>
        /// <param name="wmiConfig">WMI监听配置</param>
        public WMIMonitoringService(IProcessMonitor processMonitor, WmiListenerConfig wmiConfig = null)
        {
            _processMonitor = processMonitor ?? throw new ArgumentNullException(nameof(processMonitor));
            _wmiConfig = wmiConfig ?? new WmiListenerConfig();

            _wmiEventListener = new WMIEventListener(_wmiConfig);
            _processTree = new ConcurrentDictionary<int, ProcessTreeNode>();
            _rootProcesses = new ConcurrentDictionary<int, ProcessTreeNode>();
            _recentEvents = new ConcurrentDictionary<string, DateTime>();
            _statistics = new WMIMonitoringStatistics();

            // 设置事件处理器
            SetupEventHandlers();

            // 启动维护定时器
            _maintenanceTimer = new Timer(PerformMaintenance, null,
                TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        #endregion

        #region 服务控制方法

        /// <summary>
        /// 启动WMI监控服务
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>启动结果</returns>
        public async Task<MonitoringResult> StartAsync(CancellationToken cancellationToken = default)
        {
            if (_isRunning)
                return MonitoringResult.Failure("WMI监控服务已在运行中");

            lock (_lockObject)
            {
                if (_isRunning)
                    return MonitoringResult.Failure("WMI监控服务已在运行中");

                _isRunning = true;
            }

            try
            {
                _statistics.StartTime = DateTime.UtcNow;

                // 启动基础进程监控器
                var baseResult = await _processMonitor.StartMonitoringAsync(cancellationToken);
                if (!baseResult.IsSuccess)
                {
                    _isRunning = false;
                    return MonitoringResult.Failure($"启动基础进程监控失败: {baseResult.Message}");
                }

                // 启动WMI事件监听器
                var wmiResult = await _wmiEventListener.StartListeningAsync(cancellationToken);
                if (!wmiResult.IsSuccess)
                {
                    await _processMonitor.StopMonitoringAsync(cancellationToken);
                    _isRunning = false;
                    return MonitoringResult.Failure($"启动WMI监听器失败: {wmiResult.Message}");
                }

                // 初始化进程树
                await InitializeProcessTreeAsync(cancellationToken);

                _statistics.IsRunning = true;
                return MonitoringResult.Success("WMI监控服务启动成功");
            }
            catch (Exception ex)
            {
                _isRunning = false;
                OnMonitoringError(new MonitoringErrorEventArgs(ex, "启动WMI监控服务"));
                return MonitoringResult.Failure($"启动WMI监控服务失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 停止WMI监控服务
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>停止结果</returns>
        public async Task<MonitoringResult> StopAsync(CancellationToken cancellationToken = default)
        {
            if (!_isRunning)
                return MonitoringResult.Success("WMI监控服务未在运行");

            try
            {
                _isRunning = false;

                // 停止WMI事件监听器
                var wmiResult = await _wmiEventListener.StopListeningAsync(cancellationToken);

                // 停止基础进程监控器
                var baseResult = await _processMonitor.StopMonitoringAsync(cancellationToken);

                _statistics.IsRunning = false;
                _statistics.TotalRunTime = DateTime.UtcNow - _statistics.StartTime;

                var combinedSuccess = wmiResult.IsSuccess && baseResult.IsSuccess;
                var message = combinedSuccess
                    ? "WMI监控服务停止成功"
                    : $"WMI监控服务停止完成，但有部分问题: WMI={wmiResult.Message}, Base={baseResult.Message}";

                return combinedSuccess
                    ? MonitoringResult.Success(message)
                    : MonitoringResult.Failure(message);
            }
            catch (Exception ex)
            {
                OnMonitoringError(new MonitoringErrorEventArgs(ex, "停止WMI监控服务"));
                return MonitoringResult.Failure($"停止WMI监控服务失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 重启WMI监控服务
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>重启结果</returns>
        public async Task<MonitoringResult> RestartAsync(CancellationToken cancellationToken = default)
        {
            var stopResult = await StopAsync(cancellationToken);
            if (!stopResult.IsSuccess)
                return stopResult;

            await Task.Delay(1000, cancellationToken); // 等待1秒确保资源完全释放

            return await StartAsync(cancellationToken);
        }

        #endregion

        #region 事件处理器设置

        /// <summary>
        /// 设置事件处理器
        /// </summary>
        private void SetupEventHandlers()
        {
            // WMI事件处理器
            _wmiEventListener.ProcessCreated += OnWmiProcessCreated;
            _wmiEventListener.ProcessDeleted += OnWmiProcessDeleted;
            _wmiEventListener.EventError += OnWmiEventError;

            // 基础监控事件处理器
            _processMonitor.ProcessStarted += OnBaseProcessStarted;
            _processMonitor.ProcessExited += OnBaseProcessExited;
            _processMonitor.ProcessKilled += OnBaseProcessKilled;
            _processMonitor.ErrorOccurred += OnBaseMonitoringError;
        }

        #endregion

        #region WMI事件处理

        /// <summary>
        /// 处理WMI进程创建事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void OnWmiProcessCreated(object sender, WmiProcessEventArgs e)
        {
            try
            {
                if (!ShouldProcessEvent(e, "ProcessCreated"))
                    return;

                _statistics.WmiProcessCreatedCount++;

                // 创建或更新进程树节点
                var processInfo = CreateProcessInfoFromWmiEvent(e);
                var treeNode = CreateOrUpdateProcessTreeNode(processInfo);

                // 检查是否为AI工具进程
                if (IsAIToolProcess(e.ProcessName))
                {
                    HandleAIToolProcessDetected(processInfo, treeNode);
                }

                // 触发事件
                var eventArgs = new WMIProcessEventArgs(e, processInfo, treeNode);
                OnWmiProcessEvent(eventArgs);
                OnProcessTreeChanged(new ProcessTreeChangedEventArgs(treeNode, ProcessTreeChangeType.NodeAdded));
            }
            catch (Exception ex)
            {
                OnMonitoringError(new MonitoringErrorEventArgs(ex, "处理WMI进程创建事件", e.ProcessId));
            }
        }

        /// <summary>
        /// 处理WMI进程删除事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void OnWmiProcessDeleted(object sender, WmiProcessEventArgs e)
        {
            try
            {
                if (!ShouldProcessEvent(e, "ProcessDeleted"))
                    return;

                _statistics.WmiProcessDeletedCount++;

                // 更新进程树
                if (_processTree.TryGetValue(e.ProcessId, out var treeNode))
                {
                    treeNode.MarkAsExited();
                    HandleProcessExitInTree(treeNode);

                    // 触发事件
                    var processInfo = treeNode.ProcessInfo;
                    var eventArgs = new WMIProcessEventArgs(e, processInfo, treeNode);
                    OnWmiProcessEvent(eventArgs);
                    OnProcessTreeChanged(new ProcessTreeChangedEventArgs(treeNode, ProcessTreeChangeType.NodeExited));
                }
            }
            catch (Exception ex)
            {
                OnMonitoringError(new MonitoringErrorEventArgs(ex, "处理WMI进程删除事件", e.ProcessId));
            }
        }

        /// <summary>
        /// 处理WMI事件错误
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">错误事件参数</param>
        private void OnWmiEventError(object sender, WmiEventErrorArgs e)
        {
            _statistics.WmiErrorCount++;
            OnMonitoringError(new MonitoringErrorEventArgs(e.Exception, $"WMI事件错误: {e.Context}"));
        }

        #endregion

        #region 基础监控事件处理

        /// <summary>
        /// 处理基础监控进程启动事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void OnBaseProcessStarted(object sender, ProcessMonitoringEventArgs e)
        {
            try
            {
                _statistics.BaseProcessStartedCount++;

                // 创建或更新进程树节点
                var treeNode = CreateOrUpdateProcessTreeNode(e.ProcessInfo);

                // 与WMI事件进行关联
                CorrelateWithWmiEvents(e.ProcessInfo, treeNode);
            }
            catch (Exception ex)
            {
                OnMonitoringError(new MonitoringErrorEventArgs(ex, "处理基础监控进程启动事件", e.ProcessInfo.ProcessId));
            }
        }

        /// <summary>
        /// 处理基础监控进程退出事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void OnBaseProcessExited(object sender, ProcessMonitoringEventArgs e)
        {
            try
            {
                _statistics.BaseProcessExitedCount++;

                if (_processTree.TryGetValue(e.ProcessInfo.ProcessId, out var treeNode))
                {
                    treeNode.MarkAsExited();
                    HandleProcessExitInTree(treeNode);
                }
            }
            catch (Exception ex)
            {
                OnMonitoringError(new MonitoringErrorEventArgs(ex, "处理基础监控进程退出事件", e.ProcessInfo.ProcessId));
            }
        }

        /// <summary>
        /// 处理基础监控进程被终止事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void OnBaseProcessKilled(object sender, ProcessMonitoringEventArgs e)
        {
            try
            {
                _statistics.BaseProcessKilledCount++;

                if (_processTree.TryGetValue(e.ProcessInfo.ProcessId, out var treeNode))
                {
                    treeNode.MarkAsExited();
                    treeNode.AddTag("KILLED");
                    HandleProcessExitInTree(treeNode);
                }
            }
            catch (Exception ex)
            {
                OnMonitoringError(new MonitoringErrorEventArgs(ex, "处理基础监控进程被终止事件", e.ProcessInfo.ProcessId));
            }
        }

        /// <summary>
        /// 处理基础监控错误事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">错误事件参数</param>
        private void OnBaseMonitoringError(object sender, MonitoringErrorEventArgs e)
        {
            _statistics.BaseMonitoringErrorCount++;
            OnMonitoringError(e);
        }

        #endregion

        #region 进程树管理

        /// <summary>
        /// 初始化进程树
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>任务</returns>
        private async Task InitializeProcessTreeAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Run(() =>
                {
                    // 获取当前监控的进程
                    var monitoredProcesses = _processMonitor.MonitoredProcesses;

                    foreach (var processInfo in monitoredProcesses)
                    {
                        CreateOrUpdateProcessTreeNode(processInfo);
                    }

                    BuildProcessHierarchy();
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                OnMonitoringError(new MonitoringErrorEventArgs(ex, "初始化进程树"));
            }
        }

        /// <summary>
        /// 创建或更新进程树节点
        /// </summary>
        /// <param name="processInfo">进程信息</param>
        /// <returns>进程树节点</returns>
        private ProcessTreeNode CreateOrUpdateProcessTreeNode(ProcessInfo processInfo)
        {
            var treeNode = _processTree.GetOrAdd(processInfo.ProcessId, pid => new ProcessTreeNode(processInfo));

            // 更新现有节点的进程信息
            if (treeNode.ProcessInfo != processInfo)
            {
                treeNode.UpdateProcessInfo(processInfo);
            }

            // 如果有父进程ID，尝试建立父子关系
            if (processInfo.ParentProcessId.HasValue && processInfo.ParentProcessId.Value != 0)
            {
                EstablishParentChildRelationship(treeNode, processInfo.ParentProcessId.Value);
            }
            else
            {
                // 没有父进程，添加到根节点集合
                _rootProcesses.TryAdd(processInfo.ProcessId, treeNode);
            }

            return treeNode;
        }

        /// <summary>
        /// 建立父子进程关系
        /// </summary>
        /// <param name="childNode">子节点</param>
        /// <param name="parentProcessId">父进程ID</param>
        private void EstablishParentChildRelationship(ProcessTreeNode childNode, int parentProcessId)
        {
            if (_processTree.TryGetValue(parentProcessId, out var parentNode))
            {
                if (childNode.Parent != parentNode)
                {
                    parentNode.AddChild(childNode);
                    _rootProcesses.TryRemove(childNode.ProcessInfo.ProcessId, out _);
                }
            }
        }

        /// <summary>
        /// 构建进程层次结构
        /// </summary>
        private void BuildProcessHierarchy()
        {
            // 多轮建立父子关系，因为进程可能以任意顺序被检测到
            for (int round = 0; round < 3; round++)
            {
                foreach (var node in _processTree.Values)
                {
                    if (node.IsRoot && node.ProcessInfo.ParentProcessId.HasValue)
                    {
                        EstablishParentChildRelationship(node, node.ProcessInfo.ParentProcessId.Value);
                    }
                }
            }
        }

        /// <summary>
        /// 处理进程树中的进程退出
        /// </summary>
        /// <param name="exitedNode">退出的节点</param>
        private void HandleProcessExitInTree(ProcessTreeNode exitedNode)
        {
            // 处理孤儿子进程
            foreach (var child in exitedNode.Children.ToList())
            {
                child.MarkAsOrphaned();
                _rootProcesses.TryAdd(child.ProcessInfo.ProcessId, child);
            }

            // 从父节点移除
            exitedNode.Parent?.RemoveChild(exitedNode);

            // 如果是根节点，从根节点集合移除
            _rootProcesses.TryRemove(exitedNode.ProcessInfo.ProcessId, out _);

            // 清理已退出的节点（延迟清理，避免立即删除）
            Task.Delay(TimeSpan.FromMinutes(5)).ContinueWith(_ =>
            {
                if (exitedNode.State == ProcessTreeNodeState.Exited)
                {
                    _processTree.TryRemove(exitedNode.ProcessInfo.ProcessId, out _);
                }
            });
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 检查是否应该处理事件（去重）
        /// </summary>
        /// <param name="e">WMI事件参数</param>
        /// <param name="eventType">事件类型</param>
        /// <returns>是否应该处理</returns>
        private bool ShouldProcessEvent(WmiProcessEventArgs e, string eventType)
        {
            var eventKey = $"{eventType}_{e.ProcessId}_{e.Timestamp:yyyyMMddHHmmss}";
            var now = DateTime.UtcNow;

            // 检查最近是否有相同的事件
            if (_recentEvents.TryGetValue(eventKey, out var lastEventTime))
            {
                if (now - lastEventTime < _eventDeduplicationWindow)
                {
                    return false; // 重复事件，跳过
                }
            }

            _recentEvents.TryAdd(eventKey, now);
            return true;
        }

        /// <summary>
        /// 从WMI事件创建进程信息
        /// </summary>
        /// <param name="e">WMI事件参数</param>
        /// <returns>进程信息</returns>
        private ProcessInfo CreateProcessInfoFromWmiEvent(WmiProcessEventArgs e)
        {
            var processInfo = new ProcessInfo(e.ProcessId, e.ProcessName)
            {
                ParentProcessId = e.ParentProcessId,
                FullPath = e.ProcessPath
            };

            // 如果是AI工具进程，标记它
            if (IsAIToolProcess(e.ProcessName))
            {
                processInfo.MarkAsAITool(DetermineAIToolType(e.ProcessName));
            }

            return processInfo;
        }

        /// <summary>
        /// 检查是否为AI工具进程
        /// </summary>
        /// <param name="processName">进程名称</param>
        /// <returns>是否为AI工具进程</returns>
        private bool IsAIToolProcess(string processName)
        {
            if (string.IsNullOrEmpty(processName))
                return false;

            var aiToolNames = new[] { "claude", "copilot", "openai", "anthropic" };
            return aiToolNames.Any(name => processName.Contains(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 确定AI工具类型
        /// </summary>
        /// <param name="processName">进程名称</param>
        /// <returns>AI工具类型</returns>
        private AIToolType DetermineAIToolType(string processName)
        {
            if (string.IsNullOrEmpty(processName))
                return AIToolType.Unknown;

            if (processName.Contains("claude", StringComparison.OrdinalIgnoreCase))
                return AIToolType.ClaudeCode;
            if (processName.Contains("copilot", StringComparison.OrdinalIgnoreCase))
                return AIToolType.GitHubCopilot;
            if (processName.Contains("openai", StringComparison.OrdinalIgnoreCase))
                return AIToolType.OpenAICodex;

            return AIToolType.Other;
        }

        /// <summary>
        /// 处理AI工具进程检测
        /// </summary>
        /// <param name="processInfo">进程信息</param>
        /// <param name="treeNode">进程树节点</param>
        private void HandleAIToolProcessDetected(ProcessInfo processInfo, ProcessTreeNode treeNode)
        {
            _statistics.AIToolProcessDetectedCount++;
            treeNode.AddTag("AI_TOOL_DETECTED");

            var eventArgs = new AIToolProcessEventArgs(processInfo, treeNode);
            OnAIToolProcessDetected(eventArgs);
        }

        /// <summary>
        /// 关联WMI事件与基础监控
        /// </summary>
        /// <param name="processInfo">进程信息</param>
        /// <param name="treeNode">进程树节点</param>
        private void CorrelateWithWmiEvents(ProcessInfo processInfo, ProcessTreeNode treeNode)
        {
            // 这里可以添加WMI事件与基础监控事件的关联逻辑
            // 例如，验证进程信息的一致性，合并重复的进程信息等
        }

        /// <summary>
        /// 执行定期维护任务
        /// </summary>
        /// <param name="state">定时器状态</param>
        private void PerformMaintenance(object state)
        {
            try
            {
                // 清理过期的事件去重缓存
                CleanupEventDeduplicationCache();

                // 更新统计信息
                UpdateStatistics();

                // 清理僵尸进程节点
                CleanupZombieProcessNodes();
            }
            catch (Exception ex)
            {
                OnMonitoringError(new MonitoringErrorEventArgs(ex, "执行维护任务"));
            }
        }

        /// <summary>
        /// 清理事件去重缓存
        /// </summary>
        private void CleanupEventDeduplicationCache()
        {
            var cutoffTime = DateTime.UtcNow - _eventDeduplicationWindow.Multiply(2);
            var expiredKeys = _recentEvents
                .Where(kvp => kvp.Value < cutoffTime)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _recentEvents.TryRemove(key, out _);
            }
        }

        /// <summary>
        /// 更新统计信息
        /// </summary>
        private void UpdateStatistics()
        {
            _statistics.ProcessTreeRootCount = _rootProcesses.Count;
            _statistics.ProcessTreeTotalCount = _processTree.Count;
            _statistics.ActiveProcessCount = _processTree.Values.Count(node => node.IsProcessActive);

            if (_statistics.IsRunning)
            {
                _statistics.TotalRunTime = DateTime.UtcNow - _statistics.StartTime;
            }
        }

        /// <summary>
        /// 清理僵尸进程节点
        /// </summary>
        private void CleanupZombieProcessNodes()
        {
            var cutoffTime = DateTime.UtcNow - TimeSpan.FromHours(1);
            var zombieNodes = _processTree.Values
                .Where(node => node.State == ProcessTreeNodeState.Exited && node.LastUpdatedTime < cutoffTime)
                .ToList();

            foreach (var node in zombieNodes)
            {
                _processTree.TryRemove(node.ProcessInfo.ProcessId, out _);
            }
        }

        #endregion

        #region 事件触发方法

        /// <summary>
        /// 触发WMI进程事件
        /// </summary>
        /// <param name="e">事件参数</param>
        protected virtual void OnWmiProcessEvent(WMIProcessEventArgs e)
        {
            WmiProcessEvent?.Invoke(this, e);
        }

        /// <summary>
        /// 触发进程树变化事件
        /// </summary>
        /// <param name="e">事件参数</param>
        protected virtual void OnProcessTreeChanged(ProcessTreeChangedEventArgs e)
        {
            ProcessTreeChanged?.Invoke(this, e);
        }

        /// <summary>
        /// 触发AI工具进程检测事件
        /// </summary>
        /// <param name="e">事件参数</param>
        protected virtual void OnAIToolProcessDetected(AIToolProcessEventArgs e)
        {
            AIToolProcessDetected?.Invoke(this, e);
        }

        /// <summary>
        /// 触发监控错误事件
        /// </summary>
        /// <param name="e">事件参数</param>
        protected virtual void OnMonitoringError(MonitoringErrorEventArgs e)
        {
            MonitoringError?.Invoke(this, e);
        }

        #endregion

        #region 公共查询方法

        /// <summary>
        /// 获取进程树根节点
        /// </summary>
        /// <returns>根节点列表</returns>
        public IReadOnlyList<ProcessTreeNode> GetProcessTreeRoots()
        {
            return _rootProcesses.Values.ToList();
        }

        /// <summary>
        /// 获取所有进程树节点
        /// </summary>
        /// <returns>所有节点列表</returns>
        public IReadOnlyList<ProcessTreeNode> GetAllProcessTreeNodes()
        {
            return _processTree.Values.ToList();
        }

        /// <summary>
        /// 根据进程ID获取进程树节点
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <returns>进程树节点，未找到返回null</returns>
        public ProcessTreeNode GetProcessTreeNode(int processId)
        {
            _processTree.TryGetValue(processId, out var node);
            return node;
        }

        /// <summary>
        /// 获取所有AI工具进程节点
        /// </summary>
        /// <returns>AI工具进程节点列表</returns>
        public IReadOnlyList<ProcessTreeNode> GetAIToolProcessNodes()
        {
            return _processTree.Values.Where(node => node.IsAIToolProcess).ToList();
        }

        /// <summary>
        /// 生成进程树字符串表示
        /// </summary>
        /// <returns>进程树字符串</returns>
        public string GenerateProcessTreeString()
        {
            var result = "进程树:\n";
            foreach (var root in _rootProcesses.Values.OrderBy(n => n.ProcessInfo.ProcessId))
            {
                result += root.ToTreeString();
            }
            return result;
        }

        #endregion

        #region IDisposable实现

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
        /// <param name="disposing">是否正在释放</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                try
                {
                    StopAsync().Wait(TimeSpan.FromSeconds(10));
                }
                catch { }

                _maintenanceTimer?.Dispose();
                _wmiEventListener?.Dispose();
            }

            _disposed = true;
        }

        #endregion
    }

    #region 相关事件参数类和枚举

    /// <summary>
    /// WMI进程事件参数
    /// </summary>
    public class WMIProcessEventArgs : EventArgs
    {
        public WmiProcessEventArgs WmiEventArgs { get; }
        public ProcessInfo ProcessInfo { get; }
        public ProcessTreeNode TreeNode { get; }
        public DateTime Timestamp { get; }

        public WMIProcessEventArgs(WmiProcessEventArgs wmiEventArgs, ProcessInfo processInfo, ProcessTreeNode treeNode)
        {
            WmiEventArgs = wmiEventArgs;
            ProcessInfo = processInfo;
            TreeNode = treeNode;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// 进程树变化事件参数
    /// </summary>
    public class ProcessTreeChangedEventArgs : EventArgs
    {
        public ProcessTreeNode Node { get; }
        public ProcessTreeChangeType ChangeType { get; }
        public DateTime Timestamp { get; }

        public ProcessTreeChangedEventArgs(ProcessTreeNode node, ProcessTreeChangeType changeType)
        {
            Node = node;
            ChangeType = changeType;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// AI工具进程事件参数
    /// </summary>
    public class AIToolProcessEventArgs : EventArgs
    {
        public ProcessInfo ProcessInfo { get; }
        public ProcessTreeNode TreeNode { get; }
        public DateTime Timestamp { get; }

        public AIToolProcessEventArgs(ProcessInfo processInfo, ProcessTreeNode treeNode)
        {
            ProcessInfo = processInfo;
            TreeNode = treeNode;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// 进程树变化类型
    /// </summary>
    public enum ProcessTreeChangeType
    {
        NodeAdded,
        NodeExited,
        NodeOrphaned,
        NodeUpdated,
        HierarchyChanged
    }

    /// <summary>
    /// WMI监控统计信息
    /// </summary>
    public class WMIMonitoringStatistics
    {
        public DateTime StartTime { get; set; }
        public TimeSpan TotalRunTime { get; set; }
        public bool IsRunning { get; set; }

        public int WmiProcessCreatedCount { get; set; }
        public int WmiProcessDeletedCount { get; set; }
        public int WmiErrorCount { get; set; }

        public int BaseProcessStartedCount { get; set; }
        public int BaseProcessExitedCount { get; set; }
        public int BaseProcessKilledCount { get; set; }
        public int BaseMonitoringErrorCount { get; set; }

        public int ProcessTreeRootCount { get; set; }
        public int ProcessTreeTotalCount { get; set; }
        public int ActiveProcessCount { get; set; }
        public int AIToolProcessDetectedCount { get; set; }

        public override string ToString()
        {
            return $"WMI监控统计 - 运行时间: {TotalRunTime:hh\\:mm\\:ss}, " +
                   $"WMI事件: {WmiProcessCreatedCount + WmiProcessDeletedCount}, " +
                   $"进程树节点: {ProcessTreeTotalCount}, " +
                   $"AI工具进程: {AIToolProcessDetectedCount}";
        }
    }

    #endregion
}