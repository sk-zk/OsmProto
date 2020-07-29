using LibTessDotNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using System.Linq;
using TruckLib.Model;
using System.Drawing;

namespace OsmProto
{
    static class ModelGenerator
    {
        /// <summary>
        /// Generates a prism from a given polygon.
        /// </summary>
        /// <param name="polygon">The points of the polygon, in order.</param>
        /// <param name="translation">Translation applied to the second base.</param>
        /// <returns></returns>
        public static Model GeneratePrism(List<Vector3> polygon, Vector3 translation, Color color)
        {
            // make sure the polygon is in counterclockwise order.
            // if it isn't, the side faces will generate with the wrong winding
            // and get culled.
            if (IsClockwise(polygon))
                polygon.Reverse();

            // tessellate polygon with libtess
            var (vertices, triangles) = Tessellator.TessellatePolygon(polygon);

            // create Prism3D model
            var model = new Model();
            var part = new Part("defaultpart");
            model.Parts.Add(part);
            model.Variants[0].Attributes.Add(new PartAttribute()
            {
                Tag = "visible",
                Value = 1
            });

            var piece = new Piece();
            part.Pieces.Add(piece);
            piece.UseTextureCoordinates = true;
            piece.BoundingBox.Start = new Vector3(-1, -1, -1);
            piece.BoundingBox.End = new Vector3(1, 1, 1);
            piece.BoundingBoxDiagonalSize = 3.4641016151f;

            // create first base
            CreateBottom();

            // create second base
            CreateTop();

            // create side faces
            CreateSides();

            // TODO: Check if I need sth like this or if leaving them all 0 works too
            model.BoundingBox.Start = new Vector3(-1, -1, -1);
            model.BoundingBox.End = new Vector3(1, 1, 1);
            model.BoundingBoxDiagonalSize = 3.4641016151f;

            return model;

            void CreateBottom()
            {
                foreach (var vert in vertices)
                    CreateVert(piece, vert, new Vector3(0, -1, 0), color);

                foreach (var tri in triangles)
                {
                    var mdlTri = new Triangle((ushort)tri.X, (ushort)tri.Y, (ushort)tri.Z);
                    piece.Triangles.Add(mdlTri);
                }
            }

            void CreateTop()
            {
                var topVerts = new List<Vertex>();
                foreach (var vert in piece.Vertices)
                {
                    var topVert = vert.Clone();
                    topVert.Position += translation;
                    topVert.Normal = new Vector3(0, 1, 0);
                    topVerts.Add(topVert);
                }
                piece.Vertices.AddRange(topVerts);

                var topTris = new List<Triangle>();
                var vertCount = vertices.Count;
                foreach (var tri in piece.Triangles)
                {
                    var mdlTri = new Triangle(
                        (ushort)(vertCount + tri.A),
                        (ushort)(vertCount + tri.B),
                        (ushort)(vertCount + tri.C)
                        );
                    // the tris of the second base face the other direction
                    // so the the vertex order of the tri has to be inverted
                    mdlTri.InvertOrder();
                    topTris.Add(mdlTri);
                }
                piece.Triangles.AddRange(topTris);
            }

            void CreateSides()
            {
                for (int i = 0; i < polygon.Count; i++)
                {
                    /* go through the contour and
                       create two triangles per side face:

                        A=top[i]  C=top[i+1]
                             v    v 
                             x----x
                             |   /|
                        tri1 |  / |
                             | /  | tri2
                             |/   |
                             x----x
                             ^    ^
                      B=bottom[i] D=bottom[i+1]

                      we also need to create new vertices rather than
                      sharing the base vertices because the models 
                      should have hard shading.
                    */

                    // to connect the last vertex to the first vertex
                    var nextIdx = (i >= polygon.Count - 1) ? 0 : i + 1;

                    // create all verts first because B and C can be shared
                    var a = polygon[i] + translation;
                    var b = polygon[i];
                    var c = polygon[nextIdx] + translation;
                    var d = polygon[nextIdx];

                    // not sure why I have to invert it but whatever
                    var faceNormal = -CalculateFaceNormal(a, b, c);

                    var aVertIdx = CreateVert(piece, a, faceNormal, color);
                    var bVertIdx = CreateVert(piece, b, faceNormal, color);
                    var cVertIdx = CreateVert(piece, c, faceNormal, color);
                    var dVertIdx = CreateVert(piece, d, faceNormal, color);

                    // tri 1
                    piece.Triangles.Add(new Triangle(
                        (ushort)aVertIdx,
                        (ushort)bVertIdx,
                        (ushort)cVertIdx));

                    // tri 2
                    piece.Triangles.Add(new Triangle(
                        (ushort)cVertIdx,
                        (ushort)bVertIdx,
                        (ushort)dVertIdx));
                }
            }
        }

        /// <summary>
        /// Adds a new vertex to a piece and returns its index.
        /// </summary>
        private static int CreateVert(Piece piece, Vector3 pos, Vector3 normal, Color color)
        {
            var idx = piece.Vertices.Count;
            var v = new Vertex(pos, normal);
            v.Color = color;
            v.TextureCoordinates = new List<Vector2>() { new Vector2(0, 0) };
            piece.Vertices.Add(v);
            return idx;
        }

        private static Vector3 CalculateFaceNormal(Vector3 a, Vector3 b, Vector3 c) =>
            Vector3.Normalize(Vector3.Cross(b - a, c - a));

        /// <summary>
        /// Checks if a polygon is in clockwise order. Ignores Y.
        /// </summary>
        /// <param name="polygon"></param>
        /// <returns></returns>
        private static bool IsClockwise(List<Vector3> polygon)
        {
            var sum = 0.0;
            for (int i = 0; i < polygon.Count - 1; i++)
            {
                var a = polygon[i];
                var b = polygon[i + 1];
                sum += (b.X - a.X) * (b.Z + a.Z);
            }
            return sum > 0.0;
        }
    }

    struct VecTriangle
    {
        public Vector3 A;
        public Vector3 B;
        public Vector3 C;

        public VecTriangle(Vector3 a, Vector3 b, Vector3 c)
        {
            A = a;
            B = b;
            C = c;
        }
    }
}
