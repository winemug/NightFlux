using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace NightFlux.Simulations
{
    public class InsulinModel1 : INotifyPropertyChanged
    {
        // based on Model 10 in "Insulin Kinetics in Type-1 Diabetes: Continuous andBolus Delivery of Rapid Acting Insulin"
        // with modifications accounting for:
        // 
        // absorption via lymphatic capillaries 
        // disassociation back into the monomeric compartment, instead of compartmental duplication
        // variable local degradation according to structural differences
     

        public double FastCompartment { get; private set; }
        public double SlowCompartment { get; private set; }
        public double Circulation { get; private set; }

        // TODO:
        // ideas:
        // Moving average of slow compartment size over time -> degradation of disassociation rate
        // Local degradation slowdown but increased build-up over time: ethos e.g. fast boluses
        // Blood capillary degradation - constant
        // blocked capillary pathways -> introduce delays before new pathway creation and rate increase afterwards


        public double Factorization { get; set; } = 50.00;
        public double MonomericAndDimericFormsRatio { get; set; } = 0.25; // ml / mL 
        public double HexamerDisassociationRate { get; set; } = 0.08; // mU / min
        
        public double BloodCapillaryAbsorptionRate { get; set; } = 0.03; // mU / min
        public double LymphaticCapillaryAbsorptionRate { get; set; } = 0.006; // mU / min
        public double EliminationRate { get; set; } = 0.0368; // mU / min

        public double LocalDegradationSaturationMonomers { get; set; } = 0.9; // mU / min
        public double LocalDegradationMidPointMonomers { get; set; } = 0.7; // mU

        public double LocalDegradationSaturationHexamers { get; set; } = 0.9; // mU / min
        public double LocalDegradationMidPointHexamers { get; set; } = 0.7; // mU

        public TimeSpan MinSimulationSpan = TimeSpan.FromMinutes(1);

        public InsulinModel1()
        {
        }
        
        public IEnumerable<(DateTimeOffset From, DateTimeOffset To, double Value)> Run(IDictionary<DateTimeOffset, decimal> rates)
        {
            FastCompartment = 0;
            SlowCompartment = 0;
            Circulation = 0;

            rates[DateTimeOffset.Now.AddHours(12)] = 0m;
            using var ratesEnum= rates.GetEnumerator();
            ratesEnum.MoveNext();
            var date = ratesEnum.Current.Key;
            var rate = ratesEnum.Current.Value;
            if (ratesEnum.MoveNext())
            {
                while (true)
                {
                    if (date + MinSimulationSpan < ratesEnum.Current.Key)
                    {
                        yield return ExecuteFrame(date,date + MinSimulationSpan, (double)rate);
                        date = date + MinSimulationSpan;
                    }
                    else
                    {
                        if (date + MinSimulationSpan > ratesEnum.Current.Key)
                        {
                            yield return ExecuteFrame(date, ratesEnum.Current.Key, (double)rate);
                        }
                        date = ratesEnum.Current.Key;
                        rate = ratesEnum.Current.Value;
                        if (!ratesEnum.MoveNext())
                            break;
                    }
                }
            }
        }
        

        private (DateTimeOffset From, DateTimeOffset To, double Value) ExecuteFrame
            (DateTimeOffset from, DateTimeOffset to, double hourlyRate)
        {
            var duration = to - from;

            var deposit = hourlyRate * duration.TotalHours;

            SlowCompartment += deposit * (1 - MonomericAndDimericFormsRatio);
            if (SlowCompartment > 0)
            {
                var lymphaticTransfer = SlowCompartment * LymphaticCapillaryAbsorptionRate * duration.TotalMinutes;
                var disassociation = SlowCompartment * HexamerDisassociationRate * duration.TotalMinutes; 
                var localDegradation =
                    LocalDegradationSaturationHexamers * duration.TotalMinutes * SlowCompartment
                    / ( LocalDegradationMidPointHexamers + SlowCompartment );

                var reduction = lymphaticTransfer + disassociation + localDegradation;
                if (reduction > SlowCompartment)
                {
                    var rationing = SlowCompartment / reduction;
                    lymphaticTransfer *= rationing;
                    disassociation *= rationing;
                    localDegradation *= rationing;
                }

                SlowCompartment -= lymphaticTransfer;
                SlowCompartment -= disassociation;
                SlowCompartment -= localDegradation;

                FastCompartment += disassociation;
                Circulation += lymphaticTransfer;
            }

            FastCompartment += deposit * MonomericAndDimericFormsRatio;
            if (FastCompartment > 0)
            {
                var capillaryTransfer = FastCompartment * BloodCapillaryAbsorptionRate * duration.TotalMinutes;
                var localDegradation =
                    LocalDegradationSaturationMonomers * duration.TotalMinutes * FastCompartment
                    / ( LocalDegradationMidPointMonomers + FastCompartment );

                var reduction = capillaryTransfer + localDegradation;
                if (reduction > FastCompartment)
                {
                    var rationing = FastCompartment / reduction;
                    capillaryTransfer *= rationing;
                    localDegradation *= rationing;
                }

                FastCompartment -= capillaryTransfer;
                FastCompartment -= localDegradation;

                Circulation += capillaryTransfer;
            }

            Circulation -= EliminationRate * duration.TotalHours;
            if (Circulation < 0)
                Circulation = 0;

            return (from, to, Circulation * Factorization);
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
