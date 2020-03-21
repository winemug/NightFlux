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
        public double SimulationShift { get; set; } = 0;

        private LineSeries BgSeriesDiff;
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
                Minimum = -40,
                Maximum = 40,
                Key = "dbg"
            });
            
            BgSeriesDiff = new LineSeries() {YAxisKey = "dbg"};
            Model1.Series.Add(BgSeriesDiff);

            Model1.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Right,
                Minimum = 0,
                Maximum = 50,
                Key = "ia"
            });

            Simulation1Series = new LineSeries() {YAxisKey = "ia"};
            Model1.Series.Add(Simulation1Series);
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
            foreach (var gv in await nsql.BgValues(Start, End))
            {
                if (last != 0d)
                    BgSeriesDiff.Points.Add(new DataPoint(DateTimeAxis.ToDouble(gv.Time.LocalDateTime), (last - gv.Value)*GvFactor));
            
                last = gv.Value;
            }
            
            var podSessions = await nsql.PodSessions(Start, End);
            var ps = podSessions.Last(ps => ps.Hormone == HormoneType.InsulinAspart);

            Simulation1Series.Points.Clear();
            foreach (var iv in InsulinModel.Run(ps.InfusionRates))
            {
                // simulationSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(iv.From.LocalDateTime),
                //     iv.Value));
                Simulation1Series.Points.Add(new DataPoint(DateTimeAxis.ToDouble(iv.To.AddMinutes(SimulationShift).LocalDateTime),
                    Axis.ToDouble(iv.Value)));
            }
        }
    }
}
