using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public static class AbilityDamageTracker
{
    private static readonly Dictionary<string, float> damageTotals = new Dictionary<string, float>();

    public static void Reset()
    {
        damageTotals.Clear();
    }

    public static void RecordDamage(string abilityKey, float amount, GameObject source)
    {
        if (amount <= 0f || source == null)
        {
            return;
        }

        if (!HasAbilityTag(source))
        {
            return;
        }

        string key = string.IsNullOrWhiteSpace(abilityKey) ? source.name : abilityKey;
        if (damageTotals.TryGetValue(key, out float current))
        {
            damageTotals[key] = current + amount;
        }
        else
        {
            damageTotals[key] = amount;
        }
    }

    public static void LogTotals()
    {
        if (damageTotals.Count == 0)
        {
            Debug.Log("[AbilityDamageTracker] No ability damage recorded for tagged abilities.");
            return;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("===== Ability Damage Totals =====");
        foreach (var entry in damageTotals.OrderByDescending(kvp => kvp.Value))
        {
            builder.AppendLine($"{entry.Key}: {entry.Value:0.##}");
        }

        Debug.Log(builder.ToString());
    }

    private static bool HasAbilityTag(GameObject source)
    {
        if (source.CompareTag("Ability"))
        {
            return true;
        }

        Transform current = source.transform.parent;
        while (current != null)
        {
            if (current.CompareTag("Ability"))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }
}
