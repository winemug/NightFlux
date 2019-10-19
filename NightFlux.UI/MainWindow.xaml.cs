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
            NightFluxPlotModel = CreateModel();
            DataContext = this;
        }

        private PlotModel CreateModel()
        {
            var model = new PlotModel { Title = "Something" };
            model.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom });
            model.Axes.Add(new LinearAxis { Position = AxisPosition.Left });

            var lineSeries = new LineSeries();
            var r = new Random();
            for (int x=0; x<50; x++)
            {
                lineSeries.Points.Add(new DataPoint(x, r.Next(-100, 100)));
            }
            lineSeries.Title = "Nice";
            model.Series.Add(lineSeries);

            return model;
        }
    }
}
