using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

public class CardInteractionHandler : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    private Vector3 originalHandPosition;
    private Transform originalParent;
    private int cardIndex;
    private CanvasGroup canvasGroup;
    private Image cardImage;
    private int originalSiblingIndex;

    private bool isDragging = false;
    private Vector3 dragOffset = new Vector3(0, 50, 0);
    private Tween scaleTween;
    private bool wasInPlayArea = false;

    void Start()
    {
        string name = gameObject.name;

        if (name.Contains("_"))
        {
            string[] parts = name.Split('_');
            if (parts.Length > 1 && int.TryParse(parts[1], out int index))
            {
                cardIndex = index;
            }
        }

        canvasGroup = GetComponent<CanvasGroup>();
        cardImage = GetComponent<Image>();
        originalHandPosition = transform.position;
        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isDragging) return;

        if (GameManager.Instance.currentPhase != GameManager.GamePhase.Planning)
            return;
    }

    bool IsCardInPlayedPosition()
    {
        if (GameManager.Instance.selectedLeftCardObject == gameObject ||
            GameManager.Instance.selectedRightCardObject == gameObject)
        {
            return true;
        }
        return false;
    }

    void ReturnToHandFromPlayedPosition()
    {
        // Determine which slot this card is in
        bool isLeftSlot = (GameManager.Instance.selectedLeftCardObject == gameObject);

        // Clear the slot reference in GameManager
        if (isLeftSlot)
        {
            GameManager.Instance.selectedLeftCard = null;
            GameManager.Instance.selectedLeftCardObject = null;
            GameManager.Instance.playerLeftCard.gameObject.SetActive(false);
        }
        else
        {
            GameManager.Instance.selectedRightCard = null;
            GameManager.Instance.selectedRightCardObject = null;
            GameManager.Instance.playerRightCard.gameObject.SetActive(false);
        }

        // Update phase text
        if (!GameManager.Instance.HasSelectedBothCards() && GameManager.Instance.currentPhase == GameManager.GamePhase.Planning)
        {
            GameManager.Instance.phaseText.text = "PLANNING PHASE";
            GameManager.Instance.phaseText.color = Color.yellow;
        }

        // Sort hand and rebuild display FIRST
        GameManager.Instance.SortHand();
        GameManager.Instance.UpdateHandDisplay();

        // Now find the position and animate
        StartCoroutine(AnimateBackToHand());
    }

    IEnumerator AnimateBackToHand()
    {
        // Wait for layout to update
        yield return null;
        yield return null; // Extra frame to ensure layout is complete

        // Find the newly created card in hand at the correct position
        string targetName = $"Card_{cardIndex}";
        Vector3 targetPosition = GameManager.Instance.handContainer.position; // Fallback
        GameObject targetCard = null;

        foreach (Transform child in GameManager.Instance.handContainer)
        {
            if (child.name == targetName && child.gameObject != gameObject)
            {
                targetPosition = child.position;
                targetCard = child.gameObject;
                Debug.Log($"Found target card at position: {targetPosition}");
                break;
            }
        }

        // If we found the placeholder, hide it during animation
        if (targetCard != null)
        {
            targetCard.SetActive(false);
        }

        // Now animate to that position
        float duration = 0.3f;
        float elapsed = 0f;
        Vector3 startPosition = transform.position;
        Vector3 startScale = transform.localScale;
        Quaternion startRotation = transform.rotation;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;

            transform.position = Vector3.Lerp(startPosition, targetPosition, progress);
            scaleTween?.Kill();
            transform.localScale = Vector3.Lerp(startScale, Vector3.one, progress);
            transform.rotation = Quaternion.Lerp(startRotation, Quaternion.identity, progress);

            yield return null;
        }

        transform.position = targetPosition;
        transform.localScale = Vector3.one;
        transform.rotation = Quaternion.identity;
        canvasGroup.blocksRaycasts = true;

        // Destroy the placeholder and this card, then rebuild hand one final time
        if (targetCard != null)
        {
            Destroy(targetCard);
        }

        // Destroy this card and rebuild
        Destroy(gameObject);
        GameManager.Instance.UpdateHandDisplay();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (GameManager.Instance.currentPhase != GameManager.GamePhase.Planning)
            return;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (GameManager.Instance.currentPhase != GameManager.GamePhase.Planning)
            return;

        isDragging = true;
        wasInPlayArea = IsCardInPlayedPosition();
        

        if (wasInPlayArea)
        {
            if (GameManager.Instance.selectedLeftCardObject == gameObject)
            {
                GameManager.Instance.selectedLeftCard = null;
                GameManager.Instance.selectedLeftCardObject = null;
                GameManager.Instance.playerLeftCard.gameObject.SetActive(false);
            }
            else if (GameManager.Instance.selectedRightCardObject == gameObject)
            {
                GameManager.Instance.selectedRightCard = null;
                GameManager.Instance.selectedRightCardObject = null;
                GameManager.Instance.playerRightCard.gameObject.SetActive(false);
            }

            if (!GameManager.Instance.HasSelectedBothCards())
            {
                GameManager.Instance.phaseText.text = "PLANNING PHASE";
                GameManager.Instance.phaseText.color = Color.yellow;
            }
        }
        else
            originalHandPosition = transform.position;

        transform.SetParent(transform.root);
        transform.SetAsLastSibling();

        scaleTween?.Kill();
        scaleTween = transform.DOScale(1.4f, 0.2f).SetEase(Ease.OutBack);

        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (GameManager.Instance.currentPhase != GameManager.GamePhase.Planning)
            return;

        transform.position = Input.mousePosition + dragOffset;

        float screenWidth = Screen.width;
        float normalizedX = (transform.position.x / screenWidth) * 2f - 1f;
        float targetRotation = normalizedX * 30f;
        transform.rotation = Quaternion.Euler(0, 0, -targetRotation);

        CheckDropZones(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (GameManager.Instance.currentPhase != GameManager.GamePhase.Planning)
        {
            ResetCard();
            isDragging = false;
            return;
        }

        PointerEventData cardPointerData = new PointerEventData(EventSystem.current);
        cardPointerData.position = transform.position;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(cardPointerData, results);

        bool droppedInArea = false;
        foreach (RaycastResult result in results)
        {
            if (result.gameObject.name == "LeftPlayArea")
            {
                StartCoroutine(SpinAndLandOnSlot(true, cardIndex));
                droppedInArea = true;
                break;
            }
            else if (result.gameObject.name == "RightPlayArea")
            {
                StartCoroutine(SpinAndLandOnSlot(false, cardIndex));
                droppedInArea = true;
                break;
            }
        }

        if (!droppedInArea)
        {
            ResetCard();
        }

        ResetDropZoneColors();
        Invoke("ResetDragFlag", 0.1f);
    }

    void ReturnToHandFromDrag()
    {
        GameManager.Instance.SortHand();
        GameManager.Instance.UpdateHandDisplay();
        StartCoroutine(AnimateBackToHand());
    }

    void ResetDragFlag()
    {
        isDragging = false;
        wasInPlayArea = false;
    }

    void CheckDropZones(PointerEventData eventData)
    {
        PointerEventData cardPointerData = new PointerEventData(EventSystem.current);
        cardPointerData.position = transform.position;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(cardPointerData, results);

        bool overLeft = false, overRight = false;

        foreach (RaycastResult result in results)
        {
            if (result.gameObject.name == "LeftPlayArea") overLeft = true;
            if (result.gameObject.name == "RightPlayArea") overRight = true;
        }

        GameObject left = GameObject.Find("LeftPlayArea");
        GameObject right = GameObject.Find("RightPlayArea");

        if (left) left.GetComponent<Image>().color = overLeft ?
            new Color(0, 1, 0, 0.4f) : new Color(0, 0, 0, 0.3f);
        if (right) right.GetComponent<Image>().color = overRight ?
            new Color(0, 1, 0, 0.4f) : new Color(0, 0, 0, 0.3f);
    }

    IEnumerator SpinAndLandOnSlot(bool isLeftSlot, int cardIdx)
    {
        // Check for existing card
        GameObject existingCard = isLeftSlot ?
            GameManager.Instance.selectedLeftCardObject :
            GameManager.Instance.selectedRightCardObject;

        if (existingCard != null && existingCard != gameObject)
        {
            CardInteractionHandler existingHandler = existingCard.GetComponent<CardInteractionHandler>();
            if (existingHandler != null)
            {
                existingHandler.ResetCard();
            }
            yield return new WaitForSeconds(0.1f);
        }

        GameObject targetSlot = isLeftSlot ?
            GameManager.Instance.playerLeftCard.gameObject :
            GameManager.Instance.playerRightCard.gameObject;

        // DOTween sequence
        float endZRotation = isLeftSlot ? 25f : -25f;
        Sequence seq = DOTween.Sequence();
        seq.Append(transform.DOMove(targetSlot.transform.position, 0.5f).SetEase(Ease.InOutCubic));
        seq.Join(transform.DORotate(new Vector3(0, 0, endZRotation), 0.5f).SetEase(Ease.InOutCubic));
        seq.Join(transform.DOScale(1f, 0.5f).SetEase(Ease.InElastic));

        yield return seq.WaitForCompletion();

        // transform.rotation = Quaternion.identity;

        if (isLeftSlot)
        {
            GameManager.Instance.SelectCardForLeft(cardIdx, gameObject);
        }
        else
        {
            GameManager.Instance.SelectCardForRight(cardIdx, gameObject);
        }

        canvasGroup.blocksRaycasts = true;
    }

    void ResetCard()
    {
        StopAllCoroutines();
        ResetDragFlag();
        StartCoroutine(SmoothReturnToHand());
    }

    IEnumerator SmoothReturnToHand()
    {
        scaleTween?.Kill();

        Sequence seq = DOTween.Sequence();
        seq.Append(transform.DOMove(originalHandPosition, 0.7f).SetEase(Ease.InOutCubic));
        seq.Join(transform.DORotate(Vector3.zero, 0.7f).SetEase(Ease.InOutCubic));
        seq.Join(transform.DOScale(1f, 0.7f).SetEase(Ease.InOutCubic));

        yield return seq.WaitForCompletion();

        transform.SetParent(originalParent);
        transform.position = originalHandPosition;
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        canvasGroup.blocksRaycasts = true;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.UpdateHandDisplay();
        }
    }

    void ResetDropZoneColors()
    {
        GameObject left = GameObject.Find("LeftPlayArea");
        GameObject right = GameObject.Find("RightPlayArea");
        if (left) left.GetComponent<Image>().color = new Color(0, 0, 0, 0.3f);
        if (right) right.GetComponent<Image>().color = new Color(0, 0, 0, 0.3f);
    }

    void OnDestroy()
    {
        scaleTween?.Kill();
    }
}