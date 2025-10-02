using System.Security;
using Occop.Core.Security;

namespace Occop.IntegrationTests.Infrastructure
{
    /// <summary>
    /// 测试数据生成器，提供可预测的测试数据
    /// Test data generator that provides predictable test data
    /// </summary>
    public class TestDataGenerator
    {
        private readonly Random _random;
        private int _counter = 0;

        /// <summary>
        /// 初始化测试数据生成器
        /// Initializes the test data generator
        /// </summary>
        public TestDataGenerator(int? seed = null)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random(42); // 使用固定种子以确保可重现性
        }

        /// <summary>
        /// 生成唯一ID
        /// Generates a unique ID
        /// </summary>
        public string GenerateId()
        {
            return $"test-{Interlocked.Increment(ref _counter)}-{Guid.NewGuid():N}";
        }

        /// <summary>
        /// 生成用户名
        /// Generates a username
        /// </summary>
        public string GenerateUsername()
        {
            var names = new[] { "testuser", "developer", "admin", "user", "tester" };
            var suffix = _random.Next(1000, 9999);
            return $"{names[_random.Next(names.Length)]}{suffix}";
        }

        /// <summary>
        /// 生成GitHub用户登录名
        /// Generates a GitHub user login
        /// </summary>
        public string GenerateGitHubLogin()
        {
            var prefixes = new[] { "github", "dev", "test", "user" };
            var suffix = _random.Next(100, 999);
            return $"{prefixes[_random.Next(prefixes.Length)]}-{suffix}";
        }

        /// <summary>
        /// 生成访问令牌
        /// Generates an access token
        /// </summary>
        public string GenerateAccessToken()
        {
            var chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var token = new char[40];
            for (int i = 0; i < token.Length; i++)
            {
                token[i] = chars[_random.Next(chars.Length)];
            }
            return "ghp_" + new string(token);
        }

        /// <summary>
        /// 生成设备代码
        /// Generates a device code
        /// </summary>
        public string GenerateDeviceCode()
        {
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var code = new char[8];
            for (int i = 0; i < code.Length; i++)
            {
                code[i] = chars[_random.Next(chars.Length)];
            }
            return new string(code);
        }

        /// <summary>
        /// 生成用户代码
        /// Generates a user code
        /// </summary>
        public string GenerateUserCode()
        {
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var code = new char[8];
            for (int i = 0; i < code.Length; i++)
            {
                code[i] = chars[_random.Next(chars.Length)];
                if (i == 3) code[i] = '-'; // 格式: XXXX-XXXX
            }
            return new string(code);
        }

        /// <summary>
        /// 生成SecureString
        /// Generates a SecureString
        /// </summary>
        public SecureString GenerateSecureString(int length = 32)
        {
            var secure = new SecureString();
            var chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()";

            for (int i = 0; i < length; i++)
            {
                secure.AppendChar(chars[_random.Next(chars.Length)]);
            }

            secure.MakeReadOnly();
            return secure;
        }

        /// <summary>
        /// 生成SecureData实例
        /// Generates a SecureData instance
        /// </summary>
        public SecureData GenerateSecureData()
        {
            return new SecureData
            {
                Id = GenerateId(),
                CreatedAt = DateTime.UtcNow,
                DataType = "TestData",
                IsExpired = false
            };
        }

        /// <summary>
        /// 生成安全上下文
        /// Generates a security context
        /// </summary>
        public SecurityContext GenerateSecurityContext()
        {
            return new SecurityContext
            {
                ApplicationName = "Occop.IntegrationTests",
                UserId = GenerateUsername(),
                SessionId = GenerateId(),
                SecurityLevel = SecurityLevel.High,
                CreatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// 生成清理触发器配置
        /// Generates cleanup trigger configuration
        /// </summary>
        public CleanupTriggers GenerateCleanupTriggers(
            bool onApplicationExit = true,
            bool onProcessAbnormalExit = true,
            bool onTimeout = false,
            TimeSpan? timeoutDuration = null)
        {
            return new CleanupTriggers
            {
                OnApplicationExit = onApplicationExit,
                OnProcessAbnormalExit = onProcessAbnormalExit,
                OnTimeout = onTimeout,
                TimeoutDuration = timeoutDuration,
                OnSystemShutdown = true,
                OnMemoryPressure = false
            };
        }

        /// <summary>
        /// 生成文件路径
        /// Generates a file path
        /// </summary>
        public string GenerateFilePath(string? directory = null, string? extension = null)
        {
            directory ??= Path.GetTempPath();
            extension ??= ".tmp";
            var filename = $"test-{GenerateId()}{extension}";
            return Path.Combine(directory, filename);
        }

        /// <summary>
        /// 生成临时目录路径
        /// Generates a temporary directory path
        /// </summary>
        public string GenerateDirectoryPath()
        {
            var dirName = $"test-{GenerateId()}";
            return Path.Combine(Path.GetTempPath(), dirName);
        }

        /// <summary>
        /// 生成错误消息
        /// Generates an error message
        /// </summary>
        public string GenerateErrorMessage()
        {
            var messages = new[]
            {
                "Test error occurred",
                "Operation failed during testing",
                "Simulated error for integration test",
                "Test exception thrown intentionally"
            };
            return messages[_random.Next(messages.Length)];
        }

        /// <summary>
        /// 生成随机字符串
        /// Generates a random string
        /// </summary>
        public string GenerateRandomString(int length = 16)
        {
            var chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var result = new char[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = chars[_random.Next(chars.Length)];
            }
            return new string(result);
        }

        /// <summary>
        /// 生成时间跨度
        /// Generates a time span
        /// </summary>
        public TimeSpan GenerateTimeSpan(int minSeconds = 1, int maxSeconds = 3600)
        {
            var seconds = _random.Next(minSeconds, maxSeconds);
            return TimeSpan.FromSeconds(seconds);
        }

        /// <summary>
        /// 生成过去的时间点
        /// Generates a past time point
        /// </summary>
        public DateTime GeneratePastDateTime(int maxHoursAgo = 24)
        {
            var hoursAgo = _random.Next(1, maxHoursAgo);
            return DateTime.UtcNow.AddHours(-hoursAgo);
        }

        /// <summary>
        /// 生成未来的时间点
        /// Generates a future time point
        /// </summary>
        public DateTime GenerateFutureDateTime(int maxHoursAhead = 24)
        {
            var hoursAhead = _random.Next(1, maxHoursAhead);
            return DateTime.UtcNow.AddHours(hoursAhead);
        }

        /// <summary>
        /// 生成白名单用户列表
        /// Generates a whitelist user list
        /// </summary>
        public List<string> GenerateWhitelistUsers(int count = 5)
        {
            var users = new List<string>();
            for (int i = 0; i < count; i++)
            {
                users.Add(GenerateGitHubLogin());
            }
            return users;
        }

        /// <summary>
        /// 生成黑名单用户列表
        /// Generates a blocklist user list
        /// </summary>
        public List<string> GenerateBlocklistUsers(int count = 3)
        {
            var users = new List<string>();
            for (int i = 0; i < count; i++)
            {
                users.Add($"blocked-{GenerateGitHubLogin()}");
            }
            return users;
        }
    }
}
