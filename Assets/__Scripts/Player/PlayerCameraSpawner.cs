using UnityEngine;
using TMPro.Examples;

public class PlayerCameraSpawner : MonoBehaviour
{
    public GameObject cameraPrefab;

    void Start()
    {
        Debug.Log($"[PlayerCameraSpawner] Start called on {gameObject.name}");
        var networkBehaviour = GetComponent<Unity.Netcode.NetworkBehaviour>();
        bool isOwner = networkBehaviour == null || networkBehaviour.IsOwner;

        if (isOwner)
        {
            if (cameraPrefab == null)
            {
                Debug.LogError("[PlayerCameraSpawner] cameraPrefab is null! Camera will not be instantiated.");
                return;
            }
            Debug.Log("[PlayerCameraSpawner] Instantiating camera prefab...");
            var camObj = Instantiate(cameraPrefab);
            var controller = camObj.GetComponent<CameraController>();
            if (controller != null)
            {
                Debug.Log("[PlayerCameraSpawner] CameraController found, setting CameraTarget.");
                controller.CameraTarget = this.transform;
            }
            else
            {
                Debug.LogWarning("[PlayerCameraSpawner] CameraController not found on camera prefab!");
            }
        }
        else
        {
            Debug.Log("[PlayerCameraSpawner] Not owner, camera will not be instantiated.");
        }
    }
}
