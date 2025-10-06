// using UnityEngine;

// [RequireComponent(typeof(Collider2D))]
// public class EnemyProjectileDamage : MonoBehaviour
// {
//     [Header("Projectile Damage")]
//     public float damage = 10f;
//     public float knockback = 5f;
//     public bool destroyOnHit = true;

//     private void Reset()
//     {
//         // Ensure triggers for projectile hits
//         var col = GetComponent<Collider2D>();
//         if (col != null) col.isTrigger = true;
//         gameObject.tag = "EnemyProjectile";
//     }
// }
