using UnityEngine;

// This script ensures the Canvas on this GameObject is set to the correct sorting layer.
// It's a workaround for when the Sorting Layer option doesn't appear in the Inspector.
[RequireComponent(typeof(Canvas))]
public class CanvasSorter : MonoBehaviour
{
    [Tooltip("The name of the Sorting Layer to set for this canvas.")]
    [SerializeField]
    private string sortingLayerName = "DMG";

    [Tooltip("The order within that layer. Higher numbers are drawn on top.")]
    [SerializeField]
    private int orderInLayer = 1;

    void Awake()
    {
        // Get the Canvas component on this GameObject.
        Canvas canvas = GetComponent<Canvas>();

        // Set the sorting layer and order directly through code.
        canvas.sortingLayerName = sortingLayerName;
        canvas.sortingOrder = orderInLayer;
    }
}