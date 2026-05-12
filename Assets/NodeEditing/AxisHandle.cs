using UnityEngine;

public class AxisHandle : MonoBehaviour
{
    public enum Axis { X, Y, Z }

    public Axis axis;
    public Node target;

    Camera cam;
    bool dragging = false;

    Vector3 axisDir;

    public void Init(Axis a, Node t, Camera c)
    {
        axis = a;
        target = t;
        cam = c;

        axisDir =
            axis == Axis.X ? Vector3.right :
            axis == Axis.Y ? Vector3.up :
                             Vector3.forward;
    }

    void OnMouseDown()  => dragging = true;
    void OnMouseUp()    => dragging = false;

    void Update()
    {
        if (!dragging || !target)
            return;

        Ray r = cam.ScreenPointToRay(Input.mousePosition);
        Vector3 p0 = target.transform.position;
        Vector3 v = r.direction;

        float t = Vector3.Dot(axisDir, (r.origin - p0)) /
                  Vector3.Dot(axisDir, v);

        Vector3 worldHit = p0 + v * t;

        // Convert world → local
        Transform parent = target.transform.parent;
        Vector3 local = parent.InverseTransformPoint(worldHit);

        // Clamp
        local = NodeInteractionController.ClampToCube(local);

        // Apply movement
        target.transform.localPosition = local;
    }
}
