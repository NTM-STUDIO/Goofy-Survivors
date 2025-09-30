using UnityEngine;

public class StartingMenuManager : MonoBehaviour
{
    public GameObject startingMenuUI;
    public GameObject unitSelectorUI;

    public void onClickStartGame()
    {
        startingMenuUI.SetActive(false);
        unitSelectorUI.SetActive(true);
    }
}
