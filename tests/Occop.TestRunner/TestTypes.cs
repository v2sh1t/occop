namespace Occop.TestRunner;

/// <summary>
/// 测试类型枚举
/// </summary>
[Flags]
public enum TestType
{
    /// <summary>
    /// 无
    /// </summary>
    None = 0,

    /// <summary>
    /// 单元测试
    /// </summary>
    Unit = 1 << 0,

    /// <summary>
    /// 集成测试
    /// </summary>
    Integration = 1 << 1,

    /// <summary>
    /// 性能测试
    /// </summary>
    Performance = 1 << 2,

    /// <summary>
    /// 安全测试
    /// </summary>
    Security = 1 << 3,

    /// <summary>
    /// 稳定性测试
    /// </summary>
    Stability = 1 << 4,

    /// <summary>
    /// 所有测试
    /// </summary>
    All = Unit | Integration | Performance | Security | Stability
}

/// <summary>
/// 测试运行优先级
/// </summary>
public enum TestPriority
{
    /// <summary>
    /// 低优先级
    /// </summary>
    Low = 0,

    /// <summary>
    /// 正常优先级
    /// </summary>
    Normal = 1,

    /// <summary>
    /// 高优先级
    /// </summary>
    High = 2,

    /// <summary>
    /// 关键优先级
    /// </summary>
    Critical = 3
}

/// <summary>
/// 测试运行状态
/// </summary>
public enum TestRunStatus
{
    /// <summary>
    /// 等待中
    /// </summary>
    Pending,

    /// <summary>
    /// 运行中
    /// </summary>
    Running,

    /// <summary>
    /// 已完成
    /// </summary>
    Completed,

    /// <summary>
    /// 失败
    /// </summary>
    Failed,

    /// <summary>
    /// 已取消
    /// </summary>
    Cancelled,

    /// <summary>
    /// 已跳过
    /// </summary>
    Skipped
}

/// <summary>
/// 测试运行配置
/// </summary>
public class TestRunConfig
{
    /// <summary>
    /// 要运行的测试类型
    /// </summary>
    public TestType TestTypes { get; set; } = TestType.All;

    /// <summary>
    /// 测试优先级
    /// </summary>
    public TestPriority Priority { get; set; } = TestPriority.Normal;

    /// <summary>
    /// 是否并行运行测试
    /// </summary>
    public bool RunInParallel { get; set; } = true;

    /// <summary>
    /// 最大并行度
    /// </summary>
    public int MaxParallelism { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// 测试超时时间(秒)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 3600; // 1小时

    /// <summary>
    /// 是否生成覆盖率报告
    /// </summary>
    public bool GenerateCoverageReport { get; set; } = true;

    /// <summary>
    /// 覆盖率报告格式
    /// </summary>
    public string[] CoverageReportFormats { get; set; } = new[] { "Html", "Cobertura" };

    /// <summary>
    /// 是否在失败时快速失败
    /// </summary>
    public bool FailFast { get; set; } = false;

    /// <summary>
    /// 测试过滤器
    /// </summary>
    public string? Filter { get; set; }

    /// <summary>
    /// 输出目录
    /// </summary>
    public string OutputDirectory { get; set; } = "TestResults";

    /// <summary>
    /// 详细程度
    /// </summary>
    public string Verbosity { get; set; } = "normal";

    /// <summary>
    /// 是否收集诊断信息
    /// </summary>
    public bool CollectDiagnostics { get; set; } = false;
}
