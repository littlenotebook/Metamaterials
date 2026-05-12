using UnityEngine.InputSystem;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Microstructure
{
   [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
   public class MicrostructureNode : MonoBehaviour
   {
       [Header("Appearance")]
       private float radius = 0.05f;
       [SerializeField] private Material normalMaterial;
       [SerializeField] private Material hoverMaterial;
       [SerializeField] private Material raycastMaterial;

       [Header("Axis Arrow Drag Materials")]
       [Tooltip("Assign a bright material (e.g. yellow/orange) to highlight the active drag arrow.")]
       [SerializeField] private Material highlightedArrowMaterial;
      
       [Header("Transform Controls")]
       [SerializeField] private bool enableGizmos = true;
       [SerializeField] private Color gizmoColor = Color.yellow;
       [SerializeField] private float gizmoSize = 0.1f;
      
       [Header("Interaction Settings")]
       [SerializeField] private bool enableHoverEffects = true;
       [SerializeField] private Color hoverColor = Color.cyan;
       [SerializeField] private float hoverScaleMultiplier = 1.0f;
       [SerializeField] private bool showAxesOnHover = true;
       [SerializeField] private float axisArrowLength = 0.3f;
       [SerializeField] private float axisArrowThickness = 0.02f;

       [Header("Selection")]
       [SerializeField] private Material selectedMaterial;
       private bool _isSelected = false;
       private Material _originalSelectedMaterial;
       private Vector3 _originalSelectedScale;

       private bool _isMirrored = false;
       private Dictionary<int, MicrostructureNode> _mirrorsByOctant
           = new Dictionary<int, MicrostructureNode>();

       public void SetMirrored(bool mirrored) => _isMirrored = mirrored;
       public bool IsMirrored => _isMirrored;

       public void AddMirror(int octantIndex, MicrostructureNode node)
           => _mirrorsByOctant[octantIndex] = node;

       public MicrostructureNode GetMirror(int octantIndex)
           => _mirrorsByOctant.TryGetValue(octantIndex, out var n) ? n : null;

       // ── Public properties ────────────────────────────────────────────────

       public float Radius
       {
           get => radius;
           set { radius = Mathf.Max(0.001f, value); UpdateScale(); }
       }

       public NodeData Data { get; private set; }
       public Vector3 WorldPosition { get; private set; }
       public bool IsBoundary => Data?.mirror_axes != null && Data.mirror_axes.Count > 0;
      
       public float PositionX
       {
           get => transform.position.x;
           set => SetPosition(new Vector3(value, transform.position.y, transform.position.z));
       }
       public float PositionY
       {
           get => transform.position.y;
           set => SetPosition(new Vector3(transform.position.x, value, transform.position.z));
       }
       public float PositionZ
       {
           get => transform.position.z;
           set => SetPosition(new Vector3(transform.position.x, transform.position.y, value));
       }

       // ── Private state ────────────────────────────────────────────────────

       private MeshFilter   _mf;
       private MeshRenderer _mr;
       private Collider     _collider;
       private Mesh         _instanceSphereMesh;
      
       private bool     _isHovered;
       private Vector3  _originalScale;
       private Color    _originalColor;
       private Material _originalMaterial;
      
       // Arrow GameObjects
       private GameObject _xAxisArrow;
       private GameObject _yAxisArrow;
       private GameObject _zAxisArrow;

       // Per-axis base materials (runtime-created)
       private Material _xAxisMaterial;
       private Material _yAxisMaterial;
       private Material _zAxisMaterial;

       // Per-arrow box colliders for drag-click detection
       private BoxCollider _xArrowCollider;
       private BoxCollider _yArrowCollider;
       private BoxCollider _zArrowCollider;
       
       // Raycast highlight state
       private bool     _isRaycastHighlighted = false;
       public bool IsRaycastHighlighted => _isRaycastHighlighted;
       private Material _materialBeforeRaycast;

       // ── Arrow Drag State ─────────────────────────────────────────────────

       public enum DragAxis { None, X, Y, Z }

       private DragAxis   _activeDragAxis    = DragAxis.None;
       private bool       _isDragging        = false;
       private Vector3    _dragStartMouseWorld;
       private Vector3    _dragStartNodePos;
       private GameObject _highlightedArrowObj;

       // ── Unity lifecycle ──────────────────────────────────────────────────

       private void Awake()
       {
           GetReferences();
           CreateAxisMaterials();
       }

       private void Start()
       {
           EnsureMesh();
           EnsureCollider();
           UpdateScale();
           _originalScale         = transform.localScale;
           _originalSelectedScale = transform.localScale;
          
           if (_mr != null && _mr.material != null)
           {
               _originalMaterial = _mr.material;
               if (_originalMaterial.HasProperty("_Color"))
                   _originalColor = _originalMaterial.color;
           }
           SetAxesVisible(false);
       }
       
       // ── Selection ────────────────────────────────────────────────────────

       public void SetSelected(bool selected)
       {
           _isSelected = selected;
           if (_isRaycastHighlighted) return;
           if (selected)
           {
               if (selectedMaterial != null && _mr != null)
               {
                   _originalSelectedMaterial = _mr.material;
                   _mr.material = selectedMaterial;
               }
               transform.localScale = _originalSelectedScale * 1.1f;
           }
           else
           {
               if (_mr != null && _originalSelectedMaterial != null && !_isRaycastHighlighted)
                   _mr.material = _originalSelectedMaterial;
               transform.localScale = _originalSelectedScale;
           }
       }

       public bool IsSelected() => _isSelected;

       public void ToggleRaycastHighlight()
       {
           if (!enabled || _mr == null || raycastMaterial == null) return;
           if (_isRaycastHighlighted)
           {
               RestoreMaterialAfterRaycast();
               SetAxesVisible(false);
               StopDrag();
           }
           else
           {
               _materialBeforeRaycast = _mr.material;
               _mr.material = raycastMaterial;
               _isRaycastHighlighted = true;
               SetAxesVisible(true);    // show arrows immediately on selection
               StartCoroutine(PulseScale());
           }
       }
       
       private IEnumerator PulseScale()
       {
           Vector3 orig  = transform.localScale;
           Vector3 big   = orig * 1.2f;
           float   dur   = 0.05f;
           for (float t = 0; t < dur; t += Time.deltaTime)
           { transform.localScale = Vector3.Lerp(orig, big, t / dur); yield return null; }
           for (float t = 0; t < dur; t += Time.deltaTime)
           { transform.localScale = Vector3.Lerp(big, orig, t / dur); yield return null; }
           transform.localScale = orig;
       }

       private void UpdateScale()
       {
           float s = radius;
           transform.localScale   = new Vector3(s, s, s);
           _originalScale         = transform.localScale;
           _originalSelectedScale = transform.localScale;
       }

       private void RestoreMaterialAfterRaycast()
       {
           if (!_isRaycastHighlighted) return;
           Material mat = _materialBeforeRaycast;
           if      (_isSelected && selectedMaterial != null)                  mat = selectedMaterial;
           else if (_isHovered && enableHoverEffects && hoverMaterial != null) mat = hoverMaterial;
           else if (normalMaterial != null)                                    mat = normalMaterial;
           if (_mr != null && mat != null) _mr.material = mat;
           _isRaycastHighlighted = false;
       }

       // ── Initialise ───────────────────────────────────────────────────────

       public void Initialise(NodeData data, float scaledRadius, Vector3 worldPos)
       {
           Data          = data;
           WorldPosition = worldPos;
           radius        = Mathf.Max(0.001f, scaledRadius);

           GetReferences();
           EnsureMesh();
           EnsureCollider();

           if (normalMaterial != null && _mr != null)
           {
               _mr.material      = normalMaterial;
               _originalMaterial = normalMaterial;
           }

           transform.position = worldPos;
           UpdateScale();

           CreateAxisArrows();
           SetAxesVisible(false);
       }

       // ── Public API ───────────────────────────────────────────────────────

       public void Translate(Vector3 delta) => transform.position += delta;
       public void SetPosition(Vector3 pos)  => transform.position  = pos;
       public void SetPositionX(float x)     => PositionX = x;
       public void SetPositionY(float y)     => PositionY = y;
       public void SetPositionZ(float z)     => PositionZ = z;
       public void SetRadius(float r)        => Radius    = r;

       // ── Internal helpers ─────────────────────────────────────────────────

       private void GetReferences()
       {
           if (_mf       == null) _mf       = GetComponent<MeshFilter>();
           if (_mr       == null) _mr       = GetComponent<MeshRenderer>();
           if (_collider == null) _collider = GetComponent<Collider>();
       }

       private void EnsureCollider()
       {
           if (_collider == null)
           {
               var sc    = gameObject.AddComponent<SphereCollider>();
               sc.radius    = 0.5f;
               sc.isTrigger = true;
               _collider    = sc;
           }
       }

       private void CreateAxisMaterials()
       {
           _xAxisMaterial = new Material(Shader.Find("Standard")) { color = Color.red,   name = "XAxisMaterial" };
           _yAxisMaterial = new Material(Shader.Find("Standard")) { color = Color.green, name = "YAxisMaterial" };
           _zAxisMaterial = new Material(Shader.Find("Standard")) { color = Color.blue,  name = "ZAxisMaterial" };
       }

       // ── Cone mesh ────────────────────────────────────────────────────────

       /// <summary>
       /// Builds a unit cone: base at y=0 (radius 1), tip at y=1.
       /// Callers scale it to the desired head size.
       /// </summary>
       private static Mesh CreateConeMesh(int segments = 16)
       {
           var mesh  = new Mesh { name = "ArrowCone" };
           var verts = new List<Vector3>();
           var norms = new List<Vector3>();
           var tris  = new List<int>();

           // ── Side faces ────────────────────────────────────────────────────
           // Each side triangle has its own vertices for correct normals.
           for (int i = 0; i < segments; i++)
           {
               float a0 = (i      / (float)segments) * Mathf.PI * 2f;
               float a1 = ((i + 1)/ (float)segments) * Mathf.PI * 2f;

               Vector3 b0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
               Vector3 b1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
               Vector3 tip = Vector3.up;

               // Outward-leaning normals for the side
               Vector3 edge0 = tip - b0;
               Vector3 edge1 = b1  - b0;
               Vector3 faceN = Vector3.Cross(edge1, edge0).normalized;

               int baseIdx = verts.Count;
               verts.Add(b0);  norms.Add(faceN);
               verts.Add(b1);  norms.Add(faceN);
               verts.Add(tip); norms.Add(faceN);
               tris.AddRange(new[] { baseIdx, baseIdx + 1, baseIdx + 2 });
           }

           // ── Base cap ──────────────────────────────────────────────────────
           int baseCentreIdx = verts.Count;
           verts.Add(Vector3.zero); norms.Add(Vector3.down);

           int baseRingStart = verts.Count;
           for (int i = 0; i < segments; i++)
           {
               float a = i / (float)segments * Mathf.PI * 2f;
               verts.Add(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)));
               norms.Add(Vector3.down);
           }
           for (int i = 0; i < segments; i++)
           {
               int cur  = baseRingStart + i;
               int next = baseRingStart + (i + 1) % segments;
               tris.AddRange(new[] { baseCentreIdx, next, cur });
           }

           mesh.SetVertices(verts);
           mesh.SetNormals(norms);
           mesh.SetTriangles(tris, 0);
           mesh.RecalculateBounds();
           return mesh;
       }

       // ── Arrow creation ───────────────────────────────────────────────────

       /// <summary>
       /// Creates a cylinder shaft + cone head arrow GameObject, parented to
       /// this node, pointing along <paramref name="direction"/> in local space.
       /// Adds a single BoxCollider on the root for drag-click detection.
       /// </summary>
       private GameObject CreateArrow(Vector3 direction, Material mat,
                                      string arrowName, out BoxCollider col)
       {
           // Sizes are in the node's LOCAL space, so they scale with the node sphere.
           // We work in node-local units where the sphere has radius 0.5 (unit sphere).
           // axisArrowLength / axisArrowThickness are set in world-ish units on the
           // prefab, but the arrows are children of the node whose localScale = radius,
           // so divide by radius to keep arrow sizes consistent regardless of node size.
           float invR     = radius > 0.0001f ? 1f / radius : 1f;
           float shaftLen = axisArrowLength * invR * 0.65f;
           float headLen  = axisArrowLength * invR * 0.35f;
           float shaftR   = axisArrowThickness * invR;
           float headR    = axisArrowThickness * invR * 3.5f;

           var root = new GameObject(arrowName);
           root.transform.SetParent(transform, false);
           root.transform.localPosition = Vector3.zero;
           root.transform.localRotation = Quaternion.identity;
           root.transform.localScale    = Vector3.one;

           Quaternion rot = Quaternion.FromToRotation(Vector3.up, direction);

           // ── Shaft ─────────────────────────────────────────────────────────
           var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
           shaft.name = "Shaft";
           Destroy(shaft.GetComponent<Collider>());
           shaft.transform.SetParent(root.transform, false);
           shaft.transform.localRotation = rot;
           shaft.transform.localPosition = direction * (shaftLen * 0.5f);
           // Cylinder primitive is 2 units tall (−1 to +1), so half-height = shaftLen/2
           shaft.transform.localScale    = new Vector3(shaftR, shaftLen * 0.5f, shaftR);
           shaft.GetComponent<MeshRenderer>().material = mat;

           // ── Head (cone) ───────────────────────────────────────────────────
           var head = new GameObject("Head");
           head.transform.SetParent(root.transform, false);
           // Cone tip points along +Y; rotate so +Y aligns with direction
           head.transform.localRotation = rot;
           // Base of cone sits at top of shaft
           head.transform.localPosition = direction * shaftLen;
           // Scale: X/Z = headR (radius), Y = headLen (height of unit cone)
           head.transform.localScale    = new Vector3(headR, headLen, headR);

           var headMf = head.AddComponent<MeshFilter>();
           var headMr = head.AddComponent<MeshRenderer>();
           headMf.mesh       = CreateConeMesh();
           headMr.material   = mat;

           // ── BoxCollider (local space, covers full shaft + head) ───────────
           col = root.AddComponent<BoxCollider>();
           // Centre: halfway along the arrow
           col.center = direction * ((shaftLen + headLen) * 0.5f);
           // The axis-aligned bounding size: fat enough to be clickable
           float clickW = headR * 2.5f;
           // Build size vector: wide in perpendicular axes, full length along arrow axis
           Vector3 size = new Vector3(clickW, clickW, clickW)
                          + direction * (shaftLen + headLen);
           col.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));

           return root;
       }

       private void DestroyAxisArrows()
       {
           if (_xAxisArrow != null) Destroy(_xAxisArrow);
           if (_yAxisArrow != null) Destroy(_yAxisArrow);
           if (_zAxisArrow != null) Destroy(_zAxisArrow);
           _xAxisArrow = _yAxisArrow = _zAxisArrow = null;
           _xArrowCollider = _yArrowCollider = _zArrowCollider = null;
       }

       private void CreateAxisArrows()
       {
           if (!showAxesOnHover) return;
           DestroyAxisArrows();
           _xAxisArrow = CreateArrow(Vector3.right,   _xAxisMaterial, "XAxis", out _xArrowCollider);
           _yAxisArrow = CreateArrow(Vector3.up,       _yAxisMaterial, "YAxis", out _yArrowCollider);
           _zAxisArrow = CreateArrow(Vector3.forward,  _zAxisMaterial, "ZAxis", out _zArrowCollider);
       }

       private void SetAxesVisible(bool visible)
       {
           if (_xAxisArrow != null) _xAxisArrow.SetActive(visible);
           if (_yAxisArrow != null) _yAxisArrow.SetActive(visible);
           if (_zAxisArrow != null) _zAxisArrow.SetActive(visible);
       }

       private void EnsureMesh()
       {
           GetReferences();
           if (_mf.sharedMesh != null) return;
           _instanceSphereMesh = CreateSphereMesh();
           _mf.mesh            = _instanceSphereMesh;
           if (normalMaterial != null && _mr != null)
               _mr.material = normalMaterial;
       }

       private static Mesh CreateSphereMesh()
       {
           var go   = GameObject.CreatePrimitive(PrimitiveType.Sphere);
           var mesh = Instantiate(go.GetComponent<MeshFilter>().sharedMesh);
           mesh.name = "MicrostructureNode_DefaultSphere";
           if (Application.isPlaying) Destroy(go);
           else DestroyImmediate(go);
           return mesh;
       }

       // ── Mouse interaction ────────────────────────────────────────────────

       private void OnMouseEnter()
       {
           if (!enableHoverEffects || _isRaycastHighlighted) return;
           _isHovered = true;
           if (_mr != null)
               _mr.material = hoverMaterial != null
                   ? hoverMaterial
                   : new Material(_mr.material) { color = hoverColor };
           transform.localScale = _originalScale * hoverScaleMultiplier;
           if (showAxesOnHover) SetAxesVisible(true);
       }

       private void OnMouseExit()
       {
           if (!enableHoverEffects || _isRaycastHighlighted) return;
           _isHovered = false;
           if (_mr != null && !_isSelected)
               _mr.material = normalMaterial != null ? normalMaterial : _originalMaterial;
           transform.localScale = _originalScale;
           if (showAxesOnHover) SetAxesVisible(false);
       }

       // ── Editor ───────────────────────────────────────────────────────────

       private void OnValidate()
       {
           if (Application.isPlaying) return;
           GetReferences();
           if (normalMaterial != null && _mr != null && _mr.sharedMaterial != normalMaterial)
               _mr.sharedMaterial = normalMaterial;
       }

       private void OnDrawGizmos()
       {
           if (!enableGizmos) return;
           Vector3 pos = transform.position;
           if (_isHovered && showAxesOnHover)
           {
               Gizmos.color = Color.red;
               Gizmos.DrawLine(pos, pos + Vector3.right   * axisArrowLength);
               Gizmos.DrawSphere(pos + Vector3.right   * axisArrowLength, gizmoSize * radius * 0.45f);
               Gizmos.color = Color.green;
               Gizmos.DrawLine(pos, pos + Vector3.up      * axisArrowLength);
               Gizmos.DrawSphere(pos + Vector3.up      * axisArrowLength, gizmoSize * radius * 0.45f);
               Gizmos.color = Color.blue;
               Gizmos.DrawLine(pos, pos + Vector3.forward * axisArrowLength);
               Gizmos.DrawSphere(pos + Vector3.forward * axisArrowLength, gizmoSize * radius * 0.45f);
           }
           else
           {
               float h = gizmoSize * radius * 0.5f;
               Gizmos.color = Color.red;   Gizmos.DrawSphere(pos + Vector3.right   * radius, h);
               Gizmos.color = Color.green; Gizmos.DrawSphere(pos + Vector3.up      * radius, h);
               Gizmos.color = Color.blue;  Gizmos.DrawSphere(pos + Vector3.forward * radius, h);
           }
           Gizmos.color = _isHovered ? hoverColor : gizmoColor;
           Gizmos.DrawWireSphere(pos, radius);
       }

       // ── Cleanup ──────────────────────────────────────────────────────────

       private void OnDestroy()
       {
           DestroyAxisArrows();
           if (_xAxisMaterial != null) Destroy(_xAxisMaterial);
           if (_yAxisMaterial != null) Destroy(_yAxisMaterial);
           if (_zAxisMaterial != null) Destroy(_zAxisMaterial);
           if (_instanceSphereMesh != null)
           {
               if (Application.isPlaying) Destroy(_instanceSphereMesh);
               else DestroyImmediate(_instanceSphereMesh);
           }
       }

       // ── Drag helpers ─────────────────────────────────────────────────────

       private bool GetDragWorldPoint(Ray ray, Vector3 axisDir,
                                      Vector3 planePoint, out Vector3 worldPoint)
       {
           worldPoint = Vector3.zero;
           // Build a plane whose normal is as perpendicular to axisDir as possible,
           // oriented toward the camera so the projection is stable.
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

       private static Vector3 AxisToDirection(DragAxis axis) => axis switch
       {
           DragAxis.X => Vector3.right,
           DragAxis.Y => Vector3.up,
           DragAxis.Z => Vector3.forward,
           _          => Vector3.zero,
       };

       private void ApplyHighlightToArrow(GameObject arrowObj)
       {
           if (arrowObj == null || highlightedArrowMaterial == null) return;
           _highlightedArrowObj = arrowObj;
           foreach (var r in arrowObj.GetComponentsInChildren<MeshRenderer>())
               r.material = highlightedArrowMaterial;
       }

       private void RestoreArrowMaterial(GameObject arrowObj)
       {
           if (arrowObj == null) return;
           Material baseMat = null;
           if      (arrowObj == _xAxisArrow) baseMat = _xAxisMaterial;
           else if (arrowObj == _yAxisArrow) baseMat = _yAxisMaterial;
           else if (arrowObj == _zAxisArrow) baseMat = _zAxisMaterial;
           if (baseMat == null) return;
           foreach (var r in arrowObj.GetComponentsInChildren<MeshRenderer>())
               r.material = baseMat;
           _highlightedArrowObj = null;
       }

       private void StopDrag()
       {
           if (_highlightedArrowObj != null)
               RestoreArrowMaterial(_highlightedArrowObj);
           _isDragging     = false;
           _activeDragAxis = DragAxis.None;
       }

       // ── Update ───────────────────────────────────────────────────────────

       private void Update()
       {
           if (Camera.main == null || Mouse.current == null) return;

           Vector2 mousePos = Mouse.current.position.ReadValue();
           Ray ray = Camera.main.ScreenPointToRay(mousePos);

           // ── Mouse DOWN ───────────────────────────────────────────────────
           if (Mouse.current.leftButton.wasPressedThisFrame)
           {
               // Test arrow colliders first (only when selected & not a mirror)
               if (_isRaycastHighlighted && !_isMirrored)
               {
                   DragAxis   hitAxis     = DragAxis.None;
                   GameObject hitArrowObj = null;

                   if (_xArrowCollider != null && _xAxisArrow != null && _xAxisArrow.activeSelf
                       && _xArrowCollider.bounds.IntersectRay(ray))
                   { hitAxis = DragAxis.X; hitArrowObj = _xAxisArrow; }

                   if (hitAxis == DragAxis.None
                       && _yArrowCollider != null && _yAxisArrow != null && _yAxisArrow.activeSelf
                       && _yArrowCollider.bounds.IntersectRay(ray))
                   { hitAxis = DragAxis.Y; hitArrowObj = _yAxisArrow; }

                   if (hitAxis == DragAxis.None
                       && _zArrowCollider != null && _zAxisArrow != null && _zAxisArrow.activeSelf
                       && _zArrowCollider.bounds.IntersectRay(ray))
                   { hitAxis = DragAxis.Z; hitArrowObj = _zAxisArrow; }

                   if (hitAxis != DragAxis.None)
                   {
                       Debug.Log($"[{gameObject.name}] Arrow CLICKED — Axis: {hitAxis}", this);
                       InputGuard.ConsumeClick(Time.frameCount);

                       _activeDragAxis       = hitAxis;
                       _isDragging           = true;
                       _dragStartNodePos     = transform.position;

                       Vector3 axisDir = AxisToDirection(hitAxis);
                       GetDragWorldPoint(ray, axisDir, transform.position,
                                         out _dragStartMouseWorld);

                       if (_highlightedArrowObj != null)
                           RestoreArrowMaterial(_highlightedArrowObj);
                       ApplyHighlightToArrow(hitArrowObj);

                       return; // don't also fire node-body click this frame
                   }
               }

               // Node body click
               if (InputGuard.IsClickConsumed(Time.frameCount)) return;

               if (Physics.Raycast(ray, out RaycastHit hit)
                   && hit.collider.gameObject == gameObject)
               {
                   if (_isMirrored) return;

                   Debug.Log($"Raycast hit {gameObject.name} - Toggling highlight!", this);
                   ToggleRaycastHighlight();

                   if (GraphManager.Instance != null)
                       GraphManager.Instance.OnNodeClickedFromNode(this, _isRaycastHighlighted);
                   else
                       Debug.LogWarning($"[{gameObject.name}] GraphManager.Instance is null!", this);
               }
           }

           // ── Mouse HELD — drag ────────────────────────────────────────────
           if (_isDragging && Mouse.current.leftButton.isPressed)
           {
               Vector3 axisDir = AxisToDirection(_activeDragAxis);
               if (GetDragWorldPoint(ray, axisDir, _dragStartNodePos, out Vector3 currentWorld))
               {
                   float   proj    = Vector3.Dot(currentWorld - _dragStartMouseWorld, axisDir);
                   Vector3 newPos  = _dragStartNodePos + axisDir * proj;
                   Vector3 delta   = newPos - transform.position;
                   transform.position = newPos;

                   // Only notify GraphManager for mirror propagation — edges/faces
                   // rebuild themselves via MicrostructureEdge.Update() which already
                   // watches _sourceNode and _targetNode positions every frame.
                   if (delta.sqrMagnitude > 1e-10f && GraphManager.Instance != null)
                       GraphManager.Instance.OnNodeDragged(this, delta);
               }
           }

           // ── Mouse UP ─────────────────────────────────────────────────────
           if (Mouse.current.leftButton.wasReleasedThisFrame && _isDragging)
           {
               Debug.Log($"[{gameObject.name}] Drag ENDED — axis: {_activeDragAxis}, " +
                         $"final pos: {transform.position}", this);
               StopDrag();
           }
       }
   }
}