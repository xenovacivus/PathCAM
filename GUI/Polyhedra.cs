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

namespace GUI
{
    public class Polyhedra
    {
        private static Vector3 GetPerpendicular(Vector3 normal)
        {
            Vector3 c = new Vector3(0, 1, 0);
            if (Math.Abs(normal.Y) > 0.5)
            {
                c = new Vector3(1, 0, 0);
            }
            return c;
        }

        public static void DrawCylinderWireMesh(Vector3 from, Vector3 to, float radius, int sides = 24)
        {
            Vector3 direction = to - from;
            direction.Normalize();
            Vector3 c = GetPerpendicular(direction);
            Vector3 perp1 = Vector3.Cross(direction, c);
            perp1.Normalize();
            Vector3 perp2 = Vector3.Cross(perp1, direction);

            GL.Disable(EnableCap.Lighting);
            GL.Begin(PrimitiveType.Lines);
            for (float theta = 0; theta < OpenTK.MathHelper.TwoPi; theta += OpenTK.MathHelper.TwoPi / sides)
            {
                float thetaNext = theta + OpenTK.MathHelper.TwoPi / sides;
                Vector3 a = ((float)Math.Sin(theta) * perp1 + (float)Math.Cos(theta) * perp2) * radius;
                Vector3 b = ((float)Math.Sin(thetaNext) * perp1 + (float)Math.Cos(thetaNext) * perp2) * radius;

                GL.Vertex3(to);
                GL.Vertex3(to + a);
                GL.Vertex3(from);
                GL.Vertex3(from + a);

                GL.Vertex3(to + b);
                GL.Vertex3(to + a);
                //GL.Vertex3(to + b);

                GL.Vertex3(from + b);
                GL.Vertex3(from + a);
                //GL.Vertex3(from + b);

                GL.Vertex3(a + from);
                GL.Vertex3(a + to);
            }
            GL.End();
            GL.Enable(EnableCap.Lighting);
        }

        public static void DrawCircle(Vector3 center, float radius, Vector3 normal, int sides = 24)
        {
            normal.Normalize(); // Just make sure it's normalized.
            Vector3 x = GetPerpendicular(normal);
            Vector3 y = Vector3.Cross(normal, x);
            float twopi = (float)(2.0f * Math.PI);
            float inc = twopi / sides;

            GL.Begin(PrimitiveType.TriangleFan);
            GL.Normal3(normal);
            GL.Vertex3(center);
            for (float theta = 0; theta < (twopi + 0.5f * inc); theta += inc)
            {
                float thetaNext = theta + inc;
                float sintheta = (float)Math.Sin(theta);
                float costheta = (float)Math.Cos(theta);
                GL.Vertex3(center + radius * (x * sintheta + y * costheta));
            }
            GL.End();
        }

        public static void DrawFatLine(Vector3 start, Vector3 end, float width, Vector3 normal)
        {
            Vector3 direction = end - start;
            direction.Normalize();
            Vector3 perp = Vector3.Cross(normal, direction);

            GL.Begin(PrimitiveType.Quads);
            GL.Normal3(normal);
            GL.Vertex3(start + 0.5f * perp * width);
            GL.Vertex3(end + 0.5f * perp * width);
            GL.Vertex3(end - 0.5f * perp * width);
            GL.Vertex3(start - 0.5f * perp * width);
            GL.End();

            // Add an arrow pointer in the middle
            GL.Begin(PrimitiveType.TriangleFan);
            GL.Normal3(normal);
            GL.Vertex3((start + end) * 0.5f - direction * width * 0.8f);
            GL.Vertex3((start + end) * 0.5f - direction * width + perp * width * 2);
            GL.Vertex3((start + end) * 0.5f + direction * width * 2f);
            GL.Vertex3((start + end) * 0.5f - direction * width - perp * width * 2);
            GL.End();
        }

        public static void DrawCircleLine(Vector3 center, float radius, Vector3 normal, int sides = 24)
        {
            normal.Normalize(); // Just make sure it's normalized.
            Vector3 x = GetPerpendicular(normal);
            Vector3 y = Vector3.Cross(normal, x);
            float twopi = (float)(2.0f * Math.PI);
            float inc = twopi / sides;

            GL.Begin(PrimitiveType.LineLoop);
            GL.Normal3(normal);
            for (float theta = 0; theta < (twopi - 0.5f * inc); theta += inc)
            {
                float thetaNext = theta + inc;
                float sintheta = (float)Math.Sin(theta);
                float costheta = (float)Math.Cos(theta);
                GL.Vertex3(center + radius * (x * sintheta + y * costheta));
            }
            GL.End();
        }

        public static void DrawCylinder(Vector3 from, Vector3 to, float radius, int sides = 24)
        {
            Vector3 direction = to - from;
            direction.Normalize();
            Vector3 c = GetPerpendicular(direction);
            Vector3 perp1 = Vector3.Cross(direction, c);
            perp1.Normalize();
            Vector3 perp2 = Vector3.Cross(perp1, direction);

            GL.Begin(PrimitiveType.Triangles);
            for (float theta = 0; theta < OpenTK.MathHelper.TwoPi; theta += OpenTK.MathHelper.TwoPi / sides)
            {
                float thetaNext = theta + OpenTK.MathHelper.TwoPi / sides;
                Vector3 a = ((float)Math.Sin(theta) * perp1 + (float)Math.Cos(theta) * perp2) * radius;
                Vector3 b = ((float)Math.Sin(thetaNext) * perp1 + (float)Math.Cos(thetaNext) * perp2) * radius;
                GL.Normal3(direction);
                GL.Vertex3(to);
                GL.Vertex3(to + a);
                GL.Vertex3(to + b);

                GL.Normal3(-direction);
                GL.Vertex3(from);
                GL.Vertex3(from + a);
                GL.Vertex3(from + b);

                GL.Normal3(a);
                GL.Vertex3(a + from);
                GL.Normal3(b);
                GL.Vertex3(b + from);
                GL.Vertex3(b + to);

                GL.Normal3(a);
                GL.Vertex3(a + from);
                GL.Vertex3(a + to);
                GL.Normal3(b);
                GL.Vertex3(b + to);
                
            }
            GL.End();
        }

        public static void DrawCone(Vector3 from, Vector3 to, float radius, int sides = 24)
        {
            Vector3 direction = to - from;
            direction.Normalize();
            Vector3 c = GetPerpendicular(direction);
            Vector3 perp1 = Vector3.Cross(direction, c);
            perp1.Normalize();
            Vector3 perp2 = Vector3.Cross(perp1, direction);


            GL.Begin(PrimitiveType.TriangleFan);
            GL.Normal3(direction);
            GL.Vertex3(to);
            for (float theta = 0; theta < OpenTK.MathHelper.TwoPi; theta += OpenTK.MathHelper.TwoPi/sides)
            {
                float x = (float)Math.Sin(theta);
                float y = (float)Math.Cos(theta);
                Vector3 tail = (perp1 * x + perp2 * y) * radius + from;
                GL.Normal3(perp1 * x + perp2 * y);
                GL.Vertex3(tail);
            }
            GL.End();
        }
    }
}
