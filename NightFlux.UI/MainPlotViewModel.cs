using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using NightFlux.Data;
using NightFlux.Model;
using NightFlux.Simulations;
using Nito.AsyncEx;

namespace NightFlux.UI
{
    public class MainPlotViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        
        public InsulinModel1 InsulinModel { get; set; }

        public PlotModel Model1 {get; set;}
        public PlotModel Model2 {get; set;}

        public DateTimeOffset Start { get; set; }
        public DateTimeOffset End { get; set; }

        public double GvFactor { get; set; } = -1.0;
        public MainPlotViewModel()
        {
            Start = DateTimeOffset.UtcNow.AddDays(-14);
            End = DateTimeOffset.UtcNow;
            InsulinModel = new InsulinModel1();
            InsulinModel.PropertyChanged += (sender, args) => DelayedUpdate();
        }
        private Task UpdateTask;

        private volatile int UpdateRequestedLast; 
        private void DelayedUpdate()
        {
            UpdateRequestedLast = Environment.TickCount;
            if (UpdateTask == null || UpdateTask.IsCompleted)
            {
                UpdateTask = Task.Run(async () =>
                {
                    while (Environment.TickCount - UpdateRequestedLast < 700)
                    {
                        await Task.Delay(200);
                    }
                    await Update();
                    UpdateTask = null;
                });
            }
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
                Minimum = DateTimeAxis.ToDouble(End.LocalDateTime.AddDays(-3)),
                Maximum = DateTimeAxis.ToDouble(End.LocalDateTime) });

            // model.Axes.Add(new LinearAxis { Position = AxisPosition.Left,
            //     Minimum = -40,
            //     Maximum = 40});
            //
            // var bgSeriesDiff = new LineSeries { Title = "BG Diff" };
            //
            // var last = 0d;
            // foreach (var gv in await nsql.BgValues(Start, End))
            // {
            //     if (last != 0d)
            //         bgSeriesDiff.Points.Add(new DataPoint(DateTimeAxis.ToDouble(gv.Time.LocalDateTime), (last - gv.Value)*GvFactor));
            //
            //     last = gv.Value;
            // }
            //
            // model.Series.Add(bgSeriesDiff);
            
            model.Axes.Add(new LinearAxis { Position = AxisPosition.Left,
                Minimum = 20,
                Maximum = 440});
            
            var bgSeries = new LineSeries { Title = "BG" };

            foreach (var gv in await nsql.BgValues(Start, End))
            {
                bgSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(gv.Time.LocalDateTime), gv.Value));
            }

            model.Series.Add(bgSeries);

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Right,
                Minimum = 0,
                Maximum = 50,
                Key = "ia"
            });

            foreach (var ps in await nsql.PodSessions(Start, End))
            {

                // var infusionSeries = new AreaSeries { Title = $"{ps.Name}" };
                // double lastVal = 0;
                // foreach (var rate in ps.InfusionRates)
                // {
                //     infusionSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(rate.Key.LocalDateTime), lastVal));
                //     infusionSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(rate.Key.LocalDateTime), (double)rate.Value));
                //     lastVal = (double)rate.Value;
                // }
                // infusionSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(End.LocalDateTime), lastVal));
                // model.Series.Add(infusionSeries);

                if (ps.Hormone == HormoneType.InsulinAspart)
                {
                    var simulationSeries = new LineSeries {Title = $"Simulation {ps.Name}", YAxisKey = "ia"};

                    foreach (var iv in InsulinModel.Run(ps.InfusionRates))
                    {
                        // simulationSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(iv.From.LocalDateTime),
                        //     iv.Value));
                        simulationSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(iv.To.LocalDateTime),
                            Axis.ToDouble(iv.Value)));
                    }

                    model.Series.Add(simulationSeries);
                }
            }
           
            return model;
        }
    }
}
