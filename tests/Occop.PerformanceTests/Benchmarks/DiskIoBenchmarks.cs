using BenchmarkDotNet.Attributes;
using Occop.PerformanceTests.Infrastructure;

namespace Occop.PerformanceTests.Benchmarks
{
    /// <summary>
    /// 磁盘I/O基准测试
    /// Disk I/O benchmarks
    /// </summary>
    public class DiskIoBenchmarks : BenchmarkBase
    {
        private string? _testDirectory;
        private byte[]? _testData;

        [Params(1024, 10240, 102400, 1048576)] // 1KB, 10KB, 100KB, 1MB
        public int DataSize { get; set; }

        public override void GlobalSetup()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), $"OccopPerfTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDirectory);
            _testData = PerformanceTestHelper.GenerateTestData(DataSize);
        }

        public override void GlobalCleanup()
        {
            if (_testDirectory != null && Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }

        [Benchmark(Description = "File write (sync)")]
        public void File_Write_Sync()
        {
            var filePath = Path.Combine(_testDirectory!, $"test_{Guid.NewGuid()}.dat");
            File.WriteAllBytes(filePath, _testData!);
            File.Delete(filePath);
        }

        [Benchmark(Description = "File write (async)")]
        public async Task File_Write_Async()
        {
            var filePath = Path.Combine(_testDirectory!, $"test_{Guid.NewGuid()}.dat");
            await File.WriteAllBytesAsync(filePath, _testData!);
            File.Delete(filePath);
        }

        [Benchmark(Description = "FileStream write")]
        public void FileStream_Write()
        {
            var filePath = Path.Combine(_testDirectory!, $"test_{Guid.NewGuid()}.dat");
            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096))
            {
                fs.Write(_testData!, 0, _testData!.Length);
                fs.Flush();
            }
            File.Delete(filePath);
        }

        [Benchmark(Description = "File read (sync)", Baseline = true)]
        public void File_Read_Sync()
        {
            var filePath = Path.Combine(_testDirectory!, $"test_{Guid.NewGuid()}.dat");
            File.WriteAllBytes(filePath, _testData!);
            var data = File.ReadAllBytes(filePath);
            File.Delete(filePath);
        }

        [Benchmark(Description = "File read (async)")]
        public async Task File_Read_Async()
        {
            var filePath = Path.Combine(_testDirectory!, $"test_{Guid.NewGuid()}.dat");
            await File.WriteAllBytesAsync(filePath, _testData!);
            var data = await File.ReadAllBytesAsync(filePath);
            File.Delete(filePath);
        }

        [Benchmark(Description = "FileStream read")]
        public void FileStream_Read()
        {
            var filePath = Path.Combine(_testDirectory!, $"test_{Guid.NewGuid()}.dat");
            File.WriteAllBytes(filePath, _testData!);

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096))
            {
                var buffer = new byte[_testData!.Length];
                fs.Read(buffer, 0, buffer.Length);
            }

            File.Delete(filePath);
        }
    }
}
