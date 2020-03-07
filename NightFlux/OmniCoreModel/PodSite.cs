using System;
using System.Collections.Generic;
using System.Text;

namespace NightFlux.OmniCoreModel
{
    public class PodSite
    {
        public int Id { get; set; }
        public HormoneType Hormone { get; set; }
        public decimal UnitsPerMilliliter { get; set; }
    }
}
