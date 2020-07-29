using System;
using System.IO;
using TruckLib.ScsMap;
using OsmSharp.Streams;
using OsmSharp.Streams.Complete;
using System.Xml;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using DEM.Net.Core;
using System.Threading;
using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks;

namespace OsmProto
{
    class Program
    {
        static Map map;
        static IProjection projection = new Mercator();
        static Elevation elevation;
        static OsmCompleteStreamSource osm;

        static Point offset;
        static BBox<LatLon> bounds;
        static BBox<Point> projectedBounds;

        static string outputDir;

        static float progress;
        static string progressMsg;
        static Stopwatch stopwatch;
        static Timer timer;

        public static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        static void Main(string[] args)
        {
            var osmPath = args[0];
            var mapName = args[1];
            var outDirParent = args[2];

            Console.CursorVisible = false;
            Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-US");

            InitDemNetServices();

            map = new Map(mapName)
            {
                NormalScale = 1,
                CityScale = 1
            };
            outputDir = Path.Combine(outDirParent, mapName);

            OpenOsmFile(osmPath);
            GetBoundsAndOffset(osmPath);

            Console.WriteLine();

            CreateTerrain();
            CreateRoads();
            CreateRailways();
            CreateBuildings();

            Console.Write("\n");

            Console.WriteLine("Saving map");
            map.Save(Path.Combine(outputDir, "map"), true);

            Console.WriteLine("Done");
            ClearLastLine();
        }

        private static void OpenOsmFile(string osmPath)
        {
            var fs = File.OpenRead(osmPath);
            Console.WriteLine("Opening osm file");
            var stream = new XmlOsmStreamSource(fs);
            osm = stream.ToComplete();
        }

        private static void GetBoundsAndOffset(string osmPath)
        {
            // get an offset because we want the center of the osm file
            // to be the 0|0 point on the game map
            bounds = GetOsmBounds(osmPath);
            var projBoundsMin = projection.Project(
                bounds.Min.Latitude, bounds.Min.Longitude);
            var projBoundsMax = projection.Project(
                bounds.Max.Latitude, bounds.Max.Longitude);
            projectedBounds = new BBox<Point>(projBoundsMin, projBoundsMax);
            // change bounds to match the UV grid of the sat images
            projectedBounds.Min.X -= projectedBounds.Min.X % TerrainCreator.XSize;
            projectedBounds.Min.Y -= projectedBounds.Min.Y % TerrainCreator.YSize;
            CalculateOffset(bounds.Min, bounds.Max);
        }

        private static void CalculateOffset(LatLon Min, LatLon Max)
        {
            var centerX = Min.Latitude + ((Max.Latitude - Min.Latitude) / 2);
            var centerY = Min.Longitude + ((Max.Longitude - Min.Longitude) / 2);
            offset = projection.Project(centerX, centerY);
            // change offset to match the UV grid of the sat images
            offset.X -= offset.X % TerrainCreator.XSize;
            offset.Y -= offset.Y % TerrainCreator.YSize;
        }

        /// <summary>
        /// Gets the lon/lat bounds of an osm file.
        /// </summary>
        /// <param name="osmPath"></param>
        /// <returns></returns>
        private static BBox<LatLon> GetOsmBounds(string osmPath)
        {
            // TODO: Make this faster and use less memory
            var xml = new XmlDocument();
            xml.Load(osmPath);
            var boundsNodeList = xml.GetElementsByTagName("bounds");

            if (boundsNodeList == null)
                throw new Exception("No bounds tag in file");

            var bounds = boundsNodeList[0];
            var min = new LatLon(
                double.Parse(bounds.Attributes["minlat"].Value, Culture),
                double.Parse(bounds.Attributes["minlon"].Value, Culture)
                );
            var max = new LatLon(
                double.Parse(bounds.Attributes["maxlat"].Value, Culture),
                double.Parse(bounds.Attributes["maxlon"].Value, Culture)
                );
            return new BBox<LatLon>(min, max);
        }

        private static void CreateRoads()
        {
            RunCreator(
                () => new RoadCreator(map, projection, elevation),
                c => c.Create(osm, offset));
        }

        private static void CreateRailways()
        {
            RunCreator(
                () => new RailwayCreator(map, projection, elevation),
                c => c.Create(osm, offset));
        }

        private static void CreateBuildings()
        {
            RunCreator(
                () => new BuildingCreator(map, projection, elevation),
                c => c.Create(osm, offset, outputDir));
        }

        private static void CreateTerrain()
        {
            RunCreator(
                () => new TerrainCreator(map, projection, elevation, offset, outputDir),
                c => c.Create(bounds, projectedBounds));
        }

        private static void RunCreator<T>(Func<T> constructor, Action<T> createCall) where T : Creator
        {
            stopwatch = new Stopwatch();
            stopwatch.Start();

            var creator = constructor.Invoke();
            creator.ProgressChanged += ProgressChanged;
            creator.ProgressMessageChanged += ProgressMessageChanged;
            timer = new Timer(_ => WriteProgress(), null, 500, 500);
            createCall.Invoke(creator);
            timer.Dispose();

            stopwatch.Stop();
        }

        private static void InitDemNetServices()
        {
            var services = new ServiceCollection().AddLogging(config =>
            {
                //config.AddConsole();
            }).AddDemNetCore().AddSingleton<Elevation>();
            var provider = services.BuildServiceProvider();
            elevation = provider.GetService<Elevation>();
        }

        #region Console stuff

        private static readonly object consoleLock = new object();

        private static void ProgressMessageChanged(object sender, string e)
        {
            progressMsg = e;
            lock (consoleLock)
            {
                Console.WriteLine(progressMsg);
            }
            ClearLastLine();
            stopwatch?.Restart();
        }

        private static void ProgressChanged(object sender, float e)
        {
            progress = e;
            WriteProgress();
        }

        private static void WriteProgress()
        {
            Task.Run(() =>
            {
                lock (consoleLock)
                {
                    const int totalBlocks = 40;

                    if (progressMsg == "" && progress <= 0)
                        return;

                    var prevLeft = Console.CursorLeft;
                    var prevTop = Console.CursorTop;

                    Console.SetCursorPosition(0, Console.WindowHeight - 1);
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    ClearCurrentLine();

                    var percentStr = (progress * 100).ToString("0.00").PadLeft(6);
                    Console.Write($"{percentStr}% ");

                    var blocks = (int)(totalBlocks * progress);
                    Console.Write(new string('█', blocks));
                    Console.Write(new string('░', totalBlocks - blocks));

                    const string timeFormat = @"mm':'ss";
                    var elapsedStr = stopwatch.Elapsed.ToString(timeFormat);

                    Console.Write($" ({elapsedStr} elapsed");
                    if (progress > 0 && stopwatch.Elapsed.TotalSeconds > 5)
                    {
                        var remainingStr = ((stopwatch.Elapsed / progress) - stopwatch.Elapsed).ToString(timeFormat);
                        Console.Write($", {remainingStr} remaining");
                    }
                    Console.Write(")");

                    Console.SetCursorPosition(prevLeft, prevTop);
                    Console.ResetColor();
                }
            });
        }

        private static void ClearCurrentLine()
        {
            Console.Write("\r" + new string(' ', Console.WindowWidth) + "\r");
        }

        private static void ClearLastLine()
        {
            var prev = Console.CursorTop;
            Console.CursorTop = Console.WindowHeight - 1;
            ClearCurrentLine();
            Console.CursorTop = prev;
        }

        #endregion
    }
}
