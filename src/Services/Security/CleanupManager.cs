using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Occop.Core.Managers;
using Occop.Models.Security;
using Occop.Services.Monitoring;

namespace Occop.Services.Security
{
    /// <summary>
    /// 清理管理器接口
    /// </summary>
    public interface ICleanupManager : IDisposable
    {
        /// <summary>
        /// 清理操作完成事件
        /// </summary>
        event EventHandler<CleanupResult> CleanupCompleted;

        /// <summary>
        /// 清理错误事件
        /// </summary>
        event EventHandler<Exception> CleanupError;

        /// <summary>
        /// 是否正在运行
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// 启动清理管理器
        /// </summary>
        /// <returns>启动结果</returns>
        Task<bool> StartAsync();

        /// <summary>
        /// 停止清理管理器
        /// </summary>
        /// <returns>停止结果</returns>
        Task<bool> StopAsync();

        /// <summary>
        /// 执行手动清理
        /// </summary>
        /// <param name="operationType">清理类型</param>
        /// <param name="targets">清理目标</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>清理结果</returns>
        Task<CleanupResult> ExecuteCleanupAsync(CleanupOperationType operationType, string[] targets = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 注册进程监控
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="timeoutMinutes">超时时间</param>
        /// <returns>是否成功</returns>
        bool RegisterProcessMonitoring(int processId, int timeoutMinutes = 0);

        /// <summary>
        /// 注销进程监控
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <returns>是否成功</returns>
        bool UnregisterProcessMonitoring(int processId);

        /// <summary>
        /// 获取清理统计信息
        /// </summary>
        /// <returns>统计信息</returns>
        Dictionary<string, object> GetStatistics();
    }

    /// <summary>
    /// 清理管理器
    /// 协调和管理所有清理操作，提供自动和手动清理功能
    /// 集成进程监控、安全管理器和清理触发器
    /// </summary>
    public class CleanupManager : ICleanupManager
    {
        #region 事件定义

        /// <summary>
        /// 清理操作完成事件
        /// </summary>
        public event EventHandler<CleanupResult> CleanupCompleted;

        /// <summary>
        /// 清理错误事件
        /// </summary>
        public event EventHandler<Exception> CleanupError;

        #endregion

        #region 私有字段

        private readonly ISecurityManager _securityManager;
        private readonly IProcessMonitor _processMonitor;
        private readonly CleanupTrigger _cleanupTrigger;
        private readonly SecureStorage _secureStorage;
        private readonly ConcurrentQueue<CleanupOperation> _operationQueue;
        private readonly ConcurrentDictionary<Guid, CleanupResult> _operationResults;
        private readonly SemaphoreSlim _operationSemaphore;
        private readonly Timer _cleanupVerificationTimer;
        private readonly object _stateLock = new object();
        private readonly CancellationTokenSource _cancellationTokenSource;

        private bool _isRunning;
        private bool _disposed;
        private long _totalOperationsExecuted;
        private long _totalOperationsSucceeded;
        private long _totalOperationsFailed;
        private DateTime _startTime;
        private DateTime? _lastCleanupTime;

        #endregion

        #region 配置属性

        /// <summary>
        /// 并发执行的最大清理操作数
        /// </summary>
        public int MaxConcurrentOperations { get; set; } = 3;

        /// <summary>
        /// 操作结果保留时间（小时）
        /// </summary>
        public int ResultRetentionHours { get; set; } = 24;

        /// <summary>
        /// 清理验证间隔（分钟）
        /// </summary>
        public int VerificationIntervalMinutes { get; set; } = 10;

        /// <summary>
        /// 是否启用自动清理验证
        /// </summary>
        public bool AutoVerificationEnabled { get; set; } = true;

        /// <summary>
        /// 强制清理超时时间（秒）
        /// </summary>
        public int ForceCleanupTimeoutSeconds { get; set; } = 10;

        #endregion

        #region 属性

        /// <summary>
        /// 是否正在运行
        /// </summary>
        public bool IsRunning
        {
            get
            {
                lock (_stateLock)
                {
                    return _isRunning;
                }
            }
            private set
            {
                lock (_stateLock)
                {
                    _isRunning = value;
                }
            }
        }

        /// <summary>
        /// 待处理的操作数量
        /// </summary>
        public int PendingOperationsCount => _operationQueue.Count;

        /// <summary>
        /// 已完成的操作结果数量
        /// </summary>
        public int CompletedOperationsCount => _operationResults.Count;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化清理管理器
        /// </summary>
        /// <param name="securityManager">安全管理器</param>
        /// <param name="processMonitor">进程监控器</param>
        /// <param name="secureStorage">安全存储</param>
        public CleanupManager(ISecurityManager securityManager = null, IProcessMonitor processMonitor = null, SecureStorage secureStorage = null)
        {
            _securityManager = securityManager ?? SecurityManager.Instance;
            _processMonitor = processMonitor;
            _secureStorage = secureStorage ?? new SecureStorage();
            _cleanupTrigger = new CleanupTrigger(_processMonitor);

            _operationQueue = new ConcurrentQueue<CleanupOperation>();
            _operationResults = new ConcurrentDictionary<Guid, CleanupResult>();
            _operationSemaphore = new SemaphoreSlim(MaxConcurrentOperations, MaxConcurrentOperations);
            _cancellationTokenSource = new CancellationTokenSource();

            // 初始化清理验证定时器
            _cleanupVerificationTimer = new Timer(OnVerificationTimer, null, Timeout.Infinite, Timeout.Infinite);

            // 注册清理触发器事件
            _cleanupTrigger.CleanupTriggered += OnCleanupTriggered;
            _cleanupTrigger.ErrorOccurred += OnCleanupTriggerError;

            // 注册Finalizer确保资源得到清理
            GC.ReRegisterForFinalize(this);
        }

        #endregion

        #region 启动和停止

        /// <summary>
        /// 启动清理管理器
        /// </summary>
        /// <returns>启动结果</returns>
        public async Task<bool> StartAsync()
        {
            if (IsRunning)
            {
                return true;
            }

            try
            {
                // 启动清理触发器
                var triggerStarted = await _cleanupTrigger.StartAsync();
                if (!triggerStarted)
                {
                    OnError(new InvalidOperationException("无法启动清理触发器"));
                    return false;
                }

                // 启动操作处理任务
                _ = Task.Run(ProcessOperationQueueAsync, _cancellationTokenSource.Token);

                // 启动清理验证定时器
                if (AutoVerificationEnabled && VerificationIntervalMinutes > 0)
                {
                    var intervalMs = TimeSpan.FromMinutes(VerificationIntervalMinutes).TotalMilliseconds;
                    _cleanupVerificationTimer.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(intervalMs));
                }

                IsRunning = true;
                _startTime = DateTime.UtcNow;

                return true;
            }
            catch (Exception ex)
            {
                OnError(ex);
                return false;
            }
        }

        /// <summary>
        /// 停止清理管理器
        /// </summary>
        /// <returns>停止结果</returns>
        public async Task<bool> StopAsync()
        {
            if (!IsRunning)
            {
                return true;
            }

            try
            {
                IsRunning = false;

                // 停止定时器
                _cleanupVerificationTimer.Change(Timeout.Infinite, Timeout.Infinite);

                // 执行最终清理
                await ExecuteFinalCleanupAsync();

                // 停止清理触发器
                await _cleanupTrigger.StopAsync();

                // 取消所有待处理的操作
                _cancellationTokenSource.Cancel();

                // 等待当前操作完成
                await WaitForPendingOperationsAsync(TimeSpan.FromSeconds(ForceCleanupTimeoutSeconds));

                return true;
            }
            catch (Exception ex)
            {
                OnError(ex);
                return false;
            }
        }

        #endregion

        #region 清理操作执行

        /// <summary>
        /// 执行手动清理
        /// </summary>
        /// <param name="operationType">清理类型</param>
        /// <param name="targets">清理目标</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>清理结果</returns>
        public async Task<CleanupResult> ExecuteCleanupAsync(CleanupOperationType operationType, string[] targets = null, CancellationToken cancellationToken = default)
        {
            if (!IsRunning)
            {
                throw new InvalidOperationException("清理管理器未运行");
            }

            var operation = CreateCleanupOperation(operationType, CleanupTriggerReason.Manual, targets);
            return await ExecuteOperationAsync(operation, cancellationToken);
        }

        /// <summary>
        /// 创建清理操作
        /// </summary>
        /// <param name="operationType">操作类型</param>
        /// <param name="triggerReason">触发原因</param>
        /// <param name="targets">清理目标</param>
        /// <returns>清理操作</returns>
        private CleanupOperation CreateCleanupOperation(CleanupOperationType operationType, CleanupTriggerReason triggerReason, string[] targets = null)
        {
            CleanupOperation operation;

            switch (operationType)
            {
                case CleanupOperationType.MemoryCleanup:
                    operation = CleanupOperationFactory.CreateMemoryCleanup(triggerReason, targets);
                    operation.ExecuteAction = ExecuteMemoryCleanupAsync;
                    break;

                case CleanupOperationType.EnvironmentVariableCleanup:
                    operation = CleanupOperationFactory.CreateEnvironmentVariableCleanup(triggerReason, targets);
                    operation.ExecuteAction = ExecuteEnvironmentVariableCleanupAsync;
                    break;

                case CleanupOperationType.ConfigurationFileCleanup:
                    operation = CleanupOperationFactory.CreateConfigFileCleanup(triggerReason, targets);
                    operation.ExecuteAction = ExecuteConfigFileCleanupAsync;
                    break;

                case CleanupOperationType.ProcessCleanup:
                    operation = CleanupOperationFactory.CreateProcessCleanup(triggerReason, targets?.Select(int.Parse).ToArray());
                    operation.ExecuteAction = ExecuteProcessCleanupAsync;
                    break;

                case CleanupOperationType.CompleteCleanup:
                    operation = CleanupOperationFactory.CreateCompleteCleanup(triggerReason);
                    operation.ExecuteAction = ExecuteCompleteCleanupAsync;
                    break;

                default:
                    throw new ArgumentException($"不支持的清理操作类型: {operationType}");
            }

            // 设置验证函数
            operation.VerifyAction = VerifyCleanupAsync;

            return operation;
        }

        /// <summary>
        /// 执行清理操作
        /// </summary>
        /// <param name="operation">清理操作</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>清理结果</returns>
        private async Task<CleanupResult> ExecuteOperationAsync(CleanupOperation operation, CancellationToken cancellationToken = default)
        {
            var result = await operation.ExecuteAsync(cancellationToken);

            // 记录统计信息
            Interlocked.Increment(ref _totalOperationsExecuted);
            if (result.IsSuccess)
            {
                Interlocked.Increment(ref _totalOperationsSucceeded);
            }
            else
            {
                Interlocked.Increment(ref _totalOperationsFailed);
            }

            // 保存结果
            _operationResults.TryAdd(result.ResultId, result);
            _lastCleanupTime = DateTime.UtcNow;

            // 触发完成事件
            OnCleanupCompleted(result);

            // 清理过期结果
            CleanupExpiredResults();

            return result;
        }

        #endregion

        #region 具体清理实现

        /// <summary>
        /// 执行内存清理
        /// </summary>
        /// <param name="operation">清理操作</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>清理结果</returns>
        private async Task<CleanupResult> ExecuteMemoryCleanupAsync(CleanupOperation operation, CancellationToken cancellationToken)
        {
            var result = CleanupResult.CreateSuccess(operation, "开始内存清理");
            var totalReleasedMemory = 0L;

            try
            {
                // 清理安全管理器
                if (_securityManager != null)
                {
                    var securityStartTime = DateTime.UtcNow;
                    try
                    {
                        _securityManager.ClearSecurityData();
                        var duration = DateTime.UtcNow - securityStartTime;
                        result.AddSuccessItem("SecurityManager", "安全管理器数据已清理", duration);
                    }
                    catch (Exception ex)
                    {
                        var duration = DateTime.UtcNow - securityStartTime;
                        result.AddFailureItem("SecurityManager", "安全管理器清理失败", ex.Message, duration);
                    }
                }

                // 清理安全存储
                if (_secureStorage != null)
                {
                    var storageStartTime = DateTime.UtcNow;
                    try
                    {
                        var cleanupResult = _secureStorage.ClearAll(MemoryCleanupType.Forced);
                        var duration = DateTime.UtcNow - storageStartTime;

                        if (cleanupResult.IsSuccess)
                        {
                            result.AddSuccessItem("SecureStorage", $"已清理 {cleanupResult.ClearedItemsCount} 个安全存储项", duration);
                            totalReleasedMemory += cleanupResult.ClearedItemsCount * 1024; // 估算每项1KB
                        }
                        else
                        {
                            result.AddFailureItem("SecureStorage", "安全存储清理失败", cleanupResult.Message, duration);
                        }
                    }
                    catch (Exception ex)
                    {
                        var duration = DateTime.UtcNow - storageStartTime;
                        result.AddFailureItem("SecureStorage", "安全存储清理异常", ex.Message, duration);
                    }
                }

                // 执行垃圾回收
                var gcStartTime = DateTime.UtcNow;
                try
                {
                    var beforeMemory = GC.GetTotalMemory(false);

                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();

                    var afterMemory = GC.GetTotalMemory(true);
                    var releasedMemory = Math.Max(0, beforeMemory - afterMemory);
                    totalReleasedMemory += releasedMemory;

                    var duration = DateTime.UtcNow - gcStartTime;
                    result.AddSuccessItem("GarbageCollection", $"垃圾回收完成，释放 {releasedMemory:N0} 字节", duration);
                }
                catch (Exception ex)
                {
                    var duration = DateTime.UtcNow - gcStartTime;
                    result.AddFailureItem("GarbageCollection", "垃圾回收失败", ex.Message, duration);
                }

                // 更新统计信息
                result.UpdateStatistics(releasedMemory: totalReleasedMemory);

                await Task.Delay(10, cancellationToken); // 短暂延迟确保操作完成
                return result;
            }
            catch (Exception ex)
            {
                return CleanupResult.CreateFailure(operation, "内存清理过程中发生异常", ex);
            }
        }

        /// <summary>
        /// 执行环境变量清理
        /// </summary>
        /// <param name="operation">清理操作</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>清理结果</returns>
        private async Task<CleanupResult> ExecuteEnvironmentVariableCleanupAsync(CleanupOperation operation, CancellationToken cancellationToken)
        {
            var result = CleanupResult.CreateSuccess(operation, "开始环境变量清理");
            var clearedCount = 0;

            try
            {
                var commonApiKeyVars = new[]
                {
                    "ANTHROPIC_API_KEY",
                    "OPENAI_API_KEY",
                    "CLAUDE_API_KEY",
                    "CODEX_API_KEY",
                    "GITHUB_TOKEN",
                    "CLAUDE_CODE_API_KEY"
                };

                var targetVars = operation.Targets.Any() ? operation.Targets : commonApiKeyVars;

                foreach (var varName in targetVars)
                {
                    var startTime = DateTime.UtcNow;
                    try
                    {
                        var currentValue = Environment.GetEnvironmentVariable(varName);
                        if (!string.IsNullOrEmpty(currentValue))
                        {
                            Environment.SetEnvironmentVariable(varName, null);

                            // 验证清理
                            var verifyValue = Environment.GetEnvironmentVariable(varName);
                            if (string.IsNullOrEmpty(verifyValue))
                            {
                                var duration = DateTime.UtcNow - startTime;
                                result.AddSuccessItem(varName, "环境变量已清理", duration);
                                clearedCount++;
                            }
                            else
                            {
                                var duration = DateTime.UtcNow - startTime;
                                result.AddFailureItem(varName, "环境变量清理验证失败", "变量仍然存在", duration);
                            }
                        }
                        else
                        {
                            result.AddSkippedItem(varName, "环境变量不存在或已为空");
                        }
                    }
                    catch (Exception ex)
                    {
                        var duration = DateTime.UtcNow - startTime;
                        result.AddFailureItem(varName, "环境变量清理失败", ex.Message, duration);
                    }
                }

                result.UpdateStatistics(clearedEnvVars: clearedCount);
                await Task.Delay(5, cancellationToken);
                return result;
            }
            catch (Exception ex)
            {
                return CleanupResult.CreateFailure(operation, "环境变量清理过程中发生异常", ex);
            }
        }

        /// <summary>
        /// 执行配置文件清理
        /// </summary>
        /// <param name="operation">清理操作</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>清理结果</returns>
        private async Task<CleanupResult> ExecuteConfigFileCleanupAsync(CleanupOperation operation, CancellationToken cancellationToken)
        {
            var result = CleanupResult.CreateSuccess(operation, "开始配置文件清理");
            var deletedFiles = 0;
            var totalSize = 0L;

            try
            {
                var commonConfigPaths = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "config.json"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".openai", "config.json"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "claude-code", "config.json"),
                    Path.Combine(Path.GetTempPath(), "claude_temp_config.json"),
                    Path.Combine(Path.GetTempPath(), "openai_temp_config.json")
                };

                var targetPaths = operation.Targets.Any() ? operation.Targets : commonConfigPaths;

                foreach (var filePath in targetPaths)
                {
                    var startTime = DateTime.UtcNow;
                    try
                    {
                        if (File.Exists(filePath))
                        {
                            var fileInfo = new FileInfo(filePath);
                            var fileSize = fileInfo.Length;

                            File.Delete(filePath);

                            // 验证删除
                            if (!File.Exists(filePath))
                            {
                                var duration = DateTime.UtcNow - startTime;
                                result.AddSuccessItem(filePath, $"配置文件已删除 ({fileSize:N0} 字节)", duration);
                                deletedFiles++;
                                totalSize += fileSize;
                            }
                            else
                            {
                                var duration = DateTime.UtcNow - startTime;
                                result.AddFailureItem(filePath, "配置文件删除验证失败", "文件仍然存在", duration);
                            }
                        }
                        else
                        {
                            result.AddSkippedItem(filePath, "配置文件不存在");
                        }
                    }
                    catch (Exception ex)
                    {
                        var duration = DateTime.UtcNow - startTime;
                        result.AddFailureItem(filePath, "配置文件删除失败", ex.Message, duration);
                    }
                }

                result.UpdateStatistics(cleanedDataSize: totalSize, deletedFileCount: deletedFiles);
                await Task.Delay(5, cancellationToken);
                return result;
            }
            catch (Exception ex)
            {
                return CleanupResult.CreateFailure(operation, "配置文件清理过程中发生异常", ex);
            }
        }

        /// <summary>
        /// 执行进程清理
        /// </summary>
        /// <param name="operation">清理操作</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>清理结果</returns>
        private async Task<CleanupResult> ExecuteProcessCleanupAsync(CleanupOperation operation, CancellationToken cancellationToken)
        {
            var result = CleanupResult.CreateSuccess(operation, "开始进程清理");
            var terminatedCount = 0;

            try
            {
                var processIds = operation.Targets.Select(t => int.TryParse(t, out var pid) ? pid : 0).Where(pid => pid > 0);

                foreach (var processId in processIds)
                {
                    var startTime = DateTime.UtcNow;
                    try
                    {
                        var process = Process.GetProcessById(processId);
                        var processName = process.ProcessName;

                        if (!process.HasExited)
                        {
                            process.Kill();

                            // 等待进程退出
                            var waitResult = process.WaitForExit(5000); // 5秒超时

                            var duration = DateTime.UtcNow - startTime;
                            if (waitResult && process.HasExited)
                            {
                                result.AddSuccessItem(processId.ToString(), $"进程 {processName} 已终止", duration);
                                terminatedCount++;
                            }
                            else
                            {
                                result.AddFailureItem(processId.ToString(), $"进程 {processName} 终止超时", "进程未在预期时间内退出", duration);
                            }
                        }
                        else
                        {
                            result.AddSkippedItem(processId.ToString(), $"进程 {processName} 已经退出");
                        }
                    }
                    catch (ArgumentException)
                    {
                        result.AddSkippedItem(processId.ToString(), "进程不存在或已退出");
                    }
                    catch (Exception ex)
                    {
                        var duration = DateTime.UtcNow - startTime;
                        result.AddFailureItem(processId.ToString(), "进程终止失败", ex.Message, duration);
                    }
                }

                result.UpdateStatistics(terminatedProcesses: terminatedCount);
                await Task.Delay(10, cancellationToken);
                return result;
            }
            catch (Exception ex)
            {
                return CleanupResult.CreateFailure(operation, "进程清理过程中发生异常", ex);
            }
        }

        /// <summary>
        /// 执行完整清理
        /// </summary>
        /// <param name="operation">清理操作</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>清理结果</returns>
        private async Task<CleanupResult> ExecuteCompleteCleanupAsync(CleanupOperation operation, CancellationToken cancellationToken)
        {
            var overallResult = CleanupResult.CreateSuccess(operation, "开始完整清理");

            try
            {
                var operations = new[]
                {
                    CreateCleanupOperation(CleanupOperationType.MemoryCleanup, operation.TriggerReason),
                    CreateCleanupOperation(CleanupOperationType.EnvironmentVariableCleanup, operation.TriggerReason),
                    CreateCleanupOperation(CleanupOperationType.ConfigurationFileCleanup, operation.TriggerReason)
                };

                var results = new List<CleanupResult>();

                foreach (var op in operations)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    var result = await op.ExecuteAsync(cancellationToken);
                    results.Add(result);
                }

                // 合并结果
                var combinedResult = results.CombineResults("完整清理操作");

                // 复制到主结果
                overallResult.AddItems(combinedResult.Items);
                overallResult.UpdateStatistics(
                    combinedResult.CleanedDataSize,
                    combinedResult.DeletedFileCount,
                    combinedResult.ClearedEnvironmentVariables,
                    combinedResult.TerminatedProcesses,
                    combinedResult.ReleasedMemorySize
                );

                return overallResult;
            }
            catch (Exception ex)
            {
                return CleanupResult.CreateFailure(operation, "完整清理过程中发生异常", ex);
            }
        }

        #endregion

        #region 进程监控集成

        /// <summary>
        /// 注册进程监控
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="timeoutMinutes">超时时间</param>
        /// <returns>是否成功</returns>
        public bool RegisterProcessMonitoring(int processId, int timeoutMinutes = 0)
        {
            if (!IsRunning)
            {
                return false;
            }

            try
            {
                return _cleanupTrigger.AddProcessMonitoring(processId, timeoutMinutes);
            }
            catch (Exception ex)
            {
                OnError(ex);
                return false;
            }
        }

        /// <summary>
        /// 注销进程监控
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <returns>是否成功</returns>
        public bool UnregisterProcessMonitoring(int processId)
        {
            try
            {
                return _cleanupTrigger.RemoveProcessMonitoring(processId);
            }
            catch (Exception ex)
            {
                OnError(ex);
                return false;
            }
        }

        #endregion

        #region 队列处理

        /// <summary>
        /// 处理操作队列
        /// </summary>
        /// <returns>处理任务</returns>
        private async Task ProcessOperationQueueAsync()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested && IsRunning)
            {
                try
                {
                    if (_operationQueue.TryDequeue(out var operation))
                    {
                        await _operationSemaphore.WaitAsync(_cancellationTokenSource.Token);

                        try
                        {
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await ExecuteOperationAsync(operation, _cancellationTokenSource.Token);
                                }
                                finally
                                {
                                    _operationSemaphore.Release();
                                }
                            }, _cancellationTokenSource.Token);
                        }
                        catch
                        {
                            _operationSemaphore.Release();
                            throw;
                        }
                    }
                    else
                    {
                        await Task.Delay(100, _cancellationTokenSource.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    OnError(ex);
                    await Task.Delay(1000, _cancellationTokenSource.Token);
                }
            }
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 处理清理触发事件
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">事件参数</param>
        private async void OnCleanupTriggered(object sender, CleanupTriggerEventArgs e)
        {
            if (!IsRunning)
            {
                return;
            }

            try
            {
                CleanupOperation operation;

                if (e.IsUrgent || e.Reason == CleanupTriggerReason.SystemShutdown || e.Reason == CleanupTriggerReason.ApplicationShutdown)
                {
                    // 紧急情况立即执行完整清理
                    operation = CreateCleanupOperation(CleanupOperationType.CompleteCleanup, e.Reason);
                    operation.Priority = CleanupPriority.Critical;
                    await ExecuteOperationAsync(operation, _cancellationTokenSource.Token);
                }
                else
                {
                    // 常规情况根据类型执行相应清理
                    operation = CreateCleanupOperation(
                        e.ProcessId.HasValue ? CleanupOperationType.ProcessCleanup : CleanupOperationType.MemoryCleanup,
                        e.Reason,
                        e.ProcessId.HasValue ? new[] { e.ProcessId.ToString() } : null
                    );

                    _operationQueue.Enqueue(operation);
                }
            }
            catch (Exception ex)
            {
                OnError(ex);
            }
        }

        /// <summary>
        /// 处理清理触发器错误
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">异常</param>
        private void OnCleanupTriggerError(object sender, Exception e)
        {
            OnError(e);
        }

        #endregion

        #region 验证和维护

        /// <summary>
        /// 验证清理结果
        /// </summary>
        /// <param name="operation">清理操作</param>
        /// <returns>验证结果</returns>
        private async Task<bool> VerifyCleanupAsync(CleanupOperation operation)
        {
            try
            {
                switch (operation.Type)
                {
                    case CleanupOperationType.EnvironmentVariableCleanup:
                        return VerifyEnvironmentVariableCleanup(operation.Targets);

                    case CleanupOperationType.ConfigurationFileCleanup:
                        return VerifyConfigFileCleanup(operation.Targets);

                    case CleanupOperationType.MemoryCleanup:
                    case CleanupOperationType.ProcessCleanup:
                    case CleanupOperationType.CompleteCleanup:
                    default:
                        return true; // 这些类型的清理难以直接验证
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 验证环境变量清理
        /// </summary>
        /// <param name="variableNames">变量名列表</param>
        /// <returns>验证结果</returns>
        private bool VerifyEnvironmentVariableCleanup(IEnumerable<string> variableNames)
        {
            foreach (var varName in variableNames)
            {
                var value = Environment.GetEnvironmentVariable(varName);
                if (!string.IsNullOrEmpty(value))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 验证配置文件清理
        /// </summary>
        /// <param name="filePaths">文件路径列表</param>
        /// <returns>验证结果</returns>
        private bool VerifyConfigFileCleanup(IEnumerable<string> filePaths)
        {
            foreach (var filePath in filePaths)
            {
                if (File.Exists(filePath))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 验证定时器回调
        /// </summary>
        /// <param name="state">状态</param>
        private async void OnVerificationTimer(object state)
        {
            if (!IsRunning)
            {
                return;
            }

            try
            {
                // 执行轻量级验证清理
                var operation = CreateCleanupOperation(CleanupOperationType.MemoryCleanup, CleanupTriggerReason.Scheduled);
                operation.Priority = CleanupPriority.Low;
                _operationQueue.Enqueue(operation);
            }
            catch (Exception ex)
            {
                OnError(ex);
            }
        }

        /// <summary>
        /// 清理过期结果
        /// </summary>
        private void CleanupExpiredResults()
        {
            try
            {
                var cutoffTime = DateTime.UtcNow.AddHours(-ResultRetentionHours);
                var expiredKeys = _operationResults
                    .Where(kvp => kvp.Value.EndTime < cutoffTime)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    _operationResults.TryRemove(key, out _);
                }
            }
            catch (Exception ex)
            {
                OnError(ex);
            }
        }

        /// <summary>
        /// 等待待处理操作完成
        /// </summary>
        /// <param name="timeout">超时时间</param>
        /// <returns>等待任务</returns>
        private async Task WaitForPendingOperationsAsync(TimeSpan timeout)
        {
            var startTime = DateTime.UtcNow;

            while (DateTime.UtcNow - startTime < timeout && _operationSemaphore.CurrentCount < MaxConcurrentOperations)
            {
                await Task.Delay(100);
            }
        }

        /// <summary>
        /// 执行最终清理
        /// </summary>
        /// <returns>清理任务</returns>
        private async Task ExecuteFinalCleanupAsync()
        {
            try
            {
                var operation = CreateCleanupOperation(CleanupOperationType.CompleteCleanup, CleanupTriggerReason.ApplicationShutdown);
                operation.Priority = CleanupPriority.Critical;
                operation.Timeout = TimeSpan.FromSeconds(ForceCleanupTimeoutSeconds);

                using var cts = new CancellationTokenSource(operation.Timeout);
                await ExecuteOperationAsync(operation, cts.Token);
            }
            catch
            {
                // 在最终清理中忽略错误
            }
        }

        #endregion

        #region 统计信息

        /// <summary>
        /// 获取清理统计信息
        /// </summary>
        /// <returns>统计信息</returns>
        public Dictionary<string, object> GetStatistics()
        {
            var uptime = IsRunning ? DateTime.UtcNow - _startTime : TimeSpan.Zero;

            return new Dictionary<string, object>
            {
                { "IsRunning", IsRunning },
                { "StartTime", _startTime },
                { "Uptime", uptime },
                { "LastCleanupTime", _lastCleanupTime },
                { "TotalOperationsExecuted", _totalOperationsExecuted },
                { "TotalOperationsSucceeded", _totalOperationsSucceeded },
                { "TotalOperationsFailed", _totalOperationsFailed },
                { "SuccessRate", _totalOperationsExecuted > 0 ? (double)_totalOperationsSucceeded / _totalOperationsExecuted * 100 : 0 },
                { "PendingOperationsCount", PendingOperationsCount },
                { "CompletedOperationsCount", CompletedOperationsCount },
                { "MonitoredProcessCount", _cleanupTrigger?.MonitoredProcessCount ?? 0 },
                { "ActiveTimeoutCount", _cleanupTrigger?.ActiveTimeoutCount ?? 0 },
                { "MaxConcurrentOperations", MaxConcurrentOperations },
                { "AvailableOperationSlots", _operationSemaphore.CurrentCount }
            };
        }

        #endregion

        #region 事件通知

        /// <summary>
        /// 触发清理完成事件
        /// </summary>
        /// <param name="result">清理结果</param>
        protected virtual void OnCleanupCompleted(CleanupResult result)
        {
            try
            {
                CleanupCompleted?.Invoke(this, result);
            }
            catch (Exception ex)
            {
                OnError(ex);
            }
        }

        /// <summary>
        /// 触发错误事件
        /// </summary>
        /// <param name="exception">异常</param>
        protected virtual void OnError(Exception exception)
        {
            try
            {
                CleanupError?.Invoke(this, exception);
            }
            catch
            {
                // 避免错误处理中的无限循环
            }
        }

        #endregion

        #region 资源清理

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                // 停止管理器
                StopAsync().Wait(TimeSpan.FromSeconds(ForceCleanupTimeoutSeconds));

                // 释放资源
                _cleanupTrigger?.Dispose();
                _secureStorage?.Dispose();
                _operationSemaphore?.Dispose();
                _cleanupVerificationTimer?.Dispose();
                _cancellationTokenSource?.Dispose();

                // 清理队列
                while (_operationQueue.TryDequeue(out _)) { }
                _operationResults.Clear();
            }
            catch
            {
                // 在释放过程中忽略错误
            }
            finally
            {
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// 析构函数，确保资源得到清理
        /// </summary>
        ~CleanupManager()
        {
            Dispose();
        }

        #endregion
    }
}