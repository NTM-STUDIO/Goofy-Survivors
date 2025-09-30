using UnityEngine;
using System.Collections;

public class PlayerCollision : MonoBehaviour
{
    private bool isInGracePeriod = false;
    private float gracePeriodDuration = 2f;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Enemy") && !isInGracePeriod)
        {
            StartCoroutine(GracePeriod());

            EnemyMovement enemyMovement = collision.gameObject.GetComponent<EnemyMovement>();
            if (enemyMovement != null)
            {
                Vector2 knockbackDirection = (collision.transform.position - transform.position).normalized;
                enemyMovement.ApplyKnockback(knockbackDirection);
            }
        }
    }

    private IEnumerator GracePeriod()
    {
        isInGracePeriod = true;
        yield return new WaitForSeconds(gracePeriodDuration);
        isInGracePeriod = false;
    }
}