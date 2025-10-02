using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;

namespace Occop.PerformanceTests.Infrastructure
{
    /// <summary>
    /// 基准测试配置
    /// Benchmark configuration
    /// </summary>
    public class BenchmarkConfig : ManualConfig
    {
        public BenchmarkConfig()
        {
            // 添加默认诊断器
            AddDiagnoser(MemoryDiagnoser.Default);
            AddDiagnoser(ThreadingDiagnoser.Default);

            // 添加导出器
            AddExporter(MarkdownExporter.GitHub);
            AddExporter(HtmlExporter.Default);
            AddExporter(CsvExporter.Default);

            // 配置作业
            AddJob(Job.Default
                .WithWarmupCount(3)
                .WithIterationCount(5)
                .WithInvocationCount(100));

            // 设置排序
            WithOrderer(new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest));
        }
    }

    /// <summary>
    /// 基准测试基类
    /// Base class for benchmarks
    /// </summary>
    [Config(typeof(BenchmarkConfig))]
    [MemoryDiagnoser]
    public abstract class BenchmarkBase
    {
        /// <summary>
        /// 全局设置（每个测试类运行一次）
        /// Global setup (runs once per benchmark class)
        /// </summary>
        [GlobalSetup]
        public virtual void GlobalSetup()
        {
            // 子类可以重写此方法进行初始化
        }

        /// <summary>
        /// 全局清理（每个测试类结束时运行一次）
        /// Global cleanup (runs once after all benchmarks)
        /// </summary>
        [GlobalCleanup]
        public virtual void GlobalCleanup()
        {
            // 子类可以重写此方法进行清理
        }

        /// <summary>
        /// 迭代设置（每次迭代前运行）
        /// Iteration setup (runs before each iteration)
        /// </summary>
        [IterationSetup]
        public virtual void IterationSetup()
        {
            // 子类可以重写此方法
        }

        /// <summary>
        /// 迭代清理（每次迭代后运行）
        /// Iteration cleanup (runs after each iteration)
        /// </summary>
        [IterationCleanup]
        public virtual void IterationCleanup()
        {
            // 强制GC以获得更准确的内存测量
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }

    /// <summary>
    /// 性能测试辅助类
    /// Performance test helper
    /// </summary>
    public static class PerformanceTestHelper
    {
        /// <summary>
        /// 生成测试数据
        /// Generate test data
        /// </summary>
        public static byte[] GenerateTestData(int sizeInBytes)
        {
            var data = new byte[sizeInBytes];
            new Random(42).NextBytes(data);
            return data;
        }

        /// <summary>
        /// 模拟CPU密集型操作
        /// Simulate CPU-intensive operation
        /// </summary>
        public static long SimulateCpuWork(int iterations = 1000)
        {
            long result = 0;
            for (int i = 0; i < iterations; i++)
            {
                result += Fibonacci(20);
            }
            return result;
        }

        /// <summary>
        /// 斐波那契数列（递归实现，用于CPU基准测试）
        /// Fibonacci (recursive, for CPU benchmarking)
        /// </summary>
        private static long Fibonacci(int n)
        {
            if (n <= 1) return n;
            return Fibonacci(n - 1) + Fibonacci(n - 2);
        }

        /// <summary>
        /// 模拟内存分配
        /// Simulate memory allocation
        /// </summary>
        public static List<byte[]> SimulateMemoryAllocation(int count, int sizePerAllocation)
        {
            var allocations = new List<byte[]>(count);
            for (int i = 0; i < count; i++)
            {
                allocations.Add(new byte[sizePerAllocation]);
            }
            return allocations;
        }

        /// <summary>
        /// 获取内存快照
        /// Get memory snapshot
        /// </summary>
        public static (long workingSet, long privateMemory, long managedHeap) GetMemorySnapshot()
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();
            return (
                process.WorkingSet64,
                process.PrivateMemorySize64,
                GC.GetTotalMemory(false)
            );
        }
    }
}
