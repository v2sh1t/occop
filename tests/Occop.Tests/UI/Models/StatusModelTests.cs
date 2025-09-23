using System;
using System.ComponentModel;
using Xunit;
using Occop.UI.Models;

namespace Occop.Tests.UI.Models
{
    /// <summary>
    /// Tests for StatusModel class
    /// StatusModel类的测试
    /// </summary>
    public class StatusModelTests
    {
        /// <summary>
        /// Test that StatusModel can be created with default constructor
        /// 测试StatusModel能够使用默认构造函数创建
        /// </summary>
        [Fact]
        public void Constructor_Default_ShouldCreateInstanceWithDefaultValues()
        {
            // Act
            var status = new StatusModel();

            // Assert
            Assert.Equal(StatusType.Application, status.Type);
            Assert.Equal(StatusState.Unknown, status.State);
            Assert.Equal(string.Empty, status.Title);
            Assert.Equal(string.Empty, status.Message);
            Assert.Equal(string.Empty, status.Details);
            Assert.True(DateTime.Now - status.Timestamp < TimeSpan.FromSeconds(1));
            Assert.Null(status.AdditionalData);
        }

        /// <summary>
        /// Test that StatusModel can be created with parameters
        /// 测试StatusModel能够使用参数创建
        /// </summary>
        [Fact]
        public void Constructor_WithParameters_ShouldCreateInstanceWithSpecifiedValues()
        {
            // Arrange
            var type = StatusType.Authentication;
            var state = StatusState.Ready;
            var title = "Test Title";
            var message = "Test Message";
            var details = "Test Details";

            // Act
            var status = new StatusModel(type, state, title, message, details);

            // Assert
            Assert.Equal(type, status.Type);
            Assert.Equal(state, status.State);
            Assert.Equal(title, status.Title);
            Assert.Equal(message, status.Message);
            Assert.Equal(details, status.Details);
            Assert.True(DateTime.Now - status.Timestamp < TimeSpan.FromSeconds(1));
        }

        /// <summary>
        /// Test that StatusModel handles null parameters gracefully
        /// 测试StatusModel优雅处理null参数
        /// </summary>
        [Fact]
        public void Constructor_WithNullParameters_ShouldHandleGracefully()
        {
            // Act
            var status = new StatusModel(StatusType.AITool, StatusState.Error, null!, null!, null!);

            // Assert
            Assert.Equal(StatusType.AITool, status.Type);
            Assert.Equal(StatusState.Error, status.State);
            Assert.Equal(string.Empty, status.Title);
            Assert.Equal(string.Empty, status.Message);
            Assert.Equal(string.Empty, status.Details);
        }

        /// <summary>
        /// Test that StatusModel implements INotifyPropertyChanged
        /// 测试StatusModel实现INotifyPropertyChanged
        /// </summary>
        [Fact]
        public void StatusModel_ShouldImplementINotifyPropertyChanged()
        {
            // Arrange
            var status = new StatusModel();

            // Assert
            Assert.IsAssignableFrom<INotifyPropertyChanged>(status);
        }

        /// <summary>
        /// Test that PropertyChanged event is raised when Type is changed
        /// 测试Type更改时触发PropertyChanged事件
        /// </summary>
        [Fact]
        public void Type_WhenChanged_ShouldRaisePropertyChangedEvent()
        {
            // Arrange
            var status = new StatusModel();
            var eventRaised = false;
            string? propertyName = null;

            status.PropertyChanged += (sender, args) =>
            {
                eventRaised = true;
                propertyName = args.PropertyName;
            };

            // Act
            status.Type = StatusType.SystemCleanup;

            // Assert
            Assert.True(eventRaised);
            Assert.Equal(nameof(StatusModel.Type), propertyName);
            Assert.Equal(StatusType.SystemCleanup, status.Type);
        }

        /// <summary>
        /// Test that PropertyChanged event is raised when State is changed
        /// 测试State更改时触发PropertyChanged事件
        /// </summary>
        [Fact]
        public void State_WhenChanged_ShouldRaisePropertyChangedEvent()
        {
            // Arrange
            var status = new StatusModel();
            var eventRaised = false;
            string? propertyName = null;

            status.PropertyChanged += (sender, args) =>
            {
                eventRaised = true;
                propertyName = args.PropertyName;
            };

            // Act
            status.State = StatusState.Working;

            // Assert
            Assert.True(eventRaised);
            Assert.Equal(nameof(StatusModel.State), propertyName);
            Assert.Equal(StatusState.Working, status.State);
        }

        /// <summary>
        /// Test that PropertyChanged event is raised when Title is changed
        /// 测试Title更改时触发PropertyChanged事件
        /// </summary>
        [Fact]
        public void Title_WhenChanged_ShouldRaisePropertyChangedEvent()
        {
            // Arrange
            var status = new StatusModel();
            var eventRaised = false;
            string? propertyName = null;

            status.PropertyChanged += (sender, args) =>
            {
                eventRaised = true;
                propertyName = args.PropertyName;
            };

            // Act
            status.Title = "New Title";

            // Assert
            Assert.True(eventRaised);
            Assert.Equal(nameof(StatusModel.Title), propertyName);
            Assert.Equal("New Title", status.Title);
        }

        /// <summary>
        /// Test that PropertyChanged event is raised when Message is changed
        /// 测试Message更改时触发PropertyChanged事件
        /// </summary>
        [Fact]
        public void Message_WhenChanged_ShouldRaisePropertyChangedEvent()
        {
            // Arrange
            var status = new StatusModel();
            var eventRaised = false;
            string? propertyName = null;

            status.PropertyChanged += (sender, args) =>
            {
                eventRaised = true;
                propertyName = args.PropertyName;
            };

            // Act
            status.Message = "New Message";

            // Assert
            Assert.True(eventRaised);
            Assert.Equal(nameof(StatusModel.Message), propertyName);
            Assert.Equal("New Message", status.Message);
        }

        /// <summary>
        /// Test that PropertyChanged event is raised when Details is changed
        /// 测试Details更改时触发PropertyChanged事件
        /// </summary>
        [Fact]
        public void Details_WhenChanged_ShouldRaisePropertyChangedEvent()
        {
            // Arrange
            var status = new StatusModel();
            var eventRaised = false;
            string? propertyName = null;

            status.PropertyChanged += (sender, args) =>
            {
                eventRaised = true;
                propertyName = args.PropertyName;
            };

            // Act
            status.Details = "New Details";

            // Assert
            Assert.True(eventRaised);
            Assert.Equal(nameof(StatusModel.Details), propertyName);
            Assert.Equal("New Details", status.Details);
        }

        /// <summary>
        /// Test that PropertyChanged event is raised when Timestamp is changed
        /// 测试Timestamp更改时触发PropertyChanged事件
        /// </summary>
        [Fact]
        public void Timestamp_WhenChanged_ShouldRaisePropertyChangedEvent()
        {
            // Arrange
            var status = new StatusModel();
            var eventRaised = false;
            string? propertyName = null;

            status.PropertyChanged += (sender, args) =>
            {
                eventRaised = true;
                propertyName = args.PropertyName;
            };

            var newTimestamp = DateTime.Now.AddHours(1);

            // Act
            status.Timestamp = newTimestamp;

            // Assert
            Assert.True(eventRaised);
            Assert.Equal(nameof(StatusModel.Timestamp), propertyName);
            Assert.Equal(newTimestamp, status.Timestamp);
        }

        /// <summary>
        /// Test that PropertyChanged event is raised when AdditionalData is changed
        /// 测试AdditionalData更改时触发PropertyChanged事件
        /// </summary>
        [Fact]
        public void AdditionalData_WhenChanged_ShouldRaisePropertyChangedEvent()
        {
            // Arrange
            var status = new StatusModel();
            var eventRaised = false;
            string? propertyName = null;

            status.PropertyChanged += (sender, args) =>
            {
                eventRaised = true;
                propertyName = args.PropertyName;
            };

            var additionalData = new { TestProperty = "TestValue" };

            // Act
            status.AdditionalData = additionalData;

            // Assert
            Assert.True(eventRaised);
            Assert.Equal(nameof(StatusModel.AdditionalData), propertyName);
            Assert.Equal(additionalData, status.AdditionalData);
        }

        /// <summary>
        /// Test that PropertyChanged event is not raised when setting the same value
        /// 测试设置相同值时不触发PropertyChanged事件
        /// </summary>
        [Fact]
        public void Property_WhenSetToSameValue_ShouldNotRaisePropertyChangedEvent()
        {
            // Arrange
            var status = new StatusModel { Title = "Test Title" };
            var eventRaised = false;

            status.PropertyChanged += (sender, args) =>
            {
                eventRaised = true;
            };

            // Act
            status.Title = "Test Title"; // Same value

            // Assert
            Assert.False(eventRaised);
        }
    }

    /// <summary>
    /// Tests for NotificationModel class
    /// NotificationModel类的测试
    /// </summary>
    public class NotificationModelTests
    {
        /// <summary>
        /// Test that NotificationModel can be created with default constructor
        /// 测试NotificationModel能够使用默认构造函数创建
        /// </summary>
        [Fact]
        public void Constructor_Default_ShouldCreateInstanceWithDefaultValues()
        {
            // Act
            var notification = new NotificationModel();

            // Assert
            Assert.NotEmpty(notification.Id);
            Assert.Equal(NotificationType.Info, notification.Type);
            Assert.Equal(NotificationPriority.Normal, notification.Priority);
            Assert.Equal(string.Empty, notification.Title);
            Assert.Equal(string.Empty, notification.Message);
            Assert.True(DateTime.Now - notification.Timestamp < TimeSpan.FromSeconds(1));
            Assert.Null(notification.Duration);
            Assert.False(notification.IsRead);
            Assert.False(notification.IsActionable);
            Assert.Null(notification.Action);
            Assert.Null(notification.Context);
        }

        /// <summary>
        /// Test that NotificationModel can be created with parameters
        /// 测试NotificationModel能够使用参数创建
        /// </summary>
        [Fact]
        public void Constructor_WithParameters_ShouldCreateInstanceWithSpecifiedValues()
        {
            // Arrange
            var type = NotificationType.Warning;
            var title = "Warning Title";
            var message = "Warning Message";
            var priority = NotificationPriority.High;
            var duration = TimeSpan.FromSeconds(10);

            // Act
            var notification = new NotificationModel(type, title, message, priority, duration);

            // Assert
            Assert.NotEmpty(notification.Id);
            Assert.Equal(type, notification.Type);
            Assert.Equal(title, notification.Title);
            Assert.Equal(message, notification.Message);
            Assert.Equal(priority, notification.Priority);
            Assert.Equal(duration, notification.Duration);
            Assert.False(notification.IsRead);
            Assert.False(notification.IsActionable);
        }

        /// <summary>
        /// Test that NotificationModel handles null parameters gracefully
        /// 测试NotificationModel优雅处理null参数
        /// </summary>
        [Fact]
        public void Constructor_WithNullParameters_ShouldHandleGracefully()
        {
            // Act
            var notification = new NotificationModel(NotificationType.Error, null!, null!);

            // Assert
            Assert.Equal(NotificationType.Error, notification.Type);
            Assert.Equal(string.Empty, notification.Title);
            Assert.Equal(string.Empty, notification.Message);
        }

        /// <summary>
        /// Test that NotificationModel implements INotifyPropertyChanged
        /// 测试NotificationModel实现INotifyPropertyChanged
        /// </summary>
        [Fact]
        public void NotificationModel_ShouldImplementINotifyPropertyChanged()
        {
            // Arrange
            var notification = new NotificationModel();

            // Assert
            Assert.IsAssignableFrom<INotifyPropertyChanged>(notification);
        }

        /// <summary>
        /// Test that each NotificationModel has a unique ID
        /// 测试每个NotificationModel都有唯一的ID
        /// </summary>
        [Fact]
        public void Id_ShouldBeUniqueForEachInstance()
        {
            // Act
            var notification1 = new NotificationModel();
            var notification2 = new NotificationModel();

            // Assert
            Assert.NotEqual(notification1.Id, notification2.Id);
        }

        /// <summary>
        /// Test that PropertyChanged event is raised when IsRead is changed
        /// 测试IsRead更改时触发PropertyChanged事件
        /// </summary>
        [Fact]
        public void IsRead_WhenChanged_ShouldRaisePropertyChangedEvent()
        {
            // Arrange
            var notification = new NotificationModel();
            var eventRaised = false;
            string? propertyName = null;

            notification.PropertyChanged += (sender, args) =>
            {
                eventRaised = true;
                propertyName = args.PropertyName;
            };

            // Act
            notification.IsRead = true;

            // Assert
            Assert.True(eventRaised);
            Assert.Equal(nameof(NotificationModel.IsRead), propertyName);
            Assert.True(notification.IsRead);
        }

        /// <summary>
        /// Test that PropertyChanged event is raised when IsActionable is changed
        /// 测试IsActionable更改时触发PropertyChanged事件
        /// </summary>
        [Fact]
        public void IsActionable_WhenChanged_ShouldRaisePropertyChangedEvent()
        {
            // Arrange
            var notification = new NotificationModel();
            var eventRaised = false;
            string? propertyName = null;

            notification.PropertyChanged += (sender, args) =>
            {
                eventRaised = true;
                propertyName = args.PropertyName;
            };

            // Act
            notification.IsActionable = true;

            // Assert
            Assert.True(eventRaised);
            Assert.Equal(nameof(NotificationModel.IsActionable), propertyName);
            Assert.True(notification.IsActionable);
        }
    }
}