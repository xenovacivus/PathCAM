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
using System.Drawing;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using ClipperLib;
using TriangleNet.Geometry;


namespace Geometry
{
    using Path = List<IntPoint>;
    using Paths = List<List<IntPoint>>;
    public class Slice
    {
        private Plane plane;

        // For converting to/from 2D polygons
        private float scale = 1000000;
        private Matrix4 transform;
        private Matrix4 inverseTransform;
        private PolyTree polyTree;

        public Slice(TriangleMesh mesh, Plane plane)
        {
            //GL.Disable(EnableCap.Lighting);
            //GL.LineWidth(2);
            //GL.Begin(PrimitiveType.Lines);
            //float height = 0;

            // Slice at 3 levels and combine all segments - this obviates dealing with triangles that are exactly on the plane.
            for (int i = -1; i <= 1; i++)
            {
                Vector3 offset = plane.Normal * 0.0001f * (float)i;
                LineHandler lineHandler = new LineHandler(0.0f);
                Plane testPlane = new Plane(plane.Normal, plane.Point + offset);
                foreach (Triangle t in mesh.Triangles)
                {
                    var intersect = new TrianglePlaneIntersect(t, testPlane);
                    if (intersect.Intersects)
                    {
                        lineHandler.AddSegment(intersect.PointA, intersect.PointB);
                        //GL.Color3(Color.Blue);
                        //GL.Vertex3(intersect.PointA + new Vector3(0, 0, height + .01f));
                        //GL.Color3(Color.Red);
                        //GL.Vertex3(intersect.PointB + new Vector3(0, 0, height + .01f));
                    }
                    else if (intersect.all_intersect && Vector3.Dot(t.Plane.Normal, testPlane.Normal) > 0.5f)
                    {
                        // Entire triangle intersects
                        // Add all the triangle edges (TODO: clean this up...)
                        List<Vector3> vertices = new List<Vector3>(t.Vertices);
                        for (int a = 0; a < 3; a++)
                        {
                            Vector3 v1 = vertices[a];
                            Vector3 v2 = vertices[(a + 1) % 3];
                            lineHandler.AddSegment(v1, v2);
                        }
                    }
                }
                if (this.polyTree == null)
                {
                    Init(lineHandler.GetOuterLoops(), plane);
                }
                else
                {
                    Slice s = new Slice(lineHandler.GetOuterLoops(), plane);
                    this.Union(s);
                }
            }
            //GL.End();
            //GL.Enable(EnableCap.Lighting);
            //GL.LineWidth(1);
        }

        public Slice(Slice fromSlice)
        {
            this.plane = fromSlice.plane;
            this.transform = fromSlice.transform;
            this.inverseTransform = fromSlice.inverseTransform;
            this.polyTree = fromSlice.polyTree;
        }

        public Slice(IEnumerable<LineStrip> fromLines, Plane plane)
        {
            Init(fromLines, plane);
        }

        /// <summary>
        /// Create a slice from an open path with the given width
        /// </summary>
        /// <param name="path"></param>
        /// <param name="width"></param>
        /// <param name="plane"></param>
        public Slice (LineStrip path, float width, Plane plane, bool closed = false)
        {
            this.plane = plane;
            transform = plane.CreateMatrix();
            transform = Matrix4.Mult(transform, Matrix4.CreateScale(scale));
            inverseTransform = Matrix4.Invert(transform);
            polyTree = new PolyTree();

            ClipperOffset co = new ClipperOffset();
            co.ArcTolerance = scale * 0.0001f;
            if (closed)
            {
                co.AddPath(LineStripToPolygon(path), JoinType.jtRound, EndType.etClosedLine);
            }
            else
            {
                co.AddPath(LineStripToPolygon(path), JoinType.jtRound, EndType.etOpenRound);
            }
            co.Execute(ref this.polyTree, scale * width / 2.0f);
        }
        
        private void Init(IEnumerable<LineStrip> lines, Plane plane, PolyFillType pft = PolyFillType.pftEvenOdd)
        {
            transform = plane.CreateMatrix();
            transform = Matrix4.Mult(transform, Matrix4.CreateScale(scale));
            inverseTransform = Matrix4.Invert(transform);
            this.plane = plane;
            polyTree = GetPolyTree(lines, pft);
        }

        public Plane Plane
        {
            get { return plane; }
        }

        private Path LineStripToPolygon(LineStrip line)
        {
            Path polygon = new Path();
            foreach (var point in line.Vertices)
            {
                Vector2 result = Vector3.Transform(point, transform).Xy;
                polygon.Add(new IntPoint((long)Math.Round(result.X), (long)Math.Round(result.Y)));
            }
            return polygon;
        }

        private PolyTree GetPolyTree(IEnumerable<LineStrip> lines, PolyFillType pft)
        {
            Paths polygons = new Paths();
            Clipper c = new Clipper();
            c.Clear();
            
            foreach (var line in lines)
            {
                polygons.Add(LineStripToPolygon(line));
            }

            polygons = Clipper.SimplifyPolygons(polygons, pft);
            c.AddPaths(polygons, PolyType.ptSubject, true);
            PolyTree tree = new PolyTree();
            c.Execute(ClipType.ctUnion, tree);
            return tree;
        }

        public void Offset(float offset)
        {
            Paths polygons = Clipper.ClosedPathsFromPolyTree(polyTree);
            ClipperOffset co = new ClipperOffset();
            co.ArcTolerance = scale * .0001f;
            co.AddPaths(polygons, JoinType.jtRound, EndType.etClosedPolygon);
            polyTree = new PolyTree();
            Paths offsetPaths = new Paths();
            co.Execute(ref offsetPaths, scale * offset);
            offsetPaths = Clipper.CleanPolygons(offsetPaths, scale * .0001f);
            polyTree = PolygonsToPolyTree(offsetPaths);
        }

        public IEnumerable<LineStrip> GetLines(LineType type)
        {
            PolyNode n = polyTree.GetFirst();
            while (null != n)
            {
                bool hole = n.IsHole;
                if (type == LineType.All || (hole && type == LineType.Hole) || (!hole && type == LineType.Outside))
                {
                    yield return LineStripFromPolygon(n.Contour);
                }
                n = n.GetNext();
            }
        }

        public enum LineType
        {
            Hole,
            Outside,
            All,
        }

        private LineStrip LineStripFromPolygon(Path polygon)
        {
            LineStrip line = new LineStrip();
            foreach (IntPoint point in polygon)
            {
                line.Append(TransformTo3D(point));
            }
            return line;
        }

        public float Area()
        {
            float area = 0;
            foreach (var poly in Clipper.PolyTreeToPaths(polyTree))
            {
                area += (float)Clipper.Area(poly);
            }
            return area;
        }

        public bool Contains(Slice other)
        {
            // To contain another slice:
            // 1. Area of the union must be the same
            // 2. Area of this - other must be less



            float thisArea = this.Area();
            
            Paths otherPolygons = Clipper.PolyTreeToPaths(other.polyTree);
            Paths thesePolygons = Clipper.PolyTreeToPaths(polyTree);
            Slice s = new Slice(this);
            Clipper c = new Clipper();
            c.Clear();
            c.AddPaths(thesePolygons, PolyType.ptSubject, true);
            c.AddPaths(otherPolygons, PolyType.ptClip, true);
            s.polyTree = new PolyTree();
            c.Execute(ClipType.ctUnion, s.polyTree);
            float area_union = s.Area();
            if (area_union > thisArea)
            {
                return false;
            }
            return true;
            //c.Clear();
            //c.AddPaths(thesePolygons, PolyType.ptSubject, true);
            //c.AddPaths(otherPolygons, PolyType.ptClip, true);
            //s.polyTree = new PolyTree();
            //c.Execute(ClipType.ctDifference, s.polyTree);
            //float area_difference = s.Area();
            //if (area_difference < thisArea)
            //{
            //    return true;
            //}
            //return false;
        }

        /// <summary>
        /// Get a list of triangles which will fill the area described by the slice
        /// </summary>
        public IEnumerable<Triangle> Triangles()
        {
            
            TriangleNet.Behavior behavior = new TriangleNet.Behavior();
            behavior.ConformingDelaunay = true;

            foreach (var poly in IndividualPolygons())
            {
                PolyNode node = polyTree.GetFirst();
                InputGeometry geometry = new InputGeometry();
                while (node != null)
                {
                    var offset = geometry.Points.Count();
                    var index = 0;
                    foreach (IntPoint point in node.Contour)
                    {
                        geometry.AddPoint(point.X, point.Y);
                        if (index > 0)
                        {
                            geometry.AddSegment(index - 1 + offset, index + offset);
                        }
                        index++;
                    }
                    geometry.AddSegment(index - 1 + offset, offset);

                    if (node.IsHole)
                    {
                        // To describe a hole, AddHole must be called with a location inside the hole.
                        IntPoint last = new IntPoint(0, 0);
                        bool lastKnown = false;
                        double longest = 0;
                        IntPoint longestAlong = new IntPoint(0, 0);
                        IntPoint from = new IntPoint(0, 0);
                        foreach (IntPoint point in node.Contour)
                        {
                            if (lastKnown)
                            {
                                IntPoint along = new IntPoint(point.X - last.X, point.Y - last.Y);
                                double length = Math.Sqrt(along.X * along.X + along.Y * along.Y);
                                if (length > longest)
                                {
                                    longest = length;
                                    longestAlong = along;
                                    from = last;
                                }
                            }
                            last = point;
                            lastKnown = true;
                        }
                        if (longest > 0)
                        {
                            double perpendicularX = ((double)longestAlong.Y * (double)scale * 0.001d) / longest;
                            double perpendicularY = -((double)longestAlong.X * (double)scale * 0.001d) / longest;
                            geometry.AddHole(perpendicularX + from.X + longestAlong.X / 2.0d,
                                perpendicularY + from.Y + longestAlong.Y / 2.0d);
                        }
                        else
                        {
                        }
                    }
                    node = node.GetNext();
                }

                if (geometry.Points.Count() > 0)
                {
                    var mesh = new TriangleNet.Mesh(behavior);
                    mesh.Triangulate(geometry);
                    mesh.Renumber();
                    foreach (Triangle t in this.GetMeshTriangles(mesh))
                    {
                        yield return t;
                    }
                }
            }
        }

        private Vector3 TransformTo3D(IntPoint point)
        {
            return Vector3.Transform(new Vector3(point.X, point.Y, 0), inverseTransform);
        }

        private IEnumerable<Triangle> GetMeshTriangles(TriangleNet.Mesh mesh)
        {
            List<Vector3> vertices = new List<Vector3>();
            foreach (var vertex in mesh.Vertices)
            {
                vertices.Add(TransformTo3D(new IntPoint((long)vertex.X, (long)vertex.Y)));
            }

            foreach (var triangle in mesh.Triangles)
            {
                yield return new Triangle(vertices[triangle.P0], vertices[triangle.P1], vertices[triangle.P2]);
            }
        }

        public void RemoveHoles(float maxPerimiter)
        {
            Paths keep = new Paths();
            PolyNode node = polyTree.GetFirst();
            while (node != null)
            {
                if (node.IsHole && node.ChildCount == 0)
                {
                    var line = LineStripFromPolygon(node.Contour);
                    float length = line.Length(LineStrip.Type.Closed);
                    if (length < maxPerimiter)
                    {
                        // Remove it
                    }
                    else
                    {
                        keep.Add(node.Contour);
                    }
                }
                else
                {
                    keep.Add(node.Contour);
                }
                node = node.GetNext();
            }
            Clipper c = new Clipper();
            c.Clear();
            c.AddPaths(keep, PolyType.ptSubject, true);
            polyTree = new PolyTree();
            c.Execute(ClipType.ctUnion, polyTree);
        }

        /// <summary>
        /// Get all the polygons which contain holes
        /// </summary>
        /// <returns></returns>
        public Slice PolygonsWithHoles()
        {
            Slice s = new Slice(this);
            PolyNode n = polyTree.GetFirst();
            Paths polygons = new Paths();
            while (null != n)
            {
                if (n.IsHole)
                {
                    if (!polygons.Contains(n.Parent.Contour))
                    {
                        polygons.Add(n.Parent.Contour);
                    }
                    polygons.Add(n.Contour);
                }
                n = n.GetNext();
            }

            s.polyTree = PolygonsToPolyTree(polygons);
            return s;
        }

        public Slice PolygonsWithoutHoles()
        {
            Slice s = new Slice(this);
            PolyNode n = polyTree.GetFirst();
            Paths polygons = new Paths();
            while (null != n)
            {
                if (!n.IsHole && n.ChildCount == 0)
                {
                    polygons.Add(n.Contour);
                }
                n = n.GetNext();
            }

            s.polyTree = PolygonsToPolyTree(polygons);
            return s;
        }

        public IEnumerable<Slice> IndividualPolygons()
        {
            PolyNode n = polyTree.GetFirst();
            Paths polygons = new Paths();
            while (null != n)
            {
                if (!n.IsHole && polygons.Count > 0)
                {
                    Slice s = new Slice(this);
                    s.polyTree = PolygonsToPolyTree(polygons);
                    yield return s;
                    polygons = new Paths();
                }
                polygons.Add(n.Contour);
                n = n.GetNext();
            }
            if (polygons.Count > 0)
            {
                Slice s = new Slice(this);
                s.polyTree = PolygonsToPolyTree(polygons);
                yield return s;
            }
        }


        private PolyTree PolygonsToPolyTree(Paths polygons)
        {
            var tree = new PolyTree();
            Clipper c = new Clipper();
            c.Clear();
            c.AddPaths(polygons, PolyType.ptSubject, true);
            c.Execute(ClipType.ctUnion, tree);
            return tree;
        }

        public void Subtract(Slice other)
        {
            Clipper c = new Clipper();
            c.Clear();
            c.AddPaths(Clipper.PolyTreeToPaths(polyTree), PolyType.ptSubject, true);
            c.AddPaths(Clipper.PolyTreeToPaths(other.polyTree), PolyType.ptClip, true);

            polyTree = new PolyTree();
            c.Execute(ClipType.ctDifference, polyTree);
        }

        public void SubtractFrom(Slice other)
        {
            Clipper c = new Clipper();
            c.Clear();
            c.AddPaths(PolyTreeToPolygons(other.polyTree), PolyType.ptSubject, true);
            c.AddPaths(PolyTreeToPolygons(polyTree), PolyType.ptClip, true);
            
            polyTree = new PolyTree();
            c.Execute(ClipType.ctDifference, polyTree);
        }

        private Paths PolyTreeToPolygons(PolyTree tree)
        {
            Paths p = new Paths();
            PolyNode n = tree.GetFirst();
            while (null != n)
            {
                p.Add(n.Contour);
                n = n.GetNext();
            }
            return p;
        }

        #region LineHandler

        /// <summary>
        /// Routines for dealing with a bunch of lines effeciently
        /// </summary>
        private class LineHandler
        {
            private class VectorWrapper
            {
                public Vector3 vector;
                public List<Segment> used_as_a = new List<Segment>();
                public List<Segment> used_as_b = new List<Segment>();
                public VectorWrapper(Vector3 vector)
                {
                    this.vector = vector;
                }
                public override string ToString()
                {
                    return String.Format("{0}; count(a) = {1}, count(b)={2}", vector.ToString(), used_as_a.Count, used_as_b.Count);
                }
            }
            private class Segment
            {
                internal VectorWrapper a;
                internal VectorWrapper b;
                bool normal_memoized = false;
                private Vector3 normal;
                public Vector3 Normal
                {
                    get
                    {
                        if (!normal_memoized)
                        {
                            normal_memoized = true;
                            normal = b.vector - a.vector;
                            normal.Normalize();
                        }
                        return normal;
                    }
                }
                public override string ToString()
                {
                    return String.Format("{0}, {1}", a.vector.ToString(), b.vector.ToString());
                }
            }

            // All units are in thousandths of an inch
            // Floats have a precision of about 7 digits.
            // An epsilion value of 0.001f will be valid for about +-100 inches
            private float epsilon = 0.001f;
            private List<VectorWrapper> vectors = new List<VectorWrapper>();
            private List<Segment> segments = new List<Segment>();

            public LineHandler(float epsilon = 0.01f)
            {
                this.epsilon = epsilon;
            }

            public void AddSegment (Vector3 a, Vector3 b)
            {
                var va = FindVectorWrapper(a);
                var vb = FindVectorWrapper(b);
                var s = new Segment(){a = va, b = vb};
                segments.Add(s);
                va.used_as_a.Add(s);
                vb.used_as_b.Add(s);
            }

            private VectorWrapper FindVectorWrapper(Vector3 v)
            {
                //int index = vectors.FindIndex(vectorwrapper => (vectorwrapper.vector - v).Length <= epsilon);
                int index = vectors.FindIndex(vectorwrapper => vectorwrapper.vector == v);
                if (index < 0)
                {
                    vectors.Add(new VectorWrapper(v));
                    index = vectors.Count-1;
                }
                return vectors[index];
            }

            /// <summary>
            /// Convert the internal data to a list of the largest possible contiguous loops.
            /// NOTE: This is destructive!
            /// </summary>
            /// <returns></returns>
            public List<LineStrip> GetOuterLoops()
            {
                List<LineStrip> loops = new List<LineStrip>();

                foreach (Segment segment in segments)
                {
                    LineStrip loop = FindLargestLoop(segment);
                    if (loop != null)
                    {
                        loops.Add(loop);
                    }
                }
                return loops;
            }

            /// <summary>
            /// Measure the counter-clockwise angle around the normal in radians from one vector to another.
            /// </summary>
            /// <param name="one"></param>
            /// <param name="another"></param>
            /// <param name="normal"></param>
            /// <returns></returns>
            private static float Angle(Vector3 one, Vector3 another, Vector3 normal)
            {
                float inv_cos = Vector3.Dot(one, another);
                float inv_sin = Vector3.Dot(Vector3.Cross(one, another), normal);

                float angle = 0;
                if (Math.Abs(inv_cos) > Math.Abs(inv_sin))
                {
                    if (inv_cos > 0)
                    {
                        // Between -Pi/2 and Pi/2
                        angle = (float)Math.Asin(inv_sin);
                    }
                    else
                    {
                        angle = OpenTK.MathHelper.Pi - (float)Math.Asin(inv_sin);
                    }
                }
                else
                {
                    // Determine the quadrant
                    if (inv_sin > 0)
                    {
                        // Angle is between 0 and Pi
                        angle = (float)Math.Acos(inv_cos);
                    }
                    else
                    {
                        // Angle is between Pi and 2*Pi
                        angle = (float)Math.Acos(-inv_cos) + OpenTK.MathHelper.Pi;
                    }
                }
                if (angle < 0)
                {
                    angle += OpenTK.MathHelper.TwoPi;
                }
                return angle;
            }
            
            
            //private float height = .050f;
            private LineStrip FindLargestLoop(Segment start)
            {
                //height += 0.050f;
                Vector3 normal = new Vector3 (0, 0, 1);

                Segment next = start;

                List<Segment> seen = new List<Segment>();

                while (!seen.Contains(next))
                {
                    //GL.Disable(EnableCap.Lighting);
                    //GL.LineWidth(3);
                    //GL.Begin(PrimitiveType.Lines);
                    //GL.Color3(Color.Blue);
                    //GL.Vertex3(next.a.vector.X, next.a.vector.Y, height);
                    //GL.Color3(Color.LightGreen);
                    //height += .010f;
                    //GL.Vertex3(next.b.vector.X, next.b.vector.Y, height);
                    //GL.End();
                    //GL.LineWidth(1);
                    //GL.Enable(EnableCap.Lighting);

                    seen.Add(next);
                    Segment best = null;
                    float largestAngle = 0;
                    foreach (Segment s in next.b.used_as_a)
                    {
                        float angle = Angle(-s.Normal, next.Normal, normal);
                        if (angle > largestAngle)
                        {
                            largestAngle = angle;
                            best = s;
                        }
                    }
                    if (best == null)
                    {
                        // No loops
                        return null;
                    }
                    
                    // Destructive: remove references to this element so it's not searched again.
                    // Note: only need to remove forward links (from used_as_a).  The links from
                    // used_as_b could be cleared too, but it's not necessary for the algorithm.
                    next.b.used_as_a.Clear();
                    //next.b.used_as_b.Clear();

                    next = best;
                }
                // Remove all up to the first matched index
                int index = seen.IndexOf(next);
                seen.RemoveRange(0, index);

                LineStrip loop = new LineStrip();
                foreach (Segment seg in seen)
                {
                    loop.Append(seg.a.vector);
                }

                return loop;
            }
        }

        #endregion

        public Slice GetOutsidePairs()
        {
            return GetPairs(true);
        }

        public Slice GetInsidePairs()
        {
            return GetPairs(false);
        }

        private Slice GetPairs(bool outside)
        {
            Slice s = new Slice(this);
            PolyNode n = polyTree.GetFirst();
            Paths polygons = new Paths();
            while (null != n)
            {
                int depth = 0;
                PolyNode parent = n.Parent;
                while (parent != null)
                {
                    depth++;
                    parent = parent.Parent;
                }
                int test = (depth - 1) % 4;
                if ((outside && test < 2) || (!outside && test >= 2))
                {
                    polygons.Add(n.Contour);
                }
                n = n.GetNext();
            }

            s.polyTree = PolygonsToPolyTree(polygons);
            return s;
        }

        public void Union(Slice other)
        {
            Clipper c = new Clipper();
            c.Clear();
            c.AddPaths(Clipper.PolyTreeToPaths(polyTree), PolyType.ptSubject, true);
            c.AddPaths(Clipper.PolyTreeToPaths(other.polyTree), PolyType.ptClip, true);

            polyTree = new PolyTree();
            c.Execute(ClipType.ctUnion, polyTree);
        }
    }
}
