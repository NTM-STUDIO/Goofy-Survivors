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

        // 3. Determine Text
        string buffText = "PERIGO!";
        switch(boostType)
        {
            case 0: buffText = "DOUBLE HP"; break;
            case 1: buffText = "DOUBLE DAMAGE"; break;
            case 2: buffText = "DOUBLE SPEED"; break;
        }

        // 4. Spawn Boss (TRUE = Midgame Offset)
        GameObject tempBoss = SpawnBossInternal(true);
        ulong bossId = 99999; 

        if (tempBoss != null)
        {
            if (tempBoss.TryGetComponent<NetworkObject>(out var no)) bossId = no.NetworkObjectId;
        }

        // 5. Play Cinematic
        if (GameManager.Instance.isP2P) PlayMidgameCinematicClientRpc(bossId, buffText);
        else yield return StartCoroutine(PlayMidgameCinematicLocal(tempBoss ? tempBoss.transform : null, buffText));

        if (GameManager.Instance.isP2P) yield return new WaitForSecondsRealtime(cinematicDuration);

        // 6. Despawn Boss
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
            
            if (GameManager.Instance.isP2P) ShowEndgameMessageClientRpc(bossId);
            else ShowEndgameMessageLocal(finalBoss.transform);
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
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            Camera playerCam = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponentInChildren<Camera>();
            if (playerCam != null) return playerCam;
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            Camera playerCam = playerObj.GetComponentInChildren<Camera>();
            if (playerCam != null) return playerCam;
        }

        return Camera.main;
    }

    // --- CLIENT LOGIC (MIDGAME) ---

    [ClientRpc]
    private void PlayMidgameCinematicClientRpc(ulong bossId, string buffText)
    {
        Transform target = null;
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(bossId, out var bossObj))
            target = bossObj.transform;
        
        StartCoroutine(PlayMidgameCinematicLocal(target, buffText));
    }

    private IEnumerator PlayMidgameCinematicLocal(Transform bossTransform, string buffText)
    {
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
            var camScript = cam.GetComponent<MonoBehaviour>(); 
            if (GameManager.Instance.uiManager != null)
            {
                var advCam = FindObjectOfType<AdvancedCameraController>();
                if (advCam != null && advCam.enabled)
                {
                    cameraControllerScript = advCam;
                    cameraControllerScript.enabled = false; 
                }
            }

            Vector3 originalPos = cam.transform.position;
            
            // Force Camera Y to 44 during cinematic
            Vector3 bossPos = bossTransform != null ? bossTransform.position : Vector3.zero;
            Vector3 targetPos = new Vector3(
                bossPos.x + bossCameraOffset.x, 
                44f, 
                bossPos.z + bossCameraOffset.z
            );
            
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

            if (cameraControllerScript != null) cameraControllerScript.enabled = true;
        }
        else
        {
            yield return new WaitForSecondsRealtime(cinematicDuration);
        }

        if (bossTransform != null) bossTransform.GetComponent<BossVisuals>()?.StopVisuals();

        GameManager.Instance.RequestPause(false); 
        GameManager.Instance.SetGameState(GameManager.GameState.Playing);
    }

    // --- CLIENT LOGIC (ENDGAME) ---

    [ClientRpc]
    private void ShowEndgameMessageClientRpc(ulong bossId)
    {
        Transform target = null;
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(bossId, out var bossObj))
            target = bossObj.transform;
        
        ShowEndgameMessageLocal(target);
    }

    private void ShowEndgameMessageLocal(Transform bossTransform)
    {
        if (bossTransform != null)
        {
            var visuals = bossTransform.GetComponent<BossVisuals>();
            visuals?.SetupVisuals("FINAL BOSS\nSURVIVE!");
            StartCoroutine(HideVisualsAfterDelay(visuals, 3.0f));
        }
    }

    private IEnumerator HideVisualsAfterDelay(BossVisuals visuals, float delay)
    {
        yield return new WaitForSeconds(delay);
        visuals?.StopVisuals();
    }
}