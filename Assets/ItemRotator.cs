using UnityEngine;

/// <summary>
/// Faz o objeto em que está anexado girar continuamente.
/// Ideal para itens flutuantes, poções, moedas e outros coletáveis.
/// </summary>
public class ItemRotator : MonoBehaviour
{
    [Header("Configuração da Rotação")]

    [Tooltip("A velocidade da rotação em graus por segundo.")]
    [SerializeField] private float rotationSpeed = 50f;

    [Tooltip("O eixo em torno do qual o objeto irá girar. (0, 1, 0) para girar como um pião.")]
    [SerializeField] private Vector3 rotationAxis = Vector3.up;
    
    // O método Update é chamado uma vez por frame. É o lugar perfeito para ações contínuas.
    void Update()
    {
        // A função transform.Rotate aplica uma rotação ao objeto.
        // Multiplicamos a velocidade por Time.deltaTime para garantir que a rotação
        // seja suave e independente da taxa de frames (frame rate) do jogo.
        // Se o jogo rodar mais devagar ou mais rápido, a velocidade de rotação visual será a mesma.
        transform.Rotate(rotationAxis, rotationSpeed * Time.deltaTime);
    }
}