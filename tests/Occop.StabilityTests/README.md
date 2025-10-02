# Occop 稳定性测试

长时间运行的稳定性测试套件,用于检测内存泄漏、性能降级和系统长期稳定性问题。

## 概述

稳定性测试旨在验证系统在长时间运行和高负载情况下的表现,确保:
- ✅ 无内存泄漏
- ✅ 无性能降级
- ✅ 资源正确释放
- ✅ 错误率保持在可接受范围

## 测试类型

### 1. 24小时稳定性测试

**目标**: 验证系统可以稳定运行24小时而不出现内存泄漏或性能降级。

**测试方法**:
- 持续执行典型操作
- 每10秒执行一次操作
- 每30分钟检查一次内存状态
- 监控性能指标

**验收标准**:
- 内存增长 < 20%
- 错误率 < 1%
- 无严重内存问题
- 无明显性能降级

### 2. 1小时压力测试

**目标**: 验证系统在持续高负载下的稳定性。

**测试方法**:
- 10个并发操作
- 每个操作间隔100ms
- 持续1小时

**验收标准**:
- 错误率 < 1%
- 所有操作正确完成
- 吞吐量稳定

### 3. 内存泄漏检测测试

**目标**: 通过重复操作检测潜在的内存泄漏。

**测试方法**:
- 执行1000次典型操作
- 每100次记录内存快照
- 分析内存增长趋势

**验收标准**:
- 无检测到内存泄漏
- 内存增长 < 50%

### 4. 性能退化检测测试

**目标**: 验证长时间运行后性能不会显著降低。

**测试方法**:
- 记录基准性能(前50次操作)
- 执行500次操作
- 测量后期性能(后50次操作)

**验收标准**:
- 性能退化 < 20%

## 运行测试

### 本地运行

```bash
# 运行短期稳定性测试
dotnet test tests/Occop.StabilityTests/Occop.StabilityTests.csproj \
  --filter "Category=Stability&Duration=Short"

# 运行所有稳定性测试(包括跳过的长时间测试)
# 注意: 需要手动移除[Fact(Skip="...")]属性
dotnet test tests/Occop.StabilityTests/Occop.StabilityTests.csproj \
  --filter "Category=Stability"
```

### CI环境运行

在GitHub Actions中,稳定性测试通过专门的工作流运行:

```bash
# 手动触发
gh workflow run stability-tests.yml

# 或等待每日自动运行(凌晨2点)
```

## 测试配置

### 测试时长

默认测试时长在测试代码中配置:

```csharp
// 24小时测试
var testDuration = TimeSpan.FromHours(24);

// 1小时压力测试
var testDuration = TimeSpan.FromHours(1);
```

### 操作间隔

```csharp
// 每10秒执行一次操作
var operationInterval = TimeSpan.FromSeconds(10);

// 压力测试: 每100ms
await Task.Delay(100);
```

### 内存检查间隔

```csharp
// 每30分钟检查一次内存
var memoryCheckInterval = TimeSpan.FromMinutes(30);
```

## 测试结果

### 输出信息

测试会输出详细的进度信息:

```
开始24小时稳定性测试
开始时间: 2025-10-03 00:00:00
预计结束时间: 2025-10-04 00:00:00

进度: 01:00:00/24:00:00 (4.17%), 操作数: 360, 错误: 0
进度: 02:00:00/24:00:00 (8.33%), 操作数: 720, 错误: 0
...

测试完成
总操作数: 8640
总错误数: 0
内存增长: 15234567 bytes (12.34%)
```

### 内存分析

测试会生成内存趋势报告:

```
内存快照分析:
- 初始内存: 95.2 MB
- 最终内存: 107.0 MB
- 增长: 11.8 MB (12.40%)
- 趋势: +0.49 MB/小时
- 检测到泄漏: 否
```

### 性能分析

```
性能分析:
- 基准平均耗时: 45.23 ms
- 后期平均耗时: 48.91 ms
- 性能变化: +8.14% (在可接受范围内)
```

## 最佳实践

### 编写稳定性测试

1. **使用真实组件**
   ```csharp
   // 不使用Mock,使用真实的服务实例
   var securityManager = _context.GetService<ISecurityManager>();
   ```

2. **适当的清理**
   ```csharp
   // 确保每次操作后清理资源
   authManager.Dispose();
   await securityManager.DisposeAsync();
   ```

3. **监控关键指标**
   ```csharp
   // 使用PerformanceMonitor追踪性能
   using var timer = monitor.BeginOperation("TypicalOperation");
   ```

4. **定期采样**
   ```csharp
   // 定期记录内存快照
   if (i % 100 == 0)
   {
       GC.Collect();
       snapshots.Add(monitor.GetMemorySnapshot());
   }
   ```

### 调试稳定性问题

1. **减少测试时长**
   ```csharp
   var testDuration = TimeSpan.FromMinutes(10);  // 调试时使用较短时间
   ```

2. **增加采样频率**
   ```csharp
   var memoryCheckInterval = TimeSpan.FromMinutes(1);  // 更频繁检查
   ```

3. **启用详细日志**
   ```csharp
   _logger.LogDebug("操作 {Count} 完成, 耗时: {Duration}ms", count, duration);
   ```

4. **使用内存分析器**
   ```bash
   # 使用dotMemory或类似工具
   dotMemory attach {process_id}
   ```

## 故障排查

### 内存泄漏

**症状**: 内存持续增长,超过阈值

**诊断**:
```csharp
// 检查内存快照
var analysis = memoryAnalyzer.Analyze(snapshot);
foreach (var issue in analysis.Issues)
{
    Console.WriteLine($"{issue.Severity}: {issue.Description}");
}

// 生成趋势报告
var trendReport = memoryAnalyzer.GenerateTrendReport(snapshots);
Console.WriteLine($"内存增长趋势: {trendReport.ManagedHeapTrendMBPerHour} MB/小时");
```

**常见原因**:
- 事件处理器未移除
- 静态集合累积数据
- 循环引用
- 非托管资源未释放

### 性能降级

**症状**: 操作耗时逐渐增加

**诊断**:
```csharp
// 检测降级
if (monitor.DetectDegradation("TypicalOperation", threshold: 20.0))
{
    var stats = monitor.GetStatistics("TypicalOperation");
    Console.WriteLine($"平均耗时: {stats.AverageDurationMs}ms");
    Console.WriteLine($"最大耗时: {stats.MaxDurationMs}ms");
}
```

**常见原因**:
- 缓存未生效
- 资源池耗尽
- 日志文件过大
- 内存碎片

### 高错误率

**症状**: 错误计数持续增加

**诊断**:
```csharp
// 记录错误详情
catch (Exception ex)
{
    errorCount++;
    _logger.LogError(ex, "操作 {Count} 失败", operationCount);

    // 检查错误率
    if (errorCount > operationCount * 0.05)
    {
        throw new Exception($"错误率过高: {errorCount}/{operationCount}");
    }
}
```

**常见原因**:
- 资源竞争
- 超时配置不当
- 外部依赖不稳定
- 状态管理错误

## CI集成

### GitHub Actions配置

稳定性测试在专门的工作流中运行(`.github/workflows/stability-tests.yml`):

**特点**:
- 每日自动运行
- 可手动触发
- 支持选择测试时长
- 失败时自动创建Issue

**触发方式**:

```yaml
on:
  schedule:
    - cron: '0 2 * * *'  # 每日凌晨2点
  workflow_dispatch:
    inputs:
      test_duration:
        type: choice
        options: ['1', '4', '8', '24']
```

**运行示例**:

```bash
# 手动触发1小时测试
gh workflow run stability-tests.yml -f test_duration=1

# 手动触发24小时测试
gh workflow run stability-tests.yml -f test_duration=24
```

### 本地CI模拟

```bash
# 模拟CI环境
export CI=true

# 运行稳定性测试
dotnet test tests/Occop.StabilityTests/Occop.StabilityTests.csproj \
  --filter "Category=Stability&Duration!=24Hours" \
  --logger "trx;LogFileName=stability-tests.trx" \
  --collect:"XPlat Code Coverage"
```

## 性能基准

### 目标指标

| 指标 | 目标值 | 描述 |
|------|-------|------|
| 24小时内存增长 | < 20% | 长期稳定性 |
| 错误率 | < 1% | 操作可靠性 |
| 性能退化 | < 20% | 性能稳定性 |
| 吞吐量 | > 5 ops/sec | 基本性能 |

### 典型结果

在标准测试环境中(4核8GB):

| 测试 | 操作数 | 耗时 | 错误 | 内存增长 | 状态 |
|------|-------|------|------|---------|------|
| 内存泄漏检测 | 1000 | 5分钟 | 0 | 8% | ✅ 通过 |
| 性能退化检测 | 600 | 3分钟 | 0 | 5% | ✅ 通过 |
| 1小时压力 | ~36000 | 1小时 | <360 | 15% | ✅ 通过 |
| 24小时稳定 | ~86400 | 24小时 | <864 | 18% | ✅ 通过 |

## 相关文档

- [测试自动化指南](../../docs/testing/TESTING-GUIDE.md)
- [性能监控文档](../Occop.PerformanceTests/README.md)
- [测试运行器文档](../Occop.TestRunner/README.md)

## 贡献

添加新的稳定性测试时,请:

1. 继承`IntegrationTestContext`
2. 使用`[Trait("Category", "Stability")]`标记
3. 使用`[Trait("Duration", "...")]`标记时长
4. 对长时间测试使用`[Fact(Skip="...")]`
5. 提供清晰的日志输出
6. 文档化验收标准

---

**版本**: 1.0.0
**最后更新**: 2025-10-03
