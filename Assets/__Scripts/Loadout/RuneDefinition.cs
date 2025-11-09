using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Rune", menuName = "GoofySurvivors/Loadout/Rune Definition")]
public class RuneDefinition : ScriptableObject
{
    [Header("Identity")]
    public string runeId; // unique key for persistence
    public string displayName;
    [TextArea]
    public string description;
    public Sprite icon;

    [Header("Layout")]
    [Tooltip("Row index for LoL-like selection (select exactly one per row). Runes sharing the same row index are mutually exclusive.")]
    public int rowIndex = 0;

    [Header("Bonuses Applied On Spawn")]
    public List<StatBonus> bonuses = new List<StatBonus>();

    [Header("Runtime Effects (Optional)")]
    [Tooltip("Event-driven effects that can listen and react during gameplay (e.g., +1% speed per consumable, +20% healing).")]
    public List<RuneEffect> effects = new List<RuneEffect>();
}
