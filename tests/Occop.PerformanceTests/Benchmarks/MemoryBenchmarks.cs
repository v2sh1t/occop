using BenchmarkDotNet.Attributes;
using Occop.PerformanceTests.Infrastructure;

namespace Occop.PerformanceTests.Benchmarks
{
    /// <summary>
    /// 内存操作基准测试
    /// Memory operation benchmarks
    /// </summary>
    public class MemoryBenchmarks : BenchmarkBase
    {
        [Params(100, 1000, 10000)]
        public int AllocationCount { get; set; }

        [Params(1024, 10240, 102400)]
        public int AllocationSize { get; set; }

        [Benchmark(Description = "Array allocation")]
        public void Array_Allocation()
        {
            var arrays = new List<byte[]>(AllocationCount);
            for (int i = 0; i < AllocationCount; i++)
            {
                arrays.Add(new byte[AllocationSize]);
            }
        }

        [Benchmark(Description = "List allocation")]
        public void List_Allocation()
        {
            var lists = new List<List<int>>(AllocationCount);
            for (int i = 0; i < AllocationCount; i++)
            {
                var list = new List<int>(AllocationSize / sizeof(int));
                for (int j = 0; j < AllocationSize / sizeof(int); j++)
                {
                    list.Add(j);
                }
                lists.Add(list);
            }
        }

        [Benchmark(Description = "Dictionary allocation")]
        public void Dictionary_Allocation()
        {
            var dict = new Dictionary<int, byte[]>(AllocationCount);
            for (int i = 0; i < AllocationCount; i++)
            {
                dict[i] = new byte[AllocationSize];
            }
        }

        [Benchmark(Description = "ArrayPool usage")]
        public void ArrayPool_Usage()
        {
            var pool = System.Buffers.ArrayPool<byte>.Shared;
            var arrays = new List<byte[]>(AllocationCount);

            for (int i = 0; i < AllocationCount; i++)
            {
                arrays.Add(pool.Rent(AllocationSize));
            }

            foreach (var array in arrays)
            {
                pool.Return(array);
            }
        }

        [Benchmark(Description = "Span operations", Baseline = true)]
        public void Span_Operations()
        {
            var data = new byte[AllocationSize];
            var span = data.AsSpan();

            for (int i = 0; i < AllocationCount; i++)
            {
                span.Fill((byte)(i % 256));
            }
        }
    }
}
