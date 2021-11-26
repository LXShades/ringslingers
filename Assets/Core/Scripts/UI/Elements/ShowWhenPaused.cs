using UnityEngine;

public class ShowWhenPaused : MonoBehaviour
{
    public GameObject target;

    private void Update()
    {
        if (target && GameManager.singleton)
        {
            if (target.activeSelf != GameManager.singleton.isPaused)
                target.SetActive(GameManager.singleton.isPaused);
        }
    }
}
