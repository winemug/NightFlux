using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using NightFlux.Model;

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
        public double DisassociationCompartment { get; private set; }
        public double Circulation { get; private set; }

        // TODO:
        // ideas:
        // Moving average of slow compartment size over time -> degradation of disassociation rate
        // Local degradation slowdown but increased build-up over time: ethos e.g. fast boluses
        // Blood capillary degradation - constant
        // blocked capillary pathways -> introduce delays before new pathway creation and rate increase afterwards


        public double Factorization { get; set; } = 1.00;
        public double MonomericAndDimericFormsRatio { get; set; } = 0.25; // ml / mL 
        public double HexamerDisassociationRate { get; set; } = 0.3; // mU / min

        public double BloodCapillaryAbsorptionRate { get; set; } = 0.02; // mU / min
        public double SecondaryCapillaryAbsorptionRate { get; set; } = 0.02; // mU / min
        public double LymphaticCapillaryAbsorptionRate { get; set; } = 0.01; // mU / min
        public double EliminationRate { get; set; } = 0.0368; // mU / min

        public double LocalDegradationSaturationMonomers { get; set; } = 1.93; // mU / min
        public double LocalDegradationMidPointMonomers { get; set; } = 62.6; // mU

        public double LocalDegradationSaturationHexamers { get; set; } = 1.93; // mU / min
        public double LocalDegradationMidPointHexamers { get; set; } = 62.6; // mU

        public TimeSpan MinSimulationSpan = TimeSpan.FromMinutes(1);

        public InsulinModel1()
        {
        }
       
        public IEnumerable<(DateTimeOffset From, DateTimeOffset To, double Value)> Run(PodSession podSession, TimeSpan prolongation)
        {
            FastCompartment = 0d;
            SlowCompartment = 0d;
            Circulation = 0d;
            DisassociationCompartment = 0d;

            var list = podSession.GetDeliveries();
            var t0 = list[0].Time;
            var d0 = list[0].Delivered;
            foreach (var delivery in podSession.GetDeliveries().Skip(1))
            {
                if (t0 != delivery.Time)
                {
                    foreach (var frame in ExecuteFrames(t0, delivery.Time, d0))
                        yield return frame;
                    t0 = delivery.Time;
                    d0 = delivery.Delivered - d0;
                }
            }
        }

        private IEnumerable<(DateTimeOffset From, DateTimeOffset To, double Value)> ExecuteFrames
            (DateTimeOffset from, DateTimeOffset to, double deposit)
        {
            var t = (to - from).TotalMinutes;
            var d = (int) t;
            var offset = (to - from).TotalMilliseconds / d;
            while (from < to)
            {
                yield return ExecuteFrame(from, from.AddMilliseconds(offset), deposit / d);
                from = from.AddMilliseconds(offset);
            }
        }

        private (DateTimeOffset From, DateTimeOffset To, double Value) ExecuteFrame
            (DateTimeOffset from, DateTimeOffset to, double deposit)
        {
            //Debug.WriteLine($"{from} {to} {hourlyRate}");
            var t = (to - from).TotalMinutes;
            
            var lymphaticTransfer = 0d;
            var disassociation = 0d;
            var slowLocalDegradation = 0d;

            var secondaryCapillaryTransfer = 0d;
            
            var capillaryTransfer = 0d;
            var fastLocalDegradation = 0d;
            
            lymphaticTransfer = t * LymphaticCapillaryAbsorptionRate * SlowCompartment;
            disassociation = t * HexamerDisassociationRate * SlowCompartment;

            slowLocalDegradation = Math.Pow(
                LocalDegradationSaturationHexamers * SlowCompartment 
                / (LocalDegradationMidPointHexamers + SlowCompartment), t);

            secondaryCapillaryTransfer = t * SecondaryCapillaryAbsorptionRate * DisassociationCompartment;
            if (secondaryCapillaryTransfer > DisassociationCompartment)
                secondaryCapillaryTransfer = DisassociationCompartment;
            
            capillaryTransfer = t * BloodCapillaryAbsorptionRate * FastCompartment;
            
            fastLocalDegradation = Math.Pow(
                LocalDegradationSaturationMonomers * FastCompartment
                / (LocalDegradationMidPointMonomers + FastCompartment), t);

            // Debug.WriteLine($"{from}\t{to}\t{SlowCompartment:F4}\t{slowLocalDegradation:F4}\t{FastCompartment:F4}\t{fastLocalDegradation:F4}");

            SlowCompartment += deposit * (1 - MonomericAndDimericFormsRatio);
            SlowCompartment -= lymphaticTransfer;
            SlowCompartment -= disassociation;
            SlowCompartment -= slowLocalDegradation;
            
            DisassociationCompartment += disassociation;
            DisassociationCompartment -= secondaryCapillaryTransfer;
            
            FastCompartment += deposit * MonomericAndDimericFormsRatio;
            FastCompartment -= capillaryTransfer;
            FastCompartment -= fastLocalDegradation;
            
            Circulation -= t * EliminationRate * Circulation;

            Circulation += lymphaticTransfer;
            Circulation += capillaryTransfer;
            Circulation += secondaryCapillaryTransfer;
            

            return (from, to, Circulation * Factorization);
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
