using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.IO;
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
using File = System.IO.File;

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
            await viewModel.Update();
        }

        private async void Sync_Button_Click(object sender, RoutedEventArgs e)
        {
            SyncButton.IsEnabled = false;

            using var nsql = await NightSql.GetInstance(App.Configuration);
            await nsql.StartBatchImport();
            using(var sync = new NightSync(App.Configuration))
            {
                await Task.WhenAll(
                    sync.ImportBg(nsql),
                    sync.ImportBasalProfiles(nsql),
                    sync.ImportTempBasals(nsql),
                    sync.ImportBoluses(nsql),
                    sync.ImportCarbs(nsql)
                );
            }
            await nsql.FinalizeBatchImport();
            await viewModel.Update();
            SyncButton.IsEnabled = true;
        }
    }
}
