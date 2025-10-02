# Occop 测试自动化指南

## 概述

本文档描述了Occop项目的测试自动化策略、工具和最佳实践。

## 测试架构

### 测试金字塔

```
                    /\
                   /  \
                  / UI \
                 /______\
                /        \
               /  集成测  \
              /____________\
             /              \
            /    单元测试     \
           /________________  \
```

我们的测试策略遵循测试金字塔原则:
- **单元测试** (70%): 快速、独立、覆盖单个组件
- **集成测试** (20%): 验证组件之间的交互
- **端到端测试** (10%): 验证完整的用户工作流

### 测试类型

| 测试类型 | 项目 | 目的 | 运行频率 |
|---------|------|------|----------|
| 单元测试 | Occop.Tests | 验证单个组件功能 | 每次提交 |
| 集成测试 | Occop.IntegrationTests | 验证组件协作 | 每次提交 |
| 性能测试 | Occop.PerformanceTests | 监控性能指标 | 每次提交 |
| 安全测试 | Occop.SecurityTests | 检测安全漏洞 | 每次提交 |
| 稳定性测试 | Occop.StabilityTests | 长期运行测试 | 每日 |

## 测试运行器

### Occop.TestRunner

自动化测试运行器,用于统一管理和执行所有类型的测试。

#### 功能特性

- ✅ 支持多种测试类型 (单元、集成、性能、安全、稳定性)
- ✅ 并行执行支持
- ✅ 测试覆盖率收集
- ✅ 多格式报告生成 (Text, Markdown, HTML, JSON)
- ✅ 灵活的配置选项
- ✅ CI/CD集成

#### 使用方法

##### 基本用法

```bash
# 运行所有测试
dotnet run --project tests/Occop.TestRunner

# 运行特定类型的测试
dotnet run --project tests/Occop.TestRunner -- --types Unit,Integration

# 仅运行安全测试
dotnet run --project tests/Occop.TestRunner -- --types Security
```

##### 高级选项

```bash
# 串行运行测试
dotnet run --project tests/Occop.TestRunner -- --parallel false

# 设置最大并行度
dotnet run --project tests/Occop.TestRunner -- --max-parallelism 4

# 禁用覆盖率收集
dotnet run --project tests/Occop.TestRunner -- --coverage false

# 使用测试过滤器
dotnet run --project tests/Occop.TestRunner -- --filter "FullyQualifiedName~SecurityManager"

# 快速失败模式
dotnet run --project tests/Occop.TestRunner -- --fail-fast

# 详细输出
dotnet run --project tests/Occop.TestRunner -- --verbosity detailed
```

##### 命令行参数

| 参数 | 简写 | 说明 | 默认值 |
|------|------|------|--------|
| --types | -t | 测试类型 | All |
| --parallel | -p | 并行运行 | true |
| --max-parallelism | -mp | 最大并行度 | CPU核心数 |
| --timeout | -to | 超时时间(秒) | 3600 |
| --coverage | -c | 生成覆盖率 | true |
| --filter | -f | 测试过滤器 | 无 |
| --output | -o | 输出目录 | TestResults |
| --verbosity | -v | 详细级别 | normal |
| --fail-fast | -ff | 快速失败 | false |

### 配置

测试运行器支持通过代码配置:

```csharp
var config = new TestRunConfig
{
    TestTypes = TestType.Unit | TestType.Integration,
    RunInParallel = true,
    MaxParallelism = 4,
    TimeoutSeconds = 1800,
    GenerateCoverageReport = true,
    CoverageReportFormats = new[] { "Html", "Cobertura" },
    FailFast = false,
    Filter = "Category=Critical",
    OutputDirectory = "TestResults",
    Verbosity = "normal"
};

var scheduler = new Scheduler(logger, config);
var results = await scheduler.RunAllTestsAsync();
```

## 测试报告

### 报告格式

测试运行器自动生成多种格式的报告:

#### 1. 文本报告 (TestReport.txt)

纯文本格式,适合日志记录和快速查看。

```
===============================================
           OCCOP 测试运行报告
===============================================

生成时间: 2025-10-03 12:00:00

总体摘要:
  测试总数: 156
  通过: 154 (98.72%)
  失败: 2 (1.28%)
  跳过: 0 (0.00%)
  总耗时: 00:05:23
  整体状态: ✗ 失败
```

#### 2. Markdown报告 (TestReport.md)

Markdown格式,适合GitHub/GitLab显示。

```markdown
# Occop 测试运行报告

**状态**: ✅ 通过

## 总体摘要

| 指标 | 数量 | 百分比 |
|------|------|--------|
| 测试总数 | 156 | 100% |
| ✅ 通过 | 154 | 98.72% |
| ❌ 失败 | 2 | 1.28% |
```

#### 3. HTML报告 (TestReport.html)

交互式HTML报告,包含图表和详细信息。

特点:
- 响应式设计
- 可视化进度条
- 颜色编码状态
- 详细错误信息

#### 4. JSON报告 (TestReport.json)

机器可读格式,适合集成和自动化处理。

```json
{
  "generatedAt": "2025-10-03T12:00:00Z",
  "summary": {
    "totalTests": 156,
    "passedTests": 154,
    "failedTests": 2,
    "skippedTests": 0,
    "isSuccess": false
  },
  "testResults": [...]
}
```

### 覆盖率报告

当启用覆盖率收集时,自动生成:

- **HTML报告**: `{OutputDirectory}/{TestType}_Coverage/index.html`
- **Cobertura XML**: 用于CI/CD集成
- **摘要**: 覆盖率百分比和趋势

## 持续集成 (CI/CD)

### GitHub Actions

项目包含两个主要的GitHub Actions工作流:

#### 1. CI/CD Pipeline (`.github/workflows/ci-cd.yml`)

**触发条件**:
- Push到main或develop分支
- Pull Request到main或develop分支
- 手动触发

**阶段**:

1. **Build and Unit Tests**
   - 恢复依赖
   - 构建项目
   - 运行单元测试
   - 上传测试结果

2. **Integration Tests**
   - 运行集成测试
   - 收集覆盖率
   - 上传结果

3. **Security Tests**
   - 运行安全测试
   - 检测敏感数据泄漏
   - 上传结果

4. **Performance Tests**
   - 运行性能基准测试
   - 收集性能指标
   - 上传结果

5. **Code Coverage**
   - 合并覆盖率报告
   - 生成HTML报告
   - 在PR中添加覆盖率摘要

6. **Test Report**
   - 汇总所有测试结果
   - 生成统一报告

7. **Static Analysis**
   - 代码质量检查
   - 安全扫描

8. **Deploy**
   - Staging (develop分支)
   - Production (main分支)

#### 2. Stability Tests (`.github/workflows/stability-tests.yml`)

**触发条件**:
- 每日凌晨2点自动运行
- 手动触发(可选择测试时长)

**特点**:
- 长时间运行测试 (1-24小时)
- 内存泄漏检测
- 性能退化检测
- 失败时自动创建Issue

### 本地运行CI测试

```bash
# 模拟CI环境
export CI=true

# 运行完整的CI测试流程
./scripts/run-ci-tests.sh

# 或使用测试运行器
dotnet run --project tests/Occop.TestRunner -- --types All --verbosity detailed
```

## 编写测试

### 单元测试

```csharp
using Xunit;
using FluentAssertions;

public class MyServiceTests
{
    [Fact]
    public void MyMethod_Should_ReturnExpectedResult()
    {
        // Arrange
        var service = new MyService();

        // Act
        var result = service.MyMethod();

        // Assert
        result.Should().Be("expected");
    }
}
```

### 集成测试

```csharp
using Occop.IntegrationTests.Infrastructure;

public class MyIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task Components_Should_WorkTogether()
    {
        // Arrange
        var service = GetService<IMyService>();

        // Act
        await service.DoSomethingAsync();

        // Assert
        // ...
    }
}
```

### 性能测试

```csharp
using BenchmarkDotNet.Attributes;

[MemoryDiagnoser]
public class MyBenchmarks
{
    [Benchmark]
    public void MyOperation()
    {
        // 测试代码
    }
}
```

### 安全测试

```csharp
using Occop.SecurityTests.Infrastructure;

public class MySecurityTests : SecurityTestBase
{
    [Fact]
    public async Task Operation_Should_NotLeakSensitiveData()
    {
        // 测试敏感数据不会泄漏
    }
}
```

### 稳定性测试

```csharp
[Fact(Skip = "长时间运行 - 仅在稳定性环境运行")]
[Trait("Category", "Stability")]
public async Task System_Should_RunStable_ForLongTime()
{
    // 长时间运行的稳定性测试
}
```

## 测试最佳实践

### 通用原则

1. **AAA模式**: Arrange, Act, Assert
2. **单一职责**: 每个测试只测试一个行为
3. **独立性**: 测试之间不应相互依赖
4. **可重复性**: 测试结果应该可重复
5. **快速**: 单元测试应该快速完成
6. **清晰命名**: 测试名称应该描述测试内容

### 测试命名约定

```csharp
// 推荐格式: MethodName_Scenario_ExpectedBehavior
[Fact]
public void Store_WhenDataIsValid_ShouldReturnId()
{
}

[Fact]
public void Retrieve_WhenIdNotFound_ShouldThrowException()
{
}
```

### 使用Trait组织测试

```csharp
[Trait("Category", "Security")]
[Trait("Priority", "High")]
public class SecurityTests
{
}
```

### 避免常见错误

❌ **不要**:
- 使用硬编码路径
- 依赖外部状态
- 测试实现细节而非行为
- 创建过于复杂的测试
- 忽略测试失败

✅ **应该**:
- 使用相对路径或配置
- 创建独立的测试环境
- 测试公共API和行为
- 保持测试简单明了
- 修复所有失败的测试

## 覆盖率目标

| 组件 | 目标覆盖率 | 当前覆盖率 |
|------|-----------|-----------|
| Occop.Core | 90% | - |
| Occop.Services | 85% | - |
| Occop.UI | 70% | - |
| 整体 | 80% | - |

## 性能基准

### 关键操作目标

| 操作 | 目标时间 | P95 | P99 |
|------|---------|-----|-----|
| 认证初始化 | < 100ms | < 150ms | < 200ms |
| 数据加密 | < 50ms | < 75ms | < 100ms |
| 数据解密 | < 50ms | < 75ms | < 100ms |
| 安全存储 | < 100ms | < 150ms | < 200ms |

### 内存使用目标

- 初始内存: < 100MB
- 稳态内存: < 200MB
- 24小时内存增长: < 20%

## 故障排查

### 常见问题

#### 测试运行器找不到测试项目

**问题**: 测试运行器报告找不到测试项目

**解决方案**:
```bash
# 检查项目路径
ls tests/Occop.IntegrationTests/Occop.IntegrationTests.csproj

# 确保项目已构建
dotnet build tests/Occop.IntegrationTests/Occop.IntegrationTests.csproj
```

#### 覆盖率报告生成失败

**问题**: ReportGenerator未安装

**解决方案**:
```bash
dotnet tool install --global dotnet-reportgenerator-globaltool
```

#### 并行测试导致资源冲突

**问题**: 多个测试同时访问相同资源

**解决方案**:
```bash
# 串行运行测试
dotnet run --project tests/Occop.TestRunner -- --parallel false
```

#### CI中测试超时

**问题**: 测试在CI环境中运行时间过长

**解决方案**:
- 增加超时时间
- 优化测试性能
- 使用并行执行
- 分离长时间运行的测试

### 调试测试

```bash
# 详细输出
dotnet test --verbosity detailed

# 收集诊断信息
dotnet test --diag diagnostics.log

# 运行特定测试
dotnet test --filter "FullyQualifiedName~MyTest"

# 使用测试运行器调试
dotnet run --project tests/Occop.TestRunner -- --verbosity diagnostic --types Integration
```

## 工具和依赖

### 测试框架

- **xUnit**: 主要测试框架
- **FluentAssertions**: 流畅的断言库
- **BenchmarkDotNet**: 性能基准测试

### 覆盖率工具

- **Coverlet**: 跨平台覆盖率收集
- **ReportGenerator**: 覆盖率报告生成

### CI/CD

- **GitHub Actions**: 持续集成和部署
- **Test Reporter**: 测试结果可视化

## 资源和参考

### 文档

- [xUnit文档](https://xunit.net/)
- [FluentAssertions文档](https://fluentassertions.com/)
- [BenchmarkDotNet文档](https://benchmarkdotnet.org/)
- [Coverlet文档](https://github.com/coverlet-coverage/coverlet)

### 内部文档

- [集成测试指南](../Occop.IntegrationTests/README.md)
- [性能测试指南](../Occop.PerformanceTests/README.md)
- [安全测试指南](../Occop.SecurityTests/README.md)

## 联系和支持

如有问题或建议,请:
- 创建Issue
- 联系开发团队
- 查看项目Wiki

---

**最后更新**: 2025-10-03
**版本**: 1.0.0
