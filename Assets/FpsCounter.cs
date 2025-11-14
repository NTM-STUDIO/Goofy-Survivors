using UnityEngine;
using System.Collections;

/// <summary>
/// A simple FPS counter that displays the average frames per second.
/// </summary>
public class FPSCounter : MonoBehaviour
{
    [Header("Display Settings")]
    [Tooltip("The position and size of the FPS display on the screen.")]
    public Rect displayRect = new Rect(10, 10, 200, 40);

    [Tooltip("The font size for the FPS text.")]
    public int fontSize = 24;

    [Tooltip("The color of the FPS text.")]
    public Color textColor = Color.white;

    [Header("Calculation Settings")]
    [Tooltip("How often the FPS display should be updated (in seconds).")]
    public float updateInterval = 0.5f;

    // --- Private Variables ---
    private float accum = 0; // FPS accumulated over the interval
    private int frames = 0; // Frames drawn over the interval
    private float timeleft; // Left time for current interval
    private string fpsText; // The string to display
    private GUIStyle style = new GUIStyle();

    void Start()
    {
        // Initialize the time left to the first interval.
        timeleft = updateInterval;

        // Configure the GUIStyle for the text display.
        style.alignment = TextAnchor.LowerRight;
        style.fontSize = fontSize;
        style.normal.textColor = textColor;
    }

    void Update()
    {
        // Decrement the time remaining in the current interval.
        timeleft -= Time.unscaledDeltaTime;
        
        // Accumulate the time that has passed.
        accum += Time.timeScale / Time.unscaledDeltaTime;
        
        // Increment the frame count.
        ++frames;

        // When the interval ends, calculate and update the FPS string.
        if (timeleft <= 0.0)
        {
            // Calculate the average FPS.
            float fps = accum / frames;
            
            // Format the string with one decimal place.
            fpsText = System.String.Format("{0:F1} FPS", fps);

            // Reset for the next interval.
            timeleft = updateInterval;
            accum = 0.0F;
            frames = 0;
        }
    }

    /// <summary>
    /// OnGUI is called for rendering and handling GUI events.
    /// This is used here to draw the FPS counter on the screen.
    /// </summary>
    void OnGUI()
    {
        // Update style properties in case they were changed in the inspector at runtime.
        style.fontSize = fontSize;
        style.normal.textColor = textColor;

        // Draw the FPS text at the specified position.
        GUI.Label(displayRect, fpsText, style);
    }
}