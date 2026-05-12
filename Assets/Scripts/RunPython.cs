using System.Collections;
using System.Diagnostics;
using UnityEngine;
using System.IO;
using Dummiesman;

public class RunPython : MonoBehaviour
{
    // Path to your Python executable
    public string pythonPath = "/opt/anaconda3/envs/metamaterial/bin/python";

    // Path to your Python script (the general_mesh.py)
    public string scriptPath = "/Users/hannah/Downloads/universal-metamaterial-representation/general_mesh.py";

    // Python module name (e.g., "hexagon_shell")
    public string pythonModuleName = "tetrahedron_wireframe";

    // Path to save generated mesh
    public string outputMeshPath = "Assets/Meshes/microstructure.obj";
    
    // Reference to a material in your Assets folder
    public Material defaultMaterial;

    void Start()
    {
        StartCoroutine(RunPythonAndLoadMesh());
    }

    IEnumerator RunPythonAndLoadMesh()
    {
        // First, run the Python script and wait for it to complete
        yield return StartCoroutine(RunPythonScript());

        // Then load the mesh
        LoadMeshIntoUnity(outputMeshPath);
    }
    
    public void RunSelectedPythonMesh()
    {
        StartCoroutine(RunPythonAndLoadMesh());
    }

    IEnumerator RunPythonScript()
    {
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = pythonPath,
            // Pass module name AND output path to the Python script
            Arguments = $"\"{scriptPath}\" \"{pythonModuleName}\" \"{outputMeshPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (Process proc = new Process())
        {
            proc.StartInfo = psi;
            proc.Start();
            
            // Read output asynchronously
            string output = proc.StandardOutput.ReadToEnd();
            string errors = proc.StandardError.ReadToEnd();
            
            // Wait for the process to exit
            while (!proc.HasExited)
            {
                yield return null;
            }

            UnityEngine.Debug.Log("Python output: " + output);
            if (!string.IsNullOrEmpty(errors))
                UnityEngine.Debug.LogError("Python errors: " + errors);
        }
    }

    void LoadMeshIntoUnity(string path)
    {
        if (!File.Exists(path))
        {
            UnityEngine.Debug.LogError("Mesh file not found: " + path);
            return;
        }

        try
        {
            UnityEngine.Debug.Log("Attempting to load OBJ from: " + path);

            // Check file size and content
            FileInfo fileInfo = new FileInfo(path);
            UnityEngine.Debug.Log("File size: " + fileInfo.Length + " bytes");

            // Load the OBJ file
            GameObject loadedObj = new OBJLoader().Load(path);

            if (loadedObj == null)
            {
                UnityEngine.Debug.LogError("OBJLoader returned null GameObject");
                return;
            }

            loadedObj.name = "GeneratedMesh";
            loadedObj.transform.position = Vector3.zero;

            // Check what components were created
            MeshFilter[] meshFilters = loadedObj.GetComponentsInChildren<MeshFilter>(true);
            UnityEngine.Debug.Log("Found " + meshFilters.Length + " MeshFilter components");

            foreach (MeshFilter mf in meshFilters)
            {
                if (mf.sharedMesh == null)
                {
                    UnityEngine.Debug.LogError("MeshFilter has no mesh: " + mf.gameObject.name);
                }
                else
                {
                    UnityEngine.Debug.Log("MeshFilter has mesh: " + mf.sharedMesh.name + " with " + mf.sharedMesh.vertexCount + " vertices");
                }
            }

            // Call both methods in the correct order
            EnsureRenderers(loadedObj);
            ReplaceAllMaterialsWithAssetMaterial(loadedObj);

        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError("Error loading mesh: " + e.Message + "\n" + e.StackTrace);
        }
        
        UnityEngine.Debug.Log("Loaded and saved mesh: " + path);
    }
    
    void ReplaceAllMaterialsWithAssetMaterial(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        
        foreach (Renderer renderer in renderers)
        {
            if (defaultMaterial != null)
            {
                renderer.material = defaultMaterial;
            }
            else
            {
                // Fallback: create the most basic possible material
                renderer.material = CreateFallbackMaterial();
            }
        }
    }

    Material CreateFallbackMaterial()
    {
        // Use the most reliable built-in shader
        Shader shader = Shader.Find("Legacy Shaders/Vertex Lit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        if (shader == null)
        {
            UnityEngine.Debug.LogError("No fallback shader found!");
            return null;
        }

        Material mat = new Material(shader);
        mat.color = Color.gray;
        mat.name = "RuntimeFallbackMaterial";
        return mat;
    }  

    void EnsureRenderers(GameObject obj)
    {
        MeshFilter[] meshFilters = obj.GetComponentsInChildren<MeshFilter>();
        foreach (MeshFilter mf in meshFilters)
        {
            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
            if (mr == null)
            {
                mr = mf.gameObject.AddComponent<MeshRenderer>();
                UnityEngine.Debug.Log("Added MeshRenderer to: " + mf.gameObject.name);
            }
        }
    }

}