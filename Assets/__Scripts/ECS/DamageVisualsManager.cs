using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;

public class DamageVisualsManager : MonoBehaviour
{
    public GameObject DamagePopupPrefab;
    public static DamageVisualsManager Instance;

    void Awake()
    {
        Instance = this;
    }

    public void SpawnPopup(float3 position, int amount, bool isCritical)
    {
        if (DamagePopupPrefab == null) return;

        // Instancia o Popup
        GameObject popup = Instantiate(DamagePopupPrefab, position, Quaternion.identity);
        
        // Configura (assumindo que o prefab tem o script DamagePopup)
        DamagePopup script = popup.GetComponent<DamagePopup>();
        if (script != null)
        {
            script.Setup(amount, isCritical);
        }
    }
}
