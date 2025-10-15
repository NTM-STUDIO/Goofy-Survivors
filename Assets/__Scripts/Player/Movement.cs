using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class Movement : MonoBehaviour
{
    private float speed;
    private Rigidbody2D rb;
    private Vector2 moveInput;
    private bool isKnockedBack = false;
    private Coroutine knockbackRoutine;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    // Update is called once per frame
    void Update()
    {
        speed = GetComponent<PlayerStats>().movementSpeed;
        if (!isKnockedBack)
        {

            float isometricYFactor = 1.644f; // Or your preferred 0.625f
            rb.linearVelocity = new Vector2(moveInput.x * speed * isometricYFactor, moveInput.y * speed);
        }
    }

    public void Move(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }
}
