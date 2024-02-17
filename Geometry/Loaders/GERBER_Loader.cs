using ClipperLib;
using OpenTK;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Geometry
{
    public class GERBER_Loader
    {
        List<List<IntPoint>> debugPolygons = new List<List<IntPoint>>();
        public List<List<IntPoint>> GetDebugPolygons()
        {
            return debugPolygons;
        }
        private class ClipperHelper
        {
            private Clipper c;
            bool has_remove = false;
            public ClipperHelper()
            {
                c = new Clipper();
            }
            public void Add(List<IntPoint> positivePath)
            {
                //// Force a union on every add
                //c.AddPath(positivePath, PolyType.ptClip, true);
                //PolyTree polyTree = new PolyTree();
                //c.Execute(ClipType.ctUnion, polyTree, PolyFillType.pftNonZero, PolyFillType.pftNonZero);
                //c = new Clipper();
                //c.AddPaths(Clipper.PolyTreeToPaths(polyTree), PolyType.ptSubject, true);
                //return;

                if (has_remove)
                {
                    // The remove only applies to the previous add.
                    // Process everything so the current add isn't
                    // also removed.
                    Clipper newPaths = new Clipper();
                    newPaths.AddPaths(GetPaths(), PolyType.ptSubject, true);
                    c = newPaths;
                    has_remove = false;
                }
                c.AddPath(positivePath, PolyType.ptSubject, true);
            }
            public void Remove(List<IntPoint> negativePath)
            {
                c.AddPath(negativePath, PolyType.ptClip, true);
                has_remove = true;
            }
            public List<List<IntPoint>> GetPaths()
            {
                PolyTree polyTree = new PolyTree();
                c.Execute(ClipType.ctDifference, polyTree, PolyFillType.pftNonZero, PolyFillType.pftNonZero);
                return Clipper.PolyTreeToPaths(polyTree);
            }
        }

        Dictionary<string, string[]> fileAttributeDictionary = new Dictionary<string, string[]>();
        public string[] GetFileAttributes(string attributeName)
        {
            if (fileAttributeDictionary.ContainsKey(attributeName))
            {
                return fileAttributeDictionary[attributeName];
            }
            return new string[] { };
        }

        class GerberToken
        {
            string tokenString = "";
            virtual public void SetTokenString(string tokenString)
            {
                this.tokenString = tokenString;
            }
            public string GetField(string name)
            {
                return "";
            }
            public override string ToString()
            {
                return this.GetType().ToString() + " " + tokenString;
            }
        }
        class G01 : GerberToken
        {
            public override string ToString()
            {
                return "[G01]";
            }
        }
        class G02 : GerberToken
        {
            public override string ToString()
            {
                return "[G02]";
            }
        }
        class G03 : GerberToken
        {
            public override string ToString()
            {
                return "[G03]";
            }
        }
        class G04 : GerberToken
        {
            public string comment = "";
            public G04(string tokenString)
            {
                Regex r = new Regex(@"G04(?<comment>.*)\*");
                Match m = r.Match(tokenString);
                if (m.Success)
                {
                    comment = m.Groups["comment"].Value;
                }
            }
            public override string ToString()
            {
                return "[G04]" + comment;
            }
        }
        class G75 : GerberToken
        {
            public override string ToString()
            {
                return "[G75]";
            }
        }
        class G36 : GerberToken {
            public override string ToString()
            {
                return "[G36]";
            }
        }
        class G37 : GerberToken {
            public override string ToString()
            {
                return "[G37]";
            }
        }

        public class GerberPoint
        {
            public int x, y, i, j;
            public GerberPoint()
            {
                x = y = i = j = 0;
            }
        }
        public GerberPoint currentPoint;
        
        // D01, D02, D03: Done.
        class D02 : GerberToken
        {
            private readonly bool hasX, hasY, hasI, hasJ;
            public GerberPoint fromPoint = new GerberPoint();
            public GerberPoint newPoint = new GerberPoint();
            public string name;

            public void SetStartPoint(GerberPoint startPoint)
            {
                fromPoint = startPoint;
                newPoint.x = hasX ? newPoint.x : startPoint.x;
                newPoint.y = hasY ? newPoint.y : startPoint.y;
                newPoint.i = hasI ? newPoint.i : startPoint.i;
                newPoint.j = hasJ ? newPoint.j : startPoint.j;
            }
            public D02(string tokenString)
            {
                hasX = hasY = hasI = hasJ = false;
                name = "[D02]"; // TODO: parse this from tokenString
                Regex r = new Regex("([XYIJ])([+-]?[0-9]+\\s*)");
                Match m = r.Match(tokenString);
                while (m.Success)
                {
                    switch (m.Groups[1].Value)
                    {
                        case "X":
                            newPoint.x = int.Parse(m.Groups[2].Value);
                            hasX = true;
                            break;
                        case "Y":
                            newPoint.y = int.Parse(m.Groups[2].Value);
                            hasY = true;
                            break;
                        case "I":
                            newPoint.i = int.Parse(m.Groups[2].Value);
                            hasI = true;
                            break;
                        case "J":
                            newPoint.j = int.Parse(m.Groups[2].Value);
                            hasJ = true;
                            break;
                    }
                    m = m.NextMatch();
                }
            }
            public override string ToString()
            {
                return name + " X: " + newPoint.x + ", Y: " + newPoint.y + ", I: " + newPoint.i + ", J: " + newPoint.j;
            }
        }
        class D01 : D02
        {
            // D01 is a stroke.  Linear or arc.
            public List<IntPoint> GetLinearStroke(FS format, MO units)
            {
                return new List<IntPoint>() {
                    new IntPoint(
                        Polygon2D.ToIntSpace(units.ToPathCAMUnits(format.ScaleGerberInt(fromPoint.x))),
                        Polygon2D.ToIntSpace(units.ToPathCAMUnits(format.ScaleGerberInt(fromPoint.y)))),
                    new IntPoint(
                        Polygon2D.ToIntSpace(units.ToPathCAMUnits(format.ScaleGerberInt(newPoint.x))),
                        Polygon2D.ToIntSpace(units.ToPathCAMUnits(format.ScaleGerberInt(newPoint.y))))
                };
            }

            public D01(string tokenString) : base (tokenString)
            {
                name = "[D01]";
            }

            // Counter-clockwise angle between A and B.
            private double AngleCounterClockwise(Vector2d a, Vector2d b)
            {
                a.Normalize();
                b.Normalize();
                double angle = Math.Acos(Vector2d.Dot(a, b));
                if (Vector2d.Dot(a.PerpendicularLeft, b) <= 0)
                {
                    angle = Math.PI * 2.0 - angle;
                }
                return angle;
            }

            internal List<IntPoint> GetArcStroke(FS currentFormat, MO units, bool clockwise)
            {
                Vector2d center = new Vector2d(
                    units.ToPathCAMUnits(currentFormat.ScaleGerberInt(newPoint.i + fromPoint.x)),
                    units.ToPathCAMUnits(currentFormat.ScaleGerberInt(newPoint.j + fromPoint.y)));
                Vector2d from = new Vector2d(
                    units.ToPathCAMUnits(currentFormat.ScaleGerberInt(fromPoint.x)),
                    units.ToPathCAMUnits(currentFormat.ScaleGerberInt(fromPoint.y)));
                Vector2d to = new Vector2d(
                    units.ToPathCAMUnits(currentFormat.ScaleGerberInt(newPoint.x)),
                    units.ToPathCAMUnits(currentFormat.ScaleGerberInt(newPoint.y)));

                Vector2d a = from - center;
                Vector2d b = to - center;

                // The two radii won't match perfectly (though they should be close).
                // Interpolate the radius between them.
                double radius1 = a.Length;
                double radius2 = b.Length;
                    
                double angle = AngleCounterClockwise(a, b);
                double startAngle = AngleCounterClockwise(Vector2d.UnitX, a);
                
                if (clockwise)
                {
                    angle = Math.PI * 2.0 - angle;
                }
                
                Console.WriteLine("Start: " + 180.0 * startAngle / Math.PI + ", span: " + 180.0 * angle / Math.PI);
                List<IntPoint> points = new List<IntPoint>();

                if (angle > 0)
                {
                    double divisions = Math.Ceiling(50.0 * angle / Math.PI); // 100 divisions per 360 degrees
                    for (double t = 0; t <= (1.0 + 0.5 / divisions); t += 1.0 / divisions)
                    {
                        double theta = startAngle;
                        theta += clockwise ? -t * angle : t * angle;
                        double x = center.X + Math.Cos(theta) * (t * radius2 + (1.0 - t) * radius1);
                        double y = center.Y + Math.Sin(theta) * (t * radius2 + (1.0 - t) * radius1);
                        points.Add(new IntPoint(Polygon2D.ToIntSpace(x),
                            Polygon2D.ToIntSpace(y)));
                    }
                }
                return points;
            }
        } 
        class D03 : D02
        {
            public IntPoint GetFlashPoint(FS format, MO units)
            {
                return new IntPoint(
                    Polygon2D.ToIntSpace(units.ToPathCAMUnits(format.ScaleGerberInt(newPoint.x))),
                    Polygon2D.ToIntSpace(units.ToPathCAMUnits(format.ScaleGerberInt(newPoint.y))));
            }
            public D03(string tokenString) : base(tokenString)
            {
                name = "[D03]";
            }
        }
        class Dnn : GerberToken
        {
            public string aperture_ident = "";
            public Dnn(string tokenString)
            {
                string aperture_regex = @"D[0]*[1-9][0-9]+";

                Regex r = new Regex("(?<aperture_ident>" + aperture_regex + @")\*");
                Match m = r.Match(tokenString);
                if (m.Success)
                {
                    aperture_ident = m.Groups["aperture_ident"].Value;
                }
            }
            public override string ToString()
            {
                return "[Dnn] Select Aperture " + aperture_ident;
            }
            // Set current aperature
        }

        class MO : GerberToken
        {
            public Units units;
            public MO(string tokenString)
            {
                string mo_regex = @"%MO(?<units>MM|IN)\*%";
                Regex r = new Regex(mo_regex);
                Match m = r.Match(tokenString);
                if (m.Success)
                {
                    string unit_string = m.Groups["units"].Value;
                    if (unit_string == "MM")
                    {
                        units = Units.Millimeters;
                    }
                    else if(unit_string == "IN")
                    {
                        units = Units.Inches;
                    }
                    else
                    {
                        // Default to millimeters
                        units = Units.Millimeters;
                    }
                }
                
            }
            // PathCAM uses inches... for now :).
            // A future update will probably migrate to
            // millimeters or just meters.
            public double ToPathCAMUnits(double x)
            {
                if (units == Units.Inches)
                {
                    return x;
                }
                return x * (1.0 / 25.4);
            }
            public double ToPathCAMUnits(decimal d)
            {
                return ToPathCAMUnits(Decimal.ToDouble(d));
            }
            public override string ToString()
            {
                return "[M0] Units: " + units.ToString();
            }
        }
        class FS : GerberToken
        {
            double scale = 1.0 / 1000000; // Default to 6 decimal places
            public FS(string tokenString)
            {
                // Coordinate digits are two digits: N before the decimal and N after.
                // Current Gerber files should only use "6" after, but older files might
                // have other values.
                string coordinate_digits_regex = @"[1-6][1-6]";
                string la_regex = @"%FSLA" +
                    "X" + "(?<x_format>" + coordinate_digits_regex + ")" +
                    "Y" + "(?<y_format>" + coordinate_digits_regex + ")" + @"\*%";


                Regex r = new Regex(la_regex);
                Match m = r.Match(tokenString);
                if (m.Success)
                {
                    string x_format = m.Groups["x_format"].Value;
                    string y_format = m.Groups["y_format"].Value;
                    if (x_format != y_format)
                    {
                        Console.WriteLine("Gerber Error: LP command x format does not match y format: " + x_format + " != " + y_format);
                    }
                    int decimal_places = int.Parse(x_format) % 10;
                    scale = 1.0 / Math.Pow(10, decimal_places);
                }
            }

            internal double ScaleGerberInt(int x)
            {
                return scale * x;
            }
            public override string ToString()
            {
                return "[FS] Scale = " + scale;
            }
        }

        class AD : GerberToken
        {
            //List<Vector2> draw_paths = new List<Vector2>();
            //void IOpenGLDrawable.Draw()
            //{
            //    GL.Color3(Color.Black);
            //    GL.Begin(PrimitiveType.LineLoop);
            //    foreach (Vector2 p in draw_paths)
            //    {
            //        GL.Vertex3(p.X, p.Y, 0.0f);
            //    }
            //    GL.End();
            //}
            public bool valid;
            // Aperature Definition

            // The aperture identification being defined(≥10). The
            // aperture numbers can range from 10 up to
            // 2.147.483.647 (max int 32). The D00 to D09 are
            // reserved and cannot be used for apertures.Once an
            // aperture number is assigned it cannot be re-assigned
            // – thus apertures are uniquely identified by their
            // number.
            public string aperture_ident;

            // Set the shape of the aperture by calling a template
            // with actual parameters
            // template_call is a combination of template_name and parameters.

            // The name of the template, either a standard aperture
            // or macro (see 2.2). It follows the syntax of names
            // (see 3.4.5).
            public string template_name;

            // [',' parameter {'X' parameters}*]; The number and meaning of the actual parameters
            // depend on the template.Parameters are decimals.
            // All sizes are in the unit of the MO command.
            public decimal parameter;
            public List<decimal> x_parameters;

            private IEnumerable<IntPoint> CreateCircle(double diameter, double rotationDegrees, int points)
            {
                double theta = 0.0 + rotationDegrees * Math.PI / 180.0; // rotation is in degrees counter-clockwise
                double increment = Math.PI * 2.0 / (double)points;

                for (int i = 0; i < points; i++)
                {
                    double x = 0.5 * Math.Cos(theta) * diameter;
                    double y = 0.5 * Math.Sin(theta) * diameter;
                    yield return new IntPoint(
                        Polygon2D.ToIntSpace(x),
                        Polygon2D.ToIntSpace(y));
                    theta += increment;
                }
            }


            bool polygonMade = false;
            private void MakePolygon(MO units)
            {
                if (polygonMade)
                {
                    return;
                }
                polygonMade = true;
                paths = new List<List<IntPoint>>();
                if (template_name == "C")
                {
                    double diameter = units.ToPathCAMUnits(parameter);
                    //for (double theta = 0.0f; theta < 2.0f * Math.PI - 0.5 * Math.PI / 10.0d; theta += Math.PI / 10.0d)
                    //{
                    //    double x = 0.5 * Math.Cos(theta) * Decimal.ToDouble(parameter);
                    //    double y = 0.5 * Math.Sin(theta) * Decimal.ToDouble(parameter);
                    //    yield return new IntPoint(
                    //        Polygon2D.ToIntSpace(x),
                    //        Polygon2D.ToIntSpace(y));
                    //}
                    ClipperHelper c = new ClipperHelper();
                    c.Add(new List<IntPoint>(CreateCircle(diameter, 0, 24)));
                    if (x_parameters.Count >= 1)
                    {
                        double holeDiameter = units.ToPathCAMUnits(x_parameters[0]);
                        c.Remove(new List<IntPoint>(CreateCircle(holeDiameter, 0, 24)));
                    }
                    paths = c.GetPaths();
                }
                else if (template_name == "R")
                {
                    double x_size = units.ToPathCAMUnits(parameter);
                    // y_size should always be specified.  But of for some reason it's not, default to x_size.
                    double y_size = units.ToPathCAMUnits(x_parameters.Count >= 1 ? x_parameters[0] : parameter);

                    Int64 x = Polygon2D.ToIntSpace(x_size * 0.5);
                    Int64 y = Polygon2D.ToIntSpace(y_size * 0.5);
                    // Rectangle
                    ClipperHelper c = new ClipperHelper();
                    c.Add(new List<IntPoint>()
                    {
                        new IntPoint(-x, -y),
                        new IntPoint(-x, y),
                        new IntPoint(x, y),
                        new IntPoint(x, -y),
                    });
                    if (x_parameters.Count >= 2)
                    {
                        double holeDiameter = units.ToPathCAMUnits(x_parameters[0]);
                        c.Remove(new List<IntPoint>(CreateCircle(holeDiameter, 0, 24)));
                    }
                    paths = c.GetPaths();
                }
                else if (template_name == "O")
                {
                    // Obround
                    
                    double x_size = units.ToPathCAMUnits(parameter);
                    // y_size should always be specified.  But of for some reason it's not, default to x_size.
                    double y_size = units.ToPathCAMUnits(x_parameters.Count >= 1 ? x_parameters[0] : parameter);

                    double diameter = Math.Min(x_size, y_size);

                    int points = 24;
                    double rotationDegrees = 0.0;

                    double theta = 0.0 + rotationDegrees * Math.PI / 180.0; // rotation is in degrees counter-clockwise
                    double increment = Math.PI * 2.0 / (double)points;

                    List<IntPoint> path = new List<IntPoint>();
                    for (int i = 0; i < points; i++)
                    {
                        double x = 0.5 * Math.Cos(theta) * diameter;
                        double y = 0.5 * Math.Sin(theta) * diameter;
                        if (x_size > y_size)
                        {
                            x += 0.5 * (x_size - diameter) * Math.Sign(x);    
                        }
                        else
                        {
                            y += 0.5 * (y_size - diameter) * Math.Sign(y);
                        }
                        path.Add(new IntPoint(
                            Polygon2D.ToIntSpace(x),
                            Polygon2D.ToIntSpace(y)));
                        theta += increment;
                    }
                    ClipperHelper c = new ClipperHelper();
                    c.Add(path);

                    if (x_parameters.Count >= 2)
                    {
                        double holeDiameter = units.ToPathCAMUnits(x_parameters[0]);
                        c.Remove(new List<IntPoint>(CreateCircle(holeDiameter, 0, 24)));
                    }

                    paths = c.GetPaths();
                }
                else if (template_name == "P")
                {
                    // Polygon
                    double outerDiameter = units.ToPathCAMUnits(parameter);
                    int numVertices = Decimal.ToInt32(x_parameters[0]);
                    double rotation = 0;
                    double hole_diameter = 0;
                    if (x_parameters.Count >= 2)
                    {
                        rotation = units.ToPathCAMUnits(x_parameters[1]);
                    }
                    if(x_parameters.Count >= 3)
                    {
                        hole_diameter = units.ToPathCAMUnits(x_parameters[2]);
                    }
                    ClipperHelper c = new ClipperHelper();

                    c.Add(new List<IntPoint>(CreateCircle(outerDiameter, rotation, numVertices)));
                    if (hole_diameter > 0)
                    {
                        c.Remove(new List<IntPoint>(CreateCircle(hole_diameter, 0, 24)));
                    }
                    paths = c.GetPaths();
                }
            }

            public List<List<IntPoint>> paths = new List<List<IntPoint>>();
            public AD(string tokenString)
            {
                x_parameters = new List<decimal>();

                string aperture_regex = @"D[0]*[1-9][0-9]+";
                string decimal_regex = @"[+-]?(?:(?:(?:[0-9]+)(?:\.[0-9]*)?)|(:?\.[0-9]+))";
                string name_regex = @"[._a-zA-Z$][._a-zA-Z0-9]*";

                Regex r = new Regex("%AD" +
                    "(?<aperture_ident>" + aperture_regex + ")" +
                    "(?<template_name>" + name_regex + ")," +
                    "(?<parameter>" + decimal_regex + ")" +
                    "(?:X(?<x_parameters>" + decimal_regex + @"))*\*%");
                Match m = r.Match(tokenString);
                if (m.Success)
                {
                    aperture_ident = m.Groups["aperture_ident"].Value;
                    template_name = m.Groups["template_name"].Value;
                    parameter = decimal.Parse(m.Groups["parameter"].Value);
                    var regex_x_parameters = m.Groups["x_parameters"];
                    foreach (Capture c in regex_x_parameters.Captures)
                    {
                        x_parameters.Add(decimal.Parse(c.Value));
                    }

                    Console.WriteLine("Aperature Definition: " + aperture_ident +
                        "\n\ttemplate_name = " + template_name +
                        "\n\tparameter: " + parameter +
                        "\n\tx_parameter: " + String.Join(",", x_parameters.Select(p => p.ToString()).ToArray()));
                }
                valid = m.Success;
                //Console.WriteLine("looked at " + tokenString);

                // TODO: handle apertures with holes.
                //List<IntPoint> path = new List<IntPoint>();
                //foreach (IntPoint p in this.GetPolygon())
                //{
                //    path.Add(p);
                //}
                //paths.Add(path);
                //MakePolygon();
            }

            internal List<List<IntPoint>> Stroke(List<IntPoint> stroke, MO currentUnits)
            {
                MakePolygon(currentUnits);
                if (paths.Count == 0)
                {
                    // This could be an empty aperture (circle with a hole of the same size, for example).
                }
                if (paths.Count == 1)
                {
                    List<IntPoint> aperturePoly = paths[0];
                    List<List<IntPoint>> polys = Clipper.MinkowskiSum(aperturePoly, stroke, false);
                    return polys;
                }
                if (paths.Count > 1)
                {
                    // TODO: for more complex apertures (with multiple independent outer polygons),
                    // how should they be stroked?
                    List<List<IntPoint>> polys = new List<List<IntPoint>>();
                    Clipper c = new Clipper();
                    c.AddPaths(paths, PolyType.ptSubject, true);
                    PolyTree polyTree = new PolyTree();
                    c.Execute(ClipType.ctDifference, polyTree, PolyFillType.pftNonZero, PolyFillType.pftNonZero);
                    PolyNode node = polyTree.GetFirst();
                    while (node != null)
                    {
                        if (!node.IsHole)
                        {
                            polys.AddRange(Clipper.MinkowskiSum(node.Contour, stroke, false));
                        }
                        node = node.GetNext();
                    }
                    return polys;
                }
                return new List<List<IntPoint>>();
            }

            internal List<List<IntPoint>> Flash(IntPoint flashPoint, MO currentUnits)
            {
                MakePolygon(currentUnits);
                List<List<IntPoint>> translatedPaths = new List<List<IntPoint>>();
                foreach (var path in paths)
                {
                    List<IntPoint> translatedPath = new List<IntPoint>();
                    foreach (var point in path)
                    {
                        translatedPath.Add(new IntPoint(flashPoint.X + point.X, flashPoint.Y + point.Y));
                    }
                    translatedPaths.Add(translatedPath);
                }
                return translatedPaths;
            }
        }
        class AM : GerberToken { }

        class LP : GerberToken
        {
            public Polarity polarity;
            private string polarityString = null;
            public LP(string tokenString)
            {
                string lp_regex = @"%LP(?<polarity>C|D)\*%";
                Regex r = new Regex(lp_regex);
                Match m = r.Match(tokenString);
                if (m.Success)
                {
                    polarityString = m.Groups["polarity"].Value;
                    if (polarityString == "C")
                    {
                        polarity = Polarity.Clear;
                    }
                    else if (polarityString == "D")
                    {
                        polarity = Polarity.Dark;
                    }
                }
                if (polarityString == null)
                {
                    Console.WriteLine("Bad LP command, polarity defaulting to dark");
                    polarity = Polarity.Dark;
                }
            }
            public override string ToString()
            {
                return "[LP] " + polarity.ToString();
            }
        }
        class LM : GerberToken { }
        class LR : GerberToken { }
        class LS : GerberToken { }

        class TF : GerberToken
        {
            public readonly string fileAttributeName;
            public readonly string[] fields;
            public TF(string tokenString)
            {
                string tf_regex = @"%TF" +
                    @"(?<file_attribute_name>[\._a-zA-Z]+)" + 
                    @"(?<fields>,[^\*%]+)*" + 
                    @"\*%";
                Regex r = new Regex(tf_regex);
                Match m = r.Match(tokenString);
                if (m.Success)
                {
                    fileAttributeName = m.Groups["file_attribute_name"].Value;
                    fields = m.Groups["fields"].Value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                }
            }
            public override string ToString()
            {
                return "[TF] " + fileAttributeName + " = " + string.Join(",", fields);
            }
        }
        class TA : GerberToken
        {
            public readonly string apertureAttributeName;
            public readonly string[] fields;
            public TA(string tokenString)
            {
                string ta_regex = @"%TA" +
                    @"(?<aperture_attribute_name>[\._a-zA-Z]+)" +
                    @"(?<fields>,[^\*%]+)*" +
                    @"\*%";
                Regex r = new Regex(ta_regex);
                Match m = r.Match(tokenString);
                if (m.Success)
                {
                    apertureAttributeName = m.Groups["aperture_attribute_name"].Value;
                    fields = m.Groups["fields"].Value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                }
            }
        }
        class TO : GerberToken
        {
            public readonly string objectAttributeName;
            public readonly string[] fields;
            public TO(string tokenString)
            {
                string ta_regex = @"%TO" +
                    @"(?<object_attribute_name>[\._a-zA-Z]+)" +
                    @"(?<fields>,[^\*%]+)*" +
                    @"\*%";
                Regex r = new Regex(ta_regex);
                Match m = r.Match(tokenString);
                if (m.Success)
                {
                    objectAttributeName = m.Groups["object_attribute_name"].Value;
                    fields = m.Groups["fields"].Value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                }
            }
        }
        class TD : GerberToken
        {
            // Delete an attribute from the attributes dictionary
            public readonly string deletedAttributeName;
            public TD(string tokenString)
            {
                string ta_regex = @"%TD" +
                    @"(?<deleted_attribute_name>[\._a-zA-Z]+)" +
                    @"\*%";
                Regex r = new Regex(ta_regex);
                Match m = r.Match(tokenString);
                if (m.Success)
                {
                    deletedAttributeName = m.Groups["deleted_attribute_name"].Value;
                }
            }
        }

        class M02 : GerberToken { }

        private GerberToken ReadCommandToken(StreamReader r)
        {
            StringBuilder sb = new StringBuilder();
            int i = -1;
            do
            {
                i = r.Read();
                if (i == -1)
                {
                    Console.WriteLine("End of file reached!  Incomplete Token: " + sb.ToString());
                    return null;
                }
                char c = (char)i;
                sb.Append(c);

                // Hack for kicad generated gerber files with G02 not followed by *:
                // G02X149000000Y-55990000I5250000J0D01*
                if ((sb.ToString() == "G02" || sb.ToString() == "G03") && r.Peek() != '*')
                {
                    sb.Append("*");
                    break;
                }
            }
            while (i != '*');

            GerberToken g = null;
            string s = sb.ToString();
            if (s == "G36*") { g = new G36(); }
            else if (s == "G37*") { g = new G37(); }

            else if (s == "G01*") { g = new G01(); }
            else if (s == "G02*") { g = new G02(); }
            else if (s == "G03*") { g = new G03(); }
            else if (s.StartsWith("G04")) { g = new G04(s); }
            else if (s == "G75*") { g = new G75(); }
            else if (s == "M02*") { g = new M02(); }
            else if (s.EndsWith("D01*")) { g = new D01(s); }
            else if (s.EndsWith("D02*")) { g = new D02(s); }
            else if (s.EndsWith("D03*")) { g = new D03(s); }
            else if (s.StartsWith("D")) { g = new Dnn(s); }

            if (g != null)
            {
                g.SetTokenString(s);
                return g;
            }

            Console.WriteLine("Unhandled Token: " + s);
            return null;
        }

        private GerberToken ReadExtendedCommandToken(StreamReader r)
        {
            StringBuilder sb = new StringBuilder();
            int i = -1;
            sb.Append((char)r.Read()); // First character is a %
            do
            {
                i = r.Read();
                if (i == -1)
                {
                    Console.WriteLine("End of file reached, incomplete token! " + sb.ToString());
                    return null; // Incompl
                }
                char c = (char)i;
                sb.Append(c);
            }
            while (i != '%');

            GerberToken g = null;
            string s = sb.ToString();
            if (s.StartsWith("%TF")) { g = new TF(s); }
            else if (s.StartsWith("%TA")) { g = new TA(s); }
            else if (s.StartsWith("%TO")) { g = new TO(s); }
            else if (s.StartsWith("%TD")) { g = new TD(s); }

            else if (s.StartsWith("%LP")) { g = new LP(s); }
            else if (s.StartsWith("%LM")) { g = new LM(); }
            else if (s.StartsWith("%LR")) { g = new LR(); }
            else if (s.StartsWith("%LS")) { g = new LS(); }

            else if (s.StartsWith("%MO")) { g = new MO(s); }
            else if (s.StartsWith("%AD")) { g = new AD(s); }
            else if (s.StartsWith("%AM")) { g = new AM(); }
            else if (s.StartsWith("%FS")) { g = new FS(s); }

            if (g != null)
            {
                g.SetTokenString(s);
                return g;
            }

            Console.WriteLine("Unhandled Token: " + sb.ToString());
            return null;
        }


        IEnumerable<GerberToken> ParseGerberTokens(StreamReader r)
        {

            while (!r.EndOfStream)
            {
                int c = r.Peek();
                switch (c)
                {
                    case -1:
                        break;
                    case 'X':
                    case 'Y':
                    case 'I':
                    case 'J':
                    // D01, D02, D03

                    case 'M':
                    // M02*
                    case 'D':
                    // Dnn
                    case 'G':
                        // G01, G02, G03, G75, G04
                        yield return ReadCommandToken(r);
                        break;
                    case '%':
                        // Extended command (read until following %)
                        yield return ReadExtendedCommandToken(r);
                        break;

                    default:

                        char ch = (char)r.Read();// Burn a character??
                        if (ch == ' ' || ch == '\n' || ch == '\t' || ch == '\r')
                        {

                        }
                        else
                        {
                            Console.WriteLine("Ignored Non Whitespace Character '" + ch + "'");
                        }
                        break;
                }
            }
        }
        IEnumerable<GerberToken> ParseGerberTokensUntil<T>(StreamReader r)
        {
            foreach (GerberToken g in ParseGerberTokens(r))
            {
                if (g.GetType() is T)
                {
                    yield break;
                }
                else
                {
                    yield return g;
                }
            }
        }

        List<Object> things;

        //public static GerberLoader Load(string filename)
        //{
        //    //triangles.AddTriangle(new Triangle(Vector3.Zero, new Vector3(5, 0, 0), new Vector3(0, 5, 0)));
        //    GERBER_Loader loader = new GERBER_Loader(filename);
        //    return loader;
        //}

        enum Units
        {
            Unknown, // Default
            Millimeters,
            Inches,
        }
        enum PlotState
        {
            Unknown, // Default
            LinearPlotting,
            CounterClockwiseArc,
            ClockwiseArc,
        }
        enum Polarity
        {
            Dark, // Default to dark
            Clear,
        }
        enum Mirroring
        {
            NoMirror, // default to no mirror
        }
        enum Rotation
        {
            NoRotation, // default to no rotation
        }
        enum Scaling
        {
            NoScaling, // default to no scaling
        }

        public GERBER_Loader(string filename)
        {
            PlotState plotState;
            plotState = PlotState.Unknown;
            things = new List<object>();

            // Gerber files are specified to be encoded as UTF-8 Unicode
            //string contents = System.IO.File.ReadAllText(filename, Encoding.UTF8);

            // Current point should start as undefined.
            this.currentPoint = new GerberPoint();
            currentPoint.x = 0;
            currentPoint.y = 0;
            currentPoint.i = 0;
            currentPoint.j = 0;
            StreamReader r = new StreamReader(filename, Encoding.UTF8);
            AD attachedAperture = null;
            List<AD> apertureDefinitions = new List<AD>();

            // This is the final image, constructed one step at a time.
            List<List<IntPoint>> imagePaths = new List<List<IntPoint>>();
            Clipper imageBuilder = new Clipper();

            // Use ptSubject for dark, ptClip for clear
            PolyType lightOrDark = PolyType.ptSubject;
            Polarity currentPolarity = Polarity.Dark; // Specified Default

            FS currentFormat = null;
            MO currentUnits = null;

            bool isInRegion = false;
            List<IntPoint> region = null;
            ClipperHelper regionBuilder = null;


            foreach (GerberToken g in ParseGerberTokens(r))
            {
                if (g != null)
                {
                    Console.WriteLine("Token: " + g.ToString());
                }

                if (g is D02) // Need to update any D0* commands so they have a valid current point.
                {
                    D02 d02 = g as D02;
                    d02.SetStartPoint(currentPoint);
                    currentPoint = d02.newPoint;
                }

                if (g is FS)
                {
                    currentFormat = g as FS;
                }
                if (g is MO)
                {
                    currentUnits = g as MO;
                }
                if (g is TF)
                {
                    TF tf = g as TF;
                    fileAttributeDictionary[tf.fileAttributeName] = tf.fields;
                }

                // Hack
                if (g is G04)
                {
                    G04 g04 = g as G04;
                    // Without the X2 format box checked in kicad, the FileFunction attribute
                    // goes into a comment like this:
                    //G04 #@! TF.FileFunction,Copper,L1,Top*
                    //
                    // With the X2 box checked, it would look like this:
                    //%TF.FileFunction,Copper,L1,Top*%
                    if (g04.comment.Contains("#@! TF.FileFunction"))
                    {
                        TF tf = new TF(g04.comment.Replace("#@! ", "%") + "*%");
                        fileAttributeDictionary[tf.fileAttributeName] = tf.fields;
                    }
                }

                if (g is LP)
                {
                    // Load Polarity
                    LP lp = g as LP;
                    if (lp.polarity != currentPolarity)
                    {
                        // Execute the clipping to preserve order from gerber file
                        PolyTree polyTree = new PolyTree();
                        imageBuilder.Execute(ClipType.ctDifference, polyTree, PolyFillType.pftNonZero, PolyFillType.pftNonZero);
                        imageBuilder = new Clipper();
                        PolyNode node = polyTree.GetFirst();
                        while (node != null)
                        {
                            imageBuilder.AddPath(node.Contour, PolyType.ptSubject, true);
                            node = node.GetNext();
                        }
                    }
                    switch (lp.polarity)
                    {
                        case Polarity.Clear:
                            lightOrDark = PolyType.ptClip;
                            break;
                        case Polarity.Dark:
                            lightOrDark = PolyType.ptSubject;
                            break;
                    }
                    currentPolarity = lp.polarity;
                }

                // Aperature Definitions
                if (g is AD)
                {
                    AD apertureDefinition = g as AD;
                    things.Add(apertureDefinition);
                    attachedAperture = apertureDefinition;
                    apertureDefinitions.Add(apertureDefinition);
                }
                if (g is Dnn)
                {
                    Dnn dnn = g as Dnn;
                    foreach (AD d in apertureDefinitions)
                    {
                        if (d.aperture_ident == dnn.aperture_ident)
                        {
                            attachedAperture = d;
                            Console.WriteLine("Selected aperture " + d.aperture_ident);
                        }
                    }
                }

                if (g is G36)
                {
                    Console.WriteLine("Start of Region");
                    isInRegion = true;
                    //var it = ParseGerberTokensUntil<G37>(r);
                    //while (it.Any())
                    //{
                    //    //GerberToken d02 = it.First();
                    //    //Console.WriteLine("  Contour: " + d02);
                    //    foreach (GerberToken a in it.TakeWhile((token) => token is D01 || token is G01 || token is G02 || token is G03))
                    //    {
                    //        Console.WriteLine("    Contour: " + a);
                    //    }
                    //}
                    //Console.WriteLine("End of Region");
                    regionBuilder = new ClipperHelper();
                    region = new List<IntPoint>();
                }
                if (g is G37)
                {
                    Console.WriteLine("End of Region");
                    if (region.Count > 0)
                    {
                        // A region contour is supposed to end where it starts.
                        // But clipper doesn't need (or want) another point at the start.
                        if (region.First() == region.Last())
                        {
                            region.RemoveAt(0);
                        }
                        regionBuilder.Add(region);
                    }
                    //imageBuilder = new Clipper();

                    // Execute the clipping to preserve order from gerber file
                    PolyTree polyTree = new PolyTree();
                    imageBuilder.Execute(ClipType.ctDifference, polyTree, PolyFillType.pftNonZero, PolyFillType.pftNonZero);
                    imageBuilder = new Clipper();
                    imageBuilder.AddPaths(Clipper.PolyTreeToPaths(polyTree), PolyType.ptSubject, true);
                    //PolyNode node = polyTree.GetFirst();
                    //while (node != null)
                    //{
                    //    imageBuilder.AddPath(node.Contour, PolyType.ptSubject, true);
                    //    node = node.GetNext();
                    //}
                    List<List<IntPoint>> builderPaths = regionBuilder.GetPaths();
                    //var newpths = builderPaths.First();

                    //newpths = Clipper.SimplifyPolygon(newpths, PolyFillType.pftNonZero).FirstOrDefault();
                    //newpths.Reverse();

                    imageBuilder.AddPaths(builderPaths, lightOrDark, true);
                    //imageBuilder.AddPath(newpths, lightOrDark, true);
                    polyTree = new PolyTree();
                    imageBuilder.Execute(ClipType.ctDifference, polyTree, PolyFillType.pftNonZero, PolyFillType.pftNonZero);
                    imageBuilder = new Clipper();
                    imageBuilder.AddPaths(Clipper.PolyTreeToPaths(polyTree), PolyType.ptSubject, true);



                    isInRegion = false;
                    region = null;
                    regionBuilder = null;
                }

                if (g is G01)
                {
                    plotState = PlotState.LinearPlotting;
                }
                if (g is G02)
                {
                    plotState = PlotState.ClockwiseArc;
                }
                if (g is G03)
                {
                    plotState = PlotState.CounterClockwiseArc;
                }
                if (g is G75)
                {
                    // Must be issued before the first D01 in circular mode?
                    // For compatability with older formats?
                }


                if (isInRegion)
                {
                    if (g is D01)
                    {
                        D01 d01 = g as D01;
                        switch (plotState)
                        {
                            case PlotState.LinearPlotting:
                                // Stroke from currentPoint to d01.newPoint
                                region.AddRange(d01.GetLinearStroke(currentFormat, currentUnits));
                                region.RemoveAt(region.Count - 1);
                                break;

                            case PlotState.ClockwiseArc:
                                region.AddRange(d01.GetArcStroke(currentFormat, currentUnits, true));
                                break;

                            case PlotState.CounterClockwiseArc:
                                region.AddRange(d01.GetArcStroke(currentFormat, currentUnits, false));
                                break;
                        }
                    }
                    else if (g is D03)
                    {
                        // Shouldn't happen within a region
                    }
                    else if (g is D02) // D01 and D03 are also a D02, hence the if/else.
                    {
                        // D02 closes the existing contour and begins a new one.
                        if (region.Count > 0)
                        {
                            if (region.First() == region.Last())
                            {
                                region.RemoveAt(0);
                            }
                            if (lightOrDark == PolyType.ptSubject)
                            {
                                regionBuilder.Add(region);
                            }
                            else if (lightOrDark == PolyType.ptClip)
                            {
                                regionBuilder.Remove(region);
                            }
                            //imageBuilder.AddPath(region.First(), lightOrDark, true);
                        }
                        region.Clear();
                    }
                }
                else
                {
                    if (g is D01)
                    {
                        D01 d01 = g as D01;
                        if (attachedAperture == null)
                        {
                            Console.WriteLine("Gerber Load Error: saw D01 command, but no attached aperture.");
                            continue;
                        }

                        if (plotState == PlotState.Unknown)
                        {
                            Console.WriteLine("Gerber File Error: plot state was not defined before D0* commands");
                            // Default to linear mode.
                            plotState = PlotState.LinearPlotting;
                        }

                        List<IntPoint> stroke = null;
                        switch (plotState)
                        {
                            case PlotState.Unknown:
                                break;

                            case PlotState.LinearPlotting:
                                // Stroke from currentPoint to d01.newPoint
                                stroke = d01.GetLinearStroke(currentFormat, currentUnits);
                                break;

                            case PlotState.ClockwiseArc:
                                stroke = d01.GetArcStroke(currentFormat, currentUnits, true);
                                break;

                            case PlotState.CounterClockwiseArc:
                                stroke = d01.GetArcStroke(currentFormat, currentUnits, false);

                                break;
                        }
                        if (stroke != null)
                        {
                            imageBuilder.AddPaths(attachedAperture.Stroke(stroke, currentUnits), lightOrDark, true);

                            //if (true) // Testing flatten on every add
                            //{
                            //    PolyTree polyTree = new PolyTree();
                            //    imageBuilder.Execute(ClipType.ctDifference, polyTree, PolyFillType.pftNonZero, PolyFillType.pftNonZero);
                            //    imageBuilder = new Clipper();
                            //    imageBuilder.AddPaths(Clipper.PolyTreeToPaths(polyTree), PolyType.ptSubject, true);
                            //}
                        }
                        //currentPoint = d01.newPoint;
                    }
                    if (g is D03)
                    {
                        // Flash the aperture
                        D03 d03 = g as D03;
                        if (attachedAperture == null)
                        {
                            Console.WriteLine("Gerber Load Error: saw D03 command, but no attached aperture.");
                            continue;
                        }
                        imageBuilder.AddPaths(attachedAperture.Flash(d03.GetFlashPoint(currentFormat, currentUnits), currentUnits), lightOrDark, true);

                        //if (true) // Testing flatten on every add
                        //{
                        //    PolyTree polyTree = new PolyTree();
                        //    imageBuilder.Execute(ClipType.ctDifference, polyTree, PolyFillType.pftNonZero, PolyFillType.pftNonZero);
                        //    imageBuilder = new Clipper();
                        //    imageBuilder.AddPaths(Clipper.PolyTreeToPaths(polyTree), PolyType.ptSubject, true);
                        //}

                        //currentPoint = d03.newPoint; // Already handled because D03 is also a D02
                    }
                }
            }

            //Console.WriteLine("GERBER_Loader: Triangle Generation Started");
            //Stopwatch triangleGenerationTimer = new Stopwatch();
            //triangleGenerationTimer.Start();
            if (true)
            {
                PolyTree polyTree = new PolyTree();
                imageBuilder.Execute(ClipType.ctDifference, polyTree, PolyFillType.pftPositive, PolyFillType.pftNonZero);
            
                //PrintPolyNodeInfo(polyTree, "<TREE>  ");
                //debugPolygons = Clipper.PolyTreeToPaths(polyTree);
                // Children of the first node are the outer polygons.
                //foreach (PolyNode outerPolygon in polyTree.Childs)
                //{
                //    foreach (Triangle t in PolyNodeTriangles(outerPolygon))
                //    {
                //        triangles.AddTriangle(t);
                //    }
                //}
                finalPolyTree = polyTree;
            }
            //triangleGenerationTimer.Stop();
            //Console.WriteLine("GERBER_Loader: Triangle Generation took " + triangleGenerationTimer.ElapsedMilliseconds + " milliseconds");
        }

        public TriangleMesh triangles = new TriangleMesh();
        public PolyTree finalPolyTree = null;

        private void PrintPolyNodeInfo(PolyNode node, string start)
        {
            Console.WriteLine(start + "Node IsHole: " + node.IsHole + ", Children: " + node.Childs.Count + ", Contour: " + node.Contour.Count);
            foreach (PolyNode child in node.Childs)
            {
                PrintPolyNodeInfo(child, start + "  ");
            }
        }

        //private IEnumerable<Triangle> PolyNodeTriangles(PolyNode node)
        //{
        //    Polygon2D polygon2D = new Polygon2D();
        //
        //    if (node.IsHole)
        //    {
        //        // No new polygon to add at this level, but children may
        //        // have polygons.
        //        foreach (PolyNode child in node.Childs)
        //        {
        //            foreach (Triangle t in PolyNodeTriangles(child))
        //            {
        //                yield return t;
        //            }
        //        }
        //    }
        //    else
        //    {
        //        //polygon2D.Add(node.Contour); // Add one outside polygon
        //        polygon2D.AddOutside(node.Contour);
        //        foreach (PolyNode child in node.Childs)
        //        {
        //            if (child.IsHole)
        //            {
        //                //polygon2D.Add(child.Contour); // and all holes inside.
        //                polygon2D.AddInside(child.Contour);
        //            }
        //            foreach (Triangle t in PolyNodeTriangles(child))
        //            {
        //                yield return t;
        //            }
        //        }
        //    }
        //    //foreach (Triangle t in polygon2D.EarClipForTriangles())
        //    foreach (Triangle t in polygon2D.LibTessTriangles())
        //    {
        //        yield return t;
        //    }
        //}




    }




}
