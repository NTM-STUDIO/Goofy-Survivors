using UnityEngine;
using System.Linq;

// Requer um Collider 3D em vez de 2D
[RequireComponent(typeof(Collider))] 
public class GuaranteedRarityGiver : MonoBehaviour
{
    [Header("Configuração da Recompensa")]
    [Tooltip("Marque esta caixa para dar a raridade Mítica ('Godly'). Desmarque para dar a raridade Sombra ('Shadow').")]
    [SerializeField] private bool giveMythicalRarity = false;

    [Header("Referências")]
    [Tooltip("Se deixado em branco, tentará encontrar o UpgradeManager na cena.")]
    [SerializeField] private UpgradeManager upgradeManager;

    private RarityTier shadowRarity;
    private RarityTier mythicRarity;
    private bool hasBeenTriggered = false;

    void Start()
    {
        // Esta parte do código é a mesma e funciona em 3D
        if (upgradeManager == null)
        {
            upgradeManager = FindObjectOfType<UpgradeManager>();
        }

        if (upgradeManager == null)
        {
            Debug.LogError("ERRO: O GuaranteedRarityGiver não conseguiu encontrar o UpgradeManager na cena!", this);
            enabled = false;
            return;
        }

        var allRarities = upgradeManager.GetRarityTiers();
        shadowRarity = allRarities.FirstOrDefault(r => r.name == "Shadow");
        mythicRarity = allRarities.FirstOrDefault(r => r.name == "Godly"); 

        if (shadowRarity == null || mythicRarity == null)
        {
            Debug.LogError("ERRO: Não foi possível encontrar as raridades 'Shadow' ou 'Godly' na lista do UpgradeManager. Verifique os nomes no Inspector.", this);
            enabled = false;
        }
    }

    // Usa OnTriggerEnter para física 3D, com um Collider 3D
    private void OnTriggerEnter(Collider other)
    {
        // A lógica interna é idêntica
        if (!hasBeenTriggered && other.CompareTag("Player"))
        {
            hasBeenTriggered = true; 

            RarityTier rarityToGive = giveMythicalRarity ? mythicRarity : shadowRarity;

            upgradeManager.PresentGuaranteedRarityChoices(rarityToGive);
            
            // Opcional: Desativa o objeto do NPC para que ele não possa ser usado novamente
            // gameObject.SetActive(false); 
            Destroy(gameObject); // Ou destrói o objeto
        }
    }
}