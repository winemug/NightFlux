using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.ComponentModel;
using System.Linq;
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
        public double SimulationShift { get; set; } = 3;

        private LineSeries BgSeriesDiff;
        private LineSeries BgSeries;
        private LineSeries Simulation1Series;
        private LineSeries InfusionSeries;
        
        public MainPlotViewModel()
        {
            Start = DateTimeOffset.UtcNow.AddDays(-14);
            End = DateTimeOffset.UtcNow;
            InsulinModel = new InsulinModel1();
            InsulinModel.PropertyChanged += (sender, args) => DelayedUpdate();
            this.PropertyChanged += (sender, args) => DelayedUpdate();
            
            Model1 = new PlotModel();
            Model1.Axes.Add(new DateTimeAxis {
                Position = AxisPosition.Bottom,
                Minimum = DateTimeAxis.ToDouble(End.LocalDateTime.AddDays(-1)),
                Maximum = DateTimeAxis.ToDouble(End.LocalDateTime) });

            Model1.Axes.Add(new LinearAxis { Position = AxisPosition.Left,
                Minimum = 20,
                Maximum = 440,
                Key = "bg"
            });
            
            Model1.Axes.Add(new LinearAxis { Position = AxisPosition.Left,
                Minimum = -40,
                Maximum = 40,
                Key = "dbg"
            });
            Model1.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Right,
                Minimum = 0,
                Maximum = 50,
                Key = "ia"
            });

            BgSeries = new LineSeries() {YAxisKey = "bg"};
            Model1.Series.Add(BgSeries);
            
            BgSeriesDiff = new LineSeries() {YAxisKey = "dbg"};
            //Model1.Series.Add(BgSeriesDiff);

            Simulation1Series = new LineSeries() {YAxisKey = "ia"};
            Model1.Series.Add(Simulation1Series);
            
            InfusionSeries = new LineSeries()  {YAxisKey = "ia"};
            Model1.Series.Add(InfusionSeries);
            
            DelayedUpdate();
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
                    while (Environment.TickCount - UpdateRequestedLast < 500)
                    {
                        await Task.Delay(100);
                    }
                    await Update();
                    UpdateTask = null;
                });
            }
        }
        public async Task Update()
        {
            await SetModelData();
            Model1.InvalidatePlot(true);
        }

        private async Task SetModelData()
        {
            var nsql = await NightSql.GetInstance(App.Configuration);
            var last = 0d;
            BgSeriesDiff.Points.Clear();
            BgSeries.Points.Clear();
            foreach (var gv in await nsql.BgValues(Start, End))
            {
                BgSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(gv.Time.DateTime), gv.Value));
                if (last != 0d)
                    BgSeriesDiff.Points.Add(new DataPoint(DateTimeAxis.ToDouble(gv.Time.DateTime), (last - gv.Value)*GvFactor));
            
                last = gv.Value;
            }
            
            var podSessions = await nsql.PodSessions(Start, End);
            var ps = podSessions.Last(ps => ps.Hormone == HormoneType.InsulinAspart);

            Simulation1Series.Points.Clear();
            foreach (var iv in InsulinModel.Run(ps))
            {
                // simulationSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(iv.From.LocalDateTime),
                //     iv.Value));
                Simulation1Series.Points.Add(new DataPoint(DateTimeAxis.ToDouble(iv.To.AddMinutes(SimulationShift).DateTime),
                    Axis.ToDouble(iv.Value)));
            }
            
            InfusionSeries.Points.Clear();
            foreach (var fv in ps.Frames(TimeSpan.FromMinutes(1)))
            {
                // simulationSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(iv.From.LocalDateTime),
                //     iv.Value));
                InfusionSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(fv.From.DateTime),
                    Axis.ToDouble(fv.Value)));
                InfusionSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(fv.To.DateTime),
                    Axis.ToDouble(fv.Value)));
            }
        }
    }
}
