using Geometry;
using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenTK.Graphics.OpenGL;
using System.Drawing;
using ClipperLib;
using System.Xml.Serialization;
using System.IO;

namespace GUI
{
    public class PolygonDrawing : IOpenGLDrawable, IClickable3D, IRightClickable3D
    {
        private static float pointSize = 0.08f;
        private static float lineSize = 0.05f;

        public class Polygon
        {
            public List<Vector3> points;
            public Polygon()
            {
                points = new List<Vector3>();
            }
        }

        public void SetPolygons(List<List<IntPoint>> intpolygons)
        {
            polygons.Clear();
            foreach (List<IntPoint> intpoly in intpolygons)
            {
                Polygon newpoly = new Polygon();
                foreach (IntPoint intPoint in intpoly)
                {
                    newpoly.points.Add(
                        new Vector3(Polygon2D.FromIntSpace(intPoint.X), Polygon2D.FromIntSpace(intPoint.Y), 0));
                }
                polygons.Add(newpoly);
            }
        }

        private List<Polygon> polygons;

        public PolygonDrawing()
        {
            polygons = new List<Polygon>();

            try
            {
                StreamReader r = new StreamReader("polygons.txt");
                XmlSerializer ser = new XmlSerializer(typeof(List<Polygon>));
                polygons = ser.Deserialize(r) as List<Polygon>;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Could not load polygons from file: " + ex.Message);
            }
            if (polygons == null || polygons.Count == 0)
            {
                polygons.Add(new Polygon());
                polygons[0].points.Add(new Vector3(-2, -2, 0));
                polygons[0].points.Add(new Vector3(-2, 2, 0));
                polygons[0].points.Add(new Vector3(2, 2, 0));
                polygons[0].points.Add(new Vector3(2, -2, 0));

                // A hole
                polygons.Add(new Polygon());
                polygons[1].points.Add(new Vector3(-0.5f, -0.5f, 0));
                polygons[1].points.Add(new Vector3(0.5f, -0.5f, 0));
                polygons[1].points.Add(new Vector3(0.5f, 0.5f, 0));
                polygons[1].points.Add(new Vector3(-0.5f, 0.5f, 0));

                // A hole
                polygons.Add(new Polygon());
                polygons[2].points.Add(new Vector3(0.7f, 0.0f, 0));
                polygons[2].points.Add(new Vector3(1.5f, 0.0f, 0));
                polygons[2].points.Add(new Vector3(1.0f, 0.5f, 0));
            }
        }

        ~PolygonDrawing()
        {
            XmlSerializer ser = new XmlSerializer(typeof(List<Polygon>));
            TextWriter output = new StreamWriter("polygons.txt");
            ser.Serialize(output, polygons);
        }

        public void DrawPolygonFill()
        {
            List<List<IntPoint>> polygons2d = new List<List<IntPoint>>();
            Polygon2D polygons2D = new Polygon2D();
            foreach (Polygon polygon in polygons)
            {
                List<IntPoint> polygon2D = new List<IntPoint>();
                foreach (Vector3 point in polygon.points)
                {
                    polygon2D.Add(Polygon2D.ToIntSpace(point));
                }
                polygons2D.Add(polygon2D);
            }

            GL.Begin(PrimitiveType.Triangles);
            foreach (Triangle t in polygons2D.EarClipForTriangles())
            {
                GL.Normal3(Vector3.UnitZ);
                GL.Vertex3(t.A);
                GL.Vertex3(t.B);
                GL.Vertex3(t.C);
            }
            GL.End();
        }

        public void Draw()
        {
            GL.Color3(Color.LightGreen);
            DrawPolygonFill();

            foreach (Polygon polygon in polygons)
            {
                Vector3 lastPoint = polygon.points.Last();
                for (int i = 0; i < polygon.points.Count; i++)
                {
                    Vector3 p = polygon.points[i];
                    Color pointColor = Color.Blue;
                    Color edgeColor = Color.Blue;
                    if (highlightPolygon == polygon)
                    {
                        if (selectedPoint == i)
                        {
                            if (isHoveringPoint)
                            {
                                pointColor = Color.Red;
                            }
                            if (isHoveringEdge)
                            {
                                edgeColor = Color.Red;
                            }
                        }
                    }
                    GL.Color3(pointColor);
                    Polyhedra.DrawCircle(p + new Vector3(0, 0, .001f), pointSize, new Vector3(0, 0, 1), 24);
                    //Polyhedra.DrawFatLine(lastPoint, p, 0.25f, new Vector3(0, 0, 1), 10);
                    GL.Color3(edgeColor);
                    // Make the lines look a little better - don't draw them over the circle at the end.
                    Vector3 direction = p - lastPoint;
                    direction.Normalize();
                    Polyhedra.DrawFatLine(lastPoint + direction * pointSize, p - direction * pointSize, 0.05f, new Vector3(0, 0, 1));
                    lastPoint = p;
                }
            }
            GL.LineWidth(1);
        }

        private Polygon highlightPolygon;
        private Vector3 highlightPoint;
        private bool isHoveringEdge = false;
        private bool isHoveringPoint = false;

        float IClickable3D.DistanceToObject(Ray pointer)
        {
            Plane plane = new Plane(new Vector3(0, 0, 1), new Vector3(0, 0, 0));
            Vector3 pointOnPlane = pointer.Start + pointer.Direction * plane.Distance(pointer);

            float closestHoveredPoint = float.PositiveInfinity;
            isHoveringEdge = false;
            isHoveringPoint = false;
            selectedPoint = -1;
            //highlightPolygon = null;
            foreach (Polygon polygon in polygons)
            {
                Vector3 lastPoint = polygon.points.Last();
                foreach (Vector3 p in polygon.points)
                {
                    LineSegment l = new LineSegment(lastPoint, p);
                    lastPoint = p;
                    float distanceToEdge = l.Distance(pointOnPlane);
                    float distance = (p - pointOnPlane).Length;
                    if (distance < pointSize)
                    {
                        //if (distance < closestHoveredPoint)
                        {
                            closestHoveredPoint = distance;
                            highlightPolygon = polygon;
                            selectedPoint = polygon.points.IndexOf(p);
                            highlightPoint = p;
                            isHoveringEdge = false;
                            isHoveringPoint = true;
                        }
                    }

                    if (!isHoveringPoint)
                    {
                        if (distanceToEdge < lineSize)// && distanceToEdge < closestHoveredPoint)
                        {
                            closestHoveredPoint = distance;
                            highlightPolygon = polygon;
                            selectedPoint = polygon.points.IndexOf(p);
                            highlightPoint = p;
                            isHoveringEdge = true;
                            isHoveringPoint = false;
                            hoveredPoint = pointOnPlane;
                        }
                    }
                }
            }
            if (selectedPoint >= 0)
            {
                return plane.Distance(pointer);
            }
            return float.PositiveInfinity;
        }

        private int selectedPoint = -1;
        private Vector3 selectedPointOffset;

        void IClickable3D.MouseDown(Ray pointer)
        {
            Plane p = new Plane(new Vector3(0, 0, 1), new Vector3(0, 0, 0));
            Vector3 pointOnPlane = pointer.Start + pointer.Direction * p.Distance(pointer);
            selectedPointOffset = highlightPoint - pointOnPlane;
            //if (isHoveringEdge)
            //{
            //    
            //    selectedPoint = highlightPolygon.points.IndexOf(highlightPoint);
            //}
            //if (hasHoveredPoint)
            //{
            //    // Select and move the hovered point
            //    selectedPointOffset = hoveredPoint - pointOnPlane;
            //    selectedPoint = hoveredPolygon.points.IndexOf(hoveredPoint);
            //}
            //else
            //{
            //    //return float.PositiveInfinity;
            //    // Add a new point
            //    // TODO: different way to add...
            //    polygons[0].points.Add(pointOnPlane);
            //}
        }
        
        private Vector3 hoveredPoint;

        void IClickable3D.MouseHover()
        {
            //if (lastDistanceToObjectRay == null)
            //{
            //    return;
            //}
            //
            //Plane plane = new Plane(new Vector3(0, 0, 1), new Vector3(0, 0, 0));
            //
            //Vector3 pointOnPlane = lastDistanceToObjectRay.Start + lastDistanceToObjectRay.Direction * plane.Distance(lastDistanceToObjectRay);
            //
            //hasHoveredPoint = false;
            //float closestHoveredPoint = pointSize;
            //foreach (Polygon polygon in polygons)
            //{
            //    foreach (Vector3 p in polygon.points)
            //    {
            //        float distance = (p - pointOnPlane).Length;
            //        if (distance < closestHoveredPoint)
            //        {
            //            hasHoveredPoint = true;
            //            hoveredPoint = p;
            //            hoveredPolygon = polygon;
            //            closestHoveredPoint = distance;
            //        }
            //    }
            //}
            //
            ////throw new NotImplementedException();
        }

        void IClickable3D.MouseMove(Ray pointer)
        {
            if (highlightPolygon != null && selectedPoint >= 0 && selectedPoint <= highlightPolygon.points.Count)
            {
                Plane p = new Plane(new Vector3(0, 0, 1), new Vector3(0, 0, 0));
                Vector3 pointOnPlane = pointer.Start + pointer.Direction * p.Distance(pointer);

                if (isHoveringEdge)
                {
                    // Move both points connected to the edge
                    int nextIndex = (selectedPoint + highlightPolygon.points.Count-1) % highlightPolygon.points.Count;
                    Vector3 offset = highlightPolygon.points[nextIndex] - highlightPolygon.points[selectedPoint];
                    highlightPolygon.points[selectedPoint] = pointOnPlane + selectedPointOffset;
                    highlightPolygon.points[nextIndex] = highlightPolygon.points[selectedPoint] + offset;
                }
                else if (isHoveringPoint)
                {
                    highlightPolygon.points[selectedPoint] = pointOnPlane + selectedPointOffset;
                }
            }
        }

        void IClickable3D.MouseUp(Ray pointer)
        {
            // This happens just before "MouseRightClick" occurs
            //selectedPoint = -1;
            //throw new NotImplementedException();
        }

        string[] IRightClickable3D.MouseRightClick(Ray pointer)
        {
            Console.WriteLine("MouseRightClick");
            if (highlightPolygon != null && selectedPoint >= 0)
            {
                if (isHoveringEdge)
                {
                    return new string[] { "Add Point", "Reverse Polygon", "Duplicate" };
                }
                else if (isHoveringPoint)
                {
                    return new string[] { "Delete Point" };
                }
            }
            return new string[] { };
        }

        void IRightClickable3D.MouseRightClickSelect(string result)
        {
            if (highlightPolygon != null && selectedPoint >= 0)
            {
                if (isHoveringEdge)
                {
                    if (result == "Add Point")
                    {
                        highlightPolygon.points.Insert(selectedPoint, hoveredPoint);
                    }
                    else if (result == "Reverse Polygon")
                    {
                        highlightPolygon.points.Reverse();
                    }
                    else if (result == "Duplicate")
                    {
                        Polygon p = new Polygon();
                        p.points.AddRange(highlightPolygon.points);
                        polygons.Add(p);
                    }
                }
                else if (isHoveringPoint)
                {
                    if (result == "Delete Point")
                    {
                        if (highlightPolygon.points.Count > 3)
                        {
                            highlightPolygon.points.RemoveAt(selectedPoint);
                        }
                        else
                        {
                            Console.WriteLine("Can't delete, polygon doesn't want to be one-dimensional.");
                        }
                    }
                }
            }
        }
    }
}
