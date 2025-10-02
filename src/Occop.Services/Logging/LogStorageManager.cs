using Microsoft.Extensions.Logging;
using System.IO;
using System.IO.Compression;

namespace Occop.Services.Logging
{
    /// <summary>
    /// 日志存储管理器，负责日志文件的轮换、归档和清理
    /// Log storage manager responsible for log rotation, archiving and cleanup
    /// </summary>
    public class LogStorageManager : IDisposable
    {
        private readonly string _logDirectory;
        private readonly LogStorageConfiguration _configuration;
        private readonly ILogger? _logger;
        private readonly Timer? _cleanupTimer;
        private bool _disposed = false;

        /// <summary>
        /// 初始化日志存储管理器
        /// Initializes log storage manager
        /// </summary>
        /// <param name="logDirectory">日志目录 Log directory</param>
        /// <param name="configuration">存储配置 Storage configuration</param>
        /// <param name="logger">日志记录器 Logger</param>
        public LogStorageManager(string logDirectory, LogStorageConfiguration? configuration = null, ILogger? logger = null)
        {
            if (string.IsNullOrWhiteSpace(logDirectory))
                throw new ArgumentNullException(nameof(logDirectory));

            _logDirectory = logDirectory;
            _configuration = configuration ?? new LogStorageConfiguration();
            _logger = logger;

            // 确保日志目录存在
            // Ensure log directory exists
            Directory.CreateDirectory(_logDirectory);

            if (_configuration.EnableAutomaticCleanup)
            {
                // 启动定时清理
                // Start automatic cleanup timer
                _cleanupTimer = new Timer(
                    OnCleanupTimer,
                    null,
                    _configuration.CleanupInterval,
                    _configuration.CleanupInterval);
            }
        }

        /// <summary>
        /// 执行日志轮换
        /// Performs log rotation
        /// </summary>
        /// <param name="logFilePath">日志文件路径 Log file path</param>
        /// <returns>轮换任务 Rotation task</returns>
        public async Task<LogRotationResult> RotateLogAsync(string logFilePath)
        {
            var result = new LogRotationResult
            {
                FilePath = logFilePath,
                RotationTime = DateTime.UtcNow,
                Success = false
            };

            try
            {
                if (!File.Exists(logFilePath))
                {
                    result.ErrorMessage = "Log file does not exist";
                    return result;
                }

                var fileInfo = new FileInfo(logFilePath);
                result.OriginalSize = fileInfo.Length;

                // 检查是否需要轮换
                // Check if rotation is needed
                if (fileInfo.Length < _configuration.MaxLogFileSize)
                {
                    result.Success = true;
                    result.ErrorMessage = "Rotation not required";
                    return result;
                }

                // 生成归档文件名
                // Generate archive file name
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var archiveFileName = $"{Path.GetFileNameWithoutExtension(logFilePath)}_{timestamp}{Path.GetExtension(logFilePath)}";
                var archivePath = Path.Combine(_logDirectory, "archive", archiveFileName);

                // 确保归档目录存在
                // Ensure archive directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(archivePath)!);

                // 移动文件到归档目录
                // Move file to archive directory
                File.Move(logFilePath, archivePath);

                // 如果启用压缩，压缩归档文件
                // If compression enabled, compress archived file
                if (_configuration.EnableCompression)
                {
                    var compressedPath = $"{archivePath}.gz";
                    await CompressFileAsync(archivePath, compressedPath);
                    File.Delete(archivePath);

                    result.ArchivedFilePath = compressedPath;
                    result.CompressedSize = new FileInfo(compressedPath).Length;
                }
                else
                {
                    result.ArchivedFilePath = archivePath;
                }

                result.Success = true;
                _logger?.LogInformation($"Log file rotated successfully: {logFilePath} -> {result.ArchivedFilePath}");
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                _logger?.LogError(ex, $"Failed to rotate log file: {logFilePath}");
            }

            return result;
        }

        /// <summary>
        /// 压缩文件
        /// Compresses file
        /// </summary>
        /// <param name="sourceFilePath">源文件路径 Source file path</param>
        /// <param name="destinationFilePath">目标文件路径 Destination file path</param>
        /// <returns>压缩任务 Compression task</returns>
        private async Task CompressFileAsync(string sourceFilePath, string destinationFilePath)
        {
            using var sourceStream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var destinationStream = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var compressionStream = new GZipStream(destinationStream, CompressionLevel.Optimal);

            await sourceStream.CopyToAsync(compressionStream);
        }

        /// <summary>
        /// 清理过期的日志文件
        /// Cleans up expired log files
        /// </summary>
        /// <returns>清理结果 Cleanup result</returns>
        public async Task<LogCleanupResult> CleanupExpiredLogsAsync()
        {
            var result = new LogCleanupResult
            {
                CleanupTime = DateTime.UtcNow,
                Success = false
            };

            try
            {
                var cutoffDate = DateTime.UtcNow.Subtract(_configuration.RetentionPeriod);
                var archiveDirectory = Path.Combine(_logDirectory, "archive");

                if (!Directory.Exists(archiveDirectory))
                {
                    result.Success = true;
                    result.Message = "No archive directory found";
                    return result;
                }

                var expiredFiles = Directory.GetFiles(archiveDirectory)
                    .Select(f => new FileInfo(f))
                    .Where(fi => fi.CreationTimeUtc < cutoffDate)
                    .ToList();

                foreach (var file in expiredFiles)
                {
                    try
                    {
                        result.DeletedSize += file.Length;
                        file.Delete();
                        result.DeletedFileCount++;

                        _logger?.LogDebug($"Deleted expired log file: {file.Name}");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, $"Failed to delete expired log file: {file.Name}");
                        result.FailedFileCount++;
                    }
                }

                result.Success = true;
                result.Message = $"Cleaned up {result.DeletedFileCount} expired log files";

                _logger?.LogInformation($"Log cleanup completed: {result.DeletedFileCount} files deleted, {result.DeletedSize / 1024.0 / 1024.0:F2} MB freed");
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                _logger?.LogError(ex, "Failed to cleanup expired logs");
            }

            await Task.CompletedTask;
            return result;
        }

        /// <summary>
        /// 获取日志存储统计信息
        /// Gets log storage statistics
        /// </summary>
        /// <returns>统计信息 Statistics</returns>
        public async Task<LogStorageStatistics> GetStorageStatisticsAsync()
        {
            var statistics = new LogStorageStatistics
            {
                CollectionTime = DateTime.UtcNow,
                LogDirectory = _logDirectory
            };

            try
            {
                if (!Directory.Exists(_logDirectory))
                {
                    return statistics;
                }

                // 统计当前日志文件
                // Count current log files
                var currentLogFiles = Directory.GetFiles(_logDirectory, "*.log", SearchOption.TopDirectoryOnly);
                statistics.CurrentLogFileCount = currentLogFiles.Length;
                statistics.CurrentLogSize = currentLogFiles.Sum(f => new FileInfo(f).Length);

                // 统计归档文件
                // Count archived files
                var archiveDirectory = Path.Combine(_logDirectory, "archive");
                if (Directory.Exists(archiveDirectory))
                {
                    var archivedFiles = Directory.GetFiles(archiveDirectory, "*.*", SearchOption.AllDirectories);
                    statistics.ArchivedFileCount = archivedFiles.Length;
                    statistics.ArchivedSize = archivedFiles.Sum(f => new FileInfo(f).Length);
                }

                statistics.TotalSize = statistics.CurrentLogSize + statistics.ArchivedSize;
                statistics.TotalFileCount = statistics.CurrentLogFileCount + statistics.ArchivedFileCount;

                // 查找最旧的日志文件
                // Find oldest log file
                var allFiles = Directory.GetFiles(_logDirectory, "*.*", SearchOption.AllDirectories)
                    .Select(f => new FileInfo(f))
                    .OrderBy(fi => fi.CreationTimeUtc)
                    .ToList();

                if (allFiles.Any())
                {
                    statistics.OldestLogFileDate = allFiles.First().CreationTimeUtc;
                    statistics.NewestLogFileDate = allFiles.Last().CreationTimeUtc;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to collect log storage statistics");
            }

            await Task.CompletedTask;
            return statistics;
        }

        /// <summary>
        /// 检查并执行必要的维护操作
        /// Checks and performs necessary maintenance operations
        /// </summary>
        /// <returns>维护结果 Maintenance result</returns>
        public async Task<LogMaintenanceResult> PerformMaintenanceAsync()
        {
            var result = new LogMaintenanceResult
            {
                MaintenanceTime = DateTime.UtcNow
            };

            try
            {
                // 1. 轮换大文件
                // 1. Rotate large files
                var logFiles = Directory.GetFiles(_logDirectory, "*.log", SearchOption.TopDirectoryOnly);
                foreach (var logFile in logFiles)
                {
                    var rotationResult = await RotateLogAsync(logFile);
                    if (rotationResult.Success && rotationResult.ArchivedFilePath != null)
                    {
                        result.RotatedFileCount++;
                    }
                }

                // 2. 清理过期文件
                // 2. Cleanup expired files
                var cleanupResult = await CleanupExpiredLogsAsync();
                result.DeletedFileCount = cleanupResult.DeletedFileCount;
                result.FreedSpace = cleanupResult.DeletedSize;

                // 3. 收集统计信息
                // 3. Collect statistics
                result.Statistics = await GetStorageStatisticsAsync();

                result.Success = true;
                result.Message = $"Maintenance completed: {result.RotatedFileCount} files rotated, {result.DeletedFileCount} files deleted";

                _logger?.LogInformation(result.Message);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                result.Success = false;
                _logger?.LogError(ex, "Failed to perform log maintenance");
            }

            return result;
        }

        /// <summary>
        /// 定时清理回调
        /// Cleanup timer callback
        /// </summary>
        /// <param name="state">状态 State</param>
        private void OnCleanupTimer(object? state)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await PerformMaintenanceAsync();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error during automatic maintenance");
                }
            });
        }

        /// <summary>
        /// 释放资源
        /// Disposes resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源的具体实现
        /// Actual implementation of resource disposal
        /// </summary>
        /// <param name="disposing">是否正在释放托管资源 Whether disposing managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _cleanupTimer?.Dispose();
                }

                _disposed = true;
            }
        }
    }

    /// <summary>
    /// 日志存储配置
    /// Log storage configuration
    /// </summary>
    public class LogStorageConfiguration
    {
        /// <summary>
        /// 最大日志文件大小（字节）
        /// Maximum log file size (bytes)
        /// </summary>
        public long MaxLogFileSize { get; set; } = 10 * 1024 * 1024; // 10 MB

        /// <summary>
        /// 日志保留期
        /// Log retention period
        /// </summary>
        public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(30);

        /// <summary>
        /// 是否启用压缩
        /// Whether to enable compression
        /// </summary>
        public bool EnableCompression { get; set; } = true;

        /// <summary>
        /// 是否启用自动清理
        /// Whether to enable automatic cleanup
        /// </summary>
        public bool EnableAutomaticCleanup { get; set; } = true;

        /// <summary>
        /// 清理间隔
        /// Cleanup interval
        /// </summary>
        public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(24);

        /// <summary>
        /// 最大存储大小（字节）
        /// Maximum storage size (bytes)
        /// </summary>
        public long MaxStorageSize { get; set; } = 100 * 1024 * 1024; // 100 MB
    }

    /// <summary>
    /// 日志轮换结果
    /// Log rotation result
    /// </summary>
    public class LogRotationResult
    {
        public string FilePath { get; set; } = string.Empty;
        public string? ArchivedFilePath { get; set; }
        public DateTime RotationTime { get; set; }
        public long OriginalSize { get; set; }
        public long CompressedSize { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// 日志清理结果
    /// Log cleanup result
    /// </summary>
    public class LogCleanupResult
    {
        public DateTime CleanupTime { get; set; }
        public int DeletedFileCount { get; set; }
        public int FailedFileCount { get; set; }
        public long DeletedSize { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// 日志存储统计信息
    /// Log storage statistics
    /// </summary>
    public class LogStorageStatistics
    {
        public DateTime CollectionTime { get; set; }
        public string LogDirectory { get; set; } = string.Empty;
        public int CurrentLogFileCount { get; set; }
        public long CurrentLogSize { get; set; }
        public int ArchivedFileCount { get; set; }
        public long ArchivedSize { get; set; }
        public int TotalFileCount { get; set; }
        public long TotalSize { get; set; }
        public DateTime? OldestLogFileDate { get; set; }
        public DateTime? NewestLogFileDate { get; set; }

        public double TotalSizeMB => TotalSize / 1024.0 / 1024.0;
        public double CurrentLogSizeMB => CurrentLogSize / 1024.0 / 1024.0;
        public double ArchivedSizeMB => ArchivedSize / 1024.0 / 1024.0;
    }

    /// <summary>
    /// 日志维护结果
    /// Log maintenance result
    /// </summary>
    public class LogMaintenanceResult
    {
        public DateTime MaintenanceTime { get; set; }
        public int RotatedFileCount { get; set; }
        public int DeletedFileCount { get; set; }
        public long FreedSpace { get; set; }
        public LogStorageStatistics? Statistics { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }

        public double FreedSpaceMB => FreedSpace / 1024.0 / 1024.0;
    }
}
