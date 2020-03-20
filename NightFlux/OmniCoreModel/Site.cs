using System;
using NightFlux.Model;

namespace NightFlux.OmniCoreModel
{
    public class Site
    {
        public int Id { get; set; }
        public HormoneType Hormone { get; set; }
        public decimal UnitsPerMilliliter { get; set; }
        public DateTimeOffset InfusionStart { get; set; }
        public DateTimeOffset InfusionStop { get; set; }
    }
}
