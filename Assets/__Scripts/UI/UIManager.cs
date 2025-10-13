using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class UIManager : MonoBehaviour
{
    public TextMeshProUGUI timerText;
    public GameObject pauseMenu;
    public GameObject usernamePanel;
    public GameObject statsPanel;
    public GameObject playAgainButton;
    public TMP_InputField usernameInput;

    public GameObject endGamePanel;
    public Slider xpSlider;
    public TMP_Text levelText;
    public Slider healthBar;

    public GameObject newWeaponPanel;
    public TextMeshProUGUI weaponNameText;
    public Image weaponSpriteImage;

    public void NewWeaponUi(WeaponData weaponData)
    {
        // Activate the panel
        newWeaponPanel.SetActive(true);

        // Set the weapon name and sprite
        weaponNameText.text = weaponData.weaponName;
        weaponSpriteImage.sprite = weaponData.icon;
    }

    public void CloseNewWeaponPanel()
    {
        newWeaponPanel.SetActive(false);
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
        GameManager.Instance.RestartGame();
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

    public void ShowUsernameInput()
    {
        if (usernamePanel != null)
            usernamePanel.SetActive(true);
    }

    public void SubmitUsername()
    {
        if (!string.IsNullOrEmpty(usernameInput.text))
        {
            GameManager.Instance.SubmitUsername(usernameInput.text);
            usernamePanel.SetActive(false);
        }
    }
}
