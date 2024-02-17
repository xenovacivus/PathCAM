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
    public class LineSegment
    {
        private Vector3 a;
        private Vector3 b;

        public LineSegment(Vector3 a, Vector3 b)
        {
            this.a = a;
            this.b = b;
        }

        public IEnumerable<Vector3> Points
        {
            get
            {
                yield return a;
                yield return b;
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

        public Vector3 Direction
        {
            get { return Vector3.Normalize((b - a)); }
        }

        public float Length
        {
            get { return (a - b).Length; }
        }

        /// <summary>
        /// Subtract another line segment from this one.
        /// It's assumed that the other segment lies on the same line as this one.
        /// </summary>
        /// <param name="other"></param>
        public List<LineSegment> Subtract(LineSegment other)
        {
            //Vector3 n = b - a;
            //n.Normalize();

            Plane plane = new Plane(b - a, a);
            float toB = plane.Distance(b);

            float toOtherA = plane.Distance(other.a);
            float toOtherB = plane.Distance(other.b);
            
            if (toOtherA > toOtherB)
            {
                float temp = toOtherA;
                toOtherA = toOtherB;
                toOtherB = temp;
            }
            else
            {
            }

            List<LineSegment> remainingSegments = new List<LineSegment>();

            // Check for no overlap
            if (toOtherA >= toB || toOtherB <= 0.0f)
            {
                remainingSegments.Add(new LineSegment(a, b));
                return remainingSegments;
            }

            if (toOtherA > 0)
            {
                Vector3 p1 = a;
                remainingSegments.Add(new LineSegment(a, a + plane.Normal * toOtherA)); 
            }

            if (toOtherB < toB)
            {
                remainingSegments.Add(new LineSegment(a + plane.Normal * toOtherB, b));
            }

            return remainingSegments;
        }

        public float Distance(Vector3 point)
        {
            // Compute a normal perpendicular to the line and pointing to the point
            Vector3 up = Vector3.Cross(a - point, a - b); // This points up from the line
            Vector3 normal = Vector3.Cross(up, a - b);
            float distanceToLine = 0.0f;
            if (normal.Length > 0.0f)
            {
                distanceToLine = Math.Abs(new Plane(normal, a).Distance(point));
            }

            Plane plane = new Plane(b - a, a);
            float toPoint = plane.Distance(point);
            float toB = plane.Distance(b);
            if (toPoint > 0.0f && toPoint < toB)
            {
                return distanceToLine;
            }
            return (float)Math.Min((b - point).Length, (a - point).Length);
        }

        public Vector3 ClosestPoint(Vector3 point)
        {
            Plane plane = new Plane(b - a, a);
            float toPoint = plane.Distance(point);
            float toB = plane.Distance(b);
            if (toPoint > 0.0f && toPoint < toB)
            {
                return plane.Normal * toPoint + a;
            }
            float toA = (a - point).Length;
            toB = (b - point).Length;
            if (toA < toB)
            {
                return a;
            }
            return b;
        }
    }
}
