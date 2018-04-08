﻿using OpenCV.Net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bonsai.Vision
{
    [Description("Extracts the set of points in a contour.")]
    public class ContourPoints : Transform<Contour, Point[]>
    {
        static readonly Point[] EmptyPoints = new Point[0];

        public override IObservable<Point[]> Process(IObservable<Contour> source)
        {
            return source.Select(input =>
            {
                if (input == null) return EmptyPoints;
                var points = new Point[input.Count];
                input.CopyTo(points);
                return points;
            });
        }
    }
}