using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

// Simple, inspector-driven Loadout panel. Wire your UI Buttons/Texts/Images to these fields.
public class LoadoutPanel : MonoBehaviour
{
    [Header("Data Sources")]
    [Tooltip("List of available characters (PlayerCharacterData). We'll use their playerPrefab for spawning.")]
    public List<PlayerCharacterData> characters = new List<PlayerCharacterData>();
    [Tooltip("Registry of all weapons to scroll through.")]
    public WeaponRegistry weaponRegistry;
    [Tooltip("List of rune definitions shown as selectable rounded images.")]
    public List<RuneDefinition> runeCatalog = new List<RuneDefinition>();

    [Header("Character UI")]
    public Button charLeftButton;
    public Button charRightButton;
    public TextMeshProUGUI characterNameText;
    public Image characterPreviewImage; // optional: sprite portrait

    [Header("Weapon UI")]
    public Button weaponLeftButton;
    public Button weaponRightButton;
    public TextMeshProUGUI weaponNameText;
    public Image weaponIconImage; // optional: weapon icon if you have one in WeaponData

    [Header("Runes UI")]
    [Tooltip("Parent container holding Toggle children for each rune (optional). If left empty, no auto-build.")]
    public Transform runeToggleContainer;
    [Tooltip("Optional Toggle prefab with child Image for the rune icon. If assigned, we'll auto-build the grid.")]
    public Toggle runeTogglePrefab;
    public int maxSelectedRunes = 6;

    [Header("Flow Buttons")]
    public Button applyButton;
    public Button closeButton;

    private int _charIndex = 0;
    private int _weaponIndex = 0;
    private HashSet<string> _selectedRuneIds = new HashSet<string>();
    private readonly List<Toggle> _builtToggles = new List<Toggle>();

    // --- NEW: Cache original container sizes to preserve aspect
    private Dictionary<Image, Vector2> originalContainerSizes = new Dictionary<Image, Vector2>();

    void Awake()
    {
        // Contexts for persistence
        LoadoutSelections.CharacterPrefabsContext = characters.Select(c => c != null ? c.playerPrefab : null).Where(p => p != null).ToList();
        LoadoutSelections.WeaponRegistryContext = weaponRegistry;
        LoadoutSelections.RuneCatalogContext = runeCatalog;

        WireButtons();
        BuildRuneTogglesIfNeeded();

        // Load previous selections if available
        LoadoutSelections.LoadFromPlayerPrefs();
        // Character
        var savedCharPrefab = LoadoutSelections.SelectedCharacterPrefab;
        if (savedCharPrefab != null)
        {
            int idx = characters.FindIndex(c => c != null && c.playerPrefab == savedCharPrefab);
            if (idx >= 0) _charIndex = idx;
        }
        // Weapon
        var savedWeapon = LoadoutSelections.SelectedWeapon;
        if (savedWeapon != null && weaponRegistry != null)
        {
            int wid = weaponRegistry.GetWeaponId(savedWeapon);
            if (wid >= 0) _weaponIndex = wid;
        }
        // Runes
        if (LoadoutSelections.SelectedRunes != null)
        {
            _selectedRuneIds = new HashSet<string>(LoadoutSelections.SelectedRunes.Where(r => r != null).Select(r => r.runeId));
        }

        RefreshUI();
    }

    private void WireButtons()
    {
        if (charLeftButton) charLeftButton.onClick.AddListener(() => { ShiftCharacter(-1); });
        if (charRightButton) charRightButton.onClick.AddListener(() => { ShiftCharacter(1); });
        if (weaponLeftButton) weaponLeftButton.onClick.AddListener(() => { ShiftWeapon(-1); });
        if (weaponRightButton) weaponRightButton.onClick.AddListener(() => { ShiftWeapon(1); });
        if (applyButton) applyButton.onClick.AddListener(Apply);
        if (closeButton) closeButton.onClick.AddListener(() => gameObject.SetActive(false));
    }

    private void BuildRuneTogglesIfNeeded()
    {
        if (runeToggleContainer == null || runeTogglePrefab == null || runeCatalog == null) return;
        // Clear existing
        for (int i = runeToggleContainer.childCount - 1; i >= 0; i--) Destroy(runeToggleContainer.GetChild(i).gameObject);
        _builtToggles.Clear();
        // Build new
        for (int i = 0; i < runeCatalog.Count; i++)
        {
            var rune = runeCatalog[i];
            var toggle = Instantiate(runeTogglePrefab, runeToggleContainer);
            toggle.isOn = _selectedRuneIds.Contains(rune != null ? rune.runeId : "");
            int captured = i;
            toggle.onValueChanged.AddListener(on => ToggleRune(captured, on));
            // Ensure a CanvasGroup for consistent greying when disabled
            var cg = toggle.GetComponent<CanvasGroup>();
            if (cg == null) cg = toggle.gameObject.AddComponent<CanvasGroup>();
            // Try to set icon
            var img = toggle.GetComponentInChildren<Image>();
            if (img != null && rune != null && rune.icon != null) img.sprite = rune.icon;
            _builtToggles.Add(toggle);
        }
    }

    public void Open()
    {
        gameObject.SetActive(true);
        if (runeToggleContainer != null && runeTogglePrefab != null && runeCatalog != null &&
            runeToggleContainer.childCount != runeCatalog.Count)
        {
            BuildRuneTogglesIfNeeded();
        }
        RefreshUI();
    }

    public void OpenLoadout() => Open();
    public void Close() => gameObject.SetActive(false);

    private void RefreshUI()
    {
        // Character display
        var ch = (characters != null && characters.Count > 0) ? characters[Wrap(_charIndex, characters.Count)] : null;
        if (ch != null)
        {
            if (characterNameText) characterNameText.text = ch.characterName;

            if (characterPreviewImage)
            {
                Sprite preview = null;
                if (ch.playerPrefab != null)
                {
                    var sr = ch.playerPrefab.GetComponentInChildren<SpriteRenderer>();
                    if (sr != null) preview = sr.sprite;
                }
                SetImageSpritePreserveAspect(characterPreviewImage, preview);
            }
        }

        // Weapon display
        if (weaponRegistry != null && weaponRegistry.allWeapons != null && weaponRegistry.allWeapons.Count > 0)
        {
            var wd = weaponRegistry.GetWeaponData(Wrap(_weaponIndex, weaponRegistry.allWeapons.Count));
            if (weaponNameText) weaponNameText.text = wd != null ? wd.name : "-";
            if (weaponIconImage)
            {
                Sprite icon = wd != null ? wd.icon : null;
                SetImageSpritePreserveAspect(weaponIconImage, icon);
            }
        }

        // Runes toggles states
        if (runeToggleContainer != null)
        {
            for (int i = 0; i < runeToggleContainer.childCount && i < runeCatalog.Count; i++)
            {
                var rune = runeCatalog[i];
                var t = runeToggleContainer.GetChild(i).GetComponent<Toggle>();
                if (t != null)
                {
                    bool desired = rune != null && _selectedRuneIds.Contains(rune.runeId);
                    if (t.isOn != desired) t.SetIsOnWithoutNotify(desired);
                }
            }
            ApplyRowInteractivityFromSelection();
        }
    }

    private void SetImageSpritePreserveAspect(Image image, Sprite sprite)
    {
        image.sprite = sprite;
        image.enabled = sprite != null;
        if (sprite == null) return;

        RectTransform rt = image.rectTransform;

        // Cache original container size the first time
        if (!originalContainerSizes.ContainsKey(image))
            originalContainerSizes[image] = rt.rect.size;

        Vector2 containerSize = originalContainerSizes[image];

        float spriteAspect = sprite.rect.width / sprite.rect.height;
        float containerAspect = containerSize.x / containerSize.y;

        if (spriteAspect > containerAspect)
        {
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, containerSize.x);
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, containerSize.x / spriteAspect);
        }
        else
        {
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, containerSize.y);
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, containerSize.y * spriteAspect);
        }
    }

    private void ShiftCharacter(int dir)
    {
        if (characters == null || characters.Count == 0) return;
        _charIndex = Wrap(_charIndex + dir, characters.Count);
        RefreshUI();
    }

    private void ShiftWeapon(int dir)
    {
        if (weaponRegistry == null || weaponRegistry.allWeapons == null || weaponRegistry.allWeapons.Count == 0) return;
        _weaponIndex = Wrap(_weaponIndex + dir, weaponRegistry.allWeapons.Count);
        RefreshUI();
    }

    private int Wrap(int value, int count)
    {
        if (count <= 0) return 0;
        int m = value % count;
        if (m < 0) m += count;
        return m;
    }

    public void ToggleRune(int index, bool on)
    {
        if (index < 0 || index >= runeCatalog.Count) return;
        var rune = runeCatalog[index];
        if (rune == null || string.IsNullOrEmpty(rune.runeId)) return;

        if (on)
        {
            int row = rune.rowIndex;
            var alreadyInRow = _selectedRuneIds.FirstOrDefault(id =>
            {
                var r = runeCatalog.FirstOrDefault(rc => rc != null && rc.runeId == id);
                return r != null && r.rowIndex == row;
            });

            if (!string.IsNullOrEmpty(alreadyInRow) && alreadyInRow != rune.runeId)
            {
                var t = GetToggleAt(index);
                if (t != null) t.SetIsOnWithoutNotify(false);
                return;
            }

            if (string.IsNullOrEmpty(alreadyInRow) && _selectedRuneIds.Count >= maxSelectedRunes)
            {
                var t = GetToggleAt(index);
                if (t != null) t.SetIsOnWithoutNotify(false);
                return;
            }

            _selectedRuneIds.Add(rune.runeId);
            SetRowInteractivity(row, false, index);
        }
        else
        {
            _selectedRuneIds.Remove(rune.runeId);
            int row = rune.rowIndex;
            bool anySelectedInRow = _selectedRuneIds.Any(id =>
            {
                var r = runeCatalog.FirstOrDefault(rc => rc != null && rc.runeId == id);
                return r != null && r.rowIndex == row;
            });
            if (!anySelectedInRow) SetRowInteractivity(row, true, -1);
        }
    }

    private Toggle GetToggleAt(int index)
    {
        if (index < 0 || index >= _builtToggles.Count) return null;
        return _builtToggles[index];
    }

    private void ApplyRowInteractivityFromSelection()
    {
        var rows = runeCatalog.Where(r => r != null).Select(r => r.rowIndex).Distinct();
        foreach (var row in rows)
        {
            bool anySelectedInRow = _selectedRuneIds.Any(id =>
            {
                var r = runeCatalog.FirstOrDefault(rc => rc != null && rc.runeId == id);
                return r != null && r.rowIndex == row;
            });
            if (anySelectedInRow)
            {
                int selectedIndex = -1;
                for (int i = 0; i < runeCatalog.Count; i++)
                {
                    var r = runeCatalog[i];
                    if (r != null && _selectedRuneIds.Contains(r.runeId) && r.rowIndex == row)
                    {
                        selectedIndex = i; break;
                    }
                }
                SetRowInteractivity(row, false, selectedIndex);
            }
            else
            {
                SetRowInteractivity(row, true, -1);
            }
        }
    }

    private void SetRowInteractivity(int rowIndex, bool enable, int exceptIndex)
    {
        for (int i = 0; i < runeCatalog.Count && i < _builtToggles.Count; i++)
        {
            var def = runeCatalog[i];
            if (def == null || def.rowIndex != rowIndex || i == exceptIndex) continue;
            var t = _builtToggles[i];
            if (t == null) continue;
            t.interactable = enable;
            var cg = t.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = enable ? 1f : 0.5f;
        }
    }

    public void Apply()
    {
        PlayerCharacterData ch = (characters != null && characters.Count > 0) ? characters[Wrap(_charIndex, characters.Count)] : null;
        WeaponData wd = (weaponRegistry != null && weaponRegistry.allWeapons != null && weaponRegistry.allWeapons.Count > 0)
            ? weaponRegistry.GetWeaponData(Wrap(_weaponIndex, weaponRegistry.allWeapons.Count))
            : null;
        List<RuneDefinition> runes = runeCatalog.Where(r => r != null && _selectedRuneIds.Contains(r.runeId)).ToList();

        LoadoutSelections.SetSelections(ch != null ? ch.playerPrefab : null, wd, runes);
        LoadoutSelections.SaveToPlayerPrefs();
        LoadoutSelections.MarkAsConfigured();

        var gm = GameManager.Instance;
        if (gm != null && !gm.isP2P && ch != null && ch.playerPrefab != null)
        {
            gm.SetChosenPlayerPrefab(ch.playerPrefab);
        }

        gameObject.SetActive(false);
    }
}
