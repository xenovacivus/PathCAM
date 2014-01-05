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
using Geometry;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using System.Drawing;
using Router.Paths;

namespace GUI
{
    public class TabsGUI : Tabs, IOpenGLDrawable, IClickable3D
    {
        private Slice drawSlice;
        protected Vector3 locationOffset = Vector3.Zero;
        private bool hovered = false;
        Vector3 hoveredPoint = Vector3.Zero;
        int selectedTabIndex = -1;
        int drawSliceDisplayList = -1;
        int obliterateIndicateDisplayList = -1;

        public void Draw()
        {
            GL.PushMatrix();
            GL.Translate(locationOffset);

            if (mouseHovering)
            {
                var ticks = DateTime.Now.Ticks;
                int alpha = (int)(100 * (Math.Sin(((double)(ticks / 10000))/300.0d) + 1));

                Vector3 location = hoveredPoint;
                GL.Color4(Color.FromArgb(alpha, Color.Green));
                if (selectedTabIndex >= 0)
                {
                    location = tabLocations[selectedTabIndex];
                    GL.Color3(Color.Blue);
                }
                
                Polyhedra.DrawCylinderWireMesh(location, location + new Vector3(0, 0, tabHeight), this.tabRadius);
            }
            
            GL.Color3(Color.Orange);
            if (drawSliceDisplayList > 0)
            {
                GL.CallList(drawSliceDisplayList);
            }
            else
            {
                drawSliceDisplayList = GL.GenLists(1);
                GL.NewList(drawSliceDisplayList, ListMode.CompileAndExecute);
                GL.Begin(PrimitiveType.Triangles);
                GL.Normal3(drawSlice.Plane.Normal);
                foreach (var triangle in drawSlice.Triangles())
                {
                    foreach (var point in triangle.Vertices)
                    {
                        GL.Vertex3(point);
                    }
                }
                GL.End();
                GL.EndList();
            }

            if (tabLocations.Count == 0)
            {
                GL.Color3(Color.DarkRed);
                if (obliterateIndicateDisplayList > 0)
                {
                    GL.CallList(obliterateIndicateDisplayList);
                }
                else
                {
                    Slice s = new Slice(Boundary);
                    s.Subtract(drawSlice);
                    obliterateIndicateDisplayList = GL.GenLists(1);
                    GL.NewList(obliterateIndicateDisplayList, ListMode.CompileAndExecute);
                    GL.Begin(PrimitiveType.Triangles);
                    GL.Normal3(s.Plane.Normal);
                    foreach (var triangle in s.Triangles())
                    {
                        foreach (var point in triangle.Vertices)
                        {
                            GL.Vertex3(point);
                        }
                    }
                    GL.End();
                    GL.EndList();
                }
            }
            
            GL.Color3(Color.DarkOrange);
            int i = 0;
            foreach (var tab in tabLocations)
            {
                if (!selectedTabDraggedOff || i != selectedTabIndex)
                {
                    Polyhedra.DrawCylinder(tab + new Vector3(0, 0, .001f), tab + new Vector3(0, 0, tabHeight), this.tabRadius);
                }
                i++;
            }
            GL.PopMatrix();
        }

        public TabsGUI(LineStrip boundary, float toolRadius, bool inside = false) : base(boundary, toolRadius, inside)
        {
            drawSlice = new Slice(this.TabPath, toolRadius * 2.0f, new Plane(Vector3.UnitZ, Vector3.Zero), true);
        }

        public Vector3 Offset
        {
            get { return locationOffset; }
            set { locationOffset = value; }
        }

        #region IClickable3D

        void IClickable3D.MouseDown(Ray pointer)
        {
            float distance = drawSlice.Plane.Distance(pointer);
            Vector3 mousePoint = pointer.Start + pointer.Direction * distance - locationOffset;
            if (selectedTabIndex < 0)
            {
                this.tabLocations.Add(hoveredPoint);
                selectedTabIndex = tabLocations.Count - 1;
            }
            mouseOffset = tabLocations[selectedTabIndex] - mousePoint;
            mouseHovering = true;
        }

        void IClickable3D.MouseUp(Ray pointer)
        {
            if (selectedTabDraggedOff && selectedTabIndex >= 0)
            {
                tabLocations.RemoveAt(selectedTabIndex);
            }
            selectedTabIndex = -1;
        }

        bool mouseHovering = false;
        float IClickable3D.DistanceToObject(Ray pointer)
        {
            mouseHovering = false;
            float distance = drawSlice.Plane.Distance(pointer);
            Vector3 mousePoint = pointer.Start + pointer.Direction * distance - locationOffset;
            hovered = false;
            float closestPointDistance = float.PositiveInfinity;
            Vector3 closestPoint = Vector3.Zero;
            if (distance > 0)
            {
                selectedTabIndex = -1;
                float minDistanceToTab = tabRadius;
                for (int i = 0; i < tabLocations.Count; i++)
                {
                    float d = (mousePoint - tabLocations[i]).Length;
                    if (d < minDistanceToTab)
                    {
                        selectedTabIndex = i;
                        minDistanceToTab = d;
                        hovered = true;
                    }
                }
                if (selectedTabIndex < 0)
                {
                    foreach (var segment in TabPath.Segments(LineStrip.Type.Closed))
                    {
                        Vector3 test = segment.ClosestPoint(mousePoint);
                        float d = (mousePoint - test).Length;
                        if (d < closestPointDistance)
                        {
                            closestPoint = test;
                            closestPointDistance = d;
                        }
                    }
                    if (closestPointDistance < toolRadius)
                    {
                        hovered = true;
                        hoveredPoint = closestPoint;
                    }
                }
            }

            if (!hovered)
            {
                return float.PositiveInfinity;
            }
            return distance;
        }


        void IClickable3D.MouseHover()
        {
            mouseHovering = true;
            selectedTabDraggedOff = false;
        }

        bool selectedTabDraggedOff = false;
        Vector3 mouseOffset = Vector3.Zero;
        void IClickable3D.MouseMove(Ray pointer)
        {
            float distance = drawSlice.Plane.Distance(pointer);
            Vector3 mousePoint = pointer.Start + pointer.Direction * distance + mouseOffset - locationOffset;

            selectedTabDraggedOff = true;
            float closestPointDistance = tabRadius;
            Vector3 closestPoint = mousePoint;
            foreach (var segment in TabPath.Segments(LineStrip.Type.Closed))
            {
                Vector3 test = segment.ClosestPoint(mousePoint);
                float d = (mousePoint - test).Length;
                if (d < closestPointDistance)
                {
                    selectedTabDraggedOff = false;
                    closestPoint = test;
                    closestPointDistance = d;
                }
            }

            tabLocations[selectedTabIndex] = closestPoint;
        }

        #endregion

    }
}
