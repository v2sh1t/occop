using Microsoft.Extensions.DependencyInjection;
using Occop.UI.ViewModels;
using System.Windows;

namespace Occop.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// MainWindow.xaml的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly IServiceProvider _serviceProvider;

        public MainWindow(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            InitializeComponent();
            InitializeViewModel();
        }

        private void InitializeViewModel()
        {
            try
            {
                // Get the AuthenticationViewModel from DI container
                var authenticationViewModel = _serviceProvider.GetRequiredService<AuthenticationViewModel>();

                // Set the DataContext for the AuthenticationView
                AuthenticationView.DataContext = authenticationViewModel;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"初始化视图模型时发生错误：{ex.Message}",
                    "初始化错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // Clean up ViewModel when window closes
            if (AuthenticationView.DataContext is IDisposable disposableViewModel)
            {
                disposableViewModel.Dispose();
            }

            base.OnClosed(e);
        }
    }
}