# Occop Performance Tests

性能测试和基准测试套件 - 用于监控和验证Occop应用程序的性能。

## 概述

本项目包含两个主要部分：

1. **性能监控系统**：用于运行时性能追踪、监控和警报
2. **基准测试套件**：使用BenchmarkDotNet进行详细的性能基准测试

## 性能监控系统

### 核心组件

#### PerformanceMonitor
性能监控器，用于追踪操作的执行时间和统计信息。

```csharp
var monitor = new PerformanceMonitor(logger);

// 使用using语句自动计时
using (var timer = monitor.BeginOperation("DataProcessing", "Database"))
{
    // 执行操作
    ProcessData();
}

// 手动记录操作
monitor.RecordOperation("CacheRead", 150, success: true);

// 获取统计信息
var stats = monitor.GetStatistics("DataProcessing");
Console.WriteLine($"Average: {stats.AverageDurationMs:F2} ms");

// 检测性能降级
if (monitor.DetectDegradation("DataProcessing", threshold: 20.0))
{
    Console.WriteLine("Performance degradation detected!");
}
```

#### MemoryAnalyzer
内存分析器，用于分析内存使用情况并检测内存泄漏。

```csharp
var analyzer = new MemoryAnalyzer(logger);

// 分析当前内存快照
var snapshot = monitor.GetMemorySnapshot();
var analysis = analyzer.Analyze(snapshot);

foreach (var issue in analysis.Issues)
{
    Console.WriteLine($"{issue.Severity}: {issue.Description}");
    Console.WriteLine($"Recommendation: {issue.Recommendation}");
}

// 检测内存泄漏
var snapshots = CollectSnapshotsOverTime();
if (analyzer.DetectMemoryLeak(snapshots, threshold: 10.0))
{
    Console.WriteLine("Memory leak detected!");
}

// 生成趋势报告
var report = analyzer.GenerateTrendReport(snapshots);
Console.WriteLine($"Memory trend: {report.ManagedHeapTrendMBPerHour:F2} MB/hour");
```

#### PerformanceAlertManager
性能警报管理器，自动监控性能并触发警报。

```csharp
var config = new PerformanceAlertConfig
{
    Enabled = true,
    DegradationThreshold = 20.0,      // 20% 性能降级
    HighMemoryThresholdMB = 500.0,    // 500 MB 内存警告
    MemoryLeakThreshold = 10.0,        // 10% 内存增长
    CheckIntervalSeconds = 60          // 每60秒检查一次
};

var alertManager = new PerformanceAlertManager(monitor, analyzer, config, logger);

// 订阅警报事件
alertManager.AlertRaised += (sender, args) =>
{
    Console.WriteLine($"[{args.Severity}] {args.AlertType}: {args.Message}");

    foreach (var detail in args.Details)
    {
        Console.WriteLine($"  {detail.Key}: {detail.Value}");
    }
};

// 立即检查（也会自动定期检查）
alertManager.CheckNow();
```

#### PerformanceReportGenerator
性能报告生成器，生成多种格式的性能报告。

```csharp
var generator = new PerformanceReportGenerator(monitor, analyzer);

// 生成Markdown报告
var markdownReport = generator.GenerateReport(ReportFormat.Markdown);
Console.WriteLine(markdownReport);

// 保存为HTML文件
await generator.SaveReportAsync("performance-report.html", ReportFormat.Html);

// 生成JSON报告用于API
var jsonReport = generator.GenerateReport(ReportFormat.Json);
```

## 基准测试套件

### 运行基准测试

#### 交互式菜单
```bash
dotnet run --project tests/Occop.PerformanceTests
```

#### 运行所有基准测试
```bash
dotnet run --project tests/Occop.PerformanceTests -- --all
```

#### 运行特定基准测试
```bash
# 性能监控器基准测试
dotnet run --project tests/Occop.PerformanceTests -- monitor

# 内存分析器基准测试
dotnet run --project tests/Occop.PerformanceTests -- memory

# CPU基准测试
dotnet run --project tests/Occop.PerformanceTests -- cpu

# 内存操作基准测试
dotnet run --project tests/Occop.PerformanceTests -- memalloc

# 磁盘I/O基准测试
dotnet run --project tests/Occop.PerformanceTests -- disk
```

### 可用的基准测试

#### 1. PerformanceMonitorBenchmarks
测试性能监控系统本身的开销。

- `BeginOperation_Overhead`: 测试创建操作计时器的开销
- `RecordOperation_WithMetadata`: 测试记录带元数据的操作
- `RecordOperation_WithoutMetadata`: 测试记录不带元数据的操作
- `GetStatistics`: 测试获取统计信息的性能
- `DetectDegradation`: 测试性能降级检测
- `RecordMemoryUsage`: 测试内存快照记录
- `ConcurrentOperations`: 测试并发操作的性能

#### 2. MemoryAnalyzerBenchmarks
测试内存分析功能的性能。

- `Analyze_Snapshot`: 分析单个内存快照
- `Compare_Snapshots`: 比较两个内存快照
- `DetectMemoryLeak`: 内存泄漏检测
- `GenerateTrendReport`: 生成趋势报告
- `TriggerGCAndSnapshot`: GC触发和快照

#### 3. CpuBenchmarks
测试CPU密集型操作的性能。

- `SHA256_Hashing`: SHA256哈希计算
- `AES_Encryption`: AES加密
- `Fibonacci_Calculation`: 斐波那契数列计算
- `String_Concatenation`: 字符串拼接
- `StringBuilder_Concatenation`: StringBuilder拼接
- `LINQ_Operations`: LINQ操作

#### 4. MemoryBenchmarks
测试内存分配和操作的性能。

参数化测试：
- `AllocationCount`: 100, 1000, 10000
- `AllocationSize`: 1KB, 10KB, 100KB

测试项：
- `Array_Allocation`: 数组分配
- `List_Allocation`: List分配
- `Dictionary_Allocation`: Dictionary分配
- `ArrayPool_Usage`: ArrayPool使用
- `Span_Operations`: Span操作

#### 5. DiskIoBenchmarks
测试磁盘I/O操作的性能。

参数化测试：
- `DataSize`: 1KB, 10KB, 100KB, 1MB

测试项：
- `File_Write_Sync`: 同步文件写入
- `File_Write_Async`: 异步文件写入
- `FileStream_Write`: FileStream写入
- `File_Read_Sync`: 同步文件读取
- `File_Read_Async`: 异步文件读取
- `FileStream_Read`: FileStream读取

## 集成测试

运行集成测试以验证性能监控系统的正确性：

```bash
dotnet test tests/Occop.PerformanceTests
```

### 测试覆盖

- **PerformanceMonitor**: 操作追踪、降级检测、并发安全性
- **MemoryAnalyzer**: 快照分析、泄漏检测、趋势报告
- **PerformanceAlertManager**: 警报触发、配置管理
- **PerformanceReportGenerator**: 多格式报告生成
- **OperationTimer**: 检查点追踪

## 报告输出

基准测试会生成以下报告：

- **BenchmarkDotNet.Artifacts/results/**
  - `*.html`: HTML格式的详细报告
  - `*.md`: Markdown格式的报告（适合GitHub）
  - `*.csv`: CSV格式的原始数据

性能报告支持以下格式：
- **Text**: 纯文本格式
- **Markdown**: Markdown格式（推荐用于文档）
- **HTML**: HTML格式（推荐用于查看）
- **JSON**: JSON格式（推荐用于API集成）

## 最佳实践

### 1. 使用性能监控

```csharp
// 在关键操作中使用性能监控
public async Task<Result> ProcessDataAsync(Data data)
{
    using var timer = _performanceMonitor.BeginOperation(
        "ProcessData",
        "DataProcessing"
    );

    timer.Checkpoint("Validation");
    await ValidateAsync(data);

    timer.Checkpoint("Processing");
    var result = await ProcessAsync(data);

    timer.Checkpoint("Persistence");
    await SaveAsync(result);

    return result;
}
```

### 2. 定期收集内存快照

```csharp
// 在应用程序中定期记录内存使用情况
var memoryTimer = new System.Threading.Timer(_ =>
{
    _performanceMonitor.RecordMemoryUsage();
}, null, TimeSpan.Zero, TimeSpan.FromMinutes(5));
```

### 3. 配置性能警报

```csharp
// 在应用程序启动时配置警报
var alertConfig = new PerformanceAlertConfig
{
    Enabled = configuration.GetValue<bool>("Performance:AlertsEnabled"),
    DegradationThreshold = configuration.GetValue<double>("Performance:DegradationThreshold"),
    HighMemoryThresholdMB = configuration.GetValue<double>("Performance:MemoryThresholdMB"),
    CheckIntervalSeconds = configuration.GetValue<int>("Performance:CheckIntervalSeconds")
};

var alertManager = new PerformanceAlertManager(
    performanceMonitor,
    memoryAnalyzer,
    alertConfig,
    logger
);

alertManager.AlertRaised += HandlePerformanceAlert;
```

### 4. 生成定期报告

```csharp
// 每天生成性能报告
var reportTimer = new System.Threading.Timer(async _ =>
{
    var generator = new PerformanceReportGenerator(_monitor, _analyzer);
    var reportPath = $"reports/performance-{DateTime.Now:yyyyMMdd}.html";
    await generator.SaveReportAsync(reportPath, ReportFormat.Html);
}, null, TimeSpan.Zero, TimeSpan.FromDays(1));
```

## 性能目标

基于基准测试，以下是推荐的性能目标：

| 操作 | 目标 | 警告阈值 |
|------|------|----------|
| 性能监控开销 | < 1ms | N/A |
| 内存快照 | < 10ms | N/A |
| 数据处理操作 | < 100ms | > 200ms |
| 数据库查询 | < 50ms | > 100ms |
| 文件I/O (1MB) | < 50ms | > 100ms |
| 工作集内存 | < 500MB | > 750MB |
| 托管堆 | < 200MB | > 300MB |

## 故障排查

### 基准测试运行缓慢
- 确保以Release模式运行：`dotnet run -c Release`
- 关闭其他占用资源的程序
- 检查是否有防病毒软件干扰

### 内存泄漏误报
- 调整`MemoryLeakThreshold`参数
- 增加快照数量以获得更准确的趋势
- 在测试期间强制GC

### 性能降级误报
- 调整`DegradationThreshold`参数
- 确保有足够的样本数据（至少20次执行）
- 考虑操作的自然变化性

## 贡献

性能测试和监控是持续改进的过程。建议：

1. 为新功能添加相应的基准测试
2. 定期运行基准测试并记录结果
3. 在代码审查中关注性能影响
4. 使用性能监控识别瓶颈

## 许可证

与主项目相同
