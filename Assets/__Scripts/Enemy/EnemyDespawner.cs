using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemyDespawner : MonoBehaviour
{
    [Header("Despawn Settings")]
    [Tooltip("The radius around the player. Enemies outside this radius will be removed.")]
    [SerializeField] private float despawnRadius = 60f;
    [Tooltip("How often (in seconds) to check for enemies to remove.")]
    [SerializeField] private float checkInterval = 3f;
    
    [Header("Visual Settings")]
    [Tooltip("Duração do fade-out antes de despawn")]
    [SerializeField] private float fadeOutDuration = 0.5f;
    [Tooltip("Verificar se inimigo está fora da tela antes de despawn")]
    [SerializeField] private bool requireOffScreen = true;

    [Header("Gizmo Settings")]
    [SerializeField] private bool showGizmo = true;

    // Internal References
    private Transform playerTransform; // This will be given to us by the GameManager
    [SerializeField] private EnemySpawner enemySpawner;
    private Camera mainCamera;
    private HashSet<GameObject> despawningEnemies = new HashSet<GameObject>();

    void Start()
    {
        mainCamera = Camera.main;
    }

    public void Initialize(GameObject playerObject)
    {
        // If running in multiplayer and not the server, only the server should run despawner logic.
        if (GameManager.Instance != null && GameManager.Instance.isP2P && !GameManager.Instance.IsServer)
        {
            // disable this component on clients to avoid local-only despawning.
            enabled = false;
            return;
        }

        if (playerObject != null)
        {
            playerTransform = playerObject.transform;
        }
        else
        {
            Debug.LogError("FATAL ERROR: EnemyDespawner received a null player object! Despawner will not work.", this);
            enabled = false;
            return;
        }


        if (enemySpawner == null)
        {
            Debug.LogError("FATAL ERROR: EnemyDespawner could not find the EnemySpawner in the scene!", this);
            enabled = false;
            return;
        }

        // Only start the core logic after a successful initialization.
        StartCoroutine(DespawnEnemiesCoroutine());
        Debug.Log("EnemyDespawner Initialized successfully.");
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
        foreach (EnemyStats enemy in allEnemies)
        {
            if (enemy == null || despawningEnemies.Contains(enemy.gameObject)) continue;

            // Check if enemy is far from ALL players (multiplayer support)
            if (IsEnemyFarFromAllPlayers(enemy.transform.position))
            {
                // NOVO: Verifica se está fora da tela (opcional)
                if (requireOffScreen && !IsCompletelyOffScreen(enemy.gameObject))
                {
                    continue; // Ainda visível, não despawnar
                }

                // Inicia despawn com fade-out
                StartCoroutine(DespawnWithFade(enemy.gameObject));
            }
        }
    }

    private bool IsCompletelyOffScreen(GameObject enemy)
    {
        if (mainCamera == null) 
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return true; // Se não há câmera, considera fora da tela
        }

        // Verifica posição principal do inimigo
        Vector3 viewportPoint = mainCamera.WorldToViewportPoint(enemy.transform.position);
        
        // Margem extra para garantir que está REALMENTE fora da tela
        float margin = 0.2f;
        
        bool isOffScreen = viewportPoint.z < 0f || 
                          viewportPoint.x < -margin || 
                          viewportPoint.x > 1f + margin || 
                          viewportPoint.y < -margin || 
                          viewportPoint.y > 1f + margin;
        
        // Verifica também bounds se tiver renderer
        if (isOffScreen)
        {
            Renderer[] renderers = enemy.GetComponentsInChildren<Renderer>();
            foreach (Renderer rend in renderers)
            {
                if (rend == null) continue;
                
                // Verifica se alguma parte do renderer está visível
                Plane[] planes = GeometryUtility.CalculateFrustumPlanes(mainCamera);
                if (GeometryUtility.TestPlanesAABB(planes, rend.bounds))
                {
                    return false; // Ainda parcialmente visível
                }
            }
        }
        
        return isOffScreen;
    }

    IEnumerator DespawnWithFade(GameObject enemy)
    {
        if (enemy == null) yield break;

        // Marca como em processo de despawn
        despawningEnemies.Add(enemy);

        // Coleta renderers
        Renderer[] renderers = enemy.GetComponentsInChildren<Renderer>();
        
        if (renderers.Length > 0 && fadeOutDuration > 0f)
        {
            Dictionary<Material, Color> originalColors = new Dictionary<Material, Color>();
            
            // Guarda cores originais e ativa transparência
            foreach (Renderer rend in renderers)
            {
                foreach (Material mat in rend.materials)
                {
                    if (mat.HasProperty("_Color"))
                    {
                        originalColors[mat] = mat.color;
                        
                        // Ativa modo transparente
                        if (mat.HasProperty("_Mode"))
                        {
                            mat.SetFloat("_Mode", 3);
                            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                            mat.SetInt("_ZWrite", 0);
                            mat.EnableKeyword("_ALPHABLEND_ON");
                            mat.renderQueue = 3000;
                        }
                    }
                }
            }
            
            // Fade gradual
            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                if (enemy == null) yield break;
                
                elapsed += Time.deltaTime;
                float alpha = 1f - Mathf.Clamp01(elapsed / fadeOutDuration);
                
                foreach (var kvp in originalColors)
                {
                    Material mat = kvp.Key;
                    Color originalColor = kvp.Value;
                    
                    if (mat != null && mat.HasProperty("_Color"))
                    {
                        Color newColor = originalColor;
                        newColor.a = originalColor.a * alpha;
                        mat.color = newColor;
                    }
                }
                
                yield return null;
            }
        }
        
        // Remove despawn list
        despawningEnemies.Remove(enemy);

        if (enemy != null)
        {

            Destroy(enemy);
            
            // Spawn a new enemy
            if (enemySpawner != null)
            {
                enemySpawner.SpawnReplacementEnemy(enemy);
            }
        }
    }

    private bool IsEnemyFarFromAllPlayers(Vector3 enemyPosition)
    {
        // Check if we're in multiplayer mode
        if (GameManager.Instance != null && GameManager.Instance.isP2P && GameManager.Instance.IsServer)
        {
            var allPlayers = FindObjectsByType<PlayerStats>(FindObjectsSortMode.None);
            foreach (var player in allPlayers)
            {
                if (player == null) continue;
                
                float distance = Vector3.Distance(player.transform.position, enemyPosition);
                
                if (distance <= despawnRadius)
                {
                    return false;
                }
            }
            
            return true;
        }
        else
        {
            if (playerTransform != null)
            {
                return Vector3.Distance(playerTransform.position, enemyPosition) > despawnRadius;
            }
            
            return false;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!showGizmo) return;

        // Check if we're in multiplayer mode
        if (Application.isPlaying && GameManager.Instance != null && GameManager.Instance.isP2P)
        {
            // Draw despawn radius for all players
            Gizmos.color = Color.red;
            var allPlayers = FindObjectsByType<PlayerStats>(FindObjectsSortMode.None);
            foreach (var player in allPlayers)
            {
                if (player != null)
                {
                    Gizmos.DrawWireSphere(player.transform.position, despawnRadius);
                }
            }
        }
        else
        {
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
}