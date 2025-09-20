using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.IO.Compression;
using Occop.Models.Monitoring;

namespace Occop.Services.Monitoring
{
    /// <summary>
    /// 监控数据持久化服务
    /// 负责监控状态、统计信息和事件历史的保存和恢复，支持自动备份和数据压缩
    /// </summary>
    public class MonitoringPersistence : IDisposable
    {
        #region 字段和属性

        private readonly MonitoringConfiguration _config;
        private readonly Timer _autoSaveTimer;
        private readonly SemaphoreSlim _saveSemaphore;
        private readonly object _lockObject = new object();
        private volatile bool _disposed;
        private volatile bool _isEnabled;

        /// <summary>
        /// 最后一次保存时间
        /// </summary>
        public DateTime? LastSaveTime { get; private set; }

        /// <summary>
        /// 最后一次加载时间
        /// </summary>
        public DateTime? LastLoadTime { get; private set; }

        /// <summary>
        /// 保存次数统计
        /// </summary>
        public long SaveCount { get; private set; }

        /// <summary>
        /// 加载次数统计
        /// </summary>
        public long LoadCount { get; private set; }

        /// <summary>
        /// 保存失败次数
        /// </summary>
        public long SaveFailureCount { get; private set; }

        /// <summary>
        /// 加载失败次数
        /// </summary>
        public long LoadFailureCount { get; private set; }

        /// <summary>
        /// 是否启用持久化
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled && _config.EnableStatePersistence;
            set => _isEnabled = value;
        }

        /// <summary>
        /// 数据目录路径
        /// </summary>
        public string DataDirectory { get; private set; }

        #endregion

        #region 事件定义

        /// <summary>
        /// 保存完成事件
        /// </summary>
        public event EventHandler<PersistenceEventArgs> SaveCompleted;

        /// <summary>
        /// 加载完成事件
        /// </summary>
        public event EventHandler<PersistenceEventArgs> LoadCompleted;

        /// <summary>
        /// 持久化错误事件
        /// </summary>
        public event EventHandler<PersistenceErrorEventArgs> PersistenceError;

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="config">监控配置</param>
        public MonitoringPersistence(MonitoringConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _saveSemaphore = new SemaphoreSlim(1, 1);
            _isEnabled = true;

            // 初始化数据目录
            InitializeDataDirectory();

            // 启动自动保存定时器
            if (_config.AutoSaveIntervalMinutes > 0)
            {
                var interval = TimeSpan.FromMinutes(_config.AutoSaveIntervalMinutes);
                _autoSaveTimer = new Timer(AutoSaveCallback, null, interval, interval);
            }
        }

        #endregion

        #region 初始化方法

        /// <summary>
        /// 初始化数据目录
        /// </summary>
        private void InitializeDataDirectory()
        {
            try
            {
                // 确定数据目录
                var stateFilePath = _config.StateFilePath;
                if (Path.IsPathRooted(stateFilePath))
                {
                    DataDirectory = Path.GetDirectoryName(stateFilePath);
                }
                else
                {
                    DataDirectory = Path.Combine(Environment.CurrentDirectory, "MonitoringData");
                }

                // 创建目录
                if (!Directory.Exists(DataDirectory))
                {
                    Directory.CreateDirectory(DataDirectory);
                }

                // 创建子目录
                CreateSubDirectories();
            }
            catch (Exception ex)
            {
                OnPersistenceError(new PersistenceErrorEventArgs(ex, "初始化数据目录失败"));
                throw;
            }
        }

        /// <summary>
        /// 创建子目录
        /// </summary>
        private void CreateSubDirectories()
        {
            var directories = new[]
            {
                Path.Combine(DataDirectory, "States"),      // 状态文件目录
                Path.Combine(DataDirectory, "Events"),      // 事件历史目录
                Path.Combine(DataDirectory, "Statistics"),  // 统计数据目录
                Path.Combine(DataDirectory, "Backups"),     // 备份目录
                Path.Combine(DataDirectory, "Archives")     // 归档目录
            };

            foreach (var dir in directories)
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }
        }

        #endregion

        #region 状态持久化

        /// <summary>
        /// 保存监控状态
        /// </summary>
        /// <param name="state">监控状态</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>保存结果</returns>
        public async Task<MonitoringResult> SaveStateAsync(MonitoringState state, CancellationToken cancellationToken = default)
        {
            if (!IsEnabled)
            {
                return MonitoringResult.Success("持久化已禁用，跳过状态保存");
            }

            await _saveSemaphore.WaitAsync(cancellationToken);
            try
            {
                var stateData = new MonitoringStateData
                {
                    State = state,
                    Timestamp = DateTime.UtcNow,
                    Version = "1.0"
                };

                var filePath = GetStateFilePath();
                var backupPath = GetBackupFilePath(filePath);

                // 备份现有文件
                if (File.Exists(filePath))
                {
                    File.Copy(filePath, backupPath, true);
                }

                // 保存新状态
                var result = await SaveJsonFileAsync(stateData, filePath, cancellationToken);

                if (result.Success)
                {
                    SaveCount++;
                    LastSaveTime = DateTime.UtcNow;
                    OnSaveCompleted(new PersistenceEventArgs("State", filePath, true, "状态保存成功"));
                }
                else
                {
                    SaveFailureCount++;
                    OnPersistenceError(new PersistenceErrorEventArgs(result.Exception, "状态保存失败"));
                }

                return result;
            }
            catch (Exception ex)
            {
                SaveFailureCount++;
                var errorResult = MonitoringResult.Failure($"保存状态时发生异常: {ex.Message}", ex);
                OnPersistenceError(new PersistenceErrorEventArgs(ex, "保存状态异常"));
                return errorResult;
            }
            finally
            {
                _saveSemaphore.Release();
            }
        }

        /// <summary>
        /// 加载监控状态
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>加载结果</returns>
        public async Task<(MonitoringResult Result, MonitoringState? State)> LoadStateAsync(CancellationToken cancellationToken = default)
        {
            if (!IsEnabled)
            {
                return (MonitoringResult.Success("持久化已禁用，返回默认状态"), MonitoringState.Stopped);
            }

            try
            {
                var filePath = GetStateFilePath();

                if (!File.Exists(filePath))
                {
                    return (MonitoringResult.Success("状态文件不存在，返回默认状态"), MonitoringState.Stopped);
                }

                var (result, stateData) = await LoadJsonFileAsync<MonitoringStateData>(filePath, cancellationToken);

                if (result.Success && stateData != null)
                {
                    LoadCount++;
                    LastLoadTime = DateTime.UtcNow;
                    OnLoadCompleted(new PersistenceEventArgs("State", filePath, true, "状态加载成功"));
                    return (result, stateData.State);
                }
                else
                {
                    LoadFailureCount++;
                    OnPersistenceError(new PersistenceErrorEventArgs(result.Exception, "状态加载失败"));
                    return (result, null);
                }
            }
            catch (Exception ex)
            {
                LoadFailureCount++;
                var errorResult = MonitoringResult.Failure($"加载状态时发生异常: {ex.Message}", ex);
                OnPersistenceError(new PersistenceErrorEventArgs(ex, "加载状态异常"));
                return (errorResult, null);
            }
        }

        #endregion

        #region 统计信息持久化

        /// <summary>
        /// 保存监控统计信息
        /// </summary>
        /// <param name="statistics">统计信息</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>保存结果</returns>
        public async Task<MonitoringResult> SaveStatisticsAsync(MonitoringStatistics statistics, CancellationToken cancellationToken = default)
        {
            if (!IsEnabled || statistics == null)
            {
                return MonitoringResult.Success("持久化已禁用或统计信息为空，跳过保存");
            }

            await _saveSemaphore.WaitAsync(cancellationToken);
            try
            {
                var timestamp = DateTime.UtcNow;
                var fileName = $"statistics_{timestamp:yyyyMMdd_HHmmss}.json";
                var filePath = Path.Combine(DataDirectory, "Statistics", fileName);

                var result = await SaveJsonFileAsync(statistics, filePath, cancellationToken);

                if (result.Success)
                {
                    SaveCount++;
                    OnSaveCompleted(new PersistenceEventArgs("Statistics", filePath, true, "统计信息保存成功"));

                    // 清理旧的统计文件
                    await CleanupOldStatisticsFilesAsync();
                }
                else
                {
                    SaveFailureCount++;
                    OnPersistenceError(new PersistenceErrorEventArgs(result.Exception, "统计信息保存失败"));
                }

                return result;
            }
            catch (Exception ex)
            {
                SaveFailureCount++;
                var errorResult = MonitoringResult.Failure($"保存统计信息时发生异常: {ex.Message}", ex);
                OnPersistenceError(new PersistenceErrorEventArgs(ex, "保存统计信息异常"));
                return errorResult;
            }
            finally
            {
                _saveSemaphore.Release();
            }
        }

        /// <summary>
        /// 加载最新的统计信息
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>加载结果</returns>
        public async Task<(MonitoringResult Result, MonitoringStatistics Statistics)> LoadLatestStatisticsAsync(CancellationToken cancellationToken = default)
        {
            if (!IsEnabled)
            {
                return (MonitoringResult.Success("持久化已禁用，返回新的统计对象"), new MonitoringStatistics());
            }

            try
            {
                var statisticsDir = Path.Combine(DataDirectory, "Statistics");
                if (!Directory.Exists(statisticsDir))
                {
                    return (MonitoringResult.Success("统计目录不存在，返回新的统计对象"), new MonitoringStatistics());
                }

                var files = Directory.GetFiles(statisticsDir, "statistics_*.json")
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .ToArray();

                if (files.Length == 0)
                {
                    return (MonitoringResult.Success("未找到统计文件，返回新的统计对象"), new MonitoringStatistics());
                }

                var latestFile = files[0];
                var (result, statistics) = await LoadJsonFileAsync<MonitoringStatistics>(latestFile, cancellationToken);

                if (result.Success && statistics != null)
                {
                    LoadCount++;
                    LastLoadTime = DateTime.UtcNow;
                    OnLoadCompleted(new PersistenceEventArgs("Statistics", latestFile, true, "统计信息加载成功"));
                    return (result, statistics);
                }
                else
                {
                    LoadFailureCount++;
                    OnPersistenceError(new PersistenceErrorEventArgs(result.Exception, "统计信息加载失败"));
                    return (result, new MonitoringStatistics());
                }
            }
            catch (Exception ex)
            {
                LoadFailureCount++;
                var errorResult = MonitoringResult.Failure($"加载统计信息时发生异常: {ex.Message}", ex);
                OnPersistenceError(new PersistenceErrorEventArgs(ex, "加载统计信息异常"));
                return (errorResult, new MonitoringStatistics());
            }
        }

        #endregion

        #region 事件历史持久化

        /// <summary>
        /// 保存事件历史
        /// </summary>
        /// <param name="events">事件列表</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>保存结果</returns>
        public async Task<MonitoringResult> SaveEventHistoryAsync(IEnumerable<MonitoringEvent> events, CancellationToken cancellationToken = default)
        {
            if (!IsEnabled || !_config.EnableEventHistoryPersistence || events == null)
            {
                return MonitoringResult.Success("事件历史持久化已禁用或事件列表为空，跳过保存");
            }

            await _saveSemaphore.WaitAsync(cancellationToken);
            try
            {
                var eventList = events.ToList();
                if (eventList.Count == 0)
                {
                    return MonitoringResult.Success("事件列表为空，跳过保存");
                }

                var timestamp = DateTime.UtcNow;
                var fileName = $"events_{timestamp:yyyyMMdd_HHmmss}.json";
                var filePath = Path.Combine(DataDirectory, "Events", fileName);

                var eventData = new EventHistoryData
                {
                    Timestamp = timestamp,
                    EventCount = eventList.Count,
                    Events = eventList,
                    Version = "1.0"
                };

                var result = await SaveJsonFileAsync(eventData, filePath, cancellationToken);

                if (result.Success)
                {
                    SaveCount++;
                    OnSaveCompleted(new PersistenceEventArgs("EventHistory", filePath, true, $"保存了 {eventList.Count} 个事件"));

                    // 压缩旧的事件文件
                    await CompressOldEventFilesAsync();

                    // 清理过期的事件文件
                    await CleanupExpiredEventFilesAsync();
                }
                else
                {
                    SaveFailureCount++;
                    OnPersistenceError(new PersistenceErrorEventArgs(result.Exception, "事件历史保存失败"));
                }

                return result;
            }
            catch (Exception ex)
            {
                SaveFailureCount++;
                var errorResult = MonitoringResult.Failure($"保存事件历史时发生异常: {ex.Message}", ex);
                OnPersistenceError(new PersistenceErrorEventArgs(ex, "保存事件历史异常"));
                return errorResult;
            }
            finally
            {
                _saveSemaphore.Release();
            }
        }

        /// <summary>
        /// 加载事件历史
        /// </summary>
        /// <param name="fromDate">起始日期</param>
        /// <param name="toDate">结束日期</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>加载结果</returns>
        public async Task<(MonitoringResult Result, List<MonitoringEvent> Events)> LoadEventHistoryAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
        {
            if (!IsEnabled || !_config.EnableEventHistoryPersistence)
            {
                return (MonitoringResult.Success("事件历史持久化已禁用"), new List<MonitoringEvent>());
            }

            try
            {
                var eventsDir = Path.Combine(DataDirectory, "Events");
                if (!Directory.Exists(eventsDir))
                {
                    return (MonitoringResult.Success("事件目录不存在"), new List<MonitoringEvent>());
                }

                var allEvents = new List<MonitoringEvent>();
                var files = Directory.GetFiles(eventsDir, "events_*.json")
                    .Union(Directory.GetFiles(eventsDir, "events_*.json.gz"))
                    .OrderBy(f => File.GetLastWriteTime(f))
                    .ToArray();

                foreach (var filePath in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var (result, eventData) = await LoadEventFileAsync(filePath, cancellationToken);

                        if (result.Success && eventData?.Events != null)
                        {
                            var filteredEvents = eventData.Events
                                .Where(e => (fromDate == null || e.Timestamp >= fromDate) &&
                                           (toDate == null || e.Timestamp <= toDate))
                                .ToList();

                            allEvents.AddRange(filteredEvents);
                        }
                    }
                    catch (Exception ex)
                    {
                        OnPersistenceError(new PersistenceErrorEventArgs(ex, $"加载事件文件失败: {filePath}"));
                        // 继续处理其他文件
                    }
                }

                LoadCount++;
                LastLoadTime = DateTime.UtcNow;
                OnLoadCompleted(new PersistenceEventArgs("EventHistory", eventsDir, true, $"加载了 {allEvents.Count} 个事件"));

                return (MonitoringResult.Success($"成功加载 {allEvents.Count} 个事件"), allEvents);
            }
            catch (Exception ex)
            {
                LoadFailureCount++;
                var errorResult = MonitoringResult.Failure($"加载事件历史时发生异常: {ex.Message}", ex);
                OnPersistenceError(new PersistenceErrorEventArgs(ex, "加载事件历史异常"));
                return (errorResult, new List<MonitoringEvent>());
            }
        }

        #endregion

        #region 进程信息持久化

        /// <summary>
        /// 保存监控进程信息
        /// </summary>
        /// <param name="processes">进程信息列表</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>保存结果</returns>
        public async Task<MonitoringResult> SaveProcessInfoAsync(IEnumerable<ProcessInfo> processes, CancellationToken cancellationToken = default)
        {
            if (!IsEnabled || processes == null)
            {
                return MonitoringResult.Success("持久化已禁用或进程列表为空，跳过保存");
            }

            await _saveSemaphore.WaitAsync(cancellationToken);
            try
            {
                var processList = processes.ToList();
                var processData = new ProcessInfoData
                {
                    Timestamp = DateTime.UtcNow,
                    ProcessCount = processList.Count,
                    Processes = processList,
                    Version = "1.0"
                };

                var filePath = Path.Combine(DataDirectory, "States", "processes.json");
                var result = await SaveJsonFileAsync(processData, filePath, cancellationToken);

                if (result.Success)
                {
                    SaveCount++;
                    OnSaveCompleted(new PersistenceEventArgs("ProcessInfo", filePath, true, $"保存了 {processList.Count} 个进程信息"));
                }
                else
                {
                    SaveFailureCount++;
                    OnPersistenceError(new PersistenceErrorEventArgs(result.Exception, "进程信息保存失败"));
                }

                return result;
            }
            catch (Exception ex)
            {
                SaveFailureCount++;
                var errorResult = MonitoringResult.Failure($"保存进程信息时发生异常: {ex.Message}", ex);
                OnPersistenceError(new PersistenceErrorEventArgs(ex, "保存进程信息异常"));
                return errorResult;
            }
            finally
            {
                _saveSemaphore.Release();
            }
        }

        /// <summary>
        /// 加载监控进程信息
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>加载结果</returns>
        public async Task<(MonitoringResult Result, List<ProcessInfo> Processes)> LoadProcessInfoAsync(CancellationToken cancellationToken = default)
        {
            if (!IsEnabled)
            {
                return (MonitoringResult.Success("持久化已禁用"), new List<ProcessInfo>());
            }

            try
            {
                var filePath = Path.Combine(DataDirectory, "States", "processes.json");

                if (!File.Exists(filePath))
                {
                    return (MonitoringResult.Success("进程信息文件不存在"), new List<ProcessInfo>());
                }

                var (result, processData) = await LoadJsonFileAsync<ProcessInfoData>(filePath, cancellationToken);

                if (result.Success && processData?.Processes != null)
                {
                    LoadCount++;
                    LastLoadTime = DateTime.UtcNow;
                    OnLoadCompleted(new PersistenceEventArgs("ProcessInfo", filePath, true, $"加载了 {processData.Processes.Count} 个进程信息"));
                    return (result, processData.Processes);
                }
                else
                {
                    LoadFailureCount++;
                    OnPersistenceError(new PersistenceErrorEventArgs(result.Exception, "进程信息加载失败"));
                    return (result, new List<ProcessInfo>());
                }
            }
            catch (Exception ex)
            {
                LoadFailureCount++;
                var errorResult = MonitoringResult.Failure($"加载进程信息时发生异常: {ex.Message}", ex);
                OnPersistenceError(new PersistenceErrorEventArgs(ex, "加载进程信息异常"));
                return (errorResult, new List<ProcessInfo>());
            }
        }

        #endregion

        #region 自动保存

        /// <summary>
        /// 自动保存回调
        /// </summary>
        /// <param name="state">状态对象</param>
        private async void AutoSaveCallback(object state)
        {
            if (_disposed || !IsEnabled)
                return;

            try
            {
                // 这里需要从外部获取当前的监控状态和统计信息
                // 由于这是内部回调，我们只记录自动保存事件
                OnSaveCompleted(new PersistenceEventArgs("AutoSave", DataDirectory, true, "自动保存触发"));
            }
            catch (Exception ex)
            {
                OnPersistenceError(new PersistenceErrorEventArgs(ex, "自动保存失败"));
            }
        }

        #endregion

        #region 文件操作方法

        /// <summary>
        /// 保存JSON文件
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="data">数据对象</param>
        /// <param name="filePath">文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>保存结果</returns>
        private async Task<MonitoringResult> SaveJsonFileAsync<T>(T data, string filePath, CancellationToken cancellationToken)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = false, // 减少文件大小
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                var json = JsonSerializer.Serialize(data, options);

                // 确保目录存在
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 异步写入文件
                await File.WriteAllTextAsync(filePath, json, cancellationToken);

                return MonitoringResult.Success($"文件保存成功: {filePath}");
            }
            catch (Exception ex)
            {
                return MonitoringResult.Failure($"保存文件失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 加载JSON文件
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="filePath">文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>加载结果</returns>
        private async Task<(MonitoringResult Result, T Data)> LoadJsonFileAsync<T>(string filePath, CancellationToken cancellationToken) where T : class
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return (MonitoringResult.Failure($"文件不存在: {filePath}"), null);
                }

                var json = await File.ReadAllTextAsync(filePath, cancellationToken);

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                };

                var data = JsonSerializer.Deserialize<T>(json, options);

                return (MonitoringResult.Success($"文件加载成功: {filePath}"), data);
            }
            catch (Exception ex)
            {
                return (MonitoringResult.Failure($"加载文件失败: {ex.Message}", ex), null);
            }
        }

        /// <summary>
        /// 加载事件文件（支持压缩格式）
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>加载结果</returns>
        private async Task<(MonitoringResult Result, EventHistoryData Data)> LoadEventFileAsync(string filePath, CancellationToken cancellationToken)
        {
            try
            {
                string json;

                if (filePath.EndsWith(".gz"))
                {
                    // 解压缩文件
                    await using var fileStream = File.OpenRead(filePath);
                    await using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
                    using var reader = new StreamReader(gzipStream);
                    json = await reader.ReadToEndAsync();
                }
                else
                {
                    json = await File.ReadAllTextAsync(filePath, cancellationToken);
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                };

                var data = JsonSerializer.Deserialize<EventHistoryData>(json, options);

                return (MonitoringResult.Success($"事件文件加载成功: {filePath}"), data);
            }
            catch (Exception ex)
            {
                return (MonitoringResult.Failure($"加载事件文件失败: {ex.Message}", ex), null);
            }
        }

        #endregion

        #region 清理和维护

        /// <summary>
        /// 清理旧的统计文件
        /// </summary>
        /// <returns>清理任务</returns>
        private async Task CleanupOldStatisticsFilesAsync()
        {
            try
            {
                var statisticsDir = Path.Combine(DataDirectory, "Statistics");
                if (!Directory.Exists(statisticsDir))
                    return;

                var files = Directory.GetFiles(statisticsDir, "statistics_*.json")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTime)
                    .Skip(24) // 保留最新的24个文件
                    .ToArray();

                foreach (var file in files)
                {
                    try
                    {
                        file.Delete();
                    }
                    catch (Exception ex)
                    {
                        OnPersistenceError(new PersistenceErrorEventArgs(ex, $"删除旧统计文件失败: {file.FullName}"));
                    }
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                OnPersistenceError(new PersistenceErrorEventArgs(ex, "清理旧统计文件失败"));
            }
        }

        /// <summary>
        /// 压缩旧的事件文件
        /// </summary>
        /// <returns>压缩任务</returns>
        private async Task CompressOldEventFilesAsync()
        {
            try
            {
                var eventsDir = Path.Combine(DataDirectory, "Events");
                if (!Directory.Exists(eventsDir))
                    return;

                var cutoffTime = DateTime.UtcNow.AddDays(-1); // 压缩1天前的文件
                var files = Directory.GetFiles(eventsDir, "events_*.json")
                    .Where(f => File.GetLastWriteTime(f) < cutoffTime)
                    .ToArray();

                foreach (var filePath in files)
                {
                    try
                    {
                        var compressedPath = $"{filePath}.gz";
                        if (File.Exists(compressedPath))
                            continue;

                        await using var input = File.OpenRead(filePath);
                        await using var output = File.Create(compressedPath);
                        await using var gzip = new GZipStream(output, CompressionMode.Compress);
                        await input.CopyToAsync(gzip);

                        // 删除原文件
                        File.Delete(filePath);
                    }
                    catch (Exception ex)
                    {
                        OnPersistenceError(new PersistenceErrorEventArgs(ex, $"压缩事件文件失败: {filePath}"));
                    }
                }
            }
            catch (Exception ex)
            {
                OnPersistenceError(new PersistenceErrorEventArgs(ex, "压缩旧事件文件失败"));
            }
        }

        /// <summary>
        /// 清理过期的事件文件
        /// </summary>
        /// <returns>清理任务</returns>
        private async Task CleanupExpiredEventFilesAsync()
        {
            try
            {
                var eventsDir = Path.Combine(DataDirectory, "Events");
                if (!Directory.Exists(eventsDir))
                    return;

                var cutoffTime = DateTime.UtcNow.AddDays(-_config.EventHistoryRetentionDays);
                var files = Directory.GetFiles(eventsDir, "events_*")
                    .Where(f => File.GetLastWriteTime(f) < cutoffTime)
                    .ToArray();

                foreach (var filePath in files)
                {
                    try
                    {
                        File.Delete(filePath);
                    }
                    catch (Exception ex)
                    {
                        OnPersistenceError(new PersistenceErrorEventArgs(ex, $"删除过期事件文件失败: {filePath}"));
                    }
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                OnPersistenceError(new PersistenceErrorEventArgs(ex, "清理过期事件文件失败"));
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取状态文件路径
        /// </summary>
        /// <returns>状态文件路径</returns>
        private string GetStateFilePath()
        {
            var fileName = _config.StateFilePath;
            if (Path.IsPathRooted(fileName))
            {
                return fileName;
            }
            return Path.Combine(DataDirectory, "States", fileName);
        }

        /// <summary>
        /// 获取备份文件路径
        /// </summary>
        /// <param name="originalPath">原文件路径</param>
        /// <returns>备份文件路径</returns>
        private string GetBackupFilePath(string originalPath)
        {
            var fileName = Path.GetFileNameWithoutExtension(originalPath);
            var extension = Path.GetExtension(originalPath);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var backupFileName = $"{fileName}_backup_{timestamp}{extension}";
            return Path.Combine(DataDirectory, "Backups", backupFileName);
        }

        /// <summary>
        /// 获取存储统计信息
        /// </summary>
        /// <returns>存储统计</returns>
        public PersistenceStatistics GetStatistics()
        {
            return new PersistenceStatistics
            {
                SaveCount = SaveCount,
                LoadCount = LoadCount,
                SaveFailureCount = SaveFailureCount,
                LoadFailureCount = LoadFailureCount,
                LastSaveTime = LastSaveTime,
                LastLoadTime = LastLoadTime,
                DataDirectory = DataDirectory,
                IsEnabled = IsEnabled
            };
        }

        #endregion

        #region 事件触发方法

        /// <summary>
        /// 触发保存完成事件
        /// </summary>
        /// <param name="e">事件参数</param>
        protected virtual void OnSaveCompleted(PersistenceEventArgs e)
        {
            SaveCompleted?.Invoke(this, e);
        }

        /// <summary>
        /// 触发加载完成事件
        /// </summary>
        /// <param name="e">事件参数</param>
        protected virtual void OnLoadCompleted(PersistenceEventArgs e)
        {
            LoadCompleted?.Invoke(this, e);
        }

        /// <summary>
        /// 触发持久化错误事件
        /// </summary>
        /// <param name="e">事件参数</param>
        protected virtual void OnPersistenceError(PersistenceErrorEventArgs e)
        {
            PersistenceError?.Invoke(this, e);
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
        /// <param name="disposing">是否正在释放</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                try
                {
                    _autoSaveTimer?.Dispose();
                    _saveSemaphore?.Dispose();
                }
                catch { /* 忽略释放异常 */ }
            }

            _disposed = true;
        }

        /// <summary>
        /// 析构函数
        /// </summary>
        ~MonitoringPersistence()
        {
            Dispose(false);
        }

        #endregion
    }

    #region 数据模型

    /// <summary>
    /// 监控状态数据
    /// </summary>
    public class MonitoringStateData
    {
        /// <summary>
        /// 监控状态
        /// </summary>
        public MonitoringState State { get; set; }

        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 版本
        /// </summary>
        public string Version { get; set; }
    }

    /// <summary>
    /// 事件历史数据
    /// </summary>
    public class EventHistoryData
    {
        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 事件数量
        /// </summary>
        public int EventCount { get; set; }

        /// <summary>
        /// 事件列表
        /// </summary>
        public List<MonitoringEvent> Events { get; set; }

        /// <summary>
        /// 版本
        /// </summary>
        public string Version { get; set; }
    }

    /// <summary>
    /// 进程信息数据
    /// </summary>
    public class ProcessInfoData
    {
        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 进程数量
        /// </summary>
        public int ProcessCount { get; set; }

        /// <summary>
        /// 进程列表
        /// </summary>
        public List<ProcessInfo> Processes { get; set; }

        /// <summary>
        /// 版本
        /// </summary>
        public string Version { get; set; }
    }

    /// <summary>
    /// 持久化统计信息
    /// </summary>
    public class PersistenceStatistics
    {
        /// <summary>
        /// 保存次数
        /// </summary>
        public long SaveCount { get; set; }

        /// <summary>
        /// 加载次数
        /// </summary>
        public long LoadCount { get; set; }

        /// <summary>
        /// 保存失败次数
        /// </summary>
        public long SaveFailureCount { get; set; }

        /// <summary>
        /// 加载失败次数
        /// </summary>
        public long LoadFailureCount { get; set; }

        /// <summary>
        /// 最后保存时间
        /// </summary>
        public DateTime? LastSaveTime { get; set; }

        /// <summary>
        /// 最后加载时间
        /// </summary>
        public DateTime? LastLoadTime { get; set; }

        /// <summary>
        /// 数据目录
        /// </summary>
        public string DataDirectory { get; set; }

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// 保存成功率
        /// </summary>
        public double SaveSuccessRate => SaveCount > 0 ? (double)(SaveCount - SaveFailureCount) / SaveCount * 100 : 0;

        /// <summary>
        /// 加载成功率
        /// </summary>
        public double LoadSuccessRate => LoadCount > 0 ? (double)(LoadCount - LoadFailureCount) / LoadCount * 100 : 0;
    }

    #endregion

    #region 事件参数

    /// <summary>
    /// 持久化事件参数
    /// </summary>
    public class PersistenceEventArgs : EventArgs
    {
        /// <summary>
        /// 操作类型
        /// </summary>
        public string OperationType { get; }

        /// <summary>
        /// 文件路径
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// 消息
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; }

        public PersistenceEventArgs(string operationType, string filePath, bool success, string message)
        {
            OperationType = operationType;
            FilePath = filePath;
            Success = success;
            Message = message;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// 持久化错误事件参数
    /// </summary>
    public class PersistenceErrorEventArgs : EventArgs
    {
        /// <summary>
        /// 异常信息
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// 错误上下文
        /// </summary>
        public string Context { get; }

        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; }

        public PersistenceErrorEventArgs(Exception exception, string context)
        {
            Exception = exception;
            Context = context;
            Timestamp = DateTime.UtcNow;
        }
    }

    #endregion
}