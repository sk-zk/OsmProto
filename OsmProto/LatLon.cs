using System;
using System.Collections.Generic;
using System.Text;

namespace OsmProto
{
    struct LatLon
    {
        public double Latitude;
        public double Longitude;

        public LatLon(double lat, double lon) : this()
        {
            Latitude = lat;
            Longitude = lon;
        }

        public override int GetHashCode() => HashCode.Combine(Latitude, Longitude);

        public override bool Equals(object obj) =>
            obj is LatLon l && l.Latitude == Latitude && l.Longitude == Longitude;
    }
}
