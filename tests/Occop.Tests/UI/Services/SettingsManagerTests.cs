using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Occop.UI.Models;
using Occop.UI.Services;

namespace Occop.Tests.UI.Services
{
    /// <summary>
    /// Tests for SettingsManager class
    /// SettingsManager类的测试
    /// </summary>
    public class SettingsManagerTests : IDisposable
    {
        private readonly Mock<ILogger<SettingsManager>> _mockLogger;
        private readonly SettingsManager _settingsManager;
        private readonly string _testSettingsPath;

        public SettingsManagerTests()
        {
            _mockLogger = new Mock<ILogger<SettingsManager>>();
            _settingsManager = new SettingsManager(_mockLogger.Object);

            // Get test settings path for cleanup
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _testSettingsPath = Path.Combine(appDataPath, "Occop", "settings.json");
        }

        public void Dispose()
        {
            _settingsManager?.Dispose();

            // Clean up test settings file
            if (File.Exists(_testSettingsPath))
            {
                try
                {
                    File.Delete(_testSettingsPath);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        /// <summary>
        /// Test that SettingsManager can be created
        /// 测试SettingsManager能够创建
        /// </summary>
        [Fact]
        public void Constructor_ShouldCreateInstance()
        {
            // Assert
            Assert.NotNull(_settingsManager);
            Assert.NotNull(_settingsManager.CurrentSettings);
        }

        /// <summary>
        /// Test that constructor throws on null logger
        /// 测试构造函数在logger为null时抛出异常
        /// </summary>
        [Fact]
        public void Constructor_NullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new SettingsManager(null!));
        }

        /// <summary>
        /// Test that LoadSettingsAsync creates default settings if file doesn't exist
        /// 测试LoadSettingsAsync在文件不存在时创建默认设置
        /// </summary>
        [Fact]
        public async Task LoadSettingsAsync_NoFile_ShouldCreateDefaultSettings()
        {
            // Arrange
            if (File.Exists(_testSettingsPath))
            {
                File.Delete(_testSettingsPath);
            }

            // Act
            var settings = await _settingsManager.LoadSettingsAsync();

            // Assert
            Assert.NotNull(settings);
            Assert.Equal(AppTheme.System, settings.Theme);
            Assert.True(File.Exists(_testSettingsPath));
        }

        /// <summary>
        /// Test that SaveSettingsAsync persists settings to file
        /// 测试SaveSettingsAsync将设置持久化到文件
        /// </summary>
        [Fact]
        public async Task SaveSettingsAsync_ShouldPersistSettings()
        {
            // Arrange
            var settings = new AppSettings
            {
                Theme = AppTheme.Dark,
                EnableDebugMode = true,
                SessionTimeoutMinutes = 120
            };

            // Act
            await _settingsManager.SaveSettingsAsync(settings);

            // Assert
            Assert.True(File.Exists(_testSettingsPath));
            var content = await File.ReadAllTextAsync(_testSettingsPath);
            Assert.Contains("\"Theme\"", content);
            Assert.Contains("\"EnableDebugMode\"", content);
        }

        /// <summary>
        /// Test that SaveSettingsAsync throws on null settings
        /// 测试SaveSettingsAsync在settings为null时抛出异常
        /// </summary>
        [Fact]
        public async Task SaveSettingsAsync_NullSettings_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _settingsManager.SaveSettingsAsync(null!));
        }

        /// <summary>
        /// Test that SaveSettingsAsync raises SettingsChanged event
        /// 测试SaveSettingsAsync触发SettingsChanged事件
        /// </summary>
        [Fact]
        public async Task SaveSettingsAsync_ShouldRaiseSettingsChangedEvent()
        {
            // Arrange
            var settings = new AppSettings { Theme = AppTheme.Light };
            var eventRaised = false;
            AppSettings? eventSettings = null;

            _settingsManager.SettingsChanged += (sender, s) =>
            {
                eventRaised = true;
                eventSettings = s;
            };

            // Act
            await _settingsManager.SaveSettingsAsync(settings);

            // Assert
            Assert.True(eventRaised);
            Assert.NotNull(eventSettings);
            Assert.Equal(AppTheme.Light, eventSettings.Theme);
        }

        /// <summary>
        /// Test that ResetSettingsAsync resets to default values
        /// 测试ResetSettingsAsync重置为默认值
        /// </summary>
        [Fact]
        public async Task ResetSettingsAsync_ShouldResetToDefaults()
        {
            // Arrange
            var customSettings = new AppSettings
            {
                Theme = AppTheme.Dark,
                EnableDebugMode = true
            };
            await _settingsManager.SaveSettingsAsync(customSettings);

            // Act
            await _settingsManager.ResetSettingsAsync();

            // Assert
            var currentSettings = _settingsManager.CurrentSettings;
            Assert.Equal(AppTheme.System, currentSettings.Theme);
            Assert.False(currentSettings.EnableDebugMode);
        }

        /// <summary>
        /// Test that ValidateSettings returns empty list for valid settings
        /// 测试ValidateSettings对有效设置返回空列表
        /// </summary>
        [Fact]
        public void ValidateSettings_ValidSettings_ShouldReturnEmptyList()
        {
            // Arrange
            var settings = new AppSettings();

            // Act
            var errors = _settingsManager.ValidateSettings(settings);

            // Assert
            Assert.Empty(errors);
        }

        /// <summary>
        /// Test that ValidateSettings detects invalid window opacity
        /// 测试ValidateSettings检测无效的窗口不透明度
        /// </summary>
        [Theory]
        [InlineData(-0.5)]
        [InlineData(1.5)]
        public void ValidateSettings_InvalidWindowOpacity_ShouldReturnError(double opacity)
        {
            // Arrange - use reflection to bypass property setter validation
            var settings = new AppSettings();
            var property = typeof(AppSettings).GetProperty(nameof(AppSettings.WindowOpacity));
            var backingField = typeof(AppSettings).GetField("_windowOpacity",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            backingField!.SetValue(settings, opacity);

            // Act
            var errors = _settingsManager.ValidateSettings(settings);

            // Assert
            Assert.NotEmpty(errors);
            Assert.Contains(errors, e => e.Contains("不透明度"));
        }

        /// <summary>
        /// Test that ValidateSettings detects invalid log level
        /// 测试ValidateSettings检测无效的日志级别
        /// </summary>
        [Fact]
        public void ValidateSettings_InvalidLogLevel_ShouldReturnError()
        {
            // Arrange
            var settings = new AppSettings { LogLevel = "InvalidLevel" };

            // Act
            var errors = _settingsManager.ValidateSettings(settings);

            // Assert
            Assert.NotEmpty(errors);
            Assert.Contains(errors, e => e.Contains("日志级别"));
        }

        /// <summary>
        /// Test that ValidateSettings returns error for null settings
        /// 测试ValidateSettings对null设置返回错误
        /// </summary>
        [Fact]
        public void ValidateSettings_NullSettings_ShouldReturnError()
        {
            // Act
            var errors = _settingsManager.ValidateSettings(null!);

            // Assert
            Assert.NotEmpty(errors);
            Assert.Contains(errors, e => e.Contains("不能为空"));
        }

        /// <summary>
        /// Test that ExportSettingsAsync exports to file
        /// 测试ExportSettingsAsync导出到文件
        /// </summary>
        [Fact]
        public async Task ExportSettingsAsync_ShouldExportToFile()
        {
            // Arrange
            var exportPath = Path.Combine(Path.GetTempPath(), $"test_export_{Guid.NewGuid()}.json");
            var settings = new AppSettings { Theme = AppTheme.Dark };
            await _settingsManager.SaveSettingsAsync(settings);

            try
            {
                // Act
                await _settingsManager.ExportSettingsAsync(exportPath);

                // Assert
                Assert.True(File.Exists(exportPath));
                var content = await File.ReadAllTextAsync(exportPath);
                Assert.Contains("\"Theme\"", content);
            }
            finally
            {
                if (File.Exists(exportPath))
                {
                    File.Delete(exportPath);
                }
            }
        }

        /// <summary>
        /// Test that ImportSettingsAsync imports from file
        /// 测试ImportSettingsAsync从文件导入
        /// </summary>
        [Fact]
        public async Task ImportSettingsAsync_ShouldImportFromFile()
        {
            // Arrange
            var importPath = Path.Combine(Path.GetTempPath(), $"test_import_{Guid.NewGuid()}.json");
            var exportSettings = new AppSettings { Theme = AppTheme.Light, EnableDebugMode = true };

            await _settingsManager.SaveSettingsAsync(exportSettings);
            await _settingsManager.ExportSettingsAsync(importPath);

            try
            {
                // Change current settings
                await _settingsManager.SaveSettingsAsync(new AppSettings { Theme = AppTheme.Dark });

                // Act
                var importedSettings = await _settingsManager.ImportSettingsAsync(importPath);

                // Assert
                Assert.NotNull(importedSettings);
                Assert.Equal(AppTheme.Light, importedSettings.Theme);
                Assert.True(importedSettings.EnableDebugMode);
            }
            finally
            {
                if (File.Exists(importPath))
                {
                    File.Delete(importPath);
                }
            }
        }

        /// <summary>
        /// Test that ImportSettingsAsync throws on invalid file
        /// 测试ImportSettingsAsync在无效文件时抛出异常
        /// </summary>
        [Fact]
        public async Task ImportSettingsAsync_InvalidFile_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var invalidPath = Path.Combine(Path.GetTempPath(), $"invalid_{Guid.NewGuid()}.json");
            await File.WriteAllTextAsync(invalidPath, "{ invalid json }");

            try
            {
                // Act & Assert
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    _settingsManager.ImportSettingsAsync(invalidPath));
            }
            finally
            {
                if (File.Exists(invalidPath))
                {
                    File.Delete(invalidPath);
                }
            }
        }

        /// <summary>
        /// Test that ApplySettingsAsync applies settings
        /// 测试ApplySettingsAsync应用设置
        /// </summary>
        [Fact]
        public async Task ApplySettingsAsync_ShouldApplySettings()
        {
            // Arrange
            var settings = new AppSettings { Theme = AppTheme.Dark };

            // Act
            await _settingsManager.ApplySettingsAsync(settings);

            // Assert
            Assert.Equal(AppTheme.Dark, _settingsManager.CurrentSettings.Theme);
        }
    }
}
