using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

// Attach this script to your "UnitSelectionManager" GameObject.
public class UnitCarouselSelector : MonoBehaviour
{
    [Header("List of Units")]
    [Tooltip("Add all your unit prefabs to this list.")]
    public List<GameObject> unitPrefabs;

    [Header("UI Element Links")]
    [Tooltip("The Image component that displays the unit's sprite.")]
    public Image unitDisplayImage;
    [Tooltip("The Text component that displays the unit's name.")]
    public TextMeshProUGUI unitNameText;
    [Tooltip("The button to cycle to the next unit.")]
    public Button nextButton;
    [Tooltip("The button to cycle to the previous unit.")]
    public Button previousButton;
    [Tooltip("The button that confirms the selection and starts the game.")]
    public Button selectButton;

    private int currentIndex = 0;
    private GameInitializer gameInitializer;

    void Start()
    {
        // Find the GameInitializer in the scene
        gameInitializer = FindFirstObjectByType<GameInitializer>();
        if (gameInitializer == null)
        {
            Debug.LogError("GameInitializer not found in the scene! The 'Select' button will not work.");
        }

        // --- Setup Button Listeners ---
        nextButton.onClick.AddListener(NextUnit);
        previousButton.onClick.AddListener(PreviousUnit);

        // --- Initial State ---
        // Check if we have any units to display
        if (unitPrefabs == null || unitPrefabs.Count == 0)
        {
            Debug.LogError("Unit Prefabs list is empty! The selector cannot function.");
            // Disable the UI to prevent errors
            gameObject.SetActive(false);
            return;
        }

        // Display the first unit in the list
        UpdateDisplay();
    }

    public void NextUnit()
    {
        Debug.Log("Next Unit button clicked.");
        currentIndex++;
        if (currentIndex >= unitPrefabs.Count)
        {
            currentIndex = 0;
        }
        UpdateDisplay();
    }

    public void PreviousUnit()
    {
        currentIndex--;
        // If we go before the beginning of the list, loop to the end
        if (currentIndex < 0)
        {
            currentIndex = unitPrefabs.Count - 1;
        }
        UpdateDisplay();
    }

    /// <summary>
    /// Updates the central image and text to match the currently selected unit.
    /// </summary>
    private void UpdateDisplay()
    {
        GameObject currentUnit = unitPrefabs[currentIndex];
        if (currentUnit != null)
        {
            // Update the image
            SpriteRenderer spriteRenderer = currentUnit.GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                unitDisplayImage.sprite = spriteRenderer.sprite;
            }
            else
            {
                Debug.LogWarning($"SpriteRenderer not found on {currentUnit.name}. Cannot update display image.");
                unitDisplayImage.sprite = null; // Clear the image if no sprite found
            }

            // Update the name text
            unitNameText.text = currentUnit.name;
        }
        else
        {
            Debug.LogWarning("Current unit prefab is null. Cannot update display.");
            unitDisplayImage.sprite = null;
            unitNameText.text = "Unknown Unit";
        }
    }

}