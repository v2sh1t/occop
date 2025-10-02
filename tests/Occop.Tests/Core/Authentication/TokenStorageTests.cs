using Microsoft.Extensions.Logging;
using Moq;
using Occop.Core.Authentication;
using System.Security;
using Xunit;

namespace Occop.Tests.Core.Authentication
{
    /// <summary>
    /// Unit tests for TokenStorage class
    /// TokenStorage类的单元测试
    /// </summary>
    public class TokenStorageTests : IDisposable
    {
        private readonly Mock<ILogger<TokenStorage>> _mockLogger;
        private readonly TokenStorage _tokenStorage;

        public TokenStorageTests()
        {
            _mockLogger = new Mock<ILogger<TokenStorage>>();
            _tokenStorage = new TokenStorage(_mockLogger.Object);
        }

        [Fact]
        public void Constructor_WithValidLogger_CreatesInstance()
        {
            // Arrange & Act
            var storage = new TokenStorage(_mockLogger.Object);

            // Assert
            Assert.NotNull(storage);
            Assert.False(storage.HasValidAccessToken);
            Assert.False(storage.HasValidRefreshToken);
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TokenStorage(null!));
        }

        [Fact]
        public void StoreAccessToken_WithValidToken_StoresSuccessfully()
        {
            // Arrange
            const string token = "test-access-token";
            const int expiresIn = 3600;

            // Act
            _tokenStorage.StoreAccessToken(token, expiresIn);

            // Assert
            Assert.True(_tokenStorage.HasValidAccessToken);
            Assert.NotNull(_tokenStorage.AccessTokenExpirationTime);
            Assert.True(_tokenStorage.AccessTokenExpirationTime > DateTime.UtcNow);
        }

        [Fact]
        public void StoreAccessToken_WithNullToken_ThrowsArgumentException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentException>(() => _tokenStorage.StoreAccessToken(null!, 3600));
        }

        [Fact]
        public void StoreAccessToken_WithEmptyToken_ThrowsArgumentException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentException>(() => _tokenStorage.StoreAccessToken("", 3600));
        }

        [Fact]
        public void StoreAccessToken_WithNegativeExpiresIn_ThrowsArgumentOutOfRangeException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => _tokenStorage.StoreAccessToken("token", -1));
        }

        [Fact]
        public void StoreRefreshToken_WithValidToken_StoresSuccessfully()
        {
            // Arrange
            const string refreshToken = "test-refresh-token";
            const int expiresIn = 7200;

            // Act
            _tokenStorage.StoreRefreshToken(refreshToken, expiresIn);

            // Assert
            Assert.True(_tokenStorage.HasValidRefreshToken);
            Assert.NotNull(_tokenStorage.RefreshTokenExpirationTime);
            Assert.True(_tokenStorage.RefreshTokenExpirationTime > DateTime.UtcNow);
        }

        [Fact]
        public void StoreRefreshToken_WithNullToken_ThrowsArgumentException()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentException>(() => _tokenStorage.StoreRefreshToken(null!, 3600));
        }

        [Fact]
        public void GetAccessToken_WithValidToken_ReturnsSecureStringCopy()
        {
            // Arrange
            const string token = "test-access-token";
            const int expiresIn = 3600;
            _tokenStorage.StoreAccessToken(token, expiresIn);

            // Act
            using var retrievedToken = _tokenStorage.GetAccessToken();

            // Assert
            Assert.NotNull(retrievedToken);
            Assert.Equal(token.Length, retrievedToken.Length);
        }

        [Fact]
        public void GetAccessToken_WithoutStoredToken_ReturnsNull()
        {
            // Arrange & Act
            var retrievedToken = _tokenStorage.GetAccessToken();

            // Assert
            Assert.Null(retrievedToken);
        }

        [Fact]
        public void GetAccessToken_WithExpiredToken_ReturnsNull()
        {
            // Arrange
            const string token = "test-access-token";
            const int expiresIn = 0; // Expired immediately
            _tokenStorage.StoreAccessToken(token, expiresIn);

            // Wait a bit to ensure expiration
            Thread.Sleep(100);

            // Act
            var retrievedToken = _tokenStorage.GetAccessToken();

            // Assert
            Assert.Null(retrievedToken);
            Assert.False(_tokenStorage.HasValidAccessToken);
        }

        [Fact]
        public void GetAccessTokenAsString_WithValidToken_ReturnsOriginalString()
        {
            // Arrange
            const string token = "test-access-token";
            const int expiresIn = 3600;
            _tokenStorage.StoreAccessToken(token, expiresIn);

            // Act
            var retrievedToken = _tokenStorage.GetAccessTokenAsString();

            // Assert
            Assert.Equal(token, retrievedToken);
        }

        [Fact]
        public void GetRefreshToken_WithValidToken_ReturnsSecureStringCopy()
        {
            // Arrange
            const string refreshToken = "test-refresh-token";
            const int expiresIn = 7200;
            _tokenStorage.StoreRefreshToken(refreshToken, expiresIn);

            // Act
            using var retrievedToken = _tokenStorage.GetRefreshToken();

            // Assert
            Assert.NotNull(retrievedToken);
            Assert.Equal(refreshToken.Length, retrievedToken.Length);
        }

        [Fact]
        public void WillAccessTokenExpireWithin_WithValidToken_ReturnsCorrectResult()
        {
            // Arrange
            const string token = "test-access-token";
            const int expiresIn = 3600; // 1 hour
            _tokenStorage.StoreAccessToken(token, expiresIn);

            // Act & Assert
            Assert.False(_tokenStorage.WillAccessTokenExpireWithin(TimeSpan.FromMinutes(1)));
            Assert.True(_tokenStorage.WillAccessTokenExpireWithin(TimeSpan.FromHours(2)));
        }

        [Fact]
        public void WillAccessTokenExpireWithin_WithoutToken_ReturnsTrue()
        {
            // Arrange & Act
            var willExpire = _tokenStorage.WillAccessTokenExpireWithin(TimeSpan.FromMinutes(1));

            // Assert
            Assert.True(willExpire);
        }

        [Fact]
        public void ClearAccessToken_WithStoredToken_ClearsToken()
        {
            // Arrange
            const string token = "test-access-token";
            const int expiresIn = 3600;
            _tokenStorage.StoreAccessToken(token, expiresIn);

            // Act
            _tokenStorage.ClearAccessToken();

            // Assert
            Assert.False(_tokenStorage.HasValidAccessToken);
            Assert.Null(_tokenStorage.GetAccessToken());
        }

        [Fact]
        public void ClearRefreshToken_WithStoredToken_ClearsToken()
        {
            // Arrange
            const string refreshToken = "test-refresh-token";
            const int expiresIn = 7200;
            _tokenStorage.StoreRefreshToken(refreshToken, expiresIn);

            // Act
            _tokenStorage.ClearRefreshToken();

            // Assert
            Assert.False(_tokenStorage.HasValidRefreshToken);
            Assert.Null(_tokenStorage.GetRefreshToken());
        }

        [Fact]
        public void ClearAllTokens_WithStoredTokens_ClearsAllTokens()
        {
            // Arrange
            const string accessToken = "test-access-token";
            const string refreshToken = "test-refresh-token";
            _tokenStorage.StoreAccessToken(accessToken, 3600);
            _tokenStorage.StoreRefreshToken(refreshToken, 7200);

            // Act
            _tokenStorage.ClearAllTokens();

            // Assert
            Assert.False(_tokenStorage.HasValidAccessToken);
            Assert.False(_tokenStorage.HasValidRefreshToken);
            Assert.Null(_tokenStorage.GetAccessToken());
            Assert.Null(_tokenStorage.GetRefreshToken());
        }

        [Fact]
        public void TokenExpired_Event_FiresWhenAccessTokenExpires()
        {
            // Arrange
            const string token = "test-access-token";
            const int expiresIn = 0; // Expired immediately
            TokenExpiredEventArgs? firedEventArgs = null;

            _tokenStorage.TokenExpired += (sender, e) => firedEventArgs = e;
            _tokenStorage.StoreAccessToken(token, expiresIn);

            // Wait a bit to ensure expiration
            Thread.Sleep(100);

            // Act
            var retrievedToken = _tokenStorage.GetAccessToken(); // This should trigger the event

            // Assert
            Assert.Null(retrievedToken);
            Assert.NotNull(firedEventArgs);
            Assert.Equal(TokenType.Access, firedEventArgs.TokenType);
        }

        [Fact]
        public void TokenExpired_Event_FiresWhenRefreshTokenExpires()
        {
            // Arrange
            const string refreshToken = "test-refresh-token";
            const int expiresIn = 0; // Expired immediately
            TokenExpiredEventArgs? firedEventArgs = null;

            _tokenStorage.TokenExpired += (sender, e) => firedEventArgs = e;
            _tokenStorage.StoreRefreshToken(refreshToken, expiresIn);

            // Wait a bit to ensure expiration
            Thread.Sleep(100);

            // Act
            var retrievedToken = _tokenStorage.GetRefreshToken(); // This should trigger the event

            // Assert
            Assert.Null(retrievedToken);
            Assert.NotNull(firedEventArgs);
            Assert.Equal(TokenType.Refresh, firedEventArgs.TokenType);
        }

        [Fact]
        public void MultipleTokenOperations_ThreadSafety_WorksCorrectly()
        {
            // Arrange
            const int threadCount = 10;
            const int operationsPerThread = 100;
            var tasks = new Task[threadCount];
            var exceptions = new List<Exception>();

            // Act
            for (int i = 0; i < threadCount; i++)
            {
                int threadIndex = i;
                tasks[i] = Task.Run(() =>
                {
                    try
                    {
                        for (int j = 0; j < operationsPerThread; j++)
                        {
                            var token = $"token-{threadIndex}-{j}";
                            _tokenStorage.StoreAccessToken(token, 3600);
                            var retrieved = _tokenStorage.GetAccessTokenAsString();
                            _tokenStorage.ClearAccessToken();
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (exceptions)
                        {
                            exceptions.Add(ex);
                        }
                    }
                });
            }

            Task.WaitAll(tasks);

            // Assert
            Assert.Empty(exceptions);
            Assert.False(_tokenStorage.HasValidAccessToken);
        }

        [Fact]
        public void StoreAccessToken_ReplacesExistingToken_Successfully()
        {
            // Arrange
            const string firstToken = "first-token";
            const string secondToken = "second-token";
            _tokenStorage.StoreAccessToken(firstToken, 3600);

            // Act
            _tokenStorage.StoreAccessToken(secondToken, 3600);

            // Assert
            var retrievedToken = _tokenStorage.GetAccessTokenAsString();
            Assert.Equal(secondToken, retrievedToken);
        }

        [Fact]
        public void Dispose_WithStoredTokens_ClearsAllTokens()
        {
            // Arrange
            const string accessToken = "test-access-token";
            const string refreshToken = "test-refresh-token";
            _tokenStorage.StoreAccessToken(accessToken, 3600);
            _tokenStorage.StoreRefreshToken(refreshToken, 7200);

            // Act
            _tokenStorage.Dispose();

            // Assert - Operations after dispose should throw
            Assert.Throws<ObjectDisposedException>(() => _tokenStorage.HasValidAccessToken);
        }

        [Fact]
        public void Operations_AfterDispose_ThrowObjectDisposedException()
        {
            // Arrange
            _tokenStorage.Dispose();

            // Act & Assert
            Assert.Throws<ObjectDisposedException>(() => _tokenStorage.StoreAccessToken("token", 3600));
            Assert.Throws<ObjectDisposedException>(() => _tokenStorage.GetAccessToken());
            Assert.Throws<ObjectDisposedException>(() => _tokenStorage.ClearAccessToken());
        }

        public void Dispose()
        {
            _tokenStorage?.Dispose();
        }
    }
}