using UnityEngine;

/// <summary>
/// Controls the properties of a Light component to create a pulsating spotlight effect.
/// This script must be attached to a GameObject that has a Light component set to 'Spot'.
/// It allows for independent pulsation of intensity, outer angle, and inner angle.
/// </summary>
[RequireComponent(typeof(Light))]
public class PulsatingSpotlight : MonoBehaviour
{
    [Header("Core Settings")]
    [Tooltip("How fast the light pulsates. Higher value means faster pulsation.")]
    public float pulseSpeed = 1.0f;

    [Header("Intensity Pulsation")]
    [Tooltip("Check this to enable intensity pulsation.")]
    public bool pulsateIntensity = true;
    [Range(200f, 2000f)]
    public float minIntensity = 1.0f;
    [Range(200f, 2000f)]
    public float maxIntensity = 5.0f;

    [Header("Outer Radius (Spot Angle)")]
    [Tooltip("Check this to enable the outer cone's angle to pulsate.")]
    public bool pulsateOuterAngle = false;
    [Range(1f, 179f)]
    public float minOuterAngle = 30.0f;
    [Range(1f, 179f)]
    public float maxOuterAngle = 60.0f;
    
    [Header("Inner Radius (Inner Spot Angle)")]
    [Tooltip("Check this to enable the inner bright cone's angle to pulsate.")]
    public bool pulsateInnerAngle = false;
    [Range(0f, 179f)]
    public float minInnerAngle = 10.0f;
    [Range(0f, 179f)]
    public float maxInnerAngle = 20.0f;

    private Light spotlight;

    void Awake()
    {
        // Get the Light component attached to this GameObject.
        spotlight = GetComponent<Light>();

        // Ensure the light type is set to Spot, otherwise the angle properties will do nothing.
        if (spotlight.type != LightType.Spot)
        {
            Debug.LogWarning("PulsatingSpotlight script is designed for a 'Spot' light, but the component is set to '" + spotlight.type + "'. Angle pulsation will not work.", this);
        }
    }

    void Update()
    {
        // We only need to calculate the sine wave once per frame.
        // It creates a smooth oscillation between 0 and 1.
        float remappedSine = (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f;

        // --- Update Intensity ---
        if (pulsateIntensity)
        {
            // Lerp (Linear Interpolation) finds a value between min and max based on the remapped sine wave.
            spotlight.intensity = Mathf.Lerp(minIntensity, maxIntensity, remappedSine);
        }

        // --- Update Outer Radius (Spot Angle) ---
        if (pulsateOuterAngle)
        {
            spotlight.spotAngle = Mathf.Lerp(minOuterAngle, maxOuterAngle, remappedSine);
        }
        
        // --- Update Inner Radius (Inner Spot Angle) ---
        if (pulsateInnerAngle)
        {
            spotlight.innerSpotAngle = Mathf.Lerp(minInnerAngle, maxInnerAngle, remappedSine);
        }
    }
}