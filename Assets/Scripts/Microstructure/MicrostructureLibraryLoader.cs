// MicrostructureLibraryLoader.cs
// Attach to a GameObject in the scene.
// Point it at a MicrostructureLibrary ScriptableObject and select which entry
// to load (by index or by name).  Uses MicrostructureLoader internally so all
// per-structure settings are respected.

using UnityEngine;

namespace Microstructure
{
    public class MicrostructureLibraryLoader : MonoBehaviour
    {
        [Header("Library")]
        public MicrostructureLibrary library;

        [Header("Which entry to load")]
        public int    entryIndex = 0;
        public string entryName  = "";   // overrides entryIndex when non-empty

        [Header("Shared Prefabs  (used when entry has no overrides)")]
        public GameObject defaultNodePrefab;
        public GameObject defaultEdgePrefab;
        public GameObject defaultFacePrefab;

        [Header("Options")]
        public bool  loadOnStart     = true;
        public float worldScale      = 1f;

        [Tooltip("Spawn only the single canonical octant (no reflections). " +
                 "Useful for verifying the exported octant looks correct before " +
                 "enabling full mirroring.")]
        public bool singleOctantOnly = false;

        // Live loader instance
        MicrostructureLoader _loader;

        // ─────────────────────────────────────────────────────────────────────

        void Start()
        {
            if (loadOnStart) LoadSelected();
        }

        [ContextMenu("Load Selected Entry")]
        public void LoadSelected()
        {
            if (library == null || library.Count == 0)
            {
                Debug.LogError("[LibraryLoader] No library assigned or library is empty.");
                return;
            }

            MicrostructureLibrary.Entry entry;
            if (!string.IsNullOrEmpty(entryName))
            {
                entry = library.Find(entryName);
                if (entry == null)
                {
                    Debug.LogError($"[LibraryLoader] Entry '{entryName}' not found in library.");
                    return;
                }
            }
            else
            {
                if (entryIndex < 0 || entryIndex >= library.Count)
                {
                    Debug.LogError($"[LibraryLoader] entryIndex {entryIndex} out of range.");
                    return;
                }
                entry = library.Get(entryIndex);
            }

            LoadEntry(entry);
        }

        void LoadEntry(MicrostructureLibrary.Entry entry)
        {
            // Destroy previous loader if any
            if (_loader != null) Destroy(_loader.gameObject);

            var go = new GameObject($"Microstructure_{entry.displayName}");
            go.transform.SetParent(transform, false);

            _loader                    = go.AddComponent<MicrostructureLoader>();
            _loader.microstructureJson = entry.jsonAsset;
            _loader.nodePrefab         = entry.nodePrefabOverride ?? defaultNodePrefab;
            _loader.edgePrefab         = entry.edgePrefabOverride ?? defaultEdgePrefab;
            _loader.facePrefab         = entry.facePrefabOverride ?? defaultFacePrefab;
            _loader.worldScale         = worldScale;
            _loader.singleOctantOnly   = singleOctantOnly;   // pass through toggle
            _loader.loadOnStart        = false;               // we call Load() manually below
            _loader.Load();
        }

        // ── Convenience accessors ────────────────────────────────────────────

        public MicrostructureLoader CurrentLoader => _loader;
    }
}