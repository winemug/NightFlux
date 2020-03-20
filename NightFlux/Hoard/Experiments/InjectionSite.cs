using System;
using System.Collections.Generic;

namespace NightFlux.Hoard.Experiments
{
    public class InjectionSite
    {
        public DateTimeOffset Updated;

        // infusion rate dV/dt = 0.05 ml / 2s
        // travel in interstitial space
        // distribution of pressure increase
        // blood capillary distance

        public double Depot = 0d;
        public double Deposited = 0d;
        public double Transferring = 0d;
        public double Transferred = 0d;

        public double InsulinAspartSedimentationCoefficient = 3.3;
        public double MonomerAndDimerRatio = 0.2;
        public double HexamerRatio = 0.8;

        public double LymphicCapillaryAvailabilityModifier = 0.5;
        public double BloodCapillaryAvailabilityModifier = 0.85;

        public TimeSpan LymphicCapillaryTravelTime = new TimeSpan(0, 20, 0);
        public TimeSpan BloodCapillaryTravelTime = new TimeSpan(0, 3, 30);

        public Dictionary<DateTimeOffset, double> BloodCapillaryTransferDictionary = 
            new Dictionary<DateTimeOffset, double>();
        public Dictionary<DateTimeOffset, double> LymphCapillaryTransferDictionary = 
            new Dictionary<DateTimeOffset, double>();

        public void AdvanceToDate(DateTimeOffset when)
        {
            var depotRadius = GetRadius(Depot);
            var depotArea = GetArea(depotRadius);
            var sedimentTotalVolume = Depot;
            var sedimentCount = sedimentTotalVolume / InsulinAspartSedimentationCoefficient;
            var sedimentVolume = sedimentTotalVolume / sedimentCount;
            var sedimentRadius = GetRadius(sedimentVolume);
            var sedimentArea = GetArea(sedimentRadius);
            var exposedSedimentCount = depotArea / sedimentArea;
            var exposureRatio = Math.Min(exposedSedimentCount / sedimentCount, 1d);

            var transferViaBloodCapillary = Depot * exposureRatio * MonomerAndDimerRatio 
                                            * BloodCapillaryAvailabilityModifier;

            var transferViaLymphicCapillary = Depot * exposureRatio * HexamerRatio *
                                              LymphicCapillaryAvailabilityModifier;
            
            BloodCapillaryTransferDictionary.Add(when, transferViaBloodCapillary);
            LymphCapillaryTransferDictionary.Add(when, transferViaLymphicCapillary);
            Transferring += transferViaBloodCapillary + transferViaLymphicCapillary;

            Updated = when;
            Depot = Deposited - Transferring - Transferred;

        }

        public void Inject(double amount)
        {
            Deposited += amount;
        }

        private double GetRadius(double volume)
        {
            return Math.Pow((3 * volume) / (4 * Math.PI), 1d/3d);
        }

        private double GetArea(double radius)
        {
            return 4 * Math.PI * radius * radius;
        }
    }
}