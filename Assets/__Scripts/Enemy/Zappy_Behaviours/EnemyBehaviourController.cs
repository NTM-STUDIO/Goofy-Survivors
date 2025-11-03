using UnityEngine;

public class EnemyBehaviourController : MonoBehaviour
{
    [Header("Behaviour Settings")]
    [SerializeField] private bool executeInUpdate = true;
    [SerializeField] private bool executeInFixedUpdate = false;
    
    private EnemyBehaviour[] behaviours;
    private EnemyStats stats;
    
    private void Awake()
    {
        stats = GetComponent<EnemyStats>();
        behaviours = GetComponents<EnemyBehaviour>();
        
        if (behaviours.Length == 0)
        {
            Debug.LogWarning($"EnemyBehaviourController em {gameObject.name} não encontrou nenhum EnemyBehaviour!", this);
        }
    }
    
    private void Update()
    {
        if (!executeInUpdate) return;
        ExecuteBehaviours();
    }
    
    private void FixedUpdate()
    {
        if (!executeInFixedUpdate) return;
        ExecuteBehaviours();
    }
    
    private void ExecuteBehaviours()
    {
        // Não executar comportamentos se estiver em knockback
        if (stats != null && stats.IsKnockedBack) return;
        
        foreach (var behaviour in behaviours)
        {
            if (behaviour != null && behaviour.enabled)
            {
                behaviour.Execute();
            }
        }
    }
}
