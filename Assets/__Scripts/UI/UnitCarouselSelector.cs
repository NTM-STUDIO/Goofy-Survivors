using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

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
    private GameManager gameManager;
    private UIManager uiManager;

    void Start()
    {
        // Get reliable singleton instances
        gameManager = GameManager.Instance;
        if (gameManager == null)
        {
            Debug.LogError("GameManager.Instance not found! The 'Select' button will not work.");
        }

        uiManager = FindFirstObjectByType<UIManager>(); // This is acceptable for a one-time find of another UI manager
        if (uiManager == null)
        {
            Debug.LogError("UIManager not found in the scene! The carousel cannot activate the HUD.");
        }

        // Setup Button Listeners
        nextButton.onClick.AddListener(NextUnit);
        previousButton.onClick.AddListener(PreviousUnit);
        selectButton.onClick.AddListener(SelectAndStartGame);

        if (unitPrefabs == null || unitPrefabs.Count == 0)
        {
            Debug.LogError("Unit Prefabs list is empty! The selector cannot function.");
            gameObject.SetActive(false);
            return;
        }

        UpdateDisplay();
    }

    /// <summary>
    /// This is the definitive "start game" button action for the player.
    /// </summary>
    public void SelectAndStartGame()
    {
        if (gameManager == null)
        {
            Debug.LogError("Cannot start game, GameManager reference is missing!");
            return;
        }

        // 1. Get the currently selected prefab
        GameObject selectedPrefab = unitPrefabs[currentIndex];

        // 2. Send the prefab to the GameManager
        gameManager.SetChosenPlayerPrefab(selectedPrefab);

        // 3. Tell the GameManager to start the core game logic
        gameManager.StartGame();

        // 4. Tell the UIManager to activate the in-game HUD
        if (uiManager != null)
        {
            uiManager.OnGameStart();
        }

        // 5. Hide the unit selection UI itself
        gameObject.SetActive(false); 
    }

    public void NextUnit()
    {
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
        if (currentIndex < 0)
        {
            currentIndex = unitPrefabs.Count - 1;
        }
        UpdateDisplay();
    }
    
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
                unitDisplayImage.sprite = null;
            }

            // Update the name text
            unitNameText.text = currentUnit.name;
        }
    }
}