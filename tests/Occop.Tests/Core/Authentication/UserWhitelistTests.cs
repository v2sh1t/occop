using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Occop.Services.Authentication;
using Xunit;

namespace Occop.Tests.Core.Authentication
{
    /// <summary>
    /// Unit tests for UserWhitelist class
    /// UserWhitelist类的单元测试
    /// </summary>
    public class UserWhitelistTests : IDisposable
    {
        private readonly Mock<ILogger<UserWhitelist>> _mockLogger;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<IConfigurationSection> _mockAllowedUsersSection;
        private readonly Mock<IConfigurationSection> _mockBlockedUsersSection;
        private UserWhitelist? _userWhitelist;

        public UserWhitelistTests()
        {
            _mockLogger = new Mock<ILogger<UserWhitelist>>();
            _mockConfiguration = new Mock<IConfiguration>();
            _mockAllowedUsersSection = new Mock<IConfigurationSection>();
            _mockBlockedUsersSection = new Mock<IConfigurationSection>();

            SetupDefaultConfiguration();
        }

        private void SetupDefaultConfiguration()
        {
            // Setup default configuration values
            _mockConfiguration.Setup(c => c["GitHub:WhitelistMode"]).Returns("Disabled");
            _mockConfiguration.Setup(c => c["GitHub:CaseSensitive"]).Returns("false");
            _mockConfiguration.Setup(c => c.GetSection("GitHub:AllowedUsers")).Returns(_mockAllowedUsersSection.Object);
            _mockConfiguration.Setup(c => c.GetSection("GitHub:BlockedUsers")).Returns(_mockBlockedUsersSection.Object);

            _mockAllowedUsersSection.Setup(s => s.Get<string[]>()).Returns((string[]?)null);
            _mockBlockedUsersSection.Setup(s => s.Get<string[]>()).Returns((string[]?)null);
        }

        private UserWhitelist CreateUserWhitelist()
        {
            return new UserWhitelist(_mockConfiguration.Object, _mockLogger.Object);
        }

        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            // Arrange & Act
            _userWhitelist = CreateUserWhitelist();

            // Assert
            Assert.NotNull(_userWhitelist);
            Assert.Equal(WhitelistMode.Disabled, _userWhitelist.Mode);
            Assert.False(_userWhitelist.IsCaseSensitive);
        }

        [Fact]
        public void Constructor_WithNullConfiguration_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new UserWhitelist(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new UserWhitelist(_mockConfiguration.Object, null!));
        }

        [Fact]
        public void IsUserAllowed_DisabledMode_AllowsAllUsers()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["GitHub:WhitelistMode"]).Returns("Disabled");
            _userWhitelist = CreateUserWhitelist();

            // Act & Assert
            Assert.True(_userWhitelist.IsUserAllowed("testuser"));
            Assert.True(_userWhitelist.IsUserAllowed("anotheruser"));
            Assert.True(_userWhitelist.IsUserAllowed("UPPERCASE"));
        }

        [Fact]
        public void IsUserAllowed_AllowListMode_WithAllowedUsers_AllowsOnlyListedUsers()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["GitHub:WhitelistMode"]).Returns("AllowList");
            _mockAllowedUsersSection.Setup(s => s.Get<string[]>()).Returns(new[] { "user1", "user2" });
            _userWhitelist = CreateUserWhitelist();

            // Act & Assert
            Assert.True(_userWhitelist.IsUserAllowed("user1"));
            Assert.True(_userWhitelist.IsUserAllowed("user2"));
            Assert.False(_userWhitelist.IsUserAllowed("user3"));
            Assert.False(_userWhitelist.IsUserAllowed("notallowed"));
        }

        [Fact]
        public void IsUserAllowed_AllowListMode_EmptyList_DeniesAllUsers()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["GitHub:WhitelistMode"]).Returns("AllowList");
            _mockAllowedUsersSection.Setup(s => s.Get<string[]>()).Returns(new string[0]);
            _userWhitelist = CreateUserWhitelist();

            // Act & Assert
            Assert.False(_userWhitelist.IsUserAllowed("testuser"));
            Assert.False(_userWhitelist.IsUserAllowed("anotheruser"));
        }

        [Fact]
        public void IsUserAllowed_BlockListMode_BlocksListedUsers()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["GitHub:WhitelistMode"]).Returns("BlockList");
            _mockBlockedUsersSection.Setup(s => s.Get<string[]>()).Returns(new[] { "blocked1", "blocked2" });
            _userWhitelist = CreateUserWhitelist();

            // Act & Assert
            Assert.False(_userWhitelist.IsUserAllowed("blocked1"));
            Assert.False(_userWhitelist.IsUserAllowed("blocked2"));
            Assert.True(_userWhitelist.IsUserAllowed("allowed"));
            Assert.True(_userWhitelist.IsUserAllowed("normal"));
        }

        [Fact]
        public void IsUserAllowed_CaseSensitive_RespectsCase()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["GitHub:WhitelistMode"]).Returns("AllowList");
            _mockConfiguration.Setup(c => c["GitHub:CaseSensitive"]).Returns("true");
            _mockAllowedUsersSection.Setup(s => s.Get<string[]>()).Returns(new[] { "TestUser" });
            _userWhitelist = CreateUserWhitelist();

            // Act & Assert
            Assert.True(_userWhitelist.IsUserAllowed("TestUser"));
            Assert.False(_userWhitelist.IsUserAllowed("testuser"));
            Assert.False(_userWhitelist.IsUserAllowed("TESTUSER"));
        }

        [Fact]
        public void IsUserAllowed_CaseInsensitive_IgnoresCase()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["GitHub:WhitelistMode"]).Returns("AllowList");
            _mockConfiguration.Setup(c => c["GitHub:CaseSensitive"]).Returns("false");
            _mockAllowedUsersSection.Setup(s => s.Get<string[]>()).Returns(new[] { "TestUser" });
            _userWhitelist = CreateUserWhitelist();

            // Act & Assert
            Assert.True(_userWhitelist.IsUserAllowed("TestUser"));
            Assert.True(_userWhitelist.IsUserAllowed("testuser"));
            Assert.True(_userWhitelist.IsUserAllowed("TESTUSER"));
            Assert.True(_userWhitelist.IsUserAllowed("TeStUsEr"));
        }

        [Fact]
        public void IsUserAllowed_BlockedUserOverridesAllowList_DeniesUser()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["GitHub:WhitelistMode"]).Returns("AllowList");
            _mockAllowedUsersSection.Setup(s => s.Get<string[]>()).Returns(new[] { "user1", "user2" });
            _mockBlockedUsersSection.Setup(s => s.Get<string[]>()).Returns(new[] { "user1" });
            _userWhitelist = CreateUserWhitelist();

            // Act & Assert
            Assert.False(_userWhitelist.IsUserAllowed("user1")); // Blocked overrides allowed
            Assert.True(_userWhitelist.IsUserAllowed("user2"));
        }

        [Fact]
        public void IsUserAllowed_WithNullOrEmptyUser_ThrowsArgumentException()
        {
            // Arrange
            _userWhitelist = CreateUserWhitelist();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => _userWhitelist.IsUserAllowed(null!));
            Assert.Throws<ArgumentException>(() => _userWhitelist.IsUserAllowed(""));
            Assert.Throws<ArgumentException>(() => _userWhitelist.IsUserAllowed("   "));
        }

        [Fact]
        public void ValidateUsers_WithMultipleUsers_ReturnsCorrectResults()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["GitHub:WhitelistMode"]).Returns("AllowList");
            _mockAllowedUsersSection.Setup(s => s.Get<string[]>()).Returns(new[] { "user1", "user2" });
            _userWhitelist = CreateUserWhitelist();

            var usersToValidate = new[] { "user1", "user2", "user3", "user4" };

            // Act
            var results = _userWhitelist.ValidateUsers(usersToValidate);

            // Assert
            Assert.Equal(4, results.Count);
            Assert.True(results["user1"]);
            Assert.True(results["user2"]);
            Assert.False(results["user3"]);
            Assert.False(results["user4"]);
        }

        [Fact]
        public void ValidateUsers_WithNullInput_ThrowsArgumentNullException()
        {
            // Arrange
            _userWhitelist = CreateUserWhitelist();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _userWhitelist.ValidateUsers(null!));
        }

        [Fact]
        public void ValidateUsers_WithEmptyAndNullUsers_IgnoresThem()
        {
            // Arrange
            _userWhitelist = CreateUserWhitelist();
            var usersToValidate = new[] { "user1", "", null!, "   ", "user2" };

            // Act
            var results = _userWhitelist.ValidateUsers(usersToValidate!);

            // Assert
            Assert.Equal(2, results.Count);
            Assert.True(results.ContainsKey("user1"));
            Assert.True(results.ContainsKey("user2"));
        }

        [Fact]
        public void GetAllowedUsers_ReturnsReadOnlyCollection()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["GitHub:WhitelistMode"]).Returns("AllowList");
            _mockAllowedUsersSection.Setup(s => s.Get<string[]>()).Returns(new[] { "user1", "user2" });
            _userWhitelist = CreateUserWhitelist();

            // Act
            var allowedUsers = _userWhitelist.GetAllowedUsers();

            // Assert
            Assert.NotNull(allowedUsers);
            Assert.Equal(2, allowedUsers.Count);
            Assert.Contains("user1", allowedUsers);
            Assert.Contains("user2", allowedUsers);
        }

        [Fact]
        public void GetBlockedUsers_ReturnsReadOnlyCollection()
        {
            // Arrange
            _mockBlockedUsersSection.Setup(s => s.Get<string[]>()).Returns(new[] { "blocked1", "blocked2" });
            _userWhitelist = CreateUserWhitelist();

            // Act
            var blockedUsers = _userWhitelist.GetBlockedUsers();

            // Assert
            Assert.NotNull(blockedUsers);
            Assert.Equal(2, blockedUsers.Count);
            Assert.Contains("blocked1", blockedUsers);
            Assert.Contains("blocked2", blockedUsers);
        }

        [Fact]
        public void AllowedUsersCount_ReturnsCorrectCount()
        {
            // Arrange
            _mockAllowedUsersSection.Setup(s => s.Get<string[]>()).Returns(new[] { "user1", "user2", "user3" });
            _userWhitelist = CreateUserWhitelist();

            // Act & Assert
            Assert.Equal(3, _userWhitelist.AllowedUsersCount);
        }

        [Fact]
        public void BlockedUsersCount_ReturnsCorrectCount()
        {
            // Arrange
            _mockBlockedUsersSection.Setup(s => s.Get<string[]>()).Returns(new[] { "blocked1", "blocked2" });
            _userWhitelist = CreateUserWhitelist();

            // Act & Assert
            Assert.Equal(2, _userWhitelist.BlockedUsersCount);
        }

        [Fact]
        public void GetWhitelistInfo_ReturnsCompleteInformation()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["GitHub:WhitelistMode"]).Returns("AllowList");
            _mockConfiguration.Setup(c => c["GitHub:CaseSensitive"]).Returns("true");
            _mockAllowedUsersSection.Setup(s => s.Get<string[]>()).Returns(new[] { "user1", "user2" });
            _mockBlockedUsersSection.Setup(s => s.Get<string[]>()).Returns(new[] { "blocked1" });
            _userWhitelist = CreateUserWhitelist();

            // Act
            var info = _userWhitelist.GetWhitelistInfo();

            // Assert
            Assert.NotNull(info);
            Assert.Equal(WhitelistMode.AllowList, info.Mode);
            Assert.True(info.IsCaseSensitive);
            Assert.Equal(2, info.AllowedUsersCount);
            Assert.Equal(1, info.BlockedUsersCount);
            Assert.True(info.LastUpdated <= DateTime.UtcNow);
        }

        [Fact]
        public void ReloadConfiguration_UpdatesWhitelistConfiguration()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["GitHub:WhitelistMode"]).Returns("Disabled");
            _mockAllowedUsersSection.Setup(s => s.Get<string[]>()).Returns(new[] { "user1" });
            _userWhitelist = CreateUserWhitelist();

            // Verify initial state
            Assert.Equal(WhitelistMode.Disabled, _userWhitelist.Mode);

            // Change configuration
            _mockConfiguration.Setup(c => c["GitHub:WhitelistMode"]).Returns("AllowList");
            _mockAllowedUsersSection.Setup(s => s.Get<string[]>()).Returns(new[] { "user1", "user2" });

            // Act
            _userWhitelist.ReloadConfiguration();

            // Assert
            Assert.Equal(WhitelistMode.AllowList, _userWhitelist.Mode);
            Assert.Equal(2, _userWhitelist.AllowedUsersCount);
        }

        [Fact]
        public void WhitelistChanged_Event_FiresOnConfigurationChange()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["GitHub:WhitelistMode"]).Returns("Disabled");
            _userWhitelist = CreateUserWhitelist();

            WhitelistChangedEventArgs? firedEventArgs = null;
            _userWhitelist.WhitelistChanged += (sender, e) => firedEventArgs = e;

            // Change configuration significantly
            _mockConfiguration.Setup(c => c["GitHub:WhitelistMode"]).Returns("AllowList");
            _mockAllowedUsersSection.Setup(s => s.Get<string[]>()).Returns(new[] { "user1" });

            // Act
            _userWhitelist.ReloadConfiguration();

            // Assert
            Assert.NotNull(firedEventArgs);
            Assert.NotNull(firedEventArgs.WhitelistInfo);
            Assert.Equal(WhitelistMode.AllowList, firedEventArgs.WhitelistInfo.Mode);
        }

        [Fact]
        public void InvalidWhitelistMode_DefaultsToDisabled()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["GitHub:WhitelistMode"]).Returns("InvalidMode");
            _userWhitelist = CreateUserWhitelist();

            // Act & Assert
            Assert.Equal(WhitelistMode.Disabled, _userWhitelist.Mode);
        }

        [Fact]
        public void ConfigurationError_UsesDefaults()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["GitHub:WhitelistMode"]).Throws(new Exception("Config error"));

            // Should not throw, should use defaults
            _userWhitelist = CreateUserWhitelist();

            // Act & Assert
            Assert.Equal(WhitelistMode.Disabled, _userWhitelist.Mode);
            Assert.False(_userWhitelist.IsCaseSensitive);
            Assert.Equal(0, _userWhitelist.AllowedUsersCount);
            Assert.Equal(0, _userWhitelist.BlockedUsersCount);
        }

        [Fact]
        public void Dispose_ClearsResources()
        {
            // Arrange
            _userWhitelist = CreateUserWhitelist();

            // Act
            _userWhitelist.Dispose();

            // Assert - Operations after dispose should throw
            Assert.Throws<ObjectDisposedException>(() => _userWhitelist.IsUserAllowed("test"));
        }

        [Fact]
        public void Operations_AfterDispose_ThrowObjectDisposedException()
        {
            // Arrange
            _userWhitelist = CreateUserWhitelist();
            _userWhitelist.Dispose();

            // Act & Assert
            Assert.Throws<ObjectDisposedException>(() => _userWhitelist.IsUserAllowed("test"));
            Assert.Throws<ObjectDisposedException>(() => _userWhitelist.GetAllowedUsers());
            Assert.Throws<ObjectDisposedException>(() => _userWhitelist.ReloadConfiguration());
        }

        public void Dispose()
        {
            _userWhitelist?.Dispose();
        }
    }
}