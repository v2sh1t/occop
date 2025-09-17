using System.Windows.Controls;
using Occop.UI.ViewModels;

namespace Occop.UI.Views
{
    /// <summary>
    /// Interaction logic for AuthenticationView.xaml
    /// AuthenticationView.xaml的交互逻辑
    /// </summary>
    public partial class AuthenticationView : UserControl
    {
        public AuthenticationView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Constructor with ViewModel injection
        /// 带有ViewModel注入的构造函数
        /// </summary>
        /// <param name="viewModel">The authentication ViewModel</param>
        public AuthenticationView(AuthenticationViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        /// <summary>
        /// Gets or sets the authentication ViewModel
        /// 获取或设置认证ViewModel
        /// </summary>
        public AuthenticationViewModel? ViewModel
        {
            get => DataContext as AuthenticationViewModel;
            set => DataContext = value;
        }

        /// <summary>
        /// Handle when the control is loaded
        /// 处理控件加载事件
        /// </summary>
        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // Focus on the view when loaded for better accessibility
            // 加载时聚焦到视图以提供更好的可访问性
            Focus();
        }

        /// <summary>
        /// Handle when the control is unloaded
        /// 处理控件卸载事件
        /// </summary>
        private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // Clean up ViewModel when unloading
            // 卸载时清理ViewModel
            if (ViewModel is IDisposable disposableViewModel)
            {
                disposableViewModel.Dispose();
            }
        }
    }
}