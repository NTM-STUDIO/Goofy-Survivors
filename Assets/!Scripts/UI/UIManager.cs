using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class UIManager : MonoBehaviour
{
    public TextMeshProUGUI timerText;
    public GameObject pauseMenu;
    public GameObject usernamePanel;
    public TMP_InputField usernameInput;

    public Slider xpSlider;
    public TMP_Text levelText;
    public Slider healthBar;



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

    public void ShowPauseMenu(bool show)
    {
        if (pauseMenu != null)
            pauseMenu.SetActive(show);
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
