/*
 * PathCAM - Toolpath generation software for CNC manufacturing machines
 * Copyright (C) 2013  Benjamin R. Porter https://github.com/xenovacivus
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see [http://www.gnu.org/licenses/].
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenTK;
using System.Collections;

namespace Geometry
{
    /// <summary>
    /// Class for building and storing a mesh of triangles.  Each triangle contains pointers to adjacent triangles
    /// through the edges, and edges can also be enumerated.  All operations are safe at any point, and iterating
    /// over edges and triangles is guarenteed accurate and complete if no add or clean operation occurs.
    /// A transformation can be applied/modified which will apply to all vertices and cached values (min and max point).
    /// The transformation will always be applied to the original locations of the vertices.
    /// </summary>
    public class TriangleMesh
    {
        private float epsilon = 0.0001f; // Distance between vertices considered to be unique - set to a value valid for inches.
        protected List<Vector3> vertices;
        private List<Vector3> verticesTransformed; // List of vertices with transformation applied
        private Vector3 minPoint = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        private Vector3 maxPoint = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        protected List<TriangleIndices> triangles;
        private List<Edge> segments;
        private Matrix4 transformation = Matrix4.Identity;

        public class TriangleIndices
        {
            public int a, b, c;
            public List<Edge> edges = new List<Edge>(3);
            public int LongestEdgeIndex()
            {
                float longest = 0;
                int index = -1;
                for (int i = 0; i < 3; i++)
                {
                    float length = edges[i].Length();
                    if (length > longest || index < 0)
                    {
                        index = i;
                        longest = length;
                    }
                }
                return index;
            }
            public override string ToString()
            {
                return string.Format("{0}, {1}, {2}", a, b, c);
            }
        }
        public class Edge
        {
            internal TriangleMesh parentMesh;
            internal int a, b;
            internal List<TriangleIndices> triangles = new List<TriangleIndices>();

            public Edge(TriangleMesh parent)
            {
                this.parentMesh = parent;
            }
            
            internal bool Matches(int a, int b)
            {
                return (this.a == a && this.b == b) || (this.b == a && this.a == b);
            }

            internal float Length()
            {
                Vector3 v1 = parentMesh.verticesTransformed[a];
                Vector3 v2 = parentMesh.verticesTransformed[b];
                return (v1 - v2).Length;
            }

            public LineSegment LineSegment
            {
                get { return new LineSegment(parentMesh.verticesTransformed[a], parentMesh.verticesTransformed[b]); }
            }

            public IEnumerable<Triangle> Triangles
            {
                get
                {
                    var num = triangles.Count;
                    while (--num >= 0)
                    {
                        var triangleIndices = triangles[num];
                        yield return new Triangle(parentMesh.verticesTransformed[triangleIndices.a], parentMesh.verticesTransformed[triangleIndices.b], parentMesh.verticesTransformed[triangleIndices.c]);
                    }
                }
            }

            public IEnumerable<Vector3> Vertices
            {
                get
                {
                    yield return parentMesh.verticesTransformed[a];
                    yield return parentMesh.verticesTransformed[b];
                }
            }
        }

        public TriangleMesh()
        {
            vertices = new List<Vector3>();
            verticesTransformed = new List<Vector3>();
            triangles = new List<TriangleIndices>();
            segments = new List<Edge>();
        }

        /// <summary>
        /// Ensure all triangles have some non-zero area and that each edge connects to exactly two triangles.
        /// Returns false if the above conditions are not true and can't be repaired.
        /// </summary>
        /// <returns></returns>
        public bool Clean()
        {
            foreach (Edge e in Edges)
            {
                if (e.Vertices.Count() != 2)
                {
                    return false;
                }
            }

            for (int i = 0; i < triangles.Count; i++)
            {
                var tri1 = triangles[i];
                int longestEdgeIndex = tri1.LongestEdgeIndex();
                var edge = tri1.edges[longestEdgeIndex];

                int[] vertexIndices1 = new int[] { tri1.a, tri1.b, tri1.c };
                Vector3 oppositePoint = vertices[vertexIndices1[(longestEdgeIndex + 2) % 3]];

                // Is the triangle really small, basically a line?
                // Note: use a test value of half epsilon to guarentee 
                if (edge.LineSegment.Distance(oppositePoint) < (epsilon / 2.0f))
                {
                    // If so, find the quad formed by the two attached triangles and flip the dividing line.
                    var tri2 = edge.triangles.First(t => t != tri1);

                    int splitEdgeIndex = tri2.edges.FindIndex(e => e == edge);

                    int[] vertexIndices2 = new int[] { tri2.a, tri2.b, tri2.c };


                    int a = vertexIndices2[(splitEdgeIndex + 1) % 3];
                    int b = vertexIndices2[(splitEdgeIndex + 2) % 3];
                    int c = vertexIndices2[(splitEdgeIndex + 3) % 3];
                    int d = vertexIndices1[(longestEdgeIndex + 2) % 3];

                    edge.a = b;
                    edge.b = d;

                    tri2.a = b;
                    tri2.b = c;
                    tri2.c = d;

                    tri1.a = d;
                    tri1.b = a;
                    tri1.c = b;

                    SetTriangleEdgePointers(tri2);
                    SetTriangleEdgePointers(tri1);
                }
            }
            return true;
        }

        /// <summary>
        /// Get or set the minimum distance required to distinguish unique vertices.
        /// </summary>
        public float Epsilon
        {
            get { return epsilon; }
            set { epsilon = value; }
        }

        public Matrix4 Transformation
        {
            get { return transformation; }
            set
            {
                transformation = value;
                // Update all the transformed vertices and min/max values.
                minPoint = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                maxPoint = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                var vertexCount = vertices.Count;
                while (--vertexCount >= 0)
                {
                    var vertex = Vector3.Transform(vertices[vertexCount], transformation);
                    verticesTransformed[vertexCount] = vertex;

                    minPoint.X = Math.Min(vertex.X, minPoint.X);
                    minPoint.Y = Math.Min(vertex.Y, minPoint.Y);
                    minPoint.Z = Math.Min(vertex.Z, minPoint.Z);

                    maxPoint.X = Math.Max(vertex.X, maxPoint.X);
                    maxPoint.Y = Math.Max(vertex.Y, maxPoint.Y);
                    maxPoint.Z = Math.Max(vertex.Z, maxPoint.Z);
                }
            }
        }

        /// <summary>
        /// Add a new triangle with vertices a, b, and c.
        /// Vertices must be specified in counter-clockwise order when viewed from front.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c)
        {
            var newTriangle = new TriangleIndices() { a = AddVertex(a), b = AddVertex(b), c = AddVertex(c) };
            if (newTriangle.a != newTriangle.b && newTriangle.b != newTriangle.c && newTriangle.c != newTriangle.a)
            {
                triangles.Add(newTriangle);
                var triangle = triangles[triangles.Count - 1];
                SetTriangleEdgePointers(triangle);
            }
            else
            {
                // Bad triangle = has colinear edges.
            }
        }

        private void SetTriangleEdgePointers(TriangleIndices tri)
        {
            // Remove the reference to this triangle from all edges
            foreach (var edge in tri.edges)
            {
                edge.triangles.RemoveAll(t => t == tri);
            }

            // Then rebuild the edges
            tri.edges.Clear();
            tri.edges.Add(AddEdge(tri.a, tri.b));
            tri.edges.Add(AddEdge(tri.b, tri.c));
            tri.edges.Add(AddEdge(tri.c, tri.a));
            tri.edges[0].triangles.Add(tri);
            tri.edges[1].triangles.Add(tri);
            tri.edges[2].triangles.Add(tri);
        }

        public IEnumerable<Edge> Edges
        {
            get
            {
                var num = segments.Count;
                while (--num >= 0)
                {
                    yield return segments[num];
                }
            }
        }

        private Edge AddEdge(int indexA, int indexB)
        {
            var index = segments.FindIndex(seg => seg.Matches(indexA, indexB));
            if (index < 0)
            {
                index = segments.Count;
                segments.Add(new Edge(this) { a = indexA, b = indexB });
            }
            return segments[index];
        }

        public void AddTriangle(Triangle tri)
        {
            AddTriangle(tri.A, tri.B, tri.C);
        }

        public int TriangleCount
        {
            get { return triangles.Count; }
        }

        /// <summary>
        /// Iterate over the triangles in the mesh.
        /// </summary>
        public IEnumerable<Triangle> Triangles
        {
            get
            {
                var num = triangles.Count;
                while (--num >= 0)
                {
                    var triangleIndices = triangles[num];
                    yield return new Triangle(
                        verticesTransformed[triangleIndices.a],
                        verticesTransformed[triangleIndices.b],
                        verticesTransformed[triangleIndices.c]);
                }
            }
        }

        public Vector3 MinPoint
        {
            get { return minPoint; }
        }

        public Vector3 MaxPoint
        {
            get { return maxPoint; }
        }

        private int AddVertex(Vector3 vertex)
        {
            var index = vertices.FindIndex(v => (v - vertex).Length < epsilon);
            if (index < 0)
            {
                index = vertices.Count;
                vertices.Add(vertex);
                var transformed = Vector3.Transform(vertex, transformation);
                verticesTransformed.Add(transformed);

                minPoint.X = Math.Min(minPoint.X, transformed.X);
                minPoint.Y = Math.Min(minPoint.Y, transformed.Y);
                minPoint.Z = Math.Min(minPoint.Z, transformed.Z);

                maxPoint.X = Math.Max(maxPoint.X, transformed.X);
                maxPoint.Y = Math.Max(maxPoint.Y, transformed.Y);
                maxPoint.Z = Math.Max(maxPoint.Z, transformed.Z);
            }
            return index;
        }
    }
}
