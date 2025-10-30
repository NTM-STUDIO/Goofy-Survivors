using UnityEngine;

/// <summary>
/// Classe base abstrata para comportamentos de inimigos.
/// Cada comportamento específico herda desta classe.
/// </summary>
public abstract class EnemyBehaviour : MonoBehaviour
{
    protected EnemyStats stats;
    protected EnemyMovement movement;
    
    protected virtual void Awake()
    {
        stats = GetComponent<EnemyStats>();
        movement = GetComponent<EnemyMovement>();
    }
    
    /// <summary>
    /// Método que executa a lógica do comportamento.
    /// Deve ser chamado no Update ou FixedUpdate do controlador principal.
    /// </summary>
    public abstract void Execute();
}
