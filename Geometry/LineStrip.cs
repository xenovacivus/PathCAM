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

namespace Geometry
{
    public class LineStrip
    {
        protected List<Vector3> vertices;

        public enum Type
        {
            Open,
            Closed,
        }

        public LineStrip()
        {
            vertices = new List<Vector3>();
        }

        public IEnumerable<LineSegment> Segments(Type type)
        {
            bool lastKnown = false;
            Vector3 last = Vector3.Zero;
            foreach (var point in vertices)
            {
                if (lastKnown)
                {
                    yield return new LineSegment(last, point);
                }
                last = point;
                lastKnown = true;
            }

            if (type == Type.Closed && lastKnown)
            {
                yield return new LineSegment(last, vertices[0]);
            }
        }

        public void Append(Vector3 vertex)
        {
            vertices.Add(vertex);
        }

        public List<Vector3> Vertices
        {
            get { return vertices; }
        }

        public IEnumerable<Vector3> PointsAlongLine(float distance, float toFirst, Type type = Type.Closed)
        {
            float toNext = toFirst;
            float travelled = 0.0f;
            foreach (var segment in Segments(type))
            {
                travelled += segment.Length;
                Vector3 normal = segment.A - segment.B;
                normal.Normalize();
                while (travelled > toNext)
                {
                    Vector3 point = segment.B + normal * (travelled - toNext);
                    yield return point;
                    toNext += distance;
                }
            }
        }

        public float Length(Type type = Type.Closed)
        {
            bool lastKnown = false;
            Vector3 lastVector = new Vector3(0, 0, 0);
            float length = 0.0f;
            foreach (Vector3 point in this.Vertices)
            {
                if (lastKnown)
                {
                    length += (lastVector - point).Length;
                }
                else
                {
                    lastKnown = true;
                }
                lastVector = point;
            }
            if (type == Type.Closed && lastKnown)
            {
                length += (lastVector - Vertices[0]).Length;
            }
            return length;
        }

        public void AddRange(IEnumerable<Vector3> list)
        {
            foreach (var point in list)
            {
                Append(point);
            }
        }
    }
}
