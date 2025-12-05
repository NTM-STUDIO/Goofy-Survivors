// Filename: TooltipManager.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Singleton manager that displays tooltips when hovering over UI elements.
/// Attach this to a Canvas with a tooltip panel child.
/// </summary>
public class TooltipManager : MonoBehaviour
{
    public static TooltipManager Instance { get; private set; }

    [Header("Tooltip UI")]
    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private TextMeshProUGUI tooltipTitleText;
    [SerializeField] private TextMeshProUGUI tooltipDescriptionText;
    [SerializeField] private Image tooltipIcon;

    [Header("Settings")]
    [SerializeField] private float showDelay = 0.3f;
    [SerializeField] private Vector2 offset = new Vector2(15f, -15f);
    [SerializeField] private float padding = 10f;

    private RectTransform tooltipRect;
    private Canvas parentCanvas;
    private RectTransform canvasRect;
    private float hoverTimer = 0f;
    private bool isWaitingToShow = false;
    private string pendingTitle;
    private string pendingDescription;
    private Sprite pendingIcon;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (tooltipPanel != null)
        {
            tooltipRect = tooltipPanel.GetComponent<RectTransform>();
            tooltipPanel.SetActive(false);
        }

        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null)
        {
            canvasRect = parentCanvas.GetComponent<RectTransform>();
        }
    }

    void Update()
    {
        if (isWaitingToShow)
        {
            hoverTimer += Time.unscaledDeltaTime;
            if (hoverTimer >= showDelay)
            {
                ShowTooltipImmediate(pendingTitle, pendingDescription, pendingIcon);
                isWaitingToShow = false;
            }
        }

        if (tooltipPanel != null && tooltipPanel.activeSelf)
        {
            UpdateTooltipPosition();
        }
    }

    /// <summary>
    /// Call this when the mouse enters a UI element with a tooltip.
    /// </summary>
    public void ShowTooltip(string title, string description, Sprite icon = null)
    {
        pendingTitle = title;
        pendingDescription = description;
        pendingIcon = icon;
        hoverTimer = 0f;
        isWaitingToShow = true;
    }

    /// <summary>
    /// Call this when the mouse exits a UI element.
    /// </summary>
    public void HideTooltip()
    {
        isWaitingToShow = false;
        hoverTimer = 0f;
        if (tooltipPanel != null)
        {
            tooltipPanel.SetActive(false);
        }
    }

    private void ShowTooltipImmediate(string title, string description, Sprite icon)
    {
        if (tooltipPanel == null) return;

        // Set title
        if (tooltipTitleText != null)
        {
            if (string.IsNullOrEmpty(title))
            {
                tooltipTitleText.gameObject.SetActive(false);
            }
            else
            {
                tooltipTitleText.gameObject.SetActive(true);
                tooltipTitleText.text = title;
            }
        }

        // Set description
        if (tooltipDescriptionText != null)
        {
            tooltipDescriptionText.text = description;
        }

        // Set icon
        if (tooltipIcon != null)
        {
            if (icon != null)
            {
                tooltipIcon.gameObject.SetActive(true);
                tooltipIcon.sprite = icon;
            }
            else
            {
                tooltipIcon.gameObject.SetActive(false);
            }
        }

        // Force layout rebuild before positioning
        LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRect);

        tooltipPanel.SetActive(true);
        UpdateTooltipPosition();
    }

    private void UpdateTooltipPosition()
    {
        if (tooltipRect == null || canvasRect == null) return;

        Vector2 mousePos = Input.mousePosition;

        // Convert mouse position to canvas space
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            mousePos,
            parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera,
            out Vector2 localPoint
        );

        // Apply offset
        Vector2 tooltipPos = localPoint + offset;

        // Get tooltip size
        Vector2 tooltipSize = tooltipRect.sizeDelta;

        // Clamp to canvas bounds
        float canvasWidth = canvasRect.rect.width;
        float canvasHeight = canvasRect.rect.height;

        // Adjust if tooltip goes off-screen to the right
        if (tooltipPos.x + tooltipSize.x + padding > canvasWidth / 2f)
        {
            tooltipPos.x = localPoint.x - tooltipSize.x - Mathf.Abs(offset.x);
        }

        // Adjust if tooltip goes off-screen to the bottom
        if (tooltipPos.y - tooltipSize.y - padding < -canvasHeight / 2f)
        {
            tooltipPos.y = localPoint.y + tooltipSize.y + Mathf.Abs(offset.y);
        }

        // Adjust if tooltip goes off-screen to the left
        if (tooltipPos.x - padding < -canvasWidth / 2f)
        {
            tooltipPos.x = -canvasWidth / 2f + padding;
        }

        // Adjust if tooltip goes off-screen to the top
        if (tooltipPos.y + padding > canvasHeight / 2f)
        {
            tooltipPos.y = canvasHeight / 2f - padding;
        }

        tooltipRect.anchoredPosition = tooltipPos;
    }

    /// <summary>
    /// Creates the tooltip UI if it doesn't exist. Call this from editor or at runtime.
    /// </summary>
    [ContextMenu("Create Tooltip UI")]
    public void CreateTooltipUI()
    {
        if (tooltipPanel != null)
        {
            Debug.Log("Tooltip panel already exists.");
            return;
        }

        // Create tooltip panel
        tooltipPanel = new GameObject("TooltipPanel");
        tooltipPanel.transform.SetParent(transform, false);
        
        var panelRect = tooltipPanel.AddComponent<RectTransform>();
        panelRect.pivot = new Vector2(0, 1); // Top-left pivot
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);

        // Background image
        var bgImage = tooltipPanel.AddComponent<Image>();
        bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);

        // Content size fitter for auto-sizing
        var contentFitter = tooltipPanel.AddComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Vertical layout group
        var layoutGroup = tooltipPanel.AddComponent<VerticalLayoutGroup>();
        layoutGroup.padding = new RectOffset(12, 12, 8, 8);
        layoutGroup.spacing = 4f;
        layoutGroup.childAlignment = TextAnchor.UpperLeft;
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = true;
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.childForceExpandHeight = false;

        // Create title text
        var titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(tooltipPanel.transform, false);
        tooltipTitleText = titleObj.AddComponent<TextMeshProUGUI>();
        tooltipTitleText.fontSize = 16;
        tooltipTitleText.fontStyle = FontStyles.Bold;
        tooltipTitleText.color = Color.white;
        tooltipTitleText.text = "Title";

        // Create description text
        var descObj = new GameObject("DescriptionText");
        descObj.transform.SetParent(tooltipPanel.transform, false);
        tooltipDescriptionText = descObj.AddComponent<TextMeshProUGUI>();
        tooltipDescriptionText.fontSize = 14;
        tooltipDescriptionText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
        tooltipDescriptionText.text = "Description goes here.";

        // Add layout element for max width
        var layoutElement = descObj.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = 250f;

        tooltipRect = panelRect;
        tooltipPanel.SetActive(false);

        Debug.Log("Tooltip UI created successfully!");
    }
}
