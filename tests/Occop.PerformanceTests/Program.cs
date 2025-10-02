using BenchmarkDotNet.Running;
using Occop.PerformanceTests.Benchmarks;

namespace Occop.PerformanceTests
{
    /// <summary>
    /// 基准测试运行器
    /// Benchmark test runner
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--all")
            {
                // 运行所有基准测试
                RunAllBenchmarks();
            }
            else if (args.Length > 0)
            {
                // 运行指定的基准测试
                RunSpecificBenchmark(args[0]);
            }
            else
            {
                // 显示菜单
                ShowMenu();
            }
        }

        private static void ShowMenu()
        {
            Console.WriteLine("=== Occop Performance Benchmarks ===");
            Console.WriteLine();
            Console.WriteLine("Available benchmarks:");
            Console.WriteLine("1. Performance Monitor Benchmarks");
            Console.WriteLine("2. Memory Analyzer Benchmarks");
            Console.WriteLine("3. CPU Benchmarks");
            Console.WriteLine("4. Memory Benchmarks");
            Console.WriteLine("5. Disk I/O Benchmarks");
            Console.WriteLine("6. Run All Benchmarks");
            Console.WriteLine("0. Exit");
            Console.WriteLine();
            Console.Write("Select benchmark to run: ");

            var choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    BenchmarkRunner.Run<PerformanceMonitorBenchmarks>();
                    break;
                case "2":
                    BenchmarkRunner.Run<MemoryAnalyzerBenchmarks>();
                    break;
                case "3":
                    BenchmarkRunner.Run<CpuBenchmarks>();
                    break;
                case "4":
                    BenchmarkRunner.Run<MemoryBenchmarks>();
                    break;
                case "5":
                    BenchmarkRunner.Run<DiskIoBenchmarks>();
                    break;
                case "6":
                    RunAllBenchmarks();
                    break;
                case "0":
                    return;
                default:
                    Console.WriteLine("Invalid choice. Exiting...");
                    break;
            }
        }

        private static void RunAllBenchmarks()
        {
            Console.WriteLine("Running all benchmarks...");
            Console.WriteLine();

            BenchmarkRunner.Run<PerformanceMonitorBenchmarks>();
            BenchmarkRunner.Run<MemoryAnalyzerBenchmarks>();
            BenchmarkRunner.Run<CpuBenchmarks>();
            BenchmarkRunner.Run<MemoryBenchmarks>();
            BenchmarkRunner.Run<DiskIoBenchmarks>();
        }

        private static void RunSpecificBenchmark(string name)
        {
            switch (name.ToLowerInvariant())
            {
                case "monitor":
                case "performancemonitor":
                    BenchmarkRunner.Run<PerformanceMonitorBenchmarks>();
                    break;
                case "memory":
                case "memoryanalyzer":
                    BenchmarkRunner.Run<MemoryAnalyzerBenchmarks>();
                    break;
                case "cpu":
                    BenchmarkRunner.Run<CpuBenchmarks>();
                    break;
                case "memalloc":
                case "memorybenchmarks":
                    BenchmarkRunner.Run<MemoryBenchmarks>();
                    break;
                case "disk":
                case "diskio":
                    BenchmarkRunner.Run<DiskIoBenchmarks>();
                    break;
                default:
                    Console.WriteLine($"Unknown benchmark: {name}");
                    Console.WriteLine("Available: monitor, memory, cpu, memalloc, disk");
                    break;
            }
        }
    }
}
