// MicrostructureLoader.cs
// Attach to an empty GameObject. Assign the JSON TextAsset + three prefabs.
//
// All JSON data classes are defined at the bottom of this file —
// no separate MicrostructureData.cs is required.
//
// Uses only Unity's built-in JsonUtility — no Newtonsoft or extra packages.
//
// Point arrays (control points, face positions) are stored as FLAT float lists
// with an implicit stride of 3: [x0,y0,z0, x1,y1,z1, ...].
// This avoids List<List<float>> which JsonUtility cannot deserialize.
//
// JSON contract (single-octant export):
//   nodes[]  : node_id, position:[x,y,z], active, mirror_axes?:[...]
//   edges[]  : node1, node2, start:[x,y,z], end:[x,y,z],
//              control_points_flat:[x,y,z,...], mirror_axes?:[...]
//   faces[]  : node1, node2, node3,
//              positions_flat:[x,y,z, x,y,z, x,y,z],  <- 3 corners, absolute
//              control_points_flat:[x,y,z,...]          <- N ctrl pts, absolute
//   shape    : [X, Y, Z]
//
// Coordinate system & mirroring
// ------------------------------
//   Canonical octant: [0,1]^3.
//   Reflection around coord=1 (matches Python metamaterial_grid):
//       original  ->  coord        occupies [0,1]
//       reflected ->  2 - coord    occupies [1,2]
//   Seam (|coord-1| < 1e-6) -> same world position in both octants -> spawn once.
//   worldScale applied after reflection.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Microstructure
{
    // =========================================================================
    // Loader
    // =========================================================================

    public class MicrostructureLoader : MonoBehaviour
    {
        [Header("Data")]
        public TextAsset microstructureJson;

        [Header("Prefabs")]
        public GameObject nodePrefab;
        public GameObject edgePrefab;
        public GameObject facePrefab;

        [Header("Options")]
        public bool  loadOnStart    = true;
        public float worldScale     = 1f;

        [Tooltip("Only spawn nodes marked active in the JSON.")]
        public bool activeNodesOnly = true;

        [Tooltip("Spawn only the single canonical octant (no reflections). " +
                 "Useful for verifying the exported octant before enabling full mirroring.")]
        public bool singleOctantOnly = false;

        // Runtime references
        [HideInInspector] public MicrostructureFile       Data;
        [HideInInspector] public List<MicrostructureNode> Nodes = new();
        [HideInInspector] public List<MicrostructureEdge> Edges = new();
        [HideInInspector] public List<MicrostructureFace> Faces = new();

        public static float GlobalNodeRadius { get; private set; } = 1.5f;
        public float nodeRadiusOverride
        {
            get => _nodeRadiusOverride;
            set
            {
                _nodeRadiusOverride = value;
                GlobalNodeRadius = value;
            }
        }
        // Change the default in the field declaration:
        private float _nodeRadiusOverride = 1.5f;

        readonly Dictionary<(int octant, int nodeId), MicrostructureNode> _nodeMap = new();

        // Reflection table: component 0 = keep coord, 1 = reflect (2 - coord)
        static readonly Vector3Int[] s_Reflect =
        {
            new(0,0,0), new(1,0,0), new(0,1,0), new(1,1,0),
            new(0,0,1), new(1,0,1), new(0,1,1), new(1,1,1),
        };

        const float SeamTol = 1e-6f;

        void Start() { 
            GlobalNodeRadius = nodeRadiusOverride;
            if (loadOnStart) Load(); 
        }


        // private void RegisterNodeMirrors()
        // {
        //     // Build a map of original node IDs to their canonical octant instance
        //     var canonicalNodes = new Dictionary<int, MicrostructureNode>();
            
        //     // First, collect all nodes and identify their canonical octant
        //     foreach (var node in Nodes)
        //     {
        //         // Parse octant from name or use a different method to identify canonical
        //         // Assuming node names are like "Node_123_oct0" for canonical
        //         if (node.gameObject.name.Contains("_oct0"))
        //         {
        //             // Extract node ID from name
        //             int nodeId = ExtractNodeId(node.gameObject.name);
        //             canonicalNodes[nodeId] = node;
        //         }
        //     }
            
        //     // Now for each node, find its mirrors in other octants
        //     foreach (var node in Nodes)
        //     {
        //         // Skip if this is the canonical node itself
        //         if (node.gameObject.name.Contains("_oct0")) continue;
                
        //         // Extract node ID and octant from name
        //         int nodeId = ExtractNodeId(node.gameObject.name);
        //         int octant = ExtractOctant(node.gameObject.name);
                
        //         // Find the canonical version
        //         if (canonicalNodes.TryGetValue(nodeId, out var canonicalNode))
        //         {
        //             // Register this mirror with the canonical node
        //             canonicalNode.AddMirror(octant, node);
                    
        //             // Also register the reverse mapping (optional, for completeness)
        //             node.AddMirror(0, canonicalNode);
        //         }
        //     }
            
        //     Debug.Log($"[MicrostructureLoader] Registered mirrors for {canonicalNodes.Count} canonical nodes");
        // }
        private void RegisterNodeMirrors()
        {
            // With unified naming, use _nodeMap to identify canonical nodes
            // _nodeMap[(0, nodeId)] = canonical node for octant 0
            foreach (var kvp in _nodeMap)
            {
                int octant = kvp.Key.octant;
                int nodeId = kvp.Key.nodeId;
                if (octant == 0) continue; // skip canonical

                if (_nodeMap.TryGetValue((0, nodeId), out var canonicalNode))
                {
                    var mirroredNode = kvp.Value;
                    if (mirroredNode != canonicalNode)
                    {
                        canonicalNode.AddMirror(octant, mirroredNode);
                        mirroredNode.AddMirror(0, canonicalNode);
                    }
                }
            }
            Debug.Log($"[MicrostructureLoader] RegisterNodeMirrors complete.");
        }

        private int ExtractNodeId(string name)
        {
            // Parse "Node_123" -> 123
            var parts = name.Split('_');
            if (parts.Length >= 2 && int.TryParse(parts[1], out int id))
                return id;
            return -1;
        }

        private int ExtractOctant(string name)
        {
            // No longer encoded in name — octant is tracked via _nodeMap instead
            return 0;
        }
        public void Load()
        {
            if (microstructureJson == null)
            {
                Debug.LogError("[MicrostructureLoader] No JSON assigned.");
                return;
            }
            Clear();

            Data = JsonUtility.FromJson<MicrostructureFile>(microstructureJson.text);
            if (Data == null)
            {
                Debug.LogError("[MicrostructureLoader] JSON deserialised to null.");
                return;
            }

            Debug.Log($"[MicrostructureLoader] Loaded '{Data.name}' — " +
                      $"active_nodes={Data.stats?.num_active_nodes}  " +
                      $"edges={Data.stats?.num_edges}  faces={Data.stats?.num_faces}  " +
                      $"shape=[{Data.shape?[0]},{Data.shape?[1]},{Data.shape?[2]}]");
            Debug.Log($"[MicrostructureLoader] Spawning {OctantCount()} octant(s)" +
                      $"{(singleOctantOnly ? " (single octant mode)" : "")}.");

            SpawnAllOctants();
            if (OctantVisualController.Instance != null)
                OctantVisualController.Instance.RefreshVisuals(
                    OctantMirrorSystem.Instance != null
                        ? OctantMirrorSystem.Instance.activeOctantIndex
                        : 0);
        }

        // ── Octant helpers ──────────────────────────────────────────────────

        int OctantCount()
        {
            if (singleOctantOnly) return 1;
            if (Data.shape == null || Data.shape.Length < 3) return 1;
            return (Data.shape[0] >= 2 ? 2 : 1)
                 * (Data.shape[1] >= 2 ? 2 : 1)
                 * (Data.shape[2] >= 2 ? 2 : 1);
        }
        // ALL OCTANTS
        IEnumerable<(int index, Vector3Int reflect)> ActiveOctants()
        {
            int n = OctantCount();
            for (int i = 0; i < n; i++) yield return (i, s_Reflect[i]);
        }

        //ONLY OCTANT 0
        // IEnumerable<(int index, Vector3Int reflect)> ActiveOctants()
        // {
        //     int n = OctantCount();
        //     for (int i = 0; i < n; i++) 
        //     {
        //         // Only return octant 0
        //         if (i == 7)
        //             yield return (i, s_Reflect[i]);
        //         // Skip all other octants
        //     }
        // }

        // ── Spawn ───────────────────────────────────────────────────────────

        private Transform GetOctantRoot(int octantIndex)
        {
            // Find OctantVisualController and use its roots if available
            if (OctantVisualController.Instance != null &&
                octantIndex < OctantVisualController.Instance.octantRoots.Count &&
                OctantVisualController.Instance.octantRoots[octantIndex] != null)
                return OctantVisualController.Instance.octantRoots[octantIndex].transform;

            // Fallback to loader's own transform
            return transform;
        }

        void SpawnAllOctants()
        {
            // Spawn ALL nodes first for all octants
            foreach (var (i, r) in ActiveOctants()) 
                SpawnNodesForOctant(i, r);
            
            // Spawn ALL edges for all octants
            foreach (var (i, r) in ActiveOctants()) 
                SpawnEdgesForOctant(i, r);
            
            // Spawn ALL faces for all octants (edges now exist in all octants)
            foreach (var (i, r) in ActiveOctants()) 
                SpawnFacesForOctant(i, r);
        }
        void SpawnNodesForOctant(int octIdx, Vector3Int reflect)
        {
            if (Data.nodes == null) return;
            foreach (var nd in Data.nodes)
            {
                if (activeNodesOnly && !nd.active) continue;
                if (nodePrefab == null) continue;

                bool isSeam = false;

                // bool isSeam = nd.mirror_axes != null && nd.mirror_axes.Count > 0;
                Vector3Int eff = reflect;
                if (isSeam)
                {
                    if (nd.IsOnAxis("x")) eff.x = 0;
                    if (nd.IsOnAxis("y")) eff.y = 0;
                    if (nd.IsOnAxis("z")) eff.z = 0;
                }

                int canonical = CanonicalOctantIndex(reflect, nd.mirror_axes);
                var key = (canonical, nd.node_id);
                if (_nodeMap.ContainsKey(key))
                {
                    _nodeMap[(octIdx, nd.node_id)] = _nodeMap[key];
                    continue;
                }

                var worldPos = ReflectPt(nd.position, eff) * worldScale;
                var go = Instantiate(nodePrefab, transform); // ← back to original
                // go.name = $"Node_{nd.node_id}_oct{canonical}";
                go.name = $"Node_{nd.node_id}";
                var comp = go.GetComponent<MicrostructureNode>();
                if (comp != null)
                {
                    float nodeRadius = nodeRadiusOverride > 0 ? nodeRadiusOverride : Data.node_radius * worldScale;
                    comp.Initialise(nd, nodeRadius, worldPos);
                    Debug.Log($"[MicrostructureLoader] Spawned node radius: {nodeRadius}, " +
          $"resulting scale: {comp.transform.localScale}");
                    _nodeMap[key] = comp;
                    _nodeMap[(octIdx, nd.node_id)] = comp;
                    Nodes.Add(comp);
                    OnNodeLoaded(comp);
                }
            }
        }
        void SpawnEdgesForOctant(int octIdx, Vector3Int reflect)
        {
            if (Data.edges == null) return;
            
            foreach (var ed in Data.edges)
            {
                if (edgePrefab == null) continue;

                bool isSeam = ed.mirror_axes != null && ed.mirror_axes.Count > 0;
                
                // For debugging, temporarily disable seam filtering
                // Comment out this check to spawn all edges in all octants
                // if (isSeam && octIdx != CanonicalOctantIndex(reflect, ed.mirror_axes))
                //     continue;

                Vector3Int eff = reflect;
                if (isSeam)
                {
                    // Only zero out axes that are actually on the seam AFTER reflection
                    Vector3 startPos = ReflectPt(ed.start, reflect);
                    if (ed.IsOnAxis("x") && Mathf.Abs(startPos.x - 1.0f) < SeamTol) eff.x = 0;
                    if (ed.IsOnAxis("y") && Mathf.Abs(startPos.y - 1.0f) < SeamTol) eff.y = 0;
                    if (ed.IsOnAxis("z") && Mathf.Abs(startPos.z - 1.0f) < SeamTol) eff.z = 0;
                }

                var start = ReflectPt(ed.start, eff) * worldScale;
                var end = ReflectPt(ed.end, eff) * worldScale;
                var controls = ReflectFlat(ed.control_points_flat, eff, worldScale);

                var go = Instantiate(edgePrefab, transform);
                go.name = $"Edge_{ed.node1}-{ed.node2}";
                
                // Special debug for edges 4-5 and 5-6
                if ((ed.node1 == 4 && ed.node2 == 5) || (ed.node1 == 5 && ed.node2 == 4) ||
                    (ed.node1 == 5 && ed.node2 == 6) || (ed.node1 == 6 && ed.node2 == 5))
                {
                    Debug.Log($"[SpawnEdgesForOctant] Edge {ed.node1}-{ed.node2} in octant {octIdx}:");
                    Debug.Log($"  start={start}, end={end}");
                    Debug.Log($"  controls count={controls.Count}");
                    Debug.Log($"  isSeam={isSeam}, mirror_axes={string.Join(",", ed.mirror_axes ?? new List<string>())}");
                }
                
                var comp = go.GetComponent<MicrostructureEdge>();
                if (comp != null)
                {
                    comp.Initialise(ed, Data.edge_thickness * worldScale, start, end, controls);
                    comp.SetMirrored(octIdx != 0);
                    Edges.Add(comp);
                    OnEdgeLoaded(comp);
                }
            }
        }
        // void SpawnEdgesForOctant(int octIdx, Vector3Int reflect)
        // {
        //     if (Data.edges == null) return;
        //     Debug.Log($"[SpawnEdgesForOctant] Starting to spawn edges for octant {octIdx}. Total edges in JSON: {Data.edges.Count}");
        //     foreach (var ed in Data.edges)
        //     {
        //         if (edgePrefab == null) continue;

        //         bool isSeam = ed.mirror_axes != null && ed.mirror_axes.Count > 0;
        //         if (isSeam && octIdx != CanonicalOctantIndex(reflect, ed.mirror_axes))
        //             continue;

        //         Vector3Int eff = reflect;
        //         if (isSeam)
        //         {
        //             if (ed.IsOnAxis("x")) eff.x = 0;
        //             if (ed.IsOnAxis("y")) eff.y = 0;
        //             if (ed.IsOnAxis("z")) eff.z = 0;
        //         }

        //         var start    = ReflectPt(ed.start, eff) * worldScale;
        //         var end      = ReflectPt(ed.end,   eff) * worldScale;
        //         var controls = ReflectFlat(ed.control_points_flat, eff, worldScale);

        //         var go = Instantiate(edgePrefab, transform); // ← back to original
        //         // go.name = $"Edge_{ed.node1}_{ed.node2}_oct{octIdx}";
        //         go.name = $"Edge_{ed.node1}-{ed.node2}";
        //         Debug.Log($"[SpawnEdgesForOctant] CREATED edge with name: {go.name}");
        //         var comp = go.GetComponent<MicrostructureEdge>();
        //         if (comp != null)
        //         {
        //             comp.Initialise(ed, Data.edge_thickness * worldScale, start, end, controls);
        //             Edges.Add(comp);
        //             OnEdgeLoaded(comp);
        //         }
        //     }
        // }
        private MicrostructureEdge FindLoadedEdgeForOctant(int nodeId1, int nodeId2, int octIdx)
        {
            string nameA = $"Edge_{nodeId1}-{nodeId2}";
            string nameB = $"Edge_{nodeId2}-{nodeId1}";
            
            // Calculate expected coordinate range for this octant
            float minX = (octIdx % 2 == 0) ? 0f : 1f;
            float maxX = (octIdx % 2 == 0) ? 1f : 2f;
            float minY = ((octIdx / 2) % 2 == 0) ? 0f : 1f;
            float maxY = ((octIdx / 2) % 2 == 0) ? 1f : 2f;
            float minZ = (octIdx / 4 < 1) ? 0f : 1f;
            float maxZ = (octIdx / 4 < 1) ? 1f : 2f;
            
            Debug.Log($"[FindLoadedEdgeForOctant] Looking for edge {nameA} in octant {octIdx} (x in [{minX},{maxX}])");
            
            foreach (var e in Edges)
            {
                if (e == null) continue;
                
                // Check by name
                if (e.gameObject.name == nameA || e.gameObject.name == nameB)
                {
                    // Verify this edge belongs to the correct octant by checking its position
                    if (e.BezierPts != null && e.BezierPts.Length > 0)
                    {
                        float x = e.BezierPts[0].x;
                        float y = e.BezierPts[0].y;
                        float z = e.BezierPts[0].z;
                        
                        bool inCorrectOctant = (x >= minX - 0.1f && x <= maxX + 0.1f) &&
                                            (y >= minY - 0.1f && y <= maxY + 0.1f) &&
                                            (z >= minZ - 0.1f && z <= maxZ + 0.1f);
                        
                        if (inCorrectOctant)
                        {
                            Debug.Log($"[FindLoadedEdgeForOctant] ✓ Found edge {e.gameObject.name} for octant {octIdx} (pos={x:F2},{y:F2},{z:F2})");
                            return e;
                        }
                        else
                        {
                            Debug.Log($"[FindLoadedEdgeForOctant] ✗ Edge {e.gameObject.name} is in wrong octant (pos={x:F2},{y:F2},{z:F2}, expected octant {octIdx})");
                        }
                    }
                    else
                    {
                        // Fallback: check by IsMirrored flag
                        bool expectedMirrored = (octIdx != 0);
                        if (e.IsMirrored == expectedMirrored)
                        {
                            Debug.Log($"[FindLoadedEdgeForOctant] ✓ Found edge {e.gameObject.name} by mirror flag for octant {octIdx}");
                            return e;
                        }
                    }
                }
            }
            
            Debug.LogWarning($"[FindLoadedEdgeForOctant] ✗ Could NOT find edge for nodes {nodeId1}-{nodeId2} in octant {octIdx}");
            return null;
        }

        void SpawnFacesForOctant(int octIdx, Vector3Int reflect)
        {
            if (Data.faces == null) return;
            
            foreach (var fd in Data.faces)
            {
                // Special debug for face 4-6-8
                bool isFace468 = (fd.node1 == 4 && fd.node2 == 6 && fd.node3 == 8) ||
                                (fd.node1 == 4 && fd.node2 == 8 && fd.node3 == 6) ||
                                (fd.node1 == 6 && fd.node2 == 4 && fd.node3 == 8) ||
                                (fd.node1 == 6 && fd.node2 == 8 && fd.node3 == 4) ||
                                (fd.node1 == 8 && fd.node2 == 4 && fd.node3 == 6) ||
                                (fd.node1 == 8 && fd.node2 == 6 && fd.node3 == 4);
                
                if (isFace468)
                {
                    Debug.Log($"=== DEBUG FACE 4-6-8 in octant {octIdx} ===");
                    Debug.Log($"Face nodes: {fd.node1}, {fd.node2}, {fd.node3}");
                    
                    // Use different variable names to avoid conflict
                    Vector3? corner0 = GetNodeWorldPos(fd.node1, octIdx);
                    Vector3? corner1 = GetNodeWorldPos(fd.node2, octIdx);
                    Vector3? corner2 = GetNodeWorldPos(fd.node3, octIdx);
                    Debug.Log($"Corner positions: corner0={corner0}, corner1={corner1}, corner2={corner2}");
                }
                
                // Derive corners from node positions — already correctly placed
                Vector3? p0 = GetNodeWorldPos(fd.node1, octIdx);
                Vector3? p1 = GetNodeWorldPos(fd.node2, octIdx);
                Vector3? p2 = GetNodeWorldPos(fd.node3, octIdx);

                if (p0 == null || p1 == null || p2 == null)
                {
                    Debug.LogWarning($"[MicrostructureLoader] Cannot find nodes for face " +
                                    $"{fd.node1}-{fd.node2}-{fd.node3} oct{octIdx}. Skipping.");
                    continue;
                }

                var positions = new List<Vector3> { p0.Value, p1.Value, p2.Value };

                // Control points are already in full world space [0,2]^3 —
                // do NOT reflect them, just scale by worldScale
                var controls = new List<Vector3>();
                if (fd.control_points_flat != null)
                {
                    for (int i = 0; i + 2 < fd.control_points_flat.Count; i += 3)
                    {
                        controls.Add(new Vector3(
                            (float)fd.control_points_flat[i]     * worldScale,
                            (float)fd.control_points_flat[i + 1] * worldScale,
                            (float)fd.control_points_flat[i + 2] * worldScale));
                    }
                }

                Debug.Log($"[MicrostructureLoader] Face {fd.node1}-{fd.node2}-{fd.node3} " +
                        $"oct{octIdx} — corners: {positions[0]:F3}, {positions[1]:F3}, {positions[2]:F3}, " +
                        $"controls count: {controls.Count}");

                var go = Instantiate(facePrefab, transform);
                go.name = $"Face_{fd.node1}_{fd.node2}_{fd.node3}_oct{octIdx}";
                var comp = go.GetComponent<MicrostructureFace>();
                if (comp != null)
                {
                    MicrostructureEdge e01 = FindLoadedEdgeForOctant(fd.node1, fd.node2, octIdx);
                    MicrostructureEdge e12 = FindLoadedEdgeForOctant(fd.node2, fd.node3, octIdx);
                    MicrostructureEdge e20 = FindLoadedEdgeForOctant(fd.node3, fd.node1, octIdx);
                    
                    if (isFace468)
                    {
                        Debug.Log($"Edge 4-6: {(e01 != null ? e01.gameObject.name + " exists, curve points: " + (e01.BezierPts != null ? e01.BezierPts.Length.ToString() : "null") : "NULL")}");
                        Debug.Log($"Edge 6-8: {(e12 != null ? e12.gameObject.name + " exists, curve points: " + (e12.BezierPts != null ? e12.BezierPts.Length.ToString() : "null") : "NULL")}");
                        Debug.Log($"Edge 8-4: {(e20 != null ? e20.gameObject.name + " exists, curve points: " + (e20.BezierPts != null ? e20.BezierPts.Length.ToString() : "null") : "NULL")}");
                        
                        if (e01 != null && e01.BezierPts != null)
                        {
                            Debug.Log($"Edge 4-6 Bezier points: {string.Join(" -> ", e01.BezierPts.Select(p => p.ToString("F3")))}");
                        }
                        if (e12 != null && e12.BezierPts != null)
                        {
                            Debug.Log($"Edge 6-8 Bezier points: {string.Join(" -> ", e12.BezierPts.Select(p => p.ToString("F3")))}");
                        }
                        if (e20 != null && e20.BezierPts != null)
                        {
                            Debug.Log($"Edge 8-4 Bezier points: {string.Join(" -> ", e20.BezierPts.Select(p => p.ToString("F3")))}");
                        }
                    }
                    
                    comp.Initialise(fd, worldScale, positions, controls,
                        e01?.BezierPts, e12?.BezierPts, e20?.BezierPts);

                    Faces.Add(comp);

                    if (GraphManager.Instance != null)
                        GraphManager.Instance.RegisterOriginalFace(comp);
                }
            }
        }

        // void SpawnFacesForOctant(int octIdx, Vector3Int reflect)
        // {
        //     if (Data.faces == null) return;
            
        //     // DEBUG: List all edges with their positions to verify they're from correct octant
        //     Debug.Log($"[SpawnFacesForOctant] === OCTANT {octIdx} ===");
        //     foreach (var e in Edges)
        //     {
        //         if (e != null && e.BezierPts != null && e.BezierPts.Length > 0)
        //         {
        //             float x = e.BezierPts[0].x;
        //             Debug.Log($"[SpawnFacesForOctant] Available edge: {e.gameObject.name}, first point x={x:F3}, isMirrored={e.IsMirrored}");
        //         }
        //     }
            
        //     foreach (var fd in Data.faces)
        //     {
                
        //         if (facePrefab == null) continue;

        //         // Derive corners from node positions — already correctly placed
        //         Vector3? p0 = GetNodeWorldPos(fd.node1, octIdx);
        //         Vector3? p1 = GetNodeWorldPos(fd.node2, octIdx);
        //         Vector3? p2 = GetNodeWorldPos(fd.node3, octIdx);

        //         if (p0 == null || p1 == null || p2 == null)
        //         {
        //             Debug.LogWarning($"[MicrostructureLoader] Cannot find nodes for face " +
        //                             $"{fd.node1}-{fd.node2}-{fd.node3} oct{octIdx}. Skipping.");
        //             continue;
        //         }

        //         var positions = new List<Vector3> { p0.Value, p1.Value, p2.Value };

        //         // Control points are already in full world space [0,2]^3 —
        //         // do NOT reflect them, just scale by worldScale
        //         var controls = new List<Vector3>();
        //         if (fd.control_points_flat != null)
        //         {
        //             for (int i = 0; i + 2 < fd.control_points_flat.Count; i += 3)
        //             {
        //                 controls.Add(new Vector3(
        //                     (float)fd.control_points_flat[i]     * worldScale,
        //                     (float)fd.control_points_flat[i + 1] * worldScale,
        //                     (float)fd.control_points_flat[i + 2] * worldScale));
        //             }
        //         }

        //         // Find edges that belong to THIS octant
        //         MicrostructureEdge e01 = FindLoadedEdgeForOctant(fd.node1, fd.node2, octIdx);
        //         MicrostructureEdge e12 = FindLoadedEdgeForOctant(fd.node2, fd.node3, octIdx);
        //         MicrostructureEdge e20 = FindLoadedEdgeForOctant(fd.node3, fd.node1, octIdx);
                
        //         Debug.Log($"[SpawnFacesForOctant] Octant {octIdx} - Face {fd.node1}-{fd.node2}-{fd.node3}: " +
        //                 $"e01={(e01 != null ? e01.gameObject.name : "null")}, " +
        //                 $"e12={(e12 != null ? e12.gameObject.name : "null")}, " +
        //                 $"e20={(e20 != null ? e20.gameObject.name : "null")}");

        //         var go = Instantiate(facePrefab, transform);
        //         go.name = $"Face_{fd.node1}_{fd.node2}_{fd.node3}_oct{octIdx}";
        //         var comp = go.GetComponent<MicrostructureFace>();
        //         if (comp != null)
        //         {
        //             comp.Initialise(fd, worldScale, positions, controls,
        //                 e01?.BezierPts, e12?.BezierPts, e20?.BezierPts);

        //             if (comp.GetComponent<MeshCollider>() == null && Application.isPlaying)
        //             {
        //                 comp.gameObject.AddComponent<MeshCollider>();
        //             }
        //             Faces.Add(comp);
        //         }
        //     }
        // }

        // Helper to get world position of a node by ID and octant
        private Vector3? GetNodeWorldPos(int nodeId, int octIdx)
        {
            if (_nodeMap.TryGetValue((octIdx, nodeId), out var node))
                return node.transform.position;
            // Try canonical octant as fallback
            if (_nodeMap.TryGetValue((0, nodeId), out var canonNode))
                return canonNode.transform.position;
            return null;
        }


        private MicrostructureEdge FindLoadedEdge(int nodeId1, int nodeId2)
        {
            string nameA = $"Edge_{nodeId1}-{nodeId2}";
            string nameB = $"Edge_{nodeId2}-{nodeId1}";

            foreach (var e in Edges)
            {
                if (e == null) continue;
                if (e.gameObject.name == nameA || e.gameObject.name == nameB)
                    return e;
                // Data ID fallback
                if (e.Data != null &&
                    ((e.Data.node1 == nodeId1 && e.Data.node2 == nodeId2) ||
                    (e.Data.node1 == nodeId2 && e.Data.node2 == nodeId1)))
                    return e;
            }
            return null;
        }

        // ── Reflection helpers ──────────────────────────────────────────────

        // Single coordinate: keep or reflect around 1.
        static float RC(float c, int flag)
        {
            if (flag == 0) return c;
            return Mathf.Abs(c - 1f) <= SeamTol ? 1f : 2f - c;
        }

        // Reflect a [x,y,z] List<float> point.
        static Vector3 ReflectPt(List<float> p, Vector3Int r)
        {
            if (p == null || p.Count < 3) return Vector3.zero;
            return new Vector3(RC(p[0], r.x), RC(p[1], r.y), RC(p[2], r.z));
        }

        // Unpack and reflect a flat [x,y,z, x,y,z, ...] List<float> (stride 3).
        static List<Vector3> ReflectFlat(List<float> flat, Vector3Int r, float scale)
        {
            var result = new List<Vector3>();
            if (flat == null) return result;
            for (int i = 0; i + 2 < flat.Count; i += 3)
                result.Add(new Vector3(RC(flat[i], r.x),
                                       RC(flat[i+1], r.y),
                                       RC(flat[i+2], r.z)) * scale);
            return result;
        }

        // ── Canonical octant index ──────────────────────────────────────────

        static int CanonicalOctantIndex(Vector3Int reflect, List<string> seamAxes)
        {
            Vector3Int c = reflect;
            if (seamAxes != null)
                foreach (var a in seamAxes)
                {
                    if (a == "x") c.x = 0;
                    if (a == "y") c.y = 0;
                    if (a == "z") c.z = 0;
                }
            for (int i = 0; i < s_Reflect.Length; i++)
                if (s_Reflect[i] == c) return i;
            return 0;
        }

        [ContextMenu("Clear Microstructure")]
        public void Clear()
        {
            Nodes.Clear(); Edges.Clear(); Faces.Clear(); _nodeMap.Clear();
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(transform.GetChild(i).gameObject);
            Debug.Log("[MicrostructureLoader] Cleared.");
        }

        private void OnNodeLoaded(MicrostructureNode node)
        {
            if (GraphManager.Instance != null)
                GraphManager.Instance.RegisterOriginalNode(node);
        }

        private void OnEdgeLoaded(MicrostructureEdge edge)
        {
            if (GraphManager.Instance != null)
                GraphManager.Instance.RegisterOriginalEdge(edge);
        }
    }

    // =========================================================================
    // JSON data classes  (deserialized by JsonUtility)
    // All multi-point arrays use flat List<float> with stride 3.
    // =========================================================================

    [Serializable]
    public class MicrostructureFile
    {
        public string            name;
        public int[]             shape;
        public float             node_radius;
        public float             edge_thickness;
        public float             thickness;
        public int               num_nodes;
        public MicroStats        stats;
        public List<NodeData>    nodes;
        public List<EdgeData>    edges;
        public List<FaceData>    faces;
    }

    [Serializable]
    public class MicroStats
    {
        public int num_active_nodes;
        public int num_boundary_nodes;
        public int num_boundary_edges;
        public int num_edges;
        public int num_faces;
    }

    [Serializable]
    public class NodeData
    {
        public int           node_id;
        public List<float>   position;      // [x, y, z]
        public bool          active;
        public List<string>  mirror_axes;

        public bool IsOnAxis(string axis) =>
            mirror_axes != null && mirror_axes.Contains(axis);

        public Vector3 AsVector3() =>
            position != null && position.Count >= 3
                ? new Vector3(position[0], position[1], position[2])
                : Vector3.zero;
    }

    [Serializable]
    public class EdgeData
    {
        public int           node1;
        public int           node2;
        public List<float>   start;                  // [x, y, z]
        public List<float>   end;                    // [x, y, z]
        public List<float>   control_points_flat;    // [x,y,z, x,y,z, ...] stride 3
        public List<string>  mirror_axes;

        public bool IsOnAxis(string axis) =>
            mirror_axes != null && mirror_axes.Contains(axis);
    }

    [Serializable]
    public class FaceData
    {
        public int           node1;
        public int           node2;
        public int           node3;
        /// <summary>3 corner positions as flat [x,y,z, x,y,z, x,y,z] (absolute).</summary>
        public List<float>   positions_flat;
        /// <summary>Bezier control points as flat [x,y,z, ...] (absolute, stride 3).</summary>
        public List<float>   control_points_flat;
    }
}