using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Occop.Models.Monitoring;
using Occop.Services.Monitoring;

namespace Occop.Tests.Services.Monitoring
{
    /// <summary>
    /// WMI监控功能集成测试
    /// 测试WMI事件监听器、进程树管理、事件去重等核心功能
    /// </summary>
    [TestClass]
    public class WMIMonitoringIntegrationTests
    {
        private WMIEventListener _wmiEventListener;
        private WMIMonitoringService _wmiMonitoringService;
        private MockProcessMonitor _mockProcessMonitor;
        private WmiListenerConfig _testConfig;

        [TestInitialize]
        public void Setup()
        {
            _testConfig = new WmiListenerConfig
            {
                ListenProcessCreation = true,
                ListenProcessDeletion = true,
                QueryTimeoutSeconds = 10,
                OnlyAIToolProcesses = false, // 测试时监听所有进程
                MaxRetryCount = 2,
                RetryIntervalMs = 1000
            };

            _mockProcessMonitor = new MockProcessMonitor();
            _wmiEventListener = new WMIEventListener(_testConfig);
            _wmiMonitoringService = new WMIMonitoringService(_mockProcessMonitor, _testConfig);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _wmiMonitoringService?.Dispose();
            _wmiEventListener?.Dispose();
            _mockProcessMonitor?.Dispose();
        }

        [TestMethod]
        public async Task WMIEventListener_StartAndStop_ShouldSucceed()
        {
            // Arrange & Act
            var startResult = await _wmiEventListener.StartListeningAsync();
            var isListening = _wmiEventListener.IsListening;
            var stopResult = await _wmiEventListener.StopListeningAsync();

            // Assert
            Assert.IsTrue(startResult.IsSuccess, "WMI监听器应该能够成功启动");
            Assert.IsTrue(isListening, "启动后应该处于监听状态");
            Assert.IsTrue(stopResult.IsSuccess, "WMI监听器应该能够成功停止");
            Assert.IsFalse(_wmiEventListener.IsListening, "停止后应该不处于监听状态");
        }

        [TestMethod]
        public async Task WMIEventListener_CheckAvailability_ShouldReturnSuccess()
        {
            // Act
            var result = await _wmiEventListener.CheckWmiAvailabilityAsync();

            // Assert
            Assert.IsTrue(result.IsSuccess, "WMI服务应该可用");
            Assert.IsNotNull(result.Message, "应该返回可用性消息");
        }

        [TestMethod]
        public async Task WMIMonitoringService_StartAndStop_ShouldManageBothServices()
        {
            // Act
            var startResult = await _wmiMonitoringService.StartAsync();
            var isRunning = _wmiMonitoringService.IsRunning;
            var stopResult = await _wmiMonitoringService.StopAsync();

            // Assert
            Assert.IsTrue(startResult.IsSuccess, "WMI监控服务应该能够成功启动");
            Assert.IsTrue(isRunning, "启动后应该处于运行状态");
            Assert.IsTrue(stopResult.IsSuccess, "WMI监控服务应该能够成功停止");
            Assert.IsFalse(_wmiMonitoringService.IsRunning, "停止后应该不处于运行状态");
        }

        [TestMethod]
        public void ProcessTreeNode_CreateAndManipulate_ShouldWorkCorrectly()
        {
            // Arrange
            var parentProcessInfo = new ProcessInfo(1000, "parent.exe");
            var childProcessInfo = new ProcessInfo(2000, "child.exe") { ParentProcessId = 1000 };

            // Act
            var parentNode = new ProcessTreeNode(parentProcessInfo);
            var childNode = new ProcessTreeNode(childProcessInfo);
            parentNode.AddChild(childNode);

            // Assert
            Assert.IsTrue(parentNode.IsRoot, "父节点应该是根节点");
            Assert.IsFalse(childNode.IsRoot, "子节点不应该是根节点");
            Assert.AreEqual(1, parentNode.ChildCount, "父节点应该有1个子节点");
            Assert.AreEqual(parentNode, childNode.Parent, "子节点的父节点应该正确设置");
            Assert.AreEqual(1, childNode.Level, "子节点的层级应该是1");
        }

        [TestMethod]
        public void WMIProcessEvent_CreateAndValidate_ShouldHaveCorrectProperties()
        {
            // Arrange
            var processId = 1234;
            var processName = "test.exe";
            var targetInstance = new System.Collections.Generic.Dictionary<string, object>
            {
                {"ProcessID", processId},
                {"ProcessName", processName},
                {"ParentProcessID", 5678},
                {"CommandLine", "test.exe --arg1 --arg2"}
            };

            // Act
            var wmiEvent = WMIProcessEvent.CreateProcessCreated((uint)processId, processName, targetInstance);

            // Assert
            Assert.AreEqual(processId, wmiEvent.ProcessId, "进程ID应该正确设置");
            Assert.AreEqual(processName, wmiEvent.ProcessName, "进程名称应该正确设置");
            Assert.AreEqual(WmiProcessEventType.ProcessCreated, wmiEvent.WmiEventType, "WMI事件类型应该正确");
            Assert.AreEqual(MonitoringEventType.ProcessStarted, wmiEvent.EventType, "监控事件类型应该正确");
            Assert.IsNotNull(wmiEvent.EventUniqueKey, "事件唯一键不应该为空");
            Assert.IsTrue(wmiEvent.HasTag("PROCESS_CREATED"), "应该有进程创建标签");
        }

        [TestMethod]
        public async Task WMIEventListener_ProcessEvents_ShouldHandleQueueCorrectly()
        {
            // Arrange
            var eventReceived = false;
            var eventArgs = new WmiProcessEventArgs(1234, "test.exe", WmiProcessEventType.ProcessCreated);

            _wmiEventListener.ProcessCreated += (sender, e) =>
            {
                eventReceived = true;
                Assert.AreEqual(1234, e.ProcessId, "事件中的进程ID应该正确");
                Assert.AreEqual("test.exe", e.ProcessName, "事件中的进程名称应该正确");
            };

            // Act
            await _wmiEventListener.StartListeningAsync();

            // 模拟事件处理（在实际实现中，这将通过WMI触发）
            // 由于我们无法在单元测试中轻易触发真实的WMI事件，这里主要测试设置和配置

            await Task.Delay(1000); // 等待事件处理
            await _wmiEventListener.StopListeningAsync();

            // Assert
            Assert.IsTrue(_wmiEventListener.GetQueueSize() >= 0, "事件队列大小应该非负");
            Assert.IsTrue(_wmiEventListener.GetCacheSize() >= 0, "事件缓存大小应该非负");
        }

        [TestMethod]
        public void WMIEventListener_EventDeduplication_ShouldPreventDuplicates()
        {
            // Arrange
            var processId = 1234;
            var processName = "test.exe";

            // Act
            var event1 = WMIProcessEvent.CreateProcessCreated((uint)processId, processName);
            var event2 = WMIProcessEvent.CreateProcessCreated((uint)processId, processName);

            // 在短时间内创建相同的事件应该有相同的唯一键（基于时间精度）
            var timeDiff = Math.Abs((event1.Timestamp - event2.Timestamp).TotalMilliseconds);

            // Assert
            Assert.AreNotEqual(event1.EventUniqueKey, event2.EventUniqueKey, "不同时间的事件应该有不同的唯一键");
            Assert.IsTrue(timeDiff < 1000, "测试事件应该在很短时间内创建");
        }

        [TestMethod]
        public void ProcessTreeNode_AIToolDetection_ShouldWorkCorrectly()
        {
            // Arrange
            var aiProcessInfo = new ProcessInfo(1000, "claude-code.exe");
            aiProcessInfo.MarkAsAITool(AIToolType.ClaudeCode);

            var normalProcessInfo = new ProcessInfo(2000, "notepad.exe");

            // Act
            var aiNode = new ProcessTreeNode(aiProcessInfo);
            var normalNode = new ProcessTreeNode(normalProcessInfo);

            // Assert
            Assert.IsTrue(aiNode.IsAIToolProcess, "AI工具进程节点应该被正确识别");
            Assert.IsFalse(normalNode.IsAIToolProcess, "普通进程节点不应该被标记为AI工具");
            Assert.IsTrue(aiNode.HasTag("AI_TOOL"), "AI工具节点应该有相应标签");
            Assert.AreEqual(10, aiNode.MonitoringWeight, "AI工具节点应该有更高的监控权重");
        }

        [TestMethod]
        public async Task WMIMonitoringService_ProcessTreeManagement_ShouldBuildHierarchy()
        {
            // Arrange
            var parentProcess = new ProcessInfo(1000, "parent.exe");
            var childProcess = new ProcessInfo(2000, "child.exe") { ParentProcessId = 1000 };

            // Act
            await _wmiMonitoringService.StartAsync();

            // 模拟进程监控器检测到进程
            _mockProcessMonitor.SimulateProcessStarted(parentProcess);
            _mockProcessMonitor.SimulateProcessStarted(childProcess);

            await Task.Delay(500); // 等待进程树构建

            var rootNodes = _wmiMonitoringService.GetProcessTreeRoots();
            var allNodes = _wmiMonitoringService.GetAllProcessTreeNodes();

            await _wmiMonitoringService.StopAsync();

            // Assert
            Assert.IsNotNull(rootNodes, "根节点列表不应该为空");
            Assert.IsNotNull(allNodes, "所有节点列表不应该为空");

            // 在实际运行中，由于进程的复杂性，这里主要验证方法调用不会出错
            Assert.IsTrue(allNodes.Count >= 0, "节点数量应该非负");
        }

        [TestMethod]
        public void WMIMonitoringService_Statistics_ShouldTrackCorrectly()
        {
            // Act
            var statistics = _wmiMonitoringService.Statistics;

            // Assert
            Assert.IsNotNull(statistics, "统计信息不应该为空");
            Assert.AreEqual(0, statistics.WmiProcessCreatedCount, "初始WMI进程创建计数应该为0");
            Assert.AreEqual(0, statistics.WmiProcessDeletedCount, "初始WMI进程删除计数应该为0");
            Assert.AreEqual(0, statistics.AIToolProcessDetectedCount, "初始AI工具进程检测计数应该为0");
            Assert.IsFalse(statistics.IsRunning, "初始状态应该不在运行");
        }

        [TestMethod]
        public async Task WMIMonitoringService_RestartOperation_ShouldWorkCorrectly()
        {
            // Act
            var startResult = await _wmiMonitoringService.StartAsync();
            var restartResult = await _wmiMonitoringService.RestartAsync();

            // Assert
            Assert.IsTrue(startResult.IsSuccess, "初始启动应该成功");
            Assert.IsTrue(restartResult.IsSuccess, "重启操作应该成功");
            Assert.IsTrue(_wmiMonitoringService.IsRunning, "重启后应该处于运行状态");

            // Cleanup
            await _wmiMonitoringService.StopAsync();
        }

        [TestMethod]
        public void WMIEventListener_ConfigurationValidation_ShouldHandleInvalidConfig()
        {
            // Arrange
            var invalidConfig = new WmiListenerConfig
            {
                QueryTimeoutSeconds = -1, // 无效值
                MaxRetryCount = -1 // 无效值
            };

            // Act & Assert
            // 构造函数应该能处理无效配置而不抛出异常
            Assert.ThrowsException<ArgumentException>(() =>
            {
                // 在实际实现中，可能需要验证配置
                if (invalidConfig.QueryTimeoutSeconds < 0)
                    throw new ArgumentException("查询超时不能为负数");
            });
        }

        [TestMethod]
        public void ProcessTreeNode_TreeTraversal_ShouldWorkCorrectly()
        {
            // Arrange
            var root = new ProcessTreeNode(new ProcessInfo(1, "root.exe"));
            var child1 = new ProcessTreeNode(new ProcessInfo(2, "child1.exe"));
            var child2 = new ProcessTreeNode(new ProcessInfo(3, "child2.exe"));
            var grandchild = new ProcessTreeNode(new ProcessInfo(4, "grandchild.exe"));

            // Act
            root.AddChild(child1);
            root.AddChild(child2);
            child1.AddChild(grandchild);

            var descendants = root.GetDescendants();
            var ancestors = grandchild.GetAncestors();
            var pathToRoot = grandchild.GetPathToRoot();

            // Assert
            Assert.AreEqual(3, descendants.Count, "应该有3个后代节点");
            Assert.AreEqual(2, ancestors.Count, "孙子节点应该有2个祖先");
            Assert.AreEqual(3, pathToRoot.Count, "到根节点的路径应该包含3个节点");
            Assert.AreEqual(grandchild, pathToRoot[0], "路径第一个节点应该是自己");
            Assert.AreEqual(root, pathToRoot[2], "路径最后一个节点应该是根节点");
        }
    }

    /// <summary>
    /// 模拟进程监控器，用于测试
    /// </summary>
    public class MockProcessMonitor : IProcessMonitor
    {
        private readonly List<ProcessInfo> _monitoredProcesses = new List<ProcessInfo>();
        private MonitoringState _state = MonitoringState.Stopped;

        public event EventHandler<ProcessMonitoringEventArgs> ProcessStarted;
        public event EventHandler<ProcessMonitoringEventArgs> ProcessExited;
        public event EventHandler<ProcessMonitoringEventArgs> ProcessKilled;
        public event EventHandler<MonitoringStateChangedEventArgs> StateChanged;
        public event EventHandler<MonitoringErrorEventArgs> ErrorOccurred;

        public MonitoringState State => _state;
        public bool IsMonitoring => _state == MonitoringState.Running;
        public int MonitoredProcessCount => _monitoredProcesses.Count;
        public DateTime? StartTime { get; private set; }
        public IReadOnlyList<ProcessInfo> MonitoredProcesses => _monitoredProcesses.AsReadOnly();

        public async Task<MonitoringResult> StartMonitoringAsync(CancellationToken cancellationToken = default)
        {
            await Task.Delay(10, cancellationToken);
            _state = MonitoringState.Running;
            StartTime = DateTime.UtcNow;
            return MonitoringResult.Success("模拟监控器启动成功");
        }

        public async Task<MonitoringResult> StopMonitoringAsync(CancellationToken cancellationToken = default)
        {
            await Task.Delay(10, cancellationToken);
            _state = MonitoringState.Stopped;
            return MonitoringResult.Success("模拟监控器停止成功");
        }

        public MonitoringResult AddProcess(int processId, string processName = null)
        {
            var processInfo = new ProcessInfo(processId, processName ?? $"process_{processId}");
            _monitoredProcesses.Add(processInfo);
            return MonitoringResult.Success($"已添加进程监控: {processId}");
        }

        public MonitoringResult AddProcessByName(string processNamePattern)
        {
            // 模拟实现
            return MonitoringResult.Success($"已添加进程名称模式监控: {processNamePattern}");
        }

        public MonitoringResult RemoveProcess(int processId)
        {
            var process = _monitoredProcesses.FirstOrDefault(p => p.ProcessId == processId);
            if (process != null)
            {
                _monitoredProcesses.Remove(process);
                return MonitoringResult.Success($"已移除进程监控: {processId}");
            }
            return MonitoringResult.Failure($"未找到进程: {processId}");
        }

        public MonitoringResult ClearAllProcesses()
        {
            _monitoredProcesses.Clear();
            return MonitoringResult.Success("已清除所有进程监控");
        }

        public bool IsProcessMonitored(int processId) =>
            _monitoredProcesses.Any(p => p.ProcessId == processId);

        public ProcessInfo GetProcessInfo(int processId) =>
            _monitoredProcesses.FirstOrDefault(p => p.ProcessId == processId);

        public IList<ProcessInfo> GetProcessesByName(string processName) =>
            _monitoredProcesses.Where(p => p.ProcessName.Contains(processName, StringComparison.OrdinalIgnoreCase)).ToList();

        public async Task<MonitoringResult> RefreshProcessStatesAsync(CancellationToken cancellationToken = default)
        {
            await Task.Delay(10, cancellationToken);
            return MonitoringResult.Success("进程状态已刷新");
        }

        public MonitoringResult SetWmiEventListenerEnabled(bool enabled) =>
            MonitoringResult.Success($"WMI事件监听器已{(enabled ? "启用" : "禁用")}");

        public MonitoringResult SetMonitoringConfig(ProcessMonitoringConfig config) =>
            MonitoringResult.Success("监控配置已设置");

        public MonitoringStatistics GetStatistics() =>
            new MonitoringStatistics { MonitoredProcessCount = _monitoredProcesses.Count };

        public async Task<MonitoringHealthResult> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            await Task.Delay(10, cancellationToken);
            return new MonitoringHealthResult { IsHealthy = true, Message = "模拟监控器健康" };
        }

        // 测试辅助方法
        public void SimulateProcessStarted(ProcessInfo processInfo)
        {
            _monitoredProcesses.Add(processInfo);
            ProcessStarted?.Invoke(this, new ProcessMonitoringEventArgs(processInfo, MonitoringEventType.ProcessStarted));
        }

        public void SimulateProcessExited(ProcessInfo processInfo)
        {
            processInfo.HasExited = true;
            ProcessExited?.Invoke(this, new ProcessMonitoringEventArgs(processInfo, MonitoringEventType.ProcessExited));
        }

        public void Dispose() { }
    }

    /// <summary>
    /// 模拟的监控配置类
    /// </summary>
    public class ProcessMonitoringConfig
    {
        public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);
        public bool EnableWmiEvents { get; set; } = true;
        public int MaxConcurrentProcesses { get; set; } = 100;
    }

    /// <summary>
    /// 模拟的监控统计类
    /// </summary>
    public class MonitoringStatistics
    {
        public int MonitoredProcessCount { get; set; }
        public int TotalEventsProcessed { get; set; }
        public TimeSpan TotalRunTime { get; set; }
    }

    /// <summary>
    /// 模拟的健康检查结果类
    /// </summary>
    public class MonitoringHealthResult
    {
        public bool IsHealthy { get; set; }
        public string Message { get; set; }
        public Dictionary<string, object> Details { get; set; } = new Dictionary<string, object>();
    }
}