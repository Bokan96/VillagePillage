using UnityEngine;
using UnityEngine.EventSystems;

public class PlayedCardHandler : MonoBehaviour, IPointerClickHandler
{
    public bool isLeftCard;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (GameManager.Instance.currentPhase != GameManager.GamePhase.Planning)
            return;

        GameManager.Instance.DeselectCard(isLeftCard);
    }
}