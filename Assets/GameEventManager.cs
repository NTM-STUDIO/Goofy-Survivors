using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class GameEventManager : NetworkBehaviour
{
    [Header("Prefabs & Settings")]
    [SerializeField] private GameObject bossPrefab;
    [SerializeField] private Transform bossSpawnPoint; 
    
    [Header("Midgame Cinematic")]
    [SerializeField] private float cinematicDuration = 5.0f;
    // Forçamos a câmara para Y=44 no código, este vector serve para X e Z
    [SerializeField] private Vector3 bossCameraOffset = new Vector3(0, 0, -12); 
    [SerializeField] private float cameraTransitionSpeed = 1.5f;

    [Header("Spawn Offsets")]
    [Tooltip("Offset added to BossSpawn tag for MIDGAME only")]
    [SerializeField] private Vector3 spawnOffsetMidgame = new Vector3(100f, 0f, 100f); // Y será ignorado e forçado a 13
    
    [Tooltip("Fixed Y height for BOTH spawns (Midgame and Endgame)")]
    [SerializeField] private float spawnHeightY = 13f;
    
    [Header("Difficulty")]
    [SerializeField] private float difficultyMultiplier = 2.0f;

    public EnemyStats bossStats { get; private set; }
    private float cachedBossDamage;
    
    private bool midgameTriggered = false;
    private bool endgameTriggered = false;

    private MonoBehaviour cameraControllerScript;

    public void ResetEvents()
    {
        midgameTriggered = false;
        endgameTriggered = false;
        bossStats = null;
        cachedBossDamage = 0;
        bossSpawnPoint = null; 
    }

    public void CacheBossDamage(float dmg) => cachedBossDamage = dmg;
    
    public float GetBossDamage()
    {
        if (bossStats != null) return bossStats.MaxHealth - bossStats.CurrentHealth;
        return cachedBossDamage;
    }
    
    public void ClearBossCache() => cachedBossDamage = 0;

    private void Update()
    {
        // Singleplayer ou Server executa a lógica de tempo
        if (GameManager.Instance.isP2P && !IsServer) return;

        float timeRemaining = GameManager.Instance.GetRemainingTime();
        float totalTime = GameManager.Instance.totalGameTime;
        float midTime = totalTime / 2f;

        // --- EVENTO 1: MIDGAME ---
        if (!midgameTriggered && timeRemaining <= midTime)
        {
            midgameTriggered = true;
            StartCoroutine(Sequence_Midgame());
        }

        // --- EVENTO 2: ENDGAME (10s Finais) ---
        if (!endgameTriggered && timeRemaining <= 10.0f && timeRemaining > 0f)
        {
            endgameTriggered = true;
            StartCoroutine(Sequence_Endgame());
        }
    }

    // ========================================================================
    // EVENTO 1: MIDGAME
    // ========================================================================
    private IEnumerator Sequence_Midgame()
    {
        Debug.Log("[GameEventManager] MIDGAME: Iniciando cinemática...");

        // 1. Pick Random Boost Type
        int boostType = Random.Range(0, 3);
        
        // 2. Apply Difficulty
        if (GameManager.Instance.difficultyManager != null)
            GameManager.Instance.difficultyManager.ApplyMidgameBoost(difficultyMultiplier);

        // 3. Determine Text (em português!)
        string buffText = "PERIGO!";
        switch(boostType)
        {
            case 0: buffText = "VIDA"; break;
            case 1: buffText = "DANO"; break;
            case 2: buffText = "VELOCIDADE"; break;
        }

        // 4. Spawn Boss (TRUE = Midgame Offset)
        GameObject tempBoss = SpawnBossInternal(true);
        ulong bossId = 99999; 

        if (tempBoss != null)
        {
            if (tempBoss.TryGetComponent<NetworkObject>(out var no)) bossId = no.NetworkObjectId;
        }

        // 5. Play Cinematic
        if (GameManager.Instance.isP2P)
        {
            // Pequeno delay para garantir que o boss está spawned na rede
            yield return new WaitForSecondsRealtime(0.1f);
            
            // Envia para TODOS os clientes (incluindo o host)
            PlayMidgameCinematicClientRpc(bossId, buffText);
            
            // Espera pela duração da cinemática
            yield return new WaitForSecondsRealtime(cinematicDuration);
        }
        else
        {
            yield return StartCoroutine(PlayMidgameCinematicLocal(tempBoss ? tempBoss.transform : null, buffText));
        }

        // 6. Stop Visuals ANTES de destruir o boss (em todos os clientes)
        if (tempBoss != null)
        {
            if (GameManager.Instance.isP2P && IsServer)
            {
                StopBossVisualsClientRpc(bossId);
            }
            else
            {
                var visuals = tempBoss.GetComponent<BossVisuals>();
                if (visuals != null) visuals.StopVisuals();
            }
        }

        // 7. Despawn Boss
        if (tempBoss != null)
        {
            if (GameManager.Instance.isP2P && IsServer && tempBoss.TryGetComponent<NetworkObject>(out var no))
            {
                no.Despawn(true); 
            }
            else
            {
                Destroy(tempBoss);
            }
        }
    }

    // ========================================================================
    // EVENTO 2: ENDGAME
    // ========================================================================
    private IEnumerator Sequence_Endgame()
    {
        Debug.Log("[GameEventManager] ENDGAME: Final boss spawns!");

        // Spawn Boss (FALSE = Endgame/No Offset)
        GameObject finalBoss = SpawnBossInternal(false);
        
        if (finalBoss != null)
        {
            bossStats = finalBoss.GetComponent<EnemyStats>();
            ulong bossId = 0;
            if (finalBoss.TryGetComponent<NetworkObject>(out var no)) bossId = no.NetworkObjectId;
            
            // Inicia o tracking de dano para emojis nos milestones
            var visuals = finalBoss.GetComponent<BossVisuals>();
            if (visuals != null && bossStats != null)
            {
                visuals.StartDamageTracking(bossStats);
            }
            
            // Teleporta todos os jogadores para o BossSpawnPoint (perto do host)
            Vector3 tpPosition = bossSpawnPoint != null ? bossSpawnPoint.position : finalBoss.transform.position;
            
            if (GameManager.Instance.isP2P)
            {
                // Teleporta todos os clientes para a posição do boss spawn
                TeleportAllPlayersClientRpc(tpPosition, bossId);
            }
            else
            {
                // Singleplayer: teleporta o jogador local
                TeleportLocalPlayer(tpPosition);
                
                // Inicia tracking de dano
                if (visuals != null && bossStats != null)
                {
                    visuals.StartDamageTracking(bossStats);
                }
            }
        }

        yield return null;
    }

    // ========================================================================
    // HELPER FUNCTIONS
    // ========================================================================

    private GameObject SpawnBossInternal(bool useMidgameOffset)
    {
        if (bossSpawnPoint == null)
        {
            GameObject spawnObj = GameObject.FindGameObjectWithTag("BossSpawn");
            if (spawnObj != null) bossSpawnPoint = spawnObj.transform;
        }

        Vector3 basePos = bossSpawnPoint != null ? bossSpawnPoint.position : Vector3.zero;
        Vector3 finalPos;

        if (useMidgameOffset)
        {
            // MIDGAME: Tag + OffsetX/Z + FORCE Y 13
            finalPos = basePos + spawnOffsetMidgame;
            finalPos.y = spawnHeightY; // <--- FORÇA Y 13 AQUI
        }
        else
        {
            // ENDGAME: Tag X/Z + FORCE Y 13
            finalPos = new Vector3(basePos.x, spawnHeightY, basePos.z);
        }

        if (GameManager.Instance.isP2P && IsServer)
        {
            GameObject obj = Instantiate(bossPrefab, finalPos, Quaternion.identity);
            obj.GetComponent<NetworkObject>().Spawn(true);
            return obj;
        }
        else if (!GameManager.Instance.isP2P)
        {
            return Instantiate(bossPrefab, finalPos, Quaternion.identity);
        }
        return null;
    }

    private Camera GetLocalPlayerCamera()
    {
        // Multiplayer: encontra a câmara do player LOCAL deste cliente
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && 
            NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            var localPlayerObj = NetworkManager.Singleton.LocalClient.PlayerObject;
            Camera playerCam = localPlayerObj.GetComponentInChildren<Camera>();
            
            if (playerCam != null)
            {
                Debug.Log($"[GameEventManager] Câmara encontrada no player local (MP): {localPlayerObj.name}");
                return playerCam;
            }
            else
            {
                Debug.LogWarning($"[GameEventManager] Player local encontrado ({localPlayerObj.name}) mas sem câmara!");
            }
        }

        // Singleplayer: encontra por tag
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            Camera playerCam = playerObj.GetComponentInChildren<Camera>();
            if (playerCam != null)
            {
                Debug.Log($"[GameEventManager] Câmara encontrada por tag (SP): {playerObj.name}");
                return playerCam;
            }
            else
            {
                Debug.LogWarning($"[GameEventManager] Player encontrado ({playerObj.name}) mas sem câmara como child!");
            }
        }
        else
        {
            Debug.LogWarning("[GameEventManager] Nenhum objeto com tag 'Player' encontrado!");
        }

        // Último fallback
        Debug.LogWarning("[GameEventManager] A usar Camera.main como fallback!");
        return Camera.main;
    }

    // --- CLIENT LOGIC (MIDGAME) ---

    [ClientRpc]
    private void PlayMidgameCinematicClientRpc(ulong bossId, string buffText)
    {
        Debug.Log($"[GameEventManager] PlayMidgameCinematicClientRpc chamado. IsHost: {IsHost}, IsClient: {IsClient}, BossId: {bossId}");
        
        Transform target = null;
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(bossId, out var bossObj))
        {
            target = bossObj.transform;
            Debug.Log($"[GameEventManager] Boss encontrado na rede: {target.name}");
        }
        else
        {
            Debug.LogWarning($"[GameEventManager] Boss com ID {bossId} não encontrado na rede!");
        }
        
        StartCoroutine(PlayMidgameCinematicLocal(target, buffText));
    }

    [ClientRpc]
    private void StopBossVisualsClientRpc(ulong bossId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(bossId, out var bossObj))
        {
            var visuals = bossObj.GetComponent<BossVisuals>();
            if (visuals != null) visuals.StopVisuals();
        }
    }

    private IEnumerator PlayMidgameCinematicLocal(Transform bossTransform, string buffText)
    {
        Debug.Log($"[GameEventManager] PlayMidgameCinematicLocal iniciado. IsHost: {IsHost}, IsClient: {IsClient}");
        
        GameManager.Instance.RequestPause(true, false); 
        GameManager.Instance.SetGameState(GameManager.GameState.Cinematic);

        if (bossTransform != null)
        {
            var visuals = bossTransform.GetComponent<BossVisuals>();
            visuals?.SetupVisuals(buffText);
        }

        Camera cam = GetLocalPlayerCamera();

        if (cam != null)
        {
            Debug.Log($"[GameEventManager] Câmara obtida: {cam.name}, posição: {cam.transform.position}");
            
            // Desativa o controller da câmara no player local
            var advCam = cam.GetComponentInParent<AdvancedCameraController>();
            if (advCam == null) advCam = cam.GetComponent<AdvancedCameraController>();
            if (advCam == null) advCam = FindObjectOfType<AdvancedCameraController>();
            
            if (advCam != null && advCam.enabled)
            {
                cameraControllerScript = advCam;
                cameraControllerScript.enabled = false;
                Debug.Log("[GameEventManager] AdvancedCameraController desativado");
            }

            Vector3 originalPos = cam.transform.position;
            
            // Force Camera Y to 44 during cinematic
            Vector3 bossPos = bossTransform != null ? bossTransform.position : Vector3.zero;
            Vector3 targetPos = new Vector3(
                bossPos.x + bossCameraOffset.x, 
                44f, 
                bossPos.z + bossCameraOffset.z
            );
            
            Debug.Log($"[GameEventManager] Movendo câmara de {originalPos} para {targetPos}");
            
            // GO
            float timer = 0;
            while(timer < 1f)
            {
                timer += Time.unscaledDeltaTime * cameraTransitionSpeed;
                cam.transform.position = Vector3.Lerp(originalPos, targetPos, Mathf.SmoothStep(0f, 1f, timer));
                yield return null;
            }

            // STAY
            yield return new WaitForSecondsRealtime(cinematicDuration - 2.0f); 

            // RETURN
            timer = 0;
            while(timer < 1f)
            {
                timer += Time.unscaledDeltaTime * cameraTransitionSpeed;
                cam.transform.position = Vector3.Lerp(targetPos, originalPos, Mathf.SmoothStep(0f, 1f, timer));
                yield return null;
            }
            
            cam.transform.position = originalPos;

            if (cameraControllerScript != null) 
            {
                cameraControllerScript.enabled = true;
                Debug.Log("[GameEventManager] AdvancedCameraController reativado");
            }
        }
        else
        {
            Debug.LogWarning("[GameEventManager] Câmara é NULL! A esperar sem mover...");
            yield return new WaitForSecondsRealtime(cinematicDuration);
        }

        if (bossTransform != null) bossTransform.GetComponent<BossVisuals>()?.StopVisuals();

        GameManager.Instance.RequestPause(false); 
        GameManager.Instance.SetGameState(GameManager.GameState.Playing);
    }

    // --- CLIENT LOGIC (ENDGAME) ---

    [ClientRpc]
    private void TeleportAllPlayersClientRpc(Vector3 tpPosition, ulong bossId)
    {
        // Inicia tracking de dano em todos os clientes
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(bossId, out var bossObj))
        {
            var visuals = bossObj.GetComponent<BossVisuals>();
            var stats = bossObj.GetComponent<EnemyStats>();
            if (visuals != null && stats != null)
            {
                visuals.StartDamageTracking(stats);
            }
        }
        
        // Teleporta o jogador local para a posição
        TeleportLocalPlayer(tpPosition);
    }

    private void TeleportLocalPlayer(Vector3 position)
    {
        GameObject localPlayer = null;
        
        // Multiplayer: encontra o jogador local
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient?.PlayerObject != null)
        {
            localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject.gameObject;
        }
        // Singleplayer: encontra por tag
        else
        {
            localPlayer = GameObject.FindGameObjectWithTag("Player");
        }
        
        if (localPlayer != null)
        {
            // Offset para não spawnar todos no mesmo ponto exato
            Vector3 randomOffset = new Vector3(
                Random.Range(-3f, 3f),
                0f,
                Random.Range(-3f, 3f)
            );
            
            // Teleporta o jogador
            localPlayer.transform.position = position + randomOffset;
            
            Debug.Log($"[GameEventManager] Jogador teleportado para {position + randomOffset}");
        }
    }

    private IEnumerator HideVisualsAfterDelay(BossVisuals visuals, float delay)
    {
        yield return new WaitForSeconds(delay);
        visuals?.StopVisuals();
    }
}