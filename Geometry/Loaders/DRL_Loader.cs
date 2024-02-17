using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ClipperLib;

namespace Geometry
{
    /// <summary>
    /// Loader for Excellon drill file format (PCB fabrication)
    /// </summary>
    public class DRL_Loader
    {
        enum Units
        {
            Inches,
            Millimeters,
        }

        public class Tools
        {
            int number;
            float diameter; // Diameter will be converted to PathCAM units.
        }

        public static float MMToPathCAMUnits(float f)
        {
            return f / 25.4f;
        }

        private static IEnumerable<IntPoint> CreateCircle(double diameter, double x_center, double y_center, int points)
        {
            double theta = 0.0;
            double increment = Math.PI * 2.0 / (double)points;

            for (int i = 0; i < points; i++)
            {
                double x = x_center + 0.5 * Math.Cos(theta) * diameter;
                double y = y_center + 0.5 * Math.Sin(theta) * diameter;
                yield return new IntPoint(
                    Polygon2D.ToIntSpace(x),
                    Polygon2D.ToIntSpace(y));
                theta += increment;
            }
        }

        public static PolyTree Load(string filename)
        {
            Units units = Units.Millimeters;
            bool isIncremental = false;
            int version = 2;
            int format = 1;
            string[] strings = System.IO.File.ReadAllLines(filename);
            List<Tools> tools = new List<Tools>();

            Dictionary<string, float> toolDict = new Dictionary<string, float>();
            bool headerOver = false;
            float currentDrillDiameter = 1.0f;

            Clipper c = new Clipper();

            double xydivider = 1.0;

            foreach (string s in strings)
            {

                // File Format Information from https://madpcb.com/glossary/excellon-format/


                // Number format for X...Y... values
                // ; FORMAT={3:3

                Regex formatRegex = new Regex(@"FORMAT={(?<before_decimal>\d+):(?<after_decimal>\d+)");
                Match match = formatRegex.Match(s);
                if (match.Success)
                {
                    string after_decimal_string = match.Groups["after_decimal"].Value;
                    if (int.TryParse(after_decimal_string, out int digits_after_decimal))
                    {
                        xydivider = Math.Pow(10, digits_after_decimal);
                        Console.WriteLine("Line '" + s + "' Indicates " + digits_after_decimal +
                            " digits after decimal, setting xy divider to " + xydivider);
                    }
                }

                // M48      indicates the start of the header.  should always be the first line in the header
                if (s.StartsWith("M48"))
                {
                    // Start of header
                }
                // INCH,LZ  this actually has two pieces of information; INCH indicates that coordinates that
                //          follow are in inches and LZ indicates that the leading zeros in the coordinate data
                //          are included. (This implies that the trailing zeros are suppressed.The reading
                //          software needs to know both the units and where to re - insert the decimal point; 
                //          therefore one must have either leading or trailing zeros or a decimal point in the
                //          coordinate data.If the data is in millimeters then the command would be METRIC,LZ.
                //          If the trailing zeros are included then the command would be INCH, TZ.
                if (s.Contains("INCH"))
                {
                    units = Units.Inches;
                }
                if (s.Contains("METRIC"))
                {
                    units = Units.Millimeters;
                }
                // ICI      Incremental input of program coordinates. This is very rare nowadays; if not present
                //          assume the coordinates are absolute.
                if (s.StartsWith("ICI"))
                {
                    isIncremental = true;
                }
                // VER,1    Use version 1 X and Y axis layout. (As opposed to Version 2)
                if (s.StartsWith("VER") && s.Contains(","))
                {
                    int.TryParse(s.Split(',')[1], out version);
                }
                // FMAT,2   Use Format 2 commands; alternative would be FMAT,1
                if (s.StartsWith("FMAT") && s.Contains(","))
                {
                    int.TryParse(s.Split(',')[1], out format);
                }
                // T01C0.020   Defines tool 01 as having a diameter of 0.020 inch.For each tool used in the data
                //          the diameter should be defined here. There are additional parameters but if you are
                //          a PCB designer it is not up to you to specify feed rates and such.
                Regex r = new Regex(@"T(?<tool_number>\d+)C(?<diameter>[\.0-9]+)");
                Match m = r.Match(s);
                if (m.Success)
                {
                    string tool = m.Groups["tool_number"].Value;
                    string diameter_string = m.Groups["diameter"].Value;
                    if (float.TryParse(diameter_string, out float diameter))
                    {
                        toolDict.Add(tool, diameter);
                        Console.WriteLine("Tool " + tool + " diameter " + diameter.ToString());
                    }
                    else
                    {
                        Console.WriteLine("Tool " + tool + " failed to parse diameter from " + diameter_string);
                    }
                }
                // M95      End of the header.  Data that follows will be drill and / or route commands.
                // R#	    This command drills a series of equally spaced holes from the previously specified 
                //          hole. The number following the R specifies the number of repeats. An X and/or Y 
                //          coordinate must be used to define the spacing between hole centers.
                if (s.StartsWith("R#") || s.StartsWith("M97"))
                {
                    Console.WriteLine("DRL_Loader: unsupported drill file command: " + s);
                }
                // M97      It is possible to drill a series of holes that spell out words or numbers.The M97 
                //          and M98 commands allow you to program the CNC - 7 to write a message on the board.
                //          This feature can be used to identify a company or product, supply a part number etc..
                // %        Rewind.This is often used instead of M95.It stops the machine and tells it to wait 
                //          for a command to continue.
                if (s.StartsWith("%") || s.StartsWith("M95"))
                {
                    // End of header.  Could set a flag here to no longer look for tools.
                    headerOver = true;
                }


                r = new Regex(@"T(?<tool_number>\d+)$");
                m = r.Match(s);
                if (m.Success)
                {
                    string tool = m.Groups["tool_number"].Value;
                    if (toolDict.ContainsKey(tool))
                    {
                        currentDrillDiameter = toolDict[tool];
                    }
                }

                r = new Regex(@"X(?<x_coordinate>[+-]?[0-9\.]+)" + @"Y(?<y_coordinate>[+-]?[0-9\.]+)\s*$");
                m = r.Match(s);
                if (m.Success)
                {
                    string x_string = m.Groups["x_coordinate"].Value;
                    string y_string = m.Groups["y_coordinate"].Value;
                    if (float.TryParse(x_string, out float x) &&
                        float.TryParse(y_string, out float y))
                    {
                        x /= (float)xydivider;
                        y /= (float)xydivider;
                        Console.WriteLine("Drill Hole at " + x + ", " + y + " of diameter " + currentDrillDiameter);

                        float diameter = currentDrillDiameter;
                        if (units == Units.Millimeters)
                        {
                            // Convert them to inches.
                            diameter /= 25.4f;
                            x /= 25.4f;
                            y /= 25.4f;
                        }
                        
                        List<IntPoint> path = new List<IntPoint>(
                            CreateCircle(diameter, x, y, 9)
                            );
                        c.AddPath(path, PolyType.ptSubject, true);

                    }
                    else
                    {
                        Console.WriteLine("Failed to parse floats for drill: " + x_string + ", " + y_string);
                    }
                }


                // FMAT1        FMAT2           Explanation
                // G81          G05             turn on drill mode.
                // M02          M00             End of Program
                // M24          M01             End of Pattern
                // M26          M02             Repeat Pattern Offset(this is followed by a #X#Y to indicate the number of repeats in X and Y
                // M01          M06             Optional Stop
                // M27          M08             End of Step and Repeat
                // M00          M09             Stop for Inspection
                // M26X#Y#M21   M02X#Y#M80	
                // M26#Y#M22    M02X#Y#M90
                // R#M26        R#M22

            }
            PolyTree polyTree = new PolyTree();
            c.Execute(ClipType.ctUnion, polyTree, PolyFillType.pftNonZero, PolyFillType.pftNonZero);
            return polyTree;
        }
    }
}
