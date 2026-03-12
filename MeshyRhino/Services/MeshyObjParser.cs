// <author>QROST</author>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MeshyRhino.Models;

namespace MeshyRhino.Services
{
    /// <summary>
    /// Parses OBJ content into <see cref="ParsedMesh"/>. Vertex coordinates
    /// are kept in the original unit (meters, as produced by Meshy). Unit
    /// conversion to the active document's unit system is handled by
    /// <see cref="MeshyImportService"/>.
    /// </summary>
    public static class MeshyObjParser
    {
        public static ParsedMesh Parse(string objContent, string name = "Meshy_Model")
        {
            var mesh = new ParsedMesh { Name = name };
            var lines = objContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();

                if (line.StartsWith("v "))
                    ParseVertex(line, mesh.Vertices);
                else if (line.StartsWith("vt "))
                    ParseTextureCoord(line, mesh.TextureCoords);
                else if (line.StartsWith("vn "))
                    ParseNormal(line, mesh.Normals);
                else if (line.StartsWith("f "))
                    ParseFace(line, mesh);
            }

            return mesh;
        }

        public static ParsedMesh ParseFile(string filePath, string name = null)
        {
            string content = File.ReadAllText(filePath);
            return Parse(content, name ?? Path.GetFileNameWithoutExtension(filePath));
        }

        private static void ParseVertex(string line, List<MeshVertex> vertices)
        {
            string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) return;

            if (double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) &&
                double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double y) &&
                double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double z))
            {
                vertices.Add(new MeshVertex(x, y, z));
            }
        }

        private static void ParseTextureCoord(string line, List<MeshVertex> uvs)
        {
            string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) return;

            if (double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double u) &&
                double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
            {
                uvs.Add(new MeshVertex(u, v, 0));
            }
        }

        private static void ParseNormal(string line, List<MeshVertex> normals)
        {
            string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) return;

            if (double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) &&
                double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double y) &&
                double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double z))
            {
                normals.Add(new MeshVertex(x, y, z));
            }
        }

        private static void ParseFace(string line, ParsedMesh mesh)
        {
            string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) return;

            var vIndices = new List<int>();
            var tIndices = new List<int>();
            var nIndices = new List<int>();

            for (int i = 1; i < parts.Length; i++)
            {
                string[] subParts = parts[i].Split('/');
                
                // Vertex index
                if (int.TryParse(subParts[0], out int vIdx))
                {
                    vIndices.Add(vIdx > 0 ? vIdx - 1 : mesh.Vertices.Count + vIdx);
                }

                // Texture index
                if (subParts.Length > 1 && int.TryParse(subParts[1], out int tIdx))
                {
                    tIndices.Add(tIdx > 0 ? tIdx - 1 : mesh.TextureCoords.Count + tIdx);
                }
                else
                {
                    tIndices.Add(-1);
                }

                // Normal index
                if (subParts.Length > 2 && int.TryParse(subParts[2], out int nIdx))
                {
                    nIndices.Add(nIdx > 0 ? nIdx - 1 : mesh.Normals.Count + nIdx);
                }
                else
                {
                    nIndices.Add(-1);
                }
            }

            // Handle Triangles and Quads
            if (vIndices.Count == 3)
            {
                var face = new MeshFace(vIndices[0], vIndices[1], vIndices[2])
                {
                    T0 = tIndices[0], T1 = tIndices[1], T2 = tIndices[2],
                    N0 = nIndices[0], N1 = nIndices[1], N2 = nIndices[2]
                };
                mesh.Faces.Add(face);
            }
            else if (vIndices.Count == 4)
            {
                var face = new MeshFace(vIndices[0], vIndices[1], vIndices[2], vIndices[3])
                {
                    T0 = tIndices[0], T1 = tIndices[1], T2 = tIndices[2], T3 = tIndices[3],
                    N0 = nIndices[0], N1 = nIndices[1], N2 = nIndices[2], N3 = nIndices[3]
                };
                mesh.Faces.Add(face);
            }
            else if (vIndices.Count > 4)
            {
                // Naive triangulation for N-gons > 4
                for (int i = 1; i < vIndices.Count - 1; i++)
                {
                    var face = new MeshFace(vIndices[0], vIndices[i], vIndices[i + 1])
                    {
                        T0 = tIndices[0], T1 = tIndices[i], T2 = tIndices[i + 1],
                        N0 = nIndices[0], N1 = nIndices[i], N2 = nIndices[i + 1]
                    };
                    mesh.Faces.Add(face);
                }
            }
        }
    }
}
