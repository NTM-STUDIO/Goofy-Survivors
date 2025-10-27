using UnityEngine;
using System.Linq;

[RequireComponent(typeof(Collider))]
public class GuaranteedRarityGiver : MonoBehaviour
{
    [Header("Configuração da Recompensa")]
    [Tooltip("Marque esta caixa para dar a raridade Mítica ('Godly'). Desmarque para dar a raridade Sombra ('Shadow').")]
    [SerializeField] private bool giveMythicalRarity = false;

    private UpgradeManager upgradeManager;
    private RarityTier shadowRarity;
    private RarityTier mythicRarity;
    private bool hasBeenTriggered = false;

    private void Awake()
    {
        // Use singleton instead of FindObjectOfType
        upgradeManager = UpgradeManager.Instance;

        if (upgradeManager == null)
        {
            Debug.LogError("❌ GuaranteedRarityGiver: Não foi possível encontrar UpgradeManager.Instance! Certifique-se de que o UpgradeManager existe na cena.", this);
            enabled = false;
            return;
        }
    }

    private void Start()
    {
        // Pull rarities once from the UpgradeManager
        var allRarities = upgradeManager.GetRarityTiers();

        shadowRarity = allRarities.FirstOrDefault(r => r.name == "Shadow");
        mythicRarity = allRarities.FirstOrDefault(r => r.name == "Godly");

        if (shadowRarity == null || mythicRarity == null)
        {
            Debug.LogError("❌ GuaranteedRarityGiver: Não foi possível encontrar as raridades 'Shadow' ou 'Godly'. Verifique os nomes no UpgradeManager.", this);
            enabled = false;
            return;
        }

        Debug.Log($"✅ GuaranteedRarityGiver pronto. Dará {(giveMythicalRarity ? "Godly" : "Shadow")} upgrades.", this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasBeenTriggered || !other.CompareTag("Player")) return;

        hasBeenTriggered = true;

        RarityTier rarityToGive = giveMythicalRarity ? mythicRarity : shadowRarity;

        if (rarityToGive == null)
        {
            Debug.LogError("GuaranteedRarityGiver: raridade não encontrada ao tentar dar upgrade!", this);
            return;
        }

        upgradeManager.PresentGuaranteedRarityChoices(rarityToGive);

        // Destroy the giver after use
        Destroy(gameObject);
    }
}
