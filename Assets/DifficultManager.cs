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

    // Propriedades Públicas (Lidas pelo GameManager e Inimigos)
    public float CurrentHealthMult { get; private set; } = 1f;
    public float CurrentDamageMult { get; private set; } = 1f;
    public float CurrentProjectileSpeed { get; private set; } = 10f;
    public float CurrentFireRate { get; private set; } = 2f;
    public float CurrentSightRange { get; private set; } = 999f;

    // Multiplicador de Dificuldade por nº de Jogadores
    public NetworkVariable<float> mpDifficultyMultiplier = new NetworkVariable<float>(1f);
    public float MpDifficultyMultiplier => mpDifficultyMultiplier.Value;

    // Multiplicador de XP de Equipa
    public NetworkVariable<float> netSharedXpMult = new NetworkVariable<float>(1f);
    public float SharedXpMultiplier => netSharedXpMult.Value;

    // (Mutação removida, usamos apenas stats brutos agora)
    public MutationType GlobalMutation { get; private set; } = MutationType.None;

    private int lastInterval = 0;

    public void ResetDifficulty()
    {
        // Volta tudo aos valores base
        CurrentHealthMult = 1f;
        CurrentDamageMult = 1f;
        CurrentProjectileSpeed = baseProjectileSpeed;
        CurrentFireRate = baseFireRate;
        
        lastInterval = 0; 
        GlobalMutation = MutationType.None;

        if (IsServer)
        {
            netSharedXpMult.Value = 1f;
            RecomputeMpMultiplier();
        }
        
        Debug.Log("[DifficultyManager] Dificuldade resetada para x1.");
    }

    private void Update()
    {
        // Só o servidor controla a dificuldade ao longo do tempo
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

    // Chamado pelo GameEventManager nos eventos de Midgame/Endgame
    public void ApplyMidgameBoost(float multiplier)
    {
        CurrentHealthMult *= multiplier;
        CurrentDamageMult *= multiplier;

        // Opcional: Aumentar spawn rate (reduzindo o intervalo de tiro/spawn)
        CurrentFireRate = Mathf.Max(0.1f, CurrentFireRate / 1.2f);

        Debug.Log($"[DifficultyManager] MIDGAME BOOST! HP e Dano multiplicados por {multiplier}");
    }

    // Mantido para compatibilidade, mas não aplica cores/tipos específicos
    public void SetMidgameMutation(MutationType type) => GlobalMutation = type;

    // Mantido para compatibilidade com EnemySpawner, mas agora confia nos multiplicadores globais
    public void ApplyMutationToEnemy(EnemyStats enemy)
    {
        if (enemy == null) return;
        
        // Aqui podes forçar a atualização de stats num inimigo vivo se necessário,
        // mas geralmente os inimigos leem o GameManager.currentEnemyHealthMultiplier no Start().
        // Se quiseres curar ou buffar inimigos já vivos, faz aqui.
    }

    // --- XP LOGIC ---

    public void RequestModifySharedXp(float amount)
    {
        if (IsServer) netSharedXpMult.Value += amount;
        else ModifyXpServerRpc(amount);
    }

    [ServerRpc(RequireOwnership = false)]
    private void ModifyXpServerRpc(float amount) => netSharedXpMult.Value += amount;

    // Chamado pelo GameManager quando alguém apanha um orbe
    public void DistributeXpServer(float amount)
    {
        // 1. Aplica o multiplicador de partilha (se houver)
        float finalXp = amount * SharedXpMultiplier;

        // 2. Adiciona ao Sistema Global de XP
        if (PlayerExperience.Instance != null)
        {
            PlayerExperience.Instance.AddGlobalXP(finalXp);
        }
    }

    // --- UI & UTILS ---

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
            RecomputeMpMultiplier();
        }
    }

    private void RecomputeMpMultiplier()
    {
        int count = Mathf.Max(1, NetworkManager.Singleton.ConnectedClients.Count);
        mpDifficultyMultiplier.Value = Mathf.Pow(Mathf.Max(1, mpPerPlayerMultiplier), count - 1);
    }
}