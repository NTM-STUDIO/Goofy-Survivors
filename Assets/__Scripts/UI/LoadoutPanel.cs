using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using DropsAndLoadout;

public class LoadoutPanel : MonoBehaviour
{
    [Header("Main Panel Frames (Click to open popups)")]
    public Button weaponFrameButton;
    public Image weaponFrameIcon;
    public TextMeshProUGUI weaponFrameName;
    
    public Button runeFrameButton;
    public Image runeFrameIcon;
    public TextMeshProUGUI runeFrameName;

    [Header("Shared Popup")]
    public GameObject sharedPopupPanel;
    public Transform sharedGridContainer;
    public TextMeshProUGUI optionalPopupTitle;

    [Header("Flow Buttons")]
    public Button closeLoadoutButton;
    public Button closePopupButton;

    [Header("Data")]
    public PlayerCharacterData defaultCharacter;
    public WeaponRegistry weaponRegistry;
    public List<RuneDefinition> runeCatalog = new List<RuneDefinition>();
    public GameObject inventoryItemButtonPrefab;

    private string _selectedWeaponUniqueId = "";
    private string _selectedRuneId = "";

    void Awake()
    {
        // Contexts for persistence
        if (defaultCharacter != null) {
            LoadoutSelections.CharacterPrefabsContext = new List<GameObject> { defaultCharacter.playerPrefab };
        }
        LoadoutSelections.WeaponRegistryContext = weaponRegistry;
        LoadoutSelections.RuneCatalogContext = runeCatalog;

        WireButtons();

        // Load previous selections if available
        LoadoutSelections.LoadFromPlayerPrefs();
        
        // Weapon
        var savedItem = LoadoutSelections.EquippedWeaponUniqueId;
        if (!string.IsNullOrEmpty(savedItem))
        {
            _selectedWeaponUniqueId = savedItem;
        }
        else if (LoadoutSystem.Instance != null && LoadoutSystem.Instance.Inventory != null)
        {
            var firstWep = LoadoutSystem.Instance.Inventory.FirstOrDefault(x => x.Type == DropType.Weapon);
            if (firstWep != null)
            {
                _selectedWeaponUniqueId = firstWep.ItemUniqueId;
            }
        }

        // Runes
        if (LoadoutSelections.SelectedRunes != null && LoadoutSelections.SelectedRunes.Count > 0)
        {
            var firstRune = LoadoutSelections.SelectedRunes.FirstOrDefault(r => r != null);
            if (firstRune != null) _selectedRuneId = firstRune.runeId;
        }

        // Hide popups initially
        if (sharedPopupPanel != null) sharedPopupPanel.SetActive(false);

        RefreshFrames();
        ApplyAndSave(); // Make sure initial state is saved
    }

    private void WireButtons()
    {
        if (closeLoadoutButton != null)
        {
            closeLoadoutButton.onClick.RemoveAllListeners();
            closeLoadoutButton.onClick.AddListener(Close);
        }

        if (closePopupButton != null)
        {
            closePopupButton.onClick.RemoveAllListeners();
            closePopupButton.onClick.AddListener(CloseSharedPopup);
        }

        if (weaponFrameButton != null)
        {
            weaponFrameButton.onClick.RemoveAllListeners();
            weaponFrameButton.onClick.AddListener(OpenWeaponPopup);
        }

        if (runeFrameButton != null)
        {
            runeFrameButton.onClick.RemoveAllListeners();
            runeFrameButton.onClick.AddListener(OpenRunePopup);
        }
    }

    public void Open()
    {
        gameObject.SetActive(true);
        RefreshFrames();
    }

    public void OpenLoadout() => Open();
    public void Close() => gameObject.SetActive(false);

    private void RefreshFrames()
    {
        // Weapon display
        WeaponData wdToDisplay = null;
        string wNameToDisplay = "Select Weapon";

        if (LoadoutSystem.Instance != null && LoadoutSystem.Instance.Inventory != null)
        {
            var equippedItem = LoadoutSystem.Instance.Inventory.FirstOrDefault(x => x.ItemUniqueId == _selectedWeaponUniqueId);
            if (equippedItem != null)
            {
                wNameToDisplay = equippedItem.ItemName;
                if (weaponRegistry != null && weaponRegistry.allWeapons != null)
                {
                    wdToDisplay = weaponRegistry.allWeapons.FirstOrDefault(w => w != null && 
                        (w.name.Equals(equippedItem.ItemName, System.StringComparison.OrdinalIgnoreCase) || 
                         w.weaponName.Equals(equippedItem.ItemName, System.StringComparison.OrdinalIgnoreCase) ||
                         (equippedItem.ItemName.ToLower() == "panela" && (w.name.ToLower().Contains("flyingpan") || w.name.ToLower().Contains("pitchfork")))
                        ));
                }
            }
        }

        // Se não conseguiu encontrar o drop ou o inventário está vazio, preenche com a arma Default do sistema de Selections
        if (wdToDisplay == null && LoadoutSelections.SelectedWeapon != null)
        {
            wdToDisplay = LoadoutSelections.SelectedWeapon;
            wNameToDisplay = wdToDisplay.weaponName;
            if (string.IsNullOrEmpty(wNameToDisplay)) wNameToDisplay = wdToDisplay.name;
        }

        // Aplica à UI
        if (weaponFrameName) weaponFrameName.text = wNameToDisplay;
        if (weaponFrameButton != null)
        {
            Image btnImg = weaponFrameButton.GetComponent<Image>();
            if (btnImg != null)
            {
                btnImg.sprite = wdToDisplay != null ? wdToDisplay.icon : null;
                btnImg.type = Image.Type.Simple; // Forçar tipo Simple em vez de Sliced
                btnImg.preserveAspect = true;
                
                var c = btnImg.color;
                c.a = btnImg.sprite != null ? 1f : 0f;
                btnImg.color = c;
            }
        }

        // Rune display
        var selectedRune = runeCatalog.FirstOrDefault(r => r != null && r.runeId == _selectedRuneId);
        if (runeFrameName) runeFrameName.text = selectedRune != null ? selectedRune.displayName : "Select Rune";
        
        if (runeFrameButton != null)
        {
            Image btnImg = runeFrameButton.GetComponent<Image>();
            if (btnImg != null)
            {
                btnImg.sprite = selectedRune != null ? selectedRune.icon : null;
                btnImg.type = Image.Type.Simple; // Forçar tipo Simple para não esburacar Unity
                btnImg.preserveAspect = true;

                var c = btnImg.color;
                c.a = btnImg.sprite != null ? 1f : 0f;
                btnImg.color = c;
            }
        }
    }

    private void OpenWeaponPopup()
    {
        if (sharedPopupPanel != null) sharedPopupPanel.SetActive(true);
        if (optionalPopupTitle != null) optionalPopupTitle.text = "Choose Weapon";
        PopulateWeaponInventory();
    }

    private void OpenRunePopup()
    {
        if (sharedPopupPanel != null) sharedPopupPanel.SetActive(true);
        if (optionalPopupTitle != null) optionalPopupTitle.text = "Choose Rune";
        PopulateRuneInventory();
    }

    private void CloseSharedPopup()
    {
        if (sharedPopupPanel != null) sharedPopupPanel.SetActive(false);
    }

    private void PopulateWeaponInventory()
    {
        if (sharedGridContainer == null || inventoryItemButtonPrefab == null) return;
        
        // Clear grid
        foreach (Transform child in sharedGridContainer) Destroy(child.gameObject);
        
        // Populate
        if (LoadoutSystem.Instance != null && LoadoutSystem.Instance.Inventory != null)
        {
            var weapons = LoadoutSystem.Instance.Inventory.Where(x => x != null && x.Type == DropType.Weapon).ToList();
            foreach (var drop in weapons)
            {
                string captureId = drop.ItemUniqueId;
                GameObject btnObj = Instantiate(inventoryItemButtonPrefab, sharedGridContainer);
                
                Sprite icon = null;
                if (weaponRegistry != null && weaponRegistry.allWeapons != null)
                {
                    // Fallback para nomes antigos como 'Panela' que agora é 'FlyingPan', mas tentar match direto incasensitive primeiro
                    var matchingWd = weaponRegistry.allWeapons.FirstOrDefault(w => w != null && 
                        (w.name.Equals(drop.ItemName, System.StringComparison.OrdinalIgnoreCase) || 
                         w.weaponName.Equals(drop.ItemName, System.StringComparison.OrdinalIgnoreCase) ||
                         (drop.ItemName.ToLower() == "panela" && (w.name.ToLower().Contains("flyingpan") || w.name.ToLower().Contains("pitchfork")))
                        ));
                        
                    if (matchingWd != null) icon = matchingWd.icon;
                }

                string statsStr = string.Join("\n", drop.Substats.Select(s => s.GetDescription()));
                string itemDesc = $"{drop.Description}\n\n[Stats]\n{statsStr}";

                SetupInventoryButton(btnObj, drop.ItemName, itemDesc, icon, () => {
                    _selectedWeaponUniqueId = captureId;
                    CloseSharedPopup();
                    RefreshFrames();
                    ApplyAndSave();
                });
            }
        }
    }

    private void PopulateRuneInventory()
    {
        if (sharedGridContainer == null || inventoryItemButtonPrefab == null) return;
        
        // Clear grid
        foreach (Transform child in sharedGridContainer) Destroy(child.gameObject);

        // Populate
        if (runeCatalog != null)
        {
            foreach (var rune in runeCatalog)
            {
                if (rune == null) continue;

                string captureId = rune.runeId;
                GameObject btnObj = Instantiate(inventoryItemButtonPrefab, sharedGridContainer);
                
                SetupInventoryButton(btnObj, rune.displayName, rune.description, rune.icon, () => {
                    _selectedRuneId = captureId;
                    CloseSharedPopup();
                    RefreshFrames();
                    ApplyAndSave();
                });
            }
        }
    }

    private void SetupInventoryButton(GameObject btnObj, string itemName, string itemDesc, Sprite itemIcon, UnityEngine.Events.UnityAction onClickAction)
    {
        Button btn = btnObj.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(onClickAction);
        }

        Image targetImage = btnObj.GetComponent<Image>();
        if (targetImage != null)
        {
            targetImage.sprite = itemIcon;
            targetImage.type = Image.Type.Simple; // Forçar tipo Simple em vez de Sliced nos da grid
            targetImage.preserveAspect = true;
            
            var c = targetImage.color;
            c.a = itemIcon != null ? 1f : 0f;
            targetImage.color = c;
        }

        TextMeshProUGUI txt = btnObj.GetComponentInChildren<TextMeshProUGUI>();
        if (txt != null) txt.text = itemName;

        TooltipTrigger tooltip = btnObj.GetComponent<TooltipTrigger>();
        if (tooltip == null)
        {
            tooltip = btnObj.AddComponent<TooltipTrigger>();
        }
        tooltip.SetContent(itemName, itemDesc, itemIcon);
    }

    public void ApplyAndSave()
    {
        GameObject chPrefab = defaultCharacter != null ? defaultCharacter.playerPrefab : null;
        
        WeaponData wd = null;
        ItemDrop selectedItem = null;
        if (LoadoutSystem.Instance != null && LoadoutSystem.Instance.Inventory != null)
        {
            selectedItem = LoadoutSystem.Instance.Inventory.FirstOrDefault(x => x.ItemUniqueId == _selectedWeaponUniqueId);
            if (selectedItem != null && weaponRegistry != null && weaponRegistry.allWeapons != null)
            {
                wd = weaponRegistry.allWeapons.FirstOrDefault(w => w != null && (w.name == selectedItem.ItemName || w.weaponName == selectedItem.ItemName));
            }
        }
            
        List<RuneDefinition> runes = new List<RuneDefinition>();
        var selectedRune = runeCatalog.FirstOrDefault(r => r != null && r.runeId == _selectedRuneId);
        if (selectedRune != null) runes.Add(selectedRune);

        LoadoutSelections.SetSelections(chPrefab, wd, runes, selectedItem);
        LoadoutSelections.SaveToPlayerPrefs();
        LoadoutSelections.MarkAsConfigured();

        var gm = GameManager.Instance;
        if (gm != null && !gm.isP2P && chPrefab != null)
        {
            gm.SetChosenPlayerPrefab(chPrefab);
        }

        // Sync for multiplayer if already spawned
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            var sync = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<LoadoutSync>();
            if (sync != null)
            {
                sync.RequestSendSelectionToServer();
            }
        }
    }
}
