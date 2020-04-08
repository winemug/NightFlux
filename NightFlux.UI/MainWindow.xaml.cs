using System.Windows;
using NightFlux.Data;
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
            // var nfc = NightFluxConnection.GetInstance(App.Configuration);
            // await nfc.RunSync();
        }

        private async void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            await viewModel.Update();
        }
    }
}
