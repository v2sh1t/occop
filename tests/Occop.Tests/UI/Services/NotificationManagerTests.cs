using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Occop.UI.Models;
using Occop.UI.Services;
using Occop.Services;

namespace Occop.Tests.UI.Services
{
    /// <summary>
    /// Tests for NotificationManager class
    /// NotificationManager类的测试
    /// </summary>
    public class NotificationManagerTests
    {
        private readonly Mock<ILogger<NotificationManager>> _loggerMock;
        private readonly Mock<ITrayManager> _trayManagerMock;

        public NotificationManagerTests()
        {
            _loggerMock = new Mock<ILogger<NotificationManager>>();
            _trayManagerMock = new Mock<ITrayManager>();
        }

        /// <summary>
        /// Test that NotificationManager can be created successfully
        /// 测试NotificationManager能够成功创建
        /// </summary>
        [Fact]
        public void Constructor_WithValidParameters_ShouldCreateInstance()
        {
            // Arrange & Act
            var manager = new NotificationManager(_loggerMock.Object, _trayManagerMock.Object);

            // Assert
            Assert.NotNull(manager);
            Assert.NotNull(manager.Notifications);
            Assert.Equal(0, manager.UnreadCount);
        }

        /// <summary>
        /// Test that ShowInfo creates an info notification
        /// 测试ShowInfo创建信息通知
        /// </summary>
        [Fact]
        public void ShowInfo_WithValidParameters_ShouldCreateInfoNotification()
        {
            // Arrange
            var manager = new NotificationManager(_loggerMock.Object, _trayManagerMock.Object);
            var title = "Test Title";
            var message = "Test Message";

            // Act
            var notificationId = manager.ShowInfo(title, message);

            // Assert
            Assert.NotEmpty(notificationId);
            Assert.Equal(1, manager.Notifications.Count);
            Assert.Equal(1, manager.UnreadCount);

            var notification = manager.Notifications[0];
            Assert.Equal(NotificationType.Info, notification.Type);
            Assert.Equal(title, notification.Title);
            Assert.Equal(message, notification.Message);
            Assert.False(notification.IsRead);
        }

        /// <summary>
        /// Test that ShowSuccess creates a success notification
        /// 测试ShowSuccess创建成功通知
        /// </summary>
        [Fact]
        public void ShowSuccess_WithValidParameters_ShouldCreateSuccessNotification()
        {
            // Arrange
            var manager = new NotificationManager(_loggerMock.Object, _trayManagerMock.Object);
            var title = "Success Title";
            var message = "Success Message";

            // Act
            var notificationId = manager.ShowSuccess(title, message);

            // Assert
            Assert.NotEmpty(notificationId);
            Assert.Equal(1, manager.Notifications.Count);

            var notification = manager.Notifications[0];
            Assert.Equal(NotificationType.Success, notification.Type);
            Assert.Equal(title, notification.Title);
            Assert.Equal(message, notification.Message);
        }

        /// <summary>
        /// Test that ShowWarning creates a warning notification
        /// 测试ShowWarning创建警告通知
        /// </summary>
        [Fact]
        public void ShowWarning_WithValidParameters_ShouldCreateWarningNotification()
        {
            // Arrange
            var manager = new NotificationManager(_loggerMock.Object, _trayManagerMock.Object);
            var title = "Warning Title";
            var message = "Warning Message";

            // Act
            var notificationId = manager.ShowWarning(title, message);

            // Assert
            Assert.NotEmpty(notificationId);
            Assert.Equal(1, manager.Notifications.Count);

            var notification = manager.Notifications[0];
            Assert.Equal(NotificationType.Warning, notification.Type);
            Assert.Equal(NotificationPriority.High, notification.Priority);
            Assert.Equal(title, notification.Title);
            Assert.Equal(message, notification.Message);
        }

        /// <summary>
        /// Test that ShowError creates an error notification
        /// 测试ShowError创建错误通知
        /// </summary>
        [Fact]
        public void ShowError_WithValidParameters_ShouldCreateErrorNotification()
        {
            // Arrange
            var manager = new NotificationManager(_loggerMock.Object, _trayManagerMock.Object);
            var title = "Error Title";
            var message = "Error Message";

            // Act
            var notificationId = manager.ShowError(title, message);

            // Assert
            Assert.NotEmpty(notificationId);
            Assert.Equal(1, manager.Notifications.Count);

            var notification = manager.Notifications[0];
            Assert.Equal(NotificationType.Error, notification.Type);
            Assert.Equal(NotificationPriority.Critical, notification.Priority);
            Assert.Equal(title, notification.Title);
            Assert.Equal(message, notification.Message);
        }

        /// <summary>
        /// Test that MarkAsRead updates notification read status
        /// 测试MarkAsRead更新通知已读状态
        /// </summary>
        [Fact]
        public void MarkAsRead_WithValidNotificationId_ShouldMarkNotificationAsRead()
        {
            // Arrange
            var manager = new NotificationManager(_loggerMock.Object, _trayManagerMock.Object);
            var notificationId = manager.ShowInfo("Test", "Message");

            // Act
            var result = manager.MarkAsRead(notificationId);

            // Assert
            Assert.True(result);
            Assert.Equal(0, manager.UnreadCount);

            var notification = manager.Notifications[0];
            Assert.True(notification.IsRead);
        }

        /// <summary>
        /// Test that RemoveNotification removes notification from collection
        /// 测试RemoveNotification从集合中移除通知
        /// </summary>
        [Fact]
        public void RemoveNotification_WithValidNotificationId_ShouldRemoveNotification()
        {
            // Arrange
            var manager = new NotificationManager(_loggerMock.Object, _trayManagerMock.Object);
            var notificationId = manager.ShowInfo("Test", "Message");

            // Act
            var result = manager.RemoveNotification(notificationId);

            // Assert
            Assert.True(result);
            Assert.Equal(0, manager.Notifications.Count);
            Assert.Equal(0, manager.UnreadCount);
        }

        /// <summary>
        /// Test that ClearAll removes all notifications
        /// 测试ClearAll移除所有通知
        /// </summary>
        [Fact]
        public void ClearAll_WithMultipleNotifications_ShouldRemoveAllNotifications()
        {
            // Arrange
            var manager = new NotificationManager(_loggerMock.Object, _trayManagerMock.Object);
            manager.ShowInfo("Test1", "Message1");
            manager.ShowWarning("Test2", "Message2");
            manager.ShowError("Test3", "Message3");

            // Act
            manager.ClearAll();

            // Assert
            Assert.Equal(0, manager.Notifications.Count);
            Assert.Equal(0, manager.UnreadCount);
        }

        /// <summary>
        /// Test that ClearRead only removes read notifications
        /// 测试ClearRead只移除已读通知
        /// </summary>
        [Fact]
        public void ClearRead_WithMixedReadStatus_ShouldOnlyRemoveReadNotifications()
        {
            // Arrange
            var manager = new NotificationManager(_loggerMock.Object, _trayManagerMock.Object);
            var id1 = manager.ShowInfo("Test1", "Message1");
            var id2 = manager.ShowInfo("Test2", "Message2");
            var id3 = manager.ShowInfo("Test3", "Message3");

            // Mark some as read
            manager.MarkAsRead(id1);
            manager.MarkAsRead(id3);

            // Act
            manager.ClearRead();

            // Assert
            Assert.Equal(1, manager.Notifications.Count);
            Assert.Equal(1, manager.UnreadCount);

            var remainingNotification = manager.Notifications[0];
            Assert.Equal(id2, remainingNotification.Id);
            Assert.False(remainingNotification.IsRead);
        }

        /// <summary>
        /// Test that ShowNotification with null parameter throws exception
        /// 测试ShowNotification使用null参数会抛出异常
        /// </summary>
        [Fact]
        public void ShowNotification_WithNullParameter_ShouldThrowArgumentNullException()
        {
            // Arrange
            var manager = new NotificationManager(_loggerMock.Object, _trayManagerMock.Object);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => manager.ShowNotification(null!));
        }

        /// <summary>
        /// Test that maximum notifications limit is enforced
        /// 测试强制执行最大通知数量限制
        /// </summary>
        [Fact]
        public void ShowNotification_WhenExceedingMaxLimit_ShouldRemoveOldestNotifications()
        {
            // Arrange
            var manager = new NotificationManager(_loggerMock.Object, _trayManagerMock.Object);

            // Act - Add more than the maximum allowed notifications
            for (int i = 0; i < NotificationManager.MaxNotifications + 5; i++)
            {
                manager.ShowInfo($"Title {i}", $"Message {i}");
            }

            // Assert
            Assert.Equal(NotificationManager.MaxNotifications, manager.Notifications.Count);

            // The latest notifications should be retained
            var latestNotification = manager.Notifications[0];
            Assert.Contains($"{NotificationManager.MaxNotifications + 4}", latestNotification.Title);
        }

        /// <summary>
        /// Test that high priority notifications show tray balloon tips
        /// 测试高优先级通知显示托盘气球提示
        /// </summary>
        [Fact]
        public void ShowWarning_WithTrayManager_ShouldShowBalloonTip()
        {
            // Arrange
            var manager = new NotificationManager(_loggerMock.Object, _trayManagerMock.Object);

            // Act
            manager.ShowWarning("Warning", "This is a warning");

            // Assert
            _trayManagerMock.Verify(
                x => x.ShowBalloonTip("Warning", "This is a warning", System.Windows.Forms.ToolTipIcon.Warning),
                Times.Once);
        }

        /// <summary>
        /// Test that error notifications show tray balloon tips
        /// 测试错误通知显示托盘气球提示
        /// </summary>
        [Fact]
        public void ShowError_WithTrayManager_ShouldShowBalloonTip()
        {
            // Arrange
            var manager = new NotificationManager(_loggerMock.Object, _trayManagerMock.Object);

            // Act
            manager.ShowError("Error", "This is an error");

            // Assert
            _trayManagerMock.Verify(
                x => x.ShowBalloonTip("Error", "This is an error", System.Windows.Forms.ToolTipIcon.Error),
                Times.Once);
        }
    }
}