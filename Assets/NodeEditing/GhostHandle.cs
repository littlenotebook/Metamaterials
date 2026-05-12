using UnityEngine;

public class GhostHandle : MonoBehaviour
{
    private Transform target;
    private Renderer r;

    void Awake()
    {
        r = GetComponent<Renderer>();
    }

    public void Init(Transform node, Material mat)
    {
        if (!node || !mat) return;

        target = node;

        if (r)
            r.sharedMaterial = mat;

        transform.position = node.position;

        gameObject.SetActive(true);

        Debug.Log("Ghost init on " + node.name);
    }

    void Update()
    {
        if (target)
            transform.position = target.position;
    }

    public void Hide()
    {
        target = null;
        gameObject.SetActive(false);
    }
}


// using UnityEngine;

// public class GhostHandle : MonoBehaviour
// {
//     [Header("Visibility")]
//     public float hoverScale = 1.0f;
//     public float idleScale = 0.0f;
//     public float scaleSpeed = 10f;

//     [Header("Dragging")]
//     public float dragSensitivity = 1.0f;

//     // Automatically assigned to the same GameObject unless overridden
//     [SerializeField] private Transform targetNode;   // ALWAYS the node's transform
//     public Transform TargetNode => targetNode;       // read-only getter


//     private bool _hovered = false;
//     private bool _dragging = false;
//     private Camera _cam;

//     private Vector3 _dragPlaneNormal;
//     private float _dragPlaneDistance;
//     private Vector3 _dragOffset;

//     private Renderer _rend;
//     private void Awake()
//     {
//         _cam = Camera.main;
//         if (node == null)
//             node = transform;  // Auto bind to itself

//         _rend = GetComponent<Renderer>();
//     }

//     public void SetTarget(Transform nodeTransform)
//     {
//         targetNode = nodeTransform;
//     }
//     public void Init(Transform nodeTransform, Material ghostMat)
//     {
//         targetNode = nodeTransform;
//         _rend.material = ghostMat;
//     }

//     private void Update()
//     {
//         if (targetNode != null)
//             transform.position = targetNode.position;
//         // Handle scaling animation
//         float target = (_hovered || _dragging) ? hoverScale : idleScale;
//         transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * target, Time.deltaTime * scaleSpeed);

//         // Drag movement
//         if (_dragging)
//         {
//             if (GetMousePlanePoint(out Vector3 hit))
//                 node.position = hit + _dragOffset;
//         }


//     }

//     private bool GetMousePlanePoint(out Vector3 hit)
//     {
//         Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
//         Plane plane = new Plane(_dragPlaneNormal, _dragPlaneDistance);

//         if (plane.Raycast(ray, out float enter))
//         {
//             hit = ray.GetPoint(enter);
//             return true;
//         }

//         hit = Vector3.zero;
//         return false;
//     }

//     private void OnMouseEnter()
//     {
//         _hovered = true;
//     }

//     private void OnMouseExit()
//     {
//         _hovered = false;
//     }

//     private void OnMouseDown()
//     {
//         _dragging = true;

//         // Create a movement plane perpendicular to camera
//         _dragPlaneNormal = _cam.transform.forward;
//         _dragPlaneDistance = Vector3.Dot(_dragPlaneNormal, node.position);

//         if (GetMousePlanePoint(out Vector3 hit))
//             _dragOffset = node.position - hit;
//     }

//     private void OnMouseUp()
//     {
//         _dragging = false;
//     }
// }
