using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class SimpleOBJLoader
{
    public static GameObject LoadOBJFromData(byte[] objData, Material material)
    {
        string objText = System.Text.Encoding.UTF8.GetString(objData);
        return LoadOBJFromText(objText, material);
    }

    public static GameObject LoadOBJFromText(string objText, Material material)
    {
        GameObject meshObject = new GameObject("LoadedMesh");
        MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = meshObject.AddComponent<MeshRenderer>();
        
        // Use the provided material, or create a fallback unlit material
        Material materialToUse = material;
        if (materialToUse == null)
        {
            // Create a simple unlit material as fallback
            Shader unlitShader = Shader.Find("Unlit/Color");
            if (unlitShader != null)
            {
                materialToUse = new Material(unlitShader);
                materialToUse.color = Color.white;
            }
            else
            {
                // Ultimate fallback - use the default material
                materialToUse = new Material(Shader.Find("Sprites/Default"));
                materialToUse.color = Color.gray;
            }
        }
        
        meshRenderer.material = materialToUse;

        // Rest of your OBJ parsing code remains the same...
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uv = new List<Vector2>();
        List<Vector3> normals = new List<Vector3>();
        List<int> triangles = new List<int>();

        string[] lines = objText.Split('\n');

        foreach (string line in lines)
        {
            string trimmedLine = line.Trim();
            
            if (trimmedLine.StartsWith("v "))
            {
                string[] parts = trimmedLine.Split(' ');
                if (parts.Length >= 4)
                {
                    float x = float.Parse(parts[1]);
                    float y = float.Parse(parts[2]);
                    float z = float.Parse(parts[3]);
                    vertices.Add(new Vector3(x, y, z));
                }
            }
            else if (trimmedLine.StartsWith("vt "))
            {
                string[] parts = trimmedLine.Split(' ');
                if (parts.Length >= 3)
                {
                    float u = float.Parse(parts[1]);
                    float v = float.Parse(parts[2]);
                    uv.Add(new Vector2(u, v));
                }
            }
            else if (trimmedLine.StartsWith("vn "))
            {
                string[] parts = trimmedLine.Split(' ');
                if (parts.Length >= 4)
                {
                    float x = float.Parse(parts[1]);
                    float y = float.Parse(parts[2]);
                    float z = float.Parse(parts[3]);
                    normals.Add(new Vector3(x, y, z));
                }
            }
            else if (trimmedLine.StartsWith("f "))
            {
                string[] parts = trimmedLine.Split(' ');
                for (int i = 1; i < parts.Length; i++)
                {
                    string[] indices = parts[i].Split('/');
                    int vertexIndex = int.Parse(indices[0]) - 1;
                    triangles.Add(vertexIndex);
                }
            }
        }

        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        
        if (uv.Count > 0 && uv.Count == vertices.Count)
        {
            mesh.uv = uv.ToArray();
        }
        else
        {
            mesh.uv = new Vector2[vertices.Count];
        }
        
        if (normals.Count > 0 && normals.Count == vertices.Count)
        {
            mesh.normals = normals.ToArray();
        }
        else
        {
            mesh.RecalculateNormals();
        }
        
        mesh.triangles = triangles.ToArray();

        meshFilter.mesh = mesh;
        return meshObject;
    }
}