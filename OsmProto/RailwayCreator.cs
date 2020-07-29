using OsmSharp;
using OsmSharp.Complete;
using OsmSharp.Streams.Complete;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using TruckLib.ScsMap;

namespace OsmProto
{
    class RailwayCreator : Creator
    {
        public RailwayCreator(Map map, IProjection projection, Elevation elevation) 
            : base(map, projection, elevation)
        {
        }

        public void Create(OsmCompleteStreamSource osm, Point offset)
        {
            UpdateProgressMessage("[Railways] Creating railways");
            UpdateProgress(0);

            var railways = GetRailways(osm);

            int progressReportInterval = 16;
            for (int i = 0; i < railways.Count; i++)
            {
                var rail = (CompleteWay)railways[i];

                var nodes = Utils.ProjectWayWithLineElevation(rail, offset, projection, elevation);
                AddRailwayToMap(rail, nodes);

                if (i % progressReportInterval == 0)
                    UpdateProgress((float)(i + 1) / railways.Count);
            }
        }

        private List<ICompleteOsmGeo> GetRailways(OsmCompleteStreamSource osm)
        {
            return osm.Where(x => x.Type == OsmGeoType.Way
                    && x.Tags != null
                    && x.Tags.ContainsKey("railway"))
                .ToList();
        }

        private void AddRailwayToMap(CompleteWay way, List<Vector3> nodes)
        {
            // create curve with the first two nodes
            var curve = Curve.Add(map,
               nodes[0], nodes[1],
               "osm_rail");
            curve.ViewDistance = ViewDistance;
            curve.UseLinearPath = true;

            if (way.Tags.ContainsKey("service") && (way.Tags["service"] == "siding"
                || way.Tags["service"] == "yard"))
            {
                curve.Look = "siding";
            }

            // and append the rest
            for (int i = 2; i < nodes.Count; i++)
            {
                curve = curve.Append(nodes[i], true);
            }
        }
    }
}
