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
using OpenTK;
using OpenTK.Graphics.OpenGL;

namespace Router.Paths
{
    public class Tabs
    {
        private LineStrip boundary;
        private int minTabs = 3;
        private float desiredSpacing = 2.500f;
        protected float tabRadius = 0.200f;
        protected float toolRadius;
        protected List<Vector3> tabLocations;
        protected float tabHeight = 0.050f;
        private Slice originalBoundary;
        

        public Tabs(LineStrip boundary, float toolRadius, bool inside = false)
        {
            originalBoundary = new Slice(new LineStrip[] { boundary }, new Plane(Vector3.UnitZ, Vector3.Zero));
            float offset = toolRadius;
            if (inside)
            {
                offset = -offset;
            }
            Slice slice = new Slice(originalBoundary);
            slice.Offset(offset);
            this.boundary = slice.GetLines(Slice.LineType.Outside).First(s => true);
            this.toolRadius = toolRadius;

            float length = this.boundary.Length(LineStrip.Type.Closed);
            int numTabs = (int)(length / desiredSpacing);
            if (numTabs < minTabs)
            {
                numTabs = 0;
            }


            float tabSpacing = length / numTabs;

            tabLocations = new List<Vector3>();
            foreach (var point in this.boundary.PointsAlongLine(tabSpacing, tabSpacing / 2.0f))
            {
                tabLocations.Add(point);
            }
        }

        public Slice Boundary
        {
            get { return originalBoundary; }
        }

        public LineStrip TabPath
        {
            get { return boundary; }
        }

        public Vector3 ClearHeight(Vector3 test, float height)
        {
            test.Z = Math.Max(test.Z, height);
            return test;
        }

        public IEnumerable<Vector3> TabLocations
        {
            get
            {
                foreach (var tab in tabLocations)
                {
                    yield return tab;
                }
            }
        }

        /// <summary>
        /// Create another line strip which follows the same path, but avoids tab locations.
        /// NOTE: this currently only works on closed input lines.  The algorithm could
        /// be modified to work correctly with open paths too, but that's not needed yet.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public LineStrip AvoidTabs(LineStrip input)
        {
            LineStrip ret = new LineStrip();
            foreach (var segment in input.Segments(LineStrip.Type.Closed))
            {
                if (segment.Length < 0.0001f)
                {
                    continue;
                }
                if (segment.A.Z > tabHeight && segment.B.Z > tabHeight)
                {
                    ret.Append(segment.B);
                    continue;
                }
                List<LineSegment> remainingSegments = new List<LineSegment>();
                remainingSegments.Add(segment);
                foreach (Vector3 tab in this.TabLocations)
                {
                    var i = new LineSegmentCircleIntersect(segment, tab, tabRadius + toolRadius);
                    if (i.type == LineSegmentCircleIntersect.IntersectType.Segment)
                    {
                        List<LineSegment> temp = new List<LineSegment>();
                        
                        foreach (var seg in remainingSegments)
                        {
                            temp.AddRange(seg.Subtract(i.IntersectSegment));
                        }
                        remainingSegments = temp;
                    }
                }
                remainingSegments.RemoveAll(s => s.Length < 0.0001f);

                if (remainingSegments.Count == 0)
                {
                    // Entire segment is within a tab
                    TestAddPoint(ClearHeight(segment.B, tabHeight), ret.Vertices);
                }
                else
                {
                    // Everything described in "remainingSegments" is outside of the tab, and the spaces
                    // between are on the tab.  The path between is known since it's always a straight line.
                    remainingSegments.Sort((s1, s2) => (s1.A - segment.A).Length.CompareTo((s2.A - segment.A).Length));
                    foreach (var s in remainingSegments)
                    {
                        TestAddPoint(ClearHeight(s.A, tabHeight), ret.Vertices);
                        TestAddPoint(s.A, ret.Vertices);
                        TestAddPoint(s.B, ret.Vertices);
                        TestAddPoint(ClearHeight(s.B, tabHeight), ret.Vertices);
                    }
                    TestAddPoint(ClearHeight(segment.B, tabHeight), ret.Vertices);
                }
            }

            return ret;
        }

        private void TestAddPoint(Vector3 point, List<Vector3> points)
        {
            int i = points.Count;
            // Test if this is a useless move (up+down or down+up at the same xy location)
            if (i > 1 && (points[i - 2] - point).Length < 0.0001f && (points[i - 1].Xy - point.Xy).Length < 0.0001f)
            {
                points.RemoveAt(i - 1);
            }
            else
            {
                if (i > 0 && (points[i - 1] - point).Length < 0.0001f)
                {
                    // Don't add a duplicate point
                }
                else
                {
                    points.Add(point);
                }
            }
        }
    }
}
