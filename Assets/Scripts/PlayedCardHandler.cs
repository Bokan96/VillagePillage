using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;

public class PlayedCardHandler : MonoBehaviour, IPointerClickHandler
{
    public bool isLeftCard;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (GameManager.Instance.currentPhase != GameManager.GamePhase.Planning)
            return;

        StartCoroutine(AnimateCardBackToHand());
    }

    IEnumerator AnimateCardBackToHand()
    {
        // Get the card that was played
        Card playedCard = isLeftCard ?
            GameManager.Instance.selectedLeftCard :
            GameManager.Instance.selectedRightCard;

        if (playedCard == null) yield break;

        int cardIndex = GameManager.Instance.playerHand.IndexOf(playedCard);
        if (cardIndex == -1) yield break;

        // First, deselect the card (this creates it in hand)
        if (isLeftCard)
        {
            GameManager.Instance.selectedLeftCard = null;
            GameManager.Instance.playerLeftCard.gameObject.SetActive(false);
        }
        else
        {
            GameManager.Instance.selectedRightCard = null;
            GameManager.Instance.playerRightCard.gameObject.SetActive(false);
        }

        if (!GameManager.Instance.HasSelectedBothCards() && GameManager.Instance.currentPhase == GameManager.GamePhase.Planning)
        {
            GameManager.Instance.phaseText.text = "PLANNING PHASE";
            GameManager.Instance.phaseText.color = Color.yellow;
        }

        GameManager.Instance.SortHand();
        GameManager.Instance.UpdateHandDisplay();

        // Wait one frame for hand to rebuild
        yield return null;

        // Find the card that was just created
        GameObject targetCard = null;
        string cardName = $"Card_{cardIndex}";
        foreach (Transform child in GameManager.Instance.handContainer)
        {
            if (child.name == cardName)
            {
                targetCard = child.gameObject;
                break;
            }
        }

        if (targetCard == null) yield break;

        // Store its final position
        Vector3 finalPosition = targetCard.transform.position;

        // Move it to slot position instantly
        targetCard.transform.SetParent(transform.root);
        targetCard.transform.position = transform.position;

        // Animate back to final position
        float duration = 0.3f;
        float elapsed = 0f;
        Vector3 startPosition = targetCard.transform.position;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;
            targetCard.transform.position = Vector3.Lerp(startPosition, finalPosition, progress);
            yield return null;
        }

        // Parent back to hand
        targetCard.transform.SetParent(GameManager.Instance.handContainer);
        targetCard.transform.position = finalPosition;
    }
}