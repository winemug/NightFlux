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
        public PlotModel NightFluxPlotModel { get; set;}

        public MainWindow()
        {
            InitializeComponent();
        }

        private async Task<PlotModel> GetModel()
        {
            var nv = new NightView(App.Configuration);
            var dtStart = DateTimeOffset.Now.AddHours(-6);
            var dtEnd = DateTimeOffset.Now;

            var model = new PlotModel { Title = "Something" };
            model.Axes.Add(new DateTimeAxis {
                Position = AxisPosition.Bottom,
                Minimum = DateTimeAxis.ToDouble(dtStart.LocalDateTime),
                Maximum = DateTimeAxis.ToDouble(dtEnd.LocalDateTime) });
            model.Axes.Add(new LinearAxis { Position = AxisPosition.Left });

            var lineSeries = new LineSeries();
            await foreach(var gv in nv.GlucoseValues(dtStart, dtEnd))
            {
                lineSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(gv.Time.LocalDateTime), (double)gv.Value));
            }

            lineSeries.Title = "Nice";
            model.Series.Add(lineSeries);

            return model;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            NightFluxPlotModel = await GetModel();
            DataContext = this;
        }
    }
}
