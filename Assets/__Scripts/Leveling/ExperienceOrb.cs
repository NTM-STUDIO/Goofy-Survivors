using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class ExperienceOrb : MonoBehaviour
{
    [Header("Orb Properties")]
    public int xpValue = 10;

    [Header("Fluid Movement")]
    public float smoothTime = 0.1f;
    public float collectionDistance = 2f;

    public float maxSpeed = 50f;
    private Transform attractionTarget;
    private bool isAttracted = false;
    private Vector3 currentVelocity = Vector3.zero;


    void Update()
    {

        if (!isAttracted || attractionTarget == null) return;
        if (Vector2.Distance(transform.position, attractionTarget.position) < collectionDistance)
        {
            CollectOrb();
            return;
        }

        transform.position = Vector3.SmoothDamp(
            transform.position,
            attractionTarget.position,
            ref currentVelocity,
            smoothTime,
            maxSpeed
        );
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isAttracted && other.CompareTag("Items"))
        {
            isAttracted = true;
            attractionTarget = other.transform;
        }
    }

    private void CollectOrb()
    {
        PlayerExperience playerExperience = attractionTarget.GetComponentInParent<PlayerExperience>();
        PlayerStats playerStats = attractionTarget.GetComponentInParent<PlayerStats>();

        if (playerExperience != null && playerStats != null)
        {
            float finalXp = xpValue * playerStats.xpGainMultiplier;
            playerExperience.AddXP(finalXp);
        }

        Destroy(gameObject);
    }
}

//Lembrete: Estou a usar a sombra como o Range de colecção.