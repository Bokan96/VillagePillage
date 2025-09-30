using UnityEngine;
using UnityEngine.EventSystems;

public class MarketCardHandler : MonoBehaviour, IPointerClickHandler
{
    public int marketIndex; // 0-3 for the 4 market slots

    public void OnPointerClick(PointerEventData eventData)
    {
        if (GameManager.Instance.currentPhase != GameManager.GamePhase.Market)
            return;

        GameManager.Instance.OnMarketCardClicked(marketIndex);
    }
}
