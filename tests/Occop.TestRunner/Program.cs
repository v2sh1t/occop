using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Occop.TestRunner;
using Occop.TestRunner.Reports;

// 设置控制台编码
Console.OutputEncoding = System.Text.Encoding.UTF8;

Console.WriteLine("==============================================");
Console.WriteLine("       Occop 自动化测试运行器");
Console.WriteLine("==============================================");
Console.WriteLine();

// 配置服务
var services = new ServiceCollection();

services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

var serviceProvider = services.BuildServiceProvider();
var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

// 解析命令行参数
var config = ParseArguments(args);

// 创建调度器
var scheduler = new Scheduler(
    loggerFactory.CreateLogger<Scheduler>(),
    config
);

// 创建报告生成器
var reportGenerator = new TestReportGenerator(
    loggerFactory.CreateLogger<TestReportGenerator>()
);

try
{
    // 运行测试
    var results = await scheduler.RunAllTestsAsync();

    // 生成报告
    Console.WriteLine();
    Console.WriteLine("生成测试报告...");

    var reportDir = Path.Combine(config.OutputDirectory, "Reports");
    Directory.CreateDirectory(reportDir);

    // 生成多种格式的报告
    await reportGenerator.GenerateReportAsync(
        results,
        ReportFormat.Text,
        Path.Combine(reportDir, "TestReport.txt")
    );

    await reportGenerator.GenerateReportAsync(
        results,
        ReportFormat.Markdown,
        Path.Combine(reportDir, "TestReport.md")
    );

    await reportGenerator.GenerateReportAsync(
        results,
        ReportFormat.Html,
        Path.Combine(reportDir, "TestReport.html")
    );

    await reportGenerator.GenerateReportAsync(
        results,
        ReportFormat.Json,
        Path.Combine(reportDir, "TestReport.json")
    );

    Console.WriteLine($"所有报告已生成到: {reportDir}");
    Console.WriteLine();

    // 返回退出码
    var exitCode = scheduler.IsAllTestsPassed() ? 0 : 1;
    Environment.Exit(exitCode);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"运行测试时出错: {ex.Message}");
    Console.Error.WriteLine(ex.StackTrace);
    Environment.Exit(1);
}

static TestRunConfig ParseArguments(string[] args)
{
    var config = new TestRunConfig();

    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i].ToLower())
        {
            case "--types":
            case "-t":
                if (i + 1 < args.Length)
                {
                    config.TestTypes = ParseTestTypes(args[++i]);
                }
                break;

            case "--parallel":
            case "-p":
                if (i + 1 < args.Length)
                {
                    config.RunInParallel = bool.Parse(args[++i]);
                }
                break;

            case "--max-parallelism":
            case "-mp":
                if (i + 1 < args.Length)
                {
                    config.MaxParallelism = int.Parse(args[++i]);
                }
                break;

            case "--timeout":
            case "-to":
                if (i + 1 < args.Length)
                {
                    config.TimeoutSeconds = int.Parse(args[++i]);
                }
                break;

            case "--coverage":
            case "-c":
                if (i + 1 < args.Length)
                {
                    config.GenerateCoverageReport = bool.Parse(args[++i]);
                }
                break;

            case "--filter":
            case "-f":
                if (i + 1 < args.Length)
                {
                    config.Filter = args[++i];
                }
                break;

            case "--output":
            case "-o":
                if (i + 1 < args.Length)
                {
                    config.OutputDirectory = args[++i];
                }
                break;

            case "--verbosity":
            case "-v":
                if (i + 1 < args.Length)
                {
                    config.Verbosity = args[++i];
                }
                break;

            case "--fail-fast":
            case "-ff":
                config.FailFast = true;
                break;

            case "--help":
            case "-h":
                PrintHelp();
                Environment.Exit(0);
                break;
        }
    }

    return config;
}

static TestType ParseTestTypes(string input)
{
    var types = TestType.None;

    if (input.Equals("all", StringComparison.OrdinalIgnoreCase))
    {
        return TestType.All;
    }

    var parts = input.Split(',', StringSplitOptions.RemoveEmptyEntries);

    foreach (var part in parts)
    {
        var trimmed = part.Trim();

        if (Enum.TryParse<TestType>(trimmed, true, out var testType))
        {
            types |= testType;
        }
        else
        {
            Console.WriteLine($"警告: 未知的测试类型 '{trimmed}'");
        }
    }

    return types == TestType.None ? TestType.All : types;
}

static void PrintHelp()
{
    Console.WriteLine("Occop 自动化测试运行器");
    Console.WriteLine();
    Console.WriteLine("用法: Occop.TestRunner [选项]");
    Console.WriteLine();
    Console.WriteLine("选项:");
    Console.WriteLine("  -t, --types <types>          要运行的测试类型 (Unit,Integration,Performance,Security,Stability,All)");
    Console.WriteLine("                               默认: All");
    Console.WriteLine("  -p, --parallel <bool>        是否并行运行测试 (true/false)");
    Console.WriteLine("                               默认: true");
    Console.WriteLine("  -mp, --max-parallelism <n>   最大并行度");
    Console.WriteLine("                               默认: 处理器核心数");
    Console.WriteLine("  -to, --timeout <seconds>     测试超时时间(秒)");
    Console.WriteLine("                               默认: 3600");
    Console.WriteLine("  -c, --coverage <bool>        是否生成覆盖率报告 (true/false)");
    Console.WriteLine("                               默认: true");
    Console.WriteLine("  -f, --filter <filter>        测试过滤器");
    Console.WriteLine("  -o, --output <path>          输出目录");
    Console.WriteLine("                               默认: TestResults");
    Console.WriteLine("  -v, --verbosity <level>      详细程度 (quiet,minimal,normal,detailed,diagnostic)");
    Console.WriteLine("                               默认: normal");
    Console.WriteLine("  -ff, --fail-fast             在第一个失败时停止");
    Console.WriteLine("  -h, --help                   显示此帮助信息");
    Console.WriteLine();
    Console.WriteLine("示例:");
    Console.WriteLine("  Occop.TestRunner");
    Console.WriteLine("  Occop.TestRunner --types Unit,Integration");
    Console.WriteLine("  Occop.TestRunner --types Security --verbosity detailed");
    Console.WriteLine("  Occop.TestRunner --parallel false --fail-fast");
    Console.WriteLine("  Occop.TestRunner --filter \"FullyQualifiedName~SecurityManager\"");
}
