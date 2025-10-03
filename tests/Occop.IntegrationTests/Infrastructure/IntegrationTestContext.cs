using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Occop.Services.Authentication;
using Occop.Core.Security;
using Occop.Services.Authentication;
using Occop.Services.Logging;
using Occop.Services.Security;

namespace Occop.IntegrationTests.Infrastructure
{
    /// <summary>
    /// 集成测试上下文，负责管理测试环境的初始化和清理
    /// Integration test context that manages test environment initialization and cleanup
    /// </summary>
    public class IntegrationTestContext : IDisposable
    {
        private readonly ServiceProvider _serviceProvider;
        private bool _disposed = false;

        /// <summary>
        /// 获取服务提供者
        /// Gets the service provider
        /// </summary>
        public IServiceProvider ServiceProvider => _serviceProvider;

        /// <summary>
        /// 获取配置
        /// Gets the configuration
        /// </summary>
        public IConfiguration Configuration { get; }

        /// <summary>
        /// 获取日志工厂
        /// Gets the logger factory
        /// </summary>
        public ILoggerFactory LoggerFactory { get; }

        /// <summary>
        /// 获取测试数据生成器
        /// Gets the test data generator
        /// </summary>
        public TestDataGenerator DataGenerator { get; }

        /// <summary>
        /// 获取测试助手
        /// Gets the test helper
        /// </summary>
        public TestHelper Helper { get; }

        /// <summary>
        /// 初始化集成测试上下文
        /// Initializes the integration test context
        /// </summary>
        /// <param name="configureServices">可选的服务配置委托</param>
        public IntegrationTestContext(Action<IServiceCollection>? configureServices = null)
        {
            // 构建配置
            Configuration = BuildConfiguration();

            // 配置服务
            var services = new ServiceCollection();
            ConfigureDefaultServices(services);
            configureServices?.Invoke(services);

            _serviceProvider = services.BuildServiceProvider();
            LoggerFactory = _serviceProvider.GetRequiredService<ILoggerFactory>();

            // 初始化测试助手
            DataGenerator = new TestDataGenerator();
            Helper = new TestHelper(this);
        }

        /// <summary>
        /// 构建测试配置
        /// Builds the test configuration
        /// </summary>
        private IConfiguration BuildConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.test.json", optional: true)
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Logging:LogLevel:Default"] = "Information",
                    ["Logging:LogLevel:Microsoft"] = "Warning",
                    ["Logging:EnableFileLogging"] = "false",
                    ["Authentication:MaxFailedAttempts"] = "3",
                    ["Authentication:LockoutDurationMinutes"] = "15",
                    ["Authentication:SessionTimeoutMinutes"] = "480",
                    ["Security:EnableAutoCleanup"] = "true",
                    ["Security:CleanupIntervalMinutes"] = "30"
                });

            return builder.Build();
        }

        /// <summary>
        /// 配置默认服务
        /// Configures default services
        /// </summary>
        private void ConfigureDefaultServices(IServiceCollection services)
        {
            // 注册配置
            services.AddSingleton(Configuration);

            // 注册日志服务
            services.AddLogging(builder =>
            {
                builder.AddConfiguration(Configuration.GetSection("Logging"));
                builder.AddConsole();
                builder.AddDebug();
            });

            // 注册核心服务
            services.AddSingleton<ILoggerService, LoggerService>();

            // 注册安全服务
            services.AddSingleton<ISecurityManager, SecurityManager>();
            services.AddSingleton<SecureTokenManager>();
            services.AddSingleton<SecurityAuditor>();

            // 注册认证服务
            services.AddSingleton<GitHubAuthService>();
            services.AddSingleton<UserWhitelist>();
            services.AddSingleton<AuthenticationManager>();

            // 注册其他服务
            services.AddSingleton<CleanupValidator>();
        }

        /// <summary>
        /// 获取指定类型的服务
        /// Gets a service of the specified type
        /// </summary>
        public T GetService<T>() where T : notnull
        {
            return _serviceProvider.GetRequiredService<T>();
        }

        /// <summary>
        /// 尝试获取指定类型的服务
        /// Tries to get a service of the specified type
        /// </summary>
        public T? GetServiceOrDefault<T>() where T : class
        {
            return _serviceProvider.GetService<T>();
        }

        /// <summary>
        /// 创建日志记录器
        /// Creates a logger
        /// </summary>
        public ILogger<T> CreateLogger<T>()
        {
            return LoggerFactory.CreateLogger<T>();
        }

        /// <summary>
        /// 清理测试上下文
        /// Cleans up the test context
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    // 清理安全管理器
                    var securityManager = GetServiceOrDefault<ISecurityManager>();
                    if (securityManager != null)
                    {
                        securityManager.ClearAllSecurityStateAsync().Wait();
                        securityManager.Dispose();
                    }

                    // 清理服务提供者
                    _serviceProvider?.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during test context cleanup: {ex.Message}");
                }
                finally
                {
                    _disposed = true;
                }
            }

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 析构函数
        /// Destructor
        /// </summary>
        ~IntegrationTestContext()
        {
            Dispose();
        }
    }

    /// <summary>
    /// 集成测试基类，提供测试上下文管理
    /// Base class for integration tests that provides test context management
    /// </summary>
    public abstract class IntegrationTestBase : IDisposable
    {
        protected IntegrationTestContext TestContext { get; }
        protected ILogger Logger { get; }
        protected TestDataGenerator DataGenerator { get; }
        protected TestHelper Helper { get; }

        private bool _disposed = false;

        /// <summary>
        /// 初始化集成测试基类
        /// Initializes the integration test base class
        /// </summary>
        protected IntegrationTestBase()
        {
            TestContext = CreateTestContext();
            Logger = TestContext.CreateLogger(GetType());
            DataGenerator = TestContext.DataGenerator;
            Helper = TestContext.Helper;

            Logger.LogInformation("Test initialized: {TestName}", GetType().Name);
        }

        /// <summary>
        /// 创建测试上下文（可由子类重写以自定义配置）
        /// Creates the test context (can be overridden by subclasses to customize configuration)
        /// </summary>
        protected virtual IntegrationTestContext CreateTestContext()
        {
            return new IntegrationTestContext();
        }

        /// <summary>
        /// 获取服务
        /// Gets a service
        /// </summary>
        protected T GetService<T>() where T : notnull
        {
            return TestContext.GetService<T>();
        }

        /// <summary>
        /// 清理资源
        /// Cleans up resources
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    Logger.LogInformation("Test cleanup: {TestName}", GetType().Name);
                    TestContext?.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during test cleanup: {ex.Message}");
                }
                finally
                {
                    _disposed = true;
                }
            }

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 析构函数
        /// Destructor
        /// </summary>
        ~IntegrationTestBase()
        {
            Dispose();
        }
    }
}
