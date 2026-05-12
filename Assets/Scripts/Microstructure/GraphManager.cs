using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Microstructure
{
    public class GraphManager : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject uiPanel;
        
        // Node creation UI
        [SerializeField] private TMP_InputField nodeXInput;
        [SerializeField] private TMP_InputField nodeYInput;
        [SerializeField] private TMP_InputField nodeZInput;
        [SerializeField] private Button createNodeButton;
        
        // Edge creation UI
        [SerializeField] private Button createEdgeButton;
        [SerializeField] private TextMeshProUGUI edgeCreationStatus;

        // Face creation UI
        [SerializeField] private Button createFaceButton;
        
        // Management UI
        [SerializeField] private Button deleteModeButton;
        [SerializeField] private Button clearUserNodesButton;
        [SerializeField] private Button clearUserEdgesButton;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI deleteModeText;
        
        [Header("Node Settings")]
        [SerializeField] private GameObject nodePrefab;
        [SerializeField] private float defaultNodeRadius = 0.05f;
        
        [Header("Edge Settings")]
        [SerializeField] private GameObject edgePrefab;
        [SerializeField] private float defaultEdgeThickness = 0.03f;

        [Header("Edge Curve Settings")]
        [SerializeField] private int defaultBezierM = 0;
        [SerializeField] private int targetSampleCount = 20;
        [SerializeField] private float defaultCurveAmplitude = 0f;

        [Header("Face Settings")]
        [SerializeField] private GameObject facePrefab;
        [SerializeField] private TextMeshProUGUI faceSelectionStatus;

        
        [Header("Selection & Deletion")]
        [SerializeField] private KeyCode deleteKey = KeyCode.Delete;
        [SerializeField] private KeyCode deleteModeKey = KeyCode.D;
        [SerializeField] private KeyCode edgeCreationKey = KeyCode.E;
        [SerializeField] private KeyCode faceCreationKey = KeyCode.F;

        [Header("Octant Roots (must match OctantVisualController order)")]
        [SerializeField] private List<Transform> octantNodeRoots = new List<Transform>();
        [SerializeField] private List<Transform> octantEdgeRoots = new List<Transform>();
        
        [Header("Optional")]
        [SerializeField] private Transform nodesParent;
        [SerializeField] private Transform edgesParent;
        [SerializeField] private Transform facesParent;
        [SerializeField] private bool autoFindPrefabs = true;
        
        // Tracking all graph elements
        private List<MicrostructureNode> _allNodes = new List<MicrostructureNode>();
        private List<MicrostructureEdge> _allEdges = new List<MicrostructureEdge>();

        private List<MicrostructureFace> _allFaces = new List<MicrostructureFace>();
        private HashSet<MicrostructureFace> _userFaces = new HashSet<MicrostructureFace>();
        
        // User-created elements
        private HashSet<MicrostructureNode> _userNodes = new HashSet<MicrostructureNode>();
        private HashSet<MicrostructureEdge> _userEdges = new HashSet<MicrostructureEdge>();
        
        // Track the three most recently clicked nodes for edge and face creation
        private MicrostructureNode _recentNode1 = null;
        private MicrostructureNode _recentNode2 = null;
        private MicrostructureNode _recentNode3 = null;
        
        // Delete mode state
        private bool _deleteMode = false;
        private MicrostructureNode _selectedNode = null;
        private MicrostructureEdge _selectedEdge = null;
        private MicrostructureFace _selectedFace = null;
        
        // ID management
        private const int USER_NODE_ID_OFFSET = 100000;
        private const int USER_EDGE_ID_OFFSET = 100000;
        private int _nextNodeId = USER_NODE_ID_OFFSET;
        private int _nextEdgeId = USER_EDGE_ID_OFFSET;
        private const string USER_NODE_PREFIX = "UserNode_";
        private const string USER_EDGE_PREFIX = "UserEdge_";
        
        // Events
        public System.Action<MicrostructureNode> OnNodeAdded;
        public System.Action<MicrostructureNode> OnNodeRemoved;
        public System.Action<MicrostructureEdge> OnEdgeAdded;
        public System.Action<MicrostructureEdge> OnEdgeRemoved;
        
        public static GraphManager Instance { get; private set; }
        
        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Debug.LogWarning("Multiple GraphManager instances found!");
            
            if (autoFindPrefabs)
            {
                if (nodePrefab == null)
                {
                    MicrostructureNode existingNode = FindObjectOfType<MicrostructureNode>();
                    if (existingNode != null) 
                    {
                        nodePrefab = existingNode.gameObject;
                        Debug.Log($"Auto-found node prefab: {nodePrefab.name}");
                    }
                }
                
                if (edgePrefab == null)
                {
                    MicrostructureEdge existingEdge = FindObjectOfType<MicrostructureEdge>();
                    if (existingEdge != null) 
                    {
                        edgePrefab = existingEdge.gameObject;
                        Debug.Log($"Auto-found edge prefab: {edgePrefab.name}");
                    }
                }
            }
        }
        
        private void Start()
        {
            SetupUI();
            FindAllGraphElements();
            UpdateEdgeCreationStatus();
            SetActiveOctant(0);
            Debug.Log("GraphManager Start complete - waiting for input");
        }
        
        private void SetupUI()
        {
            if (createNodeButton != null)
                createNodeButton.onClick.AddListener(CreateNodeFromUI);
            
            if (createEdgeButton != null)
                createEdgeButton.onClick.AddListener(CreateEdgeFromRecentNodes);
            
            if (deleteModeButton != null)
                deleteModeButton.onClick.AddListener(ToggleDeleteMode);
            
            if (clearUserNodesButton != null)
                clearUserNodesButton.onClick.AddListener(ClearAllUserNodes);
            
            if (clearUserEdgesButton != null)
                clearUserEdgesButton.onClick.AddListener(ClearAllUserEdges);
            
            if (nodeXInput != null) nodeXInput.text = "0";
            if (nodeYInput != null) nodeYInput.text = "0";
            if (nodeZInput != null) nodeZInput.text = "0";

            if (createFaceButton != null)
                createFaceButton.onClick.AddListener(CreateFaceFromRecentNodes);
            
            UpdateStatus("Enter world coordinates (X, Y, Z) and click 'Create Node' to add nodes in world space.");
            UpdateDeleteModeUI();
        }

        public void SetActiveOctant(int index)
        {
            OctantMirrorSystem.Instance?.SetActiveOctant(index);
            OctantVisualController.Instance?.RefreshVisuals(index);
            UpdateStatus($"Active octant set to {index}");
        }
        
        private void FindAllGraphElements()
        {
            _allNodes.Clear();
            _allNodes.AddRange(FindObjectsOfType<MicrostructureNode>());

            _allEdges.Clear();
            _allEdges.AddRange(FindObjectsOfType<MicrostructureEdge>());

            foreach (var node in _allNodes)
            {
                if (node.gameObject.name.StartsWith(USER_NODE_PREFIX))
                    _userNodes.Add(node);
            }

            foreach (var edge in _allEdges)
            {
                if (edge.gameObject.name.StartsWith(USER_EDGE_PREFIX))
                    _userEdges.Add(edge);
            }

            Debug.Log($"[FindAllGraphElements] Found {_allNodes.Count} nodes, {_allEdges.Count} edges.");
        }
        
        public void OnNodeClickedFromNode(MicrostructureNode clickedNode, bool isHighlighted)
        {
            Debug.Log($"=== OnNodeClickedFromNode: {clickedNode.gameObject.name}, highlighted: {isHighlighted} ===");

            if (_deleteMode)
            {
                RemoveNode(clickedNode);
                return;
            }

            if (isHighlighted)
            {
                ClearSelection();
                _selectedNode = clickedNode;
                clickedNode.SetSelected(true);

                if (_recentNode1 == clickedNode || _recentNode2 == clickedNode || _recentNode3 == clickedNode)
                {
                    if (_recentNode1 == clickedNode) return;
                    if (_recentNode2 == clickedNode)
                    {
                        _recentNode2 = _recentNode1;
                        _recentNode1 = clickedNode;
                    }
                    else if (_recentNode3 == clickedNode)
                    {
                        _recentNode3 = _recentNode2;
                        _recentNode2 = _recentNode1;
                        _recentNode1 = clickedNode;
                    }
                }
                else
                {
                    _recentNode3 = _recentNode2;
                    _recentNode2 = _recentNode1;
                    _recentNode1 = clickedNode;
                }
                Debug.Log($"Node added — RecentNode1: {_recentNode1?.gameObject.name}, " +
                          $"RecentNode2: {_recentNode2?.gameObject.name}, " +
                          $"RecentNode3: {_recentNode3?.gameObject.name}");
            }
            else
            {
                if (_recentNode1 == clickedNode)
                {
                    _recentNode1 = _recentNode2;
                    _recentNode2 = _recentNode3;
                    _recentNode3 = null;
                }
                else if (_recentNode2 == clickedNode)
                {
                    _recentNode2 = _recentNode3;
                    _recentNode3 = null;
                }
                else if (_recentNode3 == clickedNode)
                {
                    _recentNode3 = null;
                }
                Debug.Log($"Node removed from selection");
            }

            UpdateEdgeCreationStatus();
        }

        public void OnNodeDragged(MicrostructureNode draggedNode, Vector3 delta)
        {
            // 1. Move mirror nodes using absolute positioning
            if (OctantMirrorSystem.Instance != null)
            {
                int activeOctant = OctantMirrorSystem.Instance.activeOctantIndex;
                Vector3 draggedPos = draggedNode.transform.position;

                for (int i = 0; i < 8; i++)
                {
                    if (i == activeOctant) continue;

                    MicrostructureNode mirrorNode = draggedNode.GetMirror(i);
                    if (mirrorNode == null) continue;

                    // Use absolute mirror positioning
                    Vector3 mirroredPos = OctantMirrorSystem.Instance.MirrorPosition(draggedPos, i);
                    mirrorNode.transform.position = mirroredPos;
                }
            }

            // 2. Refresh geometry for dragged node (this will call UpdateEndpointsAndRebuild on edges)
            RefreshConnectedGeometry(draggedNode);
        }

        private void RefreshConnectedGeometry(MicrostructureNode node)
        {
            if (node?.Data == null) return;
            Debug.Log($"[GraphManager] RefreshConnectedGeometry called for node {node.gameObject.name} (ID:{node.Data.node_id})");


            // -- Edges --
            var connectedEdges = _allEdges.Where(e =>
                e != null && e.Data != null &&
                (e.Data.node1 == node.Data.node_id ||
                e.Data.node2 == node.Data.node_id)).ToList();

            foreach (var edge in connectedEdges)
            {
                if (edge == null) continue;

                // Determine current start/end world positions from the actual nodes
                MicrostructureNode nodeA = GetNodeById(edge.Data.node1);
                MicrostructureNode nodeB = GetNodeById(edge.Data.node2);

                if (nodeA == null || nodeB == null) continue;

                Vector3 posA = nodeA.transform.position;
                Vector3 posB = nodeB.transform.position;

                // Update EdgeData positions
                edge.Data.start = new List<float> { posA.x, posA.y, posA.z };
                edge.Data.end   = new List<float> { posB.x, posB.y, posB.z };

                // CRITICAL: Don't reinitialize! Just update endpoints
                // The edge's Update() will handle the actual rebuild using stored displacements
                edge.UpdateEndpointsAndRebuild(posA, posB);
            }

            // -- Faces (keep as is) --
            var connectedFaces = _allFaces.Where(f =>
                f != null && f.Data != null &&
                (f.Data.node1 == node.Data.node_id ||
                f.Data.node2 == node.Data.node_id ||
                f.Data.node3 == node.Data.node_id)).ToList();

            foreach (var face in connectedFaces)
            {
                if (face == null) continue;

                MicrostructureNode fn1 = GetNodeById(face.Data.node1);
                MicrostructureNode fn2 = GetNodeById(face.Data.node2);
                MicrostructureNode fn3 = GetNodeById(face.Data.node3);

                if (fn1 == null || fn2 == null || fn3 == null) continue;

                Vector3 p1 = fn1.transform.position;
                Vector3 p2 = fn2.transform.position;
                Vector3 p3 = fn3.transform.position;

                face.Data.positions_flat = new List<float>
                {
                    p1.x, p1.y, p1.z,
                    p2.x, p2.y, p2.z,
                    p3.x, p3.y, p3.z,
                };

                face.UpdatePositionsAndRebuild(p1, p2, p3);
            }
        }

        public MicrostructureNode GetNodeById(int nodeId)
        {
            return _allNodes.FirstOrDefault(n => n != null && n.Data != null &&
                                                 n.Data.node_id == nodeId);
        }

        // ────────────────────────────────────────────────────────────────────

        private void ClearAllNodeHighlights()
        {
            Debug.Log("[GraphManager] Clearing all node highlights");
            
            foreach (var node in _allNodes)
            {
                if (node != null && node.IsRaycastHighlighted)
                    node.ToggleRaycastHighlight();
            }
            
            if (_recentNode1 != null && _recentNode1.IsRaycastHighlighted)
                _recentNode1.ToggleRaycastHighlight();
            if (_recentNode2 != null && _recentNode2.IsRaycastHighlighted)
                _recentNode2.ToggleRaycastHighlight();
            if (_recentNode3 != null && _recentNode3.IsRaycastHighlighted)
                _recentNode3.ToggleRaycastHighlight();
        }

        public void OnEdgeClickedFromEdge(MicrostructureEdge clickedEdge, bool isHighlighted)
        {
            Debug.Log($"=== OnEdgeClickedFromEdge: {clickedEdge.gameObject.name}, highlighted: {isHighlighted} ===");

            if (_deleteMode)
            {
                RemoveEdge(clickedEdge);
                return;
            }

            UpdateStatus(isHighlighted
                ? $"Edge selected: {clickedEdge.gameObject.name}"
                : $"Edge deselected: {clickedEdge.gameObject.name}");

            if (isHighlighted)
            {
                ClearSelection();
                _selectedEdge = clickedEdge;
                clickedEdge.ToggleHighlight();
                UpdateStatus($"Edge selected: {clickedEdge.gameObject.name}. Press Delete to remove.");
            }
        }

        public void OnFaceClickedFromFace(MicrostructureFace clickedFace, bool isHighlighted)
        {
            Debug.Log($"=== OnFaceClickedFromFace: {clickedFace.gameObject.name}, highlighted: {isHighlighted} ===");

            if (_deleteMode)
            {
                RemoveFace(clickedFace);
                return;
            }

            if (isHighlighted)
            {
                ClearSelection();
                _selectedFace = clickedFace;
                
                string faceInfo = $"Face {clickedFace.Data?.node1}-{clickedFace.Data?.node2}-{clickedFace.Data?.node3}";
                UpdateStatus($"Selected: {faceInfo}");
                
                if (faceSelectionStatus != null)
                {
                    faceSelectionStatus.text = $"Selected: {faceInfo}";
                    faceSelectionStatus.color = Color.green;
                }
                
                Debug.Log($"=== SELECTED FACE ID: {clickedFace.Data?.node1}-{clickedFace.Data?.node2}-{clickedFace.Data?.node3} ===");
            }
            else
            {
                if (_selectedFace == clickedFace)
                    _selectedFace = null;
                    
                if (faceSelectionStatus != null)
                    faceSelectionStatus.text = "No face selected";
            }
        }
        
        #region Edge Creation
        
        public void CreateEdgeFromRecentNodes()
        {
            Debug.Log("=== CreateEdgeFromRecentNodes CALLED ===");
            Debug.Log($"Current recentNode1: {(_recentNode1 != null ? _recentNode1.gameObject.name : "null")}");
            Debug.Log($"Current recentNode2: {(_recentNode2 != null ? _recentNode2.gameObject.name : "null")}");
            
            if (_recentNode1 == null || _recentNode2 == null)
            {
                Debug.Log("Need two nodes to create an edge!");
                UpdateStatus("Need two nodes to create an edge. Click two nodes first.", true);
                return;
            }
            
            if (EdgeExists(_recentNode2, _recentNode1))
            {
                Debug.Log("Edge already exists between these nodes!");
                UpdateStatus("Edge already exists between these nodes!", true);
                return;
            }

            List<Vector3> targetPolyline = defaultCurveAmplitude > 0
                ? BezierFitter.SampleSineArc(
                    _recentNode2.transform.position,
                    _recentNode1.transform.position,
                    defaultCurveAmplitude,
                    targetSampleCount)
                : BezierFitter.SampleStraightLine(
                    _recentNode2.transform.position,
                    _recentNode1.transform.position,
                    targetSampleCount);

            CreateEdgeBetweenNodes(_recentNode2, _recentNode1);
            
            ClearRecentNodes();
            UpdateEdgeCreationStatus();
        }

        public MicrostructureEdge CreateEdgeBetweenNodes(MicrostructureNode source, MicrostructureNode target,
            List<Vector3> targetPolyline = null, int M = 0)
        {
            if (source.Data == null || target.Data == null)
            {
                Debug.LogError($"[GraphManager] Cannot create edge — node Data is null.");
                UpdateStatus("Error: selected node has no data!", true);
                return null;
            }

            Debug.Log($"=== Creating edge: {source.gameObject.name} → {target.gameObject.name} ===");

            // Determine which octant this edge belongs to based on source node position
            int edgeOctant = 0;
            bool isMirrored = false;
            if (OctantMirrorSystem.Instance != null)
            {
                edgeOctant = OctantMirrorSystem.Instance.GetOctantForPosition(source.transform.position);
                int activeOctant = OctantMirrorSystem.Instance.activeOctantIndex;
                isMirrored = (edgeOctant != activeOctant);
                Debug.Log($"[GraphManager] Edge octant: {edgeOctant}, Active octant: {activeOctant}, IsMirrored: {isMirrored}");
            }

            if (edgePrefab == null)
            {
                UpdateStatus("Error: No edge prefab assigned!", true);
                return null;
            }

            if (edgesParent == null)
            {
                GameObject parentObj = new GameObject("Edges");
                edgesParent = parentObj.transform;
                edgesParent.SetParent(nodesParent != null ? nodesParent.parent : null);
                edgesParent.position = Vector3.zero;
                edgesParent.rotation = Quaternion.identity;
            }

            GameObject edgeObj = Instantiate(edgePrefab, Vector3.zero, Quaternion.identity, edgesParent);
            MicrostructureEdge edge = edgeObj.GetComponent<MicrostructureEdge>();

            if (edge == null)
            {
                edge = edgeObj.AddComponent<MicrostructureEdge>();
                Debug.LogWarning("Added missing MicrostructureEdge component");
            }

            EdgeData edgeData = new EdgeData
            {
                node1 = source.Data.node_id,
                node2 = target.Data.node_id,
                start = new List<float> { source.transform.position.x, source.transform.position.y, source.transform.position.z },
                end = new List<float> { target.transform.position.x, target.transform.position.y, target.transform.position.z },
                control_points_flat = new List<float>(),
                mirror_axes = new List<string>()
            };

            // Create the edge with curve if needed
            if (M > 0 && targetPolyline != null && targetPolyline.Count >= 2)
            {
                edge.InitialiseWithFit(edgeData, defaultEdgeThickness,
                    source.transform.position,
                    target.transform.position,
                    targetPolyline, M);
            }
            else
            {
                edge.Initialise(edgeData, defaultEdgeThickness,
                    source.transform.position,
                    target.transform.position,
                    new List<Vector3>());
            }

            edge.SetOctant(edgeOctant);
            edge.SetMirrored(isMirrored);  // Set based on octant check above
            edge.SetNodes(source, target);

            edge.gameObject.name = $"{USER_EDGE_PREFIX}{_nextEdgeId}";
            _nextEdgeId++;

            _allEdges.Add(edge);
            
            // Only add to userEdges if it's not mirrored
            if (!isMirrored)
            {
                _userEdges.Add(edge);
            }

            // Force an immediate rebuild to ensure mesh is created
            edge.RebuildMesh();
            edge.UpdateControlPointVisualization();

            // Only create mirrored edges if this is an original edge in the active octant
            if (OctantMirrorSystem.Instance != null && !isMirrored)
            {
                Debug.Log("[GraphManager] Creating mirrored edges for this edge...");
                EnsureNodeMirrorsExist(source);
                EnsureNodeMirrorsExist(target);
                SpawnMirroredEdges(source, target, edge);
            }

            UpdateStatus($"Edge created between {source.gameObject.name} and {target.gameObject.name}");
            OnEdgeAdded?.Invoke(edge);

            Debug.Log($"Edge created successfully. Total edges: {_allEdges.Count}, IsMirrored: {isMirrored}");
            return edge;
        }


        private void EnsureNodeMirrorsExist(MicrostructureNode node)
        {
            // Don't skip if mirrored - we need to ensure ALL nodes have mirrors
            // Only skip if it's already a mirrored node (from another octant)
            if (node.IsMirrored) return;
            
            var mirror = OctantMirrorSystem.Instance;
            if (mirror == null) return;
            
            bool createdAny = false;
            
            for (int i = 0; i < 8; i++)
            {
                if (i == mirror.activeOctantIndex) continue;
                
                if (node.GetMirror(i) == null)
                {
                    // Create the mirror node
                    Vector3 mirroredPos = mirror.MirrorPosition(node.transform.position, i);
                    
                    Transform targetParent = (octantNodeRoots != null && i < octantNodeRoots.Count && octantNodeRoots[i] != null)
                        ? octantNodeRoots[i] : nodesParent;
                    
                    GameObject nodeObj = Instantiate(nodePrefab, mirroredPos, Quaternion.identity, targetParent);
                    nodeObj.transform.position = mirroredPos;
                    
                    MicrostructureNode mirroredNode = nodeObj.GetComponent<MicrostructureNode>() ?? nodeObj.AddComponent<MicrostructureNode>();
                    
                    NodeData nodeData = new NodeData
                    {
                        node_id = _nextNodeId,
                        position = new List<float> { mirroredPos.x, mirroredPos.y, mirroredPos.z },
                        active = true,
                        mirror_axes = new List<string>()
                    };
                    
                    InitializeNode(mirroredNode, nodeData, mirroredPos, defaultNodeRadius);
                    mirroredNode.SetMirrored(true);
                    mirroredNode.gameObject.name = $"{USER_NODE_PREFIX}{_nextNodeId}";
                    _nextNodeId++;
                    
                    _allNodes.Add(mirroredNode);
                    node.AddMirror(i, mirroredNode);
                    
                    // Also add reverse mirror reference
                    mirroredNode.AddMirror(mirror.activeOctantIndex, node);
                    
                    OctantVisualController.Instance?.ApplyVisualsToObject(nodeObj, i);
                    
                    Debug.Log($"[GraphManager] Created mirror node {mirroredNode.gameObject.name} for octant {i} at position {mirroredPos}");
                    createdAny = true;
                }
            }
            
            if (createdAny)
            {
                Debug.Log($"[GraphManager] Finished creating mirrors for node {node.gameObject.name}");
            }
        }

        private void SpawnMirroredEdges(MicrostructureNode source, MicrostructureNode target,
                                        MicrostructureEdge sourceEdge)
        {
            var mirror = OctantMirrorSystem.Instance;
            if (mirror == null) return;
            
            int activeIdx = mirror.activeOctantIndex;
            
            Debug.Log($"[GraphManager] ===== SpawnMirroredEdges START =====");
            Debug.Log($"[GraphManager] activeOctant={activeIdx}, source={source.gameObject.name} at {source.transform.position}, target={target.gameObject.name} at {target.transform.position}");

            for (int i = 0; i < 8; i++)
            {
                if (i == activeIdx) continue;
                
                Debug.Log($"[GraphManager] --- Processing octant {i} ---");
                
                MicrostructureNode mirroredSource = source.GetMirror(i);
                MicrostructureNode mirroredTarget = target.GetMirror(i);
                
                if (mirroredSource == null || mirroredTarget == null)
                {
                    Debug.LogError($"[GraphManager] Missing mirrored node for octant {i}! sourceMirror={mirroredSource != null}, targetMirror={mirroredTarget != null}");
                    continue;
                }
                
                Debug.Log($"[GraphManager] Mirrored source: {mirroredSource.gameObject.name} at {mirroredSource.transform.position}");
                Debug.Log($"[GraphManager] Mirrored target: {mirroredTarget.gameObject.name} at {mirroredTarget.transform.position}");

                // Create parent for this octant's edges
                Transform targetParent = edgesParent;
                if (octantEdgeRoots != null && i < octantEdgeRoots.Count && octantEdgeRoots[i] != null)
                {
                    targetParent = octantEdgeRoots[i];
                    Debug.Log($"[GraphManager] Using octantEdgeRoots[{i}] = {targetParent.name}");
                }
                else
                {
                    Debug.Log($"[GraphManager] Using default edgesParent");
                }

                // Create the edge object
                GameObject edgeObj = Instantiate(edgePrefab, Vector3.zero, Quaternion.identity, targetParent);
                edgeObj.name = $"{USER_EDGE_PREFIX}{_nextEdgeId}";
                _nextEdgeId++;
                
                MicrostructureEdge mirroredEdge = edgeObj.GetComponent<MicrostructureEdge>();
                if (mirroredEdge == null)
                {
                    mirroredEdge = edgeObj.AddComponent<MicrostructureEdge>();
                }

                // Set basic properties
                mirroredEdge.SetOctant(i);
                mirroredEdge.SetMirrored(true);
                mirroredEdge.SetNodes(mirroredSource, mirroredTarget);
                mirroredEdge.CopyDisplacementsFrom(sourceEdge);
                
                // For straight edges, just initialize with the endpoints
                List<Vector3> emptyControls = new List<Vector3>();
                EdgeData edgeData = new EdgeData
                {
                    node1 = mirroredSource.Data.node_id,
                    node2 = mirroredTarget.Data.node_id,
                    start = new List<float> { mirroredSource.transform.position.x, mirroredSource.transform.position.y, mirroredSource.transform.position.z },
                    end = new List<float> { mirroredTarget.transform.position.x, mirroredTarget.transform.position.y, mirroredTarget.transform.position.z },
                    control_points_flat = new List<float>(),
                    mirror_axes = new List<string>()
                };
                
                // Initialize the edge (this creates the mesh)
                mirroredEdge.Initialise(edgeData, defaultEdgeThickness, 
                    mirroredSource.transform.position, 
                    mirroredTarget.transform.position, 
                    emptyControls);
                
                // Force the mesh to update
                mirroredEdge.RebuildMesh();
                
                _allEdges.Add(mirroredEdge);
                
                // Verify the edge was created correctly
                if (mirroredEdge.BezierPts != null && mirroredEdge.BezierPts.Length >= 2)
                {
                    Debug.Log($"[GraphManager] ✓ Mirrored edge {edgeObj.name} created successfully!");
                    Debug.Log($"[GraphManager]   Start: {mirroredEdge.BezierPts[0]}, End: {mirroredEdge.BezierPts[mirroredEdge.BezierPts.Length - 1]}");
                }
                else
                {
                    Debug.LogError($"[GraphManager] ✗ Failed to create edge {edgeObj.name} - no Bezier points!");
                }
                
                // Make sure the edge is visible
                MeshRenderer renderer = mirroredEdge.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.enabled = true;
                    Debug.Log($"[GraphManager] Edge renderer enabled: {renderer.enabled}, material: {renderer.material?.name}");
                }
                
                OctantVisualController.Instance?.ApplyVisualsToObject(edgeObj, i);
            }
            
            Debug.Log($"[GraphManager] ===== SpawnMirroredEdges END =====");
        }

        /// Called when a control point on an edge is moved in the active octant.
        /// Mirrors the control point movement to all mirrored edges.
        public void OnEdgeControlPointMoved(MicrostructureEdge sourceEdge, int controlPointIndex, Vector3 newWorldPosition)
        {
            if (OctantMirrorSystem.Instance == null) return;
            
            int activeOctant = OctantMirrorSystem.Instance.activeOctantIndex;
            
            Debug.Log($"[GraphManager] === MIRRORING CONTROL POINT ===");
            Debug.Log($"[GraphManager] Source edge: {sourceEdge.gameObject.name}, octant: {activeOctant}");
            Debug.Log($"[GraphManager] Control point index: {controlPointIndex}, new position: {newWorldPosition}");
            
            // List all mirrored edges for debugging
            int totalMirroredEdges = 0;
            foreach (var edge in _allEdges)
            {
                if (edge != null && edge.IsMirrored && edge != sourceEdge)
                {
                    totalMirroredEdges++;
                    Debug.Log($"[GraphManager] Found mirrored edge: {edge.gameObject.name}, octant: {edge.GetOctant()}");
                }
            }
            Debug.Log($"[GraphManager] Total mirrored edges found: {totalMirroredEdges}");
            
            int mirroredCount = 0;
            
            foreach (var edge in _allEdges)
            {
                if (edge == null || edge == sourceEdge) continue;
                if (!edge.IsMirrored) continue;
                
                int targetOctant = edge.GetOctant();
                Debug.Log($"[GraphManager] Checking mirrored edge {edge.gameObject.name} in octant {targetOctant}");
                
                // Get the nodes for this mirrored edge
                MicrostructureNode edgeNode1 = GetNodeById(edge.Data.node1);
                MicrostructureNode edgeNode2 = GetNodeById(edge.Data.node2);
                
                if (edgeNode1 == null || edgeNode2 == null)
                {
                    Debug.LogWarning($"[GraphManager] Cannot find nodes for mirrored edge {edge.gameObject.name}");
                    continue;
                }
                
                // Get source edge nodes
                MicrostructureNode sourceNode1 = GetNodeById(sourceEdge.Data.node1);
                MicrostructureNode sourceNode2 = GetNodeById(sourceEdge.Data.node2);
                
                if (sourceNode1 == null || sourceNode2 == null)
                {
                    Debug.LogError($"[GraphManager] Cannot find source nodes!");
                    return;
                }
                
                // Calculate expected mirror positions
                Vector3 expectedPos1 = OctantMirrorSystem.Instance.MirrorPosition(sourceNode1.transform.position, targetOctant);
                Vector3 expectedPos2 = OctantMirrorSystem.Instance.MirrorPosition(sourceNode2.transform.position, targetOctant);
                
                Debug.Log($"[GraphManager] Expected positions for octant {targetOctant}: {expectedPos1} and {expectedPos2}");
                Debug.Log($"[GraphManager] Actual positions: {edgeNode1.transform.position} and {edgeNode2.transform.position}");
                
                // Check if this edge matches
                float tolerance = 0.1f;
                bool matches = (Vector3.Distance(edgeNode1.transform.position, expectedPos1) < tolerance &&
                                Vector3.Distance(edgeNode2.transform.position, expectedPos2) < tolerance) ||
                            (Vector3.Distance(edgeNode1.transform.position, expectedPos2) < tolerance &&
                                Vector3.Distance(edgeNode2.transform.position, expectedPos1) < tolerance);
                
                if (matches)
                {
                    Debug.Log($"[GraphManager] ✓ MATCH FOUND for {edge.gameObject.name}!");
                    Vector3 mirroredPosition = MirrorControlPointPosition(newWorldPosition, activeOctant, targetOctant);
                    edge.UpdateMirroredControlPoint(controlPointIndex, mirroredPosition, sourceEdge);
                    mirroredCount++;
                }
                else
                {
                    Debug.Log($"[GraphManager] ✗ No match for {edge.gameObject.name}");
                }
            }
            
            Debug.Log($"[GraphManager] Mirroring complete. Updated {mirroredCount} mirrored edges.");
        }
        private Vector3 MirrorControlPointPosition(Vector3 originalPos, int sourceOctant, int targetOctant)
        {
            var mirror = OctantMirrorSystem.Instance;
            if (mirror == null) return originalPos;
            
            var sourceOctantDef = mirror.Octants[sourceOctant];
            var targetOctantDef = mirror.Octants[targetOctant];
            
            Vector3 centre = (mirror.structureMin + mirror.structureMax) * 0.5f;
            
            // Get relative position from centre
            Vector3 relativePos = originalPos - centre;
            
            // Mirror the position
            Vector3 mirroredRelative = new Vector3(
                relativePos.x * (targetOctantDef.mirrorSigns.x / sourceOctantDef.mirrorSigns.x),
                relativePos.y * (targetOctantDef.mirrorSigns.y / sourceOctantDef.mirrorSigns.y),
                relativePos.z * (targetOctantDef.mirrorSigns.z / sourceOctantDef.mirrorSigns.z)
            );
            
            return centre + mirroredRelative;
        }

        private MicrostructureNode FindMirroredNodeInOctant(MicrostructureNode originalNode, int targetOctant)
        {
            var mirror = OctantMirrorSystem.Instance;
            if (mirror == null) return null;
            
            Vector3 mirroredPos = mirror.MirrorPosition(originalNode.transform.position, targetOctant);
            
            foreach (var node in _allNodes)
            {
                if (Vector3.Distance(node.transform.position, mirroredPos) < 0.001f)
                {
                    if (!node.IsMirrored)
                    {
                        originalNode.AddMirror(targetOctant, node);
                        node.AddMirror(OctantMirrorSystem.Instance.activeOctantIndex, originalNode);
                    }
                    return node;
                }
            }
            
            return null;
        }

        
        public void RemoveEdge(MicrostructureEdge edge)
        {
            if (edge == null) return;
            
            Debug.Log($"[GraphManager] Removing edge: {edge.gameObject.name}");
            
            _allEdges.Remove(edge);
            _userEdges.Remove(edge);
            
            if (_selectedEdge == edge)
                ClearSelection();
            
            OnEdgeRemoved?.Invoke(edge);
            Destroy(edge.gameObject);
            UpdateStatus($"Removed edge: {edge.gameObject.name}");
        }
        
        public void ClearAllUserEdges()
        {
            List<MicrostructureEdge> userEdgesCopy = _userEdges.ToList();
            foreach (var edge in userEdgesCopy)
                RemoveEdge(edge);
            UpdateStatus($"Cleared {userEdgesCopy.Count} user-created edges");
        }
        
        #endregion
        
        #region Node Management
        
        public void CreateNodeFromUI()
        {
            Debug.Log("CreateNodeFromUI called");
            if (!TryParseNodeCoordinates(out Vector3 position))
            {
                UpdateStatus("Invalid coordinates. Please enter valid numbers for X, Y, Z.", true);
                return;
            }
            CreateNodeAtPosition(position);
        }
        
        public MicrostructureNode CreateNodeAtPosition(Vector3 position, float radius = -1)
        {
            Debug.Log($"Creating node at world position: ({position.x}, {position.y}, {position.z}) ===");
            
            if (nodePrefab == null)
            {
                UpdateStatus("Error: No node prefab assigned!", true);
                return null;
            }

            if (OctantMirrorSystem.Instance != null)
            {
                if (!OctantMirrorSystem.Instance.IsInActiveOctant(position))
                {
                    Vector3 clamped = OctantMirrorSystem.Instance.ClampToActiveOctant(position);
                    Debug.LogWarning($"[GraphManager] Position {position} outside active octant — clamped to {clamped}");
                    position = clamped;
                    UpdateStatus($"Position clamped to active octant bounds: ({position.x:F2}, {position.y:F2}, {position.z:F2})", false);
                }
            }
            
            if (nodesParent == null)
            {
                GameObject parentObj = new GameObject("Nodes");
                nodesParent = parentObj.transform;
                nodesParent.SetParent(null);
                nodesParent.position = Vector3.zero;
                nodesParent.rotation = Quaternion.identity;
            }

            nodesParent.position = Vector3.zero;
            nodesParent.rotation = Quaternion.identity;

            GameObject nodeObj = Instantiate(nodePrefab, position, Quaternion.identity, nodesParent);
            nodeObj.transform.position = position;

            MicrostructureNode node = nodeObj.GetComponent<MicrostructureNode>();
            
            if (node == null)
            {
                node = nodeObj.AddComponent<MicrostructureNode>();
                Debug.LogWarning("Added missing MicrostructureNode component");
            }
            
            NodeData nodeData = new NodeData
            {
                node_id     = _nextNodeId,
                position    = new List<float> { position.x, position.y, position.z },
                active      = true,
                mirror_axes = new List<string>()
            };
            
            float finalRadius = radius > 0 ? radius : defaultNodeRadius;
            InitializeNode(node, nodeData, position, finalRadius);
            node.SetRadius(finalRadius);
            
            node.gameObject.name = $"{USER_NODE_PREFIX}{_nextNodeId}";
            _nextNodeId++;
            
            _allNodes.Add(node);
            _userNodes.Add(node);
            
            UpdateStatus($"Node created at world position ({position.x:F2}, {position.y:F2}, {position.z:F2})");
            OnNodeAdded?.Invoke(node);
            if (OctantMirrorSystem.Instance != null)
                SpawnMirroredNodes(node, position);
            Debug.Log($"Node creation complete. Total nodes: {_allNodes.Count}");
            return node;
        }

        private void SpawnMirroredNodes(MicrostructureNode sourceNode, Vector3 activePos)
        {
            var mirror = OctantMirrorSystem.Instance;
            int activeIdx = mirror.activeOctantIndex;

            for (int i = 0; i < 8; i++)
            {
                if (i == activeIdx) continue;

                Vector3 mirroredPos = mirror.MirrorPosition(activePos, i);

                Transform targetParent = (octantNodeRoots != null && i < octantNodeRoots.Count 
                                        && octantNodeRoots[i] != null)
                    ? octantNodeRoots[i] : nodesParent;

                GameObject nodeObj = Instantiate(nodePrefab, mirroredPos,
                                                Quaternion.identity, targetParent);
                nodeObj.transform.position = mirroredPos;

                MicrostructureNode mirroredNode = nodeObj.GetComponent<MicrostructureNode>()
                                            ?? nodeObj.AddComponent<MicrostructureNode>();

                NodeData nodeData = new NodeData
                {
                    node_id     = _nextNodeId,
                    position    = new List<float> { mirroredPos.x, mirroredPos.y, mirroredPos.z },
                    active      = true,
                    mirror_axes = new List<string>()
                };

                float finalRadius = defaultNodeRadius;
                InitializeNode(mirroredNode, nodeData, mirroredPos, finalRadius);
                mirroredNode.SetRadius(finalRadius);

                nodeObj.name = $"{USER_NODE_PREFIX}{_nextNodeId}";
                _nextNodeId++;

                mirroredNode.SetMirrored(true);
                _allNodes.Add(mirroredNode);
                sourceNode.AddMirror(i, mirroredNode);

                OctantVisualController.Instance?.ApplyVisualsToObject(nodeObj, i);
            }
        }

        
        public void RemoveNode(MicrostructureNode node)
        {
            if (node == null) return;
            
            Debug.Log($"[GraphManager] Removing node: {node.gameObject.name}");
            
            if (node.IsRaycastHighlighted)
                node.ToggleRaycastHighlight();
            
            List<MicrostructureEdge> connectedEdges = GetEdgesConnectedToNode(node);
            Debug.Log($"[GraphManager] Node has {connectedEdges.Count} connected edges to delete");
            foreach (var edge in connectedEdges.ToList())
                RemoveEdge(edge);
            
            List<MicrostructureFace> connectedFaces = GetFacesConnectedToNode(node);
            Debug.Log($"[GraphManager] Node has {connectedFaces.Count} connected faces to delete");
            foreach (var face in connectedFaces.ToList())
                RemoveFace(face);
            
            if (_recentNode1 == node) _recentNode1 = null;
            if (_recentNode2 == node) _recentNode2 = null;
            if (_recentNode3 == node) _recentNode3 = null;
            
            _allNodes.Remove(node);
            _userNodes.Remove(node);
            
            CleanupNodeMirrorMappings(node);
            
            if (_selectedNode == node)
                ClearSelection();
            
            OnNodeRemoved?.Invoke(node);
            Destroy(node.gameObject);
            UpdateStatus($"Removed node: {node.gameObject.name} and its {connectedEdges.Count} connected edges");
            UpdateEdgeCreationStatus();
        }

        private void CleanupNodeMirrorMappings(MicrostructureNode node)
        {
            foreach (var otherNode in _allNodes)
            {
                if (otherNode != node && otherNode.Data != null && node.Data != null)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        var mirrorRef = otherNode.GetMirror(i);
                        if (mirrorRef == node)
                            Debug.Log($"[GraphManager] Removing mirror reference from {otherNode.gameObject.name} to {node.gameObject.name}");
                    }
                }
            }
        }
        
        public void ClearAllUserNodes()
        {
            List<MicrostructureNode> userNodesCopy = _userNodes.ToList();
            foreach (var node in userNodesCopy)
                RemoveNode(node);
            
            ClearRecentNodes();
            
            UpdateStatus($"Cleared {userNodesCopy.Count} user created nodes");
            UpdateEdgeCreationStatus();
        }
        
        #endregion
        
        #region Graph Queries
        
        public List<MicrostructureNode> GetAllNodes() => new List<MicrostructureNode>(_allNodes);
        public List<MicrostructureEdge> GetAllEdges() => new List<MicrostructureEdge>(_allEdges);
        
        public List<MicrostructureEdge> GetEdgesConnectedToNode(MicrostructureNode node)
        {
            return _allEdges.Where(edge =>
                edge != null &&
                edge.Data != null &&
                node.Data != null &&
                (edge.Data.node1 == node.Data.node_id || edge.Data.node2 == node.Data.node_id)).ToList();
        }
        
        public bool EdgeExists(MicrostructureNode node1, MicrostructureNode node2)
        {
            return _allEdges.Any(edge =>
                edge != null &&
                edge.Data != null &&
                node1.Data != null &&
                node2.Data != null &&
                ((edge.Data.node1 == node1.Data.node_id && edge.Data.node2 == node2.Data.node_id) ||
                (edge.Data.node1 == node2.Data.node_id && edge.Data.node2 == node1.Data.node_id)));
        }
        
        #endregion
        
        #region Selection & Deletion

        private void ClearRecentNodes()
        {
            Debug.Log("[GraphManager] Clearing recent nodes and their highlights");
            
            if (_recentNode1 != null && _recentNode1.IsRaycastHighlighted)
                _recentNode1.ToggleRaycastHighlight();
            if (_recentNode2 != null && _recentNode2.IsRaycastHighlighted)
                _recentNode2.ToggleRaycastHighlight();
            if (_recentNode3 != null && _recentNode3.IsRaycastHighlighted)
                _recentNode3.ToggleRaycastHighlight();
            
            _recentNode1 = null;
            _recentNode2 = null;
            _recentNode3 = null;
            
            UpdateEdgeCreationStatus();
        }
        
        public void ToggleDeleteMode()
        {
            _deleteMode = !_deleteMode;
            UpdateDeleteModeUI();
            
            if (_deleteMode)
            {
                ClearSelection();
                UpdateStatus("DELETE MODE ACTIVE - Click on any node or edge to delete it");
            }
            else
            {
                UpdateStatus("Delete mode disabled. Click nodes to select them for edge creation.");
            }
        }
        
        private void ClearSelection()
        {
            if (_selectedNode != null)
            {
                _selectedNode.SetSelected(false);
                _selectedNode = null;
            }
            
            if (_selectedEdge != null)
            {
                if (_selectedEdge.IsHighlighted)
                    _selectedEdge.ToggleHighlight();
                _selectedEdge = null;
            }
            if (_selectedFace != null)
            {
                if (_selectedFace.IsHighlighted)
                    _selectedFace.ToggleHighlight();
                _selectedFace = null;
            }
        }
        
        private void SelectNode(MicrostructureNode node)
        {
            ClearSelection();
            _selectedNode = node;
            UpdateStatus($"Selected: {node.gameObject.name}");
        }
        
        private void SelectEdge(MicrostructureEdge edge)
        {
            ClearSelection();
            _selectedEdge = edge;
            UpdateStatus($"Selected: {edge.gameObject.name}");
        }

        public void CreateFaceFromRecentNodes()
        {
            Debug.Log("=== CreateFaceFromRecentNodes CALLED ===");
            
            if (_recentNode1 == null || _recentNode2 == null || _recentNode3 == null)
            {
                UpdateStatus("Need three nodes to create a face. Click three nodes first.", true);
                return;
            }

            if (facePrefab == null)
            {
                UpdateStatus("Error: No face prefab assigned!", true);
                return;
            }

            CreateFaceBetweenNodes(_recentNode3, _recentNode2, _recentNode1);
            ClearRecentNodes();
            UpdateStatus("Face created successfully!");
            UpdateEdgeCreationStatus();
        }
        
        private void DeleteSelected()
        {
            if (_selectedNode != null)
            {
                RemoveNode(_selectedNode);
                _selectedNode = null;
            }
            else if (_selectedEdge != null)
            {
                RemoveEdge(_selectedEdge);
                _selectedEdge = null;
            }
            else if (_selectedFace != null)
            {
                RemoveFace(_selectedFace);
                _selectedFace = null;
            }
        }

        public MicrostructureFace CreateFaceBetweenNodes(
            MicrostructureNode n1, MicrostructureNode n2, MicrostructureNode n3)
        {
            if (n1.Data == null || n2.Data == null || n3.Data == null)
            {
                UpdateStatus("Error: a selected node has no data!", true);
                return null;
            }

            if (facesParent == null)
            {
                GameObject parentObj = new GameObject("Faces");
                facesParent = parentObj.transform;
                facesParent.SetParent(null);
                facesParent.position = Vector3.zero;
                facesParent.rotation = Quaternion.identity;
                facesParent.localScale = Vector3.one;
            }

            facesParent.position = Vector3.zero;
            facesParent.rotation = Quaternion.identity;
            facesParent.localScale = Vector3.one;

            MicrostructureEdge e01 = FindEdgeBetween(n1, n2);
            MicrostructureEdge e12 = FindEdgeBetween(n2, n3);
            MicrostructureEdge e20 = FindEdgeBetween(n3, n1);

            Debug.Log($"[GraphManager] CreateFace — " +
                    $"e01({n1.gameObject.name}→{n2.gameObject.name}): " +
                    $"{(e01 != null ? e01.gameObject.name : "null")}, " +
                    $"e12({n2.gameObject.name}→{n3.gameObject.name}): " +
                    $"{(e12 != null ? e12.gameObject.name : "null")}, " +
                    $"e20({n3.gameObject.name}→{n1.gameObject.name}): " +
                    $"{(e20 != null ? e20.gameObject.name : "null")}");

            GameObject faceObj = Instantiate(facePrefab, Vector3.zero, Quaternion.identity, facesParent);
            MicrostructureFace face = faceObj.GetComponent<MicrostructureFace>()
                                ?? faceObj.AddComponent<MicrostructureFace>();

            FaceData faceData = new FaceData
            {
                node1 = n1.Data.node_id,
                node2 = n2.Data.node_id,
                node3 = n3.Data.node_id,
                positions_flat = new List<float>
                {
                    n1.transform.position.x, n1.transform.position.y, n1.transform.position.z,
                    n2.transform.position.x, n2.transform.position.y, n2.transform.position.z,
                    n3.transform.position.x, n3.transform.position.y, n3.transform.position.z,
                },
                control_points_flat = new List<float>()
            };

            var positions = new List<Vector3>
            {
                n1.transform.position,
                n2.transform.position,
                n3.transform.position
            };

            face.Initialise(faceData, 1f, positions, new List<Vector3>(),
                e01?.BezierPts,
                e12?.BezierPts,
                e20?.BezierPts);

            faceObj.name = $"UserFace_{n1.Data.node_id}-{n2.Data.node_id}-{n3.Data.node_id}";

            if (OctantMirrorSystem.Instance != null)
                SpawnMirroredFaces(n1, n2, n3, face);

            _allFaces.Add(face);
            _userFaces.Add(face);

            OctantVisualController.Instance?.ApplyVisualsToObject(faceObj,
                OctantMirrorSystem.Instance?.activeOctantIndex ?? 0);

            UpdateStatus($"Face created between {n1.gameObject.name}, {n2.gameObject.name}, {n3.gameObject.name}");
            return face;
        }

        private MicrostructureEdge FindEdgeBetween(MicrostructureNode a, MicrostructureNode b)
        {
            if (a?.Data == null || b?.Data == null)
            {
                Debug.LogWarning($"[FindEdgeBetween] null Data — a: {a?.gameObject.name}, b: {b?.gameObject.name}");
                return null;
            }

            var found = _allEdges.FirstOrDefault(e =>
                e?.Data != null &&
                ((e.Data.node1 == a.Data.node_id && e.Data.node2 == b.Data.node_id) ||
                (e.Data.node1 == b.Data.node_id && e.Data.node2 == a.Data.node_id)));

            if (found == null)
                Debug.LogWarning($"[FindEdgeBetween] NOT FOUND between " +
                                $"{a.gameObject.name}(id:{a.Data.node_id}) and " +
                                $"{b.gameObject.name}(id:{b.Data.node_id})");

            return found;
        }

        private void SpawnMirroredFaces(MicrostructureNode n1, MicrostructureNode n2,
                                 MicrostructureNode n3, MicrostructureFace sourceFace)
        {
            var mirror = OctantMirrorSystem.Instance;
            int activeIdx = mirror.activeOctantIndex;

            for (int i = 0; i < 8; i++)
            {
                if (i == activeIdx) continue;

                MicrostructureNode m1 = n1.GetMirror(i) ?? FindMirroredNodeInOctant(n1, i);
                MicrostructureNode m2 = n2.GetMirror(i) ?? FindMirroredNodeInOctant(n2, i);
                MicrostructureNode m3 = n3.GetMirror(i) ?? FindMirroredNodeInOctant(n3, i);

                if (m1 == null || m2 == null || m3 == null)
                {
                    Debug.LogWarning($"[GraphManager] Missing mirrored node for face in octant {i}");
                    continue;
                }

                FaceData faceData = new FaceData
                {
                    node1 = m1.Data.node_id,
                    node2 = m2.Data.node_id,
                    node3 = m3.Data.node_id,
                    positions_flat = new List<float>
                    {
                        m1.transform.position.x, m1.transform.position.y, m1.transform.position.z,
                        m2.transform.position.x, m2.transform.position.y, m2.transform.position.z,
                        m3.transform.position.x, m3.transform.position.y, m3.transform.position.z,
                    },
                    control_points_flat = new List<float>()
                };

                GameObject faceObj = Instantiate(facePrefab, Vector3.zero,
                                                Quaternion.identity, facesParent);
                MicrostructureFace mirroredFace = faceObj.GetComponent<MicrostructureFace>()
                                                ?? faceObj.AddComponent<MicrostructureFace>();

                mirroredFace.Initialise(faceData, 1f,
                    new List<Vector3> { m1.transform.position, m2.transform.position, m3.transform.position },
                    new List<Vector3>());

                mirroredFace.SetMirrored(true);
                faceObj.name = $"UserFace_{m1.Data.node_id}-{m2.Data.node_id}-{m3.Data.node_id}";

                _allFaces.Add(mirroredFace);
                _userFaces.Add(mirroredFace);

                OctantVisualController.Instance?.ApplyVisualsToObject(faceObj, i);
            }
        }

        public void RegisterOriginalFace(MicrostructureFace face)
        {
            if (face != null && !_allFaces.Contains(face))
            {
                _allFaces.Add(face);
                Debug.Log($"Registered original face: {face.gameObject.name}");
            }
        }

        public void RemoveFace(MicrostructureFace face)
        {
            if (face == null) return;
            
            Debug.Log($"[GraphManager] Removing face: {face.gameObject.name}");
            
            _allFaces.Remove(face);
            _userFaces.Remove(face);
            
            Destroy(face.gameObject);
            UpdateStatus($"Removed face: {face.gameObject.name}");
        }

        private List<MicrostructureFace> GetFacesConnectedToNode(MicrostructureNode node)
        {
            if (node.Data == null) return new List<MicrostructureFace>();
            
            return _allFaces.Where(face =>
                face != null &&
                face.Data != null &&
                (face.Data.node1 == node.Data.node_id ||
                face.Data.node2 == node.Data.node_id ||
                face.Data.node3 == node.Data.node_id)).ToList();
        }

        public void ClearAllUserFaces()
        {
            foreach (var face in _userFaces.ToList())
                RemoveFace(face);
            UpdateStatus("Cleared all user faces");
        }
        
        #endregion
        
        #region Registration
        
        public void RegisterOriginalNode(MicrostructureNode node)
        {
            if (node != null && !_allNodes.Contains(node))
            {
                _allNodes.Add(node);
                Debug.Log($"Registered original node: {node.gameObject.name} at world position {node.transform.position}");
            }
        }
        
        public void RegisterOriginalEdge(MicrostructureEdge edge)
        {
            if (edge != null && !_allEdges.Contains(edge))
            {
                _allEdges.Add(edge);
                Debug.Log($"Registered original edge: {edge.gameObject.name}");
            }
        }
        
        #endregion
        
        #region UI Helpers
        
        private void UpdateEdgeCreationStatus()
        {
            if (edgeCreationStatus != null)
            {
                if (_recentNode1 == null)
                {
                    edgeCreationStatus.text = "Click a node to select it";
                    edgeCreationStatus.color = Color.gray;
                }
                else if (_recentNode2 == null)
                {
                    edgeCreationStatus.text = $"Selected: {_recentNode1.gameObject.name}\nClick another node.";
                    edgeCreationStatus.color = Color.yellow;
                }
                else if (_recentNode3 == null)
                {
                    edgeCreationStatus.text = $"Selected: {_recentNode2.gameObject.name} ↔ {_recentNode1.gameObject.name}\n" +
                                            $"Press 'E' for edge, or click a third node for face.";
                    edgeCreationStatus.color = Color.green;
                }
                else
                {
                    edgeCreationStatus.text = $"3 nodes: {_recentNode3.gameObject.name}, " +
                                            $"{_recentNode2.gameObject.name}, {_recentNode1.gameObject.name}\n" +
                                            $"Press 'E' for edge (last 2) or 'F' for face (all 3).";
                    edgeCreationStatus.color = Color.cyan;
                }
            }
        }
        
        private bool TryParseNodeCoordinates(out Vector3 position)
        {
            position = Vector3.zero;
            
            if (nodeXInput == null || nodeYInput == null || nodeZInput == null)
                return false;
            
            if (!float.TryParse(nodeXInput.text, out float x) ||
                !float.TryParse(nodeYInput.text, out float y) ||
                !float.TryParse(nodeZInput.text, out float z))
            {
                Debug.LogError("Failed to parse coordinates!");
                return false;
            }
            
            position = new Vector3(x, y, z);
            return true;
        }
        
        private void InitializeNode(MicrostructureNode node, NodeData data, Vector3 worldPos, float radius)
        {
            var method = typeof(MicrostructureNode).GetMethod("Initialise", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
            
            if (method != null)
                method.Invoke(node, new object[] { data, radius, worldPos });
            else
            {
                Debug.LogWarning("Could not call Initialise method. Manually setting up node.");
                node.SetPosition(worldPos);
                node.SetRadius(radius);
            }
        }
        
        private void UpdateStatus(string message, bool isError = false)
        {
            if (statusText != null)
            {
                statusText.text = message;
                statusText.color = isError ? Color.red : Color.green;
            }
            Debug.Log($"[GraphManager] {message}");
        }
        
        private void UpdateDeleteModeUI()
        {
            if (deleteModeText != null)
            {
                deleteModeText.text = _deleteMode ? "DELETE MODE: ON" : "Delete Mode";
                deleteModeText.color = _deleteMode ? Color.red : Color.white;
            }
            
            if (deleteModeButton != null)
            {
                var colors = deleteModeButton.colors;
                colors.normalColor = _deleteMode ? new Color(1f, 0.5f, 0.5f) : Color.white;
                deleteModeButton.colors = colors;
            }
        }
        
        #endregion
        
        #region Input Handling
        
        private void Update()
        {
            if (Input.GetKeyDown(deleteModeKey))
                ToggleDeleteMode();
            
            if (Input.GetKeyDown(edgeCreationKey))
            {
                Debug.Log($"=== {edgeCreationKey} KEY PRESSED DETECTED! ===");
                CreateEdgeFromRecentNodes();
            }

            if (Input.GetKeyDown(faceCreationKey))
            {
                Debug.Log($"=== {faceCreationKey} KEY PRESSED DETECTED! ===");
                CreateFaceFromRecentNodes();
            }

            if (Input.GetKeyDown(deleteKey))
            {
                Debug.Log($"=== {deleteKey} KEY PRESSED DETECTED! ===");
                if (_selectedNode != null || _selectedEdge != null)
                    DeleteSelected();
                else
                    Debug.Log("No node or edge selected to delete");
            }
        }
        
        #endregion
    }
}