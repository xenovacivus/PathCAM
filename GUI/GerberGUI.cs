using ClipperLib;
using Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenTK.Graphics.OpenGL;
using System.Drawing;
using OpenTK;
using Router.Paths;
using Router;
using System.Diagnostics;

namespace GUI
{
    class GerberGUI : IOpenGLDrawable, IClickable3D, IRightClickable3D
    {
        private Router.Router router;
        public GerberGUI(Router.Router router)
        {
            this.router = router;
        }
        // Keep this around, it contains a bunch of information
        // about the loaded gerber file.
        private List<LayerAssignedGerber> layers = new List<LayerAssignedGerber>();

        enum Layer
        {
            TopCopper,
            BottomCopper,
            BoardEdge
        }

        private class LayerAssignedGerber
        {
            public GERBER_Loader loader;
            public Layer layer;
            public List<Triangle> mesh;
        }

        bool needsInitialOffset = true;

        public void AddGerberLoader(GERBER_Loader loader)
        {
            LayerAssignedGerber newLayer = new LayerAssignedGerber();
            newLayer.loader = loader;
            
            // See if the layer can be obtained from gerber file information
            newLayer.layer = Layer.TopCopper;
            bool isCopper = false;
            foreach (string s in loader.GetFileAttributes(".FileFunction"))
            {
                // Top Copper Layers:
                // %TF.FileFunction,Copper,L1,Top*%
                // Bottom Copper Layers:
                // %TF.FileFunction,Copper,L2,Bot*%
                // Inner Layers:
                // %TF.FileFunction,Copper,L3,Inr*%
                // Board Edge:
                // %TF.FileFunction,Profile,NP*%
                if (s.ToLower().Contains("copper"))
                {
                    isCopper = true;
                }
                if (isCopper && s.ToLower().Contains("top"))
                {
                    newLayer.layer = Layer.TopCopper;
                }
                if (isCopper && s.ToLower().Contains("bot"))
                {
                    newLayer.layer = Layer.BottomCopper;
                }
                if (s.ToLower().Contains("profile"))
                {
                    newLayer.layer = Layer.BoardEdge;
                }

            }
            
            newLayer.mesh = null;
            layers.Add(newLayer);
        }

        // angle between A and B.
        // Returned angle is between -PI and PI.
        private float AngleCounterClockwise(Vector2 a, Vector2 b)
        {
            float lengths = a.Length * b.Length;
            if (lengths == 0)
            {
                return 0.0f;
            }
            float angle = (float)Math.Acos(Vector2.Dot(a, b) / (lengths));
            if (Vector2.Dot(a.PerpendicularLeft, b) <= 0)
            {
                angle = -angle;
            }
            return angle;
        }


        // Removes points too close together (if they don't make a big difference),
        // attempts to smooth or simplify arcs.
        private void CleanPath(ref List<Vector2> points, float tolerance)
        {
            for (int i = 0; i < points.Count; i++)
            {
                int last_index = (i + points.Count - 1) % points.Count;
                int next_index = (i + 1) % points.Count;

                Vector2 last = points[last_index];
                Vector2 current = points[i];
                Vector2 next = points[next_index];
                
                float angle = AngleCounterClockwise((current - last), (next - current));

                float effectiveTolerance = tolerance;
                // If the angle between points is small, bias toward keeping them.
                if (Math.Abs(angle) > (5.0f * Math.PI / 180.0f))
                {
                    effectiveTolerance /= 10;
                }

                if ((current - last).Length < effectiveTolerance)
                {
                    // Points are effectively coincident, remove the current one.
                    points.RemoveAt(i);
                    i--;
                    continue;
                }
            }
        }

        private List<Vector2> WalkPathPoints(List<Vector2> points, float distance, bool enforcePointDistance)
        {
            if (distance < 0.001f / 25.4f)
            {
                distance = 0.001f / 25.4f;
            }
            List<Vector2> walkPathPoints = new List<Vector2>();

            float nextPointIn = 0.0f;
            float accumulatedAngle = 0.0f;
            for (int i = 0; i < points.Count; i++)
            {
                int last_index = (i + points.Count - 1) % points.Count;
                int next_index = (i + 1) % points.Count;

                Vector2 last = points[last_index];
                Vector2 current = points[i];
                Vector2 next = points[next_index];

                float angle = (float)(180.0f /Math.PI) * AngleCounterClockwise((current - last), (next - current));

                
                accumulatedAngle += (float)Math.Abs(angle);
                //Console.WriteLine("Angle: " + angle + ", accumulated: " + accumulatedAngle);

                // If the angle is > 5 degrees, add the point.  It's probably a relevant corner.
                if (accumulatedAngle > 25.0f || Math.Abs(angle) > 25.0f)
                {
                    walkPathPoints.Add(current);
                    nextPointIn = distance;
                    accumulatedAngle = 0.0f;
                }

                Vector2 to = (next - current);
                Vector2 toNormalized = to.Normalized();

                float length = to.Length;
                bool first = true;
                while (length > nextPointIn)
                {
                    current = current + toNormalized * nextPointIn;
                    // Only add the first point along a straight line
                    if (first || enforcePointDistance)
                    {
                        walkPathPoints.Add(current);
                        accumulatedAngle = 0.0f;
                        first = false;
                    }
                    length -= nextPointIn;
                    nextPointIn = distance;
                }
                nextPointIn -= length;
            }
            return walkPathPoints;
        }

        void AddIsolationPaths(LayerAssignedGerber layerInfo)
        {
            List<List<IntPoint>> paths = Clipper.PolyTreeToPaths(layerInfo.loader.finalPolyTree);

            ClipperOffset clipperOffset = new ClipperOffset();
            clipperOffset.AddPaths(paths, JoinType.jtRound, EndType.etClosedPolygon);
            PolyTree outputTree = new PolyTree();
            clipperOffset.Execute(ref outputTree, Polygon2D.ToIntSpace(0.5f * router.ToolDiameter));

            // TODO: use the poly tree to intentionally form paths
            // from inside to out, or otherwise.
            paths = Clipper.PolyTreeToPaths(outputTree);

            //paths = Clipper.CleanPolygons(paths, 50); // Equivalent to 50um (or 5*254 um if in inches...)

            foreach (List<IntPoint> path in paths)
            {
                List<Vector2> pathCAMUnitsPath = new List<Vector2>();
                LineStrip line = new LineStrip();
                foreach (IntPoint intPoint in path)
                {
                    // Lines from the original poly tree don't have the offset
                    // used by the UI to draw triangles.  Add that in.
                    // TODO: handle flipped polygons as well (when bottom copper is moved to the top).
                    pathCAMUnitsPath.Add(Polygon2D.FromIntSpace(intPoint).Xy);
                }

                //CleanPath(ref pathCAMUnitsPath, router.MaxCutDepth);
                pathCAMUnitsPath = WalkPathPoints(pathCAMUnitsPath, router.maxPointDistance, router.enforceMaxPointDistance);
                foreach (Vector2 point in pathCAMUnitsPath)
                {
                    line.Append(new Vector3(point));
                }

                if (line.Vertices.Count() > 1)
                {
                    line.Append(line.Vertices[0]); // Loop back to the start
                    router.RoutPath(line, false, offset + new Vector3(0, 0, (float)boardHeight));
                }
            }
            router.Complete();

            //LineStrip line = new LineStrip();
            //line.Append(new Vector3(0, 0, 0));
            //line.Append(new Vector3(2, 0, 0));
            //router.RoutPath(line, false, Vector3.Zero);
            //Router.Router router = new Router.Router();
            //PathPlanner.PlanPaths(mesh, new List<Tabs>(), router);
            //var routs = PathPlanner.PlanPaths(triangles, triangles.Tabs.ConvertAll<Tabs>(tab => tab as Tabs), router);
            //foreach (var rout in routs)
            //{
            //    router.RoutPath(rout, false, triangles.Offset);
            //}
        }

        // Iterate depth-first through the nodes
        private IEnumerable<PolyNode> DepthFirstIterate(PolyNode node)
        {
            foreach(PolyNode child in node.Childs)
            {
                foreach (PolyNode r in DepthFirstIterate(child))
                {
                    yield return r;
                }
            }
            yield return node;
        }

        void AddEdgeCutPaths ()
        {
            if (boardPolyTree != null)
            {
                List<List<IntPoint>> paths = Clipper.PolyTreeToPaths(boardPolyTree);

                ClipperOffset clipperOffset = new ClipperOffset();
                clipperOffset.AddPaths(paths, JoinType.jtRound, EndType.etClosedPolygon);
                PolyTree outputTree = new PolyTree();
                clipperOffset.Execute(ref outputTree, Polygon2D.ToIntSpace(0.5f * router.ToolDiameter));

                // TODO: use the poly tree to intentionally form paths
                // from inside to out, or otherwise.
                paths = Clipper.PolyTreeToPaths(outputTree);

                int numDepthIncreases = (int)Math.Ceiling((boardHeight - router.LastPassHeight) / router.MaxCutDepth);
                float depthIncrease = ((float)boardHeight - router.LastPassHeight) / numDepthIncreases;

                foreach (PolyNode node in DepthFirstIterate(outputTree))
                {
                    List<Vector2> pathCAMUnitsPath = new List<Vector2>();
                    LineStrip line = new LineStrip();
                    foreach (IntPoint intPoint in node.Contour)
                    {
                        pathCAMUnitsPath.Add(Polygon2D.FromIntSpace(intPoint).Xy);
                    }

                    pathCAMUnitsPath = WalkPathPoints(pathCAMUnitsPath, router.maxPointDistance, router.enforceMaxPointDistance);
                    foreach (Vector2 point in pathCAMUnitsPath)
                    {
                        line.Append(new Vector3(point));
                    }

                    if (line.Vertices.Count() > 1)
                    {
                        line.Append(line.Vertices[0]); // Loop back to the start
                        float currentDepth = (float)boardHeight;
                        for (int depth = 0; depth < numDepthIncreases; depth++)
                        {
                            currentDepth -= depthIncrease;
                            router.RoutPath(line, false, offset + new Vector3(0, 0, currentDepth));
                        }
                    }
                }
                router.Complete();

                paths = Clipper.CleanPolygons(paths, 10);
            }
            else
            {
                // Cut out the default mesh?
            }
        }

        void DrawNodeBoundaryLines(PolyNode node)
        {
            if (node.IsHole)
            {
                GL.Color3(Color.Red);
            }
            else
            {
                GL.Color3(Color.Blue);
            }

            //// Line around the contour
            //GL.Begin(PrimitiveType.LineLoop);
            //foreach (IntPoint intPoint in node.Contour)
            //{
            //    GL.Vertex3(Polygon2D.FromIntSpace(intPoint));
            //}
            //GL.End();

            // Triangles around the contour
            GL.Color3(Color.Red);
            GL.Begin(PrimitiveType.Quads);
            foreach (LineSegment s in Polygon2D.LineSegmentsFromIntPolygon(node.Contour))
            {
                GL.Normal3(Vector3.Cross(Vector3.UnitZ, s.Direction));
                GL.Vertex3(s.A);
                GL.Vertex3(s.B);
                GL.Vertex3(s.B - Vector3.UnitZ * 0.1f);
                GL.Vertex3(s.A - Vector3.UnitZ * 0.1f);
            }
            GL.End();

            foreach (PolyNode child in node.Childs)
            {
                DrawNodeBoundaryLines(child);
            }
        }

        private IEnumerable<Triangle> GetBoundaryTriangles(PolyNode node, float height)
        {
            // Triangles around the contour
            foreach (LineSegment s in Polygon2D.LineSegmentsFromIntPolygon(node.Contour))
            {
                Vector3 h = new Vector3(0, 0, height);
                if (height > 0)
                {
                    yield return new Triangle(s.A, s.B, s.B + h);
                    yield return new Triangle(s.B + h, s.A + h, s.A);
                }
                else
                {
                    yield return new Triangle(s.B, s.A, s.A + h);
                    yield return new Triangle(s.A + h, s.B + h, s.B);
                }
            }
            
            foreach (PolyNode child in node.Childs)
            {
                foreach (Triangle t in GetBoundaryTriangles(child, height))
                {
                    yield return t;
                }
            }
        }

        private void GetBoundaryTriangles (PolyNode node, float height, ref List<Triangle> mesh, Vector3 offset)
        {
            // Triangles around the contour
            //foreach (LineSegment s in Polygon2D.LineSegmentsFromIntPolygon(node.Contour))
            Vector3 A = Polygon2D.FromIntSpace(node.Contour.LastOrDefault());
            foreach (IntPoint current in node.Contour)
            {
                Vector3 B = Polygon2D.FromIntSpace(current);
                Vector3 h = new Vector3(0, 0, height);
                if (height > 0)
                {
                    mesh.Add(new Triangle(A + offset,     B + offset,     B + h + offset));
                    mesh.Add(new Triangle(B + h + offset, A + h + offset, A + offset));
                }
                else
                {
                    mesh.Add(new Triangle(B + offset,     A + offset,     A + h + offset));
                    mesh.Add(new Triangle(A + h + offset, B + h + offset, B + offset));
                }
                A = B;
            }

            foreach (PolyNode child in node.Childs)
            {
                GetBoundaryTriangles(child, height, ref mesh, offset);
            }
        }

        private void GetPolyTreeTriangles(PolyNode node, ref List<Triangle> mesh, Vector3 offset, bool reverse)
        {
            Polygon2D polygon2D = new Polygon2D();
            if (node.IsHole)
            {
                foreach (PolyNode child in node.Childs)
                {
                    GetPolyTreeTriangles(child, ref mesh, offset, reverse);
                }
            }
            else
            {
                polygon2D.AddLibTessPolygon(node.Contour);
                foreach (PolyNode child in node.Childs)
                {
                    if (child.IsHole) // Should always be true
                    {
                        polygon2D.AddLibTessPolygon(child.Contour);
                    }
                    GetPolyTreeTriangles(child, ref mesh, offset, reverse);
                }
            }
            mesh.AddRange(polygon2D.LibTessTriangles(offset, reverse));
        }

        TriangleMesh boardMesh; // TODO: handle this better?

        PolyTree drillData = null;
        internal void AddDrillData(PolyTree polyTree)
        {
            drillData = polyTree;
            ForceMeshRecompute();
        }

        internal void ForceMeshRecompute()
        {
            bool allLoadersNull = true;
            // Force recompute of meshes.  Recompute will happen
            // on next call to "Draw".
            foreach (LayerAssignedGerber l in layers)
            {
                if (l.loader != null)
                {
                    allLoadersNull = false;
                }
                l.mesh = null;
            }
            if (allLoadersNull)
            {
                needsInitialOffset = true;
                copperMaxPoint = new Vector3(float.NegativeInfinity);
                copperMinPoint = new Vector3(float.PositiveInfinity);
            }
            boardMesh = null;
        }

        public double boardHeight = 1.57f / 25.4f; // Standard FR4 thickness is 1.57mm.
        public float trackHeight = 0.1f / 25.4f; // Just some height that's visually appealing
        
        Vector3 copperMaxPoint = new Vector3(float.NegativeInfinity);
        Vector3 copperMinPoint = new Vector3(float.PositiveInfinity);

        private void MeshMinMax(List<Triangle> mesh, ref Vector3 max, ref Vector3 min)
        {
            foreach (Triangle t in mesh)
            {
                foreach (Vector3 v in t.Vertices)
                {
                    min.X = Math.Min(min.X, v.X);
                    min.Y = Math.Min(min.Y, v.Y);
                    min.Z = Math.Min(min.Z, v.Z);

                    max.X = Math.Max(max.X, v.X);
                    max.Y = Math.Max(max.Y, v.Y);
                    max.Z = Math.Max(max.Z, v.Z);
                }
            }
        }

        PolyTree SubtractPolyTree(PolyTree original, PolyTree remove)
        {
            PolyTree result = new PolyTree();
            Clipper c = new Clipper();
            c.AddPaths(Clipper.PolyTreeToPaths(original), PolyType.ptSubject, true);
            c.AddPaths(Clipper.PolyTreeToPaths(remove), PolyType.ptClip, true);
            c.Execute(ClipType.ctDifference, result, PolyFillType.pftNonZero, PolyFillType.pftNonZero);
            return result;
        }
        PolyTree boardPolyTree;
        bool CalculateMeshes()
        {
            bool updated = false;
            bool hasBoardLayer = false;
            foreach (LayerAssignedGerber l in layers)
            {
                if (l.mesh == null && l.loader != null)
                {
                    updated = true;
                    if (l.layer == Layer.TopCopper)
                    {
                        PolyTree tree = l.loader.finalPolyTree;
                        tree = drillData != null ? SubtractPolyTree(tree, drillData) : tree;
                        
                        Console.WriteLine("Starting mesh generation for " + l.layer);
                        Stopwatch sw = new Stopwatch();
                        sw.Start();

                        Vector3 location = new Vector3(0, 0, (float)boardHeight + trackHeight);
                        
                        // Generate horizontal triangles
                        l.mesh = new List<Triangle>();
                        GetPolyTreeTriangles(tree, ref l.mesh, location, false);
                        sw.Stop();
                        int triCount = l.mesh.Count;

                        Console.WriteLine("Adding Horizontal Triangles Done At: " + sw.ElapsedMilliseconds +
                            ", " + triCount + " triangles");

                        sw.Reset();
                        sw.Start();

                        // min/max Z coordinate is not used, and min/max XY
                        // of the boundary is the same as the horizontal triangles.
                        MeshMinMax(l.mesh, ref copperMaxPoint, ref copperMinPoint);
                        sw.Stop();
                        Console.WriteLine("MinMax of mesh took " + sw.ElapsedMilliseconds + " milliseconds");

                        sw.Reset();
                        sw.Start();
                        GetBoundaryTriangles(tree, -trackHeight, ref l.mesh, location);
                        sw.Stop();
                        
                        Console.WriteLine("Adding Boundary Triangles Done At: " + sw.ElapsedMilliseconds +
                            ", " + (l.mesh.Count - triCount) + " triangles");

                    }
                    else if (l.layer == Layer.BottomCopper)
                    {
                        PolyTree tree = l.loader.finalPolyTree;
                        tree = drillData != null ? SubtractPolyTree(tree, drillData) : tree;

                        Vector3 location = new Vector3(0, 0, -trackHeight);

                        // Generate horizontal triangles
                        l.mesh = new List<Triangle>();
                        GetPolyTreeTriangles(tree, ref l.mesh, location, true);
                        MeshMinMax(l.mesh, ref copperMaxPoint, ref copperMinPoint);
                        GetBoundaryTriangles(tree, -trackHeight, ref l.mesh, Vector3.Zero);

                        //l.mesh = new TriangleMesh();
                        //foreach (Triangle t in l.loader.triangles.Triangles)
                        //{
                        //    Triangle flipped = new Triangle(t.A, t.C, t.B);
                        //    l.mesh.AddTriangle(flipped);
                        //}
                        //foreach (Triangle t in GetBoundaryTriangles(l.loader.finalPolyTree, trackHeight))
                        //{
                        //    l.mesh.AddTriangle(t);
                        //}
                        //l.mesh.Transformation = Matrix4.CreateTranslation(0, 0, -trackHeight);
                    }
                    else if (l.layer == Layer.BoardEdge)
                    {
                        // Generate a board-like object.  The board edge is defined as a thick line
                        // indicating that's where the cut should occur (whole line removed, I guess?)
                        // So keep the first hole and any direct children it has.
                        Clipper c = new Clipper();
                        PolyNode n = l.loader.finalPolyTree;
                        foreach (PolyNode outer1 in n.Childs)
                        {
                            foreach (PolyNode outer2 in outer1.Childs)
                            {
                                if (outer2.IsHole)
                                {
                                    // The outside contour should be reversed, but PolyFillType.pftNonZero
                                    // doesn't care.
                                    c.AddPath(outer2.Contour, PolyType.ptSubject, true);
                                    foreach (PolyNode c2 in outer2.Childs)
                                    {
                                        c.AddPath(c2.Contour, PolyType.ptSubject, true);
                                    }
                                }
                            }
                        }

                        boardPolyTree = new PolyTree();
                        c.Execute(ClipType.ctDifference, boardPolyTree, PolyFillType.pftNonZero, PolyFillType.pftNonZero);

                        boardPolyTree = drillData != null ? SubtractPolyTree(boardPolyTree, drillData) : boardPolyTree;

                        Vector3 location = new Vector3(0, 0, 0);
                        l.mesh = new List<Triangle>();
                        GetPolyTreeTriangles(boardPolyTree, ref l.mesh, location, true); // Bottom of board
                        MeshMinMax(l.mesh, ref copperMaxPoint, ref copperMinPoint);
                        location = new Vector3(0, 0, (float)boardHeight);
                        GetPolyTreeTriangles(boardPolyTree, ref l.mesh, location, false); // Top of board
                        GetBoundaryTriangles(boardPolyTree, -(float)boardHeight, ref l.mesh, location);
                    }
                }
            
                if (l.layer == Layer.BoardEdge && l.mesh != null)
                {
                    hasBoardLayer = true;
                    boardMesh = null; // Remove the board mesh, if there was one.
                }
            }

            if (!hasBoardLayer && updated && (copperMinPoint - copperMaxPoint).Length > 0)
            {
                Vector3 min = copperMinPoint;
                Vector3 max = copperMaxPoint;
                min.Z = 0;
                max.Z = 0;
                min.X -= 0.1f;
                min.Y -= 0.1f;
                max.X += 0.1f;
                max.Y += 0.1f;
                Vector3 b = new Vector3(max.X, min.Y, 0);
                Vector3 c = new Vector3(min.X, max.Y, 0);


                Clipper clipper = new Clipper();
                List<IntPoint> path = new List<IntPoint>() {
                    new IntPoint(Polygon2D.ToIntSpace(min)),
                    new IntPoint(Polygon2D.ToIntSpace(b)),
                    new IntPoint(Polygon2D.ToIntSpace(max)),
                    new IntPoint(Polygon2D.ToIntSpace(c)),
                };
                clipper.AddPath(path, PolyType.ptSubject, true);

                if (drillData != null)
                {
                    clipper.AddPaths(Clipper.PolyTreeToPaths(drillData), PolyType.ptClip, true);
                }

                boardPolyTree = new PolyTree();
                clipper.Execute(ClipType.ctDifference, boardPolyTree, PolyFillType.pftNonZero, PolyFillType.pftNonZero);
                Polygon2D p = new Polygon2D();
                p.polygons = Clipper.PolyTreeToPaths(boardPolyTree);

                boardMesh = new TriangleMesh();

                foreach (Triangle t in GetBoundaryTriangles(boardPolyTree, (float)boardHeight))
                {
                    boardMesh.AddTriangle(t);
                }

                Vector3 h = new Vector3(0, 0, (float)boardHeight);
                foreach (Triangle t in p.EarClipForTriangles())
                {
                    Triangle atTop = new Triangle(t.A + h, t.B + h, t.C + h);
                    Triangle atBottom = new Triangle(t.A, t.C, t.B); // Reversed order
                    boardMesh.AddTriangle(atTop);
                    boardMesh.AddTriangle(atBottom);
                }
            }

            if (updated && needsInitialOffset)
            {
                offset.X = 1.0f - copperMinPoint.X;
                offset.Y = 1.0f - copperMinPoint.Y;
                needsInitialOffset = false;
            }
            return updated;
        }

        private void DrawTriangles()
        {
            try
            {
                if (boardMesh != null)
                {
                    Color horizontalFaceColor = Color.DarkGreen;
                    Color verticalFaceColor = Color.SandyBrown;
                    GL.Begin(PrimitiveType.Triangles);
                    foreach (Triangle t in boardMesh.Triangles)
                    {
                        Vector3 normal = t.Plane.Normal;
                        if (Math.Abs(normal.Z) > 0.9f)
                        {
                            GL.Color3(horizontalFaceColor);
                        }
                        else
                        {
                            GL.Color3(verticalFaceColor);
                        }
                        GL.Normal3(t.Plane.Normal);
                        foreach (Vector3 v in t.Vertices)
                        {
                            GL.Vertex3(v);
                        }
                    }
                    GL.End();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("GerberGUI Draw Exception: " + ex.Message);
            }



            try
            {
                foreach (LayerAssignedGerber l in layers)
                {
                    if (l.mesh == null)
                    {
                        continue;
                    }

                    // Colors for the board
                    Color horizontalFaceColor = Color.DarkGreen;
                    Color verticalFaceColor = Color.SandyBrown;

                    // Colors for the copper layers
                    if (l.layer == Layer.TopCopper || l.layer == Layer.BottomCopper)
                    {
                        horizontalFaceColor = Color.FromArgb(0xB8, 0x73, 0x33); // Copper?
                        verticalFaceColor = Color.SaddleBrown;
                    }

                    GL.Begin(PrimitiveType.Triangles);
                    foreach (Triangle t in l.mesh)
                    {
                        Vector3 normal = t.Plane.Normal;
                        if (Math.Abs(normal.Z) > 0.9f)
                        {
                            GL.Color3(horizontalFaceColor);
                        }
                        else
                        {
                            GL.Color3(verticalFaceColor);
                        }
                        GL.Normal3(t.Plane.Normal);
                        foreach (Vector3 v in t.Vertices)
                        {
                            GL.Vertex3(v);
                        }
                    }
                    GL.End();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("GerberGUI Draw Exception: " + ex.Message);
            }
        }

        private bool UseDisplayLists = false;
        int thisDisplayList = -1;
        void IOpenGLDrawable.Draw()
        {
            bool updated = CalculateMeshes();

            GL.PushMatrix();
            GL.Translate(offset);

            if (UseDisplayLists)
            {
                if (thisDisplayList == -1)
                {
                    thisDisplayList = GL.GenLists(1);
                    GL.NewList(thisDisplayList, ListMode.CompileAndExecute);
                    DrawTriangles();
                    GL.EndList();
                }
                else
                {
                    if (updated)
                    {
                        GL.DeleteLists(thisDisplayList, 1);
                        GL.NewList(thisDisplayList, ListMode.CompileAndExecute);
                        DrawTriangles();
                        GL.EndList();
                    }
                    else
                    {
                        GL.CallList(thisDisplayList);
                    }
                }
            }
            else
            {
                DrawTriangles();
            }

            GL.PopMatrix();
        }


        private bool isPointedAt = false;
        private Vector3 mouseHoverPoint = Vector3.Zero;
        private List<TabsGUI> tabs = new List<TabsGUI>();
        private Vector3 offset = Vector3.Zero;
        Vector3 mouseDownPoint = Vector3.Zero;
        Vector3 mouseDownOffset = Vector3.Zero;
        private bool hovered = false;
        private object hoveredMesh;
        //Edge closestEdge = null;
        Triangle hoveredTriangle = null;

        void IClickable3D.MouseDown(Ray pointer)
        {
            mouseDownPoint = mouseHoverPoint;
            mouseDownOffset = offset;
            //Console.WriteLine("Mouse Down TriMeshGUI");
        }

        void IClickable3D.MouseUp(Ray pointer)
        {
        }

        

        float IClickable3D.DistanceToObject(Ray pointer)
        {
            Ray adjustedPointer = new Ray(pointer.Start - offset, pointer.Direction);
            hovered = false;
            float distance = float.PositiveInfinity;
            hoveredTriangle = null;
            hoveredMesh = null;

            //if (!isTransforming)
            //{
            try
            {

                foreach (var l in layers)
                {
                    if (l.mesh == null)
                    {
                        continue;
                    }
                    foreach (Triangle t in l.mesh)
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
                                hoveredMesh = l;
                            }
                        }
                    }
                }

                if (boardMesh != null)
                {
                    foreach (Triangle t in boardMesh.Triangles)
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
                                hoveredMesh = boardMesh;
                            }
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine("GerberGUI DistanceToObject exception: " + ex.Message);
            }
            //}

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
            
            offset = mouseDownOffset + point - mouseDownPoint;
        }

        string[] IRightClickable3D.MouseRightClick(Ray pointer)
        {
            List<string> menuItems = new List<string>();

            var hoveredLayer = hoveredMesh as LayerAssignedGerber;
            if (hoveredLayer != null)
            {
                if (hoveredLayer.layer == Layer.TopCopper)
                {
                    menuItems.Add("Generate Isolation Routing Paths");
                    menuItems.Add("Move to Bottom Copper");
                    menuItems.Add("Move to Board Edge");
                }
                else if (hoveredLayer.layer == Layer.BottomCopper)
                {
                    menuItems.Add("Generate Isolation Routing Paths");
                    menuItems.Add("Move to Top Copper");
                    menuItems.Add("Move to Board Edge");
                }
                else if (hoveredLayer.layer == Layer.BoardEdge)
                {
                    menuItems.Add("Generate Edge Cut Routing Paths");
                    menuItems.Add("Change Board Thickness<ACTION=SELECT_FLOAT,VALUE=" + boardHeight + ">");
                    menuItems.Add("Move to Top Copper");
                    menuItems.Add("Move to Bottom Copper");
                }
                menuItems.Add("Delete");

                //else if (hoveredMesh == boardMesh)
                //{
                //    menuItems.Add("Flip along X Axis");
                //    menuItems.Add("Flip along Y Axis");
                //}
            }
            else
            {
                var hoveredBoardMesh = hoveredMesh as TriangleMesh;
                if (hoveredBoardMesh != null)
                {
                    menuItems.Add("Generate Edge Cut Routing Paths");
                    menuItems.Add("Change Board Thickness<ACTION=SELECT_FLOAT,VALUE=" + boardHeight + ">");
                }
            }
            return menuItems.ToArray();
        }

        void IRightClickable3D.MouseRightClickSelect(string result)
        {
            if (result.StartsWith("Change Board Thickness"))
            {
                if (double.TryParse(result.Remove(0, 22), out double newHeight))
                {
                    if (newHeight > 0.001 && newHeight < 1000)
                    {
                        boardHeight = newHeight;
                        ForceMeshRecompute();
                    }
                }
                return;
            }

            var hoveredLayer = hoveredMesh as LayerAssignedGerber;
            if (hoveredLayer != null)
            {
                
                if (result == "Generate Isolation Routing Paths")
                {
                    AddIsolationPaths(hoveredLayer);
                }
                else if (result == "Generate Edge Cut Routing Paths")
                {
                    AddEdgeCutPaths();
                }
                else if (result == "Delete")
                {
                    hoveredLayer.loader = null;
                    hoveredLayer.mesh = null;
                    if (hoveredLayer.layer == Layer.BoardEdge)
                    {
                        // Also remove any drill file
                        drillData = null;
                    }
                    ForceMeshRecompute();
                }
                else
                {
                    if (result == "Move to Bottom Copper")
                    {
                        hoveredLayer.layer = Layer.BottomCopper;
                    }
                    else if (result == "Move to Top Copper")
                    {
                        hoveredLayer.layer = Layer.TopCopper;
                    }
                    else if (result == "Move to Board Edge")
                    {
                        hoveredLayer.layer = Layer.BoardEdge;
                    }

                    // This will force mesh recalculation
                    hoveredLayer.mesh = null;
                }

            }
            else if (hoveredMesh is TriangleMesh) // Hovered over the default board mesh
            {
                if (result == "Generate Edge Cut Routing Paths")
                {
                    AddEdgeCutPaths();
                }
            }
        
        }
    }
}
