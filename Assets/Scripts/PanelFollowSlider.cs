using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class PanelFollowSlider : MonoBehaviour
{
    [Header("The slider to follow")]
    public Slider targetSlider;

    private RectTransform panelRect;
    private RectTransform sliderRect;

    void Awake()
    {
        if (targetSlider == null)
        {
            Debug.LogError("PanelFollowSlider: Target slider is not assigned!");
            enabled = false;
            return;
        }
        
        panelRect = GetComponent<RectTransform>();
        sliderRect = targetSlider.GetComponent<RectTransform>();
        
        // Make panel transparent but still receive raycasts
        Image panelImage = GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.color = new Color(1, 1, 1, 0); // Fully transparent
            // panelImage.raycastTarget = true; // Keep this enabled (default)
        }
    }

    void Update()
    {
        // Match panel position to slider
        panelRect.position = sliderRect.position;

        // Match panel size to slider (or slightly larger for a buffer)
        Vector2 size = sliderRect.sizeDelta;
        Debug.Log("size" + size);
        panelRect.sizeDelta = size + new Vector2(10.0f, 10.0f); // optional padding
    }
}
