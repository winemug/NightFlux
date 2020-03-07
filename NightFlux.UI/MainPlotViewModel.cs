using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Converters;
using MathNet.Numerics;
using NightFlux.Experiments;
using NightFlux.View;

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
            Start = DateTimeOffset.UtcNow.AddDays(-14);
            End = DateTimeOffset.UtcNow;
        }

        public async Task Update()
        {
            var nv = new NightView(App.Configuration);

            BgcModel = await GetBgcModel(nv);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BgcModel)));
           
        }

        private async Task<PlotModel> GetBgcModel(NightView nv)
        {
            var model = new PlotModel();
            model.Axes.Add(new DateTimeAxis {
                Position = AxisPosition.Bottom,
                Minimum = DateTimeAxis.ToDouble(Start.LocalDateTime),
                Maximum = DateTimeAxis.ToDouble(End.LocalDateTime) });

            model.Axes.Add(new LinearAxis { Position = AxisPosition.Left,
                Minimum = 0,
                Maximum = 400});

            var bgSeries = new LineSeries { Title = "Blood Glucose Concentration" };

            await foreach(var tv in nv.BasalRates(Start, End))
            {
                bgSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(tv.Key.LocalDateTime), (double)tv.Value));
            }

            model.Series.Add(bgSeries);

            //model.Axes.Add(new LinearAxis { Position = AxisPosition.Left,
            //    Minimum = 0,
            //    Maximum = 10});

            var insulinSeries = new AreaSeries {Title = "Insulin"};

            double lastVal = 0;
            //var ios = Calculations.InsulinOnSite(Start, End);
            //foreach(var tv in ios.Observations)
            //{
            //    insulinSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(tv.Key.LocalDateTime), lastVal));
            //    insulinSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(tv.Key.LocalDateTime), tv.Value));
            //    lastVal = tv.Value;
            //}
            //insulinSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(End.LocalDateTime), lastVal));

            model.Series.Add(insulinSeries);

            


            return model;
        }
    }
}
