using BenchmarkDotNet.Attributes;
using Occop.Core.Performance;
using Occop.PerformanceTests.Infrastructure;

namespace Occop.PerformanceTests.Benchmarks
{
    /// <summary>
    /// 性能监控器基准测试
    /// Performance monitor benchmarks
    /// </summary>
    public class PerformanceMonitorBenchmarks : BenchmarkBase
    {
        private IPerformanceMonitor? _monitor;
        private const int OperationCount = 1000;

        public override void GlobalSetup()
        {
            _monitor = new PerformanceMonitor();
        }

        [Benchmark(Description = "BeginOperation overhead")]
        public void BeginOperation_Overhead()
        {
            using var timer = _monitor!.BeginOperation("TestOperation", "Benchmark");
        }

        [Benchmark(Description = "RecordOperation with metadata")]
        public void RecordOperation_WithMetadata()
        {
            var metadata = new Dictionary<string, object>
            {
                ["TestKey1"] = "TestValue1",
                ["TestKey2"] = 123,
                ["TestKey3"] = true
            };

            _monitor!.RecordOperation("TestOperation", 100, true, metadata);
        }

        [Benchmark(Description = "RecordOperation without metadata")]
        public void RecordOperation_WithoutMetadata()
        {
            _monitor!.RecordOperation("TestOperation", 100, true);
        }

        [Benchmark(Description = "GetStatistics")]
        public void GetStatistics()
        {
            var stats = _monitor!.GetStatistics("TestOperation");
        }

        [Benchmark(Description = "DetectDegradation")]
        public void DetectDegradation()
        {
            // 先记录一些操作
            for (int i = 0; i < 20; i++)
            {
                _monitor!.RecordOperation("DegradationTest", i < 10 ? 100 : 200, true);
            }

            var degraded = _monitor!.DetectDegradation("DegradationTest", 20.0);
        }

        [Benchmark(Description = "RecordMemoryUsage")]
        public void RecordMemoryUsage()
        {
            _monitor!.RecordMemoryUsage();
        }

        [Benchmark(Description = "Concurrent operations", Baseline = true)]
        public void ConcurrentOperations()
        {
            Parallel.For(0, OperationCount, i =>
            {
                using var timer = _monitor!.BeginOperation($"Operation_{i % 10}", "Concurrent");
                Thread.Sleep(1); // 模拟工作
            });
        }
    }
}
