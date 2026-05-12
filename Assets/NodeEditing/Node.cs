using UnityEngine;

public class Node : MonoBehaviour
{
    public enum MoveAxis { X, Y, Z }
    public MoveAxis axis = MoveAxis.X;

    // Original OBJ cube bounds
    public Vector3 minBounds = Vector3.zero;         // (0,0,0)
    public Vector3 maxBounds = new Vector3(2, 2, 2); // (2,2,2)

    private Vector3 localStartPos;

    [Header("Debug Movement")]
    public bool demoOscillation = false; // OFF by default (you can now move nodes manually)

    void Start()
    {
        // Save original local position (inside the 2x2x2 OBJ space)
        localStartPos = transform.localPosition;
    }

    void Update()
    {
        if (!demoOscillation)
            return;

        // Simple oscillation for testing only
        float osc = Mathf.Sin(Time.time) * 0.5f;
        Vector3 newLocal = localStartPos;

        switch (axis)
        {
            case MoveAxis.X: newLocal.x = localStartPos.x + osc; break;
            case MoveAxis.Y: newLocal.y = localStartPos.y + osc; break;
            case MoveAxis.Z: newLocal.z = localStartPos.z + osc; break;
        }

        ApplyClampedPosition(newLocal);
    }

    // Call this from UI or handle drag to move the node
    public void MoveAlongAxis(float delta)
    {
        Vector3 local = transform.localPosition;

        if (axis == MoveAxis.X) local.x += delta;
        if (axis == MoveAxis.Y) local.y += delta;
        if (axis == MoveAxis.Z) local.z += delta;

        ApplyClampedPosition(local);
    }

    private void ApplyClampedPosition(Vector3 pos)
    {
        pos.x = Mathf.Clamp(pos.x, minBounds.x, maxBounds.x);
        pos.y = Mathf.Clamp(pos.y, minBounds.y, maxBounds.y);
        pos.z = Mathf.Clamp(pos.z, minBounds.z, maxBounds.z);

        transform.localPosition = pos;
    }
}
