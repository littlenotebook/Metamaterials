using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Utility that mirrors the logic of <c>example_materials/tetrahedron_wireframe.py</c>
/// for creating a simple text representation of the metamaterial.  The Python
/// script builds arrays of node positions, edge adjacencies and edge parameters
/// and then wraps them in a <c>Metamaterial</c> class; this C# helper performs
/// the same computations and emits two small ASCII files that Unity can load
/// via the existing <see cref="MetamaterialManager" />.
///
/// The output is intentionally minimal – only the node coordinates and the
/// list of edges are written – because the Unity side only requires those two
/// pieces of information for the wireframe preview.  If more of the
/// representation is needed it can be appended easily with additional methods.
/// </summary>
public static class TetrahedronWireframeRepTextGenerator
{
    // four corner points used in the Python example
    private static readonly Vector3[] baseNodePositions = new Vector3[]
    {
        new Vector3(0f, 1f, 1f),
        new Vector3(1f, 0f, 1f),
        new Vector3(0f, 0f, 0f),
        new Vector3(1f, 1f, 0f),
    };

    /// <summary>
    /// Writes out two text files to the specified directory:
    /// * tetrahedron_nodes.txt  – one "x y z" triplet per line
    /// * tetrahedron_edges.txt  – one "n1 n2" pair per line
    ///
    /// The files follow the same format used by the shipped data assets under
    /// Assets/Data/NodePositions and Assets/Data/EdgeAdjacency.
    /// </summary>
    /// <param name="outputDirectory">Directory in which to create the files.</param>
    public static void GenerateRepresentationFiles(string outputDirectory)
    {
        if (string.IsNullOrEmpty(outputDirectory))
            throw new ArgumentException("output directory must be non‑empty", nameof(outputDirectory));

        Directory.CreateDirectory(outputDirectory);

        string nodesPath = Path.Combine(outputDirectory, "tetrahedron_nodes.txt");
        string edgesPath = Path.Combine(outputDirectory, "tetrahedron_edges.txt");

        using (var writer = new StreamWriter(nodesPath))
        {
            foreach (var p in baseNodePositions)
                writer.WriteLine($"{p.x} {p.y} {p.z}");
        }

        using (var writer = new StreamWriter(edgesPath))
        {
            for (int n1 = 0; n1 < baseNodePositions.Length; n1++)
            for (int n2 = n1 + 1; n2 < baseNodePositions.Length; n2++)
                writer.WriteLine($"{n1} {n2}");
        }

        Debug.Log($"Generated tetrahedron wireframe representation files in '{outputDirectory}'.");
    }

    #if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/Generate/Tetrahedron Wireframe Representation")]
    private static void GenerateMenuItem()
    {
        string folder = UnityEditor.EditorUtility.SaveFolderPanel(
            "Select output folder for tetrahedron representation text files",
            Application.dataPath,
            "");

        if (!string.IsNullOrEmpty(folder))
            GenerateRepresentationFiles(folder);
    }
    #endif
}
