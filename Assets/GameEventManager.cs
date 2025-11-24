using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class GameEventManager : NetworkBehaviour
{
    [Header("Prefabs & Settings")]
    [SerializeField] private GameObject bossPrefab;
    [SerializeField] private Transform bossSpawnPoint; 
    
    [Header("Midgame Cinematic (DISABLED)")]
    [SerializeField] private float cinematicDuration = 5.0f;
    [SerializeField] private Vector3 bossCameraOffset = new Vector3(0, 0, -12); 
    [SerializeField] private float cameraTransitionSpeed = 1.5f;

    [Header("Spawn Offsets")]
    [Tooltip("Offset for midgame (Disabled for now)")]
    [SerializeField] private Vector3 spawnOffsetMidgame = new Vector3(100f, 2f, 100f);
    
    [Tooltip("Fixed Y height for ENDGAME spawn")]
    [SerializeField] private float endgameSpawnHeight = 13f;
    
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
    public float GetBossDamage() => (bossStats != null) ? bossStats.MaxHealth - bossStats.CurrentHealth : cachedBossDamage;
    public void ClearBossCache() => cachedBossDamage = 0;

    private void Update()
    {
        if (GameManager.Instance.isP2P && !IsServer) return;

        float timeRemaining = GameManager.Instance.GetRemainingTime();
        float totalTime = GameManager.Instance.totalGameTime;
        float midTime = totalTime / 2f;

        // ====================================================================
        // EVENTO 1: MIDGAME (COMENTADO / DESATIVADO TEMPORARIAMENTE)
        // ====================================================================
        /*
        if (!midgameTriggered && timeRemaining <= midTime)
        {
            midgameTriggered = true;
            StartCoroutine(Sequence_Midgame());
        }
        */

        // ====================================================================
        // EVENTO 2: ENDGAME (ATIVO)
        // ====================================================================
        if (!endgameTriggered && timeRemaining <= 10.0f && timeRemaining > 0f)
        {
            endgameTriggered = true;
            StartCoroutine(Sequence_Endgame());
        }
    }

    /* 
    // MIDGAME SEQUENCE COMENTADA
    private IEnumerator Sequence_Midgame()
    {
        Debug.Log("[GameEventManager] MIDGAME: Starting cinematic...");
        int boostType = Random.Range(0, 3);
        if (GameManager.Instance.difficultyManager != null)
            GameManager.Instance.difficultyManager.ApplyMidgameBoost(difficultyMultiplier);

        string buffText = "PERIGO!";
        switch(boostType) { case 0: buffText = "DOUBLE HP"; break; case 1: buffText = "DOUBLE DAMAGE"; break; case 2: buffText = "DOUBLE SPEED"; break; }

        GameObject tempBoss = SpawnBossInternal(true);
        ulong bossId = 99999; 
        if (tempBoss != null && tempBoss.TryGetComponent<NetworkObject>(out var no)) bossId = no.NetworkObjectId;

        if (GameManager.Instance.isP2P) PlayMidgameCinematicClientRpc(bossId, buffText);
        else yield return StartCoroutine(PlayMidgameCinematicLocal(tempBoss ? tempBoss.transform : null, buffText));

        if (GameManager.Instance.isP2P) yield return new WaitForSecondsRealtime(cinematicDuration);

        if (tempBoss != null) {
            if (GameManager.Instance.isP2P && IsServer && tempBoss.TryGetComponent<NetworkObject>(out var no2)) no2.Despawn(true); 
            else Destroy(tempBoss);
        }
    }
    */

    // ========================================================================
    // EVENTO 2: ENDGAME
    // ========================================================================
    private IEnumerator Sequence_Endgame()
    {
        Debug.Log("[GameEventManager] ENDGAME: Final boss spawns!");

        // Spawn Boss (FALSE = ENDGAME MODE = Force Y 13)
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
            // MIDGAME (Desativado, mas a lógica está aqui)
            finalPos = basePos + spawnOffsetMidgame;
        }
        else
        {
            // ENDGAME: Usa X e Z da Tag, mas FORÇA Y = 13
            finalPos = new Vector3(basePos.x, endgameSpawnHeight, basePos.z);
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

    /*
    [ClientRpc]
    private void PlayMidgameCinematicClientRpc(ulong bossId, string buffText) { ... }
    
    private IEnumerator PlayMidgameCinematicLocal(Transform bossTransform, string buffText) { ... }
    */

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