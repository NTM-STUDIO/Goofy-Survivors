using UnityEngine;
using System.Collections;

/// <summary>
/// This script controls a permanent shield that is always active.
/// It changes its color temporarily when it "absorbs" a mutation buff.
/// </summary>
public class ShieldWeapon : MonoBehaviour
{
    [Header("Visuals")]
    [Tooltip("The SpriteRenderer for the shield's visual effect.")]
    [SerializeField] private SpriteRenderer shieldRenderer;

    [Header("Colors")]
    [Tooltip("The default color of the shield when no buff is active.")]
    [SerializeField] private Color defaultColor = Color.white;
    [Tooltip("The color to use when a Health mutation is absorbed.")]
    [SerializeField] private Color healthBuffColor = Color.green;
    [Tooltip("The color to use when a Damage mutation is absorbed.")]
    [SerializeField] private Color damageBuffColor = Color.red;
    [Tooltip("The color to use when a Speed mutation is absorbed.")]
    [SerializeField] private Color speedBuffColor = Color.blue;

    private Coroutine activeBuffCoroutine;

    void Awake()
    {
        if (shieldRenderer == null)
        {
            shieldRenderer = GetComponentInChildren<SpriteRenderer>();
        }
        if (shieldRenderer == null)
        {
            Debug.LogError("ShieldWeapon: No SpriteRenderer found!", this);
            enabled = false;
            return;
        }
        
        // Set the shield to its default color when it's first created.
        shieldRenderer.color = defaultColor;
    }

    /// <summary>
    /// Called by the WeaponController when a mutation is stolen.
    /// This activates the temporary color change.
    /// </summary>
    public void AbsorbBuff(MutationType stolenType, float duration)
    {
        // If a buff is already active, stop its timer coroutine so the new one can start.
        if (activeBuffCoroutine != null)
        {
            StopCoroutine(activeBuffCoroutine);
        }

        // Start the new buff timer.
        activeBuffCoroutine = StartCoroutine(BuffColorRoutine(stolenType, duration));
    }

    private IEnumerator BuffColorRoutine(MutationType stolenType, float duration)
    {
        // 1. Set the new buff color.
        Color chosenColor = defaultColor;
        switch (stolenType)
        {
            case MutationType.Health: chosenColor = healthBuffColor; break;
            case MutationType.Damage: chosenColor = damageBuffColor; break;
            case MutationType.Speed: chosenColor = speedBuffColor; break;
        }
        shieldRenderer.color = chosenColor;

        // 2. Wait for the buff duration to expire.
        yield return new WaitForSeconds(duration);

        // 3. Revert back to the default color.
        shieldRenderer.color = defaultColor;

        // 4. Mark the coroutine as finished.
        activeBuffCoroutine = null;
    }
}