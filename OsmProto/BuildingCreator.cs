using OsmSharp;
using OsmSharp.Complete;
using OsmSharp.Streams.Complete;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using TruckLib;
using TruckLib.ScsMap;
using TruckLib.Sii;

namespace OsmProto
{
    class BuildingCreator : Creator
    {
        private const string pmdExtension = "pmd";
        private const string bldOutput = "model2/building/osm_proto/generated";

        public BuildingCreator(Map map, IProjection projection, Elevation elevation) 
            : base(map, projection, elevation) { }

        public void Create(OsmCompleteStreamSource src, Point offset, string outputDir)
        {
            var buildings = GetBuildings(src);
            var centroids = buildings.Select(x => GetCentroid((CompleteWay)x)).ToList();
            var elevations = GetBuildingElevations(centroids);
            CreateBuildings(buildings, centroids, elevations, offset, outputDir);
        }

        private void CreateBuildings(List<ICompleteOsmGeo> buildings, List<LatLon> centroids,
            Dictionary<LatLon, double?> elevations, Point offset, string outputDir)
        {
            UpdateProgress(0);
            UpdateProgressMessage("[Buildings] Generating building models");

            var bldOutputAbsolute = Path.Combine(outputDir, bldOutput);
            Utils.DeleteDirectoryContents(bldOutputAbsolute);

            var modelSii = new SiiFile();
            var modelSiiIdx = 0;

            int progressReportInterval = 32;
            for (int i = 0; i < buildings.Count; i++)
            {
                var building = (CompleteWay)buildings[i];

                var modelName = "generated_bld_" + building.Id;
                var bldOutputFile = $"/{bldOutput}/{modelName}.{pmdExtension}";

                var projCentroid = projection.Project(centroids[i].Latitude, centroids[i].Longitude);
                var offsetCentroid = new Vector3((float)(projCentroid.X - offset.X), 
                    0, (float)((projCentroid.Y - offset.Y) * -1));
                var nodes = Utils.ProjectWay(building, offset, projection, elevation, false);
                GenerateModel(building, nodes, offsetCentroid, modelName, bldOutputAbsolute);

                // create model.sii entry
                var unitName = new Token($"g__{modelSiiIdx}");
                var unit = new Unit("model_def", $"model.{unitName}");
                modelSiiIdx++;
                unit.Attributes.Add("model_desc", bldOutputFile);
                unit.Attributes.Add("category", "osm_proto_generated");
                modelSii.Units.Add(unit);

                // add the model to the map
                var modelMapPos = offsetCentroid;
                var elevationLatLon = GetCentroid(building);
                modelMapPos.Y = (float)(elevations[elevationLatLon] ?? 0);
                var modelMapItem = Model.Add(map, modelMapPos, 0, unitName, "default", "default");
                modelMapItem.ViewDistance = 950;

                if (i % progressReportInterval == 0)
                    UpdateProgress((float)(i + 1) / buildings.Count);
            }

            Directory.CreateDirectory(Path.Combine(outputDir, "def/world"));
            var modelSiiPath = Path.Combine(outputDir, "def/world/model.osm_proto.sii");
            modelSii.Serialize(modelSiiPath);

            UpdateProgress(1);
        }

        private Dictionary<LatLon, double?> GetBuildingElevations(List<LatLon> centroids)
        {
            UpdateProgressMessage("[Buildings] Downloading building elevations");
            UpdateProgress(0);
            var elevations = elevation.GetElevationDict(centroids);
            UpdateProgress(1);
            return elevations;
        }

        private void GenerateModel(CompleteWay building, List<Vector3> nodes,
            Vector3 centroid, string modelName, string bldOutputAbsolute)
        {
            const float heightIfUnspecified = 8f;
            const float storeyHeight = 3;

            // closed paths in osm end with the first node,
            // which is not needed for the model generator
            if (nodes.Last() == nodes[0])
                nodes.Remove(nodes.Last());

            var relativeNodes = nodes.Select(x => x -= centroid).ToList();

            var height = heightIfUnspecified;
            if (building.Tags.ContainsKey("height"))
            {
                float.TryParse(building.Tags["height"], NumberStyles.Float,
                    Program.Culture, out height);
            }
            else if(building.Tags.ContainsKey("building:levels"))
            {
                float.TryParse(building.Tags["building:levels"], NumberStyles.Float,
                    Program.Culture, out float levels);
                height = levels * storeyHeight; 
            }

            // generate building model from path
            var model = ModelGenerator.GeneratePrism(relativeNodes, new Vector3(0, height, 0), Color.LightGray);
            model.Looks[0].Materials.Add($"/{bldOutput}/__default.mat");

            model.Name = modelName;
            model.Save(bldOutputAbsolute);
        }

        /// <summary>
        /// Returns the centroid of a building.
        /// </summary>
        private static LatLon GetCentroid(CompleteWay building)
        {
            var points = building.Nodes.Select(
                    x => new GeoAPI.Geometries.Coordinate(x.Latitude.Value, x.Longitude.Value))
                .ToArray();
            var poly = new NetTopologySuite.Geometries.Polygon(new NetTopologySuite.Geometries.LinearRing(points));
            var centroid = poly.Centroid.Coordinate;

            return new LatLon(centroid.X, centroid.Y);
        }

        private List<ICompleteOsmGeo> GetBuildings(OsmCompleteStreamSource osm) =>
            osm.Where(x => x.Type == OsmGeoType.Way
                && x.Tags != null
                && x.Tags.ContainsKey("building"))
            .ToList();
    }
}
