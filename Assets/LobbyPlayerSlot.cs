using UnityEngine;
using TMPro;

public class LobbyPlayerSlot : MonoBehaviour
{
    public TextMeshProUGUI nameLabel;

    public void SetName(string name)
    {
        if (nameLabel != null)
            nameLabel.text = name;
    }
}
