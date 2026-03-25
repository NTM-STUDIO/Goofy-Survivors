using UnityEngine;

public class StartingMenuManager : MonoBehaviour
{
    public GameObject startingMenuUI;
    public GameObject unitSelectorUI;

    public void onClickStartGame()
    {
        startingMenuUI.SetActive(false);
        unitSelectorUI.SetActive(true);
        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
            var firstSelectable = unitSelectorUI.GetComponentInChildren<UnityEngine.UI.Selectable>();
            if (firstSelectable != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(firstSelectable.gameObject);
        }
    }
}
