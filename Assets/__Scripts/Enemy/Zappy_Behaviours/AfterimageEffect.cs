using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AfterimageEffect : MonoBehaviour
{
    [Header("Afterimage Settings")]
    [Tooltip("Intervalo entre spawns de afterimages")]
    public float spawnInterval = 0.01f;
    
    [Tooltip("Cor das afterimages")]
    public Color afterimageColor = new Color(1, 1, 0, 0.5f);
    
    [Tooltip("Tempo para fade out")]
    public float fadeTime = 0.5f;
    
    [Tooltip("Offset na ordem de renderização")]
    public int sortingOrderOffset = -1;
    
    private bool isActive;
    private SpriteRenderer originalSprite;
    private List<GameObject> activeAfterimages = new List<GameObject>();
    
    private void Awake()
    {
        originalSprite = GetComponent<SpriteRenderer>();
        if (originalSprite == null)
        {
            originalSprite = GetComponentInChildren<SpriteRenderer>();
        }
    }
    
    /// <summary>
    /// Inicia a criação de afterimages.
    /// </summary>
    public void StartEffect()
    {
        if (!isActive)
        {
            StartCoroutine(SpawnAfterimages());
        }
    }
    
    /// <summary>
    /// Para a criação de afterimages.
    /// </summary>
    public void StopEffect()
    {
        isActive = false;
    }
    
    private IEnumerator SpawnAfterimages()
    {
        isActive = true;
        
        while (isActive)
        {
            CreateAfterimage();
            yield return new WaitForSeconds(spawnInterval);
        }
    }
    
    private void CreateAfterimage()
    {
        if (originalSprite == null) return;
        
        GameObject afterimage = new GameObject("Afterimage");
        afterimage.transform.position = transform.position;
        afterimage.transform.rotation = transform.rotation;
        afterimage.transform.localScale = transform.localScale;
        
        SpriteRenderer afterimageSprite = afterimage.AddComponent<SpriteRenderer>();
        afterimageSprite.sprite = originalSprite.sprite;
        afterimageSprite.color = afterimageColor;
        afterimageSprite.sortingLayerName = originalSprite.sortingLayerName;
        afterimageSprite.sortingOrder = originalSprite.sortingOrder + sortingOrderOffset;
        afterimageSprite.flipX = originalSprite.flipX;
        afterimageSprite.flipY = originalSprite.flipY;
        
        // Track the afterimage for cleanup
        activeAfterimages.Add(afterimage);
        
        StartCoroutine(FadeAndDestroy(afterimage, afterimageSprite));
    }
    
    private IEnumerator FadeAndDestroy(GameObject afterimage, SpriteRenderer sprite)
    {
        float elapsedTime = 0;
        Color startColor = sprite.color;
        
        while (elapsedTime < fadeTime)
        {
            if (afterimage == null)
            {
                // Remove from tracking list if destroyed externally
                activeAfterimages.Remove(afterimage);
                yield break;
            }
            
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Lerp(startColor.a, 0, elapsedTime / fadeTime);
            sprite.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            yield return null;
        }
        
        if (afterimage != null)
        {
            // Remove from tracking list before destroying
            activeAfterimages.Remove(afterimage);
            Destroy(afterimage);
        }
    }

    private void OnDestroy()
    {
        // Stop the effect
        StopEffect();
        
        // Clean up all afterimages
        CleanupAfterimages();
    }
    
    private void OnDisable()
    {
        // Stop the effect
        StopEffect();
        
        // Clean up all active afterimages when disabled
        CleanupAfterimages();
    }
    
    private void CleanupAfterimages()
    {
        // Stop all coroutines to prevent new afterimages
        StopAllCoroutines();
        
        // Destroy all tracked afterimages
        foreach (GameObject afterimage in activeAfterimages)
        {
            if (afterimage != null)
            {
                Destroy(afterimage);
            }
        }
        
        // Clear the tracking list
        activeAfterimages.Clear();
    }
}
