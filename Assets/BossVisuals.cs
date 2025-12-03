using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class BossVisuals : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private TextMeshProUGUI bonusText;
    [SerializeField] private Transform textCanvas;
    [SerializeField] private Transform emojiSpawnPoint;

    [Header("Emoji Settings")]
    [SerializeField] private Sprite[] emojis;
    [SerializeField] private GameObject emojiPrefab;
    [SerializeField] private float emojiInterval = 0.15f;
    [SerializeField] private float emojiSpawnOffsetX = 2f;
    [SerializeField] private float emojiSpawnRandomY = 1.5f;

    [Header("Emoji Text Fallback")]
    [SerializeField] private string[] emojiTexts = new string[] { "💀", "🔥", "⚡", "💥", "😈", "👹", "☠️", "🎃", "👻", "😱", "🤯", "💣", "⚔️", "🗡️" };

    [Header("Damage Milestone Settings")]
    [SerializeField] private int emojisPerMilestone = 10; // Quantos emojis spawnar por milestone
    [SerializeField] private float milestoneEmojiInterval = 0.05f; // Intervalo entre emojis do milestone

    // Milestones de dano (em ordem)
    private readonly float[] damageMilestones = new float[] 
    { 
        10000f,   // 10k
        50000f,   // 50k
        100000f,  // 100k
        150000f,  // 150k
        200000f,  // 200k
        250000f,  // 250k
        300000f,  // 300k
        400000f,  // 400k
        500000f,  // 500k
        600000f,  // 600k
        700000f,  // 700k
        800000f,  // 800k
        900000f,  // 900k
        1000000f  // 1M
    };

    private bool isCinematicActive = false;
    private bool isDamageModeActive = false;
    private List<GameObject> activeEmojis = new List<GameObject>();
    private Camera mainCam;
    private Coroutine emojiCoroutine;
    
    // Damage tracking
    private EnemyStats trackedBoss;
    private int currentMilestoneIndex = 0;
    private float lastDamageChecked = 0f;

    private void Start()
    {
        mainCam = Camera.main;
        
        if (textCanvas == null && bonusText != null)
        {
            textCanvas = bonusText.transform.parent != null ? bonusText.transform.parent : bonusText.transform;
        }
    }

    private void Update()
    {
        // Modo de tracking de dano para o boss final
        if (isDamageModeActive && trackedBoss != null)
        {
            float totalDamage = trackedBoss.MaxHealth - trackedBoss.CurrentHealth;
            
            // Verifica se atingiu um novo milestone
            while (currentMilestoneIndex < damageMilestones.Length && 
                   totalDamage >= damageMilestones[currentMilestoneIndex])
            {
                // Atingiu um milestone! Spam de emojis!
                TriggerMilestoneEmojis(damageMilestones[currentMilestoneIndex]);
                currentMilestoneIndex++;
            }
        }
    }

    private void LateUpdate()
    {
        if (!isCinematicActive && !isDamageModeActive) return;
        
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null) return;

        // Billboard APENAS para o Canvas do texto
        if (textCanvas != null && textCanvas.gameObject.activeSelf)
        {
            textCanvas.LookAt(textCanvas.position + mainCam.transform.forward);
        }
    }

    // ========================================================================
    // MODO MIDGAME (Cinemática com texto "DOBRO DE...")
    // ========================================================================
    public void SetupVisuals(string bonusType)
    {
        mainCam = Camera.main;
        
        if (bonusText != null)
        {
            bonusText.text = $"DOBRO DE\n<color=red>{bonusType.ToUpper()}</color>";
            bonusText.gameObject.SetActive(true);
        }
        
        if (textCanvas != null)
        {
            textCanvas.gameObject.SetActive(true);
        }
        
        ClearEmojis();
        
        isCinematicActive = true;
        emojiCoroutine = StartCoroutine(SpawnEmojisRoutine());
    }

    public void StopVisuals()
    {
        isCinematicActive = false;
        
        if (emojiCoroutine != null)
        {
            StopCoroutine(emojiCoroutine);
            emojiCoroutine = null;
        }
        
        if (bonusText != null) bonusText.gameObject.SetActive(false);
        if (textCanvas != null) textCanvas.gameObject.SetActive(false);
        
        ClearEmojis();
    }

    // ========================================================================
    // MODO ENDGAME (Tracking de dano com emojis em milestones)
    // ========================================================================
    public void StartDamageTracking(EnemyStats boss)
    {
        mainCam = Camera.main;
        trackedBoss = boss;
        currentMilestoneIndex = 0;
        lastDamageChecked = 0f;
        isDamageModeActive = true;
        
        // Esconde o texto - só queremos emojis!
        if (bonusText != null) bonusText.gameObject.SetActive(false);
        if (textCanvas != null) textCanvas.gameObject.SetActive(false);
        
        Debug.Log("[BossVisuals] Damage tracking iniciado! Milestones: 10k, 50k, 100k... até 1M");
    }

    public void StopDamageTracking()
    {
        isDamageModeActive = false;
        trackedBoss = null;
        ClearEmojis();
    }

    private void TriggerMilestoneEmojis(float milestone)
    {
        string milestoneText = FormatDamageNumber(milestone);
        Debug.Log($"[BossVisuals] MILESTONE ATINGIDO: {milestoneText} de dano!");
        
        // Mostra texto do milestone brevemente
        if (bonusText != null && textCanvas != null)
        {
            StartCoroutine(ShowMilestoneText(milestoneText));
        }
        
        // Spam de emojis!
        StartCoroutine(SpawnMilestoneEmojis());
    }

    private string FormatDamageNumber(float damage)
    {
        if (damage >= 1000000f) return $"{damage / 1000000f:0.#}M";
        if (damage >= 1000f) return $"{damage / 1000f:0.#}K";
        return damage.ToString("0");
    }

    private IEnumerator ShowMilestoneText(string milestoneText)
    {
        if (bonusText != null)
        {
            bonusText.text = $"<color=yellow>{milestoneText}</color>\n<color=red>DANO!</color>";
            bonusText.gameObject.SetActive(true);
        }
        if (textCanvas != null) textCanvas.gameObject.SetActive(true);
        
        yield return new WaitForSeconds(1.5f);
        
        if (bonusText != null) bonusText.gameObject.SetActive(false);
        if (textCanvas != null) textCanvas.gameObject.SetActive(false);
    }

    private IEnumerator SpawnMilestoneEmojis()
    {
        for (int i = 0; i < emojisPerMilestone; i++)
        {
            SpawnRandomEmoji();
            yield return new WaitForSeconds(milestoneEmojiInterval);
        }
    }

    // ========================================================================
    // EMOJI SPAWNING (Comum aos dois modos)
    // ========================================================================
    private void OnDestroy()
    {
        ClearEmojis();
    }

    private void ClearEmojis()
    {
        StopAllCoroutines();
        emojiCoroutine = null;
        
        foreach (var emoji in activeEmojis)
        {
            if (emoji != null) Destroy(emoji);
        }
        activeEmojis.Clear();
    }

    private IEnumerator SpawnEmojisRoutine()
    {
        while (isCinematicActive)
        {
            SpawnRandomEmoji();
            yield return new WaitForSecondsRealtime(emojiInterval);
        }
    }

    private void SpawnRandomEmoji()
    {
        Vector3 spawnPos;
        if (emojiSpawnPoint != null)
        {
            spawnPos = emojiSpawnPoint.position;
        }
        else if (bonusText != null && bonusText.gameObject.activeSelf)
        {
            spawnPos = bonusText.transform.position + Vector3.right * emojiSpawnOffsetX;
        }
        else
        {
            // Spawn acima do boss
            spawnPos = transform.position + Vector3.up * 4f + Vector3.right * emojiSpawnOffsetX;
        }

        Vector3 offset = new Vector3(Random.Range(-0.5f, 1f), Random.Range(-emojiSpawnRandomY, emojiSpawnRandomY), 0);
        Vector3 finalPos = spawnPos + offset;

        if (emojis != null && emojis.Length > 0 && emojiPrefab != null)
        {
            GameObject newEmoji = Instantiate(emojiPrefab, finalPos, Quaternion.identity);
            
            SpriteRenderer sr = newEmoji.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sprite = emojis[Random.Range(0, emojis.Length)];
            }

            activeEmojis.Add(newEmoji);
            StartCoroutine(FloatAndDestroy(newEmoji));
        }
        else
        {
            StartCoroutine(SpawnTextEmoji(finalPos));
        }
    }

    private IEnumerator SpawnTextEmoji(Vector3 pos)
    {
        GameObject emojiObj = new GameObject("EmojiText");
        emojiObj.transform.position = pos;
        
        if (mainCam != null)
        {
            emojiObj.transform.LookAt(emojiObj.transform.position + mainCam.transform.forward);
        }

        TextMeshPro tmp = emojiObj.AddComponent<TextMeshPro>();
        tmp.text = emojiTexts[Random.Range(0, emojiTexts.Length)];
        tmp.fontSize = 8;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.sortingOrder = 100;

        activeEmojis.Add(emojiObj);

        float timer = 0;
        Vector3 startPos = emojiObj.transform.position;
        Color startColor = tmp.color;
        Vector3 camRight = mainCam != null ? mainCam.transform.right : Vector3.right;
        
        while (timer < 1.5f)
        {
            if (emojiObj == null) yield break;
            
            emojiObj.transform.position = startPos + Vector3.up * (timer * 3f) + camRight * (timer * 0.5f);
            
            if (mainCam != null)
            {
                emojiObj.transform.LookAt(emojiObj.transform.position + mainCam.transform.forward);
            }
            
            if (timer > 1f)
            {
                float alpha = 1f - ((timer - 1f) / 0.5f);
                tmp.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            }
            
            timer += Time.unscaledDeltaTime;
            yield return null;
        }
        
        activeEmojis.Remove(emojiObj);
        Destroy(emojiObj);
    }

    private IEnumerator FloatAndDestroy(GameObject obj)
    {
        float timer = 0;
        Vector3 startPos = obj.transform.position;
        SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
        Color startColor = sr != null ? sr.color : Color.white;
        Vector3 camRight = mainCam != null ? mainCam.transform.right : Vector3.right;
        
        while (timer < 2f)
        {
            if (obj == null) yield break;
            
            obj.transform.position = startPos + Vector3.up * (timer * 2.5f) + camRight * (timer * 0.3f);
            
            if (mainCam != null)
            {
                obj.transform.LookAt(obj.transform.position + mainCam.transform.forward);
            }
            
            obj.transform.Rotate(0, 0, Time.unscaledDeltaTime * 90f, Space.Self);
            
            if (sr != null && timer > 1.2f)
            {
                float alpha = 1f - ((timer - 1.2f) / 0.8f);
                sr.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            }
            
            timer += Time.unscaledDeltaTime;
            yield return null;
        }
        
        activeEmojis.Remove(obj);
        Destroy(obj);
    }
}
