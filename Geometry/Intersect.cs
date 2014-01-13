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
using OpenTK.Graphics.OpenGL;

namespace Geometry
{
    public class LineSegmentCircleIntersect
    {
        LineSegment intersectSegment;
        LineSegment beginNoIntersect;
        LineSegment endNoIntersect;
        private IntersectType intersectType = IntersectType.None;
        public enum IntersectType
        {
            Segment,
            None,
        }
        public LineSegmentCircleIntersect(LineSegment segment, Vector3 circleCenter, float circleRadius)
        {
            Vector3 a = segment.A;
            Vector3 b = segment.B;
            Vector3 c = circleCenter;

            // Compute a normal perpendicular to the line and pointing to the point
            Vector3 up = Vector3.Cross(segment.A - circleCenter, segment.A - segment.B); // This points up from the line
            Vector3 normal = Vector3.Cross(up, segment.A - segment.B);
            float distanceToLine = 0.0f;
            if (normal.Length > 0.0f)
            {
                distanceToLine = Math.Abs(new Plane(normal, segment.A).Distance(circleCenter));
            }

            if (distanceToLine >= circleRadius)
            {
                intersectType = IntersectType.None;
                return;
            }
            if (distanceToLine == float.NaN)
            {
            }
            
            float along = (float)Math.Sqrt(Math.Pow(circleRadius, 2) - Math.Pow(distanceToLine, 2));

            Plane plane = new Plane(b - a, a);

            float toA = 0.0f;
            float toB = plane.Distance(b);
            float toC = plane.Distance(c);

            float toP1 = toC - along;
            float toP2 = toC + along;

            if (toP2 < toA || toP1 > toB)
            {
                intersectType = IntersectType.None;
                return;
            }

            if (toP1 > toA)
            {
                beginNoIntersect = new LineSegment(a, a + toA * plane.Normal);
            }
            if (toP2 < toB)
            {
                endNoIntersect = new LineSegment(a + toP2 * plane.Normal, a + toB * plane.Normal);
            }

            toP1 = Math.Min(Math.Max(toP1, toA), toB);
            toP2 = Math.Min(Math.Max(toP2, toA), toB);

            intersectType = IntersectType.Segment;
            intersectSegment = new LineSegment(a + plane.Normal * toP1, a + plane.Normal * toP2);
        }

        public LineSegment IntersectSegment
        {
            get
            {
                return intersectSegment;
            }
        }
        public IntersectType type
        {
            get { return intersectType; }
        }
    }

    public class TrianglePlaneIntersect
    {
        private Vector3 pointA;
        private Vector3 pointB;
        private bool intersects = false;
        public bool all_intersect = false;
        public TrianglePlaneIntersect(Triangle triangle, Plane plane)
        {
            // Find the intersections between edges on the triangle and the plane.
            List<Vector3> vertices = new List<Vector3>(3);
            List<float> distances = new List<float>(3);
            List<Vector3> onPlane = new List<Vector3>();

            foreach (Vector3 v in triangle.Vertices)
            {
                vertices.Add(v);
                distances.Add(plane.Distance(v));
            }
            
            for (int i = 0; i < 3; i++)
            {
                float d1 = distances[i];
                float d2 = distances[(i + 1) % 3];
                float d3 = distances[(i + 2) % 3];
                Vector3 v1 = vertices[i];
                Vector3 v2 = vertices[(i + 1) % 3];
                Vector3 v3 = vertices[(i + 2) % 3];

                if (d1 * d2 < 0)
                {
                    // One point on the edge from D1 to D2
                    Vector3 intersect = new Vector3(v2 * d1 - v1 * d2) / (d1 - d2);
                    onPlane.Add(intersect);
                    if (d3 == 0)
                    {
                        // Other point is on D3
                        onPlane.Add(v3);
                        break;
                    }
                    else
                    {
                        if (d1 * d3 < 0)
                        {
                            // Intersect with v1 to v3
                            onPlane.Add(new Vector3(v3 * d1 - v1 * d3) / (d1 - d3));
                            break;
                        }
                        else if (d2 * d3 < 0)
                        {
                            // Intersect with v2 to v3
                            onPlane.Add(new Vector3(v3 * d2 - v2 * d3) / (d2 - d3));
                            break;
                        }
                        else
                        {
                            // how the heck would we get here?
                        }
                    }
                }
                if (d1 == 0 && d2 == 0)
                {
                    if (d3 == 0)
                    {
                        // Triangle intersects perfectly with the plane - need to return all 3 edges here?
                        all_intersect = true;
                        break;
                    }
                    else
                    {
                        onPlane.Add(v1);
                        onPlane.Add(v2);
                        break;
                    }
                }

                //if (d1 * d2 < 0)
                //{
                //    // Edge of the triangle crosses the plane, interpolate to get the intersection point
                //    Vector3 intersect = new Vector3(v2 * d1 - v1 * d2) / (d1 - d2);
                //    onPlane.Add(intersect);
                //}
            }

            if (onPlane.Count == 2)
            {
                pointA = onPlane[0];
                pointB = onPlane[1];
                intersects = true;
            }

            if (intersects)
            {
                // Check the vector direction - flip the vertices if necessary
                if (Vector3.Dot(Vector3.Cross(pointB - pointA, plane.Normal), triangle.Plane.Normal) < 0)
                {
                    pointA = onPlane[1];
                    pointB = onPlane[0];
                }
            }
        }
        
        public bool Intersects
        {
            get { return intersects; }
        }
        
        public Vector3 PointA
        {
            get { return pointA; }
        }
        
        public Vector3 PointB
        {
            get { return pointB; }
        }
    }

    public class TriangleRayIntersect
    {
        private Vector3 point;
        private bool intersects;

        public TriangleRayIntersect (Triangle triangle, Ray ray)
        {
            point = Vector3.Zero;
            intersects = false;
            
            float distance = triangle.Plane.Distance(ray);
            if (distance <= 0 || float.IsNaN(distance))
            {
                // Plane is behind the ray, no intersection
                return;
            }
            
            point = ray.Direction * distance + ray.Start;
            // Make sure the point is within the triangle
            foreach (Plane p in triangle.EdgePlanes)
            {
                if (p.Distance(point) < 0)
                {
                    // Outside of triangle, no intersection
                    return;
                }
            }
            intersects = true;
        }

        public Vector3 Point
        {
            get { return point; }
        }

        public bool Intersects
        {
            get { return intersects; }
        }
    }
}
