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

namespace Router.Paths
{
    public class PathPlanner
    {
        /// <summary>
        /// Find a toolpath which will remove everything described in the slice.
        /// </summary>
        /// <param name="slice"></param>
        public static void PlanPaths(Slice slice)
        {
            // 1. Get all small holes in the slice - rout them first
            // 2. Rout everything remaining in the slice from 
        }

        public static List<LineStrip> PlanPaths(TriangleMesh triangles, List<Tabs> tabs, Router router)
        {
            List<LineStrip> routs = new List<LineStrip>();

            float toolRadius = router.ToolDiameter / 2.0f; // Router units are inches
            float maxCutDepth = router.MaxCutDepth;
            float lastPassHeight = router.LastPassHeight;
            float cleanPassFactor = 0.90f; // 90% of the tool radius will be removed on the clean pass

            float minZ = triangles.MinPoint.Z;
            float maxZ = triangles.MaxPoint.Z;

            Slice boundary = new Slice(triangles, new Plane(Vector3.UnitZ, new Vector3(0, 0, minZ)));
            boundary.Offset(toolRadius * (cleanPassFactor + 1.05f)); // Note: this is slightly larger to allow some polygon width to exist
            foreach (var tab in tabs)
            {
                if (tab.TabLocations.Count() == 0)
                {
                    boundary.Add(tab.Boundary);
                }
            }

            Slice top = new Slice(triangles, new Plane(Vector3.UnitZ, new Vector3(0, 0, maxZ)));
            top.SubtractFrom(boundary);

            //GL.PushMatrix();
            //GL.Translate(0, 0, 1);
            //DrawSlice(Color.Black, Color.DarkGray, boundary);
            //GL.PopMatrix();

            Slice holes = top.PolygonsWithoutHoles();
            

            List<Hole> holeRouts = new List<Hole>();
            foreach (var polygon in holes.IndividualPolygons())
            {
                holeRouts.Add(new Hole(polygon, toolRadius, cleanPassFactor));
            }

            // Figure out a nice even maximum cut depth
            int layers = (int)((maxZ - minZ) / maxCutDepth + 0.95f);
            float actualCutDepth = (maxZ - minZ) / layers;
            
            for (float height = minZ; height < maxZ; height += actualCutDepth)
            {
                Slice current = new Slice(triangles, new Plane(Vector3.UnitZ, new Vector3(0, 0, height)));
                current.Offset(toolRadius);
                GL.PushMatrix();
                GL.Translate(0, 0, -0.001f);
                //DrawSlice(Color.Tan, Color.Gray, boundary);
                GL.PopMatrix();
                //DrawSlice(Color.Red, Color.Blue, current);

                
                Slice original = new Slice(current);
                current.SubtractFrom(boundary);

                // current will now be several polygons representing the area to rout out, minus the tool radius offset on either side.
                // Split it into polygons around the outside and inside of parts (the first two will be outside polygons, the next two inside, next two outside, ...).
                Slice outsidePairs = current.GetOutsidePairs();
                //DrawSlice(Color.Gold, Color.Yellow, outsidePairs);

                Slice insidePairs = current.GetInsidePairs();
                //DrawSlice(Color.Orange, Color.NavajoWhite, insidePairs);
                

                // If a polygon has no holes, that means it's a hole in the actual shape to be cut out.
                // These can be cut first before outside cuts are done.
                holes = current.PolygonsWithoutHoles();
                foreach (var holePolygon in insidePairs.PolygonsWithoutHoles().IndividualPolygons())
                {
                    foreach (var hole in holeRouts)
                    {
                        if (hole.Contains(holePolygon))
                        {
                            hole.AddPolygon(holePolygon);
                            break;
                        }
                    }
                }

            
                // Rout all outside paths.  These will be done from top down, one layer at a time for structural reasons.
                // For the top several layers, two paths could be combined...
                //var withHoles = outsidePairs.PolygonsWithHoles();
                var outsideRouts = RoutAreasWithHoles(outsidePairs, toolRadius, cleanPassFactor, tabs, false);
                var newLines = new List<LineStrip>();
                foreach (var line in outsideRouts)
                {
                    var r = new LineStrip();
                    r.AddRange(line.Vertices);
                    r.Append(line.Vertices[0]);
                    newLines.Add(r);
                }
                routs.InsertRange(0, newLines);


                outsideRouts = RoutAreasWithHoles(insidePairs.PolygonsWithHoles(), toolRadius, cleanPassFactor, tabs, true);
                newLines = new List<LineStrip>();
                foreach (var line in outsideRouts)
                {
                    var r = new LineStrip();
                    r.AddRange(line.Vertices);
                    r.Append(line.Vertices[0]);
                    newLines.Add(r);
                }
                routs.InsertRange(0, newLines);
            }

            foreach (var hole in holeRouts)
            {
                var newRouts = new List<LineStrip>();
                foreach (var line in hole.GetRouts())
                {
                    LineStrip r = new LineStrip();
                    // Note: these might not start and end in the same place, but that's OK.
                    r.AddRange(line.Vertices);
                    newRouts.Add(r);
                }
                routs.InsertRange(0, newRouts);
            }

            // Adjust the lowest point - allow plunging through the bottom of the material for a clean cut.
            foreach (LineStrip r in routs)
            {
                for (int i = 0; i < r.Vertices.Count; i++)
                {
                    var point = r.Vertices[i];
                    if (point.Z < (minZ + .0001f))
                    {
                        r.Vertices[i] = new Vector3(point.X, point.Y, lastPassHeight);
                    }
                }
            }
            return routs;
        }

        /// <summary>
        /// Generate paths to remove the area described in polygons.  The last path will be against the surface of the material, determined
        /// by the "inside" parameter.  If inside is true, the last pass will be the outer-most pass.  Otherwise it will be the inner most.
        /// The cleanPassFactor is respected on the last pass: only toolRadius * cleanPassFactor width of material will be removed.
        /// </summary>
        /// <param name="polygons"></param>
        /// <param name="toolRadius"></param>
        /// <param name="cleanPassFactor"></param>
        /// <param name="tabs"></param>
        /// <param name="inside"></param>
        /// <returns></returns>
        private static List<LineStrip> RoutAreasWithHoles(Slice polygons, float toolRadius, float cleanPassFactor, List<Tabs> tabs, bool inside)
        {
            List<LineStrip> paths = new List<LineStrip>();
            foreach (var individualPolygon in polygons.IndividualPolygons())
            {
                paths.AddRange(RoutAreasWithHolesHelper(individualPolygon, toolRadius, cleanPassFactor, tabs, inside));
            }
            return paths;
        }
        private static List<LineStrip> RoutAreasWithHolesHelper(Slice polygons, float toolRadius, float cleanPassFactor, List<Tabs> tabs, bool inside)
        {
            List<LineStrip> paths = new List<LineStrip>();

            Slice.LineType firstLineType = Slice.LineType.Hole;
            Slice.LineType lastLineType = Slice.LineType.Outside;
            float offset = toolRadius * cleanPassFactor;
            if (inside)
            {
                firstLineType = Slice.LineType.Outside;
                lastLineType = Slice.LineType.Hole;
                offset = -offset;
            }

            

            // The holes are the paths surrounding the final object - rout them last for the cleanest finish.
            // Also avoid any tabs that may exist on these paths.
            Slice lastPaths = new Slice(polygons.GetLines(firstLineType), polygons.Plane);
            
            var routLast = lastPaths.GetLines(Slice.LineType.All);
            foreach (var line in routLast)
            {
                var fixedLine = line;
                foreach (Tabs t in tabs)
                {
                    fixedLine = t.AvoidTabs(fixedLine);
                }
                paths.Add(fixedLine);
            }

            // Compute the rest of the routs required to remove the material.  Do these first - they will
            // be a distance away from the final product, so if forces push the bit around some, there's no issue.
            // Also avoid tabs on these paths.
            lastPaths.Offset(offset);

            Slice obliterate = new Slice(polygons.GetLines(lastLineType), polygons.Plane);
            if (inside)
            {
                obliterate.SubtractFrom(lastPaths);
            }
            else
            {
                obliterate.Subtract(lastPaths);
            }
            
            //DrawSlice(Color.Orange, Color.Red, obliterate);
            
            var lines = PathTree.ObliterateSlice(obliterate, toolRadius);
            
            foreach (var line in lines)
            {
                var fixedLine = line;
                foreach (Tabs t in tabs)
                {
                    fixedLine = t.AvoidTabs(fixedLine);
                }
                paths.Insert(0, fixedLine);
            }

            return paths;
        }

        private static void DrawSlice(Color lineColor, Color planeColor, Slice s)
        {
            GL.Disable(EnableCap.Lighting);
            GL.Color3(lineColor);
            foreach (var line in s.GetLines(Slice.LineType.All))
            {
                GL.Begin(PrimitiveType.LineLoop);
                foreach (var p in line.Vertices)
                {
                    GL.Vertex3(p);
                }
                GL.End();
            }
            GL.PointSize(2);
            GL.Color3(Color.Black);
            foreach (var line in s.GetLines(Slice.LineType.All))
            {
                GL.Begin(PrimitiveType.Points);
                foreach (var p in line.Vertices)
                {
                    GL.Vertex3(p);
                }
                GL.End();
            }
            GL.PointSize(1);
            GL.Enable(EnableCap.Lighting);

            GL.Color3(planeColor);
            GL.Begin(PrimitiveType.Triangles);
            GL.Normal3(s.Plane.Normal);
            foreach (var t in s.Triangles())
            {
                foreach (var p in t.Vertices)
                {
                    GL.Vertex3(p);
                }
            }
            GL.End();
        }

        #region Helper Classes

        // Contains polygons at various heights which are all in the same hole
        public class Hole
        {
            public Slice topPolygon;
            public List<Slice> polygons;
            public float toolRadius;
            public float cleanRoutFactor;

            public Hole(Slice top, float toolRadius, float cleanRoutFactor)
            {
                topPolygon = top;
                polygons = new List<Slice>();
                this.cleanRoutFactor = cleanRoutFactor;
                this.toolRadius = toolRadius;
            }

            public void AddPolygon(Slice polygon)
            {
                polygons.Add(polygon);
            }

            public List<LineStrip> GetRouts()
            {
                GL.PushMatrix();
                Slice lastPolygon = null;

                for (int i = polygons.Count - 1; i >= 0; i--)
                {
                    var p = polygons[i];
                    var routLast = p.GetLines(Slice.LineType.Outside).First(s => true);
                    routLast.Vertices.Add(routLast.Vertices[0]);

                    Slice obliterate = new Slice(p);
                    obliterate.Offset(-toolRadius * cleanRoutFactor);

                    var routFirst = PathTree.ObliterateSlice(obliterate, toolRadius * 2.0f);

                    if (lastPolygon == null)
                    {
                        lastPolygon = p;
                    }

                    bool first = true;
                    foreach (var rout in routFirst)
                    {
                        first = true;
                        foreach (var point in rout.Vertices)
                        {
                            AddRoutPoint(lastPolygon, point, first);
                            first = false;
                        }
                    }

                    first = true;
                    foreach (var point in routLast.Vertices)
                    {
                        AddRoutPoint(p, point, first);
                        first = false;
                    }
                    lastPolygon = p;
                }
                GL.PopMatrix();
                return routs;
            }

            private List<LineStrip> routs = new List<LineStrip>();
            private void AddRoutPoint(Slice currentPolygon, Vector3 newPoint, bool check = true)
            {
                if (routs.Count == 0)
                {
                    routs.Add(new LineStrip());
                }
                LineStrip currentRout = routs[routs.Count-1];
                if (check)
                {
                    if (currentRout.Vertices.Count > 0)
                    {
                        Slice larger = new Slice(currentPolygon);
                        larger.Offset(toolRadius * 1.05f);
                        Vector3 lastPoint = currentRout.Vertices[currentRout.Vertices.Count - 1];
                        LineStrip path = new LineStrip();
                        path.Append(lastPoint);
                        path.Append(newPoint);
                        Slice test = new Slice(path, toolRadius * 2.0f, currentPolygon.Plane);


                        if (!larger.Contains(test))
                        {
                            // Can't move at this level - need to go to the save Z move height.
                            currentRout = new LineStrip();
                            routs.Add(currentRout);
                            //DrawSlice(Color.Black, Color.Red, test);
                        }
                        else
                        {
                            //DrawSlice(Color.Black, Color.Green, test);
                        }
                        //larger.Subtract(test);
                        //GL.Translate(0, 0, 100);
                    }
                }
                // If the Z height changed, move to the new position and and drop, or rise and then move.
                if (currentRout.Vertices.Count > 0)
                {
                    var lastPoint = currentRout.Vertices[currentRout.Vertices.Count - 1];
                    if (newPoint.Z < lastPoint.Z)
                    {
                        currentRout.Vertices.Add(new Vector3(newPoint.X, newPoint.Y, lastPoint.Z));
                    }
                    else if (newPoint.Z > lastPoint.Z)
                    {
                        currentRout.Vertices.Add(new Vector3(lastPoint.X, lastPoint.Y, newPoint.Z));
                    }
                }
                currentRout.Vertices.Add(newPoint);
            }

            public bool Contains(Slice p)
            {
                return (topPolygon.Contains(p));
            }
        }

        private class PathTree
        {
            private Slice slice;
            private List<PathTree> children = new List<PathTree>();
            private List<PathTree> badTrees;

            #region Public Methods

            /// <summary>
            /// Generate a set of tool paths which will completely remove the material specified in
            /// the polygons, plus an offset equal to the radius of the tool used.
            /// </summary>
            /// <param name="polygons"></param>
            /// <param name="maxShrink">maximum distance between disjoint paths</param>
            /// <returns></returns>
            public static List<LineStrip> ObliterateSlice(Slice polygons, float maxShrink)
            {
                List<LineStrip> lines = new List<LineStrip>();

                foreach (Slice slice in polygons.IndividualPolygons())
                {
                    PathTree tree = new PathTree();
                    Slice inside = new Slice(slice.GetLines(Slice.LineType.Hole), slice.Plane);
                    Slice shrink = new Slice(slice);
                    while (shrink.Area() > 0)
                    {
                        shrink = new Slice(shrink.GetLines(Slice.LineType.Outside), shrink.Plane);
                        foreach (var a in shrink.IndividualPolygons())
                        {
                            if (!tree.AddPolygon(a))
                            {
                                // The new polygon didn't fit into the path tree...  shouldn't get here.
                            }
                        }

                        shrink.Offset(-maxShrink);
                        shrink.Subtract(inside);
                    }

                    LineStrip toolPath = new LineStrip();
                    tree.GenerateToolPath(toolPath, tree.CreatePath(), maxShrink * 2.0f);
                    lines.Add(toolPath);
                }
                return lines;
            }

            #endregion

            #region Private Methods

            private PathTree()
            {
                slice = null;
                badTrees = new List<PathTree>();
            }

            private PathTree(Slice slice, PathTree parent)
            {
                this.badTrees = parent.badTrees;
                this.slice = slice;
            }

            private LineStrip CreatePath()
            {
                return slice.GetLines(Geometry.Slice.LineType.Outside).First(s => true);
            }

            private void Draw(Color lineColor, Color planeColor)
            {
                GL.PushMatrix();
                GL.Translate(0, 0, 10);
                if (slice != null)
                {
                    DrawSlice(lineColor, planeColor, slice);
                }
                
                foreach (var t in children)
                {
                    t.Draw(lineColor, planeColor);
                }
                GL.PopMatrix();
            }

            /// <summary>
            /// Generate a tool path from the current path tree
            /// </summary>
            /// <param name="start">line strip to add path information</param>
            /// <param name="thisPath">line strip representing the path at the current level</param>
            /// <param name="maxDistance">maximum distance to jump from a parent path to a child path</param>
            private void GenerateToolPath(LineStrip start, LineStrip thisPath, float maxDistance)
            {
                List<PathTree> childs = new List<PathTree>();
                foreach (PathTree child in children)
                {
                    childs.Add(child);
                }
                
                var pathVertices = thisPath.Vertices.Count;
                for (int i = 0; i < pathVertices; i++)
                {
                    var p1 = thisPath.Vertices[i];
                    var p2 = thisPath.Vertices[(i + 1) % pathVertices];

                    Segment parentSegment = new Segment(p1, p2);
                    start.Append(p1);

                    int childIndex = 0;
                    int closestIndex = 0;

                    for (int childTreeIndex = 0; childTreeIndex < childs.Count; childTreeIndex++)
                    {
                        var child = childs[childTreeIndex];
                        bool found = false;
                        var childPath = child.CreatePath();
                        var childVertices = childPath.Vertices.Count;

                        // Prefer point to point matches
                        var closest = maxDistance;
                        for (childIndex = 0; childIndex < childVertices; childIndex++)
                        {
                            var c1 = childPath.Vertices[childIndex];
                            var len = (c1 - p1).Length;
                            if (len < closest)
                            {
                                closest = len;
                                closestIndex = childIndex;
                                found = true;
                            }
                        }

                        // If a point to point match isn't found, look for line to point matches.
                        if (!found)
                        {
                            closest = maxDistance;
                            Vector3 insertPoint = Vector3.Zero;
                            bool insertParent = false;

                            // If there is no point to point match, find a point to line match.
                            for (childIndex = 0; childIndex < childVertices; childIndex++)
                            {
                                var c1 = childPath.Vertices[childIndex];
                                var c2 = childPath.Vertices[(childIndex + 1) % childVertices];
                                Segment childSegment = new Segment(c1, c2);

                                var fromParentSegment = parentSegment.DistanceTo(c1);
                                if (fromParentSegment < closest)
                                {
                                    insertParent = true;
                                    closest = fromParentSegment;
                                    insertPoint = parentSegment.PointOnLine;
                                    closestIndex = childIndex;
                                    found = true;
                                }

                                var fromChildSegment = childSegment.DistanceTo(p1);
                                if (fromChildSegment < closest)
                                {
                                    // Note: this happens very rarely
                                    insertParent = false;
                                    closest = fromChildSegment;
                                    insertPoint = childSegment.PointOnLine;
                                    closestIndex = childIndex;
                                    found = true;
                                }
                            }
                            if (found)
                            {
                                if (insertParent)
                                {
                                    p1 = insertPoint;
                                    start.Append(p1);
                                }
                                else
                                {
                                    closestIndex++;
                                    childPath.Vertices.Insert(closestIndex, insertPoint);
                                }
                            }
                        }

                        if (found)
                        {
                            // Reorder the child vertices
                            var last = childPath.Vertices.GetRange(0, closestIndex);
                            childPath.Vertices.RemoveRange(0, closestIndex);
                            childPath.Vertices.AddRange(last);

                            child.GenerateToolPath(start, childPath, maxDistance);
                            start.Append(p1);
                            
                            childs.RemoveAt(childTreeIndex);
                            childTreeIndex--;
                        }
                    }
                }

                if (childs.Count > 0)
                {
                    // No path to these children - need to handle them some other way
                    badTrees.AddRange(childs);
                }

                // Complete the loop
                start.Append(thisPath.Vertices[0]);
            }

            private bool AddPolygon(Slice slice)
            {
                if (this.slice == null)
                {
                    this.slice = slice;
                    return true;
                }

                if (this.slice.Contains(slice))
                {
                    foreach (var t in children)
                    {
                        if (t.AddPolygon(slice))
                        {
                            return true;
                        }
                    }
                    PathTree newTree = new PathTree(slice, this);
                    children.Add(newTree);
                    return true;
                }
                return false;
            }

            #endregion

            #region Helper Classes

            // Helper class for finding the shortest distance from a point to a plane
            private class Segment
            {
                private Vector3 a;
                private Vector3 b;
                private Vector3 pointOnLine;

                public Segment(Vector3 a, Vector3 b)
                {
                    this.a = a;
                    this.b = b;
                    pointOnLine = Vector3.Zero;
                }

                public Vector3 PointOnLine
                {
                    get { return pointOnLine; }
                }
                

                public float DistanceTo(Vector3 point)
                {
                    float distanceFromA3 = new Plane(a - b, a).Distance(point);
                    float distanceFromB3 = new Plane(b - a, b).Distance(point);

                    if (distanceFromA3 > 0 || distanceFromB3 > 0)
                    {
                        return float.PositiveInfinity;
                    }
                    
                    pointOnLine = (a * distanceFromB3 + b * distanceFromA3) / (distanceFromA3 + distanceFromB3);
                    
                    // Compute a normal perpendicular to the line and pointing to the point
                    Vector3 up = Vector3.Cross(a - point, a - b); // This points up from the line
                    Vector3 normal = Vector3.Cross(up, a - b);
                    float distanceToLine = Math.Abs(new Plane(normal, a).Distance(point));

                    return distanceToLine;
                }
            }

            #endregion
        }

        #endregion


    }
}
