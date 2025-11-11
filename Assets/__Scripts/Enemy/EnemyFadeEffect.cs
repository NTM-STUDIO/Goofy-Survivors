using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Componente que controla o efeito de fade-in dos inimigos.
/// É adicionado automaticamente pelo spawner e funciona em todos os clientes.
/// </summary>
public class EnemyFadeEffect : MonoBehaviour
{
    private bool isInitialized = false;
    
    void Start()
    {
        // Se o fade não foi iniciado pelo spawner, inicia automaticamente
        if (!isInitialized)
        {
            StartFadeIn(0.5f);
        }
    }

    public void StartFadeIn(float duration)
    {
        isInitialized = true;
        StartCoroutine(FadeInCoroutine(duration));
    }

    IEnumerator FadeInCoroutine(float duration)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) 
        {
            Destroy(this); // Remove componente se não houver renderers
            yield break;
        }
        
        Dictionary<Material, Color> originalColors = new Dictionary<Material, Color>();
        List<Material> materialInstances = new List<Material>();
        
        // Cria instâncias dos materiais para não afetar outros objetos
        foreach (Renderer rend in renderers)
        {
            Material[] mats = rend.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                Material mat = mats[i];
                if (mat.HasProperty("_Color"))
                {
                    Color originalColor = mat.color;
                    originalColors[mat] = originalColor;
                    materialInstances.Add(mat);
                    
                    // Define transparente no início
                    Color transparent = originalColor;
                    transparent.a = 0f;
                    mat.color = transparent;
                    
                    // Ativa modo transparente se shader suportar
                    if (mat.HasProperty("_Mode"))
                    {
                        mat.SetFloat("_Mode", 3);
                        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        mat.SetInt("_ZWrite", 0);
                        mat.DisableKeyword("_ALPHATEST_ON");
                        mat.EnableKeyword("_ALPHABLEND_ON");
                        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        mat.renderQueue = 3000;
                    }
                }
            }
        }
        
        // Fade in gradual
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsed / duration);
            
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
        
        // Restaura cores e configurações originais
        foreach (var kvp in originalColors)
        {
            Material mat = kvp.Key;
            Color originalColor = kvp.Value;
            
            if (mat != null && mat.HasProperty("_Color"))
            {
                mat.color = originalColor;
                
                // Restaura modo opaco
                if (mat.HasProperty("_Mode"))
                {
                    mat.SetFloat("_Mode", 0);
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    mat.SetInt("_ZWrite", 1);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.DisableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = -1;
                }
            }
        }
        
        // Remove este componente após o fade completar
        Destroy(this);
    }

    void OnDestroy()
    {
        // Cleanup: garante que os materiais ficam opacos
        StopAllCoroutines();
    }
}
