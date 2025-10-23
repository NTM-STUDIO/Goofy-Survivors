using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI timerText;
    public GameObject pauseMenu;
    public GameObject statsPanel;
    public GameObject playAgainButton;
    public GameObject endGamePanel;
    public Slider xpSlider;
    public TMP_Text levelText;
    public Slider healthBar;
    public GameObject painelPrincipal;
    public GameObject multiplayerPanel;
    public GameObject unitSelectionUI;
    public GameObject Managers;

    [Header("New Weapon Panel")]
    public GameObject newWeaponPanel;
    public TextMeshProUGUI weaponNameText;
    public Image weaponSpriteImage;
    
    [Header("Component References")]
    public AdvancedCameraController advancedCameraController;

    // Cached reference to the GameManager
    private GameManager gameManager;

    // It's good practice to get references in Awake()
    void Awake()
    {
        // Cache the GameManager instance to avoid repeated, slow calls to FindObjectOfType
        gameManager = GameManager.Instance; 
        if (gameManager == null)
        {
            // Fallback if the singleton instance isn't set yet
            gameManager = FindObjectOfType<GameManager>();
        }
    }

    public void OpenNewWeaponPanel(WeaponData weaponData)
    {
        // Activate the panel
        newWeaponPanel.SetActive(true);

        // Set the weapon name and sprite
        weaponNameText.text = weaponData.weaponName;
        weaponSpriteImage.sprite = weaponData.icon;

        // Use the cached reference to request a pause
        if (gameManager != null)
        {
            gameManager.RequestPause();
        }
    }

    public void CloseNewWeaponPanel()
    {
        newWeaponPanel.SetActive(false);
        
        // Use the cached reference to request a resume
        if (gameManager != null)
        {
            gameManager.RequestResume(); // CORRECTED METHOD
        }
    }

    public void UpdateTimerText(float time)
    {
        int minutes = Mathf.FloorToInt(time / 60);
        int seconds = Mathf.FloorToInt(time % 60);
        timerText.text = $"{minutes:00}:{seconds:00}";
    }

    public void UpdateHealthBar(float currentHealth, float maxHealth)
    {
        if (healthBar == null)
        {
            // Using GameObject.Find is also slow; consider assigning this in the inspector
            healthBar = GameObject.Find("HP BAR").GetComponent<Slider>();
        }
        if (healthBar != null)
        {
            healthBar.maxValue = maxHealth;
            healthBar.value = currentHealth;
        }
    }

    public void ShowEndGamePanel(bool show)
    {
        if (endGamePanel != null)
            endGamePanel.SetActive(show);
    }

    public void PlayAgainButton()
    {
        // Use the cached reference
        if (gameManager != null)
        {
            gameManager.RestartGame();
        }
    }
    public void ShowPauseMenu(bool show)
    {
        if (pauseMenu != null)
            pauseMenu.SetActive(show);
    }

    public void ShowStatsPanel(bool show)
    {
        if (statsPanel != null)
        {
            statsPanel.SetActive(show);
        }
    }

    public void ShowMultiplayerPanel(bool show)
    {
        if (multiplayerPanel != null)
        {
            multiplayerPanel.SetActive(show);
        }
    }

    public void ShowUnitSelectionUI(bool show)
    {
        Debug.Log("Toggling Unit Selection UI: " + show);
        if (unitSelectionUI != null)
        {
            unitSelectionUI.SetActive(!show);
            painelPrincipal.SetActive(show);
        }
    }

    public void PlayButton()
    {
        painelPrincipal.SetActive(false);
        unitSelectionUI.SetActive(false);
        timerText.gameObject.SetActive(true);
        xpSlider.gameObject.SetActive(true);
        levelText.gameObject.SetActive(true);
        healthBar.gameObject.SetActive(true);
        advancedCameraController.enabled = true;
        Managers.SetActive(true);
        
        // Use the cached reference
        if (gameManager != null)
        {
            gameManager.StartGame();
        }
    }
}