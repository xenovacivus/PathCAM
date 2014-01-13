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
    public class Plane
    {
        private Vector3 normal;
        private Vector3 point;

        public Plane(Vector3 normal, Vector3 point)
        {
            this.normal = normal;
            this.point = point;
            this.normal.Normalize();  // Make sure the normal is correct
        }

        public Vector3 Normal
        {
            get { return normal; }
            set { normal = value; }
        }

        public Vector3 Point
        {
            get { return point; }
            set { point = value; }
        }

        /// <summary>
        /// Compute the distance from the plane to a point.
        /// </summary>
        /// <param name="point"></param>
        /// <returns>Nearest distance from this plane to the specified point.  The result is negative if the point is on the back side of the plane.</returns>
        public float Distance(Vector3 point)
        {
            float scalar = Vector3.Dot(normal, this.point);
            return Vector3.Dot(normal, point) - scalar;
        }

        /// <summary>
        /// Compute the distance along the ray to this plane.
        /// </summary>
        /// <param name="ray"></param>
        /// <returns>Distance along the ray until the plane is reached.  If the plane is behind the ray, the return value is negative.</returns>
        public float Distance(Ray ray)
        {
            float fromRayStart = Distance(ray.Start);

            float alongRay = Math.Abs(fromRayStart / Vector3.Dot(ray.Direction, normal));
            Vector3 target = ray.Start + alongRay * ray.Direction; // This should be on the plane - if not, the distance has the wrong sign.
            Vector3 target2 = ray.Start - alongRay * ray.Direction; // Rather than compare with zero, which is not deterministic, compare with the possible result if the plane were behind the ray.
            
            if (Math.Abs(Distance(target)) > Math.Abs(Distance(target2)))
            {
                alongRay = -alongRay;
            }
            return alongRay;
        }

        /// <summary>
        /// Create a matrix which aligns the z axis with plane normal.
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public Matrix4 CreateMatrix()
        {
            Vector3 up = new Vector3(0, 0, 1);
            if (Math.Abs(Normal.Z) > 0.8)
            {
                up = new Vector3(1, 0, 0);
            }
            float scalar = Vector3.Dot(Point, Normal);
            Matrix4 transform = Matrix4.LookAt(Normal * scalar, Normal * (scalar - 1), up);
            return transform;
        }
    }
}
