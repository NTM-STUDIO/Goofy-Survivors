using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// REMOVIDO: NetworkBehaviour. Agora é um MonoBehaviour normal.
public class LobbyPlayerSlot : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_Text playerNameText;
    public TMP_Text unitNameText;
    public Button nextButton;
    public Button prevButton;
    public Image unitDisplayImage; // Adicionei para mostrar o sprite da unidade

    private int currentIndex = 0;
    private ulong playerId;
    private LobbyManagerP2P manager;
    private List<GameObject> unitPrefabs;

    public void Initialize(LobbyManagerP2P manager, ulong playerId, string playerName, bool isLocalPlayer, List<GameObject> prefabs, int initialSelection)
    {
        this.manager = manager;
        this.playerId = playerId;
        this.unitPrefabs = prefabs;
        this.currentIndex = initialSelection;

        playerNameText.text = playerName;
        
        // Só o próprio jogador pode mexer nos botões dele
        nextButton.interactable = isLocalPlayer;
        prevButton.interactable = isLocalPlayer;
        
        nextButton.onClick.AddListener(OnNextPressed);
        prevButton.onClick.AddListener(OnPrevPressed);

        UpdateDisplay();
    }

    private void OnNextPressed()
    {
        currentIndex = (currentIndex + 1) % unitPrefabs.Count;
        UpdateDisplay();
        // Informa o Manager que a seleção mudou. O Manager tratará de avisar a rede.
        manager.LocalPlayerChangedSelection(currentIndex);
    }

    private void OnPrevPressed()
    {
        currentIndex--;
        if (currentIndex < 0) currentIndex = unitPrefabs.Count - 1;
        UpdateDisplay();
        manager.LocalPlayerChangedSelection(currentIndex);
    }

    // Método chamado pelo Manager quando recebe atualizações da rede
    public void UpdateSelection(int newIndex)
    {
        currentIndex = newIndex;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (unitPrefabs == null || unitPrefabs.Count == 0) return;
        GameObject unit = unitPrefabs[Mathf.Clamp(currentIndex, 0, unitPrefabs.Count - 1)];
        if (unit != null)
        {
             unitNameText.text = unit.name;
             // Se quiser mostrar a imagem:
             var spriteRenderer = unit.GetComponentInChildren<SpriteRenderer>();
                if (spriteRenderer && unitDisplayImage) unitDisplayImage.sprite = spriteRenderer.sprite;
        }
    }

    public int GetCurrentIndex()
    {
        return currentIndex;
    }
}