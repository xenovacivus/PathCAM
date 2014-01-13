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
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using OpenTK;

namespace Geometry
{
    #region DAE File XML Description

    [Serializable()]
    public class Source
    {
        [XmlElement("float_array")]
        public string float_array { get; set; }

        [XmlAttribute("id")]
        public string id { get; set; }
    }

    [Serializable()]
    public class Input
    {
        [XmlAttribute("semantic")]
        public string semantic { get; set; }

        [XmlAttribute("source")]
        public string source { get; set; }

        [XmlAttribute("offset")]
        public string offset { get; set; }
    }

    [Serializable()]
    public class Vertices
    {
        [XmlElement("input")]
        public Input[] input { get; set; }
    }

    [Serializable()]
    public class Triangles
    {
        [XmlElement("p")]
        public string p { get; set; }

        [XmlElement("input")]
        public Input[] input { get; set; }
    }

    [Serializable()]
    public class Mesh
    {
        [XmlElement("source")]
        public Source[] sources { get; set; }

        [XmlElement("vertices")]
        public Vertices vertices { get; set; }

        [XmlElement("triangles")]
        public Triangles triangles { get; set; }

        public Input FindInputBySemantic(Input[] inputs, string semantic)
        {
            foreach (Input i in inputs)
            {
                if (i.semantic == semantic)
                {
                    return i;
                }
            }
            return null;
        }

        public Source FindSourceById(Source[] sources, string id)
        {
            foreach (Source s in sources)
            {
                if (("#" + s.id) == id)
                {
                    return s;
                }
            }
            return null;
        }

        public float[] PositionFloats()
        {
            if (vertices == null)
            {
                return new float[] { };
            }
            Input i = FindInputBySemantic(vertices.input, "POSITION");
            Source s = FindSourceById(sources, i.source);
            IEnumerable<float> floats = s.float_array.Split(new char[] { ' ' }).Select(float_str => float.Parse(float_str));
            return floats.ToArray();
        }

        public int[] TrianglePositionIndices()
        {
            if (triangles == null)
            {
                return new int[] { };
            }
            int num_inputs = triangles.input.Count();
            int index = int.Parse(FindInputBySemantic(triangles.input, "VERTEX").offset);

            int[] indices = triangles.p.Split(new char[] { ' ' }).Select(int_str => int.Parse(int_str)).ToArray();
            List<int> position_indices = new List<int>();
            for (int i = index; i < indices.Count(); i += num_inputs)
            {
                position_indices.Add(indices[i]);
            }
            return position_indices.ToArray();
        }

    }

    [Serializable()]
    public class Geometry
    {
        [XmlElement("mesh")]
        public Mesh mesh { get; set; }
    }

    [XmlRootAttribute("COLLADA", Namespace = "http://www.collada.org/2005/11/COLLADASchema")]
    [Serializable()]
    public class Collada
    {
        [XmlArray("library_geometries")]
        [XmlArrayItem("geometry")]
        public Geometry[] library_geometries { get; set; }
    }

    #endregion

    public class DAE_Loader
    {
        public static void Load(string filepath, TriangleMesh triMesh, float scale)
        {
            using (System.IO.TextReader reader = File.OpenText(filepath))
            {
                XmlSerializer ser = new XmlSerializer(typeof(Collada));
                Collada c = (Collada)ser.Deserialize(reader);
                foreach (Geometry g in c.library_geometries)
                {
                    float[] floats = g.mesh.PositionFloats();
                    int[] indices = g.mesh.TrianglePositionIndices();

                    List<Vector3> vertices = new List<Vector3>();

                    for (int i = 0; i < floats.Count(); i += 3)
                    {
                        Vector3 v = new Vector3(floats[i], floats[i + 1], floats[i + 2]);
                        vertices.Add(v * scale);
                    }

                    for (int i = 0; i < indices.Count(); i += 3)
                    {
                        triMesh.AddTriangle(vertices[indices[i]], vertices[indices[i + 1]], vertices[indices[i + 2]]);
                    }
                }
            }
            triMesh.Clean();
        }
    }
}
