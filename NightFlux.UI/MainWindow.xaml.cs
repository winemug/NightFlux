using System.Windows;
using NightFlux.Imports;

namespace NightFlux.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private MainPlotViewModel viewModel;

        public MainWindow()
        {
            InitializeComponent();
            viewModel = new MainPlotViewModel();
            this.DataContext = viewModel;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await NightSync.Run(App.Configuration);
            await viewModel.Update();
        }
    }
}
