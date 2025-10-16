using UnityEngine;

// This ensures the script can only be attached to a GameObject with a Light component.
[RequireComponent(typeof(Light))]
public class DayNightCycle : MonoBehaviour
{
    [Header("Cycle Timing")]
    [Tooltip("The duration of a full day-night cycle in real-world seconds.")]
    public float cycleDurationSeconds = 120f; // 2 minutes for a full day

    [Header("Sun Settings")]
    [Tooltip("The peak intensity of the sun at midday. You requested a default of 0.3.")]
    public float maxIntensity = 0.3f;

    [Tooltip("An animation curve to control the sun's brightness over the day. X-axis is time (0=midnight, 0.5=midday), Y-axis is intensity multiplier (0 to 1).")]
    public AnimationCurve intensityCurve;

    [Tooltip("A gradient to control the sun's color over the day. Left side is midnight, middle is midday.")]
    public Gradient sunColorGradient;

    // Internal variable to track the current time of day (0.0 to 1.0)
    private float timeOfDay;
    private Light sun;

    void Awake()
    {
        // Get the Light component attached to this GameObject.
        sun = GetComponent<Light>();
    }

    void Update()
    {
        // 1. Advance the time of day
        // We divide by the duration to make the time advance correctly over the specified period.
        timeOfDay += Time.deltaTime / cycleDurationSeconds;

        // Loop the time back to 0 after it reaches 1 (end of the day)
        if (timeOfDay >= 1f)
        {
            timeOfDay -= 1f;
        }

        // 2. Rotate the sun
        // We multiply by 360 to convert our 0-1 time value into a full 360-degree rotation.
        // This rotates the light around the world's X-axis, simulating rising and setting.
        transform.rotation = Quaternion.Euler(timeOfDay * 360f, -30f, 0f);

        // 3. Update the sun's intensity and color
        // We use the AnimationCurve and Gradient to get the correct values for the current time.
        float intensityMultiplier = intensityCurve.Evaluate(timeOfDay);
        Color currentColor = sunColorGradient.Evaluate(timeOfDay);

        sun.intensity = intensityMultiplier * maxIntensity;
        sun.color = currentColor;
    }
}