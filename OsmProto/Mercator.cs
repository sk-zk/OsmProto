using DotSpatial.Projections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OsmProto
{
    class Mercator : IProjection
    {
        private ProjectionInfo projection = KnownCoordinateSystems.Projected.World.WebMercator;
        private ProjectionInfo geo = KnownCoordinateSystems.Geographic.World.WGS1984;

        /// <summary>
        /// Projects the position of the node to mercator.
        /// </summary>
        /// <param name="node">The OSM node.</param>
        /// <returns>The X/Y position on a Mercator map.</returns>
        public Point Project(OsmSharp.Node node) =>
            Project(node.Latitude.Value, node.Longitude.Value);

        /// <summary>
        /// Projects a global coordinate to mercator.
        /// </summary>
        /// <param name="lat">Latitude</param>
        /// <param name="lon">Longitude</param>
        /// <returns>The X/Y position on a Mercator map.</returns>
        public Point Project(double lat, double lon) 
        {
            var xy = new[] { lon, lat };
            var z = new[] { 0.0 };
            Reproject.ReprojectPoints(xy, z, geo, projection, 0, 1);
            return new Point(xy[0], xy[1]);
        }

        public LatLon Unproject(double X, double Y)
        {
            var xy = new[] { X, Y };
            var z = new[] { 0.0 };
            Reproject.ReprojectPoints(xy, z, projection, geo, 0, 1);
            return new LatLon(xy[1], xy[0]);
        }
    }
}
