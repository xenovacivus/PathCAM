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
using Geometry;

namespace Geometry
{
    public class Polygon
    {
        private List<Vector3> vertices;

        public Polygon()
        {
            vertices = new List<Vector3>();
        }

        public void Add(Vector3 vertex)
        {
            vertices.Add(vertex);
        }

        public IEnumerable<Vector3> Vertices
        {
            get { return vertices; }
        }

        public Plane Plane
        {
            get
            {
                // TODO: memoize, forget if vertices change
                var normal = Vector3.Zero;
                var center = Vector3.Zero;
                var verts = vertices.Count;
                // Compute the normal
                for (int i = 0; i < verts; i++)
                {
                    var i1 = i;
                    var i2 = (i + 1) % verts;
                    var v1 = vertices[i];
                    var v2 = vertices[i2];
                    normal.X += (v1.Y - v2.Y) * (v1.Z + v2.Z);
                    normal.Y += (v1.Z - v2.Z) * (v1.X + v2.X);
                    normal.Z += (v1.X - v2.X) * (v1.Y + v2.Y);
                    center += v1;
                }
                center = center / verts;
                normal.Normalize();

                return new Plane(normal, center);
            }
        }

        // Ear clipping algorithm to separate a polygon into triangles
        // This should work on any list of triangles with no holes.
        public IEnumerable<Triangle> ToTriangles()
        {
            Plane p = Plane;

            if (vertices.Count == 3)
            {
            }

            while (vertices.Count >= 3)
            {
                int verts = vertices.Count;
                int i = 0;
                // Find an ear on the face, remove it
                for (i = 0; i < verts; i++)
                {
                    Vector3 v1 = vertices[i];
                    Vector3 v2 = vertices[(i + 1) % verts];
                    Vector3 v3 = vertices[(i + 2) % verts];
                    var tri = new Triangle(v1, v2, v3);

                    bool anyPointInPolygon = false;
                    foreach (var otherPoint in vertices)
                    {
                        if (otherPoint != v1 && otherPoint != v2 && otherPoint != v3 && tri.IsPointInTriangle(otherPoint))
                        {
                            anyPointInPolygon = true;
                            break;
                        }
                    }

                    // First check: see if any point in the original polygon is inside the new triangle
                    if (anyPointInPolygon)
                    {
                        // Can't use this triangle, move onto the next one
                    }
                    else
                    {
                        // Make sure the triangle points the right way.
                        if (Vector3.Dot(tri.Plane.Normal, p.Normal) > 0.9f)
                        {
                            yield return tri;
                            this.vertices.RemoveAt((i + 1) % verts);
                            break;
                        }
                        else
                        {
                        }
                    }
                }
                if (i == verts)
                {
                    // Got a bad face
                    break;
                }
            }
        }
    }
}
