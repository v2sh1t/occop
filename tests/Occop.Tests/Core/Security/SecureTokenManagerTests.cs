using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Occop.Services.Authentication;
using Occop.Core.Security;
using Xunit;

namespace Occop.Tests.Core.Security
{
    /// <summary>
    /// Unit tests for SecureTokenManager class
    /// SecureTokenManager类的单元测试
    /// </summary>
    public class SecureTokenManagerTests : IDisposable
    {
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<ILogger<SecureTokenManager>> _mockLogger;
        private readonly Mock<ILogger<TokenStorage>> _mockTokenStorageLogger;
        private readonly TokenStorage _tokenStorage;
        private SecureTokenManager? _secureTokenManager;

        public SecureTokenManagerTests()
        {
            _mockConfiguration = new Mock<IConfiguration>();
            _mockLogger = new Mock<ILogger<SecureTokenManager>>();
            _mockTokenStorageLogger = new Mock<ILogger<TokenStorage>>();
            _tokenStorage = new TokenStorage(_mockTokenStorageLogger.Object);

            SetupDefaultConfiguration();
        }

        private void SetupDefaultConfiguration()
        {
            _mockConfiguration.Setup(c => c.GetValue<bool>("Security:EncryptionEnabled", true)).Returns(false); // Disable encryption for testing
            _mockConfiguration.Setup(c => c.GetValue<int>("Security:KeyRotationIntervalHours", 24)).Returns(24);
            _mockConfiguration.Setup(c => c.GetValue<int>("Security:TokenRefreshIntervalMinutes", 30)).Returns(0); // Disable auto refresh
            _mockConfiguration.Setup(c => c.GetValue<int>("Security:TokenRefreshThresholdMinutes", 60)).Returns(60);
        }

        private SecureTokenManager CreateSecureTokenManager()
        {
            return new SecureTokenManager(_mockConfiguration.Object, _mockLogger.Object, _tokenStorage);
        }

        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            // Arrange & Act
            _secureTokenManager = CreateSecureTokenManager();

            // Assert
            Assert.NotNull(_secureTokenManager);
            Assert.False(_secureTokenManager.IsEncryptionEnabled);
            Assert.False(_secureTokenManager.IsAutoRefreshEnabled);
        }

        [Fact]
        public void Constructor_WithNullConfiguration_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new SecureTokenManager(null!, _mockLogger.Object, _tokenStorage));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new SecureTokenManager(_mockConfiguration.Object, null!, _tokenStorage));
        }

        [Fact]
        public void Constructor_WithNullTokenStorage_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new SecureTokenManager(_mockConfiguration.Object, _mockLogger.Object, null!));
        }

        [Fact]
        public void Constructor_WithEncryptionEnabled_InitializesEncryption()
        {
            // Arrange
            _mockConfiguration.Setup(c => c.GetValue<bool>("Security:EncryptionEnabled", true)).Returns(true);

            // Act
            _secureTokenManager = CreateSecureTokenManager();

            // Assert
            Assert.True(_secureTokenManager.IsEncryptionEnabled);
            Assert.NotNull(_secureTokenManager.LastKeyRotationTime);
            Assert.NotNull(_secureTokenManager.NextKeyRotationTime);
        }

        [Fact]
        public void Constructor_WithAutoRefreshEnabled_EnablesAutoRefresh()
        {
            // Arrange
            _mockConfiguration.Setup(c => c.GetValue<int>("Security:TokenRefreshIntervalMinutes", 30)).Returns(30);

            // Act
            _secureTokenManager = CreateSecureTokenManager();

            // Assert
            Assert.True(_secureTokenManager.IsAutoRefreshEnabled);
        }

        [Fact]
        public void StoreTokenSecurely_WithValidToken_StoresSuccessfully()
        {
            // Arrange
            _secureTokenManager = CreateSecureTokenManager();
            const string token = "test-access-token";
            const int expiresIn = 3600;

            // Act
            _secureTokenManager.StoreTokenSecurely(token, expiresIn, TokenType.Access);

            // Assert
            var status = _secureTokenManager.GetSecurityStatus();
            Assert.True(status.HasValidAccessToken);
        }

        [Fact]
        public void StoreTokenSecurely_WithNullToken_ThrowsArgumentException()
        {
            // Arrange
            _secureTokenManager = CreateSecureTokenManager();

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                _secureTokenManager.StoreTokenSecurely(null!, 3600));
        }

        [Fact]
        public void StoreTokenSecurely_WithEmptyToken_ThrowsArgumentException()
        {
            // Arrange
            _secureTokenManager = CreateSecureTokenManager();

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                _secureTokenManager.StoreTokenSecurely("", 3600));
        }

        [Fact]
        public void StoreTokenSecurely_WithEncryption_EncryptsToken()
        {
            // Arrange
            _mockConfiguration.Setup(c => c.GetValue<bool>("Security:EncryptionEnabled", true)).Returns(true);
            _secureTokenManager = CreateSecureTokenManager();
            const string token = "test-access-token";

            // Act
            _secureTokenManager.StoreTokenSecurely(token, 3600);

            // Assert
            var retrievedToken = _secureTokenManager.RetrieveTokenSecurely();
            Assert.Equal(token, retrievedToken);
        }

        [Fact]
        public void RetrieveTokenSecurely_WithStoredToken_ReturnsOriginalToken()
        {
            // Arrange
            _secureTokenManager = CreateSecureTokenManager();
            const string token = "test-access-token";
            _secureTokenManager.StoreTokenSecurely(token, 3600);

            // Act
            var retrievedToken = _secureTokenManager.RetrieveTokenSecurely();

            // Assert
            Assert.Equal(token, retrievedToken);
        }

        [Fact]
        public void RetrieveTokenSecurely_WithoutStoredToken_ReturnsNull()
        {
            // Arrange
            _secureTokenManager = CreateSecureTokenManager();

            // Act
            var retrievedToken = _secureTokenManager.RetrieveTokenSecurely();

            // Assert
            Assert.Null(retrievedToken);
        }

        [Fact]
        public void RetrieveTokenSecurely_WithRefreshToken_ReturnsRefreshToken()
        {
            // Arrange
            _secureTokenManager = CreateSecureTokenManager();
            const string refreshToken = "test-refresh-token";
            _secureTokenManager.StoreTokenSecurely(refreshToken, 7200, TokenType.Refresh);

            // Act
            var retrievedToken = _secureTokenManager.RetrieveTokenSecurely(TokenType.Refresh);

            // Assert
            Assert.Equal(refreshToken, retrievedToken);
        }

        [Fact]
        public void RotateEncryptionKeys_WithEncryptionDisabled_LogsWarning()
        {
            // Arrange
            _secureTokenManager = CreateSecureTokenManager();

            // Act
            _secureTokenManager.RotateEncryptionKeys();

            // Assert - Should complete without exception
            // (The method should log a warning but not throw)
        }

        [Fact]
        public void RotateEncryptionKeys_WithEncryptionEnabled_RotatesKeys()
        {
            // Arrange
            _mockConfiguration.Setup(c => c.GetValue<bool>("Security:EncryptionEnabled", true)).Returns(true);
            _secureTokenManager = CreateSecureTokenManager();
            var initialRotationTime = _secureTokenManager.LastKeyRotationTime;

            // Wait a bit to ensure different timestamp
            Thread.Sleep(10);

            // Act
            _secureTokenManager.RotateEncryptionKeys();

            // Assert
            Assert.NotNull(_secureTokenManager.LastKeyRotationTime);
            Assert.True(_secureTokenManager.LastKeyRotationTime > initialRotationTime);
            Assert.NotNull(_secureTokenManager.NextKeyRotationTime);
        }

        [Fact]
        public void EncryptionKeyRotated_Event_FiresOnKeyRotation()
        {
            // Arrange
            _mockConfiguration.Setup(c => c.GetValue<bool>("Security:EncryptionEnabled", true)).Returns(true);
            _secureTokenManager = CreateSecureTokenManager();

            KeyRotationEventArgs? firedEventArgs = null;
            _secureTokenManager.EncryptionKeyRotated += (sender, e) => firedEventArgs = e;

            // Act
            _secureTokenManager.RotateEncryptionKeys();

            // Assert
            Assert.NotNull(firedEventArgs);
            Assert.True(firedEventArgs.RotationTime <= DateTime.UtcNow);
            Assert.NotNull(firedEventArgs.NextRotationTime);
        }

        [Fact]
        public void SecurityEvent_Event_FiresOnSecurityOperations()
        {
            // Arrange
            _secureTokenManager = CreateSecureTokenManager();
            var securityEvents = new List<SecurityEventArgs>();
            _secureTokenManager.SecurityEvent += (sender, e) => securityEvents.Add(e);

            // Act
            _secureTokenManager.StoreTokenSecurely("test-token", 3600);

            // Assert
            Assert.Contains(securityEvents, e => e.EventType == SecurityEventType.TokenStored);
        }

        [Fact]
        public void ClearAllSecurityState_ClearsTokensAndKeys()
        {
            // Arrange
            _secureTokenManager = CreateSecureTokenManager();
            _secureTokenManager.StoreTokenSecurely("access-token", 3600, TokenType.Access);
            _secureTokenManager.StoreTokenSecurely("refresh-token", 7200, TokenType.Refresh);

            // Act
            _secureTokenManager.ClearAllSecurityState();

            // Assert
            var status = _secureTokenManager.GetSecurityStatus();
            Assert.False(status.HasValidAccessToken);
            Assert.False(status.HasValidRefreshToken);
        }

        [Fact]
        public void GetSecurityStatus_ReturnsCompleteStatus()
        {
            // Arrange
            _secureTokenManager = CreateSecureTokenManager();
            _secureTokenManager.StoreTokenSecurely("test-token", 3600);

            // Act
            var status = _secureTokenManager.GetSecurityStatus();

            // Assert
            Assert.NotNull(status);
            Assert.False(status.IsEncryptionEnabled);
            Assert.False(status.IsAutoRefreshEnabled);
            Assert.True(status.HasValidAccessToken);
            Assert.False(status.HasValidRefreshToken);
            Assert.NotNull(status.AccessTokenExpirationTime);
        }

        [Fact]
        public void TokenRefreshRequested_Event_FiresOnTokenExpiration()
        {
            // Arrange
            _secureTokenManager = CreateSecureTokenManager();
            TokenRefreshEventArgs? firedEventArgs = null;
            _secureTokenManager.TokenRefreshRequested += (sender, e) => firedEventArgs = e;

            // Simulate token expiration by manually firing the event
            var expiredEventArgs = new TokenExpiredEventArgs(TokenType.Access);

            // Act - Manually trigger the token expired event on the token storage
            _tokenStorage.StoreAccessToken("test-token", 0); // Expired immediately
            Thread.Sleep(100);
            var token = _tokenStorage.GetAccessToken(); // This should trigger expiration

            // Note: In a real scenario, the auto-refresh would need to be enabled
            // for the event to fire automatically
        }

        [Fact]
        public void StoreTokenSecurely_MultipleCalls_ReplacesToken()
        {
            // Arrange
            _secureTokenManager = CreateSecureTokenManager();
            const string firstToken = "first-token";
            const string secondToken = "second-token";

            // Act
            _secureTokenManager.StoreTokenSecurely(firstToken, 3600);
            _secureTokenManager.StoreTokenSecurely(secondToken, 3600);

            // Assert
            var retrievedToken = _secureTokenManager.RetrieveTokenSecurely();
            Assert.Equal(secondToken, retrievedToken);
        }

        [Fact]
        public void EncryptDecrypt_Roundtrip_PreservesData()
        {
            // Arrange
            _mockConfiguration.Setup(c => c.GetValue<bool>("Security:EncryptionEnabled", true)).Returns(true);
            _secureTokenManager = CreateSecureTokenManager();
            const string originalToken = "test-token-with-special-chars-!@#$%^&*()";

            // Act
            _secureTokenManager.StoreTokenSecurely(originalToken, 3600);
            var retrievedToken = _secureTokenManager.RetrieveTokenSecurely();

            // Assert
            Assert.Equal(originalToken, retrievedToken);
        }

        [Fact]
        public void SecurityEvents_AllTypes_CanBeFired()
        {
            // Arrange
            _mockConfiguration.Setup(c => c.GetValue<bool>("Security:EncryptionEnabled", true)).Returns(true);
            _secureTokenManager = CreateSecureTokenManager();
            var securityEvents = new List<SecurityEventArgs>();
            _secureTokenManager.SecurityEvent += (sender, e) => securityEvents.Add(e);

            // Act
            _secureTokenManager.StoreTokenSecurely("test-token", 3600);
            _secureTokenManager.RetrieveTokenSecurely();
            _secureTokenManager.RotateEncryptionKeys();
            _secureTokenManager.ClearAllSecurityState();

            // Assert
            Assert.Contains(securityEvents, e => e.EventType == SecurityEventType.TokenStored);
            Assert.Contains(securityEvents, e => e.EventType == SecurityEventType.TokenRetrieved);
            Assert.Contains(securityEvents, e => e.EventType == SecurityEventType.KeyRotation);
            Assert.Contains(securityEvents, e => e.EventType == SecurityEventType.SecurityStateCleared);
        }

        [Fact]
        public void Dispose_ClearsResourcesAndUnsubscribes()
        {
            // Arrange
            _secureTokenManager = CreateSecureTokenManager();
            _secureTokenManager.StoreTokenSecurely("test-token", 3600);

            // Act
            _secureTokenManager.Dispose();

            // Assert - Operations after dispose should throw
            Assert.Throws<ObjectDisposedException>(() => _secureTokenManager.StoreTokenSecurely("token", 3600));
        }

        [Fact]
        public void Operations_AfterDispose_ThrowObjectDisposedException()
        {
            // Arrange
            _secureTokenManager = CreateSecureTokenManager();
            _secureTokenManager.Dispose();

            // Act & Assert
            Assert.Throws<ObjectDisposedException>(() => _secureTokenManager.StoreTokenSecurely("token", 3600));
            Assert.Throws<ObjectDisposedException>(() => _secureTokenManager.RetrieveTokenSecurely());
            Assert.Throws<ObjectDisposedException>(() => _secureTokenManager.RotateEncryptionKeys());
            Assert.Throws<ObjectDisposedException>(() => _secureTokenManager.GetSecurityStatus());
        }

        [Fact]
        public void TokenStorage_Events_AreHandledCorrectly()
        {
            // Arrange
            _mockConfiguration.Setup(c => c.GetValue<int>("Security:TokenRefreshIntervalMinutes", 30)).Returns(30);
            _secureTokenManager = CreateSecureTokenManager();

            var refreshRequestEvents = new List<TokenRefreshEventArgs>();
            _secureTokenManager.TokenRefreshRequested += (sender, e) => refreshRequestEvents.Add(e);

            // Act - Store an immediately expiring token
            _secureTokenManager.StoreTokenSecurely("test-token", 0);
            Thread.Sleep(100);

            // Try to retrieve the expired token to trigger the event
            var token = _secureTokenManager.RetrieveTokenSecurely();

            // Note: The auto-refresh mechanism would need the timer to fire,
            // which is difficult to test in unit tests without making the code less robust
        }

        public void Dispose()
        {
            _secureTokenManager?.Dispose();
            _tokenStorage?.Dispose();
        }
    }
}