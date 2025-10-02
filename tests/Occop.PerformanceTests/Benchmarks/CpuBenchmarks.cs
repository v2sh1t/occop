using BenchmarkDotNet.Attributes;
using Occop.PerformanceTests.Infrastructure;
using System.Security.Cryptography;

namespace Occop.PerformanceTests.Benchmarks
{
    /// <summary>
    /// CPU密集型操作基准测试
    /// CPU-intensive operation benchmarks
    /// </summary>
    public class CpuBenchmarks : BenchmarkBase
    {
        private byte[]? _data;
        private const int DataSize = 1024 * 1024; // 1 MB

        public override void GlobalSetup()
        {
            _data = PerformanceTestHelper.GenerateTestData(DataSize);
        }

        [Benchmark(Description = "SHA256 hashing", Baseline = true)]
        public void SHA256_Hashing()
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(_data!);
        }

        [Benchmark(Description = "AES encryption")]
        public void AES_Encryption()
        {
            using var aes = Aes.Create();
            aes.GenerateKey();
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            var encrypted = encryptor.TransformFinalBlock(_data!, 0, _data!.Length);
        }

        [Benchmark(Description = "Fibonacci calculation")]
        public void Fibonacci_Calculation()
        {
            var result = PerformanceTestHelper.SimulateCpuWork(100);
        }

        [Benchmark(Description = "String concatenation")]
        public void String_Concatenation()
        {
            string result = string.Empty;
            for (int i = 0; i < 1000; i++)
            {
                result += i.ToString();
            }
        }

        [Benchmark(Description = "StringBuilder concatenation")]
        public void StringBuilder_Concatenation()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 1000; i++)
            {
                sb.Append(i);
            }
            var result = sb.ToString();
        }

        [Benchmark(Description = "LINQ operations")]
        public void LINQ_Operations()
        {
            var numbers = Enumerable.Range(0, 10000);
            var result = numbers
                .Where(n => n % 2 == 0)
                .Select(n => n * n)
                .OrderByDescending(n => n)
                .Take(100)
                .ToList();
        }
    }
}
