using System.Security;
using Microsoft.Extensions.Logging;
using Occop.Services.Authentication;
using Occop.Core.Security;
using FluentAssertions;
using FluentAssertions.Execution;

namespace Occop.IntegrationTests.Infrastructure
{
    /// <summary>
    /// 测试助手类，提供常用的测试辅助方法
    /// Test helper class that provides common test utility methods
    /// </summary>
    public class TestHelper
    {
        private readonly IntegrationTestContext _context;
        private readonly ILogger<TestHelper> _logger;

        /// <summary>
        /// 初始化测试助手
        /// Initializes the test helper
        /// </summary>
        public TestHelper(IntegrationTestContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = _context.CreateLogger<TestHelper>();
        }

        #region 安全管理器助手方法

        /// <summary>
        /// 初始化并验证安全管理器
        /// Initializes and validates the security manager
        /// </summary>
        public async Task<ISecurityManager> InitializeSecurityManagerAsync(SecurityContext? context = null)
        {
            _logger.LogInformation("Initializing security manager for test");

            var securityManager = _context.GetService<ISecurityManager>();
            context ??= _context.DataGenerator.GenerateSecurityContext();

            await securityManager.InitializeAsync(context);

            securityManager.IsInitialized.Should().BeTrue("安全管理器应该已初始化");
            _logger.LogInformation("Security manager initialized successfully");

            return securityManager;
        }

        /// <summary>
        /// 存储并验证安全数据
        /// Stores and validates secure data
        /// </summary>
        public async Task<SecureData> StoreSecureDataAsync(ISecurityManager securityManager, SecureString? data = null)
        {
            data ??= _context.DataGenerator.GenerateSecureString();

            _logger.LogInformation("Storing secure data");
            var secureData = await securityManager.StoreSecureDataAsync(data);

            secureData.Should().NotBeNull("安全数据不应为空");
            secureData.Id.Should().NotBeNullOrEmpty("安全数据ID不应为空");
            _logger.LogInformation("Secure data stored with ID: {DataId}", secureData.Id);

            return secureData;
        }

        /// <summary>
        /// 检索并验证安全数据
        /// Retrieves and validates secure data
        /// </summary>
        public async Task<SecureString?> RetrieveSecureDataAsync(ISecurityManager securityManager, string dataId)
        {
            _logger.LogInformation("Retrieving secure data: {DataId}", dataId);
            var data = await securityManager.RetrieveSecureDataAsync(dataId);

            _logger.LogInformation("Secure data retrieved: {Found}", data != null);
            return data;
        }

        /// <summary>
        /// 验证安全状态
        /// Validates security state
        /// </summary>
        public async Task ValidateSecurityStateAsync(ISecurityManager securityManager, bool shouldBeValid = true)
        {
            _logger.LogInformation("Validating security state");
            var result = await securityManager.ValidateSecurityStateAsync();

            using (new AssertionScope())
            {
                result.Should().NotBeNull("验证结果不应为空");
                result.IsValid.Should().Be(shouldBeValid, $"安全状态应该{(shouldBeValid ? "有效" : "无效")}");

                if (!shouldBeValid)
                {
                    result.IssuesFound.Should().BeGreaterThan(0, "应该发现问题");
                }
            }

            _logger.LogInformation("Security state validation result: {IsValid}, Issues: {IssuesFound}",
                result.IsValid, result.IssuesFound);
        }

        #endregion

        #region 认证助手方法

        /// <summary>
        /// 模拟认证流程准备
        /// Simulates authentication flow preparation
        /// </summary>
        public async Task<(string deviceCode, string userCode)> PrepareAuthenticationAsync()
        {
            _logger.LogInformation("Preparing authentication flow");

            var deviceCode = _context.DataGenerator.GenerateDeviceCode();
            var userCode = _context.DataGenerator.GenerateUserCode();

            await Task.Delay(10); // 模拟异步操作

            _logger.LogInformation("Authentication prepared - Device: {DeviceCode}, User: {UserCode}",
                deviceCode, userCode);

            return (deviceCode, userCode);
        }

        /// <summary>
        /// 验证认证状态
        /// Validates authentication state
        /// </summary>
        public void ValidateAuthenticationState(
            AuthenticationManager authManager,
            AuthenticationState expectedState,
            bool shouldBeAuthenticated = false)
        {
            using (new AssertionScope())
            {
                authManager.CurrentState.Should().Be(expectedState,
                    $"认证状态应该是 {expectedState}");
                authManager.IsAuthenticated.Should().Be(shouldBeAuthenticated,
                    $"IsAuthenticated应该是 {shouldBeAuthenticated}");
            }

            _logger.LogInformation("Authentication state validated: {State}, IsAuthenticated: {IsAuthenticated}",
                expectedState, shouldBeAuthenticated);
        }

        /// <summary>
        /// 验证认证失败场景
        /// Validates authentication failure scenarios
        /// </summary>
        public void ValidateAuthenticationFailure(
            AuthenticationManager authManager,
            int expectedFailedAttempts)
        {
            var status = authManager.GetAuthenticationStatus();

            using (new AssertionScope())
            {
                status.IsAuthenticated.Should().BeFalse("不应该处于已认证状态");
                status.FailedAttempts.Should().Be(expectedFailedAttempts,
                    $"失败尝试次数应该是 {expectedFailedAttempts}");
            }

            _logger.LogInformation("Authentication failure validated: {FailedAttempts} attempts",
                expectedFailedAttempts);
        }

        #endregion

        #region 文件和目录助手方法

        /// <summary>
        /// 创建临时测试文件
        /// Creates a temporary test file
        /// </summary>
        public string CreateTempTestFile(string? content = null)
        {
            var filePath = _context.DataGenerator.GenerateFilePath();
            content ??= $"Test file created at {DateTime.UtcNow:O}";

            File.WriteAllText(filePath, content);
            _logger.LogInformation("Created temp test file: {FilePath}", filePath);

            return filePath;
        }

        /// <summary>
        /// 创建临时测试目录
        /// Creates a temporary test directory
        /// </summary>
        public string CreateTempTestDirectory()
        {
            var dirPath = _context.DataGenerator.GenerateDirectoryPath();
            Directory.CreateDirectory(dirPath);
            _logger.LogInformation("Created temp test directory: {DirectoryPath}", dirPath);

            return dirPath;
        }

        /// <summary>
        /// 清理临时文件
        /// Cleans up a temporary file
        /// </summary>
        public void CleanupTempFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogInformation("Cleaned up temp file: {FilePath}", filePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup temp file: {FilePath}", filePath);
            }
        }

        /// <summary>
        /// 清理临时目录
        /// Cleans up a temporary directory
        /// </summary>
        public void CleanupTempDirectory(string dirPath)
        {
            try
            {
                if (Directory.Exists(dirPath))
                {
                    Directory.Delete(dirPath, recursive: true);
                    _logger.LogInformation("Cleaned up temp directory: {DirectoryPath}", dirPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup temp directory: {DirectoryPath}", dirPath);
            }
        }

        #endregion

        #region 等待和重试助手方法

        /// <summary>
        /// 等待条件满足
        /// Waits for a condition to be met
        /// </summary>
        public async Task<bool> WaitForConditionAsync(
            Func<bool> condition,
            TimeSpan? timeout = null,
            TimeSpan? checkInterval = null)
        {
            timeout ??= TimeSpan.FromSeconds(30);
            checkInterval ??= TimeSpan.FromMilliseconds(100);

            var startTime = DateTime.UtcNow;
            _logger.LogInformation("Waiting for condition (timeout: {Timeout})", timeout);

            while (DateTime.UtcNow - startTime < timeout)
            {
                if (condition())
                {
                    var elapsed = DateTime.UtcNow - startTime;
                    _logger.LogInformation("Condition met after {Elapsed}", elapsed);
                    return true;
                }

                await Task.Delay(checkInterval.Value);
            }

            _logger.LogWarning("Condition not met within timeout: {Timeout}", timeout);
            return false;
        }

        /// <summary>
        /// 重试操作直到成功
        /// Retries an operation until it succeeds
        /// </summary>
        public async Task<T> RetryAsync<T>(
            Func<Task<T>> operation,
            int maxAttempts = 3,
            TimeSpan? delayBetweenAttempts = null)
        {
            delayBetweenAttempts ??= TimeSpan.FromMilliseconds(100);

            Exception? lastException = null;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    _logger.LogInformation("Retry attempt {Attempt}/{MaxAttempts}", attempt, maxAttempts);
                    return await operation();
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogWarning(ex, "Attempt {Attempt} failed", attempt);

                    if (attempt < maxAttempts)
                    {
                        await Task.Delay(delayBetweenAttempts.Value);
                    }
                }
            }

            throw new InvalidOperationException(
                $"Operation failed after {maxAttempts} attempts",
                lastException);
        }

        #endregion

        #region 断言助手方法

        /// <summary>
        /// 验证异常被抛出
        /// Validates that an exception is thrown
        /// </summary>
        public async Task<TException> AssertThrowsAsync<TException>(
            Func<Task> action,
            string? because = null)
            where TException : Exception
        {
            _logger.LogInformation("Expecting exception: {ExceptionType}", typeof(TException).Name);

            var exception = await action.Should()
                .ThrowAsync<TException>(because ?? "期望抛出异常");

            _logger.LogInformation("Exception thrown as expected: {Message}", exception.Which.Message);
            return exception.Which;
        }

        /// <summary>
        /// 验证操作不抛出异常
        /// Validates that an operation does not throw
        /// </summary>
        public async Task AssertDoesNotThrowAsync(
            Func<Task> action,
            string? because = null)
        {
            _logger.LogInformation("Expecting no exception");

            await action.Should()
                .NotThrowAsync(because ?? "不应该抛出异常");

            _logger.LogInformation("No exception thrown as expected");
        }

        #endregion

        #region 性能测量助手方法

        /// <summary>
        /// 测量操作执行时间
        /// Measures operation execution time
        /// </summary>
        public async Task<(T result, TimeSpan elapsed)> MeasureAsync<T>(Func<Task<T>> operation)
        {
            _logger.LogInformation("Starting performance measurement");
            var startTime = DateTime.UtcNow;

            var result = await operation();

            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation("Operation completed in {Elapsed}", elapsed);

            return (result, elapsed);
        }

        /// <summary>
        /// 验证操作在指定时间内完成
        /// Validates that an operation completes within a specified time
        /// </summary>
        public async Task<T> AssertCompletesWithinAsync<T>(
            Func<Task<T>> operation,
            TimeSpan maxDuration,
            string? because = null)
        {
            var (result, elapsed) = await MeasureAsync(operation);

            elapsed.Should().BeLessThanOrEqualTo(maxDuration,
                because ?? $"操作应该在 {maxDuration} 内完成");

            _logger.LogInformation("Operation completed within expected duration: {Elapsed} <= {MaxDuration}",
                elapsed, maxDuration);

            return result;
        }

        #endregion
    }
}
