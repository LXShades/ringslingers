using UnityEngine;

public class TickerComponent : MonoBehaviour
{
    [Header("Ticker Setup")]
    public TickerSettings settings = TickerSettings.Default;

    public ITickerBase ticker;

    private void Awake()
    {
        ticker = GetComponent<ITickableBase>().CreateTicker();
    }
}
