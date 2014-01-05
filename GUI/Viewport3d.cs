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

using OpenTK;
using OpenTK.Graphics.OpenGL;
using Router;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Geometry;

namespace GUI
{
    public class Viewport3d : IClickable3D, IOpenGLDrawable
    {
        private Drawing3D parent;
        private bool isMouseDown = false;
        private Plane plane = new Plane(Vector3.UnitZ, Vector3.Zero);
        private Matrix4 mouseDownMatrix = Matrix4.Identity;
        private Ray mouseDownRay = new Ray(Vector3.Zero, -Vector3.UnitZ);
        List<Object> objects = new List<Object>();
        public Matrix4 viewMatrix = Matrix4.CreateTranslation(0, 0, -5);
        private Matrix4 projectionMatrix;
        private Matrix4 inverseProjectionMatrix;
        Vector3 mouseDownCameraPosition = Vector3.Zero;

        public Viewport3d(Drawing3D parent)
        {
            this.parent = parent;
            parent.Resize += parent_Resize;
            viewMatrix = Matrix4.Mult(Matrix4.CreateRotationX(-OpenTK.MathHelper.PiOver4), viewMatrix);
        }

        void parent_Resize(object sender, EventArgs e)
        {
            Rectangle r = parent.ClientRectangle;
            if (r.Height == 0)
            {
                r.Height = 1;
            }
            float aspect = r.Width / (float)r.Height;
            projectionMatrix = OpenTK.Matrix4.CreatePerspectiveFieldOfView(OpenTK.MathHelper.PiOver4, aspect, 0.01f, 100.0f);
            inverseProjectionMatrix = Matrix4.Invert(projectionMatrix);
        }

        internal void Zoom(Ray pointer, int ticks)
        {
            Vector3 target = pointer.Start + (plane.Distance(pointer) * pointer.Direction);
            viewMatrix = Matrix4.Mult(Matrix4.CreateTranslation((target - CameraPosition) * .10f * ticks), viewMatrix);
            ClampMatrix(ref viewMatrix);
        }

        internal void BeginRotate()
        {
            mouseDownMatrix = viewMatrix;
        }

        public void ViewportRotate(float deltaX, float deltaY)
        {
            float pixelsPerRadian = 200.0f; // Larger factor means slower/more precise rotation.

            viewMatrix = mouseDownMatrix;
            Ray pointer = GetPointerRay(new Point(parent.ClientRectangle.Width / 2, parent.ClientRectangle.Height / 2));
            Vector3 point = plane.Distance(pointer) * pointer.Direction + pointer.Start;

            viewMatrix = Matrix4.Mult(Matrix4.CreateTranslation(point), mouseDownMatrix);
            viewMatrix = Matrix4.Mult(Matrix4.CreateRotationZ(deltaX / pixelsPerRadian), viewMatrix);
            Matrix4 m = viewMatrix;
            m.Invert();
            Vector3 left = new Vector3(m.Row0.X, m.Row0.Y, m.Row0.Z);
            viewMatrix = Matrix4.Mult(Matrix4.CreateFromAxisAngle(left, deltaY / pixelsPerRadian), viewMatrix);
            viewMatrix = Matrix4.Mult(Matrix4.CreateTranslation(-point), viewMatrix);
            ClampMatrix(ref viewMatrix);
        }

        public void MouseMove(Ray pointer)
        {
            float distance = plane.Distance(pointer);
            Vector3 point = pointer.Start + pointer.Direction * distance;

            if (isMouseDown)
            {
                Vector3 downLocation = plane.Distance(mouseDownRay) * mouseDownRay.Direction + mouseDownRay.Start;
                viewMatrix = Matrix4.Mult(Matrix4.CreateTranslation(point.X - downLocation.X, point.Y - downLocation.Y, 0), mouseDownMatrix);
                ClampMatrix(ref viewMatrix);
            }
        }

        private void ClampMatrix(ref Matrix4 m)
        {
            float maxDistance = 50.0f;
            Vector3 direction = m.Row3.Xyz;
            float toZero = direction.Length;
            if (toZero > maxDistance)
            {
                direction.Normalize();
                m.Row3 -= new Vector4(direction * (toZero - maxDistance), 0);
            }
        }

        public Matrix4 ProjectionMatrix
        {
            get { return this.projectionMatrix; }
        }

        private Vector3 ComputePointerDirection(Vector2 screenLocation)
        {
            Matrix4 m = viewMatrix;
            m.Row3 = new Vector4(0, 0, 0, 1); // Only keep the rotation part.

            Rectangle r = parent.ClientRectangle;
            if (r.Height == 0)
            {
                r.Height = 1;
            }
            if (r.Width == 0)
            {
                r.Width = 1;
            }

            // Scale the x & y positions such that the point (0, 0) is in the center and (1, 1) is in the upper right
            float y = -(2.0f * screenLocation.Y / (float)r.Height) + 1.0f;
            float x = (2.0f * screenLocation.X / (float)r.Width) - 1.0f;

            Vector3 test = Vector3.Transform(new Vector3(x, y, 0), inverseProjectionMatrix);

            test.Normalize();
            
            return Vector3.TransformVector(test, Matrix4.Invert(m));
        }

        public Vector3 ComputeMouseTarget(Vector2 screen_location)
        {
            Matrix4 m = viewMatrix;
            if (this.isMouseDown)
            {
                m = mouseDownMatrix;
            }

            Rectangle r = parent.ClientRectangle;

            if (r.Height == 0)
            {
                r.Height = 1;
            }

            // Scale the x & y positions such that the point (0, 0) is in the center and (0.5, 0.5) is in the upper right
            float y = -screen_location.Y / (float)r.Height + 0.5f;
            float x = screen_location.X / (float)r.Width - 0.5f;

            float a = (float)(0.5f / Math.Tan(Math.PI / 8.0d));
            float aspect = r.Width / (float)r.Height;

            Vector3 v = new Vector3(x * aspect / a, y / a, -1);

            Vector3 planeNormal = new Vector3(0, 0, 1);
            Vector3 pointOnPlane = new Vector3(0, 0, 0);

            pointOnPlane = Vector3.Transform(pointOnPlane, m);
            planeNormal = Vector3.Transform(planeNormal, m) - Vector3.Transform(new Vector3(0, 0, 0), m);

            Vector3 cameraPosition = new Vector3(m.Row3.X, m.Row3.Y, m.Row3.Z);
            float distanceToPlane = Vector3.Dot(pointOnPlane, planeNormal);
            v.Normalize();
            float distanceAlongLine = distanceToPlane / (Vector3.Dot(planeNormal, v));

            v = v * distanceAlongLine;

            v = Vector3.Transform(v, Matrix4.Invert(m));
            return v;
        }

        private void DrawAxis()
        {
            int min = -10;
            int max = 10;

            // 1 inch spaced grid lines
            GL.Disable(EnableCap.Lighting);
            
            GL.Begin(PrimitiveType.Lines);
            for (float i = min; i <= max; i += 1.0f)
            {
                GL.Color3(Color.LightPink);
                GL.Vertex3(min, i, 0);
                GL.Vertex3(max, i, 0);
                GL.Color3(Color.LightGreen);
                GL.Vertex3(i, min, 0);
                GL.Vertex3(i, max, 0);
            }
            GL.End();
            
            GL.Enable(EnableCap.Lighting);

            // Axis arrows for X (red) and Y (green)
            float width = 0.15f;
            float length = 1.0f;
            float z = -0.001f;

            GL.Normal3(Vector3.UnitZ);
            GL.Color3(Color.Red);
            GL.Begin(PrimitiveType.Quads);
            GL.Vertex3(width/2, width/2, z);
            GL.Vertex3(-width/2, -width/2, z);
            GL.Vertex3(length, -width/2, z);
            GL.Vertex3(length, width/2, z);
            GL.End();

            GL.Begin(PrimitiveType.Triangles);
            GL.Vertex3(length, width, z);
            GL.Vertex3(length, -width, z);
            GL.Vertex3(length + width * Math.Sqrt(3), 0, z);
            GL.End();

            GL.Color3(Color.Green);
            GL.Begin(PrimitiveType.Quads);
            GL.Vertex3(-width / 2, -width / 2, z);
            GL.Vertex3(width / 2, width / 2, z);
            GL.Vertex3(width / 2, length, z);
            GL.Vertex3(-width / 2, length, z);

            GL.End();

            GL.Begin(PrimitiveType.Triangles);
            GL.Vertex3(width, length, z);
            GL.Vertex3(-width, length, z);
            GL.Vertex3(0, length + width * Math.Sqrt(3), z);
            GL.End();
        }

        public void Draw()
        {
            this.DrawAxis();
        }

        public void AddObject(object o)
        {
            objects.Add(o);
        }

        internal List<object> GetObjects()
        {
            return objects;
        }

        internal Ray GetPointerRay(Point point)
        {
            Vector3 direction2 = ComputePointerDirection(new Vector2(point.X, point.Y));
            Vector3 location = mouseDownCameraPosition;
            if (!isMouseDown)
            {
                location = CameraPosition;
            }
            return new Ray(location, direction2);
        }

        private Vector3 CameraPosition
        {
            get
            {
                Matrix4 m = viewMatrix;
                m.Invert();
                return new Vector3(m.Row3.X, m.Row3.Y, m.Row3.Z);
            }
        }

        private Vector3 CameraForward
        {
            get
            {
                Matrix4 m = viewMatrix;
                m.Invert();
                return new Vector3(-m.Row2.X, -m.Row2.Y, -m.Row2.Z);
            }
        }

        #region IClickable3D

        void IClickable3D.MouseDown(Ray pointer)
        {
            mouseDownRay = pointer;
            mouseDownMatrix = viewMatrix;
            mouseDownCameraPosition = CameraPosition;
            isMouseDown = true;
        }

        void IClickable3D.MouseUp(Ray pointer)
        {
            isMouseDown = false;
        }

        void IClickable3D.MouseHover()
        {
        }

        float IClickable3D.DistanceToObject(Ray pointer)
        {
            return plane.Distance(pointer);
        }

        #endregion
    }

}
