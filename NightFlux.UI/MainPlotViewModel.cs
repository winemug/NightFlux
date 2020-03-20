using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using NightFlux.Data;

namespace NightFlux.UI
{
    public class MainPlotViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public PlotModel Model1 {get; set;}
        public PlotModel Model2 {get; set;}

        public DateTimeOffset Start { get; set; }
        public DateTimeOffset End { get; set; }

        public MainPlotViewModel()
        {
            Start = DateTimeOffset.UtcNow.AddDays(-2);
            End = DateTimeOffset.UtcNow;
        }

        public async Task Update()
        {
            Model1 = await GetModel1();
        }

        private async Task<PlotModel> GetModel1()
        {
            var nsql = await NightSql.GetInstance(App.Configuration);

            var model = new PlotModel();
            model.Axes.Add(new DateTimeAxis {
                Position = AxisPosition.Bottom,
                Minimum = DateTimeAxis.ToDouble(End.LocalDateTime.AddHours(-6)),
                Maximum = DateTimeAxis.ToDouble(End.LocalDateTime) });

            model.Axes.Add(new LinearAxis { Position = AxisPosition.Left,
                Minimum = 20,
                Maximum = 440});

            var bgSeries = new LineSeries { Title = "Blood Glucose Concentration" };

            foreach (var gv in await nsql.BgValues(Start, End))
                bgSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(gv.Time.LocalDateTime), gv.Value));

            model.Series.Add(bgSeries);


            foreach (var ps in await nsql.PodSessions(Start, End))
            {
                model.Axes.Add(new LinearAxis
                {
                    Position = AxisPosition.Left,
                    Minimum = 0,
                    Maximum = 30
                });
                var infusionSeries = new AreaSeries { Title = $"{ps.Name}" };
                double lastVal = 0;
                foreach (var rate in ps.InfusionRates)
                {
                    infusionSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(rate.Key.LocalDateTime), lastVal));
                    infusionSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(rate.Key.LocalDateTime), (double)rate.Value));
                    lastVal = (double)rate.Value;
                }
                infusionSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(End.LocalDateTime), lastVal));
                model.Series.Add(infusionSeries);
            }

            return model;
        }
    }
}
