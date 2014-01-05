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
using OpenTK;
using Geometry;
using System.Text.RegularExpressions;

namespace Geometry
{
    public class OBJ_Loader
    {
        public static void Load(string filepath, TriangleMesh triMesh, float scale)
        {
            List<Vector3> vertices = new List<Vector3>();
            string[] strings = System.IO.File.ReadAllLines(filepath);
            
            // Lines starting with v are a vertex:
            // "v 10.2426 4.5e-013 -31.7638"
            Regex vertexRegex = new Regex(@"^v\s+(?<x>\S+)\s+(?<y>\S+)\s+(?<z>\S+)", RegexOptions.IgnoreCase);
            
            // Lines starting with f are a face.  The indices are <vertex>/<texture>/<normal>, where texture and normal are optional.
            // "f 1/1/1 2/2/1 3/3/1 4/4/1 5/5/1"
            Regex faceRegex = new Regex(@"^f(?<face_data>\s+(?<vertex>\d+)/?(?<texture_coordinate>\d+)?/?(?<vertex_normal>\d+)?)+", RegexOptions.IgnoreCase);

            foreach (string s in strings)
            {
                if (vertexRegex.IsMatch(s))
                {
                    Match m = vertexRegex.Match(s);
                    float x = float.Parse(m.Groups["x"].Value);
                    float y = float.Parse(m.Groups["y"].Value);
                    float z = float.Parse(m.Groups["z"].Value);
                    // Rotate 90 degrees about the X axis - for some reason .obj files saved from sketchup have this issue...
                    Vector3 v = new Vector3(x, -z, y);
                    vertices.Add(v * scale);
                }
                else if (faceRegex.IsMatch(s))
                {
                    Match m = faceRegex.Match(s);

                    //Console.WriteLine(m.Groups["face_data"].Captures.Count);
                    //Console.WriteLine(m.Groups["vertex"].Captures.Count);
                    //Console.WriteLine(m.Groups["texture_coordinate"].Captures.Count);
                    //Console.WriteLine(m.Groups["vertex_normal"].Captures.Count);

                    //Face face = new Face();
                    Polygon polygon = new Polygon();

                    CaptureCollection vert_captures = m.Groups["vertex"].Captures;
                    CaptureCollection texcoord_captures = m.Groups["texture_coordinate"].Captures;
                    CaptureCollection norm_captures = m.Groups["vertex_normal"].Captures;

                    var vertexIndices = vert_captures.Cast<Capture>().Select(capture => int.Parse(capture.Value) - 1);

                    foreach (var vertexIndex in vertexIndices)
                    {
                        if (vertexIndex < 0 || vertexIndex > vertices.Count)
                        {
                            Console.WriteLine("Bad vertex index {0}, only {1} vertices loaded", vertexIndex, vertices.Count);
                        }
                        else
                        {
                            polygon.Add(vertices[vertexIndex]);
                        }
                    }
                    //for (int i = 0; i < vert_captures.Count; i++)
                    //{
                    //    int vert_index = int.Parse(vert_captures[i].Value) - 1;
                    //    
                    //}
                    if (texcoord_captures.Count == vert_captures.Count)
                    {
                        // TODO: Add texture coordinates to the face
                    }
                    if (norm_captures.Count == vert_captures.Count)
                    {
                        // TODO: Add vertex normals to the face
                    }

                    if (polygon.Vertices.Count() < 3)
                    {
                        Console.WriteLine("Bad face defined, less than 3 vertices");
                    }
                    else
                    {
                        foreach (var triangle in polygon.ToTriangles())
                        {
                            triMesh.AddTriangle(triangle);
                        }
                    }
                }
            }
        }
    }
}
