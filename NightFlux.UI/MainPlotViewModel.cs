using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
            
            Model1.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Right,
                Minimum = 0,
                Maximum = 120,
                Key = "ia"
            });

           
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

            var s = Model1.Series.FirstOrDefault() as LineSeries;
            if (s == null)
            {
                s = new LineSeries() {YAxisKey = "bg", Title="BG"};
                Model1.Series.Add(s);
            }

            s.Points.Clear();
            //var last = 0d;
            foreach (var gv in await nsql.BgValues(Start, End))
            {
                s.Points.Add(new DataPoint(DateTimeAxis.ToDouble(gv.Time.LocalDateTime), gv.Value));
                //if (last != 0d)
                //    BgSeriesDiff.Points.Add(new DataPoint(DateTimeAxis.ToDouble(gv.Time.LocalDateTime), (last - gv.Value)*GvFactor));
                //last = gv.Value;
            }

            while (Model1.Series.Count > 1)
                Model1.Series.RemoveAt(1);
            
            var podSessions = await nsql.PodSessions(Start, End);
            foreach (var ps in podSessions)
            {
                if (ps.Hormone == HormoneType.InsulinAspart)
                {
                    var simulationSerie = new LineSeries() { YAxisKey = "ia" };
                    foreach (var iv in InsulinModel.Run(ps, TimeSpan.FromMinutes(1), TimeSpan.FromHours(6) ))
                    {
                        simulationSerie.Points.Add(new DataPoint(
                            DateTimeAxis.ToDouble(iv.To.AddMinutes(SimulationShift).LocalDateTime),
                            Axis.ToDouble(iv.Value)));
                    }
                    Model1.Series.Add(simulationSerie);
                    
                    var infusionSerie = new LineSeries() { YAxisKey = "ia" };
                    foreach (var fv in ps.Frames(TimeSpan.FromMinutes(1), TimeSpan.FromHours(6)))
                    {
                        //Debug.WriteLine($"{fv.From}\t{fv.To}\t{fv.Value}");
                        infusionSerie.Points.Add(new DataPoint(DateTimeAxis.ToDouble(fv.From.LocalDateTime),
                            Axis.ToDouble(fv.Value)));
                        infusionSerie.Points.Add(new DataPoint(DateTimeAxis.ToDouble(fv.To.LocalDateTime),
                            Axis.ToDouble(fv.Value)));
                    }
                    
                    Model1.Series.Add(infusionSerie);
                }
            }
        }
    }
}
