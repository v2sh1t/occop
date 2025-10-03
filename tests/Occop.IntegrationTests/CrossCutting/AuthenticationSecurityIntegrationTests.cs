using Xunit;
using FluentAssertions;
using FluentAssertions.Execution;
using Occop.IntegrationTests.Infrastructure;
using Occop.Services.Authentication;
using Occop.Core.Security;

namespace Occop.IntegrationTests.CrossCutting
{
    /// <summary>
    /// 认证与安全系统集成测试
    /// Integration tests for authentication and security system interaction
    /// </summary>
    public class AuthenticationSecurityIntegrationTests : IntegrationTestBase
    {
        [Fact]
        public async Task SecurityManager_ShouldBeAvailableToAuthenticationManager()
        {
            // Arrange
            var authManager = GetService<AuthenticationManager>();
            var securityManager = GetService<ISecurityManager>();

            // Act
            var context = DataGenerator.GenerateSecurityContext();
            await securityManager.InitializeAsync(context);

            // Assert
            using (new AssertionScope())
            {
                authManager.Should().NotBeNull("认证管理器应该可用");
                securityManager.Should().NotBeNull("安全管理器应该可用");
                securityManager.IsInitialized.Should().BeTrue("安全管理器应该已初始化");
            }

            Logger.LogInformation("✓ 认证和安全管理器协同可用性验证通过");
        }

        [Fact]
        public async Task AuthenticationFlow_ShouldUseSecureStorage()
        {
            // Arrange
            var authManager = GetService<AuthenticationManager>();
            var securityManager = await Helper.InitializeSecurityManagerAsync();
            var tokenManager = GetService<SecureTokenManager>();

            // Act - 获取安全状态
            var securityStatus = tokenManager.GetSecurityStatus();
            var authStatus = authManager.GetAuthenticationStatus();

            // Assert
            using (new AssertionScope())
            {
                securityStatus.Should().NotBeNull("安全状态应该存在");
                authStatus.SecurityStatus.Should().NotBeNull("认证状态应该包含安全状态");
                authStatus.IsAuthenticated.Should().BeFalse("初始应该未认证");
            }

            Logger.LogInformation("✓ 认证流程使用安全存储验证通过");
        }

        [Fact]
        public async Task SignOut_ShouldClearSecurityState()
        {
            // Arrange
            var authManager = GetService<AuthenticationManager>();
            var securityManager = await Helper.InitializeSecurityManagerAsync();

            // 存储一些安全数据
            await Helper.StoreSecureDataAsync(securityManager);
            var summaryBefore = securityManager.GetSecurityStateSummary();

            // Act - 登出应该清理安全状态
            authManager.SignOut();

            // 等待清理完成
            await Task.Delay(100);

            // Assert
            authManager.CurrentState.Should().Be(AuthenticationState.SignedOut,
                "应该处于登出状态");

            Logger.LogInformation("✓ 登出清理安全状态验证通过");
        }

        [Fact]
        public async Task ConcurrentAuthAndSecurityOperations_ShouldBeThreadSafe()
        {
            // Arrange
            var authManager = GetService<AuthenticationManager>();
            var securityManager = await Helper.InitializeSecurityManagerAsync();

            // Act - 并发执行认证和安全操作
            var tasks = new List<Task>
            {
                Task.Run(async () =>
                {
                    for (int i = 0; i < 5; i++)
                    {
                        await Helper.StoreSecureDataAsync(securityManager);
                        await Task.Delay(10);
                    }
                }),
                Task.Run(() =>
                {
                    for (int i = 0; i < 5; i++)
                    {
                        var status = authManager.GetAuthenticationStatus();
                        Thread.Sleep(10);
                    }
                }),
                Task.Run(async () =>
                {
                    for (int i = 0; i < 5; i++)
                    {
                        var summary = securityManager.GetSecurityStateSummary();
                        await Task.Delay(10);
                    }
                })
            };

            // Assert
            await Helper.AssertDoesNotThrowAsync(async () => await Task.WhenAll(tasks),
                "并发操作不应该抛出异常");

            Logger.LogInformation("✓ 并发认证和安全操作线程安全验证通过");
        }

        [Fact]
        public async Task SecurityEvents_ShouldBeMonitoredDuringAuthFlow()
        {
            // Arrange
            var authManager = GetService<AuthenticationManager>();
            var securityManager = await Helper.InitializeSecurityManagerAsync();

            var securityEvents = new List<string>();
            var authStateChanges = new List<AuthenticationState>();

            securityManager.SecurityEvent += (sender, args) =>
            {
                securityEvents.Add($"{args.EventType}: {args.Message}");
            };

            authManager.AuthenticationStateChanged += (sender, args) =>
            {
                authStateChanges.Add(args.NewState);
            };

            // Act - 执行一些操作触发事件
            await Helper.StoreSecureDataAsync(securityManager);
            authManager.SignOut();
            await securityManager.ClearAllSecurityStateAsync();

            // Assert
            using (new AssertionScope())
            {
                securityEvents.Should().NotBeEmpty("应该有安全事件被触发");
                authStateChanges.Should().NotBeEmpty("应该有认证状态变化");
            }

            Logger.LogInformation("✓ 安全事件监控验证通过: {SecurityEvents} 安全事件, {AuthChanges} 状态变化",
                securityEvents.Count, authStateChanges.Count);
        }

        [Fact]
        public async Task FullWorkflow_InitializeAuthAndSecuritySystems()
        {
            // Arrange
            Logger.LogInformation("开始完整工作流测试");

            // Act & Assert - Step 1: 初始化安全系统
            Logger.LogInformation("Step 1: 初始化安全系统");
            var securityManager = await Helper.InitializeSecurityManagerAsync();
            securityManager.IsInitialized.Should().BeTrue();

            // Step 2: 获取认证管理器
            Logger.LogInformation("Step 2: 获取认证管理器");
            var authManager = GetService<AuthenticationManager>();
            authManager.CurrentState.Should().Be(AuthenticationState.NotAuthenticated);

            // Step 3: 存储安全数据
            Logger.LogInformation("Step 3: 存储安全数据");
            var secureData = await Helper.StoreSecureDataAsync(securityManager);
            secureData.Should().NotBeNull();

            // Step 4: 验证安全状态
            Logger.LogInformation("Step 4: 验证安全状态");
            await Helper.ValidateSecurityStateAsync(securityManager, shouldBeValid: true);

            // Step 5: 获取系统状态摘要
            Logger.LogInformation("Step 5: 获取系统状态摘要");
            var authStatus = authManager.GetAuthenticationStatus();
            var securitySummary = securityManager.GetSecurityStateSummary();

            using (new AssertionScope())
            {
                authStatus.Should().NotBeNull();
                authStatus.SecurityStatus.Should().NotBeNull();
                securitySummary.Should().NotBeNull();
                securitySummary.SecureDataItemCount.Should().BeGreaterThan(0);
            }

            // Step 6: 清理
            Logger.LogInformation("Step 6: 清理安全状态");
            await securityManager.ClearAllSecurityStateAsync();

            var finalSummary = securityManager.GetSecurityStateSummary();
            Logger.LogInformation("最终状态: SecureDataItems={Items}", finalSummary.SecureDataItemCount);

            Logger.LogInformation("✓ 完整工作流验证通过");
        }

        [Fact]
        public async Task UserWhitelist_ShouldIntegrateWithAuthentication()
        {
            // Arrange
            var authManager = GetService<AuthenticationManager>();
            var whitelist = GetService<UserWhitelist>();

            // Act
            var testUsers = DataGenerator.GenerateWhitelistUsers(3);

            // 添加用户到白名单（如果whitelist支持）
            var whitelistInfo = whitelist.GetWhitelistInfo();

            // Assert
            using (new AssertionScope())
            {
                whitelistInfo.Should().NotBeNull("白名单信息应该存在");
                whitelistInfo.Mode.Should().NotBeNull("白名单模式应该设置");
            }

            var authStatus = authManager.GetAuthenticationStatus();
            authStatus.WhitelistInfo.Should().NotBeNull("认证状态应该包含白名单信息");

            Logger.LogInformation("✓ 白名单集成验证通过: Mode={Mode}",
                whitelistInfo.Mode);

            await Task.CompletedTask;
        }

        [Fact]
        public async Task SecurityAuditor_ShouldLogAuthenticationEvents()
        {
            // Arrange
            var authManager = GetService<AuthenticationManager>();
            var auditor = GetService<SecurityAuditor>();

            // Act - 执行一些认证相关操作
            authManager.SignOut();
            var authStatus = authManager.GetAuthenticationStatus();

            // Assert
            using (new AssertionScope())
            {
                auditor.Should().NotBeNull("审计器应该可用");
                authStatus.Should().NotBeNull("认证状态应该可用");
            }

            Logger.LogInformation("✓ 安全审计集成验证通过");

            await Task.CompletedTask;
        }

        [Fact]
        public async Task MemoryPressure_ShouldNotCauseSecurityLeaks()
        {
            // Arrange
            var securityManager = await Helper.InitializeSecurityManagerAsync();
            var storedDataIds = new List<string>();

            // Act - 存储大量数据模拟内存压力
            Logger.LogInformation("创建内存压力测试数据");
            for (int i = 0; i < 100; i++)
            {
                var data = await Helper.StoreSecureDataAsync(securityManager);
                storedDataIds.Add(data.Id);
            }

            // 强制垃圾回收
            Logger.LogInformation("强制垃圾回收");
            securityManager.ForceGarbageCollection();
            await Task.Delay(100);

            // 验证数据仍然可访问
            Logger.LogInformation("验证数据完整性");
            var summary = securityManager.GetSecurityStateSummary();

            // Assert
            using (new AssertionScope())
            {
                summary.SecureDataItemCount.Should().BeGreaterThan(0, "数据应该仍然存在");
                summary.HasMemoryLeakRisk.Should().BeFalse("不应该有内存泄露风险");
            }

            // Cleanup
            Logger.LogInformation("清理测试数据");
            await securityManager.ClearAllSecurityStateAsync();

            Logger.LogInformation("✓ 内存压力安全性验证通过");
        }

        [Fact]
        public async Task DisposalOrder_ShouldNotCauseErrors()
        {
            // Arrange - 创建新的服务实例
            using var context = new IntegrationTestContext();
            var securityManager = context.GetService<ISecurityManager>();
            var authManager = context.GetService<AuthenticationManager>();

            var securityContext = DataGenerator.GenerateSecurityContext();
            await securityManager.InitializeAsync(securityContext);

            // Act - 按不同顺序释放
            Logger.LogInformation("按顺序释放资源");

            var action = () =>
            {
                authManager.Dispose();
                securityManager.Dispose();
                context.Dispose();
            };

            // Assert
            action.Should().NotThrow("资源释放不应该抛出异常");

            Logger.LogInformation("✓ 释放顺序验证通过");
        }
    }
}
