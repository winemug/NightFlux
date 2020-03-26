﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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

        public double BloodCapillaryAbsorptionRate { get; set; } = 0.05; // mU / min
        public double SecondaryCapillaryAbsorptionRate { get; set; } = 0.04; // mU / min
        public double LymphaticCapillaryAbsorptionRate { get; set; } = 0.01; // mU / min
        public double EliminationRate { get; set; } = 0.0368; // mU / min

        public double LocalDegradationSaturationMonomers { get; set; } = 0.006; // mU / min
        public double LocalDegradationMidPointMonomers { get; set; } = 0.6; // mU

        public double LocalDegradationSaturationHexamers { get; set; } = 0.006; // mU / min
        public double LocalDegradationMidPointHexamers { get; set; } = 0.6; // mU

        public TimeSpan MinSimulationSpan = TimeSpan.FromMinutes(1);

        public InsulinModel1()
        {
        }
       
        public IEnumerable<(DateTimeOffset From, DateTimeOffset To, double Value)> Run(PodSession podSession)
        {
            FastCompartment = 0;
            SlowCompartment = 0;
            Circulation = 0;

            foreach (var frame in podSession.Frames(MinSimulationSpan))
            {
                yield return ExecuteFrame(frame.From,frame.To, frame.Value);
            }
        }
        

        private (DateTimeOffset From, DateTimeOffset To, double Value) ExecuteFrame
            (DateTimeOffset from, DateTimeOffset to, double hourlyRate)
        {
            //Debug.WriteLine($"{from} {to} {hourlyRate}");
            var durationMinutes = (to - from).Minutes;

            var deposit = hourlyRate / 60d * durationMinutes;

            var lymphaticTransfer = 0d;
            var disassociation = 0d;
            var slowLocalDegradation = 0d;

            var secondaryCapillaryTransfer = 0d;
            
            var capillaryTransfer = 0d;
            var fastLocalDegradation = 0d;
            
            if (SlowCompartment > 0)
            {

                var reduction = lymphaticTransfer + disassociation + slowLocalDegradation;
                if (reduction > SlowCompartment)
                {
                    var rationing = SlowCompartment / reduction;
                    lymphaticTransfer *= rationing;
                    disassociation *= rationing;
                    slowLocalDegradation *= rationing;
                }
            }

            if (DisassociationCompartment > 0)
            {
                secondaryCapillaryTransfer =
                    DisassociationCompartment * SecondaryCapillaryAbsorptionRate * durationMinutes;
                if (secondaryCapillaryTransfer > DisassociationCompartment)
                    secondaryCapillaryTransfer = DisassociationCompartment;
                
            }
            
            if (FastCompartment > 0)
            {
                capillaryTransfer = FastCompartment * BloodCapillaryAbsorptionRate * durationMinutes;
                fastLocalDegradation =
                    LocalDegradationSaturationMonomers * FastCompartment * durationMinutes
                    / (LocalDegradationMidPointMonomers + FastCompartment);

                var reduction = capillaryTransfer + fastLocalDegradation;
                if (reduction > FastCompartment)
                {
                    var rationing = FastCompartment / reduction;
                    capillaryTransfer *= rationing;
                    fastLocalDegradation *= rationing;
                }
            }

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
            
            Circulation += lymphaticTransfer;
            Circulation += capillaryTransfer;
            Circulation += secondaryCapillaryTransfer;
            Circulation -= EliminationRate * durationMinutes;

            if (Circulation < 0)
                Circulation = 0;
            

            return (from, to, Circulation * Factorization);
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
