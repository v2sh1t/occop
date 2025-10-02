# Occop 安全测试套件

## 概述

Occop.SecurityTests 是一个全面的安全测试套件,用于验证应用程序在各种安全场景和异常情况下的稳定性、安全性和可靠性。

## 测试类别

### 1. 敏感数据测试 (SensitiveDataTests)

测试敏感信息泄露检测和防护机制。

#### SensitiveDataScannerTests
- API密钥检测
- 密码检测
- 信用卡号检测
- JWT令牌检测
- 连接字符串检测
- 电子邮件地址检测
- 社会安全号码检测
- 私钥检测
- 自定义模式检测
- 批量扫描
- 文件和对象扫描

#### LogSensitiveDataLeakTests
- 日志中的敏感数据过滤
- API密钥过滤
- 密码过滤
- 连接字符串过滤
- JWT令牌过滤
- Bearer令牌过滤
- 邮箱部分遮蔽
- 信用卡部分遮蔽
- 多敏感项过滤
- 字典数据过滤
- 日志上下文安全
- 日志分析器安全

### 2. 异常处理测试 (ExceptionHandlingTests)

测试系统在异常情况下的行为。

#### ExceptionScenarioTests
- 内存压力处理
- 已释放对象访问
- 空输入处理
- 并发释放
- 内存不足处理
- 数据损坏验证
- 清理失败报告
- 快速操作完整性
- 线程中断清理
- 无效状态恢复
- 审计异常处理
- 安全异常触发
- 清理失败报告
- 文件系统错误处理
- 强制垃圾回收
- 多错误收集

#### ResourceLeakTests
- 多次初始化内存泄露检测
- 重复操作资源释放
- 事件处理器泄露检测
- 连续审计日志泄露
- 连续日志记录内存
- 文件句柄释放
- 定时器资源释放
- 线程资源管理
- 异常路径资源释放
- 弱引用垃圾回收
- 大对象堆管理
- 终结器执行

### 3. 并发测试 (ConcurrencyTests)

测试并发操作的线程安全性。

#### ConcurrencySafetyTests
- 并发存储操作
- 并发检索操作
- 并发清理操作
- 读写冲突处理
- 并发审计记录
- 高负载性能
- 死锁场景
- 并发字典操作
- 原子操作准确性
- 信号量资源限制
- 异步锁临界区
- 任务取消处理
- 内存屏障可见性

#### LoadStressTests
- 持续负载稳定性
- 突发负载恢复
- 大负载处理
- 内存压力适应
- 高审计量处理
- 快速创建销毁
- 混合操作扩展
- 峰值负载资源
- 最佳吞吐量
- 响应时间一致性
- 压力下错误率

### 4. 模糊测试 (FuzzingTests)

使用随机和边缘情况输入测试API的健壮性。

#### ApiFuzzingTests
- 空输入处理
- 空输入处理
- 特殊字符处理
- Unicode输入处理
- 超长输入处理
- 无效数据ID处理
- 畸形SecureString处理
- 快速随机输入
- 二进制数据处理
- 恶意模式检测
- 边缘情况扫描
- 并发模糊输入
- 无效配置处理
- 畸形审计数据

### 5. 漏洞扫描 (Infrastructure)

#### VulnerabilityScanner
检测常见安全漏洞:
- SQL注入模式
- 命令注入模式
- 路径遍历模式
- 跨站脚本(XSS)模式
- 弱加密算法
- 硬编码凭据
- 不安全的反序列化
- 未加密的敏感数据传输

#### SensitiveDataScanner
扫描敏感信息:
- 16种预定义敏感数据模式
- 自定义模式支持
- 文本、文件、目录和对象扫描
- 详细的扫描报告
- 敏感级别分类

## 使用方法

### 运行所有测试

```bash
dotnet test tests/Occop.SecurityTests/Occop.SecurityTests.csproj
```

### 运行特定类别的测试

```bash
# 敏感数据测试
dotnet test --filter "FullyQualifiedName~SensitiveDataTests"

# 异常处理测试
dotnet test --filter "FullyQualifiedName~ExceptionHandlingTests"

# 并发测试
dotnet test --filter "FullyQualifiedName~ConcurrencyTests"

# 模糊测试
dotnet test --filter "FullyQualifiedName~FuzzingTests"
```

### 使用漏洞扫描器

```csharp
var scanner = new VulnerabilityScanner();

// 扫描单个文件
var result = await scanner.ScanFileAsync("path/to/file.cs");

// 扫描目录
var results = await scanner.ScanDirectoryAsync("path/to/directory", "*.cs");

// 生成报告
var report = scanner.GenerateReport(results);

Console.WriteLine($"Total vulnerabilities: {report.TotalVulnerabilities}");
Console.WriteLine($"Critical: {report.CriticalVulnerabilities}");
Console.WriteLine($"High: {report.HighVulnerabilities}");
```

### 使用敏感数据扫描器

```csharp
var scanner = new SensitiveDataScanner();

// 扫描文本
var result = scanner.ScanText("API_KEY=secret123", "config");

if (result.ContainsSensitiveData)
{
    Console.WriteLine($"Found {result.Findings.Count} sensitive items");
    foreach (var finding in result.Findings)
    {
        Console.WriteLine($"- {finding.Type} at position {finding.Position}");
    }
}

// 扫描文件
var fileResult = await scanner.ScanFileAsync("config.json");

// 扫描目录
var dirResults = await scanner.ScanDirectoryAsync("/logs", "*.log");
```

## 测试基础设施

### SecurityTestBase

所有安全测试的基类,提供:
- 集成测试上下文管理
- 敏感数据扫描器
- 测试数据生成器
- 内存管理工具
- 临时文件管理

### VulnerabilityScanner

安全漏洞扫描器,支持:
- 预定义漏洞规则
- 自定义规则添加
- 代码、文件和目录扫描
- 详细的漏洞报告

### SensitiveDataScanner

敏感信息扫描器,支持:
- 16种预定义敏感模式
- 自定义模式支持
- 多种扫描目标
- 敏感级别分类
- 批量扫描

## 测试策略

### 1. 零泄露验证
确保系统不泄露任何敏感信息到日志、错误消息或其他输出。

### 2. 异常场景覆盖
测试系统在各种异常情况下的行为,包括:
- 内存压力
- 资源耗尽
- 并发冲突
- 网络中断
- 进程崩溃

### 3. 并发安全
验证系统在高并发场景下的线程安全性和数据一致性。

### 4. 负载测试
测试系统在持续负载和突发负载下的性能和稳定性。

### 5. 模糊测试
使用随机和边缘情况输入测试API的健壮性。

### 6. 漏洞扫描
自动检测代码中的常见安全漏洞。

## 测试覆盖

### 核心功能
- SecurityManager: 100%
- SecurityAuditor: 100%
- SensitiveDataFilter: 100%
- LoggerService: 100%

### 安全场景
- 敏感数据泄露: 20+测试
- 异常处理: 30+测试
- 并发安全: 15+测试
- 负载压力: 12+测试
- 模糊测试: 15+测试

### 漏洞类型
- 注入攻击: 3种
- XSS: 1种
- 加密: 1种
- 凭据: 1种
- 反序列化: 1种
- 数据传输: 1种

## 最佳实践

### 1. 定期运行
在每次提交前运行安全测试套件。

### 2. 持续监控
在CI/CD流水线中集成安全测试。

### 3. 漏洞扫描
定期扫描代码库,检测新引入的漏洞。

### 4. 敏感数据审查
定期审查日志和输出,确保没有敏感数据泄露。

### 5. 性能基准
建立性能基准,监控系统性能退化。

## 故障排查

### 测试失败
1. 检查测试输出的详细错误信息
2. 验证测试环境配置
3. 确认依赖项正确安装
4. 查看相关日志文件

### 内存泄露
1. 运行ResourceLeakTests
2. 使用内存分析工具
3. 检查Dispose模式实现
4. 验证事件处理器正确取消订阅

### 并发问题
1. 运行ConcurrencySafetyTests
2. 增加并发级别重现问题
3. 使用线程分析工具
4. 检查锁和同步机制

### 性能问题
1. 运行LoadStressTests
2. 分析性能瓶颈
3. 检查资源使用情况
4. 优化关键路径

## 贡献

添加新的安全测试时,请遵循以下原则:
1. 测试应该详细且可重现
2. 使用描述性的测试名称
3. 包含充分的断言
4. 记录测试目的和预期行为
5. 清理测试资源

## 许可证

本项目采用 MIT 许可证。
