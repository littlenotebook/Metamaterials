using System.IO;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.Networking;
using Dummiesman;
using System.Collections;

public class StructureSelector : MonoBehaviour
{
    [Header("Manifest file name")]
    public string manifestFileName = "manifest.txt";

    [Header("UI Dropdown")]
    public TMP_Dropdown meshDropdown;

    [Header("Material Settings")]
    [Tooltip("Name of material inside Resources/MicrostructureMaterial")]
    public string materialName = "MicrostructureMaterial";
    private Material microstructureMaterial;

    private List<string> objPaths = new List<string>();
    private GameObject currentMesh;

    void Start()
    {
        DestroyExistingMeshes();

        meshDropdown.captionText.text = "Choose microstructure...";
        meshDropdown.captionText.alpha = 0.5f;
        meshDropdown.onValueChanged.AddListener(OnMeshSelected);

        // Load unity material
        microstructureMaterial = Resources.Load<Material>(materialName);
        if (microstructureMaterial == null)
            Debug.LogError("Material not found in Resources/: " + materialName);

        LoadManifest();
    }

    void LoadManifest()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "Meshes", manifestFileName);
        if (!File.Exists(path))
        {
            Debug.LogError("Manifest file not found: " + path);
            return;
        }

        string[] lines = File.ReadAllLines(path);
        ProcessManifestLines(lines);
    }

    void DestroyExistingMeshes()
    {
        foreach (GameObject go in FindObjectsOfType<GameObject>())
        {
            if (go.name.Contains("microstructure") ||
                go.name.Contains("GeneratedMesh") ||
                go.name.Contains("shell") ||
                go.name.Contains("Node"))
            {
                Destroy(go);
            }
        }
    }

    void ProcessManifestLines(string[] lines)
    {
        objPaths.Clear();
        meshDropdown.ClearOptions();

        List<string> options = new List<string>();

        foreach (string raw in lines)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            if (raw.StartsWith("#") || raw.StartsWith("//")) continue;

            string fileName = raw.Trim();
            string objPath = Path.Combine(Application.streamingAssetsPath, "Meshes", fileName);

            if (File.Exists(objPath))
            {
                objPaths.Add(objPath);
                options.Add(Path.GetFileNameWithoutExtension(fileName));
            }
            else
            {
                Debug.LogWarning("OBJ not found: " + objPath);
            }
        }

        if (options.Count == 0)
        {
            Debug.LogWarning("No OBJ entries found in manifest.");
            return;
        }

        meshDropdown.AddOptions(options);
        Debug.Log($"Loaded {options.Count} mesh entries.");
    }

    void OnMeshSelected(int index)
    {
        if (index < 0 || index >= objPaths.Count)
            return;

        meshDropdown.captionText.alpha = 1f;
        LoadMesh(objPaths[index]);
    }

    void LoadMesh(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogError("Mesh file missing: " + path);
            return;
        }

        if (currentMesh != null)
        {
            Destroy(currentMesh);
            currentMesh = null;
        }

        try
        {
            // Load OBJ
            currentMesh = new OBJLoader().Load(path);
            currentMesh.name = Path.GetFileNameWithoutExtension(path);

            // Apply Unity material
            if (microstructureMaterial != null)
            {
                Renderer[] renderers = currentMesh.GetComponentsInChildren<Renderer>();
                foreach (Renderer r in renderers)
                {
                    Material[] newMats = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < newMats.Length; i++)
                        newMats[i] = microstructureMaterial;

                    r.materials = newMats;
                }
            }

            // Reset transform
            currentMesh.transform.SetParent(transform, false);
            currentMesh.transform.localPosition = Vector3.zero;
            currentMesh.transform.localRotation = Quaternion.identity;
            currentMesh.transform.localScale = Vector3.one * 1.5f;

            // Create nodes
            CreateNodesFromMesh(currentMesh);

            Debug.Log("Loaded mesh using Unity material: " + path);
        }
        catch (System.Exception e)
        {
            Debug.LogError("OBJ load failed: " + e.Message);
        }
    }
void CreateNodesFromMesh(GameObject meshRoot)
{
    MeshFilter[] meshFilters = meshRoot.GetComponentsInChildren<MeshFilter>();
    
    if (meshFilters.Length == 0)
    {
        Debug.LogError("No MeshFilters found!");
        return;
    }

    List<Vector3> allVertices = new List<Vector3>();
    List<Transform> vertexSources = new List<Transform>();
    
    // Collect ALL vertices from ALL meshes
    foreach (MeshFilter mf in meshFilters)
    {
        Mesh mesh = mf.sharedMesh;
        if (mesh == null) continue;
        
        Vector3[] vertices = mesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            // Convert to world space
            Vector3 worldVertex = mf.transform.TransformPoint(vertices[i]);
            allVertices.Add(worldVertex);
            vertexSources.Add(mf.transform);
        }
        
        Debug.Log($"Collected {vertices.Length} vertices from {mf.name}");
    }
    
    Debug.Log($"Total vertices collected: {allVertices.Count}");
    
    if (allVertices.Count == 0)
    {
        Debug.LogError("No vertices found!");
        return;
    }
    
    // Cluster vertices that are close together (these are sphere nodes)
    float clusterRadius = 0.05f;
    List<Vector3> nodePositions = new List<Vector3>();
    List<List<Vector3>> clusters = new List<List<Vector3>>();
    
    // Simple clustering algorithm
    bool[] vertexAssigned = new bool[allVertices.Count];
    
    for (int i = 0; i < allVertices.Count; i++)
    {
        if (vertexAssigned[i]) continue;
        
        Vector3 seedVertex = allVertices[i];
        List<Vector3> cluster = new List<Vector3>();
        cluster.Add(seedVertex);
        vertexAssigned[i] = true;
        
        // Find all vertices close to this one
        for (int j = i + 1; j < allVertices.Count; j++)
        {
            if (vertexAssigned[j]) continue;
            
            if (Vector3.Distance(seedVertex, allVertices[j]) < clusterRadius)
            {
                cluster.Add(allVertices[j]);
                vertexAssigned[j] = true;
            }
        }
        
        if (cluster.Count > 10) // Only consider clusters with enough vertices
        {
            clusters.Add(cluster);
            
            // Calculate cluster center (average position)
            Vector3 clusterCenter = Vector3.zero;
            foreach (Vector3 v in cluster)
            {
                clusterCenter += v;
            }
            clusterCenter /= cluster.Count;
            
            nodePositions.Add(clusterCenter);
        }
    }
    
    Debug.Log($"Found {clusters.Count} potential sphere clusters");
    
    // Create nodes at cluster centers
    for (int i = 0; i < nodePositions.Count; i++)
    {
        Vector3 nodePos = nodePositions[i];
        List<Vector3> cluster = clusters[i];
        
        GameObject nodeGO = new GameObject($"Node_{i}_{cluster.Count}verts");
        nodeGO.transform.SetParent(meshRoot.transform, false);
        nodeGO.transform.position = nodePos;
        
        Node node = nodeGO.AddComponent<Node>();
        
        SphereCollider col = nodeGO.AddComponent<SphereCollider>();
        col.radius = 0.05f;
        
        Debug.Log($"Created node {i} at {nodePos} with {cluster.Count} vertices");
        
        // Visualize the cluster (optional, for debugging)
        if (Application.isPlaying)
        {
            StartCoroutine(VisualizeCluster(cluster, nodePos, Color.green));
        }
    }
    
    Debug.Log($"Created {nodePositions.Count} sphere nodes from vertex clusters");
}

// Optional debugging visualization
IEnumerator VisualizeCluster(List<Vector3> cluster, Vector3 center, Color color)
{
    foreach (Vector3 vertex in cluster)
    {
        Debug.DrawLine(center, vertex, color, 5f);
        yield return null;
    }
    
    // Draw the cluster center
    GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    sphere.transform.position = center;
    sphere.transform.localScale = Vector3.one * 0.1f;
    sphere.GetComponent<Renderer>().material.color = color;
    Destroy(sphere, 5f);
}

// Helper function to calculate mesh aspect ratio
    float CalculateMeshAspectRatio(Mesh mesh)
    {
        Vector3 boundsSize = mesh.bounds.size;
        
        // Sort dimensions
        float[] dims = { boundsSize.x, boundsSize.y, boundsSize.z };
        System.Array.Sort(dims);
        
        // Avoid division by zero
        if (dims[0] < 0.0001f) return 1000f;
        
        // Aspect ratio = longest dimension / shortest dimension
        return dims[2] / dims[0];
    }

    // void CreateNodesFromMesh(GameObject meshRoot)
    // {
    //     MeshFilter[] meshFilters = meshRoot.GetComponentsInChildren<MeshFilter>();

    //     foreach (MeshFilter mf in meshFilters)
    //     {
    //         Mesh mesh = mf.sharedMesh;
    //         if (mesh == null) continue;

    //         // -----------------------------------------
    //         // HEURISTICS TO DETECT SPHERES (actual nodes)
    //         // -----------------------------------------

    //         // 1) If object name suggests a node
    //         string lowerName = mf.name.ToLower();
    //         bool looksLikeNodeName =
    //             lowerName.Contains("sphere") ||
    //             lowerName.Contains("node") ||
    //             lowerName.Contains("ball") ||
    //             lowerName.Contains("vertex");

    //         // 2) If the mesh vertex count is near a sphere mesh (typical 162, 482, 1026, etc.)
    //         bool looksLikeSphereMesh =
    //             mesh.vertexCount > 100 && // too many to be a strut
    //             mesh.vertexCount < 2000;  // too few to be full geometry

    //         if (!(looksLikeNodeName || looksLikeSphereMesh))
    //         {
    //             // skip non-sphere parts (struts, plates, etc.)
    //             continue;
    //         }

    //         // -----------------------------------------
    //         // Create a Node object at the sphere center
    //         // -----------------------------------------
    //         GameObject nodeGO = new GameObject("Node");
    //         nodeGO.transform.SetParent(meshRoot.transform, false);

    //         Vector3 center = mf.transform.position;
    //         nodeGO.transform.position = center;

    //         Node node = nodeGO.AddComponent<Node>();

    //         SphereCollider col = nodeGO.AddComponent<SphereCollider>();
    //         col.radius = 0.05f;

    //         Debug.Log($"Node created at: {center}, from child object {mf.name}");

    //         // Optional: hide the original sphere mesh
    //         mf.gameObject.SetActive(false);
    //     }

    //     Debug.Log("Finished creating SPHERE nodes only.");
    // }

}


// before adding nodes

// using System.IO;
// using System.Collections.Generic;
// using UnityEngine;
// using TMPro;
// using UnityEngine.Networking;
// using Dummiesman;
// using System.Collections;

// public class StructureSelector : MonoBehaviour
// {
//     [Header("Manifest file name")]
//     public string manifestFileName = "manifest.txt";

//     [Header("UI Dropdown")]
//     public TMP_Dropdown meshDropdown;

//     [Header("Material Settings")]
//     [Tooltip("Name of material inside Resources/MicrostructureMaterial")]
//     public string materialName = "MicrostructureMaterial";
//     private Material microstructureMaterial;

//     private List<string> objPaths = new List<string>();
//     private GameObject currentMesh;

//     void Start()
//     {
//         DestroyExistingMeshes();

//         meshDropdown.captionText.text = "Choose microstructure...";
//         meshDropdown.captionText.alpha = 0.5f;
//         meshDropdown.onValueChanged.AddListener(OnMeshSelected);

//         // Load unity material
//         microstructureMaterial = Resources.Load<Material>(materialName);

//         if (microstructureMaterial == null)
//             Debug.LogError("Material not found in Resources/: " + materialName);

//         LoadManifest();
//     }

//     void LoadManifest()
//     {
//         string path = Path.Combine(Application.streamingAssetsPath, "Meshes", manifestFileName);

//         if (!File.Exists(path))
//         {
//             Debug.LogError("Manifest file not found: " + path);
//             return;
//         }

//         string[] lines = File.ReadAllLines(path);
//         ProcessManifestLines(lines);
//     }

//     void DestroyExistingMeshes()
//     {
//         foreach (GameObject go in FindObjectsOfType<GameObject>())
//         {
//             if (go.name.Contains("microstructure") ||
//                 go.name.Contains("GeneratedMesh") ||
//                 go.name.Contains("shell"))
//             {
//                 Destroy(go);
//             }
//         }
//     }

//     IEnumerator LoadManifestCoroutine()
//     {
//         string path = Path.Combine(Application.streamingAssetsPath, "Meshes", manifestFileName);

//         if (!path.Contains("://"))
//             path = "file://" + path;

//         UnityWebRequest req = UnityWebRequest.Get(path);
//         yield return req.SendWebRequest();

//         if (req.result != UnityWebRequest.Result.Success)
//         {
//             Debug.LogError("Failed to load manifest: " + req.error);
//             yield break;
//         }

//         string[] lines = req.downloadHandler.text.Split('\n');
//         ProcessManifestLines(lines);
//     }

//     void ProcessManifestLines(string[] lines)
//     {
//         objPaths.Clear();
//         meshDropdown.ClearOptions();

//         List<string> options = new List<string>();

//         foreach (string raw in lines)
//         {
//             if (string.IsNullOrWhiteSpace(raw)) continue;
//             if (raw.StartsWith("#") || raw.StartsWith("//")) continue;

//             string fileName = raw.Trim();
//             string objPath = Path.Combine(Application.streamingAssetsPath, "Meshes", fileName);

//             if (File.Exists(objPath))
//             {
//                 objPaths.Add(objPath);
//                 options.Add(Path.GetFileNameWithoutExtension(fileName));
//             }
//             else
//             {
//                 Debug.LogWarning("OBJ not found: " + objPath);
//             }
//         }

//         if (options.Count == 0)
//         {
//             Debug.LogWarning("No OBJ entries found in manifest.");
//             return;
//         }

//         meshDropdown.AddOptions(options);
//         Debug.Log($"Loaded {options.Count} mesh entries.");
//     }

//     void OnMeshSelected(int index)
//     {
//         if (index < 0 || index >= objPaths.Count)
//             return;

//         meshDropdown.captionText.alpha = 1f;
//         LoadMesh(objPaths[index]);
//     }

//     void LoadMesh(string path)
//     {
//         if (!File.Exists(path))
//         {
//             Debug.LogError("Mesh file missing: " + path);
//             return;
//         }

//         if (currentMesh != null)
//         {
//             Destroy(currentMesh);
//             currentMesh = null;
//         }

//         try
//         {
//             // Load OBJ (ignore .mtl)
//             currentMesh = new OBJLoader().Load(path);
//             currentMesh.name = Path.GetFileNameWithoutExtension(path);

//             // Apply our Unity material
//             if (microstructureMaterial != null)
//             {
//                 Renderer[] renderers = currentMesh.GetComponentsInChildren<Renderer>();
//                 foreach (Renderer r in renderers)
//                 {
//                     Material[] newMats = new Material[r.sharedMaterials.Length];
//                     for (int i = 0; i < newMats.Length; i++)
//                         newMats[i] = microstructureMaterial;

//                     r.materials = newMats;
//                 }
//             }

//             // Reset transform
//             currentMesh.transform.SetParent(transform, false);
//             currentMesh.transform.localPosition = Vector3.zero;
//             currentMesh.transform.localRotation = Quaternion.identity;
//             currentMesh.transform.localScale = Vector3.one * 1.5f;

//             Debug.Log("Loaded mesh using Unity material: " + path);
//         }
//         catch (System.Exception e)
//         {
//             Debug.LogError("OBJ load failed: " + e.Message);
//         }
//     }


// }


// // using mtl files

// using System.IO;
// using System.Collections.Generic;
// using UnityEngine;
// using TMPro;
// using UnityEngine.Networking;
// using Dummiesman;
// using System.Collections;

// public class StructureSelector : MonoBehaviour
// {
//     [Header("Manifest file name")]
//     public string manifestFileName = "manifest.txt";

//     [Header("UI Dropdown")]
//     public TMP_Dropdown meshDropdown;

//     private List<string> objPaths = new List<string>();
//     private GameObject currentMesh;

//     void Start()
//     {
//         DestroyExistingMeshes();

//         meshDropdown.captionText.text = "Choose microstructure...";
//         meshDropdown.captionText.alpha = 0.5f;

//         meshDropdown.onValueChanged.AddListener(OnMeshSelected);

//         LoadManifest();
//     }

//     void LoadManifest()
//     {
//         string path = Path.Combine(Application.streamingAssetsPath, "MaterialMeshes", manifestFileName);

//         if (!File.Exists(path))
//         {
//             Debug.LogError("Manifest file not found: " + path);
//             return;
//         }

//         string[] lines = File.ReadAllLines(path);
//         ProcessManifestLines(lines);
//     }


//     void DestroyExistingMeshes()
//     {
//         foreach (GameObject go in FindObjectsOfType<GameObject>())
//         {
//             if (go.name.Contains("microstructure") ||
//                 go.name.Contains("GeneratedMesh") ||
//                 go.name.Contains("shell"))
//             {
//                 Destroy(go);
//             }
//         }
//     }

//     IEnumerator LoadManifestCoroutine()
//     {
//         string path = Path.Combine(Application.streamingAssetsPath, "MaterialMeshes", manifestFileName);

//         // UnityWebRequest requires file://
//         if (!path.Contains("://"))
//             path = "file://" + path;

//         UnityWebRequest req = UnityWebRequest.Get(path);
//         yield return req.SendWebRequest();

//         if (req.result != UnityWebRequest.Result.Success)
//         {
//             Debug.LogError("Failed to load manifest: " + req.error);
//             yield break;
//         }

//         string[] lines = req.downloadHandler.text.Split('\n');
//         ProcessManifestLines(lines);
//     }

//     void ProcessManifestLines(string[] lines)
//     {
//         objPaths.Clear();
//         meshDropdown.ClearOptions();

//         List<string> options = new List<string>();

//         foreach (string raw in lines)
//         {
//             if (string.IsNullOrWhiteSpace(raw)) continue;
//             if (raw.StartsWith("#") || raw.StartsWith("//")) continue;

//             string fileName = raw.Trim();
//             string objPath = Path.Combine(Application.streamingAssetsPath, "MaterialMeshes", fileName);

//             if (File.Exists(objPath))
//             {
//                 objPaths.Add(objPath);
//                 options.Add(Path.GetFileNameWithoutExtension(fileName));
//             }
//             else
//             {
//                 Debug.LogWarning("OBJ not found: " + objPath);
//             }
//         }

//         if (options.Count == 0)
//         {
//             Debug.LogWarning("No OBJ entries found in manifest.");
//             return;
//         }

//         meshDropdown.AddOptions(options);
//         Debug.Log($"Loaded {options.Count} mesh entries.");
//     }

//     void OnMeshSelected(int index)
//     {
//         if (index < 0 || index >= objPaths.Count)
//             return;

//         meshDropdown.captionText.alpha = 1f;
//         LoadMesh(objPaths[index]);
//     }

//     void LoadMesh(string path)
//     {
//         if (!File.Exists(path))
//         {
//             Debug.LogError("Mesh file missing: " + path);
//             return;
//         }

//         if (currentMesh != null)
//         {
//             Destroy(currentMesh);
//             currentMesh = null;
//         }

//         try
//         {
//             // ✨ Load OBJ + its .mtl automatically
//             currentMesh = new OBJLoader().Load(path);
//             string mtlPath = Path.ChangeExtension(path, ".mtl");
//             Debug.Log("Expected MTL path: " + mtlPath + " exists? " + File.Exists(mtlPath));
//             currentMesh.name = Path.GetFileNameWithoutExtension(path);

//             // load shader
//             foreach (var r in currentMesh.GetComponentsInChildren<Renderer>())
//             {
//                 foreach (var mat in r.sharedMaterials)
//                 {
//                     if (mat != null)
//                     {
//                         Shader s = Shader.Find("Universal Render Pipeline/Lit"); 
//                         if (s != null)
//                         {
//                             mat.shader = s;
//                             mat.SetColor("_BaseColor", new Color(0.505882f, 0.921569f, 1f)); // your RGB
//                         }
//                         else
//                         {
//                             Debug.LogError("URP Lit shader not found!");
//                         }
//                     }
//                 }
//             }



//             // Reset transform
//             currentMesh.transform.SetParent(transform, false);
//             currentMesh.transform.localPosition = Vector3.zero;
//             currentMesh.transform.localRotation = Quaternion.identity;
//             currentMesh.transform.localScale = Vector3.one * 1.5f;

//             Debug.Log("Loaded mesh with .mtl materials: " + path);
//         }
//         catch (System.Exception e)
//         {
//             Debug.LogError("OBJ load failed: " + e.Message);
//         }
//     }
// }