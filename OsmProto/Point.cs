using System;
using System.Collections.Generic;
using System.Text;

namespace OsmProto
{
    struct Point
    {
        public double X;
        public double Y;

        public Point(double x, double y) : this()
        {
            X = x;
            Y = y;
        }

        public override int GetHashCode() => HashCode.Combine(X, Y);

        public override bool Equals(object obj) => 
            obj is Point p && p.X == X && p.Y == Y;
    }
}
