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

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void UpdateTimerText(float time)
    {
        int minutes = Mathf.FloorToInt(time / 60);
        int seconds = Mathf.FloorToInt(time % 60);
        timerText.text = $"{minutes:00}:{seconds:00}";
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
