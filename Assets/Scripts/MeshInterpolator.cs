using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Dummiesman; // Runtime OBJ loader

public class MeshInterpolator : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_Dropdown startDropdown;
    public TMP_Dropdown endDropdown;
    public Slider slider;
    public Button runButton;
    public TMP_Text statusText;

    [Header("Mesh Settings")]
    [Tooltip("Folder inside StreamingAssets")]
    public string meshesRootPath = "Meshes";
    public Material meshMaterial; // Material to apply to all meshes

    private List<string> availableMicrostructures;
    private GameObject[] loadedMeshes; // Store the full GameObjects
    private GameObject currentMesh;
    private bool isReady = false;

    void Start()
    {
        string fullPath = Path.Combine(Application.streamingAssetsPath, meshesRootPath);

        if (!Directory.Exists(fullPath))
        {
            Debug.LogError("StreamingAssets subfolder missing: " + fullPath);
            if (statusText) statusText.text = "ERROR: Missing StreamingAssets folder.";
            return;
        }

        // Find all microstructures
        availableMicrostructures = Directory.GetFiles(fullPath, "*.obj", SearchOption.TopDirectoryOnly)
                                           .Select(Path.GetFileNameWithoutExtension)
                                           .ToList();

        PopulateDropdown(startDropdown, availableMicrostructures, "Choose starting microstructure...");
        PopulateDropdown(endDropdown, availableMicrostructures, "Choose ending microstructure...");

        startDropdown.onValueChanged.AddListener(_ => OnDropdownChanged());
        endDropdown.onValueChanged.AddListener(_ => OnDropdownChanged());

        slider.gameObject.SetActive(false);
        runButton.gameObject.SetActive(false);

        if (statusText) statusText.text = "Select two microstructures to begin.";
    }

    void PopulateDropdown(TMP_Dropdown dropdown, List<string> options, string placeholder)
    {
        dropdown.ClearOptions();
        var dropdownOptions = new List<string> { placeholder };
        dropdownOptions.AddRange(options);
        dropdown.AddOptions(dropdownOptions);
        dropdown.value = 0;
        dropdown.captionText.text = placeholder;
        dropdown.captionText.alpha = 0.5f;
        dropdown.RefreshShownValue();
    }

    void OnDropdownChanged()
    {
        if (startDropdown.value == 0 || endDropdown.value == 0)
        {
            isReady = false;
            runButton.gameObject.SetActive(false);
            slider.gameObject.SetActive(false);
            if (statusText) statusText.text = "Please select both starting and ending microstructures.";
            return;
        }

        if (startDropdown.value == endDropdown.value)
        {
            isReady = false;
            runButton.gameObject.SetActive(false);
            slider.gameObject.SetActive(false);
            if (statusText) statusText.text = "Start and end must be different.";
            return;
        }

        runButton.gameObject.SetActive(true);
        runButton.onClick.RemoveAllListeners();
        runButton.onClick.AddListener(OnRunClicked);

        if (statusText) statusText.text = "Press Run to load meshes.";
    }

    void OnRunClicked()
    {
        string startName = availableMicrostructures[startDropdown.value - 1];
        string endName = availableMicrostructures[endDropdown.value - 1];
        string folderName = $"{startName}-{endName}";
        string interpFolder = Path.Combine(Application.streamingAssetsPath, meshesRootPath, folderName);

        if (!Directory.Exists(interpFolder))
        {
            Debug.LogWarning("Interpolation folder not found: " + interpFolder);
            if (statusText) statusText.text = "Interpolation folder not found.";
            return;
        }

        string[] objFiles = Directory.GetFiles(interpFolder, "*.obj");
        System.Array.Sort(objFiles);

        if (objFiles.Length == 0)
        {
            Debug.LogWarning("No .obj files found in interpolation folder!");
            if (statusText) statusText.text = "No .obj files in folder.";
            return;
        }

        // Destroy previously loaded meshes
        if (loadedMeshes != null)
        {
            foreach (var go in loadedMeshes)
                if (go != null) Destroy(go);
        }

        loadedMeshes = new GameObject[objFiles.Length];

        for (int i = 0; i < objFiles.Length; i++)
        {
            loadedMeshes[i] = LoadOBJAsGameObject(objFiles[i]);
            if (loadedMeshes[i] != null)
                loadedMeshes[i].SetActive(false);
        }

        if (loadedMeshes.Length > 0)
        {
            // Show first mesh
            currentMesh = loadedMeshes[0];
            currentMesh.SetActive(true);
        }

        // Setup slider
        slider.gameObject.SetActive(true);
        slider.minValue = 0;
        slider.maxValue = loadedMeshes.Length - 1;
        slider.wholeNumbers = true;
        slider.onValueChanged.RemoveAllListeners();
        slider.onValueChanged.AddListener(OnSliderChanged);

        isReady = true;
        if (statusText) statusText.text = $"Loaded {loadedMeshes.Length} meshes. Use slider to browse.";
    }

    GameObject LoadOBJAsGameObject(string path)
    {
        try
        {
            GameObject obj = new OBJLoader().Load(path);

            // Apply our meshMaterial to all renderers
            if (meshMaterial != null)
            {
                Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
                foreach (Renderer r in renderers)
                {
                    // For renderers with multiple materials (submeshes), replace all
                    Material[] mats = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++)
                        mats[i] = meshMaterial;
                    r.materials = mats; // Assign new array of materials
                }
            }

            obj.transform.SetParent(transform, false);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;
            obj.transform.localScale = Vector3.one * 4f;

            return obj;
        }
        catch (System.Exception e)
        {
            Debug.LogError("OBJ load failed: " + e.Message);
            return null;
        }
    }


    void OnSliderChanged(float value)
    {
        if (!isReady || loadedMeshes == null || loadedMeshes.Length == 0)
            return;

        int index = Mathf.Clamp(Mathf.RoundToInt(value), 0, loadedMeshes.Length - 1);

        if (currentMesh != null)
            currentMesh.SetActive(false);

        currentMesh = loadedMeshes[index];
        if (currentMesh != null)
            currentMesh.SetActive(true);
    }
}
