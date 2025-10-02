using Xunit;
using FluentAssertions;
using FluentAssertions.Execution;
using Occop.IntegrationTests.Infrastructure;
using Occop.Core.Authentication;
using Occop.Core.Security;

namespace Occop.IntegrationTests.Authentication
{
    /// <summary>
    /// 认证管理器集成测试
    /// Integration tests for authentication manager
    /// </summary>
    public class AuthenticationManagerIntegrationTests : IntegrationTestBase
    {
        private AuthenticationManager GetAuthenticationManager()
        {
            return GetService<AuthenticationManager>();
        }

        [Fact]
        public void AuthenticationManager_ShouldBeInitializedCorrectly()
        {
            // Arrange & Act
            var authManager = GetAuthenticationManager();

            // Assert
            using (new AssertionScope())
            {
                authManager.Should().NotBeNull("认证管理器应该被正确注入");
                authManager.CurrentState.Should().Be(AuthenticationState.NotAuthenticated,
                    "初始状态应该是未认证");
                authManager.IsAuthenticated.Should().BeFalse("初始应该未认证");
                authManager.CurrentUserLogin.Should().BeNull("初始应该没有用户登录");
            }

            Logger.LogInformation("✓ 认证管理器初始化验证通过");
        }

        [Fact]
        public void GetAuthenticationStatus_ShouldReturnValidStatus()
        {
            // Arrange
            var authManager = GetAuthenticationManager();

            // Act
            var status = authManager.GetAuthenticationStatus();

            // Assert
            using (new AssertionScope())
            {
                status.Should().NotBeNull("状态不应为空");
                status.CurrentState.Should().Be(AuthenticationState.NotAuthenticated);
                status.IsAuthenticated.Should().BeFalse();
                status.CurrentUserLogin.Should().BeNull();
                status.IsLockedOut.Should().BeFalse("初始不应该被锁定");
                status.FailedAttempts.Should().Be(0, "初始失败尝试应为0");
                status.SecurityStatus.Should().NotBeNull("安全状态应该存在");
            }

            Logger.LogInformation("✓ 认证状态查询验证通过");
        }

        [Fact]
        public void ValidateAuthentication_WhenNotAuthenticated_ShouldReturnFalse()
        {
            // Arrange
            var authManager = GetAuthenticationManager();

            // Act
            var isValid = authManager.ValidateAuthentication();

            // Assert
            isValid.Should().BeFalse("未认证时验证应该返回false");
            Logger.LogInformation("✓ 未认证状态验证通过");
        }

        [Fact]
        public void SignOut_WhenNotAuthenticated_ShouldNotThrow()
        {
            // Arrange
            var authManager = GetAuthenticationManager();

            // Act & Assert
            var action = () => authManager.SignOut();
            action.Should().NotThrow("即使未认证，登出也不应该抛出异常");

            authManager.CurrentState.Should().Be(AuthenticationState.SignedOut,
                "登出后状态应该是SignedOut");

            Logger.LogInformation("✓ 未认证状态登出验证通过");
        }

        [Fact]
        public async Task RefreshAuthentication_WhenNotAuthenticated_ShouldReturnFalse()
        {
            // Arrange
            var authManager = GetAuthenticationManager();

            // Act
            var result = await authManager.RefreshAuthenticationAsync();

            // Assert
            result.Should().BeFalse("未认证时刷新应该返回false");
            Logger.LogInformation("✓ 未认证状态刷新验证通过");
        }

        [Fact]
        public void IsLockedOut_InitialState_ShouldBeFalse()
        {
            // Arrange
            var authManager = GetAuthenticationManager();

            // Act & Assert
            authManager.IsLockedOut.Should().BeFalse("初始状态不应该被锁定");
            authManager.LockoutExpirationTime.Should().BeNull("初始状态没有锁定过期时间");

            Logger.LogInformation("✓ 初始锁定状态验证通过");
        }

        [Fact]
        public void AuthenticationStateTransitions_ShouldWorkCorrectly()
        {
            // Arrange
            var authManager = GetAuthenticationManager();
            var stateChanges = new List<AuthenticationState>();

            authManager.AuthenticationStateChanged += (sender, args) =>
            {
                stateChanges.Add(args.NewState);
                Logger.LogInformation("状态变化: {OldState} -> {NewState}",
                    args.OldState, args.NewState);
            };

            // Act & Assert - 登出操作应该触发状态变化
            authManager.SignOut();

            stateChanges.Should().Contain(AuthenticationState.SignedOut,
                "应该包含SignedOut状态变化");

            Logger.LogInformation("✓ 状态转换验证通过，记录了 {Count} 次状态变化", stateChanges.Count);
        }

        [Fact]
        public void AuthenticationFailedEvent_ShouldNotBeTriggeredInitially()
        {
            // Arrange
            var authManager = GetAuthenticationManager();
            var failedEventTriggered = false;

            authManager.AuthenticationFailed += (sender, args) =>
            {
                failedEventTriggered = true;
            };

            // Act
            var status = authManager.GetAuthenticationStatus();

            // Assert
            failedEventTriggered.Should().BeFalse("初始状态不应该触发失败事件");
            Logger.LogInformation("✓ 认证失败事件验证通过");
        }

        [Fact]
        public void SessionExpiredEvent_ShouldNotBeTriggeredWhenNotAuthenticated()
        {
            // Arrange
            var authManager = GetAuthenticationManager();
            var sessionExpiredEventTriggered = false;

            authManager.SessionExpired += (sender, args) =>
            {
                sessionExpiredEventTriggered = true;
            };

            // Act
            authManager.ValidateAuthentication();

            // Assert
            sessionExpiredEventTriggered.Should().BeFalse("未认证时不应该触发会话过期事件");
            Logger.LogInformation("✓ 会话过期事件验证通过");
        }

        [Fact]
        public void LastAuthenticationTime_InitialState_ShouldBeNull()
        {
            // Arrange
            var authManager = GetAuthenticationManager();

            // Act & Assert
            authManager.LastAuthenticationTime.Should().BeNull("初始状态最后认证时间应为空");
            Logger.LogInformation("✓ 最后认证时间初始状态验证通过");
        }

        [Fact]
        public void GetAuthenticationStatus_ShouldIncludeWhitelistInfo()
        {
            // Arrange
            var authManager = GetAuthenticationManager();

            // Act
            var status = authManager.GetAuthenticationStatus();

            // Assert
            using (new AssertionScope())
            {
                status.WhitelistInfo.Should().NotBeNull("白名单信息应该存在");
                status.WhitelistInfo!.Mode.Should().NotBeNull("白名单模式应该设置");
            }

            Logger.LogInformation("✓ 白名单信息验证通过: Mode={Mode}, Allowed={Allowed}, Blocked={Blocked}",
                status.WhitelistInfo.Mode,
                status.WhitelistInfo.AllowedUsersCount,
                status.WhitelistInfo.BlockedUsersCount);
        }

        [Fact]
        public void Dispose_ShouldCleanUpResourcesProperly()
        {
            // Arrange
            var authManager = GetAuthenticationManager();

            // 订阅事件以验证清理
            var eventHandlerCalled = false;
            authManager.AuthenticationStateChanged += (s, e) => eventHandlerCalled = true;

            // Act
            authManager.Dispose();

            // Assert - 验证可以多次调用Dispose
            var action = () => authManager.Dispose();
            action.Should().NotThrow("多次调用Dispose不应该抛出异常");

            // 验证对象已经被释放
            var throwAction = () => authManager.ValidateAuthentication();
            throwAction.Should().Throw<ObjectDisposedException>("已释放的对象应该抛出ObjectDisposedException");

            Logger.LogInformation("✓ Dispose清理验证通过");
        }
    }
}
