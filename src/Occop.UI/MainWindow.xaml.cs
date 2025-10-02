using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Occop.UI.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace Occop.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// MainWindow.xaml的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ILogger<MainWindow> _logger;
        private readonly MainViewModel _viewModel;

        public MainWindow(ILogger<MainWindow> logger, MainViewModel viewModel)
        {
            InitializeComponent();

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            // Set the DataContext
            DataContext = _viewModel;

            _logger.LogInformation("MainWindow initialized");
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            try
            {
                _logger.LogInformation("MainWindow closing");

                // Dispose of ViewModel
                _viewModel?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during MainWindow closing");
            }

            base.OnClosing(e);
        }
    }
}