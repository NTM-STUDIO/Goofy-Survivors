using UnityEngine;
using TMPro; // Make sure to import TextMeshPro

public class DamagePopup : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float fadeOutSpeed = 3f;
    [SerializeField] private float lifetime = 0.75f;

    [Header("Color Settings")]
    [SerializeField] private Color normalHitColor = Color.white;
    // --- NEW: Add a field for the critical hit color in the Inspector ---
    [SerializeField] private Color criticalHitColor = Color.yellow;

    private TMP_Text textMesh;
    private Color textColor;
    private Transform mainCameraTransform;

    private void Awake()
    {
        textMesh = GetComponent<TMP_Text>();
        // It's safer to cache the camera transform in case the main camera changes, but this is fine.
        if (Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }
        else
        {
            Debug.LogError("DamagePopup: Main Camera not found! The popup will not face the camera.", this);
            enabled = false; // Disable the script if there's no camera to face.
        }
    }

    /// <summary>
    /// This is called by the script that creates the popup to set the damage value and crit status.
    /// </summary>
    // --- METHOD MODIFIED: Now accepts a boolean for isCritical ---
    public void Setup(int damageAmount, bool isCritical)
    {
        textMesh.text = damageAmount.ToString();

        if (isCritical)
        {
            // --- CRITICAL HIT VISUALS ---
            textColor = criticalHitColor;
            textMesh.fontSize *= 1.25f; // Make the text 25% bigger
            textMesh.fontStyle = FontStyles.Bold; // Make the text bold
        }
        else
        {
            // --- NORMAL HIT VISUALS ---
            textColor = normalHitColor;
            // No style changes needed for normal hits.
        }

        textMesh.color = textColor;
    }

    private void Update()
    {
        if (mainCameraTransform == null) return;

        // --- Movement ---
        // Move the text upwards
        transform.position += Vector3.up * moveSpeed * Time.deltaTime;

        // --- Lifetime & Fading ---
        lifetime -= Time.deltaTime;
        if (lifetime < 0)
        {
            // Start fading out by reducing the alpha channel of the color
            textColor.a -= fadeOutSpeed * Time.deltaTime;
            textMesh.color = textColor;
            if (textColor.a <= 0)
            {
                // Destroy the object once it's fully faded
                Destroy(gameObject);
            }
        }
        
        // --- Billboarding ---
        // Make the text always face the camera by matching its rotation
        transform.rotation = mainCameraTransform.rotation;
    }
}