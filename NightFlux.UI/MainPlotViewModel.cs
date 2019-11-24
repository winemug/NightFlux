using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace NightFlux.UI
{
    public class MainPlotViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public PlotModel BgcModel {get; set;}
        public PlotModel InsulinModel {get; set;}

        public DateTimeOffset Start { get; set; }
        public DateTimeOffset End { get; set; }

        public MainPlotViewModel()
        {
            Start = DateTimeOffset.UtcNow.AddDays(-3);
            End = DateTimeOffset.UtcNow;
        }

        public async Task Update()
        {
            var nv = new NightView(App.Configuration);

            BgcModel = await GetBgcModel(nv);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BgcModel)));
            
            InsulinModel = await GetInsulinModel(nv);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InsulinModel)));
        }

        private async Task<PlotModel> GetBgcModel(NightView nv)
        {
            var model = new PlotModel();
            model.Axes.Add(new DateTimeAxis {
                Position = AxisPosition.Bottom,
                Minimum = DateTimeAxis.ToDouble(Start.LocalDateTime),
                Maximum = DateTimeAxis.ToDouble(End.LocalDateTime) });

            model.Axes.Add(new LinearAxis { Position = AxisPosition.Left,
                Minimum = 20,
                Maximum = 400});

            var bgSeries = new LineSeries { Title = "Blood Glucose Concentration" };

            await foreach(var tv in nv.GlucoseValues(Start, End))
            {
                bgSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(tv.Time.LocalDateTime), (double)tv.Value));
            }

            model.Series.Add(bgSeries);
            return model;
        }

        private async Task<PlotModel> GetInsulinModel(NightView nv)
        {
            var model = new PlotModel();
            model.Axes.Add(new DateTimeAxis {
                Position = AxisPosition.Bottom,
                Minimum = DateTimeAxis.ToDouble(Start.LocalDateTime),
                Maximum = DateTimeAxis.ToDouble(End.LocalDateTime) });

            model.Axes.Add(new LinearAxis { Position = AxisPosition.Left,
                Minimum = 0,
                Maximum = 10});

            var basalSeries = new AreaSeries {Title = "Basal rate", InterpolationAlgorithm = new PreviousValueInterpolationAlgorithm() };

            await foreach(var tv in nv.BasalRates(Start, End))
            {
                basalSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(tv.Time.LocalDateTime), (double)tv.Value));
            }

            model.Series.Add(basalSeries);
            return model;
        }
    }
}
