using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace NightFlux.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
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
            viewModel.Update();
        }

        private async void Sync_Button_Click(object sender, RoutedEventArgs e)
        {
            SyncButton.IsEnabled = false;
            using(var sync = new NightSync(App.Configuration))
            {
                await Task.WhenAll(
                        sync.ImportBg(),
                        sync.ImportBasalProfiles(),
                        sync.ImportTempBasals(),
                        sync.ImportBoluses(),
                        sync.ImportExtendedBoluses(),
                        sync.ImportCarbs()
                    );
            }

            var csvsync = new CsvImport(App.Configuration);
            await csvsync.ImportFile(@"C:\Users\kurtl\Desktop\boluses.tsv").ConfigureAwait(true);

            await viewModel.Update();
            SyncButton.IsEnabled = true;
        }
    }
}
