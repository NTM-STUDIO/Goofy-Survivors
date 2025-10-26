using System.Collections.Generic;
using UnityEngine;

public class ShadowClone : MonoBehaviour
{
    [Header("Clone Stats")]
    [SerializeField] private float lifetime = 5f; // Duração de 5 segundos
    [SerializeField] private float health = 1f;   // Vida para ser 'one-shottable'

    [Header("Internal References")]
    [Tooltip("Um objeto filho vazio para organizar as armas do clone.")]
    [SerializeField] private Transform weaponContainer;

    void Start()
    {
        // Garante que o container de armas exista se não for atribuído
        if (weaponContainer == null)
        {
            weaponContainer = new GameObject("WeaponContainer").transform;
            weaponContainer.SetParent(this.transform);
            weaponContainer.localPosition = Vector3.zero;
        }

        // Inicia o contador para autodestruição
        Destroy(gameObject, lifetime);
    }

    /// <summary>
    /// Recebe a lista de armas do jogador e as recria como filhas do clone.
    /// </summary>
    public void Initialize(List<WeaponData> playerWeapons)
    {
        if (playerWeapons == null) return;

        foreach (var weaponData in playerWeapons)
        {
            // Cria um novo GameObject para cada arma
            GameObject weaponControllerObj = new GameObject(weaponData.weaponName + " (Clone)");
            weaponControllerObj.transform.SetParent(weaponContainer);

            // Adiciona o componente WeaponController e atribui os dados da arma
            WeaponController wc = weaponControllerObj.AddComponent<WeaponController>();
            wc.weaponData = weaponData;
        }
    }

    /// <summary>
    /// Método público para o clone receber dano.
    /// </summary>
    public void TakeDamage(float amount)
    {
        if (health <= 0) return; // Já está morrendo

        health -= amount;
        if (health <= 0)
        {
            // Opcional: Adicione um efeito de "puff" de fumaça aqui antes de destruir
            Destroy(gameObject);
        }
    }
}