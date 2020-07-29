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
    class RoadCreator : Creator
    {
        private const int maxSegmentLength = 50;

        private const string Highway10m = "osm_hw_10m";
        private const string Highway5m = "osm_hw_5m";
        private const string HighwayPath = "osm_hw_path";
        private static readonly Dictionary<string, (string Unit, string Look)> roadAppearances
            = new Dictionary<string, (string Unit, string Look)>
            {
                ["motorway"] = (Highway10m, "motorway"),
                ["motorway_link"] = (Highway10m, "motorway"),
                ["trunk"] = (Highway10m, "motorway"),
                ["trunk_link"] = (Highway10m, "motorway"),
                ["service"] = (Highway5m, "service"),

                ["primary"] = (Highway10m, "primary"),
                ["primary_link"] = (Highway10m, "primary"),

                ["secondary"] = (Highway10m, "secondary"),
                ["secondary_link"] = (Highway10m, "secondary"),

                ["tertiary"] = (Highway10m, "tertiary"),
                ["tertiary_link"] = (Highway10m, "tertiary"),

                ["residential"] = (Highway5m, "residential"),
                ["living_street"] = (Highway5m, "residential"),

                ["pedestrian"] = (Highway5m, "pedestrian"),

                ["platform"] = (Highway5m, "platform"),

                ["unclassified"] = (Highway10m, "unclassified"),

                ["track"] = (HighwayPath, "track"),
                ["path"] = (HighwayPath, "path"),
                ["cycleway"] = (HighwayPath, "cycleway"),
                ["footway"] = (HighwayPath, "footway"),
                ["steps"] = (HighwayPath, "footway"), // TODO: proper steps curve
            };

        public RoadCreator(Map map, IProjection projection, Elevation elevation) 
            : base(map, projection, elevation)
        {
        }

        public void Create(OsmCompleteStreamSource osm, Point offset)
        {
            UpdateProgressMessage("[Roads] Creating roads");
            UpdateProgress(0);

            var highways = GetHighways(osm);

            int progressReportInterval = 16;
            for (int i = 0; i < highways.Count; i++)
            {
                var way = (CompleteWay)highways[i];

                // ignore roads that don't even exist
                if (way.Tags["highway"] == "proposed") continue;

                var nodes = Utils.ProjectWayWithLineElevation(way, offset, projection, elevation);
                if (nodes.Count > 1)
                    AddRoadToMap(way, nodes);

                if (i % progressReportInterval == 0)
                    UpdateProgress((float)(i+1) / highways.Count);
            }
        }

        private List<ICompleteOsmGeo> GetHighways(OsmCompleteStreamSource osm) =>
            osm.Where(x => x.Type == OsmGeoType.Way
                    && x.Tags != null
                    && x.Tags.ContainsKey("highway"))
                .ToList();

        private void AddRoadToMap(CompleteWay way, List<Vector3> nodes)
        {
            // create curve with the first two nodes
            var curve = CreateNextCurve(null, nodes[0], nodes[1]);

            // and append the rest
            for (int i = 2; i < nodes.Count; i++)
            {
                curve = CreateNextCurve(curve, curve.ForwardNode.Position, nodes[i]);
            }

            Curve CreateNextCurve(Curve prevCurve, Vector3 prevNodePos, Vector3 nextNodePos)
            {
                var distToNext = Vector3.Distance(prevNodePos, nextNodePos);

                if (distToNext <= maxSegmentLength)
                {
                    prevCurve = CreateCurveItem(prevCurve, prevNodePos, nextNodePos);
                }
                else
                {
                    // split segment into multiple seg.s of even length 
                    // if longer than maxSegmentLength
                    var segCount = MathF.Ceiling(distToNext / maxSegmentLength);
                    var segVector = nextNodePos - prevNodePos;
                    var partVector = segVector / segCount;

                    for (int segIdx = 0; segIdx < segCount; segIdx++)
                    {
                        var prevCurvePos = prevCurve is null ?
                            prevNodePos : prevCurve.ForwardNode.Position;

                        prevCurve = CreateCurveItem(prevCurve, prevNodePos, prevCurvePos + partVector);
                    }
                }

                return prevCurve;
            }

            Curve CreateCurveItem(Curve prevCurve, Vector3 prevNodePos, Vector3 nextNodePos)
            {
                if (prevCurve is null)
                {
                    prevCurve = Curve.Add(map,
                        prevNodePos, nextNodePos,
                        "osm_hw_5m");
                    prevCurve.Look = "unclassified";
                    DetermineRoadAppearance(way, prevCurve);
                }
                else
                {
                    prevCurve = prevCurve.Append(nextNodePos, true);
                }
                return prevCurve;
            }
        }

        private void DetermineRoadAppearance(CompleteWay way, Curve curve)
        {
            curve.ViewDistance = ViewDistance;
            curve.UseLinearPath = true;

            var hwTag = way.Tags["highway"];
            if (roadAppearances.ContainsKey(hwTag))
            {
                curve.Look = roadAppearances[hwTag].Look;
                curve.Model = roadAppearances[hwTag].Unit;
            }
            else
            {
                // Console.WriteLine($"Unhandled highway tag: {way.Tags["highway"]}");
            }
        }

    }
}
