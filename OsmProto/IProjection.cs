using OsmSharp;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace OsmProto
{
    interface IProjection
    {
        Point Project(Node node);
        Point Project(double lat, double lon);
        LatLon Unproject(double X, double Y);
    }
}
