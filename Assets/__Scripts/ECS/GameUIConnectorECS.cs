using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameUIConnectorECS : MonoBehaviour
{
    [Header("UI References")]
    public Slider HealthBar;
    public Slider XpBar;
    public TextMeshProUGUI LevelText;

    private EntityManager _entityManager;
    private Entity _playerEntity;

    void Start()
    {
        // Pega referência ao mundo ECS padrão
        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
    }

    void Update()
    {
        // 1. Tenta encontrar o Player se ainda não tivermos a referência
        if (_playerEntity == Entity.Null || !_entityManager.Exists(_playerEntity))
        {
            // Busca a primeira entidade que tenha os dados do Player
            var query = _entityManager.CreateEntityQuery(typeof(PlayerStatsData));
            if (!query.IsEmpty)
            {
                _playerEntity = query.GetSingletonEntity();
            }
            return;
        }

        // 2. Se achou o player, lê os dados e atualiza a tela
        if (_entityManager.HasComponent<PlayerStatsData>(_playerEntity))
        {
            var stats = _entityManager.GetComponentData<PlayerStatsData>(_playerEntity);

            // Atualiza Barra de Vida
            if (HealthBar != null)
            {
                HealthBar.maxValue = stats.MaxHealth;
                HealthBar.value = stats.CurrentHealth;
            }

            // Atualiza Barra de XP
            if (XpBar != null)
            {
                XpBar.maxValue = stats.MaxXP;
                XpBar.value = stats.CurrentXP;
            }

            // Atualiza Texto de Nível
            if (LevelText != null)
            {
                LevelText.text = "LVL " + stats.Level;
            }
        }
    }
}
