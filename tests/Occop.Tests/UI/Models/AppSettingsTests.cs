using System;
using System.ComponentModel;
using Xunit;
using Occop.UI.Models;

namespace Occop.Tests.UI.Models
{
    /// <summary>
    /// Tests for AppSettings class
    /// AppSettings类的测试
    /// </summary>
    public class AppSettingsTests
    {
        /// <summary>
        /// Test that AppSettings can be created with default values
        /// 测试AppSettings能够使用默认值创建
        /// </summary>
        [Fact]
        public void Constructor_Default_ShouldCreateInstanceWithDefaultValues()
        {
            // Act
            var settings = new AppSettings();

            // Assert
            Assert.Equal(AppTheme.System, settings.Theme);
            Assert.Equal(1.0, settings.WindowOpacity);
            Assert.True(settings.EnableAnimations);
            Assert.False(settings.StartWithWindows);
            Assert.Equal(WindowStartupState.Normal, settings.WindowStartupState);
            Assert.True(settings.CheckForUpdatesOnStartup);
            Assert.True(settings.EnableNotifications);
            Assert.True(settings.EnableSoundNotifications);
            Assert.True(settings.EnableTrayNotifications);
            Assert.Equal(NotificationPriority.Normal, settings.MinimumNotificationPriority);
            Assert.Equal(TimeSpan.FromSeconds(5), settings.NotificationDuration);
            Assert.True(settings.MinimizeToTray);
            Assert.False(settings.CloseToTray);
            Assert.True(settings.ShowTrayIcon);
            Assert.Equal(480, settings.SessionTimeoutMinutes);
            Assert.Equal(3, settings.MaxFailedAttempts);
            Assert.Equal(15, settings.LockoutDurationMinutes);
            Assert.True(settings.RememberAuthentication);
            Assert.Equal("Information", settings.LogLevel);
            Assert.True(settings.EnableFileLogging);
            Assert.Equal(30, settings.LogRetentionDays);
            Assert.False(settings.EnableDebugMode);
            Assert.False(settings.EnableTelemetry);
            Assert.Equal("zh-CN", settings.Language);
        }

        /// <summary>
        /// Test that WindowOpacity is clamped to valid range
        /// 测试WindowOpacity被限制在有效范围内
        /// </summary>
        [Theory]
        [InlineData(-0.5, 0.0)]
        [InlineData(0.0, 0.0)]
        [InlineData(0.5, 0.5)]
        [InlineData(1.0, 1.0)]
        [InlineData(1.5, 1.0)]
        public void WindowOpacity_ShouldBeClampedToValidRange(double input, double expected)
        {
            // Arrange
            var settings = new AppSettings();

            // Act
            settings.WindowOpacity = input;

            // Assert
            Assert.Equal(expected, settings.WindowOpacity);
        }

        /// <summary>
        /// Test that SessionTimeoutMinutes is clamped to minimum value
        /// 测试SessionTimeoutMinutes被限制到最小值
        /// </summary>
        [Theory]
        [InlineData(-10, 1)]
        [InlineData(0, 1)]
        [InlineData(1, 1)]
        [InlineData(60, 60)]
        public void SessionTimeoutMinutes_ShouldBeClampedToMinimum(int input, int expected)
        {
            // Arrange
            var settings = new AppSettings();

            // Act
            settings.SessionTimeoutMinutes = input;

            // Assert
            Assert.Equal(expected, settings.SessionTimeoutMinutes);
        }

        /// <summary>
        /// Test that MaxFailedAttempts is clamped to minimum value
        /// 测试MaxFailedAttempts被限制到最小值
        /// </summary>
        [Theory]
        [InlineData(-5, 1)]
        [InlineData(0, 1)]
        [InlineData(1, 1)]
        [InlineData(5, 5)]
        public void MaxFailedAttempts_ShouldBeClampedToMinimum(int input, int expected)
        {
            // Arrange
            var settings = new AppSettings();

            // Act
            settings.MaxFailedAttempts = input;

            // Assert
            Assert.Equal(expected, settings.MaxFailedAttempts);
        }

        /// <summary>
        /// Test that Clone creates an exact copy
        /// 测试Clone创建精确副本
        /// </summary>
        [Fact]
        public void Clone_ShouldCreateExactCopy()
        {
            // Arrange
            var original = new AppSettings
            {
                Theme = AppTheme.Dark,
                WindowOpacity = 0.8,
                EnableAnimations = false,
                StartWithWindows = true,
                WindowStartupState = WindowStartupState.MinimizedToTray,
                CheckForUpdatesOnStartup = false,
                EnableNotifications = false,
                SessionTimeoutMinutes = 120,
                LogLevel = "Debug",
                EnableDebugMode = true
            };

            // Act
            var clone = original.Clone();

            // Assert
            Assert.Equal(original.Theme, clone.Theme);
            Assert.Equal(original.WindowOpacity, clone.WindowOpacity);
            Assert.Equal(original.EnableAnimations, clone.EnableAnimations);
            Assert.Equal(original.StartWithWindows, clone.StartWithWindows);
            Assert.Equal(original.WindowStartupState, clone.WindowStartupState);
            Assert.Equal(original.CheckForUpdatesOnStartup, clone.CheckForUpdatesOnStartup);
            Assert.Equal(original.EnableNotifications, clone.EnableNotifications);
            Assert.Equal(original.SessionTimeoutMinutes, clone.SessionTimeoutMinutes);
            Assert.Equal(original.LogLevel, clone.LogLevel);
            Assert.Equal(original.EnableDebugMode, clone.EnableDebugMode);
        }

        /// <summary>
        /// Test that CopyFrom copies all values from source
        /// 测试CopyFrom从源复制所有值
        /// </summary>
        [Fact]
        public void CopyFrom_ShouldCopyAllValues()
        {
            // Arrange
            var source = new AppSettings
            {
                Theme = AppTheme.Light,
                WindowOpacity = 0.9,
                EnableAnimations = false,
                EnableDebugMode = true
            };

            var target = new AppSettings();

            // Act
            target.CopyFrom(source);

            // Assert
            Assert.Equal(source.Theme, target.Theme);
            Assert.Equal(source.WindowOpacity, target.WindowOpacity);
            Assert.Equal(source.EnableAnimations, target.EnableAnimations);
            Assert.Equal(source.EnableDebugMode, target.EnableDebugMode);
        }

        /// <summary>
        /// Test that CopyFrom throws ArgumentNullException for null source
        /// 测试CopyFrom对null源抛出ArgumentNullException
        /// </summary>
        [Fact]
        public void CopyFrom_NullSource_ShouldThrowArgumentNullException()
        {
            // Arrange
            var target = new AppSettings();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => target.CopyFrom(null!));
        }

        /// <summary>
        /// Test that AppSettings implements INotifyPropertyChanged
        /// 测试AppSettings实现INotifyPropertyChanged
        /// </summary>
        [Fact]
        public void PropertyChanged_ShouldBeRaisedWhenPropertyChanges()
        {
            // Arrange
            var settings = new AppSettings();
            var propertyChangedRaised = false;
            string? changedPropertyName = null;

            settings.PropertyChanged += (sender, e) =>
            {
                propertyChangedRaised = true;
                changedPropertyName = e.PropertyName;
            };

            // Act
            settings.Theme = AppTheme.Dark;

            // Assert
            Assert.True(propertyChangedRaised);
            Assert.Equal(nameof(AppSettings.Theme), changedPropertyName);
        }

        /// <summary>
        /// Test that LogLevel handles null values
        /// 测试LogLevel处理null值
        /// </summary>
        [Fact]
        public void LogLevel_NullValue_ShouldUseDefault()
        {
            // Arrange
            var settings = new AppSettings();

            // Act
            settings.LogLevel = null!;

            // Assert
            Assert.Equal("Information", settings.LogLevel);
        }

        /// <summary>
        /// Test that Language handles null values
        /// 测试Language处理null值
        /// </summary>
        [Fact]
        public void Language_NullValue_ShouldUseDefault()
        {
            // Arrange
            var settings = new AppSettings();

            // Act
            settings.Language = null!;

            // Assert
            Assert.Equal("zh-CN", settings.Language);
        }
    }
}
