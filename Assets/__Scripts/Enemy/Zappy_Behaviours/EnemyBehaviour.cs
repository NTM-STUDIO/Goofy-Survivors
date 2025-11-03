using UnityEngine;

public abstract class EnemyBehaviour : MonoBehaviour
{
    protected EnemyStats stats;
    protected EnemyMovement movement;
    
    protected virtual void Awake()
    {
        stats = GetComponent<EnemyStats>();
        movement = GetComponent<EnemyMovement>();
    }
    
    public abstract void Execute();
}
