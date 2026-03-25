using UnityEngine;
using UnityEngine.SceneManagement;

public class GameFlowController : MonoBehaviour
{
    [Header("UI Navigation")]
    [Tooltip("Referência ao painel anterior para o qual queremos voltar.")]
    public GameObject previousMenuPanel;
    [Tooltip("Referência ao painel atual que queremos fechar.")]
    public GameObject currentMenuPanel;

    /// <summary>
    /// Vai para o último menu aberto.
    /// </summary>
    public void GoBack()
    {
        // Se estiveres a usar navegação por GameObjects diretamente:
        if (currentMenuPanel != null) currentMenuPanel.SetActive(false);
        if (previousMenuPanel != null) previousMenuPanel.SetActive(true);
        
        // Se tiveres um sistema de navegação no UIManager, basta substituires por:
        // UIManager.Instance.GoToPreviousMenu();
    }

    /// <summary>
    /// Reinicia a Run atual (Soft Reset).
    /// </summary>
    public void RestartRun()
    {
        // Garante que o timescale não fica bloqueado a 0
        Time.timeScale = 1f;

        if (GameManager.Instance != null)
        {
            // Soft reset: limpa o mundo, inimigos, xp, e reinicia a lógica sem recarregar a scene do zero.
            // É exatamente como se tivesses acabado de dar "Play" no jogo.
            GameManager.Instance.ActionPlayAgain(); 
        }
        else
        {
            // Fallback de segurança apenas caso o GameManager não exista
            int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
            SceneManager.LoadScene(currentSceneIndex);
        }
    }

    /// <summary>
    /// Usado quando o jogo acaba. Vale o mesmo que o Restart.
    /// </summary>
    public void PlayAgain()
    {
        RestartRun();
    }

    /// <summary>
    /// Volta para a parte de UI e reseta os managers todos como se nunca tivesse acontecido nada.
    /// </summary>
    public void LeaveGame()
    {
        Time.timeScale = 1f;

        if (GameManager.Instance != null)
        {
            // O GameManager no teu projeto provavelmente tem uma função dedicada a sair para o lobby/main menu e deitar a baixo conexões se for o caso.
            // GameManager.Instance.ActionLeaveToLobby(); 
        }

        // Para reiniciar tudo do zero de forma garantida, carregar a primeira Scene ( Splash ou Main Menu ) resolve a questão.
        // Assim as variaveis resetam completamente como se o jogo tivesse acabado de ser aberto (desde que os managers não estejam a forçar DontDestroyOnLoad sem limpeza)
        SceneManager.LoadScene(0); 
    }
}