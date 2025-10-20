using UnityEngine;
using TMPro; // Required for TextMeshPro components
using System.Collections.Generic; // Required for Lists

public class SettingsManager : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The parent panel for all settings UI elements.")]
    public GameObject settingsPanel;
    [Tooltip("The dropdown for selecting the display monitor.")]
    public TMP_Dropdown monitorDropdown;
    [Tooltip("The dropdown for selecting the FPS limit.")]
    public TMP_Dropdown fpsDropdown;

    // --- Private State ---
    private bool isPanelOpen = false;
    // A list of common FPS options. 0 means "Unlimited" in the new system.
    private readonly List<int> fpsOptions = new List<int> { 60, 144, 240 };

    void Start()
    {
        // Ensure the panel is closed on start
        settingsPanel.SetActive(false);

        // Populate the UI with available system settings
        PopulateMonitorDropdown();
        PopulateFpsDropdown();

        // Add listeners. These will call our methods whenever a new value is selected.
        monitorDropdown.onValueChanged.AddListener(SetMonitor);
        fpsDropdown.onValueChanged.AddListener(SetFpsLimit);

        // Load any previously saved settings
        LoadSettings();
    }

    void Update()
    {
        // Listen for the Escape key to toggle the menu
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleSettingsPanel();
        }
    }

    // (Inside your SettingsManager class)

    public void ToggleSettingsPanel()
    {
        isPanelOpen = !isPanelOpen;
        settingsPanel.SetActive(isPanelOpen);

        // Use the new, robust pause request system
        if (isPanelOpen)
        {
            GameManager.Instance.RequestPause();
        }
        else
        {
            GameManager.Instance.RequestResume();
        }
    }

    #region Monitor Settings

    private void PopulateMonitorDropdown()
    {
        monitorDropdown.ClearOptions();
        List<string> options = new List<string>();

        // Unity's Display class gives us access to all connected monitors
        for (int i = 0; i < Display.displays.Length; i++)
        {
            options.Add("Display " + (i + 1));
        }

        monitorDropdown.AddOptions(options);
    }

    public void SetMonitor(int monitorIndex)
    {
        if (monitorIndex < 0 || monitorIndex >= Display.displays.Length)
        {
            Debug.LogWarning("Invalid monitor index selected.");
            return;
        }

        // IMPORTANT: This works best in a standalone build. Behavior in the Unity Editor can be unpredictable.
        Display.displays[monitorIndex].Activate();

        // Save the chosen monitor index
        PlayerPrefs.SetInt("MonitorIndex", monitorIndex);
        Debug.Log("Switched to Display " + (monitorIndex + 1));
    }

    #endregion

    #region FPS Settings

    private void PopulateFpsDropdown()
    {
        fpsDropdown.ClearOptions();
        List<string> options = new List<string>();
        foreach (int fps in fpsOptions)
        {
            if (fps == 0)
            {
                options.Add("Unlimited");
            }
            else
            {
                options.Add(fps.ToString());
            }
        }
        fpsDropdown.AddOptions(options);
    }

    public void SetFpsLimit(int fpsIndex)
    {
        int fpsLimit = fpsOptions[fpsIndex];

        // In modern Unity, a targetFrameRate of 0 or -1 means unlimited. We'll use 0.
        if (fpsLimit == 0)
        {
            Application.targetFrameRate = -1; // Or QualitySettings.vSyncCount = 0;
        }
        else
        {
            Application.targetFrameRate = fpsLimit;
        }

        // Save the setting so it persists
        PlayerPrefs.SetInt("FpsLimitIndex", fpsIndex);
    }

    #endregion

    // --- SAVING AND LOADING ---

    private void LoadSettings()
    {
        // Load and apply the saved monitor, defaulting to the primary display (index 0)
        int monitorIndex = PlayerPrefs.GetInt("MonitorIndex", 0);
        if (monitorIndex >= Display.displays.Length) monitorIndex = 0; // Sanity check
        monitorDropdown.value = monitorIndex;
        // NOTE: We don't call SetMonitor() here on startup to avoid unexpected screen changes.
        // The game will launch on the monitor the OS deems primary or where it was last placed.
        // The dropdown will correctly reflect the *saved preference* for the user to apply.

        // Load and apply the saved FPS limit, defaulting to Unlimited
        int fpsIndex = PlayerPrefs.GetInt("FpsLimitIndex", fpsOptions.Count - 1); // Default to last entry (Unlimited)
        fpsDropdown.value = fpsIndex;
        SetFpsLimit(fpsIndex);

        // Update the displayed text in the dropdowns
        monitorDropdown.RefreshShownValue();
        fpsDropdown.RefreshShownValue();
    }
}