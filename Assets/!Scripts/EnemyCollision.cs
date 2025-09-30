using UnityEngine;
using System.Collections;
public class EnemyCollision : MonoBehaviour
{
    public float knockbackForce = 10f;   // Intensity of the knockback
    private Rigidbody2D rb;
    private bool hasBeenKnockedBack = false;  // To check if knockback has been applied
    private bool isKnockbacking = false;  // To check if the enemy is in knockback state
    public float knockbackDuration = 0.2f; // Duration of knockback before reactivating movement

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player") && !hasBeenKnockedBack)
        {
            Debug.Log("Inimigo tocou o jogador!");

            // Prevent movement during knockback
            DisableMovement();

            // Direção do knockback (oposta ao movimento do inimigo)
            Vector2 knockbackDirection = (transform.position - collision.transform.position).normalized;

            // Zera a velocidade para garantir que não haverá deslizamento
            rb.linearVelocity = Vector2.zero;

            // Aplica o knockback
            rb.AddForce(knockbackDirection * knockbackForce, ForceMode2D.Impulse);

            // Marca que o inimigo foi knockbacked
            hasBeenKnockedBack = true;

            // Start Coroutine to reset movement and knockback after a delay
            StartCoroutine(ResetAfterKnockback());
        }
    }

    private void DisableMovement()
    {
        isKnockbacking = true;  // Mark that the enemy is in knockback state
        rb.linearVelocity = Vector2.zero;  // Zero out any movement velocity
        rb.isKinematic = true;  // Temporarily disable physics calculations for movement
    }

    private void EnableMovement()
    {
        rb.isKinematic = false; // Re-enable physics for movement
        isKnockbacking = false;  // Reset knockback state
    }

    private IEnumerator ResetAfterKnockback()
    {
        // Wait for the knockback duration
        yield return new WaitForSeconds(knockbackDuration);

        // Re-enable movement and reset knockback state
        EnableMovement();

        // Reset knockback flag
        hasBeenKnockedBack = false;
    }
}
