// MicrostructureLibrary.cs
// A ScriptableObject that acts as the central catalogue for every microstructure JSON.
// Create one via Assets ▶ Create ▶ Microstructure ▶ Library, then drag JSON files in.
//
// Place under Assets/Scripts/Microstructure/

using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Microstructure
{
    [CreateAssetMenu(
        fileName = "MicrostructureLibrary",
        menuName  = "Microstructure/Library",
        order     = 1
    )]
    public class MicrostructureLibrary : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            [Tooltip("Human-readable name (can differ from the JSON 'name' field).")]
            public string    displayName;

            [Tooltip("The exported .json file (must live under Assets/).")]
            public TextAsset jsonAsset;

            [Tooltip("Default prefab overrides for this specific microstructure (optional).")]
            public GameObject nodePrefabOverride;
            public GameObject edgePrefabOverride;
            public GameObject facePrefabOverride;

            // Cached parsed header (read-only at runtime)
            [HideInInspector] public string parsedName;
            [HideInInspector] public int    numActiveNodes;  // Changed from numNodes
            [HideInInspector] public int    numEdges;
            [HideInInspector] public int    numFaces;
            [HideInInspector] public int    totalNodes;      // Added to store total nodes if needed
        }

        [Header("Catalogue")]
        public List<Entry> entries = new();

        // ── Runtime lookup ───────────────────────────────────────────────────

        /// <summary>Return the Entry whose displayName matches (case-insensitive).</summary>
        public Entry Find(string name)
        {
            return entries.Find(e =>
                string.Equals(e.displayName, name, System.StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Return the Entry at a given index.</summary>
        public Entry Get(int index) => entries[index];

        public int Count => entries.Count;

#if UNITY_EDITOR
        // ── Editor helper: refresh parsed stats ─────────────────────────────
        [ContextMenu("Refresh Stats From JSON")]
        public void RefreshStats()
        {
            foreach (var e in entries)
            {
                if (e.jsonAsset == null) continue;
                try
                {
                    var data = JsonUtility.FromJson<MicrostructureFile>(e.jsonAsset.text);
                    if (data == null) continue;
                    
                    e.parsedName = data.name;
                    
                    // FIXED: Use num_active_nodes instead of num_nodes
                    e.numActiveNodes = data.stats?.num_active_nodes ?? 0;
                    e.numEdges       = data.stats?.num_edges ?? 0;
                    e.numFaces       = data.stats?.num_faces ?? 0;
                    e.totalNodes     = data.num_nodes;  // This is the total nodes field from the root
                    
                    if (string.IsNullOrEmpty(e.displayName))
                        e.displayName = data.name;
                }
                catch (System.Exception ex) 
                { 
                    Debug.LogWarning($"Failed to parse {e.jsonAsset.name}: {ex.Message}");
                }
            }
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            Debug.Log("[MicrostructureLibrary] Stats refreshed.");
        }
#endif
    }
}