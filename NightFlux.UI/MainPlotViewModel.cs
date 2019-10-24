using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;

namespace NightFlux.UI
{
    public class MainPlotViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public PlotModel MainPlotModel {get; set;}

        public MainPlotViewModel()
        {
        }

        public async Task Update()
        {
            MainPlotModel = await GetModel();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MainPlotModel)));
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
    }
}
