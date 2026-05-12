using UnityEngine;

public class NodeInteractionController : MonoBehaviour
{
    public Camera cam;
    public Material ghostMaterial;

    GhostHandle ghost;
    Node hovered;

    public GameObject axisPrefab;

    AxisHandle[] handles = new AxisHandle[3];

    void Start()
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(g.GetComponent<Collider>());
        ghost = g.AddComponent<GhostHandle>();
        ghost.Hide();
    }

    void Update()
    {
        Hover();
    }

    void Hover()
    {
        if (!cam) return;

        Ray r = cam.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(r, out var hit))
        {
            ghost.Hide();
            hovered = null;
            HideAxes();
            return;
        }

        Node n = hit.collider.GetComponent<Node>();

        if (!n)
        {
            ghost.Hide();
            hovered = null;
            HideAxes();
            return;
        }

        if (n != hovered)
        {
            hovered = n;

            ghost.Init(n.transform, ghostMaterial);

            ShowAxes(n);
        }
    }

    void ShowAxes(Node n)
    {
        HideAxes();

        handles[0] = Make(AxisHandle.Axis.X, Vector3.right, n);
        handles[1] = Make(AxisHandle.Axis.Y, Vector3.up, n);
        handles[2] = Make(AxisHandle.Axis.Z, Vector3.forward, n);
    }

    AxisHandle Make(AxisHandle.Axis ax, Vector3 dir, Node n)
    {
        var go = Instantiate(axisPrefab);
        go.transform.position = n.transform.position;
        go.transform.forward = dir;

        var h = go.AddComponent<AxisHandle>();
        h.Init(ax, n, cam);

        return h;
    }

    void HideAxes()
    {
        foreach (var h in handles)
        {
            if (!h) continue;
            Destroy(h.gameObject);
        }
    }

    // cube is [-1,1]^3
    public static Vector3 ClampToCube(Vector3 p)
    {
        return new Vector3(
            Mathf.Clamp(p.x, 0, 2),
            Mathf.Clamp(p.y, 0, 2),
            Mathf.Clamp(p.z, 0, 2)
        );
    }
}


// using UnityEngine;
// using System.Collections.Generic;

// public class NodeInteractionController : MonoBehaviour
// {
//     public Camera cam;
//     public Material ghostMaterial;
//     public Material axisMaterial;

//     private GhostHandle ghostHandle;
//     private Node hoveredNode;
//     private Node selectedNode;
    
//     List<AxisHandle> activeAxisHandles = new List<AxisHandle>();
//     AxisHandle draggingAxis;

//     void Start()
//     {
//         ghostHandle = HandleFactory.CreateGhostHandle(ghostMaterial).AddComponent<GhostHandle>();
//         ghostHandle.gameObject.SetActive(false);
//     }

//     void Update()
//     {
//         HandleHovering();
//         HandleSelection();
//         HandleDragging();
//     }

//     void HandleHovering()
//     {
//         if (draggingAxis != null) return; // no hover while dragging

//         Ray ray = cam.ScreenPointToRay(Input.mousePosition);
//         if (Physics.Raycast(ray, out RaycastHit hit))
//         {
//             Node n = hit.collider.GetComponent<Node>();

//             if (n != null)
//             {
//                 hoveredNode = n;
//                 ghostHandle.SetTarget(n.transform);
//                 ghostHandle.gameObject.SetActive(true);
//                 ghostHandle.transform.position = n.transform.position;
//                 return;
//             }
//         }

//         hoveredNode = null;
//         ghostHandle.gameObject.SetActive(false);
//     }

//     void HandleSelection()
//     {
//         if (hoveredNode != null && Input.GetMouseButtonDown(0))
//         {
//             selectedNode = hoveredNode;
//             ShowAxisHandles(selectedNode);
//         }
//     }

//     void HandleDragging()
//     {
//         if (draggingAxis != null)
//         {
//             draggingAxis.Drag(cam);

//             if (!Input.GetMouseButton(0))
//                 draggingAxis = null;

//             return;
//         }
//     }

//     void ShowAxisHandles(Node n)
//     {
//         ClearAxisHandles();

//         Vector3 pos = n.transform.position;

//         activeAxisHandles.Add(CreateAxisHandle(n, AxisHandle.Axis.X, Color.red, Vector3.right));
//         activeAxisHandles.Add(CreateAxisHandle(n, AxisHandle.Axis.Y, Color.green, Vector3.up));
//         activeAxisHandles.Add(CreateAxisHandle(n, AxisHandle.Axis.Z, Color.blue, Vector3.forward));
//     }

//     AxisHandle CreateAxisHandle(Node node, AxisHandle.Axis axis, Color color, Vector3 dir)
//     {
//         GameObject obj = HandleFactory.CreateAxisHandle(axisMaterial, dir);
//         obj.transform.position = node.transform.position;

//         AxisHandle h = obj.AddComponent<AxisHandle>();
//         h.axis = axis;
//         h.targetNode = node;

//         // clickable collider
//         SphereCollider col = obj.AddComponent<SphereCollider>();
//         col.radius = 0.3f;

//         // set color
//         obj.GetComponent<Renderer>().material.color = color;

//         return h;
//     }

//     void ClearAxisHandles()
//     {
//         foreach (var h in activeAxisHandles)
//             Destroy(h.gameObject);

//         activeAxisHandles.Clear();
//     }

//     void OnMouseDownHandle(AxisHandle h)
//     {
//         draggingAxis = h;
//         h.BeginDrag(cam);
//     }
// }
