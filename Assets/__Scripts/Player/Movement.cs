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
            speed = GetComponent<PlayerStats>().movementSpeed;
            if (!isKnockedBack)
            {
                Vector2 clampedInput = moveInput.sqrMagnitude > 1f ? moveInput.normalized : moveInput;
                rb.linearVelocity = clampedInput * speed;
            }
        }
    }

    public void Move(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }
}
