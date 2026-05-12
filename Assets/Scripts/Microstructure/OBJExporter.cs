using System.IO;
using System.Text;
using UnityEngine;

public static class OBJExporter
{
    public static void ExportMesh(Mesh mesh, string filePath)
    {
        StringBuilder sb = new StringBuilder();

        // Vertices
        foreach (Vector3 v in mesh.vertices)
        {
            sb.AppendLine($"v {v.x} {v.y} {v.z}");
        }

        // Normals
        foreach (Vector3 n in mesh.normals)
        {
            sb.AppendLine($"vn {n.x} {n.y} {n.z}");
        }

        // UVs
        foreach (Vector2 uv in mesh.uv)
        {
            sb.AppendLine($"vt {uv.x} {uv.y}");
        }

        // Faces (OBJ is 1-indexed)
        for (int i = 0; i < mesh.triangles.Length; i += 3)
        {
            int a = mesh.triangles[i] + 1;
            int b = mesh.triangles[i + 1] + 1;
            int c = mesh.triangles[i + 2] + 1;

            sb.AppendLine($"f {a}/{a}/{a} {b}/{b}/{b} {c}/{c}/{c}");
        }

        File.WriteAllText(filePath, sb.ToString());
        Debug.Log("OBJ exported to: " + filePath);
    }
}