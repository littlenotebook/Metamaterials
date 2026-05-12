using System.Collections.Generic;
using UnityEngine;

namespace Representation
{
    /// <summary>
    /// Creates spherical node GameObjects from a list of world-space positions.
    /// Radius = Thickness * 51/50 to match the Python implementation.
    /// </summary>
    [DisallowMultipleComponent]
    public class SurfaceMeshingTorch : MonoBehaviour
    {
        [System.Serializable]
        public class RepresentationData
        {
            public List<Vector3> NodePositions = new List<Vector3>();
            public float Thickness = 0.01f;
        }

        [Header("Defaults")]
        [Tooltip("Optional prefab to use instead of Unity's primitive sphere.")]
        public GameObject DefaultSpherePrefab;

        [Tooltip("Optional parent transform for generated spheres.")]
        public Transform GeneratedParent;

        [Tooltip("Automatically clear old spheres before creating new ones.")]
        public bool AutoClearBeforeBuild = true;

        private readonly List<GameObject> _generated = new List<GameObject>();

        /// <summary>
        /// Instantiate spheres at node positions.
        /// </summary>
        public void CreateNodeSpheresFromRepresentation(
            RepresentationData rep,
            GameObject spherePrefab = null,
            Transform parent = null)
        {
            if (rep == null || rep.NodePositions == null || rep.NodePositions.Count == 0)
                return;

            if (AutoClearBeforeBuild)
                ClearGenerated();

            GameObject prefabToUse = spherePrefab ?? DefaultSpherePrefab;
            Transform root = parent ?? GeneratedParent ?? transform;

            float radius = rep.Thickness * 51f / 50f;
            float diameter = radius * 2f;

            for (int i = 0; i < rep.NodePositions.Count; i++)
            {
                Vector3 pos = rep.NodePositions[i];
                GameObject go;

                if (prefabToUse != null)
                {
                    go = Instantiate(prefabToUse, pos, Quaternion.identity, root);

                    // Ensure prefab scale behaves predictably
                    Vector3 baseScale = prefabToUse.transform.localScale;
                    go.transform.localScale = baseScale * diameter;
                }
                else
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    go.transform.SetParent(root, true);
                    go.transform.position = pos;

                    // Remove collider for performance
                    var col = go.GetComponent<Collider>();
                    if (col != null)
                        Destroy(col);

                    go.transform.localScale = Vector3.one * diameter;
                }

                go.name = $"NodeSphere_{i}";
                _generated.Add(go);
            }
        }

        /// <summary>
        /// Remove all previously generated spheres.
        /// </summary>
        public void ClearGenerated()
        {
            for (int i = _generated.Count - 1; i >= 0; i--)
            {
                var g = _generated[i];
                if (g != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        DestroyImmediate(g);
                    else
                        Destroy(g);
#else
                    Destroy(g);
#endif
                }
            }
            _generated.Clear();
        }
    }
}
