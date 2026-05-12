using System.Collections.Generic;
using UnityEngine;

namespace Microstructure
{
    /// <summary>
    /// Controls per-octant visual state:
    ///   - Active octant: fully opaque, raycasts enabled
    ///   - Other octants: transparent, raycasts disabled (layer swap)
    /// 
    /// Assign the 8 octant root GameObjects in the Inspector in index order
    /// matching OctantMirrorSystem's octant definitions (0=canonical +x+y+z).
    /// Each octant root should be the parent of all nodes/edges in that octant.
    /// </summary>
    public class OctantVisualController : MonoBehaviour
    {
        public static OctantVisualController Instance { get; private set; }

        [Header("Octant Roots (index 0-7, must match OctantMirrorSystem order)")]
        [Tooltip("Assign the parent GameObject for each octant's nodes and edges.")]
        public List<GameObject> octantRoots = new List<GameObject>();

        [Header("Visual Settings")]
        [SerializeField] private float inactiveAlpha = 0.15f;
        [SerializeField] private string activeLayer   = "Default";
        [SerializeField] private string inactiveLayer = "Ignore Raycast";
        [Tooltip("Optional — assign a pre-made transparent material to use instead of runtime modification.")]
        [SerializeField] private Material inactiveMaterialOverride;

        // Cache original materials so we can restore them
        private Dictionary<Renderer, Material[]> _originalMaterials
            = new Dictionary<Renderer, Material[]>();

        private int _currentActiveOctant = -1;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Debug.LogWarning("Multiple OctantVisualController instances!");
        }

        private void Start()
        {
            // Cache all renderer materials upfront
            foreach (var root in octantRoots)
            {
                if (root == null) continue;
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (!_originalMaterials.ContainsKey(r))
                        _originalMaterials[r] = r.materials;
                }
            }

            // Apply initial state
            if (OctantMirrorSystem.Instance != null)
                RefreshVisuals(OctantMirrorSystem.Instance.activeOctantIndex);
        }

/// <summary>
/// Scans ALL renderers in the scene, determines their octant by world position,
/// and applies active/inactive visuals accordingly. Call this after Load() completes.
/// </summary>
        public void RefreshVisuals(int activeOctantIndex)
        {
            _currentActiveOctant = activeOctantIndex;
            Debug.Log($"[OctantVisualController] RefreshVisuals called — activeOctant: {activeOctantIndex}");
            Debug.Log($"[OctantVisualController] inactiveMaterialOverride: {(inactiveMaterialOverride != null ? inactiveMaterialOverride.name : "NULL")}");
            Debug.Log($"[OctantVisualController] octantRoots count: {octantRoots.Count}");

            // Handle octant roots
            for (int i = 0; i < octantRoots.Count; i++)
            {
                if (octantRoots[i] == null)
                {
                    Debug.LogWarning($"[OctantVisualController] octantRoots[{i}] is NULL");
                    continue;
                }
                bool isActive = i == activeOctantIndex;
                Debug.Log($"[OctantVisualController] Setting octantRoot[{i}] ({octantRoots[i].name}) — isActive: {isActive}");
                SetOctantVisuals(octantRoots[i], isActive);
            }

            if (OctantMirrorSystem.Instance == null)
            {
                Debug.LogWarning("[OctantVisualController] OctantMirrorSystem.Instance is NULL — skipping scene scan");
                return;
            }

            // Scan all nodes
            var allNodes = GameObject.FindObjectsOfType<MicrostructureNode>();
            Debug.Log($"[OctantVisualController] Found {allNodes.Length} MicrostructureNodes in scene");
            foreach (var node in allNodes)
            {
                if (node.IsMirrored) continue;
                int octant = OctantMirrorSystem.Instance.GetOctantForPosition(node.transform.position);
                bool isActive = octant == activeOctantIndex;
                Debug.Log($"[OctantVisualController] Node '{node.gameObject.name}' at {node.transform.position} " +
                        $"→ octant {octant}, isActive: {isActive}");
                SetOctantVisuals(node.gameObject, isActive);
                SetColliderLayer(node.gameObject, isActive);
            }

            // Scan all edges
            var allEdges = GameObject.FindObjectsOfType<MicrostructureEdge>();
            Debug.Log($"[OctantVisualController] Found {allEdges.Length} MicrostructureEdges in scene");
            foreach (var edge in allEdges)
            {
                if (edge.IsMirrored) continue;
                Vector3 midpoint = edge.BezierPts != null && edge.BezierPts.Length > 0
                    ? edge.BezierPts[edge.BezierPts.Length / 2]
                    : edge.transform.position;
                int octant = OctantMirrorSystem.Instance.GetOctantForPosition(midpoint);
                bool isActive = octant == activeOctantIndex;
                Debug.Log($"[OctantVisualController] Edge '{edge.gameObject.name}' midpoint {midpoint} " +
                        $"→ octant {octant}, isActive: {isActive}");
                SetOctantVisuals(edge.gameObject, isActive);
                SetColliderLayer(edge.gameObject, isActive);
            }

            // Scan all faces
            var allFaces = GameObject.FindObjectsOfType<MicrostructureFace>();
            Debug.Log($"[OctantVisualController] Found {allFaces.Length} MicrostructureFaces in scene");
            foreach (var face in allFaces)
            {
                if (face.Corners == null || face.Corners.Length < 3) continue;
                Vector3 centroid = (face.Corners[0] + face.Corners[1] + face.Corners[2]) / 3f;
                int octant = OctantMirrorSystem.Instance.GetOctantForPosition(centroid);
                bool isActive = octant == activeOctantIndex;
                Debug.Log($"[OctantVisualController] Face '{face.gameObject.name}' centroid {centroid} " +
                        $"→ octant {octant}, isActive: {isActive}");
                SetOctantVisuals(face.gameObject, isActive);
                SetColliderLayer(face.gameObject, isActive);
            }

            Debug.Log($"[OctantVisualController] RefreshVisuals complete");
        }

        // private void SetColliderLayer(GameObject go, bool isActive)
        // {
        //     int layer = LayerMask.NameToLayer(isActive ? activeLayer : inactiveLayer);
        //     foreach (var col in go.GetComponentsInChildren<Collider>(true))
        //         col.gameObject.layer = layer;
        // }

        private bool IsArrowObject(Transform t)
        {
            while (t != null)
            {
                if (t.name.StartsWith("DragArrow_") ||
                    t.name == "XAxis" || t.name == "YAxis" || t.name == "ZAxis" ||
                    t.name == "Shaft" || t.name == "Head")
                    return true;
                t = t.parent;
            }
            return false;
        }
        /// <summary>
        /// Call after a new mirrored node/edge is added to a non-active octant
        /// so its visuals are immediately set correctly.
        /// </summary>
        public void ApplyVisualsToObject(GameObject obj, int octantIndex)
        {
            bool isActive = octantIndex == _currentActiveOctant;
            SetOctantVisuals(obj, isActive);
        }

        private void SetOctantVisuals(GameObject root, bool isActive)
        {
            int targetLayer = LayerMask.NameToLayer(isActive ? activeLayer : inactiveLayer);

            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (IsArrowObject(r.transform)) continue;

                if (!_originalMaterials.ContainsKey(r))
                    _originalMaterials[r] = r.materials;

                if (isActive)
                    r.materials = _originalMaterials[r];
                else if (inactiveMaterialOverride != null)
                {
                    var mats = new Material[r.materials.Length];
                    for (int m = 0; m < mats.Length; m++)
                        mats[m] = inactiveMaterialOverride;
                    r.materials = mats;
                }
                else
                {
                    var transparentMats = new Material[r.materials.Length];
                    for (int m = 0; m < r.materials.Length; m++)
                    {
                        var mat = new Material(_originalMaterials[r][m]);
                        SetMaterialTransparent(mat, inactiveAlpha);
                        transparentMats[m] = mat;
                    }
                    r.materials = transparentMats;
                }

                r.gameObject.layer = targetLayer;
            }
        }

        private void SetColliderLayer(GameObject go, bool isActive)
        {
            int layer = LayerMask.NameToLayer(isActive ? activeLayer : inactiveLayer);
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
            {
                if (!IsArrowObject(col.transform))
                    col.gameObject.layer = layer;
            }
        }

        // private void SetOctantVisuals(GameObject root, bool isActive)
        // {
        //     int targetLayer = LayerMask.NameToLayer(isActive ? activeLayer : inactiveLayer);
        //     var renderers = root.GetComponentsInChildren<Renderer>(true);

        //     Debug.Log($"[SetOctantVisuals] '{root.name}' — isActive: {isActive}, " +
        //             $"renderers found: {renderers.Length}, targetLayer: {targetLayer} ({(isActive ? activeLayer : inactiveLayer)})");

        //     if (targetLayer == -1)
        //         Debug.LogError($"[SetOctantVisuals] Layer '{(isActive ? activeLayer : inactiveLayer)}' not found! " +
        //                     $"Check Project Settings → Tags and Layers.");

        //     foreach (var r in renderers)
        //     {
                
        //         if (!_originalMaterials.ContainsKey(r))
        //             _originalMaterials[r] = r.materials;

        //         if (isActive)
        //         {
        //             r.materials = _originalMaterials[r];
        //             Debug.Log($"  → Restored '{r.gameObject.name}' to original material: " +
        //                     $"{(r.materials.Length > 0 ? r.materials[0].name : "none")}");
        //         }
        //         else if (inactiveMaterialOverride != null)
        //         {
        //             var mats = new Material[r.materials.Length];
        //             for (int m = 0; m < mats.Length; m++)
        //                 mats[m] = inactiveMaterialOverride;
        //             r.materials = mats;

        //             // Check the actual alpha of the override material
        //             float alpha = -1f;
        //             if (inactiveMaterialOverride.HasProperty("_BaseColor"))
        //                 alpha = inactiveMaterialOverride.GetColor("_BaseColor").a;
        //             else if (inactiveMaterialOverride.HasProperty("_Color"))
        //                 alpha = inactiveMaterialOverride.color.a;

        //             Debug.Log($"  → Applied inactiveMaterialOverride to '{r.gameObject.name}' — " +
        //                     $"material: {inactiveMaterialOverride.name}, alpha: {alpha:F2}, " +
        //                     $"renderQueue: {inactiveMaterialOverride.renderQueue}, " +
        //                     $"shader: {inactiveMaterialOverride.shader.name}");
        //         }
        //         else
        //         {
        //             var transparentMats = new Material[r.materials.Length];
        //             for (int m = 0; m < r.materials.Length; m++)
        //             {
        //                 var mat = new Material(_originalMaterials[r][m]);
        //                 SetMaterialTransparent(mat, inactiveAlpha);
        //                 transparentMats[m] = mat;

        //                 float alpha = -1f;
        //                 if (mat.HasProperty("_BaseColor")) alpha = mat.GetColor("_BaseColor").a;
        //                 else if (mat.HasProperty("_Color")) alpha = mat.color.a;

        //                 Debug.Log($"  → Runtime transparent mat on '{r.gameObject.name}' — " +
        //                         $"original: {_originalMaterials[r][m].name}, " +
        //                         $"shader: {mat.shader.name}, alpha: {alpha:F2}, " +
        //                         $"renderQueue: {mat.renderQueue}");
        //             }
        //             r.materials = transparentMats;
        //         }

        //         r.gameObject.layer = targetLayer;
        //         Debug.Log($"  → '{r.gameObject.name}' layer set to {r.gameObject.layer}");
        //     }
        // }

        private static void SetMaterialTransparent(Material mat, float alpha)
        {
            // URP surface type = 1 means Transparent
            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1); // 0 = Opaque, 1 = Transparent
                mat.SetFloat("_Blend", 0);   // 0 = Alpha
                mat.SetFloat("_ZWrite", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = 3000;
            }
            else
            {
                // Built-in Standard shader fallback
                mat.SetFloat("_Mode", 3);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
            }

            // Apply alpha to whichever color property exists
            if (mat.HasProperty("_BaseColor"))
            {
                Color c = mat.GetColor("_BaseColor");
                c.a = alpha;
                mat.SetColor("_BaseColor", c);
            }
            else if (mat.HasProperty("_Color"))
            {
                Color c = mat.color;
                c.a = alpha;
                mat.color = c;
            }
        }

        /// <summary>
        /// Re-cache a renderer after its material has been changed externally
        /// (e.g. highlight toggled). Call before RefreshVisuals.
        /// </summary>
        public void RecacheMaterial(Renderer r)
        {
            if (r != null)
                _originalMaterials[r] = r.materials;
        }
    }
}