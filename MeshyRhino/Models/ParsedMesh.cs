// <author>QROST</author>

using System.Collections.Generic;

namespace MeshyRhino.Models
{
    public class ParsedMesh
    {
        public List<MeshVertex> Vertices { get; set; } = new List<MeshVertex>();
        public List<MeshVertex> TextureCoords { get; set; } = new List<MeshVertex>();
        public List<MeshVertex> Normals { get; set; } = new List<MeshVertex>();
        public List<MeshFace> Faces { get; set; } = new List<MeshFace>();
        public string Name { get; set; }

        public int VertexCount => Vertices.Count;
        public int FaceCount => Faces.Count;
    }

    public struct MeshVertex
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public MeshVertex(double x, double y, double z = 0)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    public struct MeshFace
    {
        public int V0 { get; set; }
        public int V1 { get; set; }
        public int V2 { get; set; }
        public int V3 { get; set; } // -1 if triangle

        public int T0 { get; set; }
        public int T1 { get; set; }
        public int T2 { get; set; }
        public int T3 { get; set; }

        public int N0 { get; set; }
        public int N1 { get; set; }
        public int N2 { get; set; }
        public int N3 { get; set; }

        public bool IsQuad => V3 >= 0;

        public MeshFace(int v0, int v1, int v2, int v3 = -1)
        {
            V0 = v0; V1 = v1; V2 = v2; V3 = v3;
            T0 = T1 = T2 = T3 = -1;
            N0 = N1 = N2 = N3 = -1;
        }
    }
}
