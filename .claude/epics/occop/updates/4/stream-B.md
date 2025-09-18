# Issue #4 - Stream B 进度更新

## 基本信息
- **Stream**: B - Shell环境检测
- **负责人**: Claude Code Agent
- **开始时间**: 2025-09-19
- **状态**: 🔄 进行中

## 工作范围
- ShellDetector基础类和接口定义
- PowerShellDetector专用检测器实现
- GitBashDetector专用检测器实现
- ShellInfo专用模型类
- Shell环境优先级选择逻辑
- Shell可用性测试和版本验证

## 依赖关系
- ✅ **Stream A已完成**: IEnvironmentDetector接口、EnvironmentInfo模型、DetectionResult模型
- ✅ **可用接口**: 基础环境检测框架和缓存机制
- 🔄 **当前任务**: 重构为更模块化的Shell专用检测架构

## 计划的工作

### 📋 1. 数据模型重构
- [ ] 创建ShellInfo模型类 - 继承EnvironmentInfo，添加Shell特定属性
- [ ] 支持Shell类型、启动参数、交互模式等属性
- [ ] 实现Shell兼容性检查和配置验证

### 📋 2. 检测器架构重构
- [ ] 创建ShellDetector基础类 - 提供Shell检测的公共功能
- [ ] 实现PowerShellDetector - 专门处理PowerShell 5.1和Core的检测
- [ ] 实现GitBashDetector - 专门处理Git Bash的检测和配置

### 📋 3. 检测功能增强
- [ ] PowerShell注册表检测优化（多版本支持）
- [ ] PowerShell PATH扫描增强（版本筛选）
- [ ] Git Bash安装路径智能搜索
- [ ] Shell启动测试和响应性验证

### 📋 4. 优先级和选择逻辑
- [ ] Shell环境评分算法（性能、兼容性、功能）
- [ ] 用户偏好集成（配置文件支持）
- [ ] 动态优先级调整（基于检测结果）

### 📋 5. 测试和验证
- [ ] 单元测试覆盖所有Shell检测器
- [ ] 集成测试验证检测准确性
- [ ] 性能测试确保检测效率

## 技术实现计划

### ShellInfo模型设计
```csharp
public class ShellInfo : EnvironmentInfo
{
    public ShellType ShellType { get; set; }
    public string[] StartupParameters { get; set; }
    public bool SupportsInteractiveMode { get; set; }
    public string ConfigurationPath { get; set; }
    public Dictionary<string, string> EnvironmentVariables { get; set; }
}
```

### 检测器架构
```csharp
public abstract class ShellDetector
{
    protected abstract Task<ShellInfo> DetectShellAsync();
    protected virtual async Task<bool> TestShellResponsivenessAsync(string shellPath);
    protected virtual async Task<string> GetShellConfigurationAsync(string shellPath);
}

public class PowerShellDetector : ShellDetector
{
    public async Task<ShellInfo> DetectPowerShell51Async();
    public async Task<ShellInfo> DetectPowerShellCoreAsync();
}

public class GitBashDetector : ShellDetector
{
    public async Task<ShellInfo> DetectGitBashAsync();
    private async Task<string> FindGitInstallationAsync();
}
```

## 文件结构
```
/src/Models/Environment/
├── EnvironmentInfo.cs        # 基础环境信息（Stream A已完成）
├── DetectionResult.cs        # 检测结果（Stream A已完成）
└── ShellInfo.cs              # Shell专用信息模型 [新增]

/src/Services/Environment/
├── IEnvironmentDetector.cs   # 环境检测接口（Stream A已完成）
├── EnvironmentDetector.cs    # 核心检测器（Stream A已完成，需要重构）
├── ShellDetector.cs          # Shell检测基础类 [新增]
├── PowerShellDetector.cs     # PowerShell专用检测器 [新增]
└── GitBashDetector.cs        # Git Bash专用检测器 [新增]

/tests/Occop.Core.Tests/Services/Environment/
├── EnvironmentDetectorTests.cs  # 现有测试（Stream A已完成）
├── ShellDetectorTests.cs        # Shell检测器测试 [新增]
├── PowerShellDetectorTests.cs   # PowerShell检测器测试 [新增]
└── GitBashDetectorTests.cs      # Git Bash检测器测试 [新增]
```

## 对其他Stream的接口提供

### 新增接口
```csharp
// Shell专用检测接口
public interface IShellDetector
{
    Task<ShellInfo> DetectShellAsync(ShellType shellType);
    Task<List<ShellInfo>> DetectAllShellsAsync();
    Task<ShellInfo> GetOptimalShellAsync(ShellRequirements requirements);
}

// Shell评估和选择
public interface IShellSelector
{
    Task<ShellInfo> SelectBestShellAsync(IEnumerable<ShellInfo> availableShells);
    double CalculateShellScore(ShellInfo shell, ShellRequirements requirements);
}
```

### 向后兼容
- 保持现有的IEnvironmentDetector接口不变
- EnvironmentDetector将内部使用新的Shell检测器
- 确保现有的调用代码无需修改

## 协调说明
- **等待状态**: 无，Stream A已完成所有依赖接口
- **共享文件**: EnvironmentDetector.cs需要重构，但保持接口兼容性
- **提交策略**: 增量提交，每完成一个检测器就提交
- **测试策略**: 与现有测试并行，确保功能正确性

## 性能目标
- **检测速度**: 单个Shell检测 < 500ms
- **并行检测**: 所有Shell检测 < 1秒
- **内存使用**: 每个检测器 < 1MB内存占用
- **缓存效率**: 98%以上的缓存命中率

---

**下一步**: 开始创建ShellInfo模型类