// Filename: TooltipTrigger.cs
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Attach this component to any UI element to show a tooltip on hover.
/// Requires a TooltipManager in the scene.
/// </summary>
public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Tooltip Content")]
    [SerializeField] private string tooltipTitle;
    [TextArea(2, 5)]
    [SerializeField] private string tooltipDescription;
    [SerializeField] private Sprite tooltipIcon;

    [Header("Dynamic Content (Optional)")]
    [Tooltip("If true, will try to get description from attached components like WeaponData, UpgradeData, etc.")]
    [SerializeField] private bool useDynamicContent = false;

    // Cache for dynamic content providers
    private ITooltipProvider tooltipProvider;

    void Awake()
    {
        if (useDynamicContent)
        {
            tooltipProvider = GetComponent<ITooltipProvider>();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (TooltipManager.Instance == null)
        {
            Debug.LogWarning("TooltipTrigger: No TooltipManager found in scene!");
            return;
        }

        string title = tooltipTitle;
        string description = tooltipDescription;
        Sprite icon = tooltipIcon;

        // Try to get dynamic content if enabled
        if (useDynamicContent && tooltipProvider != null)
        {
            var dynamicContent = tooltipProvider.GetTooltipContent();
            if (!string.IsNullOrEmpty(dynamicContent.title)) title = dynamicContent.title;
            if (!string.IsNullOrEmpty(dynamicContent.description)) description = dynamicContent.description;
            if (dynamicContent.icon != null) icon = dynamicContent.icon;
        }

        TooltipManager.Instance.ShowTooltip(title, description, icon);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.HideTooltip();
        }
    }

    void OnDisable()
    {
        // Hide tooltip if this object is disabled while hovering
        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.HideTooltip();
        }
    }

    /// <summary>
    /// Set the tooltip content at runtime.
    /// </summary>
    public void SetContent(string title, string description, Sprite icon = null)
    {
        tooltipTitle = title;
        tooltipDescription = description;
        tooltipIcon = icon;
    }

    /// <summary>
    /// Set just the description at runtime.
    /// </summary>
    public void SetDescription(string description)
    {
        tooltipDescription = description;
    }

    /// <summary>
    /// Set just the title at runtime.
    /// </summary>
    public void SetTitle(string title)
    {
        tooltipTitle = title;
    }
}

/// <summary>
/// Interface for components that can provide tooltip content dynamically.
/// Implement this on your data-holding components (like weapon slots, ability icons, etc.)
/// </summary>
public interface ITooltipProvider
{
    TooltipContent GetTooltipContent();
}

/// <summary>
/// Struct to hold tooltip content data.
/// </summary>
public struct TooltipContent
{
    public string title;
    public string description;
    public Sprite icon;

    public TooltipContent(string title, string description, Sprite icon = null)
    {
        this.title = title;
        this.description = description;
        this.icon = icon;
    }
}
