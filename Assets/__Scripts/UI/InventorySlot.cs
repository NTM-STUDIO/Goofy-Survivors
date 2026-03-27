using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class InventorySlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("UI References")]
    [Tooltip("Arrasta o Tooltip que JÁ ESTÁ NA CENA (dentro do Canvas) para aqui!")]
    [SerializeField] private GameObject tooltipInstance;
    
    // Descomenta as variáveis do teu item final aqui
    // public Item item; 

    private Canvas canvas;
    private RectTransform panel;
    private bool isHovering = false;

    private void Start()
    {
        // Garante que a Tooltip na cena comece invisível logo no início
        if (tooltipInstance != null)
        {
            tooltipInstance.SetActive(false);
        }
    }

    private void DisplayItemTooltip()
    {
        if (tooltipInstance == null)
        {
            Debug.LogWarning("O Tooltip da cena não está associado no script InventorySlot do Inspector.");
            return;
        }

        canvas = GetComponentInParent<Canvas>();
        panel = tooltipInstance.GetComponent<RectTransform>();

        // 1. Liga o Tooltip da cena
        tooltipInstance.SetActive(true);
        isHovering = true;

        // 2. Colocar na posição inicial
        UpdateTooltipPosition();

        // 3. Obter as referências com os nomes da tua hierarquia exata
        Transform itemSpriteTransform = tooltipInstance.transform.Find("Image");
        Transform nameTransform = tooltipInstance.transform.Find("Text (TMP)"); 
        
        if (itemSpriteTransform == null || nameTransform == null)
        {
            Debug.LogWarning("Não encontrei os objetos 'Image' ou 'Text (TMP)'. Verifica a tooltip na cena!");
            return;
        }

        Image itemSprite = itemSpriteTransform.GetComponent<Image>();
        TMP_Text tb_itemName = nameTransform.GetComponent<TMP_Text>();        

        if (itemSprite == null || tb_itemName == null) return;

        // 4. Configurar os valores (Descomenta estas linhas para puxar os dados do teu jogo)
        // itemSprite.sprite = item.item.RunTimeItemData.ItemPrefab.GetComponent<SpriteRenderer>().sprite;
        // tb_itemName.text = item.item.RunTimeItemData.ItemName;        
    }

    // Update para fazer o sistema seguir o rato!
    private void Update()
    {
        if (isHovering && tooltipInstance != null && tooltipInstance.activeSelf)
        {
            UpdateTooltipPosition();
        }
    }

    private void UpdateTooltipPosition()
    {
        if (canvas == null || panel == null) return;

        // A solução definitiva para Canvas Scalers!
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform, 
            Input.mousePosition, 
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, 
            out Vector2 movePos);

        // Posiciona usando Matemática correta e adiciona o Offset (15, -15) para nunca tapar o ponteiro
        panel.transform.localPosition = movePos + new Vector2(15f, -15f);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Podes meter aqui: if(item != null)
        DisplayItemTooltip();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
        
        // APENAS desativa o objeto da cena, não destrói!
        if(tooltipInstance != null)
        {
            tooltipInstance.SetActive(false);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Lógica de quando clicas no item
    }
}
