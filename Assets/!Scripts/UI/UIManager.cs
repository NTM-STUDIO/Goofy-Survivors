using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    public TextMeshProUGUI timerText;
    public GameObject pauseMenu;
    public GameObject usernamePanel;
    public TMP_InputField usernameInput;

    public Slider healthBar;
    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        //find slider by player tag
        if (healthBar == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                UIManager uiManager = playerObject.GetComponent<UIManager>();
                if (uiManager != null)
                {
                    healthBar = uiManager.healthBar;
                }
                else
                {
                    Debug.LogError("UIManager Error: UIManager component not found on the player object!");
                }
            }
            else
            {
                Debug.LogError("UIManager Error: Player with tag 'Player' not found! Make sure your player is tagged correctly.");
            }
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
