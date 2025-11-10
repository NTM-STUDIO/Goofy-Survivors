// Attach this script to your player prefab to enable the TeamWipeAbility in multiplayer.
using UnityEngine;

public class TeamWipeAbilityEnabler : MonoBehaviour
{
    void Awake()
    {
        if (GameManager.Instance != null && GameManager.Instance.isP2P)
        {
            if (GetComponent<TeamWipeAbility>() == null)
            {
                gameObject.AddComponent<TeamWipeAbility>();
            }
        }
    }
}
