using UnityEngine;
using Unity.Netcode;
using System.Linq;

public class DifficultyManager : NetworkBehaviour
{
    [Header("Scaling Settings")]
    [SerializeField] private float difficultyIncreaseInterval = 30f;
    [SerializeField] private float strengthMultiplier = 1.1f;
    [SerializeField] private float mpPerPlayerMultiplier = 1.5f;

    [Header("Caster Settings")]
    [SerializeField] private float baseProjectileSpeed = 20f;
    [SerializeField] private float baseFireRate = 2f;
    [SerializeField] private float speedMultiplier = 1.05f;
    [SerializeField] private float fireRateMultiplier = 1.05f;

    // Propriedades Públicas
    public float CurrentHealthMult { get; private set; } = 1f;
    public float CurrentDamageMult { get; private set; } = 1f;
    public float CurrentProjectileSpeed { get; private set; } = 10f;
    public float CurrentFireRate { get; private set; } = 2f;
    public float CurrentSightRange { get; private set; } = 999f;

    public NetworkVariable<float> mpDifficultyMultiplier = new NetworkVariable<float>(1f);
    public float MpDifficultyMultiplier => mpDifficultyMultiplier.Value;

    // XP
    public NetworkVariable<float> netSharedXpMult = new NetworkVariable<float>(1f);
    public float SharedXpMultiplier => netSharedXpMult.Value;

    // Mutação
    public MutationType GlobalMutation { get; private set; } = MutationType.None;

    private int lastInterval = 0;

    public void ApplyMidgameBoost(float multiplier)
    {
        CurrentHealthMult *= multiplier;
        CurrentDamageMult *= multiplier;

        // Opcional: Aumentar um pouco a velocidade ou spawn rate também
        CurrentFireRate = Mathf.Max(0.1f, CurrentFireRate / 1.2f);

        Debug.Log($"[DifficultyManager] MIDGAME BOOST! HP e Dano multiplicados por {multiplier}");
    }

public void ResetDifficulty()
    {
        // Volta tudo aos valores base
        CurrentHealthMult = 1f;
        CurrentDamageMult = 1f;
        CurrentProjectileSpeed = baseProjectileSpeed;
        CurrentFireRate = baseFireRate;
        
        // Se tiveres variáveis privadas de controle, reseta-as também
        // lastInterval = 0; 
        
        GlobalMutation = MutationType.None;

        if (IsServer)
        {
            netSharedXpMult.Value = 1f;
            // RecomputeMpMultiplier(); // Mantém isto se for MP
        }
        
        Debug.Log("[DifficultyManager] Dificuldade resetada para x1.");
    }

    private void Update()
    {
        if (!IsServer || GameManager.Instance.CurrentState != GameManager.GameState.Playing) return;

        float timeElapsed = GameManager.Instance.totalGameTime - GameManager.Instance.GetRemainingTime();
        int currentInterval = Mathf.FloorToInt(timeElapsed / difficultyIncreaseInterval);

        if (currentInterval > lastInterval)
        {
            lastInterval = currentInterval;
            IncreaseDifficulty();
        }
    }

    private void IncreaseDifficulty()
    {
        CurrentHealthMult *= strengthMultiplier;
        CurrentDamageMult *= strengthMultiplier;
        CurrentProjectileSpeed *= speedMultiplier;
        CurrentFireRate = Mathf.Max(0.2f, CurrentFireRate / fireRateMultiplier);
    }

    public void SetMidgameMutation(MutationType type) => GlobalMutation = type;

    public void ApplyMutationToEnemy(EnemyStats enemy)
    {
        if (GlobalMutation == MutationType.None || enemy == null) return;
        if (enemy.CurrentMutation == GlobalMutation) return;

        // Lógica de aplicação visual e stats
        var renderer = enemy.GetComponentInChildren<SpriteRenderer>();

        switch (GlobalMutation)
        {
            case MutationType.Health:
                enemy.baseHealth *= 2f;
                if (renderer) renderer.color = Color.green;
                break;
            case MutationType.Damage:
                enemy.baseDamage *= 2f;
                if (renderer) renderer.color = Color.red;
                break;
            case MutationType.Speed:
                enemy.moveSpeed *= 2f;
                if (renderer) renderer.color = Color.blue;
                break;
        }

        // Tenta setar a propriedade via reflection se necessário, ou diretamente se for public
        var prop = typeof(EnemyStats).GetProperty("CurrentMutation");
        if (prop != null) prop.SetValue(enemy, GlobalMutation);
    }

    public void RequestModifySharedXp(float amount)
    {
        if (IsServer) netSharedXpMult.Value += amount;
        else ModifyXpServerRpc(amount);
    }

    [ServerRpc(RequireOwnership = false)]
    private void ModifyXpServerRpc(float amount) => netSharedXpMult.Value += amount;

    public void DistributeXpServer(float amount)
    {
        DistributeXpClientRpc(amount * SharedXpMultiplier);
    }

    [ClientRpc]
    private void DistributeXpClientRpc(float amount)
    {
        FindObjectOfType<PlayerExperience>()?.AddXPFromServerScaled(amount);
    }

    public void PresentRarity(string rarityName)
    {
        if (IsServer) PresentRarityClientRpc(rarityName);
        else PresentRarityServerRpc(rarityName);
    }

    [ServerRpc(RequireOwnership = false)]
    private void PresentRarityServerRpc(string n) => PresentRarityClientRpc(n);

    [ClientRpc]
    private void PresentRarityClientRpc(string n)
    {
        var um = FindObjectOfType<UpgradeManager>();
        if (um) um.PresentGuaranteedRarityChoices(um.GetRarityTiers().FirstOrDefault(r => r.name == n));
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += (id) => RecomputeMpMultiplier();
            NetworkManager.Singleton.OnClientDisconnectCallback += (id) => RecomputeMpMultiplier();
        }
    }

    private void RecomputeMpMultiplier()
    {
        int count = Mathf.Max(1, NetworkManager.Singleton.ConnectedClients.Count);
        mpDifficultyMultiplier.Value = Mathf.Pow(Mathf.Max(1, mpPerPlayerMultiplier), count - 1);
    }
}