using Occop.IntegrationTests.Infrastructure;
using Occop.Core.Security;
using Occop.Services.Security;

namespace Occop.SecurityTests.Infrastructure
{
    /// <summary>
    /// 安全测试基类，提供安全测试的通用功能
    /// Security test base class providing common security testing functionality
    /// </summary>
    public abstract class SecurityTestBase : IDisposable
    {
        protected IntegrationTestContext TestContext { get; }
        protected SensitiveDataScanner Scanner { get; }
        protected TestDataGenerator DataGenerator { get; }
        private bool _disposed = false;

        /// <summary>
        /// 初始化安全测试基类
        /// Initializes security test base
        /// </summary>
        protected SecurityTestBase()
        {
            TestContext = new IntegrationTestContext($"SecurityTest_{Guid.NewGuid():N}");
            Scanner = new SensitiveDataScanner();
            DataGenerator = new TestDataGenerator();
        }

        /// <summary>
        /// 验证文本不包含敏感数据
        /// Verifies text doesn't contain sensitive data
        /// </summary>
        protected ScanResult VerifyNoSensitiveData(string text, string source = "test")
        {
            var result = Scanner.ScanText(text, source);
            return result;
        }

        /// <summary>
        /// 模拟内存压力
        /// Simulates memory pressure
        /// </summary>
        protected void SimulateMemoryPressure(int sizeInMB = 50)
        {
            var allocations = new List<byte[]>();
            for (int i = 0; i < sizeInMB; i++)
            {
                allocations.Add(new byte[1024 * 1024]); // 1 MB
            }

            // 让GC可以回收
            allocations.Clear();
        }

        /// <summary>
        /// 等待并验证GC回收
        /// Waits and verifies GC collection
        /// </summary>
        protected async Task<long> WaitForGarbageCollectionAsync()
        {
            var beforeMemory = GC.GetTotalMemory(false);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            await Task.Delay(100); // 给GC一些时间

            var afterMemory = GC.GetTotalMemory(false);
            return beforeMemory - afterMemory;
        }

        /// <summary>
        /// 获取当前内存使用量
        /// Gets current memory usage
        /// </summary>
        protected long GetCurrentMemoryUsage()
        {
            return GC.GetTotalMemory(false);
        }

        /// <summary>
        /// 创建测试用的临时文件
        /// Creates temporary file for testing
        /// </summary>
        protected string CreateTempFile(string content, string extension = ".txt")
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"occop_test_{Guid.NewGuid():N}{extension}");
            File.WriteAllText(tempFile, content);
            return tempFile;
        }

        /// <summary>
        /// 删除临时文件
        /// Deletes temporary file
        /// </summary>
        protected void DeleteTempFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                }
                catch
                {
                    // 忽略删除错误
                }
            }
        }

        /// <summary>
        /// 释放资源
        /// Disposes resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源的具体实现
        /// Actual implementation of resource disposal
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    TestContext?.Dispose();
                }
                _disposed = true;
            }
        }
    }
}
