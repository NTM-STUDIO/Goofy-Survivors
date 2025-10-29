using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

public class UIManager : MonoBehaviour
{
    [Header("Core UI Panels")]
    [Tooltip("O painel do menu principal com os botões 'Jogar', 'Multiplayer', etc.")]
    [SerializeField] private GameObject painelPrincipal;
    [Tooltip("O painel para escolher o modo P2P (Host/Client)")]
    [SerializeField] private GameObject multiplayerPanel;
    [Tooltip("O painel de seleção de unidades para o modo Single Player")]
    [SerializeField] private GameObject unitSelectionUI;
    [Tooltip("O menu de pausa que aparece ao pressionar ESC")]
    [SerializeField] private GameObject pauseMenu;
    [Tooltip("O painel de estatísticas que aparece ao pressionar TAB")]
    [SerializeField] private GameObject statsPanel;
    [Tooltip("O painel de fim de jogo (Vitória/Derrota)")]
    [SerializeField] private GameObject endGamePanel;

    [Header("In-Game HUD Elements")]
    [SerializeField] private GameObject inGameHudContainer; // Um objeto pai para todo o HUD
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private Slider xpSlider;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private Slider healthBar;
    
    [Header("New Weapon Panel")]
    [SerializeField] private GameObject newWeaponPanel;
    [SerializeField] private TextMeshProUGUI weaponNameText;
    [SerializeField] private Image weaponSpriteImage;
    
    [Header("System References")]
    [Tooltip("Referência opcional para a câmara do jogador")]
    [SerializeField] private AdvancedCameraController advancedCameraController;

    // Referência para o cérebro do jogo
    private GameManager gameManager;

    void Start()
    {
        // É crucial obter a instância Singleton no Start para garantir que o Awake do GameManager já correu.
        gameManager = GameManager.Instance; 
        if (gameManager == null)
        {
            Debug.LogError("FATAL ERROR: UIManager não encontrou o GameManager.Instance!");
        }

        // Estado inicial da UI no arranque do jogo
        painelPrincipal.SetActive(true);
        multiplayerPanel.SetActive(false);
        unitSelectionUI.SetActive(false);
        inGameHudContainer.SetActive(false);
        endGamePanel.SetActive(false);
        pauseMenu.SetActive(false);
    }

    // ===============================================================
    // FLUXO DO MENU PRINCIPAL
    // ===============================================================

    /// <summary>
    /// Chamado pelo botão 'Jogar' para o modo Single Player.
    /// </summary>
    public void PlayButton()
    {
        // Garante que a flag P2P está desligada
        if(gameManager != null) gameManager.isP2P = false;

        painelPrincipal.SetActive(false);
        unitSelectionUI.SetActive(true);
    }

    /// <summary>
    /// Chamado pelo botão 'Multiplayer'. Prepara o jogo para o modo P2P.
    /// </summary>
    public void MultiplayerButton()
    {
        // 1. INFORMA o GameManager da decisão do jogador. Este é o passo mais importante.
        if (gameManager != null)
        {
            gameManager.isP2P = true;
        }
        else
        {
            Debug.LogError("Não foi possível definir o modo P2P porque a referência do GameManager é nula!");
            return;
        }

        // 2. ATUALIZA a UI para mostrar as opções de Host/Client.
        painelPrincipal.SetActive(false);
        multiplayerPanel.SetActive(true);
    }

    /// <summary>
    /// Chamado por um botão 'Voltar' no painel multiplayer para regressar ao menu principal.
    /// </summary>
    public void BackToMainMenu()
    {
        // Desfaz a decisão de jogar em P2P
        if(gameManager != null) gameManager.isP2P = false;

        multiplayerPanel.SetActive(false);
        painelPrincipal.SetActive(true);
    }

    /// <summary>
    /// Fecha a aplicação.
    /// </summary>
    public void QuitButton()
    {
        Application.Quit();
    }


    // ===============================================================
    // FLUXO DE CONEXÃO P2P
    // ===============================================================

    /// <summary>
    /// Chamado pelo botão 'Host'. Inicia a sessão de rede como Host.
    /// </summary>
    public void StartHost()
    {
        Debug.Log("[UIManager] A iniciar como Host...");
        NetworkManager.Singleton.StartHost();
        // A UI é escondida. O LobbyManagerP2P assumirá o controlo a partir daqui.
        multiplayerPanel.SetActive(false);
    }

    /// <summary>
    /// Chamado pelo botão 'Client'. Tenta conectar-se a um Host.
    /// </summary>
    public void StartClient()
    {
        Debug.Log("[UIManager] A iniciar como Cliente...");
        NetworkManager.Singleton.StartClient();
        // A UI é escondida. O LobbyManagerP2P assumirá o controlo a partir daqui.
        multiplayerPanel.SetActive(false);
    }
    
    // ===============================================================
    // UI DURANTE O JOGO (Chamada por outros sistemas)
    // ===============================================================

    /// <summary>
    /// Ativa o HUD do jogo. Esta função deve ser chamada pelo GameManager quando o jogo realmente começa.
    /// </summary>
    public void OnGameStart()
    {
        // Esconde todos os menus pré-jogo
        painelPrincipal.SetActive(false);
        multiplayerPanel.SetActive(false);
        unitSelectionUI.SetActive(false);
        
        // Ativa o HUD
        inGameHudContainer.SetActive(true);

        if (advancedCameraController != null) advancedCameraController.enabled = true;
    }

    public void OpenNewWeaponPanel(WeaponData weaponData)
    {
        newWeaponPanel.SetActive(true);
        weaponNameText.text = weaponData.weaponName;
        weaponSpriteImage.sprite = weaponData.icon;
        
        // Em P2P, a pausa é desativada, mas podemos querer parar o input do jogador.
        // O GameManager deve ter a lógica para lidar com isto.
        gameManager?.RequestPause(); 
    }

    public void CloseNewWeaponPanel()
    {
        newWeaponPanel.SetActive(false);
        gameManager?.RequestResume();
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
        endGamePanel?.SetActive(show);
        if (show) inGameHudContainer.SetActive(false);
    }

    public void PlayAgainButton()
    {
        // O GameManager já tem a lógica para decidir o que fazer com base na flag isP2P.
        gameManager?.RestartGame();
    }


    public void ShowPauseMenu(bool show)
    {
        // A lógica de pausa já está no GameManager para verificar se está em modo P2P ou não.
        pauseMenu?.SetActive(show);
    }

    public void ToggleStatsPanel()
    {
        if (statsPanel != null)
            statsPanel.SetActive(!statsPanel.activeSelf);
    }
}