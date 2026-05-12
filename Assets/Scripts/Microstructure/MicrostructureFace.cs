using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Microstructure
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class MicrostructureFace : MonoBehaviour
    {
        [Header("Appearance")]
        public Material material;
        [SerializeField] private Material highlightMaterial;

        [Header("Curve Settings")]
        [Tooltip("Bézier degree n = M+1. 0 or 1 = flat triangle.")]
        public int bezierM = 1; // degree n = M+1
        [Tooltip("Sampling resolution per edge. Higher = smoother.")]
        public int samplingL = 8;

        // ── Public state ──────────────────────────────────────────────────────
        public FaceData   Data    { get; private set; }
        public Vector3[]  Corners { get; private set; }
        public Vector3    FaceNormal { get; private set; }

        // Bézier control points (world space, full array)
        public Vector3[] ControlPoints { get; private set; }
        public int       BezierDegree  { get; private set; }

        // Stored interior displacements from Corners[0]
        private List<Vector3> _interiorDisplacements = new List<Vector3>();

        // Mirror flag
        private bool _isMirrored = false;
        public bool IsMirrored => _isMirrored;
        public void SetMirrored(bool m) => _isMirrored = m;

        // Edge curve references for C0 boundary consistency
        private Vector3[] _edge01Pts; // Corners[0] → Corners[1]
        private Vector3[] _edge12Pts; // Corners[1] → Corners[2]
        private Vector3[] _edge20Pts; // Corners[2] → Corners[0]

        // Shell half-thickness
        private float _shellHalfThickness = 0.015f;

        // Highlight state
        public bool IsHighlighted { get; private set; } = false;
        private Material _materialBeforeHighlight;
        private MeshRenderer _mr;
        private MeshCollider _mc;
        private MeshFilter _mf;

        private void GetReferences()
        {
            _mf = GetComponent<MeshFilter>();
            _mr = GetComponent<MeshRenderer>();
            _mc = GetComponent<MeshCollider>();
        }


        public void Initialise(FaceData data, float worldScale,
                            List<Vector3> positions,
                            List<Vector3> controlPoints,
                            Vector3[] edge01Pts = null,
                            Vector3[] edge12Pts = null,
                            Vector3[] edge20Pts = null)
        {
            Data = data;
            _mf  = GetComponent<MeshFilter>();
            _mr  = GetComponent<MeshRenderer>();

            if (material != null && _mr != null) _mr.material = material;

            if (positions == null || positions.Count < 3)
            {
                Debug.LogWarning($"[MicrostructureFace] {gameObject.name}: fewer than 3 positions.");
                return;
            }

            Corners = new[] { positions[0], positions[1], positions[2] };
            _shellHalfThickness = 0.03f * worldScale;

            Vector3 ab = Corners[1] - Corners[0];
            Vector3 ac = Corners[2] - Corners[0];
            FaceNormal = Vector3.Cross(ab, ac).normalized;
            if (FaceNormal == Vector3.zero) FaceNormal = Vector3.up;

            // Store edge curves
            _edge01Pts = edge01Pts;
            _edge12Pts = edge12Pts;
            _edge20Pts = edge20Pts;

            // Detect edge degree and force matching face degree
            int edgeDegree = 1;
            if (edge01Pts != null && edge01Pts.Length > 0)
            {
                edgeDegree = edge01Pts.Length - 1;
            }
            
            int targetDegree = edgeDegree;
            int targetBezierM = targetDegree - 1;
            
            Debug.Log($"[MicrostructureFace] {gameObject.name} — " +
                    $"Edge degree: {edgeDegree} (from {edge01Pts?.Length ?? 0} control points), " +
                    $"Setting face bezierM={targetBezierM} (degree {targetDegree})");
            
            // Override the inspector value
            bezierM = targetBezierM;
            int n = bezierM + 1;

            // IGNORE the JSON interior displacements - compute our own from edges
            _interiorDisplacements = new List<Vector3>();

            if (n == 3 && _edge01Pts != null && _edge12Pts != null && _edge20Pts != null)
            {
                // Compute interior point as average of the three edge midpoints
                Vector3 edge01_mid = BezierTriangle.EvalBezierArray(_edge01Pts, 0.5f);
                Vector3 edge12_mid = BezierTriangle.EvalBezierArray(_edge12Pts, 0.5f);
                Vector3 edge20_mid = BezierTriangle.EvalBezierArray(_edge20Pts, 0.5f);
                bool isMirrored = Corners[0].x > 1.0f || Corners[0].y > 1.0f || Corners[0].z > 1.0f;
                
                Debug.Log($"[MicrostructureFace] {gameObject.name} - Computing interior point:" +
                        $"\n  isMirrored = {isMirrored}" +
                        $"\n  Corners: p0={Corners[0]}, p1={Corners[1]}, p2={Corners[2]}" +
                        $"\n  Edge midpoints: e01={edge01_mid}, e12={edge12_mid}, e20={edge20_mid}");
                
                
                Vector3 interiorAbs = (edge01_mid + edge12_mid + edge20_mid) / 3f;
                Debug.Log($"[MicrostructureFace]  interiorAbs (from average) = {interiorAbs}");
                bool interiorInCorrectOctant = Corners[0].x > 1.0f ? interiorAbs.x > 1.0f : interiorAbs.x <= 1.0f;
                Debug.Log($"[MicrostructureFace]  interior point in correct octant? {interiorInCorrectOctant}");

                Vector3 interiorDisp = interiorAbs - Corners[0];
                
                _interiorDisplacements.Add(interiorDisp);
                
                Debug.Log($"[MicrostructureFace] Computed interior point from edge midpoints: " +
                        $"e01_mid={edge01_mid}, e12_mid={edge12_mid}, e20_mid={edge20_mid}, " +
                        $"interior={interiorAbs}, disp={interiorDisp}");
            }
            else if (n == 2)
            {
                // No interior points needed for degree 2
                Debug.Log($"[MicrostructureFace] Degree 2 face - no interior points");
            }
            else
            {
                Debug.LogWarning($"[MicrostructureFace] Unknown degree n={n}, using flat face");
            }

            if (transform.position != Vector3.zero)
                Debug.LogWarning($"[MicrostructureFace] {gameObject.name} — " +
                                $"transform.position is {transform.position}, not zero!");
            transform.position   = Vector3.zero;
            transform.rotation   = Quaternion.identity;
            transform.localScale = Vector3.one;

            gameObject.name = $"Face_{data.node1}-{data.node2}-{data.node3}";

            Debug.Log($"[MicrostructureFace] {gameObject.name} - Edge curves received:" +
              $"\n  edge01 ({data.node1}-{data.node2}): {(edge01Pts != null ? edge01Pts.Length + " points" : "NULL")}" +
              $"\n  edge12 ({data.node2}-{data.node3}): {(edge12Pts != null ? edge12Pts.Length + " points" : "NULL")}" +
              $"\n  edge20 ({data.node3}-{data.node1}): {(edge20Pts != null ? edge20Pts.Length + " points" : "NULL")}");
    
            
            BuildAndApplyMesh();
        }

        /// <summary>
        /// Call this to attach edge curve data for C0 boundary consistency
        /// before or after Initialise. Rebuilds the mesh.
        /// </summary>
        public void SetEdgeCurves(
            Vector3[] edge01, Vector3[] edge12, Vector3[] edge20)
        {
            _edge01Pts = edge01;
            _edge12Pts = edge12;
            _edge20Pts = edge20;
            if (Corners != null) BuildAndApplyMesh();
        }

        /// <summary>
        /// Fit interior control points to a target patch via regression.
        /// targetWorldPositions: sampled world positions on the target surface
        /// with corresponding barycentric coordinates.
        /// </summary>
        public void FitToTarget(
            List<(Vector3 bary, Vector3 worldPos)> targetSamples)
        {
            if (Corners == null || targetSamples == null || targetSamples.Count == 0)
                return;

            int n = bezierM + 1;
            _interiorDisplacements = BezierTriangle.FitInteriorDisplacements(
                n, Corners[0], Corners[1], Corners[2],
                _edge01Pts, _edge12Pts, _edge20Pts,
                targetSamples);

            BuildAndApplyMesh();
        }

        // ── Mesh building ─────────────────────────────────────────────────────

        private Mesh BuildMeshFromEdgeSampling(int subdivisions)
        {
            // Instead of using Bezier triangle, create a mesh by directly sampling
            // points along the edges and connecting them
            
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Vector3> normals = new List<Vector3>();
            
            // Create a grid of points using bilinear interpolation between edges
            for (int i = 0; i <= subdivisions; i++)
            {
                float u = i / (float)subdivisions;
                
                for (int j = 0; j <= subdivisions - i; j++)
                {
                    float v = j / (float)subdivisions;
                    float w = 1 - u - v;
                    
                    if (w < -1e-6f) continue;
                    
                    // Sample point along each edge at the appropriate parameters
                    Vector3 p_ab = Vector3.zero; // edge 0-1 at parameter v/(v+w)
                    Vector3 p_bc = Vector3.zero; // edge 1-2 at parameter w/(w+u)
                    Vector3 p_ca = Vector3.zero; // edge 2-0 at parameter u/(u+v)
                    
                    float denom_ab = v + w;
                    if (denom_ab > 1e-6f && _edge01Pts != null && _edge01Pts.Length > 0)
                        p_ab = BezierTriangle.EvalBezierArray(_edge01Pts, v / denom_ab);
                    
                    float denom_bc = w + u;
                    if (denom_bc > 1e-6f && _edge12Pts != null && _edge12Pts.Length > 0)
                        p_bc = BezierTriangle.EvalBezierArray(_edge12Pts, w / denom_bc);
                    
                    float denom_ca = u + v;
                    if (denom_ca > 1e-6f && _edge20Pts != null && _edge20Pts.Length > 0)
                        p_ca = BezierTriangle.EvalBezierArray(_edge20Pts, u / denom_ca);
                    
                    // Weighted blend of the three edge points
                    Vector3 vertex;
                    if (denom_ab > 1e-6f && denom_bc > 1e-6f && denom_ca > 1e-6f)
                    {
                        // Use area weighting
                        vertex = (p_ab * w + p_bc * u + p_ca * v) / (u + v + w + 1e-6f);
                    }
                    else
                    {
                        vertex = (p_ab + p_bc + p_ca) / 3f;
                    }
                    
                    vertices.Add(vertex);
                    
                    // Simple normal from face plane + edge influence
                    Vector3 normal = FaceNormal;
                    normals.Add(normal);
                }
            }
            
            // Build triangles
            int idx = 0;
            for (int i = 0; i < subdivisions; i++)
            {
                for (int j = 0; j < subdivisions - i; j++)
                {
                    int current = idx;
                    int right = idx + 1;
                    int down = idx + (subdivisions - i + 1);
                    int downRight = down + 1;
                    
                    triangles.Add(current);
                    triangles.Add(right);
                    triangles.Add(down);
                    
                    if (j < subdivisions - i - 1)
                    {
                        triangles.Add(right);
                        triangles.Add(downRight);
                        triangles.Add(down);
                    }
                    
                    idx++;
                }
                idx++; // Skip the extra index at the end of each row
            }
            
            Mesh mesh = new Mesh { name = "EdgeSampledFace" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetNormals(normals);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals(); // Recalculate for better smoothing
            
            return mesh;
        }

        private void BuildAndApplyMesh()
        {
            if (_mf == null || Corners == null) return;

            int n = Mathf.Max(1, bezierM + 1);
            BezierDegree = n;

            ControlPoints = BezierTriangle.BuildControlPoints(
                n, Corners[0], Corners[1], Corners[2],
                _edge01Pts, _edge12Pts, _edge20Pts,
                _interiorDisplacements.Count > 0 ? _interiorDisplacements : null);

            // Debug: Draw spheres at control points
            DebugDrawControlPoints();

            if (_mf.sharedMesh != null)
            {
                if (Application.isPlaying) Destroy(_mf.sharedMesh);
                else DestroyImmediate(_mf.sharedMesh);
            }

            _mf.mesh = BuildShellMesh(n, samplingL);
        }
        private void DebugDrawControlPoints()
        {
            if (ControlPoints == null || ControlPoints.Length == 0) return;
            
            // Create a child object to hold the debug spheres
            GameObject debugParent = new GameObject($"Debug_ControlPoints_{gameObject.name}");
            debugParent.transform.SetParent(transform);
            debugParent.transform.localPosition = Vector3.zero;
            
            // Color for different types of control points
            // Corners: Red
            // Edge points: Yellow
            // Interior points: Green
            
            int n = BezierDegree;
            var indices = BezierTriangle.GetIndices(n);
            
            for (int idx = 0; idx < ControlPoints.Length && idx < indices.Count; idx++)
            {
                var (i, j, k) = indices[idx];
                Vector3 pos = ControlPoints[idx];
                
                // Skip zero positions
                if (pos == Vector3.zero) continue;
                
                // Create sphere
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.SetParent(debugParent.transform);
                sphere.transform.position = pos;
                sphere.transform.localScale = Vector3.one * 0.03f;
                
                // Set color based on point type
                Renderer renderer = sphere.GetComponent<Renderer>();
                if (i == n || j == n || k == n) // Corner
                {
                    // renderer.material.color = Color.red;
                    // sphere.name = $"Corner_{i},{j},{k}";
                }
                else if (i == 0 || j == 0 || k == 0) // Edge point
                {
                    renderer.material.color = Color.yellow;
                    sphere.name = $"Edge_{i},{j},{k}";
                }
                else // Interior point
                {
                    renderer.material.color = Color.green;
                    sphere.name = $"Interior_{i},{j},{k}";
                    // Make interior points slightly larger
                    sphere.transform.localScale = Vector3.one * 0.05f;
                }
                
                Debug.Log($"[DebugDraw] {sphere.name} at {pos}");
            }
            
            Debug.Log($"[DebugDraw] Created {ControlPoints.Length} debug spheres for {gameObject.name}");
        }

        private Mesh BuildShellMeshFromEdgeSampling(int subdivisions)
        {
            // First get the base surface
            Mesh baseMesh = BuildMeshFromEdgeSampling(subdivisions);
            Vector3[] baseVerts = baseMesh.vertices;
            Vector3[] baseNormals = baseMesh.normals;
            
            // Create top and bottom layers
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();
            List<Vector3> norms = new List<Vector3>();
            
            int vertexCount = baseVerts.Length;
            
            // Add top vertices (offset along normal)
            for (int i = 0; i < vertexCount; i++)
            {
                verts.Add(baseVerts[i] + baseNormals[i] * _shellHalfThickness);
                norms.Add(baseNormals[i]);
            }
            
            // Add bottom vertices (offset opposite normal)
            for (int i = 0; i < vertexCount; i++)
            {
                verts.Add(baseVerts[i] - baseNormals[i] * _shellHalfThickness);
                norms.Add(-baseNormals[i]);
            }
            
            // Add top triangles (same as base mesh)
            int[] baseTris = baseMesh.triangles;
            for (int i = 0; i < baseTris.Length; i += 3)
            {
                tris.Add(baseTris[i]);
                tris.Add(baseTris[i + 1]);
                tris.Add(baseTris[i + 2]);
            }
            
            // Add bottom triangles (reversed winding)
            int bottomOffset = vertexCount;
            for (int i = 0; i < baseTris.Length; i += 3)
            {
                tris.Add(bottomOffset + baseTris[i]);
                tris.Add(bottomOffset + baseTris[i + 2]);
                tris.Add(bottomOffset + baseTris[i + 1]);
            }
            
            // Add side walls (simplified)
            Mesh shellMesh = new Mesh { name = "EdgeSampledShell" };
            shellMesh.SetVertices(verts);
            shellMesh.SetTriangles(tris, 0);
            shellMesh.SetNormals(norms);
            shellMesh.RecalculateBounds();
            
            return shellMesh;
        }

        /// Updates the corner positions and rebuilds the face mesh.
        /// This is called by GraphManager when nodes move.
        public void UpdatePositionsAndRebuild(Vector3 newP1, Vector3 newP2, Vector3 newP3)
        {
            if (Corners == null || Corners.Length < 3)
            {
                Corners = new Vector3[3];
            }
            
            Vector3 oldP1 = Corners[0];
            Vector3 oldP2 = Corners[1];
            Vector3 oldP3 = Corners[2];
            
            Corners[0] = newP1;
            Corners[1] = newP2;
            Corners[2] = newP3;
            
            // Recompute face normal
            Vector3 ab = Corners[1] - Corners[0];
            Vector3 ac = Corners[2] - Corners[0];
            FaceNormal = Vector3.Cross(ab, ac).normalized;
            if (FaceNormal == Vector3.zero) FaceNormal = Vector3.up;
            
            // Rebuild the mesh with updated corner positions
            BuildAndApplyMesh();
            
            Debug.Log($"[MicrostructureFace] {gameObject.name} updated positions: " +
                    $"({oldP1}→{newP1}), ({oldP2}→{newP2}), ({oldP3}→{newP3})");
        }

        public void ToggleHighlight()
        {
            if (_mr == null) return;

            if (IsHighlighted)
            {
                // Restore previous material
                if (_materialBeforeHighlight != null)
                    _mr.material = _materialBeforeHighlight;
                IsHighlighted = false;
                Debug.Log($"[{gameObject.name}] Highlight OFF");
            }
            else
            {
                if (highlightMaterial == null)
                {
                    Debug.LogWarning($"[{gameObject.name}] No highlight material assigned!");
                    return;
                }
                _materialBeforeHighlight = _mr.material;
                _mr.material = highlightMaterial;
                IsHighlighted = true;
                Debug.Log($"[{gameObject.name}] Highlight ON - Face ID: {Data?.node1}-{Data?.node2}-{Data?.node3}");
            }
        }

        private void Update()
        {
            // Click detection for faces
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (Microstructure.InputGuard.IsClickConsumed(Time.frameCount)) return;

                Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
                RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity);
                
                // Sort by distance
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                
                foreach (var hit in hits)
                {
                    if (hit.collider.gameObject == gameObject)
                    {
                        if (_isMirrored)
                        {
                            Debug.Log($"[{gameObject.name}] Click ignored — this is a mirrored face.");
                            return;
                        }
                        
                        Debug.Log($"=== FACE CLICKED ===");
                        Debug.Log($"Face name: {gameObject.name}");
                        Debug.Log($"Face nodes: {Data?.node1}-{Data?.node2}-{Data?.node3}");
                        Debug.Log($"Face octant: {gameObject.name.Split('_').LastOrDefault()}");
                        Debug.Log($"IsMirrored: {_isMirrored}");
                        
                        ToggleHighlight();
                        
                        if (GraphManager.Instance != null)
                            GraphManager.Instance.OnFaceClickedFromFace(this, IsHighlighted);
                        
                        Microstructure.InputGuard.ConsumeClick(Time.frameCount);
                        return;
                    }
                }
            }
        }

        private Mesh BuildShellMesh(int n, int L)
        {
            Debug.Log($"[BuildShellMesh] {gameObject.name} — ControlPoints array size: {ControlPoints?.Length ?? 0}");
            if (ControlPoints != null && ControlPoints.Length > 0)
            {
                for (int i = 0; i < Mathf.Min(ControlPoints.Length, 10); i++)
                {
                    Debug.Log($"[BuildShellMesh] ControlPoint[{i}] = {ControlPoints[i]}");
                }
            }
            BezierTriangle.SampleGrid(ControlPoints, n, L,
                out Vector3[] midPositions, out Vector3[] midNormals);

            int ptCount = midPositions.Length;

            for (int i = 0; i < Mathf.Min(ptCount, 5); i++)
            {
                Debug.Log($"[BuildShellMesh] Sample[{i}]: pos={midPositions[i]}, normal={midNormals[i]}");
            }

            // Fix normals to ensure they're properly normalized
            for (int i = 0; i < ptCount; i++)
            {
                if (midNormals[i].sqrMagnitude < 0.5f || float.IsNaN(midNormals[i].x))
                {
                    // Average with flat face normal
                    Vector3 ab = Corners[1] - Corners[0];
                    Vector3 ac = Corners[2] - Corners[0];
                    Vector3 faceN = Vector3.Cross(ab, ac).normalized;
                    midNormals[i] = faceN;
                }
                else
                {
                    // Ensure normal is normalized
                    midNormals[i] = midNormals[i].normalized;
                }
            }

            // Cache the grid once
            var baryGrid = BezierTriangle.SampleBarycentricGrid(L);

            var topPos = new Vector3[ptCount];
            var botPos = new Vector3[ptCount];
            
            for (int i = 0; i < ptCount; i++)
            {
                Vector3 wp = midPositions[i];
                Vector3 n3 = midNormals[i];

                // Guard: if normal is degenerate use face normal
                if (n3.sqrMagnitude < 0.01f || float.IsNaN(n3.x))
                {
                    Vector3 ab = Corners[1] - Corners[0];
                    Vector3 ac = Corners[2] - Corners[0];
                    n3 = Vector3.Cross(ab, ac).normalized;
                }

                // Use a much smaller thickness or adaptive thickness to prevent spikes
                // The spikes often happen at boundaries where normals flip
                float thickness = _shellHalfThickness;
                
                // Reduce thickness near edges to prevent spikes
                Vector3 bary = baryGrid[i];
                float edgeFactor = Mathf.Min(bary.x, bary.y, bary.z);
                if (edgeFactor < 0.1f)
                {
                    // Near edge - reduce thickness smoothly
                    thickness *= Mathf.Pow(edgeFactor / 0.1f, 0.5f);
                }

                topPos[i] = wp + n3 * thickness;
                botPos[i] = wp - n3 * thickness;
            }

            var gridIndex = BuildGridIndexMap(L);
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs   = new List<Vector2>();
            var tris  = new List<int>();

            int topBase = 0;
            for (int i = 0; i < ptCount; i++)
            {
                verts.Add(topPos[i]);
                norms.Add(midNormals[i]);
                uvs.Add(new Vector2(baryGrid[i].x, baryGrid[i].y));
            }

            int botBase = ptCount;
            for (int i = 0; i < ptCount; i++)
            {
                verts.Add(botPos[i]);
                norms.Add(-midNormals[i]);
                uvs.Add(new Vector2(baryGrid[i].x, baryGrid[i].y));
            }

            // Top triangles
            for (int i = 0; i <= L; i++)
                for (int j = 0; j <= L - i - 1; j++)
                {
                    int a = gridIndex[i,     j    ];
                    int b = gridIndex[i + 1, j    ];
                    int c = gridIndex[i,     j + 1];
                    int d = gridIndex[i + 1, j + 1];
                    tris.Add(topBase + a); tris.Add(topBase + b); tris.Add(topBase + c);
                    if (d >= 0)
                    {
                        tris.Add(topBase + b); tris.Add(topBase + d); tris.Add(topBase + c);
                    }
                }

            // Bottom triangles (reversed winding)
            for (int i = 0; i <= L; i++)
                for (int j = 0; j <= L - i - 1; j++)
                {
                    int a = gridIndex[i,     j    ];
                    int b = gridIndex[i + 1, j    ];
                    int c = gridIndex[i,     j + 1];
                    int d = gridIndex[i + 1, j + 1];
                    tris.Add(botBase + a); tris.Add(botBase + c); tris.Add(botBase + b);
                    if (d >= 0)
                    {
                        tris.Add(botBase + b); tris.Add(botBase + c); tris.Add(botBase + d);
                    }
                }

            AddSideWalls(L, gridIndex, topBase, botBase,
                        verts, norms, uvs, tris, topPos, botPos, midNormals);

            var mesh = new Mesh { name = "BezierFace" };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            
            // Add collider for raycasting
            if (_mc == null)
                _mc = gameObject.AddComponent<MeshCollider>();
            _mc.sharedMesh = mesh;
            
            return mesh;
        }

        /// <summary>
        /// Builds a 2D index map from barycentric grid (i,j) → flat index.
        /// -1 means the point is outside the triangle (i+j > L).
        /// </summary>
        private int[,] BuildGridIndexMap(int L)
        {
            var map  = new int[L + 2, L + 2];
            for (int i = 0; i <= L + 1; i++)
                for (int j = 0; j <= L + 1; j++)
                    map[i, j] = -1;

            int idx = 0;
            for (int i = 0; i <= L; i++)
                for (int j = 0; j <= L - i; j++)
                    map[i, j] = idx++;

            return map;
        }

        private void AddSideWalls(
            int L, int[,] gridIndex,
            int topBase, int botBase,
            List<Vector3> verts, List<Vector3> norms,
            List<Vector2> uvs,   List<int> tris,
            Vector3[] topPos, Vector3[] botPos, Vector3[] midNormals)
        {
            // Collect boundary edges in order: 3 sides of the triangle
            var boundaryEdges = new List<(int a, int b)>();

            // Edge 0: i=0, j=0..L  (s=0 side, u varies)
            for (int j = 0; j < L; j++)
                boundaryEdges.Add((gridIndex[0, j], gridIndex[0, j + 1]));

            // Edge 1: j=0, i=0..L  (t=0 side, s varies)
            for (int i = 0; i < L; i++)
                boundaryEdges.Add((gridIndex[i, 0], gridIndex[i + 1, 0]));

            // Edge 2: i+j=L        (u=0 side)
            for (int i = 0; i < L; i++)
            {
                int j  = L - i;
                int j2 = L - (i + 1);
                if (j >= 0 && j2 >= 0)
                    boundaryEdges.Add((gridIndex[i, j], gridIndex[i + 1, j2]));
            }

            foreach (var (a, b) in boundaryEdges)
            {
                if (a < 0 || b < 0) continue;

                int vBase = verts.Count;

                // Four vertices of the side quad
                verts.Add(topPos[a]); verts.Add(topPos[b]);
                verts.Add(botPos[a]); verts.Add(botPos[b]);

                // Side normal: average of the two edge normals, projected outward
                Vector3 sideN = -(midNormals[a] + midNormals[b]).normalized;
                norms.Add(sideN); norms.Add(sideN);
                norms.Add(sideN); norms.Add(sideN);

                uvs.Add(Vector2.zero); uvs.Add(Vector2.right);
                uvs.Add(Vector2.up);   uvs.Add(Vector2.one);

                tris.Add(vBase);     tris.Add(vBase + 1); tris.Add(vBase + 2);
                tris.Add(vBase + 1); tris.Add(vBase + 3); tris.Add(vBase + 2);
            }
        }

        void OnDestroy()
        {
            if (_mf != null && _mf.sharedMesh != null)
            {
                if (Application.isPlaying) Destroy(_mf.sharedMesh);
                else DestroyImmediate(_mf.sharedMesh);
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (Corners == null || Corners.Length < 3) return;
            Gizmos.color = Color.green;
            Gizmos.DrawLine(Corners[0], Corners[1]);
            Gizmos.DrawLine(Corners[1], Corners[2]);
            Gizmos.DrawLine(Corners[2], Corners[0]);
            if (ControlPoints != null)
            {
                Gizmos.color = Color.yellow;
                foreach (var cp in ControlPoints)
                    Gizmos.DrawSphere(cp, 0.02f);
            }
            Vector3 centroid = (Corners[0] + Corners[1] + Corners[2]) / 3f;
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(centroid, FaceNormal * 0.15f);
        }
#endif
    }
}





// BEFORE CURVED FACES IMPLEMENTATION
// // MicrostructureFace.cs
// // TODO: Implement simplified representation of faces in a microstructure as triangular prisms. 
// // Each face is defined by three vertices and a normal vector, which determines the orientation of the face in 3D space.
// // This class is used to construct a triangular prism mesh in Unity where two edges of the prism are the edges of the microstructure, and the final vertex is in the center of the face
// // Make sure the height of the triangular prism is the diameter of the edge of the microstructure to ensure that the prisms are properly sized and do not overlap with each other.

// // MicrostructureFace.cs
// // Represents a microstructure face as a triangular prism.
// //
// // Geometry:
// //   - The triangle base is defined by the three corner vertices (positions[0..2]).
// //   - The face normal is computed from the cross product of the triangle edges.
// //   - The prism is extruded symmetrically along the face normal by a height
// //     equal to the edge diameter (edge_thickness * 2), so the prism is centred
// //     on the triangle plane and adjacent prisms do not overlap edge tubes.
// //   - The mesh is double-sided so the face is visible from both directions.

// using System.Collections.Generic;
// using UnityEngine;

// namespace Microstructure
// {
//     [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
//     public class MicrostructureFace : MonoBehaviour
//     {
//         [Header("Appearance")]
//         public Material material;

//         // ── Public state ─────────────────────────────────────────────────────

//         /// <summary>Raw JSON record.</summary>
//         public FaceData Data { get; private set; }

//         /// <summary>The three world-space corner positions of this face.</summary>
//         public Vector3[] Corners { get; private set; }

//         /// <summary>Unit normal computed from the triangle winding.</summary>
//         public Vector3 FaceNormal { get; private set; }
//         private bool _isMirrored = false;
//         public bool IsMirrored => _isMirrored;
//         public void SetMirrored(bool mirrored) => _isMirrored = mirrored;

//         // ── Private ──────────────────────────────────────────────────────────

//         MeshFilter   _mf;
//         MeshRenderer _mr;

//         // ── Initialise ───────────────────────────────────────────────────────

//         /// <summary>
//         /// Called by MicrostructureLoader after instantiation.
//         ///
//         /// <paramref name="positions"/> — the three world-space corner vertices
//         /// already reflected and scaled by worldScale.
//         ///
//         /// <paramref name="controlPoints"/> — Bezier surface control points,
//         /// unused here because faces are rendered as triangular prisms.
//         ///
//         /// <paramref name="worldScale"/> — used to derive the prism height:
//         ///     height = edge_thickness_canonical * 2 * worldScale
//         /// where edge_thickness_canonical defaults to 0.03 (the standard export
//         /// value).  This makes the prism height exactly equal to the edge tube
//         /// diameter, so prisms and tubes are flush with each other.
//         /// </summary>
//         public void Initialise(FaceData data, float worldScale,
//                                List<Vector3> positions, List<Vector3> controlPoints)
//         {
//             Debug.Log($"[MicrostructureFace] === INITIALISE START ===");
//             Debug.Log($"[MicrostructureFace] GameObject name: {gameObject.name}");
//             Debug.Log($"[MicrostructureFace] Data nodes: ({data.node1}, {data.node2}, {data.node3})");
//             Debug.Log($"[MicrostructureFace] positions count: {positions?.Count ?? -1}");
//             Debug.Log($"[MicrostructureFace] worldScale: {worldScale}");
            
//             Data = data;

//             _mf = GetComponent<MeshFilter>();
//             _mr = GetComponent<MeshRenderer>();
            
//             Debug.Log($"[MicrostructureFace] MeshFilter: {(_mf != null ? "OK" : "NULL")}");
//             Debug.Log($"[MicrostructureFace] MeshRenderer: {(_mr != null ? "OK" : "NULL")}");

//             // ── Material ─────────────────────────────────────────────────────
//             if (material == null)
//             {
//                 Debug.LogWarning($"[MicrostructureFace] {gameObject.name}: " +
//                                  "no material assigned — mesh will be invisible. " +
//                                  "Assign a material to the 'Material' field on the Face prefab.");
                
//                 // Try to create a default material so we can see something
//                 material = new Material(Shader.Find("Standard"));
//                 material.color = new Color(0.5f, 0.7f, 0.3f, 0.7f); // Semi-transparent green
//                 Debug.Log($"[MicrostructureFace] Created default material with color {material.color}");
//             }
            
//             if (_mr != null)
//             {
//                 _mr.material = material;
//                 Debug.Log($"[MicrostructureFace] Material assigned to MeshRenderer: {material.name}");
                
//                 // Enable shadows to make it more visible
//                 _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
//                 _mr.receiveShadows = true;
//             }
//             else
//             {
//                 Debug.LogError($"[MicrostructureFace] {gameObject.name}: MeshRenderer is null!");
//             }

//             if (_mf == null)
//             {
//                 Debug.LogError($"[MicrostructureFace] {gameObject.name}: MeshFilter is null!");
//                 Debug.Log($"[MicrostructureFace] Adding MeshFilter component...");
//                 _mf = gameObject.AddComponent<MeshFilter>();
//                 Debug.Log($"[MicrostructureFace] MeshFilter added: {(_mf != null ? "SUCCESS" : "FAILED")}");
//             }

//             // ── Positions check ───────────────────────────────────────────────
//             if (positions == null || positions.Count < 3)
//             {
//                 Debug.LogError($"[MicrostructureFace] {gameObject.name}: " +
//                                $"fewer than 3 positions ({positions?.Count ?? -1}), skipping mesh.");
//                 return;
//             }

//             Corners = new[] { positions[0], positions[1], positions[2] };
//             Debug.Log($"[MicrostructureFace] Corners: " +
//                       $"[0]={Corners[0]:F3}  [1]={Corners[1]:F3}  [2]={Corners[2]:F3}");
            
//             // Log distance between corners to check if they're valid
//             float dist01 = Vector3.Distance(Corners[0], Corners[1]);
//             float dist12 = Vector3.Distance(Corners[1], Corners[2]);
//             float dist20 = Vector3.Distance(Corners[2], Corners[0]);
//             Debug.Log($"[MicrostructureFace] Edge lengths: 0-1={dist01:F3}, 1-2={dist12:F3}, 2-0={dist20:F3}");

//             // ── Normal ────────────────────────────────────────────────────────
//             Vector3 ab = Corners[1] - Corners[0];
//             Vector3 ac = Corners[2] - Corners[0];
//             FaceNormal = Vector3.Cross(ab, ac).normalized;

//             float triangleArea = Vector3.Cross(ab, ac).magnitude * 0.5f;
//             Debug.Log($"[MicrostructureFace] ab={ab:F3}, ac={ac:F3}");
//             Debug.Log($"[MicrostructureFace] Cross product magnitude={Vector3.Cross(ab, ac).magnitude:F6}");
//             Debug.Log($"[MicrostructureFace] FaceNormal={FaceNormal:F3}  " +
//                       $"triangleArea={triangleArea:F6}");

//             if (triangleArea < 1e-6f)
//             {
//                 Debug.LogError($"[MicrostructureFace] {gameObject.name}: " +
//                                $"degenerate triangle (area={triangleArea}) — corners may be collinear.");
//                 return;
//             }

//             if (FaceNormal == Vector3.zero)
//             {
//                 Debug.LogError($"[MicrostructureFace] {gameObject.name}: " +
//                                "FaceNormal is zero — cannot build prism. Skipping.");
//                 return;
//             }

//             // ── Prism height ──────────────────────────────────────────────────
//             float edgeThicknessCanonical = 0.03f;
//             float prismHeight = 2f * edgeThicknessCanonical * worldScale;
//             Debug.Log($"[MicrostructureFace] prismHeight={prismHeight:F4}  " +
//                       $"(2 * {edgeThicknessCanonical} * worldScale={worldScale})");

//             transform.position   = Vector3.zero;
//             transform.rotation   = Quaternion.identity;
//             transform.localScale = Vector3.one;

//             gameObject.name = $"Face_{data.node1}-{data.node2}-{data.node3}";
//             Debug.Log($"[MicrostructureFace] GameObject renamed to: {gameObject.name}");

//             // ── Build mesh ────────────────────────────────────────────────────
//             if (_mf != null)
//             {
//                 Mesh m = BuildPrismMesh(prismHeight);
//                 if (m != null)
//                 {
//                     _mf.mesh = m;
//                     Debug.Log($"[MicrostructureFace] Mesh built successfully — " +
//                               $"vertices={m.vertexCount}  triangles={m.triangles.Length / 3}  " +
//                               $"bounds={m.bounds}");
                    
//                     // Enable the MeshRenderer
//                     if (_mr != null)
//                     {
//                         _mr.enabled = true;
//                         Debug.Log($"[MicrostructureFace] MeshRenderer enabled and material: {(_mr.sharedMaterial != null ? _mr.sharedMaterial.name : "NULL")}");
//                     }
                    
//                     // Log mesh bounds to see if it's positioned correctly
//                     Debug.Log($"[MicrostructureFace] Mesh bounds center: {m.bounds.center}");
//                     Debug.Log($"[MicrostructureFace] Mesh bounds size: {m.bounds.size}");
//                 }
//                 else
//                 {
//                     Debug.LogError($"[MicrostructureFace] BuildPrismMesh returned null!");
//                 }
//             }
//             else
//             {
//                 Debug.LogError($"[MicrostructureFace] {gameObject.name}: " +
//                                "MeshFilter is null — mesh cannot be assigned.");
//             }
            
//             Debug.Log($"[MicrostructureFace] === INITIALISE END ===");
//         }

//         // ── Mesh construction ─────────────────────────────────────────────────

//         /// <summary>
//         /// Builds a triangular prism centred on the face triangle.
//         ///
//         ///   Bottom triangle : Corners[i] - FaceNormal * height/2
//         ///   Top    triangle : Corners[i] + FaceNormal * height/2
//         ///
//         /// Faces: 2 triangular caps + 3 rectangular sides, all double-sided.
//         /// </summary>
//         Mesh BuildPrismMesh(float height)
//         {
//             Debug.Log($"[MicrostructureFace] BuildPrismMesh — height={height:F4}  " +
//                       $"normal={FaceNormal:F3}");
            
//             if (Corners == null || Corners.Length < 3)
//             {
//                 Debug.LogError($"[MicrostructureFace] BuildPrismMesh: Corners array is invalid!");
//                 return null;
//             }

//             // Extrude half each way so the prism is centred on the triangle plane.
//             Vector3 halfOffset = FaceNormal * (height * 0.5f);
//             Debug.Log($"[MicrostructureFace] halfOffset={halfOffset:F4}");

//             Vector3 b0 = Corners[0] - halfOffset;  // bottom triangle
//             Vector3 b1 = Corners[1] - halfOffset;
//             Vector3 b2 = Corners[2] - halfOffset;
//             Vector3 t0 = Corners[0] + halfOffset;  // top triangle
//             Vector3 t1 = Corners[1] + halfOffset;
//             Vector3 t2 = Corners[2] + halfOffset;
            
//             Debug.Log($"[MicrostructureFace] Bottom vertices: b0={b0:F3}, b1={b1:F3}, b2={b2:F3}");
//             Debug.Log($"[MicrostructureFace] Top vertices: t0={t0:F3}, t1={t1:F3}, t2={t2:F3}");

//             var verts = new List<Vector3>();
//             var norms = new List<Vector3>();
//             var uvs   = new List<Vector2>();
//             var tris  = new List<int>();

//             // Add a triangle with a given outward normal.
//             void AddTri(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 n)
//             {
//                 int i = verts.Count;
//                 verts.Add(v0); verts.Add(v1); verts.Add(v2);
//                 norms.Add(n);  norms.Add(n);  norms.Add(n);
//                 uvs.Add(Vector2.zero); uvs.Add(Vector2.right); uvs.Add(Vector2.up);
//                 tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
//             }

//             // Add a quad (v0-v1 = first edge, v2-v3 = parallel second edge).
//             void AddQuad(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Vector3 n)
//             {
//                 int i = verts.Count;
//                 verts.Add(v0); verts.Add(v1); verts.Add(v2); verts.Add(v3);
//                 norms.Add(n);  norms.Add(n);  norms.Add(n);  norms.Add(n);
//                 uvs.Add(new Vector2(0,0)); uvs.Add(new Vector2(1,0));
//                 uvs.Add(new Vector2(0,1)); uvs.Add(new Vector2(1,1));
//                 // Two triangles: (0,1,2) and (1,3,2)
//                 tris.Add(i);     tris.Add(i + 1); tris.Add(i + 2);
//                 tris.Add(i + 1); tris.Add(i + 3); tris.Add(i + 2);
//             }

//             // ── Bottom cap faces -FaceNormal ─────────────────────────────────
//             Debug.Log($"[MicrostructureFace] Adding bottom cap");
//             AddTri(b0, b2, b1, -FaceNormal);

//             // ── Top cap faces +FaceNormal ────────────────────────────────────
//             Debug.Log($"[MicrostructureFace] Adding top cap");
//             AddTri(t0, t1, t2, FaceNormal);

//             // ── Three rectangular sides ──────────────────────────────────────
//             // Side normals point outward (away from triangle centroid).
//             // Cross(FaceNormal, edge_direction) gives the outward side normal.
//             Debug.Log($"[MicrostructureFace] Adding side 1");
//             AddQuad(b0, b1, t0, t1, Vector3.Cross(FaceNormal, b1 - b0).normalized);
            
//             Debug.Log($"[MicrostructureFace] Adding side 2");
//             AddQuad(b1, b2, t1, t2, Vector3.Cross(FaceNormal, b2 - b1).normalized);
            
//             Debug.Log($"[MicrostructureFace] Adding side 3");
//             AddQuad(b2, b0, t2, t0, Vector3.Cross(FaceNormal, b0 - b2).normalized);

//             Debug.Log($"[MicrostructureFace] Initial geometry: vertices={verts.Count}, triangles={tris.Count/3}");

//             // ── Double-sided: duplicate all geometry with flipped winding ────
//             int singleCount = tris.Count;
//             int vertBase    = verts.Count;

//             for (int i = 0; i < vertBase; i++)
//             {
//                 verts.Add(verts[i]);
//                 norms.Add(-norms[i]);   // flip normal for back face
//                 uvs.Add(uvs[i]);
//             }

//             for (int i = 0; i < singleCount; i += 3)
//             {
//                 // Reverse winding: swap second and third index
//                 tris.Add(vertBase + tris[i]);
//                 tris.Add(vertBase + tris[i + 2]);
//                 tris.Add(vertBase + tris[i + 1]);
//             }

//             Debug.Log($"[MicrostructureFace] Final geometry: vertices={verts.Count}, triangles={tris.Count/3}");

//             var mesh = new Mesh { name = "FacePrism" };
//             mesh.SetVertices(verts);
//             mesh.SetNormals(norms);
//             mesh.SetUVs(0, uvs);
//             mesh.SetTriangles(tris, 0);
//             mesh.RecalculateBounds();
            
//             Debug.Log($"[MicrostructureFace] Mesh created with bounds: center={mesh.bounds.center}, size={mesh.bounds.size}");
            
//             return mesh;
//         }

//         // ── Cleanup ───────────────────────────────────────────────────────────

//         void OnDestroy()
//         {
//             if (_mf != null && _mf.sharedMesh != null)
//             {
//                 if (Application.isPlaying) Destroy(_mf.sharedMesh);
//                 else                        DestroyImmediate(_mf.sharedMesh);
//             }
//         }
        
//         void Start()
//         {
//             Debug.Log($"[MicrostructureFace] Start() called on {gameObject.name} - MeshRenderer enabled: {(_mr != null ? _mr.enabled : false)}");
//         }
        
//         void Update()
//         {
//             // Optional: Log once to verify the object exists and is visible
//             if (Time.frameCount % 300 == 0) // Log every 5 seconds at 60fps
//             {
//                 Debug.Log($"[MicrostructureFace] {gameObject.name} still alive - Position: {transform.position}, Visible: {(_mr != null ? _mr.enabled : false)}");
//             }
//         }

// #if UNITY_EDITOR
//         void OnDrawGizmosSelected()
//         {
//             if (Corners == null || Corners.Length < 3) return;
//             Gizmos.color = Color.green;
//             Gizmos.DrawLine(Corners[0], Corners[1]);
//             Gizmos.DrawLine(Corners[1], Corners[2]);
//             Gizmos.DrawLine(Corners[2], Corners[0]);
//             // Draw normal arrow from centroid
//             Vector3 centroid = (Corners[0] + Corners[1] + Corners[2]) / 3f;
//             Gizmos.color = Color.blue;
//             Gizmos.DrawRay(centroid, FaceNormal * 0.15f);
//         }
// #endif
//     }
// }