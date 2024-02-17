using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Geometry;
using OpenTK;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using OpenTK.Graphics.OpenGL;
//using System.Drawing;
using ClipperLib;
//using System.Xml.Serialization;
//using System.IO;
using LibTessDotNet;

namespace Geometry
{
    /// <summary>
    /// A polygon in 2d space.
    /// </summary>
    public class Polygon2D
    {
        // Assuming float/double input units are in meters, scale them to 0.1 micrometers.
        // The largest coordinates available are then +- 2^62 * 0.1um > 450 million kilometers.
        // If input units are inches, it's somewhat less but still > 100 million kilometers.
        // Should be sufficient for anything feeding into a CNC machine.
        //
        // Additional note: any algorithm that multiplies the integers will need to accomodate
        // a 128 bit result.  All algorithms in this class do that properly.
        public static readonly Int64 scale = 1000000;

        public static IEnumerable<LineSegment> LineSegmentsFromIntPolygon(List<IntPoint> intPolygon)
        {
            if (intPolygon.Count <= 1)
            {
                yield break;
            }
            Vector3 last = FromIntSpace(intPolygon.Last());
            foreach (IntPoint intPoint in intPolygon)
            {
                Vector3 current = FromIntSpace(intPoint);
                yield return new LineSegment(last, current);
                last = current;
            }
        }

        public static Int64 ToIntSpace(double floatSpace)
        {
            return (Int64)Math.Round(floatSpace * scale);
        }
        public static float FromIntSpace(Int64 i)
        {
            return (1.0f / scale) * i;
        }

        public static IntPoint ToIntSpace(Vector3 v)
        {
            return new IntPoint(
                ToIntSpace(v.X),
                ToIntSpace(v.Y));
        }
        public static Vector3 FromIntSpace(IntPoint p)
        {
            return new Vector3(
                FromIntSpace(p.X),
                FromIntSpace(p.Y), 0.0f);
        }

        ///// <summary>
        ///// 2D vector crossproduct.
        ///// Since the crossproduct isn't defined in 2D, this can
        ///// either be imagined as the resulting Z component in 3D,
        ///// or sin(theta) * length(a) * length(b), where theta is
        ///// the angle between the to vectors.
        ///// </summary>
        ///// <returns></returns>
        //private Int64 cross(IntPoint v1, IntPoint v2)
        //{
        //    return v1.X * v2.Y - v1.Y * v2.X;
        //}
        //private Int64 dot(IntPoint v1, IntPoint v2)
        //{
        //    return v1.X * v2.X + v1.Y * v2.Y;
        //}

        public static bool IsVectorBetween(IntPoint a, IntPoint b, IntPoint c)
        {
            // returns true if vector "b" is between "a" and "c" in the counterclockwise direction.
            // Returns false if any of the inputs are overlapping.
            //
            // True if this:
            //  c   b
            //  |  /
            //  | /
            //  |/
            //  0---->a
            //

            // False if this:
            //  b   c
            //   \  |
            //    \ |
            //     \|
            //      0---->a
            //

            IntPoint a_perpendicular = new IntPoint(-a.Y, a.X);
            int s1 = DotProductSign(a_perpendicular, b);
            int s2 = DotProductSign(a_perpendicular, c);

            // s1 & s2 both 1: both within 180 degrees (counter-clockwise) of a.
            // s1 & s2 both -1: both between 180 degrees and 360 degrees (counter-clockwise) of a.
            // s1 & s2 are both 0: colinear with a.

            if (s1 == 0)
            {
                if (DotProductSign(a, b) >= 0)
                {
                    // a and b point exactly the same direction
                    return false;
                }
            }
            if (s2 == 0)
            {
                if (DotProductSign(a, c) >= 0)
                {
                    // a and c point exactly the same direction
                    return false;
                }
            }
            if (s1 == 0 && s2 == 0)
            {   // Any of these are possibilities if s1 == s2 == 0
                // 0---->a---->b---->c (covered by cases above)
                // b<----0---->a---->c (covered by cases above)
                // c<----0---->a---->b (covered by cases above)
                // c<----b<----0---->a (return false)

                return false;
            }

            if (s1 > s2)
            {
                // b comes strictly before c
                return true;
            }
            if (s1 < s2)
            {
                // c comes before b
                return false;
            }



            // They're on the same side of the line of "a".
            IntPoint b_perpendicular = new IntPoint(-b.Y, b.X);
            int s3 = DotProductSign(b_perpendicular, c);

            // If s3 is positive, c comes after b.
            // If s3 is negative, c comes before b (return false)
            // If s3 is zero, c and b point exactly the same direction (return false)
            return s3 > 0;
        }


        // Unsigned integer multiply (16 bit = 8 bit * 8 bit)
        // Easy to test every possible input for this one.
        public static void Int16Multiply(byte a, byte b, out byte high, out byte low)
        {
            byte b_low = (byte)(b & 0xF);
            byte a_low = (byte)(a & 0xF);
            byte a_high = (byte)((a >> 4) & 0xF);
            byte b_high = (byte)((b >> 4) & 0xF);
            low = (byte)(b_low * a_low);
            high = (byte)(a_high * b_high);

            // mid is a_high * b_low + b_high * a_low, but that might overflow
            // so the addition needs to be done in two steps.
            byte mid = (byte)(a_high * b_low + (low >> 4));

            // Put the high half of mid into the low half of high.
            high = (byte)(high + (mid >> 4));

            // Add the other half that might have overflowed earlier
            mid = (byte)(b_high * a_low + (mid & 0x0F));

            // And put that in high as well
            high = (byte)(high + (mid >> 4));
            low = (byte)((low & 0x0F) + (mid << 4));
        }

        // Unsigned integer multiply (128 bit = 64 bit * 64 bit)
        // This one isn't fully testable in reasonable time, but it's
        // patterened exactly after the UInt16Multiply function which is testable.
        public static void UInt128Multiply(UInt64 a, UInt64 b, out UInt64 high, out UInt64 low)
        {
            UInt64 b_low = (UInt64)(b & 0xFFFFFFFF);
            UInt64 a_low = (UInt64)(a & 0xFFFFFFFF);
            UInt64 a_high = (UInt64)((a >> 32) & 0xFFFFFFFF);
            UInt64 b_high = (UInt64)((b >> 32) & 0xFFFFFFFF);
            low = (UInt64)(b_low * a_low);
            high = (UInt64)(a_high * b_high);

            // mid is a_high * b_low + b_high * a_low, but that might overflow
            // so the addition needs to be done in two steps.
            UInt64 mid = (UInt64)(a_high * b_low + (low >> 32));

            // Put the high half of mid into the low half of high.
            high = (UInt64)(high + (mid >> 32));

            // Add the other half that might have overflowed earlier
            mid = (UInt64)(b_high * a_low + (mid & 0xFFFFFFFF));

            // And put that in high as well
            high = (UInt64)(high + (mid >> 32));
            low = (UInt64)((low & 0xFFFFFFFF) + (mid << 32));
        }

        /// <summary>
        /// Compute the sign of the dot product between v1 and v2.
        /// Returns 1 if v1 and v2 point the same way,
        /// 0 if they are perpendicular,
        /// and -1 if they point different directions.
        /// </summary>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        /// <returns>-1, 0, or 1 depending on where v1 and v2 point</returns>
        public static int DotProductSign(IntPoint v1, IntPoint v2)
        {
            int term1_sign = Math.Sign(v1.X) * Math.Sign(v2.X);
            int term2_sign = Math.Sign(v1.Y) * Math.Sign(v2.Y);

            // These shortcuts give a 17% performance increase.
            // Should probably do more performance testing.
            if (term1_sign == 0)
            {
                return term2_sign;
            }
            if (term2_sign == 0)
            {
                return term1_sign;
            }
            if (term1_sign == term2_sign)
            {
                return term1_sign;
            }

            // Otherwise the result depends on which is bigger.
            // v1.X * v2.X or v1.Y * v2.Y

            UInt64 v1x_abs = v1.X >= 0 ? (UInt64)v1.X : (UInt64)(-v1.X);
            UInt64 v1y_abs = v1.Y >= 0 ? (UInt64)v1.Y : (UInt64)(-v1.Y);
            UInt64 v2x_abs = v2.X >= 0 ? (UInt64)v2.X : (UInt64)(-v2.X);
            UInt64 v2y_abs = v2.Y >= 0 ? (UInt64)v2.Y : (UInt64)(-v2.Y);

            // Logically a shortcut, but doesn't add much from performance profiling
            //if ((v1x_abs > v1y_abs) && (v2x_abs > v2y_abs))
            //{
            //    return term1_sign;
            //}
            //if ((v1y_abs > v1x_abs) && (v2y_abs > v2x_abs))
            //{
            //    return term2_sign;
            //}

            UInt128Multiply(v1x_abs, v2x_abs, out UInt64 term1High, out UInt64 term1Low);
            UInt128Multiply(v1y_abs, v2y_abs, out UInt64 term2High, out UInt64 term2Low);

            if (term1High == term2High)
            {
                if (term1Low > term2Low)
                {
                    // first term is strictly larger
                    return term1_sign;
                }
                else if (term1Low < term2Low)
                {
                    // Second term is strictly larger
                    return term2_sign;
                }
                else
                {
                    return 0;
                }
            }
            else
            {
                if (term1High > term2High)
                {
                    return term1_sign;
                }
                else
                {
                    return term2_sign;
                }
            }
        }

        /// <summary>
        /// Subtract one point from another.
        /// The resulting vector will point from "from" to "to".
        /// </summary>
        /// <param name="to"></param>
        /// <param name="from"></param>
        /// <returns></returns>
        public static IntPoint Subtract(IntPoint to, IntPoint from)
        {
            return new IntPoint(to.X - from.X, to.Y - from.Y);
        }

        private class ClipperEdge
        {
            // An edge directed from a to b
            public IntPoint a;
            public IntPoint b;
            public ClipperEdge(IntPoint a, IntPoint b)
            {
                this.a = a;
                this.b = b;
            }

            /// <summary>
            /// This function can be used to check whether a line is between two points.
            /// Returns 0 if on the line,
            /// 1 if on one side, -1 if on the other.
            /// </summary>
            /// <param name="p"></param>
            /// <returns></returns>
            public int SideOfLine(IntPoint p)
            {
                // Returns 0 if on the line,
                // 1 if on one side, -1 if on the other.

                IntPoint a_to_b = Subtract(b, a);
                IntPoint thisPerpendicular = new IntPoint(a_to_b.Y, -a_to_b.X);

                return DotProductSign(thisPerpendicular, p);
            }

            public bool IsPointOnEdge(IntPoint p)
            {
                // Implementation based on:
                // Point p is on the infinite line on which this edge resides
                // and p is between a and b

                // This method only relies on the sign of the result of a dot product.
                // In some cases, the sign of a dot product is trivial to compute, but
                // in others a multiply is needed.
                // Using a 64*64 = 128 bit multiply, this method can accurately check
                // intersections with lines that use the full 64 bits of the input
                // signed integer.

                //
                //          b
                //          |
                //          |
                // p--------|
                //          |
                //          |
                //          |
                //          a
                //

                IntPoint a_to_b = Subtract(b, a);
                IntPoint thisPerpendicular = new IntPoint(a_to_b.Y, -a_to_b.X);
                IntPoint a_to_p = Subtract(p, a);
                int s1 = DotProductSign(a_to_p, thisPerpendicular);
                if (s1 != 0)
                {
                    return false;
                }

                // p is on the infinite line contianing a, b

                IntPoint b_to_p = Subtract(p, b);

                int s2 = DotProductSign(a_to_p, b_to_p);
                if (s2 > 0)
                {
                    // Point is outside the line
                    return false;
                }
                if (s2 == 0)
                {
                    // p is on either a or b
                    return false;
                }
                return true;
            }


            public bool Intersects(ClipperEdge other, bool touchingIsIntersection = false)
            {
                // Implementation based on:
                // Segment A crosses the infinite line on which B resides
                // and Segment B crosses the infinite on which A resides

                // This method only relies on the sign of the result of a dot product.
                // In some cases, the sign of a dot product is trivial to compute, but
                // in others a multiply is needed.
                // Using a 64*64 = 128 bit multiply, this method can accurately check
                // intersections with lines that use the full 64 bits of the input
                // signed integer.

                //
                //          b
                //          |
                //          |
                // other.a--|---------other.b
                //          |
                //          |
                //          |
                //          a
                //

                IntPoint a_to_b = Subtract(this.b, this.a);
                IntPoint thisPerpendicular = new IntPoint(a_to_b.Y, -a_to_b.X);
                IntPoint a_to_other_a = Subtract(other.a, this.a);
                int s1 = DotProductSign(a_to_other_a, thisPerpendicular);
                if (s1 == 0 && !touchingIsIntersection)
                {
                    // other.a is on this line (not necessarily between the points on the edge)
                    return false;
                }

                IntPoint a_to_other_b = Subtract(other.b, this.a);
                int s2 = DotProductSign(a_to_other_b, thisPerpendicular);
                if (s2 == 0 && !touchingIsIntersection)
                {
                    // other_b is on this line (not necessarily between the points on the edge)
                    return false;
                }

                // Points are on the same side of the line
                if (s1 == s2 && s1 != 0)
                {
                    return false;
                }

                IntPoint other_a_to_other_b = Subtract(other.b, other.a);
                IntPoint otherPerpendicular = new IntPoint(other_a_to_other_b.Y, -other_a_to_other_b.X);
                int s4 = DotProductSign(a_to_other_a, otherPerpendicular);
                if (s4 == 0)
                {
                    // point a is on the other line (and between the points on the edge)
                    return touchingIsIntersection;
                }

                IntPoint b_to_other_a = Subtract(other.a, this.b);
                int s3 = DotProductSign(b_to_other_a, otherPerpendicular);
                if (s3 == 0)
                {
                    // point b is on the other line (and between the points on the edge)
                    return touchingIsIntersection;
                }

                if (s3 == s4)
                {
                    // point a and b are on the same side of the line.
                    return false;
                }
                return true;
            }

        }

        public class ClipperPolygon
        {
            // 2D polygon with integer type.  IntPoint is set to be 64 bits, 62 of which are
            // safelty usable (the rest are needed for additions/subtractions without overflow)
            public List<IntPoint> points = new List<IntPoint>();

            public ClipperPolygon()
            {
            }

        }


        public List<List<IntPoint>> polygons = new List<List<IntPoint>>();

        public Vector3 ipoint = new Vector3();
        public Polygon2D()
        {

        }

        private IEnumerable<ClipperEdge> GetClipperEdges()
        {
            foreach (List<IntPoint> polygon in polygons)
            {
                if (polygon.Count < 1)
                {
                    continue;
                }
                IntPoint from = polygon.Last();
                foreach (IntPoint to in polygon)
                {
                    yield return new ClipperEdge(from, to);
                    from = to;
                }
            }
        }

        private bool Intersects(ClipperEdge e)
        {
            foreach (ClipperEdge c in this.GetClipperEdges())
            {
                if (c.Intersects(e))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Run ear clipping to get the triangles in the polygon.
        /// Returned triangles are converted from int space.
        /// This method is destructive.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<Triangle> EarClipForTriangles()
        {
            if (polygons.Count == 0)
            {
                yield break;
            }
            
            PreparePolygonsForEarClipping();

            List<IntPoint> polys = polygons[0];
            int i = 0;
            int j = 0;
            while (polys.Count > 2)
            {


                // Fix any twisted loops
                for (int x = 0; x < polys.Count; x++)
                {
                    List<int> matching_indices = new List<int>();
                    for (int y = 0; y < polys.Count; y++)
                    {
                        if (polys[x] == polys[y] && x != y)
                        {
                            matching_indices.Add(y);
                        }
                    }

                    if (matching_indices.Count > 1)
                    {
                        //    Console.WriteLine("Matching Indices: " + matching_indices.Count);
                    }
                    IntPoint x1 = polys[x];
                    IntPoint x2 = polys[(x + 1) % polys.Count];
                    IntPoint x3 = polys[(x + 2) % polys.Count];
                }


                // Loop around the polygon and clean up any extraneous edges
                // (ones that reach out and back again on the same path)
                for (int x = 0; x < polys.Count; x++)
                {
                    IntPoint x1 = polys[x];
                    IntPoint x2 = polys[(x + 1) % polys.Count];
                    IntPoint x3 = polys[(x + 2) % polys.Count];

                    if (x1 == x3)
                    {
                        // Remove the two following x
                        polys.RemoveAt((x + 1) % polys.Count);
                        if (x == polys.Count)
                        {
                            x--;
                        }
                        polys.RemoveAt((x + 1) % polys.Count);
                        x = 0; // Start at the beginning again.
                        continue;
                    }

                    // x1 -> x2 -> x3 are all on the same line
                    // Triangle points must be counter-clockwise (angle p1->p2->p3 needs to be less than 180 degrees).
                    IntPoint perpendicular_left = new IntPoint(x1.Y - x2.Y, x2.X - x1.X);
                    if (DotProductSign(Subtract(x3, x2), perpendicular_left) == 0)
                    {
                        if (DotProductSign(Subtract(x2, x1),
                            Subtract(x3, x2)) < 0)
                        {
                            // Make this:
                            //        x1 ---> x2
                            // x3 <---------- x2
                            // Look like this:
                            // x3 <-- x1
                            polys.RemoveAt((x + 1) % polys.Count);
                            x = 0;
                            continue;
                        }
                    }


                    // Check if p1 == p2 (duplicate point)
                    if (x1 == x2)
                    {
                        polys.RemoveAt((x + 1) % polys.Count);
                        x = 0; // Start at the beginning again.
                        continue;
                    }
                }
                if (polys.Count <= 2)
                {
                    break;
                }


                j = j + 1;
                if (j > (polys.Count + 10))
                {
                    break;
                }


                i = i % polys.Count;
                IntPoint p1 = polys[i];
                IntPoint p2 = polys[(i + 1) % polys.Count];
                IntPoint p3 = polys[(i + 2) % polys.Count];

                // Check if there are reverse edges that match p1, p2, p3.
                // This might just be part of a bridge to other polygons.
                // Usually they are just a single segment long, but if mulitple
                // polygons are strung along the same path and removed, it will
                // leave a multi-link forward and backward path.
                bool matches_backward_links = false;
                for (int x = 0; x < polys.Count; x++)
                {
                    IntPoint x1 = polys[x];
                    IntPoint x2 = polys[(x + 1) % polys.Count];
                    IntPoint x3 = polys[(x + 2) % polys.Count];

                    if (p2 == x2 && p1 == x3 && p3 == x1)
                    {
                        matches_backward_links = true;
                        break;
                    }

                }
                if (matches_backward_links)
                {
                    i = (i + 1) % polys.Count;
                    continue;
                }

                // Triangle points must be counter-clockwise (angle p1->p2->p3 needs to be less than 180 degrees).
                IntPoint p1_to_p2_perpendicular_left = new IntPoint(p1.Y - p2.Y, p2.X - p1.X);
                if (DotProductSign(Subtract(p3, p2), p1_to_p2_perpendicular_left) <= 0)
                {
                    i = i + 1;
                    continue;
                }

                IntPoint p2_to_p3_perpendicular_left = new IntPoint(
                    p2.Y - p3.Y, p3.X - p2.X);

                IntPoint p3_to_p1_perpendicular_left = new IntPoint(
                    p3.Y - p1.Y, p1.X - p3.X);

                bool has_point_in_triangle = false;

                // Is there a point inside (not touching) the possible triangle?
                //for (int a = 0; a < polys.Count-3; a++)
                //{
                //int b = (a + i + 3) % polys.Count;
                IntPoint last = polys.Last();
                for (int b = 0; b < polys.Count; b++)
                {
                    ClipperEdge testEdge = new ClipperEdge(last, polys[b]);
                    last = polys[b];

                    //// Overlaps between these edges are expected.
                    //if ((testEdge.a == p1 && testEdge.b == p2) || 
                    //    (testEdge.a == p2 && testEdge.b == p3))
                    //{
                    //    continue;
                    //}
                    //

                    //// Overlaps between reversed edges are not OK.
                    //if ((testEdge.b == p1 && testEdge.a == p2) ||
                    //    (testEdge.b == p2 && testEdge.a == p3))
                    //{
                    //    has_point_in_triangle = true;
                    //    break;
                    //}


                    //int before_b = (b + polys.Count - 1) % polys.Count;
                    //int after_b = (b + polys.Count + 1) % polys.Count;
                    IntPoint other = polys[b];

                    // For inside holes, don't include the reverse edge for p1->p2.
                    //if (other == p1)
                    //{
                    //    if (p2 == polys[before_b])
                    //    {
                    //        continue;
                    //    }
                    //}
                    //
                    //if (other == p2)
                    //{
                    //    if (p1 == polys[after_b])
                    //    {
                    //        continue;
                    //    }
                    //}


                    //if (other == p1 || other == p2 || other == p3)
                    // {
                    //   continue;
                    //}

                    // p1_to_other can be used to check both edges p1_to_p2 and p3_to_p1
                    IntPoint p1_to_other = Subtract(testEdge.a, p1);
                    IntPoint testEdgeDir = Subtract(testEdge.b, testEdge.a);
                    int s1 = DotProductSign(p1_to_other, p1_to_p2_perpendicular_left);
                    if (s1 < 0)
                    {
                        // Point is outside the edge
                        continue;
                    }
                    if (s1 == 0)
                    {
                        // Point is touching the edge
                        if (DotProductSign(testEdgeDir, p1_to_p2_perpendicular_left) <= 0)
                        {
                            // but the test edge is headed away or is parallel
                            continue;
                        }
                    }

                    s1 = DotProductSign(p1_to_other, p3_to_p1_perpendicular_left);
                    if (s1 < 0) { continue; }
                    if (s1 == 0 && DotProductSign(testEdgeDir, p3_to_p1_perpendicular_left) <= 0)
                    {
                        continue;
                    }
                    IntPoint p2_to_other = Subtract(testEdge.a, p2);
                    s1 = DotProductSign(p2_to_other, p2_to_p3_perpendicular_left);
                    if (s1 < 0) { continue; }
                    if (s1 == 0 && DotProductSign(testEdgeDir, p2_to_p3_perpendicular_left) <= 0)
                    {
                        continue;
                    }



                    //if (DotProductSign(p1_to_other, p1_to_p2_perpendicular_left) <= 0)
                    //{
                    //    // Point is outside (not touching) the edge p1_to_p2
                    //    continue;
                    //}
                    //if (DotProductSign(p1_to_other, p3_to_p1_perpendicular_left) <= 0)
                    //{
                    //    // Point is outside (not touching) the edge p3_to_p1
                    //    continue;
                    //}
                    //IntPoint p2_to_other = subtract(testEdge.a, p2);
                    //if (DotProductSign(p2_to_other, p2_to_p3_perpendicular_left) <= 0)
                    //{
                    //    // Point is outside (not touching) the edge p2_to_p3
                    //    continue;
                    //}
                    has_point_in_triangle = true;
                    break;

                }
                if (has_point_in_triangle)
                {
                    i = i + 1;
                    continue;
                }

                // Don't use this edge if it intersects with any other
                // edges.
                ClipperEdge newEdge = new ClipperEdge(p3, p1);
                if (Intersects(newEdge))
                {
                    i = i + 1;
                    continue;
                }

                Triangle t = new Triangle(
                    FromIntSpace(p1),
                    FromIntSpace(p2),
                    FromIntSpace(p3));

                if (t.Plane.Normal.Z <= 0)
                {
                    Console.WriteLine("backwards triangle!");
                }

                //ClipperEdge e = new ClipperEdge(p1, p3);
                //if (Intersects(e))
                //{
                //    i = i + 1;
                //    continue;
                //}

                j = 0;

                polys.RemoveAt((i + 1) % polys.Count);
                ipoint = FromIntSpace(polys[i % polys.Count]);
                if (i == polys.Count)
                {
                    i--;
                }
                //i = (i + polys.Count - 1) % polys.Count; // i-- with wrap
                yield return t;
            }
        }

        public void Add(List<IntPoint> polygon)
        {
            polygons.Add(polygon);
        }

        //List<IntPoint> outside = new List<IntPoint>();
        //public void AddOutside(List<IntPoint> polygon)
        //{
        //    outside = polygon;
        //}
        List<List<IntPoint>> inside = new List<List<IntPoint>>();
        public void AddLibTessPolygon(List<IntPoint> polygon)
        {
            inside.Add(polygon);
        }

        public IEnumerable<Triangle> LibTessTriangles(Vector3 offset, bool reverse = false)
        {
            Matrix4 transformation = 
                Matrix4.CreateScale(1.0f / (float)scale) *
                Matrix4.CreateTranslation(offset);

            // Can't reverse through matrix operations...
            //if (reverse)
            //{
            //    Matrix4 xZeroPlaneReflection = Matrix4.Identity;
            //    xZeroPlaneReflection.M11 = -1;
            //    transformation = xZeroPlaneReflection * Matrix4.CreateRotationY((float)Math.PI) * transformation;
            //}
            foreach (Triangle t in LibTessTriangles(transformation, reverse))
            {
                yield return t;
            }
        }

        public IEnumerable<Triangle> LibTessTriangles(Matrix4 transform, bool reverse = false)
        {
            Tess tess = new LibTessDotNet.Tess();
            List<ContourVertex> contours = new List<ContourVertex>();

            foreach (List<IntPoint> polygon in inside)
            {
                List<ContourVertex> insideContour = new List<ContourVertex>();
                foreach (IntPoint point in polygon)
                {
                    Vector3 v = Vector3.Transform(new Vector3(point.X, point.Y, 0.0f), transform);
                    insideContour.Add(new ContourVertex(new Vec3(v.X, v.Y, v.Z)));
                }
                tess.AddContour(insideContour);
            }

            tess.Tessellate(WindingRule.NonZero);
            int offset1 = 1;
            int offset2 = 2;

            if (reverse)
            {
                offset1 = 2;
                offset2 = 1;
            }

            int numTriangles = tess.ElementCount;
            for (int i = 0; i < numTriangles; i++)
            {
                var v0 = tess.Vertices[tess.Elements[i * 3]].Position;
                var v1 = tess.Vertices[tess.Elements[i * 3 + offset1]].Position;
                var v2 = tess.Vertices[tess.Elements[i * 3 + offset2]].Position;

                yield return new Triangle(
                    new Vector3(v0.X, v0.Y, v0.Z),
                    new Vector3(v1.X, v1.Y, v1.Z),
                    new Vector3(v2.X, v2.Y, v2.Z)
                    );
            }
        }



        /// <summary>
        /// Simplify and connect all polygons together.
        /// TODO: connecting the polygons isn't really necessary,
        /// a better aproach would be to divide the polygons into groups,
        /// where each group contains 1 outer polygon and 0 or more inner polygons.
        /// </summary>
        private void PreparePolygonsForEarClipping()
        {
            // Clipper will fix overlapping polygons and orient outside/inside polygons properly.
            polygons = Clipper.SimplifyPolygons(polygons, PolyFillType.pftEvenOdd);
            if (polygons.Count == 0)
            {
                return;
            }

            // Combine the first polygon (source) with all the subsequent polygons (targets).
            // This can be done either with a line linking the two polygons (and a mirrored
            // return line), or with a single point if the polygons touch at a point.
            // It's assumed the polygons are all:
            // 1. simple (no overlapping or crossing edges)
            // 2. oriented (counter-clockwise vertices for a polygon, clockwise for a hole within a polygon)
            // Shared vertices are OK, including multiple polygons sharing the same vertex.
            // This gets especially interesting when multiple polygons and multiple holes share a vertex.
            List<IntPoint> p1 = polygons[0];
            for (int polygon_index = 1; polygon_index < polygons.Count; polygon_index++)
            {
                // Search for a link from one polygon to another
                List<IntPoint> p2 = polygons[polygon_index];
                for (int p1_point_index = 0; p1_point_index < p1.Count; p1_point_index++)
                {
                    for (int p2_point_index = 0; p2_point_index < p2.Count; p2_point_index++)
                    {

                        // Create a new edge that links the p1 to p2
                        ClipperEdge link = new ClipperEdge(
                            p1[p1_point_index],
                            p2[p2_point_index]);

                        if (link.a == link.b)
                        {
                            // Polygons share a point (good place to connect them)
                            // If this is used, need to add two additional checks:
                            // 1. (target is hole) == (target is inside)
                            // 2. no twist is created
                            // Both of those are checked for non-zero length links below,
                            // so while this is a more efficient solution, it needs more
                            // implementation work and some testing.
                            // Save it for TODO after more cleanup/refactoring.
                            continue;
                            // 
                            // 
                            // p1.InsertRange(p1_point_index, p2.GetRange(0, p2_point_index));
                            // p1.InsertRange(p1_point_index, p2.GetRange(p2_point_index, p2.Count - p2_point_index));
                            // 
                            // // Remove p2 from the polygon set
                            // polygons.RemoveAt(polygon_index);
                            // 
                            // // Prepare for next iteration of polygon to combine
                            // polygon_index = 0;
                            // 
                            // // Break out of both for loops
                            // p1_point_index = p1.Count; // outer loop
                            // break; // inner loop
                        }



                        // Keep the edge if it doesn't intersect any other edges
                        bool intersects = false;
                        for (int i = 0; i < polygons.Count; i++)
                        {
                            IntPoint last = polygons[i].Last();
                            for (int j = 0; j < polygons[i].Count; j++)
                            {

                                // These checks ensure there's only 1 point on the polygons
                                // where the new lines will attach them.
                                bool isTestPointA = (i == 0) && (j == p1_point_index);
                                bool isTestPointB = (i == polygon_index) && (j == p2_point_index);
                                IntPoint point = polygons[i][j];
                                if (link.b == point && !isTestPointB)
                                {
                                    //intersects = true;
                                    //break;
                                }
                                if (link.a == point && !isTestPointA)
                                {
                                    //intersects = true;
                                    //break;
                                }

                                //if (i == 0)
                                //{
                                //    if (j == p1_point_index || (j+1)%polygons[i].Count == p1_point_index)
                                //    {
                                //        // line is attached to these edges
                                //        continue;
                                //    }
                                //}
                                //if (i == polygon_index)
                                //{
                                //    if (j == p2_point_index || (j + 1) % polygons[i].Count == p2_point_index)
                                //    {
                                //        // line is attached to these edges
                                //        continue;
                                //    }
                                //}
                                ClipperEdge c = new ClipperEdge(last, polygons[i][j]);
                                if (c.Intersects(link, false))
                                {
                                    intersects = true;
                                    break;
                                }

                                // Check for overlap as well
                                if (c.b == link.a && c.a == link.b) // opposite direction
                                {
                                    intersects = true;
                                    break;
                                }
                                if (c.a == link.a && c.b == link.b)
                                {
                                    intersects = true;
                                    break;
                                }

                                // Is the test edge touching any other points?
                                // This function excludes endpoints.
                                if (link.IsPointOnEdge(polygons[i][j]))
                                {
                                    intersects = true;
                                    break;
                                }

                                // Line connecting to the target ends on an edge.
                                // OK if target polygon is on the same side of the edge as the
                                // connecting line, otherwise the connecting line + target will
                                // cross the edge.
                                // Note: Clipper always seems to add a point on the edge whenever
                                // this happens, so it will always be a point check rather than an
                                // edge check.
                                //if (c.IsPointOnEdge(e.b))
                                //{
                                //    IntPoint nextOnTarget = p2[(p2_point_index + 1) % p2.Count];
                                //    if (c.SideOfLine(e.a) != c.SideOfLine(nextOnTarget))
                                //    {
                                //        intersects = true;
                                //        break;
                                //    }
                                //}


                                last = polygons[i][j];
                            }
                            if (intersects)
                            {
                                break;
                            }
                        }

                        //foreach (ClipperEdge c in this.GetClipperEdges())
                        //{
                        //    // don't compare intersection of attached edges
                        //    if (c.a == e.a || c.a == e.b || c.b == e.a || c.b == e.b)
                        //    {
                        //        continue;
                        //    }
                        //    if (c.Intersects(e, true))
                        //    {
                        //        intersects = true;
                        //        break;
                        //    }
                        //}

                        if (!intersects)
                        {
                            // Combine the two polygons together using this ege
                            // Shift p2 so p2_point_index is 0
                            //p2.AddRange(p2.GetRange(0, p2_point_index));
                            //p2.RemoveRange(0, p2_point_index);

                            bool is_connection_ok = false;

                            // Final check: if these are some of the doubled points,
                            // make sure it's added on the right side.
                            //if (avoid_points.Contains(e.a) || avoid_points.Contains(e.b))
                            //{

                            // TODO: If two outside polygons share a vertex,
                            // a link can match from that vertext to a polygon inside
                            // the wrong polygon.
                            //
                            // Prevent an outside polygon from attaching to an inside
                            // polygon (a hole) with a line outside the polygon.
                            IntPoint prior = p1[(p1_point_index - 1 + p1.Count) % p1.Count];
                            IntPoint current = p1[p1_point_index];
                            IntPoint next = p1[(p1_point_index + 1) % p1.Count];

                            // The newly added line must be between prior and next
                            //
                            //  current<---------prior
                            //    | \
                            //    |  \
                            //    |   \
                            //    V    V
                            //  next   new
                            //
                            IntPoint v1 = Subtract(next, current);
                            IntPoint v2 = Subtract(link.b, current);
                            IntPoint v3 = Subtract(prior, current);

                            bool p1_connector_inside = IsVectorBetween(v1, v2, v3);

                            // Check the target polygon
                            prior = p2[(p2_point_index - 1 + p2.Count) % p2.Count];
                            current = p2[p2_point_index];
                            next = p2[(p2_point_index + 1) % p2.Count];

                            // The newly added line must be between prior and next
                            //
                            //  current<---------prior
                            //    | \
                            //    |  \
                            //    |   \
                            //    V    V
                            //  next   new
                            //
                            v1 = Subtract(next, current);
                            v2 = Subtract(link.a, current);
                            v3 = Subtract(prior, current);

                            bool p2_connector_inside = IsVectorBetween(v1, v2, v3);

                            // Only allow them to connect if both are outside or both are inside.
                            is_connection_ok = p2_connector_inside == p1_connector_inside;

                            // If the connecting line target point is a point on the source polygon,
                            // make sure the line + target polygon doesn't cross the source polygon
                            // at that point.
                            for (int i = 0; i < p1.Count; i++)
                            {
                                if (i == p1_point_index)
                                {
                                    continue;
                                }

                                current = p1[i];
                                if (current == link.b)
                                {

                                    prior = p1[(i - 1 + p1.Count) % p1.Count];
                                    next = p1[(i + 1) % p1.Count];

                                    IntPoint target_polygon_next = p2[(p2_point_index + 1) % p2.Count];

                                    v1 = Subtract(next, current);
                                    v2 = Subtract(target_polygon_next, current);
                                    v3 = Subtract(prior, current);

                                    if (!IsVectorBetween(v1, v2, v3))
                                    {
                                        is_connection_ok = false;
                                    }
                                }
                            }
                            // Same thing, but from target polygon to source polygon
                            for (int i = 0; i < p2.Count; i++)
                            {
                                if (i == p2_point_index)
                                {
                                    continue;
                                }

                                current = p2[i];
                                if (current == link.a)
                                {
                                    prior = p2[(i - 1 + p2.Count) % p2.Count];
                                    next = p2[(i + 1) % p2.Count];

                                    IntPoint source_polygon_next = p1[(p1_point_index + 1) % p1.Count];

                                    v1 = Subtract(next, current);
                                    v2 = Subtract(source_polygon_next, current);
                                    v3 = Subtract(prior, current);

                                    if (p2_connector_inside != IsVectorBetween(v1, v2, v3))
                                    {
                                        is_connection_ok = false;
                                    }
                                    //if (!p2_connector_inside && !IsVectorBetween(v1, v2, v3))
                                    //{
                                    //    is_connection_ok = false;
                                    //}
                                }
                            }

                            if (is_connection_ok)
                            {
                                p1.Insert(p1_point_index + 1, p1[p1_point_index]);
                                p1.Insert(p1_point_index + 1, p2[p2_point_index]);
                                p1.InsertRange(p1_point_index + 1, p2.GetRange(0, p2_point_index));
                                p1.InsertRange(p1_point_index + 1, p2.GetRange(p2_point_index, p2.Count - p2_point_index));

                                // Remove p2 from the polygon set
                                polygons.RemoveAt(polygon_index);

                                // Prepare for next iteration of polygon to combine
                                polygon_index = 0;

                                // Break out of both for loops
                                p1_point_index = p1.Count; // outer loop
                                break; // inner loop
                            }
                        }
                    }
                }
            }
        }


    }
}


