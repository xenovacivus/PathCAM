using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using QuantumConcepts.Formats.StereoLithography;
using OpenTK;

namespace Geometry
{
    public class STL_Loader
    {
        public static void Load(string filepath, TriangleMesh triMesh, float scale)
        {
            STLDocument stl = null;

            using (Stream filestream = File.OpenRead(filepath))
            {
                stl = STLDocument.Read(filestream);
            }

            foreach (var facet in stl.Facets)
            {
                List<Vector3> vertices = new List<Vector3>();
                foreach (var vertex in facet.Vertices)
                {
                    Vector3 v = new Vector3((float)vertex.X, (float)vertex.Y, (float)vertex.Z);
                    vertices.Add(v * scale);
                }
                triMesh.AddTriangle(new Triangle(vertices[0], vertices[1], vertices[2]));
            }
            triMesh.Clean();
        }
    }
}
