// using UnityEngine;
// using UnityEngine.InputSystem;

// namespace Microstructure
// {
//     /// <summary>
//     /// Attached to a MicrostructureNode. Handles axis-arrow dragging,
//     /// mirrors translation to all octant copies, and notifies connected
//     /// edges and faces to rebuild.
//     /// </summary>
//     public class NodeDragHandler : MonoBehaviour
//     {
//         [Header("Arrow Materials")]
//         [SerializeField] public Material xNormalMaterial;
//         [SerializeField] public Material yNormalMaterial;
//         [SerializeField] public Material zNormalMaterial;
//         [SerializeField] public Material highlightedArrowMaterial;

//         [Header("Arrow Settings")]
//         [SerializeField] private float arrowLength   = 0.5f;
//         [SerializeField] private float arrowThickness = 0.04f;
//         [SerializeField] private float hitRadius      = 0.12f;

//         // ── State ─────────────────────────────────────────────────────────────
//         private MicrostructureNode _node;
//         private bool   _isDragging     = false;
//         private int    _dragAxis       = -1; // 0=X, 1=Y, 2=Z
//         private float  _dragStartWorld = 0f;
//         private Vector3 _nodeStartPos;

//         // Arrow GameObjects
//         private GameObject _xArrow, _yArrow, _zArrow;
//         private Renderer   _xRend,  _yRend,  _zRend;

//         // Axis directions in world space
//         private static readonly Vector3[] AxisDirs =
//             { Vector3.right, Vector3.up, Vector3.forward };
//         private static readonly string[]  AxisNames = { "X", "Y", "Z" };

//         // ── Init ──────────────────────────────────────────────────────────────

//         public void Initialise(MicrostructureNode node)
//         {
//             _node = node;
//             BuildArrows();
//             SetArrowsVisible(false);
//         }

//         public void ShowArrows(bool visible) => SetArrowsVisible(visible);

//         // ── Arrow construction ────────────────────────────────────────────────

//         private void BuildArrows()
//         {
//             DestroyArrows();
//             _xArrow = BuildArrow(Vector3.right,   xNormalMaterial, "DragArrow_X");
//             _yArrow = BuildArrow(Vector3.up,       yNormalMaterial, "DragArrow_Y");
//             _zArrow = BuildArrow(Vector3.forward,  zNormalMaterial, "DragArrow_Z");

//             _xRend = GetArrowRenderer(_xArrow);
//             _yRend = GetArrowRenderer(_yArrow);
//             _zRend = GetArrowRenderer(_zArrow);
//         }

//         private GameObject BuildArrow(Vector3 dir, Material mat, string name)
//         {
//             var root = new GameObject(name);
//             root.transform.SetParent(transform, false);
//             root.transform.localPosition = Vector3.zero;

//             Quaternion rot = Quaternion.FromToRotation(Vector3.up, dir);

//             // Shaft
//             var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
//             shaft.name = "Shaft";
//             shaft.transform.SetParent(root.transform, false);
//             shaft.transform.localRotation = rot;
//             shaft.transform.localScale    = new Vector3(
//                 arrowThickness, arrowLength * 0.5f, arrowThickness);
//             shaft.transform.localPosition = dir * (arrowLength * 0.5f);
//             Destroy(shaft.GetComponent<Collider>());
//             if (mat != null) shaft.GetComponent<Renderer>().material = mat;

//             // Head (cone approximated by a tapered cylinder)
//             var head = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
//             head.name = "Head";
//             head.transform.SetParent(root.transform, false);
//             head.transform.localRotation = rot;
//             head.transform.localScale    = new Vector3(
//                 arrowThickness * 3f, arrowLength * 0.15f, arrowThickness * 3f);
//             head.transform.localPosition = dir * (arrowLength * 1.05f);
//             Destroy(head.GetComponent<Collider>());
//             if (mat != null) head.GetComponent<Renderer>().material = mat;

//             return root;
//         }

//         private Renderer GetArrowRenderer(GameObject arrow)
//         {
//             // Return shaft renderer (first child)
//             if (arrow == null) return null;
//             var renderers = arrow.GetComponentsInChildren<Renderer>();
//             return renderers.Length > 0 ? renderers[0] : null;
//         }

//         private void SetArrowMaterials(int arrowIdx, Material mat)
//         {
//             var arrow = arrowIdx == 0 ? _xArrow : arrowIdx == 1 ? _yArrow : _zArrow;
//             if (arrow == null) return;
//             foreach (var r in arrow.GetComponentsInChildren<Renderer>())
//                 r.material = mat;
//         }

//         private void ResetArrowMaterial(int arrowIdx)
//         {
//             Material mat = arrowIdx == 0 ? xNormalMaterial
//                          : arrowIdx == 1 ? yNormalMaterial
//                          : zNormalMaterial;
//             SetArrowMaterials(arrowIdx, mat);
//         }

//         private void DestroyArrows()
//         {
//             if (_xArrow != null) Destroy(_xArrow);
//             if (_yArrow != null) Destroy(_yArrow);
//             if (_zArrow != null) Destroy(_zArrow);
//         }

//         private void SetArrowsVisible(bool v)
//         {
//             if (_xArrow != null) _xArrow.SetActive(v);
//             if (_yArrow != null) _yArrow.SetActive(v);
//             if (_zArrow != null) _zArrow.SetActive(v);
//         }

//         // ── Update ────────────────────────────────────────────────────────────

//         private void Update()
//         {
//             if (!Application.isPlaying) return;
//             if (Camera.main == null) return;

//             var mouse = Mouse.current;
//             if (mouse == null) return;

//             Vector2 mousePos = mouse.position.ReadValue();

//             if (mouse.leftButton.wasPressedThisFrame)
//                 TryBeginDrag(mousePos);

//             if (_isDragging && mouse.leftButton.isPressed)
//                 ContinueDrag(mousePos);

//             if (_isDragging && mouse.leftButton.wasReleasedThisFrame)
//                 EndDrag();
//         }

//         // ── Drag logic ────────────────────────────────────────────────────────

//         private void TryBeginDrag(Vector2 mousePos)
//         {
//             Ray ray = Camera.main.ScreenPointToRay(mousePos);
//             float bestDist = float.MaxValue;
//             int   bestAxis = -1;

//             for (int a = 0; a < 3; a++)
//             {
//                 GameObject arrow = a == 0 ? _xArrow : a == 1 ? _yArrow : _zArrow;
//                 if (arrow == null || !arrow.activeInHierarchy) continue;

//                 Vector3 arrowTip    = transform.position + AxisDirs[a] * arrowLength;
//                 Vector3 arrowOrigin = transform.position;

//                 float dist = DistanceRayToSegment(ray, arrowOrigin, arrowTip);
//                 if (dist < hitRadius && dist < bestDist)
//                 {
//                     bestDist = dist;
//                     bestAxis = a;
//                 }
//             }

//             if (bestAxis < 0) return;

//             // Consume click so camera doesn't rotate
//             InputGuard.ConsumeClick(Time.frameCount);

//             _isDragging     = true;
//             _dragAxis       = bestAxis;
//             _nodeStartPos   = transform.position;
//             _dragStartWorld = ProjectMouseOnAxis(mousePos, _dragAxis);

//             // Highlight the selected arrow
//             SetArrowMaterials(_dragAxis, highlightedArrowMaterial);

//             Debug.Log($"[NodeDragHandler] {gameObject.name} — " +
//                       $"drag BEGIN on axis {AxisNames[_dragAxis]}, " +
//                       $"startPos: {_nodeStartPos}, startProj: {_dragStartWorld:F3}");
//         }

//         private void ContinueDrag(Vector2 mousePos)
//         {
//             float currentProj = ProjectMouseOnAxis(mousePos, _dragAxis);
//             float delta       = currentProj - _dragStartWorld;

//             Vector3 newPos = _nodeStartPos + AxisDirs[_dragAxis] * delta;
//             MoveNodeAndMirrors(newPos);
//         }

//         private void EndDrag()
//         {
//             Debug.Log($"[NodeDragHandler] {gameObject.name} — " +
//                       $"drag END on axis {AxisNames[_dragAxis]}, " +
//                       $"final pos: {transform.position}");

//             ResetArrowMaterial(_dragAxis);
//             _isDragging = false;
//             _dragAxis   = -1;
//         }

//         // ── Node + mirror movement ────────────────────────────────────────────

//         private void MoveNodeAndMirrors(Vector3 newWorldPos)
//         {
//             // Move this node
//             transform.position = newWorldPos;
//             _node.WorldPosition = newWorldPos;

//             // Mirror to all other octants
//             if (OctantMirrorSystem.Instance != null && !_node.IsMirrored)
//             {
//                 for (int i = 0; i < 8; i++)
//                 {
//                     if (i == OctantMirrorSystem.Instance.activeOctantIndex) continue;
//                     var mirror = _node.GetMirror(i);
//                     if (mirror == null) continue;
//                     Vector3 mirroredPos =
//                         OctantMirrorSystem.Instance.MirrorPosition(newWorldPos, i);
//                     mirror.transform.position = mirroredPos;
//                     mirror.WorldPosition      = mirroredPos;

//                     // Rebuild edges/faces connected to the mirror
//                     NotifyConnectedElements(mirror);
//                 }
//             }

//             // Rebuild edges/faces connected to this node
//             NotifyConnectedElements(_node);
//         }

//         private void NotifyConnectedElements(MicrostructureNode node)
//         {
//             if (GraphManager.Instance == null) return;

//             // Edges — SetNodes triggers position update next frame via Update()
//             var edges = GraphManager.Instance.GetEdgesConnectedToNode(node);
//             foreach (var edge in edges)
//             {
//                 if (edge == null) continue;
//                 // Update endpoint in BezierPts and rebuild
//                 edge.OnEndpointMoved();
//             }

//             // Faces — rebuild mesh from updated corner positions
//             var faces = GraphManager.Instance.GetFacesConnectedToNode(node);
//             foreach (var face in faces)
//             {
//                 if (face == null) continue;
//                 face.OnCornerMoved(node);
//             }
//         }

//         // ── Math helpers ──────────────────────────────────────────────────────

//         /// <summary>
//         /// Projects the mouse ray onto the drag axis line and returns
//         /// the signed distance along the axis from the node's start position.
//         /// </summary>
//         private float ProjectMouseOnAxis(Vector2 mousePos, int axis)
//         {
//             Ray     ray      = Camera.main.ScreenPointToRay(mousePos);
//             Vector3 axisDir  = AxisDirs[axis];
//             Vector3 axisOrig = _nodeStartPos;

//             // Closest point on the axis line to the mouse ray
//             // Using parametric closest-point-between-two-lines formula
//             Vector3 w  = ray.origin - axisOrig;
//             float   a  = Vector3.Dot(axisDir, axisDir);   // always 1
//             float   b  = Vector3.Dot(axisDir, ray.direction);
//             float   c  = Vector3.Dot(ray.direction, ray.direction); // always 1
//             float   d  = Vector3.Dot(axisDir, w);
//             float   e  = Vector3.Dot(ray.direction, w);
//             float   denom = a * c - b * b;

//             if (Mathf.Abs(denom) < 1e-6f)
//                 return _dragStartWorld; // parallel — no movement

//             float t = (b * e - c * d) / denom;
//             return t; // signed distance along axis from _nodeStartPos
//         }

//         /// <summary>
//         /// Returns the minimum distance between a ray and a line segment.
//         /// Used for arrow hit detection.
//         /// </summary>
//         private float DistanceRayToSegment(Ray ray, Vector3 segA, Vector3 segB)
//         {
//             Vector3 d1 = ray.direction.normalized;
//             Vector3 d2 = (segB - segA).normalized;
//             Vector3 r  = ray.origin - segA;

//             float   a  = Vector3.Dot(d1, d1);
//             float   e  = Vector3.Dot(d2, d2);
//             float   f  = Vector3.Dot(d2, r);
//             float   b  = Vector3.Dot(d1, d2);
//             float   c  = Vector3.Dot(d1, r);
//             float   denom = a * e - b * b;

//             float s, t;
//             if (Mathf.Abs(denom) < 1e-6f)
//             {
//                 s = 0f;
//                 t = f / e;
//             }
//             else
//             {
//                 s = (b * f - c * e) / denom;
//                 t = (a * f - b * c) / denom;
//             }

//             t = Mathf.Clamp01(t);
//             Vector3 closest1 = ray.origin + d1 * s;
//             Vector3 closest2 = segA       + d2 * (t * Vector3.Distance(segA, segB));
//             return Vector3.Distance(closest1, closest2);
//         }

//         private void OnDestroy() => DestroyArrows();
//     }
// }