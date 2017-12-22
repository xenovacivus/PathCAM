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
using Geometry;
using OpenTK.Graphics.OpenGL;
using OpenTK;
using System.Drawing;
using Router.Paths;

namespace GUI
{
    class TriangleMeshGUI : TriangleMesh, IOpenGLDrawable, IClickable3D
    {
        private bool isPointedAt = false;
        private Vector3 mouseHoverPoint = Vector3.Zero;
        private List<TabsGUI> tabs = new List<TabsGUI>();
        private Vector3 offset = Vector3.Zero;

        // Variables for animated rotation/translation
        bool isTransforming = false;
        double secondsToTransform = 1.0f;
        DateTime transformBeginTime = DateTime.MinValue;
        Matrix4 targetTransform;
        Matrix4 fromTransform;
        
        public TriangleMeshGUI() : base()
        {
        }

        public List<TabsGUI> Tabs
        {
            get { return tabs; }
        }

        public Vector3 Offset
        {
            get { return offset; }
            set
            {
                Vector3 newOffset = value;
                // Snap to 1/4 inch locations
                //newOffset.X = (float)Math.Round(newOffset.X * 4.0f) / 4.0f;
                //newOffset.Y = (float)Math.Round(newOffset.Y * 4.0f) / 4.0f;
                //newOffset.Z = (float)Math.Round(newOffset.Z * 4.0f) / 4.0f;
                offset = newOffset;
                foreach (var tab in tabs)
                {
                    tab.Offset = offset;
                }
            }
        }

        private float lastToolRadius = 0.1f;
        public void GenerateTabPaths(float toolRadius)
        {
            lastToolRadius = toolRadius;
            tabs.Clear();
            try
            {
                Slice s = new Slice(this, new Plane(Vector3.UnitZ, new Vector3(0, 0, MinPoint.Z + 0.002f)));
                foreach (var line in s.GetLines(Slice.LineType.Outside))
                {
                    tabs.Add(new TabsGUI(line, toolRadius, false));
                }
                foreach (var line in s.GetLines(Slice.LineType.Hole))
                {
                    tabs.Add(new TabsGUI(line, toolRadius, true));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error generating tab paths: " + ex.Message);
            }
            this.Offset = offset; // Force the offset update in the tabs
        }

        int lastNumTriangles = -1;
        int triangleDisplayList = -1;
        int lineDisplayList = -1;
        int badLineDisplayList = -1;

        public void RefreshDisplayLists()
        {
            lastNumTriangles = -1;
        }

        public bool UseDisplayLists = true;

        public void Draw()
        {
            GL.PushMatrix();
            GL.Translate(offset);

            if (isTransforming)
            {
                double deltaTime = (DateTime.Now - transformBeginTime).TotalSeconds;
                if (deltaTime > secondsToTransform)
                {
                    isTransforming = false;
                    this.Transformation = targetTransform;
                    this.RefreshDisplayLists();
                }
                else
                {
                    float linear = (float)(deltaTime / secondsToTransform);
                    float interp = linear < 0.5f ? 2 * linear * linear : linear * (4 - 2 * linear) - 1.0f;
                    Quaternion source = fromTransform.ExtractRotation();
                    Quaternion target = targetTransform.ExtractRotation();
                    Quaternion slerp = Quaternion.Slerp(source, target, (float)interp);

                    Vector3 translatePart = fromTransform.ExtractTranslation() * (1-interp) + targetTransform.ExtractTranslation() * interp;

                    Matrix4 x = Matrix4.CreateFromQuaternion(slerp) * Matrix4.CreateTranslation(translatePart);
                    
                    //Matrix4 x = targetTransform * (float)interp + fromTransform * (float)(1 - interp);
                    
                    GL.MultMatrix(ref x);
                }
            }

            Color triangleColor = Color.Green;
            Color lineColor = Color.Green;
            Color badLineColor = Color.White;

            if (hovered)
            {
                lineColor = Color.LightBlue;
            }

            bool useDisplayLists = UseDisplayLists;

            // Draw the point pointed to by the mouse
            //GL.Color3(Color.Blue);
            //GL.PointSize(2);
            //GL.Begin(PrimitiveType.Points);
            //GL.Vertex3(mousePoint);
            //GL.End();
            //GL.PointSize(1);
            //GL.Color3(Color.LightGreen);
            
            // Draw the slice at the mouse point
            //GL.Disable(EnableCap.Lighting);
            //GL.Color3(Color.Black);
            //Slice s = new Slice(this, new Plane(Vector3.UnitZ, mouseHoverPoint));
            //foreach (var line in s.GetLines(Slice.LineType.All))
            //{
            //    GL.Begin(PrimitiveType.LineLoop);
            //    foreach (var point in line.Vertices)
            //    {
            //        GL.Vertex3(point);
            //    }
            //    GL.End();
            //}
            //GL.Translate(0, 0, .25f);
            //GL.Color3(Color.DarkBlue);
            //GL.Begin(PrimitiveType.Triangles);
            //foreach (var tri in s.Triangles())
            //{
            //    foreach (var point in tri.Vertices)
            //    {
            //        GL.Vertex3(point);
            //    }
            //}
            //GL.End();
            //GL.Translate(0, 0, -.25f);
            //GL.Enable(EnableCap.Lighting);

            // Draw the triangles & edges
            if (TriangleCount != lastNumTriangles)
            {
                if (triangleDisplayList > 0) // triangleDisplayList is used as the sentinel
                {
                    GL.DeleteLists(triangleDisplayList, 1);
                    GL.DeleteLists(lineDisplayList, 1);
                    GL.DeleteLists(badLineDisplayList, 1);
                    triangleDisplayList = -1;
                }
                lastNumTriangles = TriangleCount;
            }
            
            if (triangleDisplayList >= 0)
            {
                GL.Color3(triangleColor);
                GL.CallList(triangleDisplayList);
            
                GL.Disable(EnableCap.Lighting);
                GL.Color3(lineColor);
                GL.CallList(lineDisplayList);
            
                GL.Color3(badLineColor);
                GL.CallList(badLineDisplayList);
                GL.Enable(EnableCap.Lighting);
            }
            else
            {
                if (useDisplayLists)
                {
                    triangleDisplayList = GL.GenLists(1);
                    lineDisplayList = GL.GenLists(1);
                    badLineDisplayList = GL.GenLists(1);
                }
            
                // Triangles
                GL.Color3(triangleColor);
                if (useDisplayLists)
                {
                    GL.NewList(triangleDisplayList, ListMode.CompileAndExecute);
                }
                GL.Begin(PrimitiveType.Triangles);
                foreach (Triangle t in base.Triangles)
                {
                    GL.Normal3(t.Plane.Normal);
                    var dot = Vector3.Dot(t.Plane.Normal, Vector3.UnitZ);
                    if (dot >= 0.999f)
                    {
                        GL.Color3(Color.Orange);
                    }
                    else if (dot > 0.001f)
                    {
                        GL.Color3(Color.Yellow);
                    }
                    else
                    {
                        GL.Color3(Color.DarkRed);
                    }

                    foreach (Vector3 v in t.Vertices)
                    {
                        GL.Vertex3(v);
                    }
                }
                GL.End();
                if (useDisplayLists)
                {
                    GL.EndList();
                }
            
                // Outside Edges
                List<LineSegment> edges = new List<LineSegment>();
                List<LineSegment> badEdges = new List<LineSegment>();
                
                foreach (Edge edge in this.Edges)
                {
                    List<Triangle> triangles = new List<Triangle>(edge.Triangles);
                    if (triangles.Count == 2)
                    {
                        if (Vector3.Dot(triangles[0].Plane.Normal, triangles[1].Plane.Normal) < Math.Cos(Math.PI * 2.0f / 20.0f))
                        {
                            edges.Add(edge.LineSegment);
                        }
                    }
                    else
                    {
                        badEdges.Add(edge.LineSegment);
                    }
                }
            
                GL.Disable(EnableCap.Lighting);
                GL.Color3(lineColor);
                if (useDisplayLists)
                {
                    GL.NewList(lineDisplayList, ListMode.CompileAndExecute);
                }
                GL.Begin(PrimitiveType.Lines);
                foreach(var line in edges)
                {
                    GL.Vertex3(line.A);
                    GL.Vertex3(line.B);
                }
                GL.End();
                if (useDisplayLists)
                {
                    GL.EndList();
                }
            
                GL.Color3(badLineColor);
                if (useDisplayLists)
                {
                    GL.NewList(badLineDisplayList, ListMode.CompileAndExecute);
                }
                GL.Begin(PrimitiveType.Lines);
                foreach(var line in badEdges)
                {
                    GL.Vertex3(line.A);
                    GL.Vertex3(line.B);
                }
                GL.End();
                if (useDisplayLists)
                {
                    GL.EndList();
                }
                GL.Enable(EnableCap.Lighting);
            }

            //// Highlight the closest triangle - debugging
            //if (hoveredTriangle != null)
            //{
            //    GL.LineWidth(2);
            //    GL.Color3(0, 0, 0);
            //    GL.Begin(PrimitiveType.LineLoop);
            //    GL.Vertex3(hoveredTriangle.A);
            //    GL.Vertex3(hoveredTriangle.B);
            //    GL.Vertex3(hoveredTriangle.C);
            //    GL.End();
            //    GL.LineWidth(1);
            //}


            // Mesh analysis testing...
            //GL.PushMatrix();
            //GL.Translate(10, 0, 0);
            //AnalyzedTriangleMesh test = new AnalyzedTriangleMesh(this);
            //var meshes = test.Analyze();
            //foreach (var mesh in meshes)
            //{
            //    GL.Begin(PrimitiveType.Triangles);
            //    foreach (Triangle t in mesh.Triangles)
            //    {
            //        GL.Normal3(t.Plane.Normal);
            //        var dot = Vector3.Dot(t.Plane.Normal, Vector3.UnitZ);
            //        if (dot >= 0.999f)
            //        {
            //            GL.Color3(Color.Orange);
            //        }
            //        else if (dot > 0.001f)
            //        {
            //            GL.Color3(Color.Yellow);
            //        }
            //        else
            //        {
            //            GL.Color3(Color.DarkRed);
            //        }
            //
            //        foreach (Vector3 v in t.Vertices)
            //        {
            //            GL.Vertex3(v);
            //        }
            //    }
            //    GL.End();
            //}
            //GL.PopMatrix();

            // Draw the lines connected to the closest line to the cursor (debugging)
            //if (closestEdge != null)
            //{
            //    List<Triangle> triangles = new List<Triangle>(closestEdge.Triangles);
            //    if (triangles.Count == 2)
            //    {
            //        GL.Begin(PrimitiveType.Lines);
            //        GL.Color3(Color.Blue);
            //        foreach (var point in closestEdge.Vertices)
            //        {
            //            GL.Vertex3(point);
            //        }
            //        GL.End();
            //        
            //        GL.Color3(Color.White);
            //        foreach (var t in triangles)
            //        {
            //            GL.Begin(PrimitiveType.LineLoop);
            //            foreach (var p in t.Vertices)
            //            {
            //                GL.Vertex3(p);
            //            }
            //            GL.End();
            //        }
            //    }
            //}


            // Draw a perimeter around each triangle (debugging)
            //GL.Disable(EnableCap.Lighting);
            //foreach (Triangle t in Triangles)
            //{
            //    GL.Begin(PrimitiveType.LineLoop);
            //    foreach (Vector3 v in t.Vertices)
            //    {
            //        GL.Vertex3(v);
            //    }
            //    GL.End();
            //}
            //GL.Enable(EnableCap.Lighting);

            GL.PopMatrix();
        }

        Vector3 mouseDownPoint = Vector3.Zero;
        Vector3 mouseDownOffset = Vector3.Zero;
        void IClickable3D.MouseDown(Ray pointer)
        {
            mouseDownPoint = mouseHoverPoint;
            mouseDownOffset = offset;
            //Console.WriteLine("Mouse Down TriMeshGUI");
        }

        void IClickable3D.MouseUp(Ray pointer)
        {
        }

        private bool hovered = false;
        //Edge closestEdge = null;
        Triangle hoveredTriangle = null;

        float IClickable3D.DistanceToObject(Ray pointer)
        {
            Ray adjustedPointer = new Ray(pointer.Start - offset, pointer.Direction);
            hovered = false;
            float distance = float.PositiveInfinity;
            hoveredTriangle = null;

            if (!isTransforming)
            {
                foreach (Triangle t in base.Triangles)
                {
                    TriangleRayIntersect i = new TriangleRayIntersect(t, adjustedPointer);
                    if (i.Intersects)
                    {
                        float d = (adjustedPointer.Start - i.Point).Length;
                        if (d < distance)
                        {
                            distance = d;
                            mouseHoverPoint = i.Point;
                            hoveredTriangle = t;
                        }
                    }
                }
            }

            // Remember the closest edge - debugging
            //float x = 0;
            //closestEdge = null;
            //foreach (Edge edge in this.Edges)
            //{
            //    List<Vector3> vertices = new List<Vector3>(edge.Vertices);
            //    LineSegment segment = new LineSegment(vertices[0], vertices[1]);
            //    float d = segment.Distance(mouseHoverPoint);
            //    if (d < x || closestEdge == null)
            //    {
            //        closestEdge = edge;
            //        x = d;
            //    }
            //}

            isPointedAt = distance < float.PositiveInfinity;
            return distance;
        }

        void IClickable3D.MouseHover()
        {
            hovered = true;
        }

        void IClickable3D.MouseMove(Ray pointer)
        {
            Ray adjustedPointer = new Ray(pointer.Start - mouseDownOffset, pointer.Direction);
            // Move the object in the XY plane
            Plane plane = new Plane(Vector3.UnitZ, mouseDownPoint);
            Vector3 point = plane.Distance(adjustedPointer) * adjustedPointer.Direction + adjustedPointer.Start;
            Offset = mouseDownOffset + point - mouseDownPoint;
        }

        internal void SetClickedFaceAsBottom(Ray pointer)
        {
            Ray adjustedPointer = new Ray(pointer.Start - offset, pointer.Direction);
            hovered = false;
            float distance = float.PositiveInfinity;
            Vector3 clickedPoint = Vector3.Zero;
            Plane clickedPlane = null;

            var triangleIndex = 0;
            var count = 0;
            foreach (Triangle t in base.Triangles)
            {
                TriangleRayIntersect i = new TriangleRayIntersect(t, adjustedPointer);
                if (i.Intersects)
                {
                    float d = (adjustedPointer.Start - i.Point).Length;
                    if (d < distance)
                    {
                        distance = d;
                        clickedPoint = i.Point;
                        clickedPlane = t.Plane;
                        triangleIndex = count;
                    }
                }
                count++;
            }

            if (clickedPlane != null)
            {
                fromTransform = Transformation;
                this.Transformation = Matrix4.Identity;
                Vector3 originalCenter = (this.MinPoint + this.MaxPoint) * 0.5f;

                clickedPlane = Triangles.ElementAt(triangleIndex).Plane;
                var up = Vector3.UnitZ;
                if (Math.Abs(clickedPlane.Normal.Z) > 0.95)
                {
                    up = Vector3.UnitX;
                }
                Matrix4 rotate = Matrix4.LookAt(Vector3.Zero, clickedPlane.Normal, up);
                rotate = rotate.ClearTranslation();

                // We want the original clicked point to be at the same X and Y, and at Z = 0
                Vector3 fix = Vector3.Transform(clickedPlane.Point, rotate);
                Vector3 newCenter = Vector3.Transform(originalCenter, rotate);
                Console.WriteLine("Original Center: " + originalCenter);
                rotate = Matrix4.Mult(rotate, Matrix4.CreateTranslation(originalCenter.X - newCenter.X, originalCenter.Y - newCenter.Y, -fix.Z));

                transformBeginTime = DateTime.Now;
                targetTransform = rotate;
                this.isTransforming = true;

                this.Transformation = targetTransform;
                this.GenerateTabPaths(lastToolRadius);
                this.Transformation = Matrix4.Identity;
                this.RefreshDisplayLists();
            }
        }
    }
}
