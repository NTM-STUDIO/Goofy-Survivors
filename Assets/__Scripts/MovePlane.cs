using UnityEngine;

public class SmoothDiagonalMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("The total distance the object will travel back and forth on the X-axis.")]
    public float totalDistanceX = 5f;

    [Tooltip("The total distance the object will travel back and forth on the Z-axis.")]
    public float totalDistanceZ = 5f;

    [Tooltip("How fast the object moves along its path. Smaller values are slower.")]
    public float speed = 0.5f;

    // This will store the object's initial position when the game starts.
    private Vector3 startPosition;

    void Start()
    {
        // Record the starting position to prevent drifting.
        startPosition = transform.position;
    }

    void Update()
    {
        // --- The Fix: Use a SINGLE sine wave for both axes ---
        // This single value, which oscillates between -1 and 1, will now drive
        // the movement for both X and Z simultaneously.
        float sineWave = Mathf.Sin(Time.time * speed);

        // --- Calculate X position ---
        float amplitudeX = totalDistanceX / 2f;
        float newX = startPosition.x + (sineWave * amplitudeX);

        // --- Calculate Z position ---
        // We use the SAME sineWave value here. This is the key.
        float amplitudeZ = totalDistanceZ / 2f;
        float newZ = startPosition.z + (sineWave * amplitudeZ);

        // Apply the new position. Because newX and newZ are driven by the same
        // timer, the object will move in a perfect straight line.
        transform.position = new Vector3(newX, startPosition.y, newZ);
    }
}