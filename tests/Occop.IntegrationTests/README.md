# Occop 集成测试

本项目包含 Occop 应用程序的集成测试，用于验证系统组件之间的正确交互。

## 项目结构

```
Occop.IntegrationTests/
├── Infrastructure/              # 测试基础设施
│   ├── IntegrationTestContext.cs   # 测试上下文和基类
│   ├── TestDataGenerator.cs        # 测试数据生成器
│   └── TestHelper.cs                # 测试助手方法
├── Authentication/              # 认证集成测试
│   └── AuthenticationManagerIntegrationTests.cs
├── Security/                   # 安全集成测试
│   └── SecurityManagerIntegrationTests.cs
├── CrossCutting/              # 跨组件集成测试
│   └── AuthenticationSecurityIntegrationTests.cs
├── UI/                        # UI集成测试（待实现）
└── Reports/                   # 测试报告生成
    └── TestReportGenerator.cs
```

## 测试策略

### 集成测试原则

1. **使用真实组件**：不使用 Mock，所有测试都使用真实的服务实例
2. **完整的服务容器**：每个测试都有完整配置的依赖注入容器
3. **独立测试环境**：每个测试类都有独立的测试上下文
4. **自动清理**：测试完成后自动清理资源

### 测试分类

1. **组件测试**：测试单个组件的完整功能
   - Authentication: 认证管理器测试
   - Security: 安全管理器测试

2. **集成测试**：测试多个组件的协同工作
   - CrossCutting: 认证与安全系统集成

3. **端到端测试**：测试完整的用户工作流
   - 待实现

## 使用方法

### 运行所有测试

```bash
dotnet test
```

### 运行特定测试类

```bash
dotnet test --filter "FullyQualifiedName~AuthenticationManagerIntegrationTests"
```

### 运行特定测试

```bash
dotnet test --filter "FullyQualifiedName~AuthenticationManager_ShouldBeInitializedCorrectly"
```

## 编写新测试

### 1. 继承测试基类

```csharp
public class MyIntegrationTests : IntegrationTestBase
{
    // 测试方法
}
```

### 2. 使用测试助手

```csharp
[Fact]
public async Task MyTest()
{
    // 使用 Helper 进行常见操作
    var securityManager = await Helper.InitializeSecurityManagerAsync();

    // 使用 DataGenerator 生成测试数据
    var testData = DataGenerator.GenerateSecureString();

    // 使用 Logger 记录测试信息
    Logger.LogInformation("Test started");
}
```

### 3. 获取服务

```csharp
var authManager = GetService<AuthenticationManager>();
var securityManager = GetService<ISecurityManager>();
```

## 测试数据生成

TestDataGenerator 提供了多种测试数据生成方法：

- `GenerateId()`: 生成唯一ID
- `GenerateUsername()`: 生成用户名
- `GenerateAccessToken()`: 生成访问令牌
- `GenerateSecureString()`: 生成 SecureString
- `GenerateSecurityContext()`: 生成安全上下文
- 更多...

## 测试助手方法

TestHelper 提供了常用的测试辅助方法：

### 安全管理器助手
- `InitializeSecurityManagerAsync()`: 初始化安全管理器
- `StoreSecureDataAsync()`: 存储安全数据
- `RetrieveSecureDataAsync()`: 检索安全数据
- `ValidateSecurityStateAsync()`: 验证安全状态

### 认证助手
- `PrepareAuthenticationAsync()`: 准备认证流程
- `ValidateAuthenticationState()`: 验证认证状态
- `ValidateAuthenticationFailure()`: 验证认证失败

### 文件和目录助手
- `CreateTempTestFile()`: 创建临时测试文件
- `CreateTempTestDirectory()`: 创建临时测试目录
- `CleanupTempFile()`: 清理临时文件
- `CleanupTempDirectory()`: 清理临时目录

### 等待和重试助手
- `WaitForConditionAsync()`: 等待条件满足
- `RetryAsync()`: 重试操作直到成功

### 断言助手
- `AssertThrowsAsync()`: 验证异常被抛出
- `AssertDoesNotThrowAsync()`: 验证操作不抛出异常

### 性能测量助手
- `MeasureAsync()`: 测量操作执行时间
- `AssertCompletesWithinAsync()`: 验证操作在指定时间内完成

## 测试报告

测试完成后可以生成多种格式的报告：

- 文本格式：`TestReportGenerator.GenerateTextReport()`
- Markdown 格式：`TestReportGenerator.GenerateMarkdownReport()`
- HTML 格式：`TestReportGenerator.GenerateHtmlReport()`

## 最佳实践

1. **清晰的测试名称**：使用描述性的测试方法名
2. **充分的日志记录**：使用 Logger 记录关键测试步骤
3. **详细的断言**：使用 FluentAssertions 的 because 参数
4. **资源清理**：确保测试后清理临时资源
5. **独立性**：每个测试应该独立运行，不依赖其他测试

## 配置

测试使用内存配置，默认值在 `IntegrationTestContext.BuildConfiguration()` 中设置：

```csharp
"Logging:LogLevel:Default" = "Information"
"Authentication:MaxFailedAttempts" = "3"
"Authentication:LockoutDurationMinutes" = "15"
"Security:EnableAutoCleanup" = "true"
```

可以通过创建 `appsettings.test.json` 文件覆盖这些设置。

## 故障排查

### 测试失败

1. 检查日志输出，了解失败原因
2. 确认所有依赖服务都已正确注册
3. 验证测试数据的有效性
4. 检查是否有资源未正确清理

### 性能问题

1. 使用性能测量助手方法识别瓶颈
2. 检查是否有不必要的等待或延迟
3. 验证并发操作的线程安全性

## 贡献指南

添加新测试时请遵循以下准则：

1. 继承 `IntegrationTestBase` 基类
2. 使用 `TestHelper` 和 `TestDataGenerator`
3. 添加充分的日志和断言
4. 确保测试可重复运行
5. 更新本 README 文档

## 相关文档

- [架构设计文档](../../docs/architecture/)
- [测试策略文档](../../docs/testing/)
- [API 文档](../../docs/api/)
