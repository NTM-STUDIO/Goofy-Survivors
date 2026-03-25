using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TooltipManager : MonoBehaviour
{
    public static TooltipManager Instance { get; private set; }

    [Header("Tooltip UI")]
    public GameObject tooltipPanel;
    public TextMeshProUGUI tooltipTitleText;
    public TextMeshProUGUI tooltipDescriptionText;
    public Image tooltipIcon;

    [Header("Settings")]
    public float showDelay = 0.2f;
    public Vector2 offset = new Vector2(15f, -15f);

    private RectTransform tooltipRect;
    private CanvasGroup canvasGroup;
    private bool isHovering = false;
    private float hoverTimer = 0f;

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
            // Ativa garantidamente para os scripts filhos/layouts renderizarem
            tooltipPanel.SetActive(true);
            tooltipRect = tooltipPanel.GetComponent<RectTransform>();
            
            canvasGroup = tooltipPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = tooltipPanel.AddComponent<CanvasGroup>();
            
            canvasGroup.alpha = 0f; 
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    void Update()
    {
        if (tooltipRect == null) return;

        if (isHovering)
        {
            hoverTimer += Time.unscaledDeltaTime;
            if (hoverTimer >= showDelay)
            {
                if (canvasGroup.alpha == 0f) LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRect);
                canvasGroup.alpha = 1f;

                // Segue o Rato diretamente pelas posições x e y
                Vector2 mousePos = Input.mousePosition;
                tooltipRect.position = new Vector2(mousePos.x + offset.x, mousePos.y + offset.y);
            }
        }
        else
        {
            canvasGroup.alpha = 0f;
            hoverTimer = 0f;
            // Estaciona longe da câmara para evitar piscar quando ativado
            tooltipRect.anchoredPosition = new Vector2(9000, 9000); 
        }
    }

    public void ShowTooltip(string title, string description, Sprite icon = null)
    {
        isHovering = true;
        hoverTimer = 0f;

        if (tooltipPanel == null) return;

        if (tooltipTitleText != null)
        {
            tooltipTitleText.gameObject.SetActive(!string.IsNullOrEmpty(title));
            tooltipTitleText.text = title;
        }

        if (tooltipDescriptionText != null)    
            tooltipDescriptionText.text = description;

        if (tooltipIcon != null)
        {
            tooltipIcon.gameObject.SetActive(icon != null);
            tooltipIcon.sprite = icon;
        }
    }

    public void HideTooltip()
    {
        isHovering = false;
    }
}
