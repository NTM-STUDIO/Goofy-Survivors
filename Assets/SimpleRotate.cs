using UnityEngine;

public class SimpleRotate : MonoBehaviour
{
    [Tooltip("The speed of rotation in degrees per second.")]
    [SerializeField]
    private float rotationSpeed = 360f;

    [Tooltip("The axis around which the object will rotate.")]
    [SerializeField]
    private Vector3 rotationAxis = Vector3.forward; // Vector3.forward is the Z-axis

    void Update()
    {
        // Rotate the object around the specified axis at the desired speed.
        // Time.deltaTime ensures the rotation is smooth and independent of the frame rate.
        transform.Rotate(rotationAxis, rotationSpeed * Time.deltaTime);
    }
}