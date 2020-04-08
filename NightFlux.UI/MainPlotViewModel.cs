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
using NightFlux.Imports;
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

        public double WindowMinutes { get; set; } = 1;
        
        public MainPlotViewModel()
        {
            Start = DateTimeOffset.UtcNow.AddDays(-28);
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
                Title = "BG (mg/dL)",
                Minimum = 20,
                Maximum = 400,
                Key = "bg"
            });
            
            Model1.Axes.Add(new LinearAxis
            {
                Title = "Insulin (U)",
                Position = AxisPosition.Right,
                Minimum = 0,
                Maximum = 250,
                Key = "insulin"
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
            using var nsReader = new NightscoutReader(App.Configuration);
            var tsvReader = new TsvReader(App.Configuration);

            var s = Model1.Series.FirstOrDefault() as LineSeries;
            if (s == null)
            {
                s = new LineSeries() {
                    YAxisKey = "bg",
                    Title="BG",
                    Color = OxyColor.FromRgb(255, 0, 0)
                };
                Model1.Series.Add(s);
            }

            s.Points.Clear();
            //var last = 0d;
            await foreach (var gv in nsReader.BgValues(Start, End))
            {
                s.Points.Add(new DataPoint(DateTimeAxis.ToDouble(gv.Time.LocalDateTime), 440d - gv.Value));
                //if (last != 0d)
                //    BgSeriesDiff.Points.Add(new DataPoint(DateTimeAxis.ToDouble(gv.Time.LocalDateTime), (last - gv.Value)*GvFactor));
                //last = gv.Value;
            }

            while (Model1.Series.Count > 1)
                Model1.Series.RemoveAt(1);
            
            await foreach (var ps in tsvReader.PodSessions(Start, End))
            {
                if (ps.Hormone == HormoneType.Glucagon)
                {
                    var simulationSerie = new LineSeries()
                    {
                        YAxisKey = "insulin",
                        Color = OxyColor.FromRgb(35,215,255)
                    };
                    foreach (var iv in InsulinModel.Run(ps,TimeSpan.FromHours(12) ))
                    {
                        simulationSerie.Points.Add(new DataPoint(
                            DateTimeAxis.ToDouble(iv.To.AddMinutes(SimulationShift).LocalDateTime),
                            Axis.ToDouble(iv.Value)));
                    }
                    Model1.Series.Add(simulationSerie);
                    
                    var deliverySeries = new LineSeries()
                    {
                        YAxisKey = "insulin",
                        Color = OxyColor.FromRgb(0,128,255),
                        LineStyle = LineStyle.Dash
                    };

                    foreach (var fv in ps.GetDeliveries())
                    {
                        //Debug.WriteLine($"{fv.From}\t{fv.To}\t{fv.Value}");
                        deliverySeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(fv.Time.LocalDateTime),
                            Axis.ToDouble(fv.Delivered)));
                        
                    }
                    Model1.Series.Add(deliverySeries);

                }
            }
        }
    }
}
