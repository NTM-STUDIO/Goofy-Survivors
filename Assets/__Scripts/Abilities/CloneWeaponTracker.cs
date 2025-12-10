using UnityEngine;

public class CloneWeaponTracker : MonoBehaviour
{
    private ShadowClone parentClone;
    private bool isQuitting = false;
    
    public void SetParentClone(ShadowClone clone)
    {
        parentClone = clone;
    }
    
    void Update()
    {
        // If parent clone was destroyed, destroy this weapon too
        if (!isQuitting && parentClone == null)
        {
            // If this object is itself a ShadowClone (tracker accidentally on the clone root), do not self-destruct
            if (GetComponent<ShadowClone>() != null) return;

            // In edit mode or during domain reload, avoid destroying
            if (!Application.isPlaying) return;

            Debug.Log($"[CloneWeaponTracker] Parent clone destroyed, destroying weapon: {gameObject.name}");
            Destroy(gameObject);
        }
    }
    
    void OnApplicationQuit()
    {
        isQuitting = true;
    }
    
    void OnDestroy()
    {
        if (!isQuitting)
        {
            Debug.Log($"[CloneWeaponTracker] Weapon destroyed: {gameObject.name}");
        }
    }
}
