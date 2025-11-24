using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class BossVisuals : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private TextMeshProUGUI bonusText; // Arraste o texto do Canvas World Space aqui
    [SerializeField] private Transform emojiSpawnPoint; // Um ponto vazio acima da cabeça do boss

    [Header("Emoji Settings")]
    [SerializeField] private Sprite[] emojis; // Arraste suas sprites de emoji aqui
    [SerializeField] private GameObject emojiPrefab; // Um prefab simples com SpriteRenderer e um script de subir/sumir
    [SerializeField] private float emojiInterval = 0.5f;

    private bool isCinematicActive = false;

    public void SetupVisuals(string bonusType)
    {
        if (bonusText != null)
        {
            bonusText.text = $"DOBRO DE\n<color=red>{bonusType.ToUpper()}</color>";
            bonusText.gameObject.SetActive(true);
        }
        
        // Começa a chuva de emojis
        isCinematicActive = true;
        StartCoroutine(SpawnEmojisRoutine());
    }

    public void StopVisuals()
    {
        isCinematicActive = false;
        if (bonusText != null) bonusText.gameObject.SetActive(false);
    }

    private IEnumerator SpawnEmojisRoutine()
    {
        while (isCinematicActive)
        {
            if (emojis.Length > 0 && emojiPrefab != null)
            {
                SpawnRandomEmoji();
            }
            yield return new WaitForSecondsRealtime(emojiInterval);
        }
    }

    private void SpawnRandomEmoji()
    {
        Vector3 offset = new Vector3(Random.Range(-1f, 1f), Random.Range(0f, 1f), 0);
        GameObject newEmoji = Instantiate(emojiPrefab, emojiSpawnPoint.position + offset, Quaternion.identity);
        
        SpriteRenderer sr = newEmoji.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sprite = emojis[Random.Range(0, emojis.Length)];
        }

        // Movimento simples para cima (se o prefab não tiver script)
        StartCoroutine(FloatAndDestroy(newEmoji));
    }

    private IEnumerator FloatAndDestroy(GameObject obj)
    {
        float timer = 0;
        Vector3 startPos = obj.transform.position;
        while (timer < 2f)
        {
            if (obj == null) yield break;
            obj.transform.position = startPos + Vector3.up * (timer * 2f);
            timer += Time.unscaledDeltaTime;
            yield return null;
        }
        Destroy(obj);
    }
}