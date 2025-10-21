using UnityEngine;
using System.Collections;

public class EnemyDespawner : MonoBehaviour
{
    [Header("Despawn Settings")]
    [Tooltip("O raio à volta do jogador. Inimigos fora deste raio serão removidos.")]
    public float despawnRadius = 50f;

    [Tooltip("Com que frequência (em segundos) verificar se há inimigos a remover.")]
    public float checkInterval = 2f;

    [Header("Gizmo Settings")]
    [Tooltip("Mostrar o raio de despawn no editor?")]
    public bool showGizmo = true;

    private Transform playerTransform;
    private EnemySpawner enemySpawner; // Referência para o spawner

    void Start()
    {
        // Encontrar o jogador
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
        else
        {
            Debug.LogError("EnemyDespawner: Jogador não encontrado! Certifique-se de que o objeto do jogador tem a tag 'Player'.");
            enabled = false;
            return;
        }

        // Encontrar o EnemySpawner na cena
        enemySpawner = FindObjectOfType<EnemySpawner>();
        if (enemySpawner == null)
        {
            Debug.LogError("EnemyDespawner: EnemySpawner não encontrado na cena!");
            enabled = false;
            return;
        }

        StartCoroutine(DespawnEnemiesCoroutine());
    }

    IEnumerator DespawnEnemiesCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(checkInterval);
            DespawnFarEnemies();
        }
    }

    void DespawnFarEnemies()
    {
        if (playerTransform == null || enemySpawner == null) return;

        EnemyStats[] allEnemies = FindObjectsByType<EnemyStats>(FindObjectsSortMode.None);
        int respawnedCount = 0;

        foreach (EnemyStats enemy in allEnemies)
        {
            if (Vector3.Distance(playerTransform.position, enemy.transform.position) > despawnRadius)
            {
                // Em vez de destruir, chama a função de respawn
                enemySpawner.RespawnEnemy(enemy.gameObject);
                respawnedCount++;
            }
        }

        if (respawnedCount > 0)
        {
            Debug.Log($"EnemyDespawner: Reposicionados {respawnedCount} inimigos por estarem fora do raio de {despawnRadius} unidades.");
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!showGizmo) return;

        // Tenta encontrar o jogador no editor para desenhar o gizmo mesmo sem o jogo a correr
        if (playerTransform == null)
        {
             GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }
        
        if (playerTransform != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(playerTransform.position, despawnRadius);
        }
    }
}
