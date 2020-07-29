using DEM.Net.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace OsmProto
{
    class Elevation
    {
        private DEMDataSet dataSet;
        private IElevationService elevationService;
        private ILogger<Elevation> logger;

        public Elevation(IElevationService elevationService, ILogger<Elevation> logger)
        {
            this.elevationService = elevationService;
            this.logger = logger;
            dataSet = DEMDataSet.SRTM_GL1;
        }

        public void DownloadMissingFiles(LatLon Min, LatLon Max)
        {
            elevationService.DownloadMissingFiles(dataSet, new BoundingBox(
                Math.Floor(Min.Longitude), Math.Ceiling(Max.Longitude),
                Math.Floor(Min.Latitude), Math.Ceiling(Max.Latitude)
                ));
        }

        public double GetElevation(double lat, double lon)
        {
            var point = elevationService.GetPointElevation(lat, lon, dataSet);
            return point?.Elevation ?? 0;
        }

        public List<GeoPoint> GetWayElevation(IEnumerable<OsmSharp.Node> wayNodes)
        {
            var geoPoints = wayNodes.Select(n => new GeoPoint(n.Latitude ?? 0, n.Longitude ?? 0));
            var elevations = elevationService.GetLineGeometryElevation(geoPoints, dataSet);
            return elevations;
        }

        public List<GeoPoint> GetElevationList(IEnumerable<LatLon> points)
        {
            var geoPoints = points.Select(p => new GeoPoint(p.Latitude, p.Longitude));
            var elevations = elevationService.GetPointsElevation(geoPoints, dataSet);
            return elevations.ToList();
        }

        public Dictionary<LatLon, double?> GetElevationDict(IEnumerable<LatLon> points)
        {
            var geoPoints = points.Select(p => new GeoPoint(p.Latitude, p.Longitude));
            var elevations = elevationService.GetPointsElevation(geoPoints, dataSet);
            return ElevationsToDict(elevations);
        }

        private static Dictionary<LatLon, double?> ElevationsToDict(IEnumerable<GeoPoint> elevations) =>
            elevations.ToDictionary(
                k => new LatLon(k.Latitude, k.Longitude),
                v => v.Elevation);

    }
}
