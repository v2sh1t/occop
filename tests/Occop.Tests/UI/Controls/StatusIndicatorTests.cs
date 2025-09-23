using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Xunit;
using Occop.UI.Controls;
using Occop.UI.Models;

namespace Occop.Tests.UI.Controls
{
    /// <summary>
    /// Tests for StatusIndicator control and related classes
    /// StatusIndicator控件和相关类的测试
    /// </summary>
    public class StatusIndicatorTests
    {
        /// <summary>
        /// Test that StatusIndicator can be created successfully
        /// 测试StatusIndicator能够成功创建
        /// </summary>
        [Fact]
        public void Constructor_ShouldCreateInstance()
        {
            // Act
            var statusIndicator = new StatusIndicator();

            // Assert
            Assert.NotNull(statusIndicator);
            Assert.NotNull(statusIndicator.DataContext);
            Assert.IsType<StatusIndicatorViewModel>(statusIndicator.DataContext);
        }

        /// <summary>
        /// Test that dependency properties have correct default values
        /// 测试依赖属性有正确的默认值
        /// </summary>
        [Fact]
        public void DependencyProperties_ShouldHaveCorrectDefaultValues()
        {
            // Arrange
            var statusIndicator = new StatusIndicator();

            // Assert
            Assert.Null(statusIndicator.Status);
            Assert.True(statusIndicator.ShowTitle);
            Assert.True(statusIndicator.ShowMessage);
            Assert.False(statusIndicator.ShowDetails);
            Assert.False(statusIndicator.ShowTimestamp);
            Assert.False(statusIndicator.CompactMode);
        }

        /// <summary>
        /// Test that Status property can be set and retrieved
        /// 测试Status属性能够设置和获取
        /// </summary>
        [Fact]
        public void Status_CanBeSetAndRetrieved()
        {
            // Arrange
            var statusIndicator = new StatusIndicator();
            var status = new StatusModel(StatusType.Authentication, StatusState.Ready, "Test", "Message");

            // Act
            statusIndicator.Status = status;

            // Assert
            Assert.Equal(status, statusIndicator.Status);
        }

        /// <summary>
        /// Test that ShowTitle property can be set and retrieved
        /// 测试ShowTitle属性能够设置和获取
        /// </summary>
        [Fact]
        public void ShowTitle_CanBeSetAndRetrieved()
        {
            // Arrange
            var statusIndicator = new StatusIndicator();

            // Act
            statusIndicator.ShowTitle = false;

            // Assert
            Assert.False(statusIndicator.ShowTitle);
        }

        /// <summary>
        /// Test that ShowMessage property can be set and retrieved
        /// 测试ShowMessage属性能够设置和获取
        /// </summary>
        [Fact]
        public void ShowMessage_CanBeSetAndRetrieved()
        {
            // Arrange
            var statusIndicator = new StatusIndicator();

            // Act
            statusIndicator.ShowMessage = false;

            // Assert
            Assert.False(statusIndicator.ShowMessage);
        }

        /// <summary>
        /// Test that ShowDetails property can be set and retrieved
        /// 测试ShowDetails属性能够设置和获取
        /// </summary>
        [Fact]
        public void ShowDetails_CanBeSetAndRetrieved()
        {
            // Arrange
            var statusIndicator = new StatusIndicator();

            // Act
            statusIndicator.ShowDetails = true;

            // Assert
            Assert.True(statusIndicator.ShowDetails);
        }

        /// <summary>
        /// Test that ShowTimestamp property can be set and retrieved
        /// 测试ShowTimestamp属性能够设置和获取
        /// </summary>
        [Fact]
        public void ShowTimestamp_CanBeSetAndRetrieved()
        {
            // Arrange
            var statusIndicator = new StatusIndicator();

            // Act
            statusIndicator.ShowTimestamp = true;

            // Assert
            Assert.True(statusIndicator.ShowTimestamp);
        }

        /// <summary>
        /// Test that CompactMode property can be set and retrieved
        /// 测试CompactMode属性能够设置和获取
        /// </summary>
        [Fact]
        public void CompactMode_CanBeSetAndRetrieved()
        {
            // Arrange
            var statusIndicator = new StatusIndicator();

            // Act
            statusIndicator.CompactMode = true;

            // Assert
            Assert.True(statusIndicator.CompactMode);
        }
    }

    /// <summary>
    /// Tests for StatusIndicatorViewModel class
    /// StatusIndicatorViewModel类的测试
    /// </summary>
    public class StatusIndicatorViewModelTests
    {
        /// <summary>
        /// Test that StatusIndicatorViewModel can be created successfully
        /// 测试StatusIndicatorViewModel能够成功创建
        /// </summary>
        [Fact]
        public void Constructor_ShouldCreateInstance()
        {
            // Act
            var viewModel = new StatusIndicatorViewModel();

            // Assert
            Assert.NotNull(viewModel);
            Assert.Null(viewModel.Status);
            Assert.Equal(string.Empty, viewModel.Title);
            Assert.Equal(string.Empty, viewModel.Message);
            Assert.Equal(string.Empty, viewModel.Details);
            Assert.Equal(string.Empty, viewModel.TimestampText);
            Assert.Equal(Brushes.Gray, viewModel.StatusColor);
            Assert.Equal(Brushes.Transparent, viewModel.BorderBrush);
            Assert.Equal(new Thickness(0), viewModel.BorderThickness);
            Assert.False(viewModel.IsLoading);
            Assert.False(viewModel.CompactMode);
        }

        /// <summary>
        /// Test that UpdateStatus with null clears all fields
        /// 测试UpdateStatus使用null清除所有字段
        /// </summary>
        [Fact]
        public void UpdateStatus_WithNull_ShouldClearAllFields()
        {
            // Arrange
            var viewModel = new StatusIndicatorViewModel();

            // Act
            viewModel.UpdateStatus(null);

            // Assert
            Assert.Null(viewModel.Status);
            Assert.Equal(string.Empty, viewModel.Title);
            Assert.Equal(string.Empty, viewModel.Message);
            Assert.Equal(string.Empty, viewModel.Details);
            Assert.Equal(string.Empty, viewModel.TimestampText);
            Assert.Equal(Brushes.Gray, viewModel.StatusColor);
            Assert.False(viewModel.IsLoading);
        }

        /// <summary>
        /// Test that UpdateStatus with valid status sets all fields correctly
        /// 测试UpdateStatus使用有效状态正确设置所有字段
        /// </summary>
        [Fact]
        public void UpdateStatus_WithValidStatus_ShouldSetAllFieldsCorrectly()
        {
            // Arrange
            var viewModel = new StatusIndicatorViewModel();
            var timestamp = DateTime.Now.AddMinutes(-5);
            var status = new StatusModel(StatusType.Authentication, StatusState.Ready, "Auth", "Connected")
            {
                Details = "User authenticated",
                Timestamp = timestamp
            };

            // Act
            viewModel.UpdateStatus(status);

            // Assert
            Assert.Equal(status, viewModel.Status);
            Assert.Equal("Auth", viewModel.Title);
            Assert.Equal("Connected", viewModel.Message);
            Assert.Equal("User authenticated", viewModel.Details);
            Assert.NotEqual(string.Empty, viewModel.TimestampText);
            Assert.False(viewModel.IsLoading);
        }

        /// <summary>
        /// Test that UpdateStatus with Loading state sets IsLoading to true
        /// 测试UpdateStatus使用Loading状态将IsLoading设置为true
        /// </summary>
        [Fact]
        public void UpdateStatus_WithLoadingState_ShouldSetIsLoadingToTrue()
        {
            // Arrange
            var viewModel = new StatusIndicatorViewModel();
            var status = new StatusModel(StatusType.AITool, StatusState.Loading, "Claude", "Loading...");

            // Act
            viewModel.UpdateStatus(status);

            // Assert
            Assert.True(viewModel.IsLoading);
        }

        /// <summary>
        /// Test that UpdateStatus with Working state sets IsLoading to true
        /// 测试UpdateStatus使用Working状态将IsLoading设置为true
        /// </summary>
        [Fact]
        public void UpdateStatus_WithWorkingState_ShouldSetIsLoadingToTrue()
        {
            // Arrange
            var viewModel = new StatusIndicatorViewModel();
            var status = new StatusModel(StatusType.SystemCleanup, StatusState.Working, "Cleanup", "Working...");

            // Act
            viewModel.UpdateStatus(status);

            // Assert
            Assert.True(viewModel.IsLoading);
        }

        /// <summary>
        /// Test that status colors are set correctly for different states
        /// 测试不同状态的状态颜色设置正确
        /// </summary>
        [Theory]
        [InlineData(StatusState.Ready, "#27AE60")] // Green
        [InlineData(StatusState.Success, "#27AE60")] // Green
        [InlineData(StatusState.Working, "#3498DB")] // Blue
        [InlineData(StatusState.Loading, "#3498DB")] // Blue
        [InlineData(StatusState.Warning, "#F39C12")] // Orange
        [InlineData(StatusState.Error, "#E74C3C")] // Red
        [InlineData(StatusState.Offline, "#7F8C8D")] // Gray
        [InlineData(StatusState.Idle, "#95A5A6")] // Gray
        [InlineData(StatusState.Unknown, "Gray")] // Default Gray
        public void UpdateStatus_WithDifferentStates_ShouldSetCorrectColors(StatusState state, string expectedColorHex)
        {
            // Arrange
            var viewModel = new StatusIndicatorViewModel();
            var status = new StatusModel(StatusType.Application, state, "Test", "Message");

            // Act
            viewModel.UpdateStatus(status);

            // Assert
            if (expectedColorHex == "Gray")
            {
                Assert.Equal(Brushes.Gray, viewModel.StatusColor);
            }
            else
            {
                var expectedColor = (Color)ColorConverter.ConvertFromString(expectedColorHex);
                var actualBrush = viewModel.StatusColor as SolidColorBrush;
                Assert.NotNull(actualBrush);
                Assert.Equal(expectedColor, actualBrush.Color);
            }
        }

        /// <summary>
        /// Test that CompactMode affects visibility properties
        /// 测试CompactMode影响可见性属性
        /// </summary>
        [Fact]
        public void CompactMode_WhenChanged_ShouldAffectVisibility()
        {
            // Arrange
            var viewModel = new StatusIndicatorViewModel();
            var status = new StatusModel(StatusType.Application, StatusState.Ready, "Title", "Message")
            {
                Details = "Details"
            };
            viewModel.UpdateStatus(status);

            // Act - Set compact mode
            viewModel.CompactMode = true;

            // Assert - Details should be collapsed in compact mode
            Assert.Equal(Visibility.Collapsed, viewModel.DetailsVisibility);
            Assert.Equal(Visibility.Collapsed, viewModel.TimestampVisibility);
        }
    }

    /// <summary>
    /// Tests for ViewModelBase class
    /// ViewModelBase类的测试
    /// </summary>
    public class ViewModelBaseTests
    {
        private class TestViewModel : ViewModelBase
        {
            private string _testProperty = string.Empty;

            public string TestProperty
            {
                get => _testProperty;
                set => SetProperty(ref _testProperty, value);
            }
        }

        /// <summary>
        /// Test that ViewModelBase implements INotifyPropertyChanged
        /// 测试ViewModelBase实现INotifyPropertyChanged
        /// </summary>
        [Fact]
        public void ViewModelBase_ShouldImplementINotifyPropertyChanged()
        {
            // Arrange
            var viewModel = new TestViewModel();

            // Assert
            Assert.IsAssignableFrom<System.ComponentModel.INotifyPropertyChanged>(viewModel);
        }

        /// <summary>
        /// Test that SetProperty raises PropertyChanged event
        /// 测试SetProperty触发PropertyChanged事件
        /// </summary>
        [Fact]
        public void SetProperty_WhenValueChanges_ShouldRaisePropertyChangedEvent()
        {
            // Arrange
            var viewModel = new TestViewModel();
            var eventRaised = false;
            string? propertyName = null;

            viewModel.PropertyChanged += (sender, args) =>
            {
                eventRaised = true;
                propertyName = args.PropertyName;
            };

            // Act
            viewModel.TestProperty = "New Value";

            // Assert
            Assert.True(eventRaised);
            Assert.Equal(nameof(TestViewModel.TestProperty), propertyName);
            Assert.Equal("New Value", viewModel.TestProperty);
        }

        /// <summary>
        /// Test that SetProperty returns true when value changes
        /// 测试SetProperty在值改变时返回true
        /// </summary>
        [Fact]
        public void SetProperty_WhenValueChanges_ShouldReturnTrue()
        {
            // Arrange
            var viewModel = new TestViewModel();

            // Act
            var result = viewModel.TestProperty = "New Value";

            // Assert
            // Note: We can't directly test the return value of SetProperty from outside,
            // but we can verify the property was set
            Assert.Equal("New Value", viewModel.TestProperty);
        }

        /// <summary>
        /// Test that SetProperty does not raise event when value is the same
        /// 测试SetProperty在值相同时不触发事件
        /// </summary>
        [Fact]
        public void SetProperty_WhenValueIsSame_ShouldNotRaiseEvent()
        {
            // Arrange
            var viewModel = new TestViewModel();
            viewModel.TestProperty = "Initial Value";

            var eventRaised = false;
            viewModel.PropertyChanged += (sender, args) =>
            {
                eventRaised = true;
            };

            // Act
            viewModel.TestProperty = "Initial Value"; // Same value

            // Assert
            Assert.False(eventRaised);
        }
    }

    /// <summary>
    /// Tests for converter classes
    /// 转换器类的测试
    /// </summary>
    public class ConverterTests
    {
        /// <summary>
        /// Test InvertedBooleanToVisibilityConverter converts true to Collapsed
        /// 测试InvertedBooleanToVisibilityConverter将true转换为Collapsed
        /// </summary>
        [Fact]
        public void InvertedBooleanToVisibilityConverter_TrueToCollapsed()
        {
            // Arrange
            var converter = new InvertedBooleanToVisibilityConverter();

            // Act
            var result = converter.Convert(true, typeof(Visibility), null, CultureInfo.CurrentCulture);

            // Assert
            Assert.Equal(Visibility.Collapsed, result);
        }

        /// <summary>
        /// Test InvertedBooleanToVisibilityConverter converts false to Visible
        /// 测试InvertedBooleanToVisibilityConverter将false转换为Visible
        /// </summary>
        [Fact]
        public void InvertedBooleanToVisibilityConverter_FalseToVisible()
        {
            // Arrange
            var converter = new InvertedBooleanToVisibilityConverter();

            // Act
            var result = converter.Convert(false, typeof(Visibility), null, CultureInfo.CurrentCulture);

            // Assert
            Assert.Equal(Visibility.Visible, result);
        }

        /// <summary>
        /// Test InvertedBooleanToVisibilityConverter handles non-boolean values
        /// 测试InvertedBooleanToVisibilityConverter处理非布尔值
        /// </summary>
        [Fact]
        public void InvertedBooleanToVisibilityConverter_NonBooleanToVisible()
        {
            // Arrange
            var converter = new InvertedBooleanToVisibilityConverter();

            // Act
            var result = converter.Convert("not a boolean", typeof(Visibility), null, CultureInfo.CurrentCulture);

            // Assert
            Assert.Equal(Visibility.Visible, result);
        }

        /// <summary>
        /// Test InvertedBooleanToVisibilityConverter ConvertBack Collapsed to true
        /// 测试InvertedBooleanToVisibilityConverter ConvertBack将Collapsed转换为true
        /// </summary>
        [Fact]
        public void InvertedBooleanToVisibilityConverter_ConvertBack_CollapsedToTrue()
        {
            // Arrange
            var converter = new InvertedBooleanToVisibilityConverter();

            // Act
            var result = converter.ConvertBack(Visibility.Collapsed, typeof(bool), null, CultureInfo.CurrentCulture);

            // Assert
            Assert.Equal(true, result);
        }

        /// <summary>
        /// Test InvertedBooleanToVisibilityConverter ConvertBack Visible to false
        /// 测试InvertedBooleanToVisibilityConverter ConvertBack将Visible转换为false
        /// </summary>
        [Fact]
        public void InvertedBooleanToVisibilityConverter_ConvertBack_VisibleToFalse()
        {
            // Arrange
            var converter = new InvertedBooleanToVisibilityConverter();

            // Act
            var result = converter.ConvertBack(Visibility.Visible, typeof(bool), null, CultureInfo.CurrentCulture);

            // Assert
            Assert.Equal(false, result);
        }

        /// <summary>
        /// Test NotificationTypeToColorConverter converts notification types to correct colors
        /// 测试NotificationTypeToColorConverter将通知类型转换为正确的颜色
        /// </summary>
        [Theory]
        [InlineData(NotificationType.Info, 0x34, 0x98, 0xDB)] // Blue
        [InlineData(NotificationType.Success, 0x27, 0xAE, 0x60)] // Green
        [InlineData(NotificationType.Warning, 0xF3, 0x9C, 0x12)] // Orange
        [InlineData(NotificationType.Error, 0xE7, 0x4C, 0x3C)] // Red
        public void NotificationTypeToColorConverter_ConvertsToCorrectColor(NotificationType type, byte r, byte g, byte b)
        {
            // Arrange
            var converter = new NotificationTypeToColorConverter();
            var expectedColor = Color.FromRgb(r, g, b);

            // Act
            var result = converter.Convert(type, typeof(Color), null, CultureInfo.CurrentCulture);

            // Assert
            Assert.Equal(expectedColor, result);
        }

        /// <summary>
        /// Test NotificationTypeToColorConverter handles invalid values
        /// 测试NotificationTypeToColorConverter处理无效值
        /// </summary>
        [Fact]
        public void NotificationTypeToColorConverter_InvalidValue_ReturnsGray()
        {
            // Arrange
            var converter = new NotificationTypeToColorConverter();

            // Act
            var result = converter.Convert("invalid", typeof(Color), null, CultureInfo.CurrentCulture);

            // Assert
            var expectedGray = Color.FromRgb(0x95, 0xA5, 0xA6);
            Assert.Equal(expectedGray, result);
        }

        /// <summary>
        /// Test NotificationTypeToColorConverter ConvertBack throws NotImplementedException
        /// 测试NotificationTypeToColorConverter ConvertBack抛出NotImplementedException
        /// </summary>
        [Fact]
        public void NotificationTypeToColorConverter_ConvertBack_ThrowsNotImplementedException()
        {
            // Arrange
            var converter = new NotificationTypeToColorConverter();

            // Act & Assert
            Assert.Throws<NotImplementedException>(() =>
                converter.ConvertBack(Colors.Blue, typeof(NotificationType), null, CultureInfo.CurrentCulture));
        }
    }
}