using UnityEngine;

public class GameInitializer : MonoBehaviour
{
    public GameObject gameManager; // Assign the GameManager object here
    public GameObject cinemachineCamera; // Or whatever Cinemachine component you use
    public GameObject unitSelectionUI; // Assign the parent UI panel/canvas here

    void Start()
    {

    }

    public void OnSelectUnit(GameObject unitPrefab)
    {
        if (unitPrefab != null)
        {
            Instantiate(unitPrefab, Vector3.zero, Quaternion.identity);
            StartGame(unitPrefab);
        }
        else
        {
            Debug.LogError("Unit prefab is not assigned in SelectUnitUI.");
        }
    }

    public void StartGame(GameObject selectedPrefab)
    {

        if (selectedPrefab != null)
        {
            Instantiate(selectedPrefab, Vector3.zero, Quaternion.identity);
        }
        else
        {
            Debug.LogError("Selected prefab is null in GameInitializer.");
            return; // Exit if no prefab to instantiate
        } 

        if (gameManager != null)
        {
            gameManager.SetActive(true); // Enable the GameManager
        }
        else
        {
            Debug.LogError("GameManager reference is not assigned in GameInitializer.");
        }

        if (cinemachineCamera != null)
        {
            cinemachineCamera.SetActive(true); // Activate the Cinemachine camera
        }
        else
        {
            Debug.LogError("Cinemachine camera reference is not assigned in GameInitializer.");
        }

        if (unitSelectionUI != null)
        {
            unitSelectionUI.SetActive(false); // Hide the unit selection UI
        }
        else
        {
            Debug.LogError("Unit Selection UI reference is not assigned in GameInitializer.");
        }
    }
}