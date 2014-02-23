using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenTK;

namespace Geometry
{
    public class AnalyzedTriangleMesh : TriangleMesh
    {

        public AnalyzedTriangleMesh(TriangleMesh mesh)
        {
            foreach (Triangle t in mesh.Triangles)
            {
                this.AddTriangle(t);
            }
        }

        public List<TriangleMesh> Analyze()
        {
            List<TriangleMesh> meshes = new List<TriangleMesh>();
            // While there is a triangle on the top:

            // 1. Find a triangle on the top, remove it
            // 2. Find all connected triangles, remove them
            // 3. Add all removed triangles to a new mesh

            // Find

            var t = triangles;

            List<TriangleIndices> ignore = new List<TriangleIndices>();

            Func<Triangle, bool> TopTriangleCriteria = triangle => Vector3.Dot(triangle.Plane.Normal, Vector3.UnitZ) == 1 && triangle.A.Z == MaxPoint.Z;
            Func<Triangle, bool> BottomTriangleCriteria = triangle => Vector3.Dot(triangle.Plane.Normal, Vector3.UnitZ) == -1 && triangle.A.Z == MinPoint.Z;
            Func<Triangle, bool> PocketCriteria1 = triangle => !TopTriangleCriteria(triangle) && Vector3.Dot(triangle.Plane.Normal, Vector3.UnitZ) > 0;
            Func<Triangle, bool> PocketCriteria2 = triangle => !TopTriangleCriteria(triangle) && Vector3.Dot(triangle.Plane.Normal, Vector3.UnitZ) >= 0;


            while (t.Count > 0)
            {
                var triangleIndices = t[0];
                var triangle = new Triangle(vertices[triangleIndices.a], vertices[triangleIndices.b], vertices[triangleIndices.c]);
                var dot = Vector3.Dot(triangle.Plane.Normal, Vector3.UnitZ);
                Func<float, bool> dotTestTop = dot_value => dot_value == 1;
                Func<float, bool> dotTestBottom = dot_value => dot_value == -1;
                if (TopTriangleCriteria(triangle))
                {
                    var connected = GetConnectedTriangles(triangleIndices, ignore, TopTriangleCriteria);
                    ignore.AddRange(connected);
                    TriangleMesh mesh = new TriangleMesh();
                    foreach (var indices in connected)
                    {
                        t.Remove(indices);
                        var newTriangle = new Triangle(vertices[indices.a], vertices[indices.b], vertices[indices.c]);
                        mesh.AddTriangle(newTriangle);
                    }
                    //meshes.Add(mesh);
                }
                else if (BottomTriangleCriteria(triangle))
                {
                    var connected = GetConnectedTriangles(triangleIndices, ignore, BottomTriangleCriteria);
                    ignore.AddRange(connected);
                    TriangleMesh mesh = new TriangleMesh();
                    foreach (var indices in connected)
                    {
                        t.Remove(indices);
                        var newTriangle = new Triangle(vertices[indices.a], vertices[indices.b], vertices[indices.c]);
                        mesh.AddTriangle(newTriangle);
                    }
                    //meshes.Add(mesh);
                }
                else if (PocketCriteria1(triangle))
                {
                    var connected = GetConnectedTriangles(triangleIndices, ignore, PocketCriteria2);
                    ignore.AddRange(connected);
                    TriangleMesh mesh = new TriangleMesh();
                    foreach (var indices in connected)
                    {
                        t.Remove(indices);
                        var newTriangle = new Triangle(vertices[indices.a], vertices[indices.b], vertices[indices.c]);
                        mesh.AddTriangle(newTriangle);
                    }
                    meshes.Add(mesh);
                }
                else
                {
                    //ignore.Add(t[0]);
                    t.RemoveAt(0);
                }
            }

            return meshes;
        }

        /// <summary>
        /// Get all connected triangles that satisfy a certain criteria
        /// </summary>
        /// <returns></returns>
        public TriangleIndices[] GetConnectedTriangles(TriangleIndices triangleIndices, List<TriangleIndices> ignoreIndices, Func<Triangle, bool> dotCriteria)
        {
            List<TriangleIndices> connected = new List<TriangleIndices>();
            connected.Add(triangleIndices);
            //ignoreIndices.Add(triangleIndices);

            foreach (var edge in triangleIndices.edges)
            {
                foreach (var otherindex in edge.triangles)
                {
                    if (ignoreIndices.Contains(otherindex) || connected.Contains(otherindex))
                    {
                        // Don't need to look at this one
                    }
                    else
                    {
                        var triangle = new Triangle(vertices[otherindex.a], vertices[otherindex.b], vertices[otherindex.c]);
                        var dot = Vector3.Dot(triangle.Plane.Normal, Vector3.UnitZ);
                        if (dotCriteria(triangle))
                        {
                            // Satisfies criteria, keep the triangle
                            List<TriangleIndices> newIgnore = new List<TriangleIndices>();
                            newIgnore.AddRange(ignoreIndices);
                            newIgnore.AddRange(connected);
                            var moreconnected = GetConnectedTriangles(otherindex, newIgnore, dotCriteria);
                            //ignoreIndices.AddRange(moreconnected);
                            connected.AddRange(moreconnected);
                        }
                    }
                }
            }

            return connected.ToArray();
        }
    }
}
