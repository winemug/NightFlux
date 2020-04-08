using System;
using System.Collections.Generic;
using NightFlux.Model;

namespace NightFlux.Simulations
{
    public class InsulinModel2
    {
        public IEnumerable<(DateTimeOffset From, DateTimeOffset To, double Value)> Run(PodSession podSession, TimeSpan prolongation)
        {
            foreach (var delivery in podSession.GetDeliveries())
            {

            }

            return null;
        }
    }
}