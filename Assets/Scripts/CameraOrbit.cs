using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.EventSystems;

using EnhancedTouch = UnityEngine.InputSystem.EnhancedTouch;

public class CameraOrbit : MonoBehaviour
{
    [Header("Orbit Settings")]
    public Transform pivot;
    public float distance = 8f;
    public float sensitivity = 4f;

    [Header("Zoom Settings")]
    public float zoomSpeed = 2f;
    public float minDistance = 2f;
    public float maxDistance = 20f;

    private float rotX;
    private float rotY;

    // Drag tracking
    private bool isDragging = false;
    private Vector2 lastPointerPosition;
    private const float dragThreshold = 3f; // pixels before drag is confirmed

    void OnEnable()
    {
        EnhancedTouch.EnhancedTouchSupport.Enable();
    }

    void OnDisable()
    {
        EnhancedTouch.EnhancedTouchSupport.Disable();
    }

    void Start()
    {
        if (pivot == null)
        {
            GameObject go = new GameObject("CameraPivot");
            go.transform.position = new Vector3(1f, 1f, 1f);
            pivot = go.transform;
        }

        Vector3 angles = transform.eulerAngles;
        rotX = angles.y;
        rotY = angles.x;
    }

    void LateUpdate()
    {
        HandleZoom();
        HandleRotation();
        ApplyCameraTransform();
    }

    void HandleZoom()
    {
        var mouse = Mouse.current;
        float scroll = 0f;

        if (mouse != null)
            scroll = mouse.scroll.ReadValue().y;

        // Pinch-to-zoom fallback for touch
        if (Mathf.Abs(scroll) < 0.01f && EnhancedTouch.Touch.activeTouches.Count >= 2)
        {
            var touches = EnhancedTouch.Touch.activeTouches;
            float prevDistance = Vector2.Distance(touches[0].startScreenPosition, touches[1].startScreenPosition);
            float currDistance = Vector2.Distance(touches[0].screenPosition, touches[1].screenPosition);
            scroll = (currDistance - prevDistance) * 0.05f;
        }

        if (Mathf.Abs(scroll) > 0.01f)
        {
            distance -= scroll * zoomSpeed * Time.unscaledDeltaTime * 100f;
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
        }
    }

    void HandleRotation()
    {
        var mouse   = Mouse.current;
        var pointer = Pointer.current;

        bool overUI = IsPointerOverUI();
        if (overUI)
        {
            isDragging = false;
            return;
        }

        bool buttonHeld = false;
        if (mouse != null)
            buttonHeld = mouse.leftButton.isPressed
                    || mouse.rightButton.isPressed
                    || mouse.middleButton.isPressed;

        if (!buttonHeld && EnhancedTouch.Touch.activeTouches.Count == 1)
            buttonHeld = true;

        if (!buttonHeld)
        {
            isDragging = false;
            return;
        }

        if (pointer == null) return;
        Vector2 currentPos = pointer.position.ReadValue();

        if (!isDragging)
        {
            lastPointerPosition = currentPos;
            isDragging = true;
            // Mark this click as consumed so nodes/edges don't react to it
            Microstructure.InputGuard.ConsumeClick(Time.frameCount);
            return;
        }

        Vector2 delta = currentPos - lastPointerPosition;
        lastPointerPosition = currentPos;

        if (delta.magnitude < dragThreshold) return;

        rotX += delta.x * sensitivity * 0.1f;
        rotY -= delta.y * sensitivity * 0.1f;
        rotY  = Mathf.Clamp(rotY, -85f, 85f);
    }

    void ApplyCameraTransform()
    {
        Quaternion rotation = Quaternion.Euler(rotY, rotX, 0);
        Vector3 offset = rotation * new Vector3(0, 0, -distance);
        transform.position = pivot.position + offset;
        transform.rotation = rotation;
    }

private bool IsPointerOverUI()
{
    if (EventSystem.current == null) return false;

    var pointer = Pointer.current;
    if (pointer == null) return false;

    var pointerEventData = new PointerEventData(EventSystem.current);
    pointerEventData.position = pointer.position.ReadValue();

    var results = new System.Collections.Generic.List<RaycastResult>();
    EventSystem.current.RaycastAll(pointerEventData, results);

    foreach (var result in results)
    {
        // Only treat as blocking if the hit object has an interactive component
        var go = result.gameObject;
        if (go.GetComponent<UnityEngine.UI.Button>() != null)     return true;
        if (go.GetComponent<UnityEngine.UI.Dropdown>() != null)   return true;
        if (go.GetComponent<UnityEngine.UI.InputField>() != null) return true;
        if (go.GetComponent<UnityEngine.UI.Slider>() != null)     return true;
        if (go.GetComponent<UnityEngine.UI.Toggle>() != null)     return true;
        if (go.GetComponent<UnityEngine.UI.ScrollRect>() != null) return true;

        // Debug.Log($"[CameraOrbit] Ignored non-interactive UI hit: '{go.name}'");
    }

    return false;
}
}