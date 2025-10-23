using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("Core UI Panels")]
    [SerializeField] private GameObject pauseMenu;
    [SerializeField] private GameObject statsPanel;
    [SerializeField] private GameObject endGamePanel;
    [SerializeField] private GameObject painelPrincipal; // This is your Main Menu Panel
    [SerializeField] private GameObject multiplayerPanel;
    [SerializeField] private GameObject unitSelectionUI; // This is your Character Select Panel

    [Header("In-Game HUD")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private Slider xpSlider;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private Slider healthBar;
    
    [Header("New Weapon Panel")]
    [SerializeField] private GameObject newWeaponPanel;
    [SerializeField] private TextMeshProUGUI weaponNameText;
    [SerializeField] private Image weaponSpriteImage;
    
    [Header("System References")]
    [SerializeField] private AdvancedCameraController advancedCameraController;
    [SerializeField] private GameObject Managers;

    private GameManager gameManager;

    void Awake()
    {
        gameManager = GameManager.Instance; 
        if (gameManager == null)
        {
            Debug.LogError("FATAL ERROR: UIManager woke up but could not find the GameManager.Instance!");
        }
    }

    /// <summary>
    /// This method is called by the MAIN MENU Play button.
    /// Its only job is to hide the main menu and show the character selection screen.
    /// </summary>
    public void PlayButton()
    {
        if (painelPrincipal != null) painelPrincipal.SetActive(false);
        if (unitSelectionUI != null) unitSelectionUI.SetActive(true);
        
        // It no longer tries to start the game or activate the HUD.
    }

    /// <summary>
    /// This method will be called by the UnitCarouselSelector AFTER the game has started,
    /// to activate all the necessary in-game UI and components.
    /// </summary>
    public void OnGameStart()
    {
        // Now we activate the HUD and other game systems
        SetInGameHudVisibility(true);
        if(advancedCameraController != null) advancedCameraController.enabled = true;
        if(Managers != null) Managers.SetActive(true);
    }

    public void SetInGameHudVisibility(bool isVisible)
    {
        if (xpSlider != null) xpSlider.gameObject.SetActive(isVisible);
        if (timerText != null) timerText.gameObject.SetActive(isVisible);
        if (healthBar != null) healthBar.gameObject.SetActive(isVisible);
        if (levelText != null) levelText.gameObject.SetActive(isVisible);
    }

    public void OpenNewWeaponPanel(WeaponData weaponData)
    {
        newWeaponPanel.SetActive(true);
        weaponNameText.text = weaponData.weaponName;
        weaponSpriteImage.sprite = weaponData.icon;
        if (gameManager != null) gameManager.RequestPause();
    }

    public void CloseNewWeaponPanel()
    {
        newWeaponPanel.SetActive(false);
        if (gameManager != null) gameManager.RequestResume();
    }

    public void UpdateTimerText(float time)
    {
        int minutes = Mathf.FloorToInt(time / 60);
        int seconds = Mathf.FloorToInt(time % 60);
        timerText.text = $"{minutes:00}:{seconds:00}";
    }

    public void UpdateHealthBar(float currentHealth, float maxHealth)
    {
        if (healthBar != null)
        {
            healthBar.maxValue = maxHealth;
            healthBar.value = currentHealth;
        }
    }

    public void ShowEndGamePanel(bool show)
    {
        if (endGamePanel != null) endGamePanel.SetActive(show);
    }

    public void PlayAgainButton()
    {
        if (gameManager != null) gameManager.RestartGame();
    }



    public void ShowPauseMenu(bool show)
    {
        if (pauseMenu != null) pauseMenu.SetActive(show);
    }

    public void ToggleStatsPanel()
    {
        if (statsPanel != null)
        {
            statsPanel.SetActive(!statsPanel.activeSelf);
        }
    }

    public void ShowMultiplayerPanel(bool show)
    {
        if (multiplayerPanel != null) multiplayerPanel.SetActive(show);
    }

    public void ShowUnitSelectionUI(bool show)
    {
        if (unitSelectionUI != null)
        {
            unitSelectionUI.SetActive(!show);
            painelPrincipal.SetActive(show);
        }
    }
}