using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerExperience : MonoBehaviour
{
    [Header("UI References")]
    public Slider xpSlider;
    public TMP_Text levelText;

    [Header("Experience State")]
    public int currentLevel = 1;
    public float currentXP = 0;
    public float xpToNextLevel = 100;

    // --- VARIÁVEIS DE CONFIGURAÇÃO DA CURVA DE XP ---
    // Estas novas variáveis substituem o antigo 'xpIncreaseFactor'
    [Header("XP Curve Settings")]
    [Tooltip("XP fixo adicionado por nível para os níveis iniciais.")]
    public float earlyLevelXpBonus = 75f;
    [Tooltip("Fator de escala para os níveis 11-25.")]
    public float midGameScalingFactor = 1.2f;
    [Tooltip("Fator de escala para os níveis 26-35.")]
    public float lateGameScalingFactor = 1.1f;
    [Tooltip("Fator de escala para os níveis 36+.")]
    public float endGameScalingFactor = 1.01f;

    [Header("System References")]
    [SerializeField] private UpgradeManager upgradeManager;
    [SerializeField] private UIManager uiManager;

    public void Start()
    {
        upgradeManager = FindFirstObjectByType<UpgradeManager>();
        uiManager = FindFirstObjectByType<UIManager>();
        xpSlider = uiManager.xpSlider;
        levelText = uiManager.levelText;

        UpdateUI();
    }

    public void AddXP(float xp)
    {
        currentXP += xp;

        // Usa um loop 'while' para o caso de o jogador ganhar XP suficiente
        // para subir vários níveis de uma só vez.
        while (currentXP >= xpToNextLevel)
        {
            LevelUp();
        }

        UpdateUI();
    }

    private void LevelUp()
    {
        // Subtrai o XP necessário para o nível que acabamos de completar.
        currentXP -= xpToNextLevel;
        currentLevel++;
        
        // --- LÓGICA DA NOVA CURVA DE XP ---
        // Aqui calculamos o XP necessário para o *próximo* nível
        // com base no nível que o jogador ACABOU DE ATINGIR.

        // Regra 1: Até o nível 10, a progressão é aditiva.
        if (currentLevel <= 10)
        {
            xpToNextLevel += earlyLevelXpBonus;
        }
        // Regra 2: Do nível 11 ao 25, a progressão usa o fator de 1.2.
        else if (currentLevel <= 25)
        {
            xpToNextLevel *= midGameScalingFactor;
        }
        // Regra 3: Do nível 26 ao 35, a progressão usa o fator de 1.1.
        else if (currentLevel <= 35)
        {
            xpToNextLevel *= lateGameScalingFactor;
        }
        // Regra 4: A partir do nível 36, a progressão usa o fator de 1.01.
        else
        {
            xpToNextLevel *= endGameScalingFactor;
        }

        // Arredonda o valor para evitar números decimais estranhos na barra de XP (ex: 245.9999).
        xpToNextLevel = Mathf.FloorToInt(xpToNextLevel);

        // Chama o upgrade manager para apresentar as escolhas de upgrade.
        if (upgradeManager != null)
        {
            upgradeManager.AddLevelUpToQueue();
        }
    }

    // Criei uma função separada para atualizar a UI para evitar código repetido.
    private void UpdateUI()
    {
        if(xpSlider == null || levelText == null) return;
        
        levelText.text = $"Lvl: {currentLevel}";
        xpSlider.maxValue = xpToNextLevel;
        xpSlider.value = currentXP;
    }
}