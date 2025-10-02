using System;
using System.ComponentModel;
using System.Windows;
using Microsoft.Extensions.Logging;
using Occop.UI.ViewModels;

namespace Occop.UI
{
    /// <summary>
    /// SettingsWindow.xaml 的交互逻辑
    /// 设置窗口，用于应用配置管理
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly ILogger<SettingsWindow> _logger;
        private readonly SettingsViewModel _viewModel;

        public SettingsWindow(ILogger<SettingsWindow> logger, SettingsViewModel viewModel)
        {
            InitializeComponent();

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            // 设置DataContext
            DataContext = _viewModel;

            // 订阅ViewModel属性变化事件
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            _logger.LogInformation("SettingsWindow initialized");

            // 加载设置
            Loaded += async (s, e) => await _viewModel.LoadSettingsCommand.ExecuteAsync(null);
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // 可以在这里处理ViewModel属性变化事件
            if (e.PropertyName == nameof(SettingsViewModel.HasUnsavedChanges))
            {
                UpdateWindowTitle();
            }
        }

        private void UpdateWindowTitle()
        {
            Title = _viewModel.HasUnsavedChanges ? "设置 - Occop *" : "设置 - Occop";
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            try
            {
                // 检查是否有未保存的更改
                if (_viewModel.HasUnsavedChanges)
                {
                    var result = MessageBox.Show(
                        "有未保存的更改。确定要关闭窗口吗？",
                        "确认关闭",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result != MessageBoxResult.Yes)
                    {
                        e.Cancel = true;
                        return;
                    }
                }

                // 取消订阅事件
                if (_viewModel != null)
                {
                    _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                }

                _logger.LogInformation("SettingsWindow closing");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during SettingsWindow closing");
            }

            base.OnClosing(e);
        }

        /// <summary>
        /// 显示设置对话框
        /// </summary>
        /// <param name="owner">父窗口</param>
        /// <returns>是否保存了设置</returns>
        public bool ShowSettingsDialog(Window? owner = null)
        {
            try
            {
                Owner = owner;
                WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen;

                _logger.LogInformation("Showing settings dialog");

                return ShowDialog() == true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing settings dialog");
                return false;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // 保存成功后关闭窗口
            if (_viewModel.SaveSettingsCommand.CanExecute(null))
            {
                _viewModel.SaveSettingsCommand.ExecuteAsync(null).ContinueWith(task =>
                {
                    if (task.IsCompletedSuccessfully && !_viewModel.HasUnsavedChanges)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            DialogResult = true;
                            Close();
                        });
                    }
                });
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // 执行取消命令
            if (_viewModel.CancelCommand.CanExecute(null))
            {
                _viewModel.CancelCommand.Execute(null);
            }

            // 关闭窗口
            DialogResult = false;
            Close();
        }
    }
}
