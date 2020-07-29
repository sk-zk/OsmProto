using AerialImageRetrieval;
using DEM.Net.Core;
using ImageMagick;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using TruckLib;
using TruckLib.Model;
using TruckLib.ScsMap;
using TruckLib.Sii;

namespace OsmProto
{
    class TerrainCreator : Creator
    {
        public static int XSize => QuadSize * TerrainCols;
        public static int YSize => QuadSize * TerrainRows;
        private const ushort TerrainCols = 32;
        private const ushort TerrainRows = 16;
        private static int QuadSize => 16;

        private Point offset;

        private readonly ImageRetrieval ir;

        // terrain items are lowered slightly
        // to avoid z-fighting on most items
        private const float terrainOffset = -0.15f;

        private string modDir;
        private const string satOutputDir = "material/terrain/osm_proto/generated";
        private const int bingMaxLevel = 18;
        private const string ddsExtension = "dds";
        private string satAbsoluteOutputDir;

        private MatFile satMat;
        private Tobj satTobj;

        private DdsWriteDefines writeDefines;

        public TerrainCreator(Map map, IProjection projection, Elevation elevation, 
            Point offset, string modDir) : base(map, projection, elevation)
        {
            this.offset = offset;

            this.modDir = modDir;
            satAbsoluteOutputDir = Path.Combine(modDir, satOutputDir);

            Utils.DeleteDirectoryContents(satAbsoluteOutputDir);

            ir = new ImageRetrieval
            {
                ImageFormat = MagickFormat.Dds,
                Labeled = false
            };

            writeDefines = new DdsWriteDefines
            {
                Mipmaps = 1,
                FastMipmaps = true,
            };

            satMat = MatFile.Open("Assets/sat_image_tmpl.mat");
            satMat.Attributes["aux[0]"][0] = XSize;
            satMat.Attributes["aux[0]"][1] = YSize;

            satTobj = Tobj.Open("Assets/sat_image_tmpl.tobj");
        }

        public void Create(BBox<LatLon> bounds, BBox<Point> projBounds)
        {
            UpdateProgressMessage("[Terrain] Downloading DEM tiles");
            UpdateProgress(0);
            elevation.DownloadMissingFiles(bounds.Min, bounds.Max);
            UpdateProgress(1);

            var terrains = CreateTerrains(projBounds);
            LoadAndApplyElevations(terrains);
        }

        private List<Terrain> CreateTerrains(BBox<Point> projBounds)
        {
            UpdateProgressMessage("[Terrain] Creating terrain items and materials");
            UpdateProgress(0);

            var stepSize = StepSize.Meters16;

            var terrains = new List<Terrain>();
            var satMatSii = new SiiFile();
            var satMatIdx = 1;

            int i = 0;
            int totalX = (int)Math.Ceiling((projBounds.Max.X - projBounds.Min.X) / XSize);
            int totalY = (int)Math.Ceiling((projBounds.Max.Y - projBounds.Min.Y ) / YSize);

            int progressReportInterval = 5;
            for (double y = projBounds.Min.Y; y < projBounds.Max.Y; y += YSize)
            {
                for (double x = projBounds.Min.X; x < projBounds.Max.X; x += XSize)
                {
                    // create terrain item
                    var tPos = new Vector3((float)(x - offset.X), 0, (float)(y - offset.Y));
                    var trn = AddTerrainGrid(stepSize, TerrainCols, TerrainRows, tPos);
                    trn.ViewDistance = 1500;
                    terrains.Add(trn);

                    LoadAndApplySatelliteImage(trn, satMatIdx++, satMatSii);

                    i++;
                    if (i % progressReportInterval == 0)
                        UpdateProgress((float)i / (totalX * totalY));
                }
            }

            var satMatSiiDir = Path.Combine(modDir, "def/world/");
            Directory.CreateDirectory(satMatSiiDir);
            satMatSii.Serialize(Path.Combine(satMatSiiDir, "terrain_material.osm_proto.sii"));

            UpdateProgress(1);

            return terrains;
        }

        private void LoadAndApplySatelliteImage(Terrain trn, int satMatIdx, SiiFile satMatSii)
        {
            // get lat/lon of backward node.
            var latLonBw = MapPosToLatLon(trn.Node.Position);

            // get lat/lon of opposite corner
            var max = MapPosToLatLon(trn.Node.Position + new Vector3(XSize, 0, YSize));

            var satFileName = $"generated_sat_{satMatIdx}";
            var satTexturePath = Path.Combine(satAbsoluteOutputDir, satFileName + "." + ddsExtension);

            var img = ir.RetrieveMaxResolution(latLonBw.Latitude, latLonBw.Longitude,
                max.Latitude, max.Longitude, bingMaxLevel);
            var geo = new MagickGeometry
            {
                IgnoreAspectRatio = true,
                Width = NextMultipleOf4(img.Width),
                Height = NextMultipleOf4(img.Height),
            };
            img.Scale(geo);
            img.Write(satTexturePath, writeDefines);

            // add tobj and mat for it
            var satMatPath = $"/{satOutputDir}/{satFileName}.mat";
            satTobj.TexturePaths[0] = $"/{satOutputDir}/{satFileName}.{ddsExtension}";
            satTobj.Save(Path.Combine(satAbsoluteOutputDir, satFileName + ".tobj"));
            satMat.Attributes["texture"] = satFileName + ".tobj";
            satMat.Serialize(Path.Combine(satAbsoluteOutputDir, satFileName + ".mat"));

            // add it to a sii file
            var unitName = "g__" + satMatIdx.ToString();
            var unit = new Unit("material_def", "terrain_mat." + unitName);
            unit.Attributes.Add("path", satMatPath);
            satMatSii.Units.Add(unit);

            // assign it to the item
            trn.Right.Terrain.QuadData.Material = unitName;

            static int NextMultipleOf4(int n) => (n + 3) & ~0x3;
        }

        private void LoadAndApplyElevations(List<Terrain> terrains)
        {
            UpdateProgressMessage("[Terrain] Applying elevation");
            UpdateProgress(0);

            // batch requests to elevation API to reduce amount of disk reads
            int count = terrains.Count;
            int perBatch = 128;
            int totalBatches = (int)Math.Ceiling((float)count / perBatch);

            var tasks = new Task[totalBatches];
            int progress = 0;

            for (int batch = 0; batch < totalBatches; batch++)
            {
                var batchOffset = batch * perBatch;
                var end = (batch + 1 == totalBatches) // last batch?
                    ? (count % perBatch)
                    : perBatch;

                var t = LoadAndApplyElevationsAsync(terrains, perBatch, batchOffset, end);
                t.ContinueWith(x => 
                {
                    progress++;
                    UpdateProgress((float)progress / totalBatches);
                });
                tasks[batch] = t;
            }

            Task.WaitAll(tasks);

            UpdateProgress(1);
        }

        private async Task LoadAndApplyElevationsAsync(List<Terrain> terrains, 
            int perBatch, int batchOffset, int end)
        {
            await Task.Run(() =>
            {
                var coordsToRequest = new List<LatLon>();
                var listOffsets = new int[perBatch];

                // get needed coords
                for (int i = 0; i < end; i++)
                {
                    var index = i + batchOffset;

                    listOffsets[i] = coordsToRequest.Count;
                    var terrain = terrains[index];
                    AddPointsToElevationRequestList(terrain, coordsToRequest);
                }

                // fetch elevations
                var elevations = elevation.GetElevationList(coordsToRequest);

                // apply elevations
                for (int i = 0; i < end; i++)
                {
                    var index = i + batchOffset;
                    var terrain = terrains[index];
                    ApplyElevations(terrain, elevations, listOffsets[i]);
                }
            });
        }

        private void AddPointsToElevationRequestList(Terrain terrain, List<LatLon> coordsToRequest)
        {
            var offsets = terrain.Right.Terrain.QuadData.Offsets;

            coordsToRequest.Capacity += offsets.Count + 1;
            coordsToRequest.Add(MapPosToLatLon(terrain.Node.Position));

            // get lat/lons of terrain verts.
            for (int j = 0; j < offsets.Count; j++)
            {
                // convert offset relative to item pos
                // to absolute game map pos
                var absPos = terrain.Node.Position + offsets[j].Data;

                // convert game map pos to lat/lon
                var globeCoord = MapPosToLatLon(absPos);

                coordsToRequest.Add(globeCoord);
            }
        }

        private void ApplyElevations(Terrain terrain, List<GeoPoint> elevations, int listOffset)
        {
            // set height of item nodes.
            // both nodes are set to the same height because if we didn't,
            // the length of the line from bw node to fw node increases 
            // and the game generates more quad columns than we wanted.
            // of course, this will leave the bw node icon
            // hanging in midair, but I can live with that.
            var nodeElevation = elevations[listOffset].Elevation ?? 0;
            foreach (var node in new[] { terrain.Node, terrain.ForwardNode })
            {
                var pos = node.Position;
                pos.Y = (float)nodeElevation;
                node.Position = pos;
            }

            // set height of terrain verts.
            var offsets = terrain.Right.Terrain.QuadData.Offsets;
            for (int j = 0; j < offsets.Count; j++)
            {
                // get elevation at point and apply
                var vertData = offsets[j];
                vertData.Data.Y =
                    (float)(elevations[j + listOffset + 1].Elevation ?? 0)
                    - terrain.Node.Position.Y
                    + terrainOffset;
                offsets[j] = vertData;
            }
        }

        private Terrain AddTerrainGrid(StepSize stepSize, ushort cols, ushort rows, Vector3 topLeftPos)
        {
            /* Crazy hack to create a Terrain item where all quads are squares.

               This can be done via vertex offsets (which is what the Vertex tool
               in the editor manipulates).

               However, the side lengths we can use are restricted to the 
               step size values of the terrain: 2, 4, 12 or 16 meters.

               On top of that, setting the terrain size is rather unintuitive.
               The game has a hardcoded sequence for the width of each row of
               terrain quads, with 2 meters in the first row and ending
               at 100 meters for rows 15 and beyond. This means that if we want
               10 rows of 2m * 2m quads, the terrain size we set on the item
               is not 20, but 67 - because 67 is the highest value for which
               the game will create 10 rows of quads. */

            var quadSize = StepSizeToUshort(stepSize);

            var terrainSize = RowsToTerrainSize(rows);

            var length = cols * quadSize;
            var topRightPos = topLeftPos;
            topRightPos.X += length;
            Token terrainMat = "82";
            var trn = Terrain.Add(map, topLeftPos, topRightPos, terrainMat, 0, terrainSize);
            trn.StepSize = stepSize;
            trn.Right.Terrain.CalculateQuadGrid(stepSize, length);
            trn.Right.Terrain.Noise = TerrainNoise.Percent0;

            var qd = trn.Right.Terrain.QuadData;
            qd.Offsets.Capacity = qd.Cols * qd.Rows; // avoid resizing multiple times

            for (ushort col = 0; col < qd.Cols + 1; col++)
            {
                for (ushort row = 0; row < qd.Rows + 1; row++)
                {
                    var x = col * quadSize;
                    var y = row * quadSize;

                    var offset = new VertexData()
                    {
                        X = col,
                        Y = row,
                        Data = new Vector3(x, 0, y)
                    };
                    qd.Offsets.Add(offset);
                }
            }

            return trn;

            static ushort StepSizeToUshort(StepSize stepSize)
            {
                switch (stepSize)
                {
                    case StepSize.Meters2:
                        return 2;
                    default:
                    case StepSize.Meters4:
                        return 4;
                    case StepSize.Meters12:
                        return 12;
                    case StepSize.Meters16:
                        return 16;
                }
            }

            static float RowsToTerrainSize(ushort rows)
            {
                float terrainSize = 0;
                for (int i = 0; i < rows; i++)
                {
                    terrainSize += RoadTerrain.GetRowWidthAt(i);
                }
                return terrainSize;
            }
        }

        private LatLon MapPosToLatLon(Vector3 absPos)
        {
            var projPos = absPos;
            projPos.X += (float)offset.X;
            projPos.Z += (float)offset.Y * -1;

            var globeCoord = projection.Unproject(projPos.X, -projPos.Z);
            return globeCoord;
        }

    }
}
