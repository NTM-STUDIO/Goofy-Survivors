using UnityEngine;

[RequireComponent(typeof(Light))]
public class PerpetualDayCycle : MonoBehaviour
{
    [Header("Cycle Timing")]
    [Tooltip("The duration of one half of the day (e.g., sunrise to sunset) in real-world seconds.")]
    public float dayDurationSeconds = 60f; // 1 minute from sunrise to sunset

    [Header("Sun Angles")]
    [Tooltip("The sun's rotation angle on the X-axis at sunrise.")]
    public float sunriseAngle = 10f;
    [Tooltip("The sun's rotation angle on the X-axis at sunset.")]
    public float sunsetAngle = 170f;

    [Header("Sun Settings")]
    [Tooltip("The peak intensity of the sun at midday.")]
    public float maxIntensity = 0.3f;

    [Tooltip("Controls brightness. X-axis is time (0=sunrise, 0.5=midday, 1=sunset). Y-axis is intensity (0 to 1).")]
    public AnimationCurve intensityCurve;

    [Tooltip("Controls color. The gradient represents the colors from sunrise (left) to sunset (right).")]
    public Gradient sunColorGradient;

    private Light sun;

    void Awake()
    {
        sun = GetComponent<Light>();
    }

    void Update()
    {
        // 1. Calculate the progress of the day (from 0 to 1 and back to 0)
        // Mathf.PingPong is a perfect function for a back-and-forth motion.
        // It creates a value that goes from 0 up to 1, and then back down to 0.
        float dayProgress = Mathf.PingPong(Time.time / dayDurationSeconds, 1.0f);

        // 2. Rotate the sun back and forth
        // We use Lerp to find the current angle between sunrise and sunset based on our day progress.
        float currentSunAngle = Mathf.Lerp(sunriseAngle, sunsetAngle, dayProgress);
        transform.rotation = Quaternion.Euler(currentSunAngle, -30f, 0f);

        // 3. Update the sun's intensity and color
        // The curve and gradient are now evaluated over the course of the day (0=sunrise, 1=sunset).
        float intensityMultiplier = intensityCurve.Evaluate(dayProgress);
        Color currentColor = sunColorGradient.Evaluate(dayProgress);

        sun.intensity = intensityMultiplier * maxIntensity;
        sun.color = currentColor;
    }
}