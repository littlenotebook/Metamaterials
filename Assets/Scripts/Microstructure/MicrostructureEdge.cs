using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Microstructure
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class MicrostructureEdge : MonoBehaviour
    {
        [Header("Appearance")]
        [SerializeField] private float thickness = 0.03f;
        [Range(4, 24)] [SerializeField] private int radialSegments = 8;
        [Range(1, 32)]  [SerializeField] private int curveSegments  = 16;
        [SerializeField] private Material material;
        [SerializeField] private Material highlightMaterial;

        private List<Vector3> _storedDisplacements = new List<Vector3>();
        public List<Vector3> StoredDisplacements => _storedDisplacements; 
        private int _bezierM = 0;

        // Store direct references - these are set at creation and should remain valid
        private MicrostructureNode _sourceNode;
        private MicrostructureNode _targetNode;
        private Vector3 _lastSourcePos;
        private Vector3 _lastTargetPos;
        private bool _isMirrored = false;
        private int _myOctant = 0; // Which octant this edge belongs to

        [Header("Control Point Visualization")]
        [SerializeField] private bool showControlPoints = true;
        [SerializeField] private float controlPointRadius = 0.08f;
        [SerializeField] private Color controlPointColor = Color.magenta;

        // Store control point spheres for cleanup
        private List<GameObject> _controlPointSpheres = new List<GameObject>();

        // Control point dragging
        private bool _isDraggingControlPoint = false;
        private int _draggingControlPointIndex = -1;
        private Vector3 _dragStartMouseWorld;
        private Vector3 _dragStartControlPointPos;
        private GameObject _highlightedControlPoint;
        private Material _originalControlPointMaterial;

        public float Thickness
        {
            get => thickness;
            set { thickness = value; RebuildMesh(); }
        }

        public int RadialSegments
        {
            get => radialSegments;
            set { radialSegments = Mathf.Clamp(value, 4, 24); RebuildMesh(); }
        }

        public int CurveSegments
        {
            get => curveSegments;
            set { curveSegments = Mathf.Clamp(value, 1, 32); RebuildMesh(); }
        }

        public EdgeData Data { get; private set; }
        public Vector3[] BezierPts { get; private set; }
        public bool IsBoundary => Data?.mirror_axes != null && Data.mirror_axes.Count > 0;

        public bool IsHighlighted { get; private set; } = false;
        private Material _materialBeforeHighlight;

        private MeshFilter   _mf;
        private MeshRenderer _mr;
        private MeshCollider _mc;
        private bool         _needsRebuild;

        public bool IsMirrored => _isMirrored;
        public void SetMirrored(bool mirrored) => _isMirrored = mirrored;
        public void SetOctant(int octant) => _myOctant = octant;
        public int GetOctant() => _myOctant;

        public void SetNodes(MicrostructureNode source, MicrostructureNode target)
        {
            _sourceNode = source;
            _targetNode = target;
            _lastSourcePos = source.transform.position;
            _lastTargetPos = target.transform.position;
            
            Debug.Log($"[{gameObject.name}] SetNodes: source={source.gameObject.name}({source.Data?.node_id}), target={target.gameObject.name}({target.Data?.node_id}), octant={_myOctant}");
        }

        public void SetData(EdgeData data) 
        { 
            Data = data; 
        }

        public void SetStoredDisplacements(List<Vector3> displacements)
        {
            _storedDisplacements = new List<Vector3>(displacements);
            Debug.Log($"[{gameObject.name}] Set stored displacements: {_storedDisplacements.Count} displacements");
        }

        // Update the node references (used when mirror nodes are created)
        public void UpdateNodeReferences(MicrostructureNode newSource, MicrostructureNode newTarget)
        {
            _sourceNode = newSource;
            _targetNode = newTarget;
            _lastSourcePos = newSource.transform.position;
            _lastTargetPos = newTarget.transform.position;
            
            // Also update the Data IDs to match the new nodes
            if (Data != null)
            {
                Data.node1 = newSource.Data.node_id;
                Data.node2 = newTarget.Data.node_id;
            }
            
            Debug.Log($"[{gameObject.name}] Updated node references: source={newSource.gameObject.name}({newSource.Data?.node_id}), target={newTarget.gameObject.name}({newTarget.Data?.node_id})");
        }

        public void InitialiseWithFit(
            EdgeData data, float scaledThickness,
            Vector3 endpoint0, Vector3 endpoint1,
            List<Vector3> targetPolyline, int M)
        {
            _bezierM = M;
            _storedDisplacements = BezierFitter.FitDisplacements(
                targetPolyline, endpoint0, endpoint1, M);

            var worldPts = BezierFitter.DisplacementsToWorldPoints(
                _storedDisplacements, endpoint0, endpoint1);

            var controlPtsOnly = worldPts.Count > 2
                ? worldPts.GetRange(1, worldPts.Count - 2)
                : new List<Vector3>();

            Initialise(data, scaledThickness, endpoint0, endpoint1, controlPtsOnly);
        }

        public void RebuildFromDisplacements(Vector3 endpoint0, Vector3 endpoint1)
        {
            Debug.Log($"[{gameObject.name}] RebuildFromDisplacements: endpoint0={endpoint0}, endpoint1={endpoint1}, displacements={_storedDisplacements.Count}");
            if (_storedDisplacements == null || _storedDisplacements.Count == 0)
            {
                // Straight edge
                BezierPts = new[] { endpoint0, endpoint1 };
            }
            else
            {
                // Build world points from displacements
                var pts = BezierFitter.DisplacementsToWorldPoints(
                    _storedDisplacements, endpoint0, endpoint1);
                BezierPts = pts.ToArray();
                Debug.Log($"[{gameObject.name}] Created {BezierPts.Length} Bezier points from displacements");
            }
            RebuildMesh();
        }

        /// <summary>
        /// Creates a copy of this edge with mirrored displacements for another octant
        /// </summary>
        public MicrostructureEdge CreateMirroredCopy(int targetOctant, MicrostructureNode mirroredSource, MicrostructureNode mirroredTarget)
        {
            if (OctantMirrorSystem.Instance == null) return null;
            
            // Get the mirror signs for source and target octants
            var activeOctant = OctantMirrorSystem.Instance.ActiveOctant;
            var targetOctantDef = OctantMirrorSystem.Instance.Octants[targetOctant];
            
            // Mirror the displacements
            List<Vector3> mirroredDisplacements = new List<Vector3>();
            foreach (var disp in _storedDisplacements)
            {
                Vector3 mirroredDisp = new Vector3(
                    disp.x * (targetOctantDef.mirrorSigns.x / activeOctant.mirrorSigns.x),
                    disp.y * (targetOctantDef.mirrorSigns.y / activeOctant.mirrorSigns.y),
                    disp.z * (targetOctantDef.mirrorSigns.z / activeOctant.mirrorSigns.z)
                );
                mirroredDisplacements.Add(mirroredDisp);
            }
            
            // Create the mirrored edge (you'll need to instantiate and set up)
            // This should be called from GraphManager.SpawnMirroredEdges
            return null; // Placeholder - actual implementation in GraphManager
        }

        private void OnEnable() => GetReferences();

        private void Update()
        {
            if (_needsRebuild)
            {
                _needsRebuild = false;
                RebuildMesh();
            }

            if (!Application.isPlaying) return;

            // Handle control point dragging for non-mirrored edges
            if (!_isMirrored && showControlPoints)
            {
                HandleControlPointDrag();
            }

            // Rebuild edge if either node has moved
            if (_sourceNode != null && _targetNode != null)
            {
                Vector3 currentSourcePos = _sourceNode.transform.position;
                Vector3 currentTargetPos = _targetNode.transform.position;
                
                bool sourceMoved = currentSourcePos != _lastSourcePos;
                bool targetMoved = currentTargetPos != _lastTargetPos;

                if (sourceMoved || targetMoved)
                {
                    _lastSourcePos = currentSourcePos;
                    _lastTargetPos = currentTargetPos;

                    if (_storedDisplacements.Count > 0)
                    {
                        RebuildFromDisplacements(currentSourcePos, currentTargetPos);
                    }
                    else if (BezierPts != null && BezierPts.Length >= 2)
                    {
                        BezierPts[0] = currentSourcePos;
                        BezierPts[BezierPts.Length - 1] = currentTargetPos;
                        RebuildMesh();
                    }
                    
                    UpdateControlPointVisualization();
                }
            }

            // Click detection for edges (existing code)
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
            if (Microstructure.InputGuard.IsClickConsumed(Time.frameCount)) return;

            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit) && hit.collider.gameObject == gameObject)
            {
                if (_isMirrored)
                {
                    return;
                }
                ToggleHighlight();
                if (GraphManager.Instance != null)
                    GraphManager.Instance.OnEdgeClickedFromEdge(this, IsHighlighted);
            }
        }

        private void HandleControlPointDrag()
        {
            if (Camera.main == null || Mouse.current == null) return;
            
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            
            // Mouse DOWN - check for control point click
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                // Raycast to find control point spheres
                RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity);
                foreach (var hit in hits)
                {
                    var cpData = hit.collider.GetComponent<ControlPointData>();
                    if (cpData != null && cpData.edge == this)
                    {
                        // Clicked on a control point
                        _isDraggingControlPoint = true;
                        _draggingControlPointIndex = cpData.controlPointIndex;
                        _dragStartControlPointPos = BezierPts[_draggingControlPointIndex];
                        
                        // Get drag start point on plane
                        Vector3 axisDir = Vector3.zero;
                        GetDragWorldPoint(ray, axisDir, _dragStartControlPointPos, out _dragStartMouseWorld);
                        
                        // Highlight the selected control point
                        HighlightControlPoint(cpData.gameObject);
                        
                        InputGuard.ConsumeClick(Time.frameCount);
                        break;
                    }
                }
            }
            
            // Mouse DRAG - move control point
            if (_isDraggingControlPoint && Mouse.current.leftButton.isPressed)
            {
                // Get current world point on the plane
                Vector3 currentWorld;
                if (GetDragWorldPointForControlPoint(ray, _dragStartControlPointPos, out currentWorld))
                {
                    Vector3 delta = currentWorld - _dragStartMouseWorld;
                    Vector3 newPos = _dragStartControlPointPos + delta;
                    
                    // Use the mirroring method instead of direct update
                    UpdateControlPointPosition(_draggingControlPointIndex, newPos);
                    
                    // Update drag start for smooth movement
                    _dragStartControlPointPos = newPos;
                    GetDragWorldPointForControlPoint(ray, newPos, out _dragStartMouseWorld);
                    
                    // Update visualization
                    UpdateControlPointVisualization();
                }
            }
            
            // Mouse UP - stop dragging
            if (Mouse.current.leftButton.wasReleasedThisFrame && _isDraggingControlPoint)
            {
                _isDraggingControlPoint = false;
                _draggingControlPointIndex = -1;
                if (_highlightedControlPoint != null)
                {
                    RestoreControlPointMaterial(_highlightedControlPoint);
                    _highlightedControlPoint = null;
                }
            }
        }

        private bool GetDragWorldPointForControlPoint(Ray ray, Vector3 planePoint, out Vector3 worldPoint)
        {
            worldPoint = Vector3.zero;
            // Use a plane that faces the camera
            Vector3 planeNormal = Camera.main.transform.forward;
            Plane plane = new Plane(planeNormal, planePoint);
            if (!plane.Raycast(ray, out float dist)) return false;
            worldPoint = ray.GetPoint(dist);
            return true;
        }

        private void UpdateStoredDisplacementsFromBezierPoints()
        {
            if (_sourceNode == null || BezierPts == null || BezierPts.Length <= 2) return;
            
            // Convert world control points to displacements from source
            Vector3 sourcePos = BezierPts[0];
            List<Vector3> worldControlPoints = new List<Vector3>();
            for (int i = 1; i < BezierPts.Length - 1; i++)
            {
                worldControlPoints.Add(BezierPts[i]);
            }
            
            if (worldControlPoints.Count > 0)
            {
                Vector3 endPos = BezierPts[BezierPts.Length - 1];
                _storedDisplacements = BezierFitter.FitDisplacements(
                    worldControlPoints, sourcePos, endPos, worldControlPoints.Count);
            }
        }

        private void HighlightControlPoint(GameObject controlPoint)
        {
            _highlightedControlPoint = controlPoint;
            var renderer = controlPoint.GetComponent<Renderer>();
            if (renderer != null)
            {
                _originalControlPointMaterial = renderer.material;
                Material highlightMat = new Material(Shader.Find("Standard"));
                highlightMat.color = Color.yellow;
                highlightMat.EnableKeyword("_EMISSION");
                highlightMat.SetColor("_EmissionColor", Color.yellow);
                renderer.material = highlightMat;
            }
        }

        private void RestoreControlPointMaterial(GameObject controlPoint)
        {
            var renderer = controlPoint.GetComponent<Renderer>();
            if (renderer != null && _originalControlPointMaterial != null)
            {
                renderer.material = _originalControlPointMaterial;
            }
        }

        // public void UpdateControlPointVisualization()
        // {
        //     // Only show for non-mirrored edges (original edges in active octant)
        //     if (!showControlPoints || _isMirrored)
        //     {
        //         ClearControlPointVisualization();
        //         return;
        //     }
            
        //     // Check if this edge is in the active octant
        //     if (OctantMirrorSystem.Instance != null)
        //     {
        //         int activeOctant = OctantMirrorSystem.Instance.activeOctantIndex;
        //         if (_myOctant != activeOctant)
        //         {
        //             ClearControlPointVisualization();
        //             return;
        //         }
        //     }
            
        //     // Even for straight edges with only 2 points, we still want control points!
        //     // For straight edges, we need to show control points from stored displacements
        //     if (BezierPts == null || BezierPts.Length < 2)
        //     {
        //         ClearControlPointVisualization();
        //         return;
        //     }
            
        //     // For edges with only 2 points (straight), create visual control points from stored displacements
        //     if (BezierPts.Length == 2 && _storedDisplacements.Count > 0)
        //     {
        //         // Rebuild from displacements to get the Bezier points
        //         RebuildFromDisplacements(BezierPts[0], BezierPts[1]);
        //     }
            
        //     // Clear existing spheres
        //     ClearControlPointVisualization();
            
        //     // If still only 2 points, create default control points at 1/3 and 2/3
        //     if (BezierPts.Length == 2)
        //     {
        //         // Create temporary control points for visualization
        //         Vector3 start = BezierPts[0];
        //         Vector3 end = BezierPts[1];
        //         Vector3 dir = end - start;
        //         Vector3 cp1 = start + dir * 0.33f;
        //         Vector3 cp2 = start + dir * 0.67f;
                
        //         // Add slight offset to make them visible
        //         Vector3 perpendicular = Vector3.Cross(dir, Vector3.up).normalized;
        //         if (perpendicular == Vector3.zero)
        //             perpendicular = Vector3.Cross(dir, Vector3.right).normalized;
        //         cp1 += perpendicular * thickness;
        //         cp2 += perpendicular * thickness;
                
        //         // Create spheres at these positions
        //         GameObject sphere1 = CreateControlPointSphere(cp1, 1);
        //         GameObject sphere2 = CreateControlPointSphere(cp2, 2);
        //         _controlPointSpheres.Add(sphere1);
        //         _controlPointSpheres.Add(sphere2);
                
        //         Debug.Log($"[{gameObject.name}] Created 2 default control points for straight edge");
        //     }
        //     else
        //     {
        //         // Create spheres for interior control points (skip endpoints)
        //         for (int i = 1; i < BezierPts.Length - 1; i++)
        //         {
        //             GameObject sphere = CreateControlPointSphere(BezierPts[i], i);
        //             _controlPointSpheres.Add(sphere);
        //         }
        //     }
        // }
        public void UpdateControlPointVisualization()
        {
            // Only show for non-mirrored edges
            if (!showControlPoints || _isMirrored)
            {
                ClearControlPointVisualization();
                return;
            }
            
            // Check if this edge is in the active octant
            if (OctantMirrorSystem.Instance != null)
            {
                int activeOctant = OctantMirrorSystem.Instance.activeOctantIndex;
                if (_myOctant != activeOctant)
                {
                    ClearControlPointVisualization();
                    return;
                }
            }
            
            if (BezierPts == null || BezierPts.Length < 2)
            {
                ClearControlPointVisualization();
                return;
            }
            
            // Clear existing spheres
            ClearControlPointVisualization();
            
            // For edges with stored displacements, rebuild Bezier points
            if (_storedDisplacements.Count > 0 && BezierPts.Length == 2)
            {
                RebuildFromDisplacements(BezierPts[0], BezierPts[1]);
            }
            
            // Create spheres for interior control points (skip endpoints)
            // If only 2 points, we need to create default control points for visualization
            if (BezierPts.Length == 2)
            {
                Vector3 start = BezierPts[0];
                Vector3 end = BezierPts[1];
                Vector3 dir = end - start;
                
                // Create control points ON the straight line (no offset for visualization)
                Vector3 cp1 = start + dir * 0.33f;
                Vector3 cp2 = start + dir * 0.67f;
                
                GameObject sphere1 = CreateControlPointSphere(cp1, 1);
                GameObject sphere2 = CreateControlPointSphere(cp2, 2);
                _controlPointSpheres.Add(sphere1);
                _controlPointSpheres.Add(sphere2);
                
                Debug.Log($"[{gameObject.name}] Created 2 default control point spheres on straight line");
            }
            else
            {
                // Create spheres for interior control points
                for (int i = 1; i < BezierPts.Length - 1; i++)
                {
                    GameObject sphere = CreateControlPointSphere(BezierPts[i], i);
                    _controlPointSpheres.Add(sphere);
                }
            }
        }

        public void UpdateControlPointPosition(int controlPointIndex, Vector3 newWorldPosition)
        {
            if (_isMirrored) return; // Only update from original edge
            
            // Update the local control point
            BezierPts[controlPointIndex] = newWorldPosition;
            
            // Update stored displacements
            UpdateStoredDisplacementsFromBezierPoints();
            
            // Rebuild the mesh
            RebuildMesh();
            
            // Notify GraphManager to update all mirrored edges
            if (GraphManager.Instance != null)
            {
                GraphManager.Instance.OnEdgeControlPointMoved(this, controlPointIndex, newWorldPosition);
            }
        }

        public void UpdateMirroredControlPoint(int controlPointIndex, Vector3 mirroredPosition, MicrostructureEdge sourceEdge)
        {
            if (!_isMirrored) return;
            
            // Update the Bezier point
            if (BezierPts != null && controlPointIndex < BezierPts.Length)
            {
                BezierPts[controlPointIndex] = mirroredPosition;
                
                // Update stored displacements from the mirrored control points
                UpdateStoredDisplacementsFromBezierPoints();
                
                // Rebuild the mesh
                RebuildMesh();
                
                // Update visualization
                UpdateControlPointVisualization();
                
                Debug.Log($"[{gameObject.name}] Mirrored control point {controlPointIndex} to position {mirroredPosition}");
            }
        }

        private GameObject CreateControlPointSphere(Vector3 position, int index)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.SetParent(transform);
            sphere.transform.position = position;
            sphere.transform.localScale = Vector3.one * controlPointRadius;
            
            // Add collider for dragging
            SphereCollider collider = sphere.GetComponent<SphereCollider>();
            if (collider != null)
            {
                collider.isTrigger = true;
                collider.radius = 1.5f; // Make it easier to click
            }
            
            // Make sphere bright and visible
            Renderer renderer = sphere.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = controlPointColor;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", controlPointColor * 0.8f);
            renderer.material = mat;
            
            sphere.name = $"ControlPoint_{index}";
            
            // Store control point data
            var cpData = sphere.AddComponent<ControlPointData>();
            cpData.edge = this;
            cpData.controlPointIndex = index;
            
            return sphere;
        }

        // Helper class for control point data
        private class ControlPointData : MonoBehaviour
        {
            public MicrostructureEdge edge;
            public int controlPointIndex;
        }
        private void ClearControlPointVisualization()
        {
            foreach (var sphere in _controlPointSpheres)
            {
                if (sphere != null)
                    Destroy(sphere);
            }
            _controlPointSpheres.Clear();
        }

        public void ToggleHighlight()
        {
            if (_mr == null) return;

            if (IsHighlighted)
            {
                if (_materialBeforeHighlight != null)
                    _mr.material = _materialBeforeHighlight;
                IsHighlighted = false;
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
            }
        }

        private void GetReferences()
        {
            if (_mf == null) _mf = GetComponent<MeshFilter>();
            if (_mr == null) _mr = GetComponent<MeshRenderer>();
            if (_mc == null) _mc = GetComponent<MeshCollider>();
        }

        // public void Initialise(EdgeData data, float scaledThickness,
        //                     Vector3 start, Vector3 end, List<Vector3> controlPoints)
        // {
        //     Data = data;
        //     thickness = scaledThickness;

        //     List<Vector3> pts = new List<Vector3> { start };
            
        //     // If no control points provided, create default ones at 1/3 and 2/3 along the edge
        //     if (controlPoints == null || controlPoints.Count == 0)
        //     {
        //         // Create default control points at 1/3 and 2/3 of the way from start to end
        //         Vector3 dir = end - start;
        //         Vector3 defaultCp1 = start + dir * 0.33f;
        //         Vector3 defaultCp2 = start + dir * 0.67f;
                
        //         // Add a slight perpendicular offset to make them visible and draggable
        //         // This creates a small initial curve that shows the control points
        //         Vector3 perpendicular = Vector3.Cross(dir, Vector3.up).normalized;
        //         if (perpendicular == Vector3.zero)
        //             perpendicular = Vector3.Cross(dir, Vector3.right).normalized;
                
        //         defaultCp1 += perpendicular * (thickness * 2f);
        //         defaultCp2 += perpendicular * (thickness * 2f);
                
        //         pts.Add(defaultCp1);
        //         pts.Add(defaultCp2);
                
        //         Debug.Log($"[{gameObject.name}] Created default control points at 1/3 and 2/3 along edge");
        //     }
        //     else
        //     {
        //         pts.AddRange(controlPoints);
        //     }
            
        //     pts.Add(end);
        //     BezierPts = pts.ToArray();

        //     // Store displacements if we have interior control points
        //     List<Vector3> interiorControlPoints = new List<Vector3>();
        //     for (int i = 1; i < BezierPts.Length - 1; i++)
        //     {
        //         interiorControlPoints.Add(BezierPts[i]);
        //     }
            
        //     if (interiorControlPoints.Count > 0)
        //     {
        //         _storedDisplacements = BezierFitter.FitDisplacements(
        //             interiorControlPoints, start, end, interiorControlPoints.Count);
        //         Debug.Log($"[{gameObject.name}] Stored {_storedDisplacements.Count} displacements from {interiorControlPoints.Count} control points");
        //     }
        //     else
        //     {
        //         _storedDisplacements.Clear();
        //         Debug.Log($"[{gameObject.name}] No interior control points - straight edge");
        //     }

        //     GetReferences();

        //     if (material != null && _mr != null)
        //         _mr.material = material;

        //     transform.position = Vector3.zero;
        //     transform.rotation = Quaternion.identity;
        //     transform.localScale = Vector3.one;

        //     gameObject.name = $"Edge_{data.node1}-{data.node2}";
        //     RebuildMesh();
        // }
        public void Initialise(EdgeData data, float scaledThickness,
                    Vector3 start, Vector3 end, List<Vector3> controlPoints)
        {
            Data = data;
            thickness = scaledThickness;

            List<Vector3> pts = new List<Vector3> { start };
            
            // If no control points provided, create default ones at 1/3 and 2/3 along the edge
            if (controlPoints == null || controlPoints.Count == 0)
            {
                // Create default control points ON the straight line (no offset)
                Vector3 dir = end - start;
                Vector3 defaultCp1 = start + dir * 0.33f;
                Vector3 defaultCp2 = start + dir * 0.67f;
                
                pts.Add(defaultCp1);
                pts.Add(defaultCp2);
                
                Debug.Log($"[{gameObject.name}] Created default control points ON the straight line at 1/3 and 2/3");
            }
            else
            {
                pts.AddRange(controlPoints);
            }
            
            pts.Add(end);
            BezierPts = pts.ToArray();

            // Store displacements if we have interior control points
            List<Vector3> interiorControlPoints = new List<Vector3>();
            for (int i = 1; i < BezierPts.Length - 1; i++)
            {
                interiorControlPoints.Add(BezierPts[i]);
            }
            
            if (interiorControlPoints.Count > 0)
            {
                _storedDisplacements = BezierFitter.FitDisplacements(
                    interiorControlPoints, start, end, interiorControlPoints.Count);
                Debug.Log($"[{gameObject.name}] Stored {_storedDisplacements.Count} displacements");
            }
            else
            {
                _storedDisplacements.Clear();
            }

            GetReferences();

            if (material != null && _mr != null)
                _mr.material = material;

            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            gameObject.name = $"Edge_{data.node1}-{data.node2}";
            RebuildMesh();
        }

        public void SetThickness(float t) { thickness = t; RebuildMesh(); }
        public void SetBezierPoints(Vector3[] pts) { BezierPts = pts; RebuildMesh(); }

        public void RebuildMesh()
        {
            if (_mf == null) _mf = GetComponent<MeshFilter>();
            if (_mf == null) return;

            if (_mf.sharedMesh != null)
            {
                if (Application.isPlaying) Destroy(_mf.sharedMesh);
                else DestroyImmediate(_mf.sharedMesh);
            }

            var mesh = BuildTubeMesh();
            _mf.mesh = mesh;

            if (Application.isPlaying)
            {
                if (_mc == null) _mc = gameObject.AddComponent<MeshCollider>();
                _mc.sharedMesh = mesh;
            }

            UpdateControlPointVisualization();
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
                RebuildMesh();
            else
            {
                _needsRebuild = true;
                GetReferences();
                if (material != null && _mr != null && _mr.sharedMaterial != material)
                    _mr.sharedMaterial = material;
            }
        }

        Vector3 EvalBezier(float t)
        {
            if (BezierPts == null || BezierPts.Length == 0) return Vector3.zero;
            var pts = (Vector3[])BezierPts.Clone();
            int n = pts.Length;
            for (int r = 1; r < n; r++)
                for (int i = 0; i < n - r; i++)
                    pts[i] = Vector3.Lerp(pts[i], pts[i + 1], t);
            return pts[0];
        }

        List<Vector3> SampleSpine(int count)
        {
            var result = new List<Vector3>(count);
            for (int i = 0; i < count; i++)
                result.Add(EvalBezier(i / (float)(count - 1)));
            return result;
        }

        Mesh BuildTubeMesh()
        {
            if (BezierPts == null || BezierPts.Length < 2)
                return new Mesh { name = "EmptyEdge" };

            var spine = SampleSpine(curveSegments + 1);
            var tangents = ComputeTangents(spine);
            var frames = ComputeParallelTransportFrames(spine, tangents);

            int rings = spine.Count;
            int sides = radialSegments;

            var mesh = new Mesh { name = "EdgeTube" };
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            for (int r = 0; r < rings; r++)
            {
                Vector3 centre = spine[r];
                Vector3 normal = frames[r].normal;
                Vector3 binormal = frames[r].binormal;

                for (int s = 0; s < sides; s++)
                {
                    float angle = s / (float)sides * Mathf.PI * 2f;
                    Vector3 radial = Mathf.Cos(angle) * normal + Mathf.Sin(angle) * binormal;
                    vertices.Add(transform.InverseTransformPoint(centre + radial * thickness));
                    normals.Add(radial);
                    uvs.Add(new Vector2(s / (float)sides, r / (float)(rings - 1)));
                }
            }

            for (int r = 0; r < rings - 1; r++)
                for (int s = 0; s < sides; s++)
                {
                    int next = (s + 1) % sides;
                    int a = r * sides + s, b = r * sides + next;
                    int c = (r + 1) * sides + s, d = (r + 1) * sides + next;
                    tris.AddRange(new[] { a, c, b, b, c, d });
                }

            // Start cap
            {
                int capCenter = vertices.Count;
                Vector3 capPos = spine[0];
                Vector3 capNormal = -tangents[0];
                vertices.Add(transform.InverseTransformPoint(capPos));
                normals.Add(capNormal);
                uvs.Add(new Vector2(0.5f, 0.5f));

                int capRingStart = vertices.Count;
                var n0 = frames[0].normal;
                var b0 = frames[0].binormal;
                for (int s = 0; s < sides; s++)
                {
                    float angle = s / (float)sides * Mathf.PI * 2f;
                    Vector3 radial = Mathf.Cos(angle) * n0 + Mathf.Sin(angle) * b0;
                    vertices.Add(transform.InverseTransformPoint(capPos + radial * thickness));
                    normals.Add(capNormal);
                    uvs.Add(new Vector2(0.5f + 0.5f * Mathf.Cos(angle), 0.5f + 0.5f * Mathf.Sin(angle)));
                }

                for (int s = 0; s < sides; s++)
                    tris.AddRange(new[] { capCenter, capRingStart + (s + 1) % sides, capRingStart + s });
            }

            // End cap
            {
                int capCenter = vertices.Count;
                Vector3 capPos = spine[rings - 1];
                Vector3 capNormal = tangents[rings - 1];
                vertices.Add(transform.InverseTransformPoint(capPos));
                normals.Add(capNormal);
                uvs.Add(new Vector2(0.5f, 0.5f));

                int capRingStart = vertices.Count;
                var nL = frames[rings - 1].normal;
                var bL = frames[rings - 1].binormal;
                for (int s = 0; s < sides; s++)
                {
                    float angle = s / (float)sides * Mathf.PI * 2f;
                    Vector3 radial = Mathf.Cos(angle) * nL + Mathf.Sin(angle) * bL;
                    vertices.Add(transform.InverseTransformPoint(capPos + radial * thickness));
                    normals.Add(capNormal);
                    uvs.Add(new Vector2(0.5f + 0.5f * Mathf.Cos(angle), 0.5f + 0.5f * Mathf.Sin(angle)));
                }

                for (int s = 0; s < sides; s++)
                    tris.AddRange(new[] { capCenter, capRingStart + s, capRingStart + (s + 1) % sides });
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private struct TransportFrame
        {
            public Vector3 tangent;
            public Vector3 normal;
            public Vector3 binormal;
        }

        private List<Vector3> ComputeTangents(List<Vector3> spine)
        {
            int n = spine.Count;
            var tangents = new List<Vector3>(n);
            for (int i = 0; i < n; i++)
            {
                Vector3 t;
                if (i == 0)
                    t = spine[1] - spine[0];
                else if (i == n - 1)
                    t = spine[n - 1] - spine[n - 2];
                else
                    t = spine[i + 1] - spine[i - 1];
                tangents.Add(t.normalized);
            }
            return tangents;
        }

        private List<TransportFrame> ComputeParallelTransportFrames(
            List<Vector3> spine, List<Vector3> tangents)
        {
            int n = spine.Count;
            var frames = new List<TransportFrame>(n);

            Vector3 t0 = tangents[0];
            Vector3[] candidates = { Vector3.right, Vector3.up, Vector3.forward };
            Vector3 seedUp = Vector3.up;
            float minDot = float.MaxValue;
            foreach (var c in candidates)
            {
                float d = Mathf.Abs(Vector3.Dot(t0, c));
                if (d < minDot) { minDot = d; seedUp = c; }
            }

            Vector3 n0 = Vector3.Cross(t0, seedUp).normalized;
            if (n0.sqrMagnitude < 0.001f)
            {
                n0 = Vector3.Cross(t0, Vector3.right).normalized;
                if (n0.sqrMagnitude < 0.001f)
                    n0 = Vector3.Cross(t0, Vector3.forward).normalized;
            }
            Vector3 b0 = Vector3.Cross(t0, n0).normalized;
            n0 = Vector3.Cross(b0, t0).normalized;

            frames.Add(new TransportFrame { tangent = t0, normal = n0, binormal = b0 });

            for (int i = 1; i < n; i++)
            {
                var prev = frames[i - 1];
                Vector3 t1 = tangents[i];

                Vector3 axis = Vector3.Cross(prev.tangent, t1);
                float sinA = axis.magnitude;
                float cosA = Vector3.Dot(prev.tangent, t1);

                Vector3 newN, newB;

                if (sinA < 1e-6f)
                {
                    newN = prev.normal;
                    newB = prev.binormal;
                }
                else
                {
                    axis = axis / sinA;
                    float angle = Mathf.Atan2(sinA, cosA);
                    Quaternion rot = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, axis);
                    newN = rot * prev.normal;
                    newB = rot * prev.binormal;
                }

                newN = Vector3.Cross(Vector3.Cross(t1, newN).normalized, t1).normalized;
                newB = Vector3.Cross(t1, newN).normalized;

                frames.Add(new TransportFrame
                {
                    tangent = t1,
                    normal = newN,
                    binormal = newB
                });
            }

            if (n > 1)
            {
                Vector3 firstN = frames[0].normal;
                Vector3 lastT = frames[n - 1].tangent;
                Vector3 projectedFirstN = (firstN - Vector3.Dot(firstN, lastT) * lastT).normalized;

                if (projectedFirstN.sqrMagnitude > 0.001f)
                {
                    Vector3 lastN = frames[n - 1].normal;
                    float cosT = Mathf.Clamp(Vector3.Dot(projectedFirstN, lastN), -1f, 1f);
                    float sinT = Vector3.Dot(Vector3.Cross(projectedFirstN, lastN), lastT);
                    float totalTwist = Mathf.Atan2(sinT, cosT);

                    for (int i = 0; i < n; i++)
                    {
                        float correction = -totalTwist * (i / (float)(n - 1));
                        if (Mathf.Abs(correction) < 1e-6f) continue;

                        Quaternion detwist = Quaternion.AngleAxis(correction * Mathf.Rad2Deg, frames[i].tangent);
                        var f = frames[i];
                        f.normal = detwist * f.normal;
                        f.binormal = detwist * f.binormal;
                        frames[i] = f;
                    }
                }
            }

            return frames;
        }

        public void UpdateEndpointsAndRebuild(Vector3 newSourcePos, Vector3 newTargetPos)
        {
            _lastSourcePos = newSourcePos;
            _lastTargetPos = newTargetPos;
            
            if (BezierPts != null && BezierPts.Length >= 2)
            {
                BezierPts[0] = newSourcePos;
                BezierPts[BezierPts.Length - 1] = newTargetPos;
                RebuildMesh();
            }
        }

        // Force a rebuild using current node positions
        public void RefreshFromNodes()
        {
            if (_sourceNode != null && _targetNode != null)
            {
                Vector3 posA = _sourceNode.transform.position;
                Vector3 posB = _targetNode.transform.position;
                
                // Use stored displacements if available
                if (_storedDisplacements.Count > 0)
                {
                    RebuildFromDisplacements(posA, posB);
                }
                else
                {
                    UpdateEndpointsAndRebuild(posA, posB);
                }
            }
        }

        public void CopyDisplacementsFrom(MicrostructureEdge otherEdge)
        {
            if (otherEdge == null) return;
            
            _storedDisplacements = new List<Vector3>(otherEdge.StoredDisplacements);
            _bezierM = otherEdge._bezierM;
            
            Debug.Log($"[{gameObject.name}] Copied {_storedDisplacements.Count} displacements from {otherEdge.gameObject.name}");
            
            // Rebuild using copied displacements
            if (_sourceNode != null && _targetNode != null && _storedDisplacements.Count > 0)
            {
                RebuildFromDisplacements(_sourceNode.transform.position, _targetNode.transform.position);
            }
        }

        public void DebugDisplacements()
        {
            Debug.Log($"[{gameObject.name}] Stored displacements count: {_storedDisplacements.Count}");
            for (int i = 0; i < Mathf.Min(_storedDisplacements.Count, 5); i++)
            {
                Debug.Log($"  Disp[{i}]: {_storedDisplacements[i]}");
            }
            
            if (BezierPts != null)
            {
                Debug.Log($"BezierPts length: {BezierPts.Length}");
                Debug.Log($"  Start: {BezierPts[0]}, End: {BezierPts[BezierPts.Length - 1]}");
            }

        }

        private bool GetDragWorldPoint(Ray ray, Vector3 axisDir, Vector3 planePoint, out Vector3 worldPoint)
        {
            worldPoint = Vector3.zero;
            // Build a plane that faces the camera
            Vector3 camFwd = Camera.main.transform.forward;
            Vector3 planeNormal = camFwd - Vector3.Dot(camFwd, axisDir) * axisDir;
            if (planeNormal.sqrMagnitude < 0.001f)
                planeNormal = Vector3.up - Vector3.Dot(Vector3.up, axisDir) * axisDir;
            planeNormal.Normalize();
            var plane = new Plane(planeNormal, planePoint);
            if (!plane.Raycast(ray, out float dist)) return false;
            worldPoint = ray.GetPoint(dist);
            return true;
        }
        private void OnDestroy()
        {
            ClearControlPointVisualization();
            if (_mf != null && _mf.sharedMesh != null)
            {
                if (Application.isPlaying) Destroy(_mf.sharedMesh);
                else DestroyImmediate(_mf.sharedMesh);
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (BezierPts == null || BezierPts.Length < 2) return;
            Gizmos.color = Color.cyan;
            for (int i = 0; i < BezierPts.Length - 1; i++)
                Gizmos.DrawLine(BezierPts[i], BezierPts[i + 1]);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(BezierPts[0], thickness * 0.5f);
            Gizmos.DrawWireSphere(BezierPts[BezierPts.Length - 1], thickness * 0.5f);
        }
#endif
    }

    public class ControlPointData : MonoBehaviour
    {
        public MicrostructureEdge edge;
        public int controlPointIndex;
    }
}