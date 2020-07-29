using OsmSharp.Complete;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;

namespace OsmProto
{
    static class Utils
    {
        /// <summary>
        /// Returns the coordinates of a way's nodes projected and with elevation.
        /// </summary>
        /// <param name="way"></param>
        /// <param name="offsetX"></param>
        /// <param name="offsetY"></param>
        /// <returns></returns>
        public static List<Vector3> ProjectWay(CompleteWay way, Point offset,
            IProjection projection, Elevation elevation, bool fetchElevation = true)
        {
            var nodes = new List<Vector3>();
            for (int i = 0; i < way.Nodes.Length; i++)
            {
                var node = way.Nodes[i];
                // convert to X/Y
                var projPos = projection.Project(node);

                // apply offset
                projPos.X -= offset.X;
                projPos.Y -= offset.Y;

                // flip vertically because Z is down
                projPos.Y *= -1;

                double el = 0;
                if (fetchElevation)
                    el = elevation.GetElevation(node.Latitude.Value, node.Longitude.Value);

                var vector = new Vector3((float)projPos.X, (float)el, (float)projPos.Y);
                nodes.Add(vector);
            }

            return nodes;
        }

        /// <summary>
        /// Converts a way into a projected path that includes a point for every elevation change on the path.
        /// </summary>
        /// <param name="way"></param>
        /// <param name="offset"></param>
        /// <param name="projection"></param>
        /// <param name="elevation"></param>
        /// <returns></returns>
        public static List<Vector3> ProjectWayWithLineElevation(CompleteWay way, Point offset,
            IProjection projection, Elevation elevation)
        {
            // get line points
            var geoPoints = elevation.GetWayElevation(way.Nodes).ToList();

            // convert to projected points
            var nodes = geoPoints.Select(point =>
            {
                // convert to X/Y
                var projPos = projection.Project(point.Latitude, point.Longitude);

                // apply offset
                projPos.X -= offset.X;
                projPos.Y -= offset.Y;

                // flip vertically because Z is down
                projPos.Y *= -1;

                return new Vector3((float)projPos.X, (float)point.Elevation, (float)projPos.Y);
            }).ToList();

            // remove points that are too close to each other to avoid "Curve item is too small" errors
            const float epsilon = 0.8f;
            const float epsilonSquared = epsilon * epsilon;
            var unique = new List<Vector3>(nodes.Count);
            unique.Add(nodes[0]);
            for (int i = 1; i < nodes.Count; i++)
            {
                if (Vector3.DistanceSquared(nodes[i - 1], nodes[i]) < epsilonSquared)
                    continue;
                else
                    unique.Add(nodes[i]);
            }

            return unique;
        }

        public static void DeleteDirectoryContents(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
                while (Directory.Exists(path))
                    System.Threading.Thread.Sleep(10);
            }
            Directory.CreateDirectory(path);
        }
    }
}
