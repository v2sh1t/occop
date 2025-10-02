using BenchmarkDotNet.Attributes;
using Occop.Core.Performance;
using Occop.PerformanceTests.Infrastructure;

namespace Occop.PerformanceTests.Benchmarks
{
    /// <summary>
    /// 内存分析器基准测试
    /// Memory analyzer benchmarks
    /// </summary>
    public class MemoryAnalyzerBenchmarks : BenchmarkBase
    {
        private IMemoryAnalyzer? _analyzer;
        private MemorySnapshot? _snapshot;
        private List<MemorySnapshot>? _snapshots;

        public override void GlobalSetup()
        {
            _analyzer = new MemoryAnalyzer();
            _snapshot = new MemorySnapshot
            {
                Timestamp = DateTime.UtcNow,
                WorkingSetBytes = 100 * 1024 * 1024, // 100 MB
                PrivateMemoryBytes = 80 * 1024 * 1024, // 80 MB
                ManagedHeapBytes = 50 * 1024 * 1024, // 50 MB
                Gen0CollectionCount = 10,
                Gen1CollectionCount = 5,
                Gen2CollectionCount = 2
            };

            // 创建一系列快照用于趋势分析
            _snapshots = new List<MemorySnapshot>();
            var baseTime = DateTime.UtcNow;
            for (int i = 0; i < 10; i++)
            {
                _snapshots.Add(new MemorySnapshot
                {
                    Timestamp = baseTime.AddMinutes(i),
                    WorkingSetBytes = (100 + i * 10) * 1024 * 1024,
                    PrivateMemoryBytes = (80 + i * 8) * 1024 * 1024,
                    ManagedHeapBytes = (50 + i * 5) * 1024 * 1024,
                    Gen0CollectionCount = 10 + i,
                    Gen1CollectionCount = 5 + i / 2,
                    Gen2CollectionCount = 2 + i / 5
                });
            }
        }

        [Benchmark(Description = "Analyze snapshot")]
        public void Analyze_Snapshot()
        {
            var result = _analyzer!.Analyze(_snapshot!);
        }

        [Benchmark(Description = "Compare snapshots")]
        public void Compare_Snapshots()
        {
            var baseline = _snapshots![0];
            var current = _snapshots![^1];
            var result = _analyzer!.Compare(baseline, current);
        }

        [Benchmark(Description = "Detect memory leak")]
        public void DetectMemoryLeak()
        {
            var leaked = _analyzer!.DetectMemoryLeak(_snapshots!, 10.0);
        }

        [Benchmark(Description = "Generate trend report")]
        public void GenerateTrendReport()
        {
            var report = _analyzer!.GenerateTrendReport(_snapshots!);
        }

        [Benchmark(Description = "Trigger GC and snapshot")]
        public void TriggerGCAndSnapshot()
        {
            var snapshot = _analyzer!.TriggerGCAndSnapshot();
        }
    }
}
