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


    }
}