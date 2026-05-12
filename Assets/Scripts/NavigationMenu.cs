using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class NavigationMenu : MonoBehaviour
{
    [Header("Panel Settings")]
    public Color panelColor = new Color(0.15f, 0.15f, 0.15f, 0.9f);
    public Vector2 panelSize = new Vector2(0, 60);
    public float topPadding = 0f;

    [Header("Button Settings")]
    public Vector2 buttonSize = new Vector2(300, 40);
    public Color buttonColor = new Color(0.25f, 0.25f, 0.25f, 1f);
    public Color textColor = Color.white;
    public int fontSize = 24;

    void Awake()
    {
        SetupMenuLayout();
    }

    private void SetupMenuLayout()
    {
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null)
        {
            Debug.LogError("NavigationMenu must be placed under a Canvas!");
            return;
        }

        RectTransform menuRect = GetComponent<RectTransform>();
        if (menuRect == null)
            menuRect = gameObject.AddComponent<RectTransform>();

        menuRect.anchorMin = Vector2.zero;
        menuRect.anchorMax = Vector2.one;
        menuRect.offsetMin = Vector2.zero;
        menuRect.offsetMax = Vector2.zero;
        menuRect.pivot = new Vector2(0.5f, 0.5f);

        // Create TopBar Panel
        GameObject panelObj = new GameObject("TopBar", typeof(RectTransform), typeof(Image));
        panelObj.transform.SetParent(transform, false);
        Image panelImage = panelObj.GetComponent<Image>();
        panelImage.color = panelColor;

        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 1);
        panelRect.anchorMax = new Vector2(1, 1);
        panelRect.pivot = new Vector2(0.5f, 1);
        panelRect.sizeDelta = panelSize;
        panelRect.anchoredPosition = new Vector2(0, -topPadding);

        HorizontalLayoutGroup layout = panelObj.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 20;
        layout.padding = new RectOffset(20, 20, 5, 5);
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = false;
        layout.childControlHeight = false;

        CreateButton(panelObj.transform, "Generate Microstructure", GoToSampleScene);
        CreateButton(panelObj.transform, "Interpolate Between Microstructures", GoToInterpolationScene);
    }

    private void CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObj = new GameObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObj.transform.SetParent(parent, false);

        Image btnImage = buttonObj.GetComponent<Image>();
        btnImage.color = buttonColor;

        Button button = buttonObj.GetComponent<Button>();
        button.onClick.AddListener(action);

        RectTransform btnRect = buttonObj.GetComponent<RectTransform>();
        btnRect.sizeDelta = buttonSize;

        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(buttonObj.transform, false);

        TextMeshProUGUI tmpText = textObj.GetComponent<TextMeshProUGUI>();
        tmpText.text = label;
        tmpText.color = textColor;
        tmpText.fontSize = fontSize;
        tmpText.alignment = TextAlignmentOptions.Center;
        tmpText.textWrappingMode = TextWrappingModes.Normal; // UPDATED for modern TMP

        RectTransform txtRect = textObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;
    }

    public void GoToSampleScene() => LoadSceneIfExists("SampleScene");
    public void GoToInterpolationScene() => LoadSceneIfExists("InterpolationScene");

    private void LoadSceneIfExists(string sceneName)
    {
        if (Application.CanStreamedLevelBeLoaded(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogError($"Scene '{sceneName}' not found in Build Settings!");
        }
    }
}
