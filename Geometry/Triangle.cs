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
using System.Threading.Tasks;
using OpenTK;

namespace Geometry
{
    /// <summary>
    /// Class describing a triangle.
    /// </summary>
    public class Triangle
    {
        private Vector3 a, b, c;
        private Plane plane = null;
        private Plane[] edgePlanes = null;

        public Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            this.a = a;
            this.b = b;
            this.c = c;
        }

        public IEnumerable<Vector3> Vertices
        {
            get
            {
                yield return a;
                yield return b;
                yield return c;
            }
        }

        public Vector3 A
        {
            get { return a; }
        }
        public Vector3 B
        {
            get { return b; }
        }
        public Vector3 C
        {
            get { return c; }
        }

        /// <summary>
        /// Get the plane on which the triangle resides
        /// </summary>
        public Plane Plane
        {
            get
            {
                if (plane == null)
                {
                    Vector3 normal = Vector3.Cross(b - a, c - b);
                    normal.Normalize();
                    plane = new Plane(normal, a);
                }
                return plane;
            }
        }

        public bool IsPointInTriangle(Vector3 point)
        {
            foreach (Plane p in EdgePlanes)
            {
                if (p.Distance(point) < 0)
                {
                    // Outside of triangle, no intersection
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Iterate over planes intersecting the triangle's edges, and perpendicular to the plane of the triangle.
        /// The normals for the plane point to the inside of the triangle.
        /// </summary>
        public IEnumerable<Plane> EdgePlanes
        {
            get
            {
                if (edgePlanes == null)
                {
                    edgePlanes = new Plane[3];
                    edgePlanes[0] = ComputeEdgePlane(a, b, Plane.Normal);
                    edgePlanes[1] = ComputeEdgePlane(b, c, Plane.Normal);
                    edgePlanes[2] = ComputeEdgePlane(c, a, Plane.Normal);
                }
                for (int i = 0; i < 3; i++)
                {
                    yield return edgePlanes[i];
                }
            }
        }

        private Plane ComputeEdgePlane(Vector3 p1, Vector3 p2, Vector3 normal)
        {
            Vector3 edgeNormal = Vector3.Cross(p1 - p2, normal);
            edgeNormal.Normalize();
            return new Plane(edgeNormal, p1);
        }
    }
}
