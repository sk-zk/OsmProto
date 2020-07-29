using LibTessDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace OsmProto
{
    static class Tessellator
    {
        /// <summary>
        /// Tessellates a polygon with libtess.
        /// </summary>
        /// <param name="contour"></param>
        /// <returns></returns>
        public static (List<Vector3> Vertices, List<Vector3> Triangles) TessellatePolygon(List<Vector3> contour)
        {
            var tess = new Tess();
            var tessContour = contour.Select(v => new ContourVertex()
            {
                Position = new Vec3() { X = v.X, Y = v.Z }
            }).ToArray();
            tess.AddContour(tessContour, ContourOrientation.CounterClockwise);
            tess.Tessellate(WindingRule.EvenOdd, ElementType.Polygons, 3);

            // convert verts to Vector3
            var verts = new List<Vector3>();
            foreach (var vert in tess.Vertices)
            {
                verts.Add(new Vector3(
                    vert.Position.X, 0, vert.Position.Y));
            }

            // convert triangles to Vector3
            var tris = new List<Vector3>();
            var tessTris = tess.Elements;
            for (int i = 0; i < tess.ElementCount * 3; i += 3)
            {
                tris.Add(new Vector3(
                    tessTris[i], tessTris[i + 1], tessTris[i + 2]));
            }

            return (verts, tris);
        }

    }
}
