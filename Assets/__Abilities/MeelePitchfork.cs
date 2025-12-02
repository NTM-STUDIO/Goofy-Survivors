using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class MeleePitchfork : NetworkBehaviour
{
    [Header("References")]
    [Tooltip("ARRASTA O OBJETO FILHO (Visuals) PARA AQUI!")]
    [SerializeField] private Transform visualTransform; 

    [Header("Settings")]
    [SerializeField] private float stabDistance = 2.5f; 
    [SerializeField] private AnimationCurve stabCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.5f, 1), new Keyframe(1, 0));

    // Ajuste extra se o sprite estiver virado para cima (põe 90 ou -90 aqui)
    [SerializeField] private float visualRotationOffset = 0f; 

    private PlayerStats ownerStats;
    private WeaponData weaponData;
    private List<GameObject> hitEnemies = new List<GameObject>(); 
    private bool isSinglePlayer;

    public void Initialize(Vector3 direction, PlayerStats stats, WeaponData data)
    {
        ownerStats = stats;
        weaponData = data;
        isSinglePlayer = (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening);

        if (visualTransform == null) 
        {
            // Tenta encontrar o primeiro filho se não arrastaste nada
            if (transform.childCount > 0) visualTransform = transform.GetChild(0);
            else visualTransform = transform; // Fallback (vai rodar o pai se não houver filho)
        }

        // --- AQUI ESTÁ A CORREÇÃO DO Z ---
        if (direction != Vector3.zero)
        {
            // 1. Calcula o ângulo 2D (X e Z do mundo -> X e Y do ângulo)
            float angle = Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg;
            
            // 2. Aplica APENAS no Z do Visual. X e Y ficam a 0 (ou 90 se ajustares no inspector).
            // Isto roda o sprite como um relógio.
            visualTransform.localRotation = Quaternion.Euler(0, 0, angle + visualRotationOffset);
        }

        // 2. Escala
        float sizeScale = weaponData.area * (stats != null ? stats.projectileSizeMultiplier : 1f);
        visualTransform.localScale = Vector3.one * sizeScale;

        // 3. Velocidade e Alcance
        float speedStats = (stats != null) ? Mathf.Max(0.1f, stats.attackSpeedMultiplier) : 1f;
        float duration = weaponData.duration / speedStats;
        float finalDist = stabDistance * sizeScale;

        StartCoroutine(StabRoutine(duration, finalDist));
        Destroy(gameObject, duration + 0.1f);
    }

    private IEnumerator StabRoutine(float duration, float distance)
    {
        float timer = 0f;
        Vector3 startLocalPos = Vector3.zero;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float progress = timer / duration;
            float curveVal = stabCurve.Evaluate(progress);

            // --- MOVIMENTO ---
            // Como rodámos o Z do Visual, a direção "Frente" do sprite é o eixo X local (Right).
            // Isto faz a arma ir para a frente na direção do ângulo.
            visualTransform.localPosition = startLocalPos + (Vector3.right * curveVal * distance);

            yield return null;
        }
    }

    // Nota: O Collider DEVE estar no objeto que tem este script (o Pai) 
    // ou usas um script extra no filho para passar a colisão. 
    // Se o Collider estiver no filho, o OnTriggerEnter aqui no pai NÃO funciona automaticamente.
    private void OnTriggerEnter(Collider other)
    {
        if (weaponData == null || ownerStats == null) return;
        if (hitEnemies.Contains(other.gameObject)) return;

        if (other.CompareTag("Enemy"))
        {
            var enemy = other.GetComponent<EnemyStats>();
            if (enemy != null)
            {
                hitEnemies.Add(other.gameObject);
                DamageResult dmg = ownerStats.CalculateDamage(weaponData.damage);

                if (isSinglePlayer || IsServer)
                {
                    enemy.TakeDamageFromAttacker(dmg.damage, dmg.isCritical, ownerStats);
                    
                    // Empurra na direção do visual (para onde a arma aponta)
                    float kb = weaponData.knockback * ownerStats.knockbackMultiplier;
                    if (kb > 0) enemy.ApplyKnockback(kb, 0.2f, visualTransform.right);
                }
            }
        }
    }
}