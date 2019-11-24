using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OxyPlot;

namespace NightFlux.UI
{
    public class PreviousValueInterpolationAlgorithm : IInterpolationAlgorithm
    {
        public List<DataPoint> CreateSpline(List<DataPoint> points, bool isClosed, double tolerance)
        {
            var list = new List<DataPoint>();
            foreach (var point in points)
            {
                if (list.Count > 0)
                {
                    var lastPoint = list.Last();
                    list.Add(new DataPoint(point.X, lastPoint.Y));
                }
                list.Add(point);
            }
            return list;
        }

        public List<ScreenPoint> CreateSpline(IList<ScreenPoint> points, bool isClosed, double tolerance)
        {
            var list = new List<ScreenPoint>();
            foreach (var point in points)
            {
                if (list.Count > 0)
                {
                    var lastPoint = list.Last();
                    list.Add(new ScreenPoint(point.X, lastPoint.Y));
                }
                list.Add(point);
            }
            return list;
        }
    }
}
