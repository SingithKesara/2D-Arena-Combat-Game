using UnityEngine;
using UnityEngine.EventSystems;

public class MenuAutoSelect : MonoBehaviour
{
    [Tooltip("Assign the first button (Play) here")]
    public GameObject firstButton;

    void Start()
    {
        if (firstButton != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(firstButton);
        }
    }
}
