using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Script para gerenciar os campos de uma row de estatísticas de habilidade
/// Deve ser anexado ao prefab RowAbilityDmgStat
/// </summary>
public class AbilityStatRow : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("RawImage que mostra o ícone/logo da habilidade")]
    public RawImage abilityIcon;
    
    [Tooltip("TextMeshProUGUI que mostra o nome da habilidade")]
    public TextMeshProUGUI abilityNameText;
    
    [Tooltip("TextMeshProUGUI que mostra o dano da habilidade")]
    public TextMeshProUGUI damageText;

    /// <summary>
    /// Configura os valores da row com os dados da habilidade
    /// </summary>
    /// <param name="abilityName">Nome da habilidade</param>
    /// <param name="damage">Dano total causado pela habilidade</param>
    /// <param name="icon">Textura do ícone da habilidade (opcional)</param>
    public void SetData(string abilityName, float damage, Texture icon = null)
    {
        Debug.Log($"[AbilityStatRow] SetData chamado: {abilityName} = {damage}");
        
        if (abilityNameText != null)
        {
            abilityNameText.text = abilityName;
            Debug.Log($"[AbilityStatRow] Nome configurado: {abilityName}");
        }
        else
        {
            Debug.LogWarning("[AbilityStatRow] abilityNameText é NULL!");
        }
        
        if (damageText != null)
        {
            damageText.text = Mathf.RoundToInt(damage).ToString();
            Debug.Log($"[AbilityStatRow] Dano configurado: {Mathf.RoundToInt(damage)}");
        }
        else
        {
            Debug.LogWarning("[AbilityStatRow] damageText é NULL!");
        }
        
        if (abilityIcon != null && icon != null)
        {
            abilityIcon.texture = icon;
            abilityIcon.gameObject.SetActive(true);
        }
        else if (abilityIcon != null)
        {
            // Se não houver ícone, podemos esconder ou deixar vazio
            abilityIcon.gameObject.SetActive(false);
        }
    }
}
