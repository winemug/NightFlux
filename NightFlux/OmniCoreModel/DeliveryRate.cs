using System;
using System.Collections.Generic;
using System.Text;

namespace NightFlux.OmniCoreModel
{
    public struct DeliveryRate
    {
        public PodSite Site { get; set; }
        public DeliveryType Type { get; set; }
        public decimal Rate { get; set; }
    }
}
