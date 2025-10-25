using UnityEngine;
using System.Collections;

[RequireComponent(typeof(SphereCollider))]
public class Magnet : MonoBehaviour
{
    [Header("Magnet Settings")]
    public float boostRadius = 9999f;
    public float duration = 2f; // how long the boost lasts

    private void Awake()
    {
        SphereCollider sc = GetComponent<SphereCollider>();
        sc.isTrigger = true;
        gameObject.tag = "Items";
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Items")) return;

        var pickupController = other.GetComponent<PickupRadiusController>();
        if (pickupController == null)
        {
            Debug.LogWarning("Magnet: Player does not have a PickupRadiusController!");
            return;
        }

        StartCoroutine(ApplyMagnetEffect(pickupController));
    }

    private IEnumerator ApplyMagnetEffect(PickupRadiusController pickupController)
    {
        pickupController.AddRadiusModifier(boostRadius);
        Debug.Log($"🧲 Magnet activated: {boostRadius} radius for {duration} seconds.");

        // Hide the item immediately (collected)
        GetComponent<Collider>().enabled = false;
        if (TryGetComponent(out MeshRenderer mesh))
            mesh.enabled = false;

        yield return new WaitForSeconds(duration);

        pickupController.RemoveRadiusModifier(boostRadius);
        Debug.Log("🧲 Magnet effect ended. Radius restored.");

        Destroy(gameObject);
    }
}
