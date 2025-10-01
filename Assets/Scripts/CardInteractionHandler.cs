using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class CardInteractionHandler : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    private Vector3 originalPosition;
    private Transform originalParent;
    private int cardIndex;
    private CanvasGroup canvasGroup;
    private Image cardImage;
    private int originalSiblingIndex;

    private bool isDragging = false;
    private Vector3 dragOffset = new Vector3(0, 50, 0);

    void Start()
    {
        string name = gameObject.name;

        // Only parse if name matches expected format "Card_X"
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
        originalPosition = transform.position;
        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();
    }

    // Click to return dimmed card to hand
    public void OnPointerClick(PointerEventData eventData)
    {
        if (isDragging) return; // Ignore clicks if we just dragged

        if (GameManager.Instance.currentPhase != GameManager.GamePhase.Planning)
            return;

        // If card is dimmed (selected), return it to hand
        if (cardImage.color.a < 1f)
        {
            GameManager.Instance.ReturnCardToHand(cardIndex);
        }
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
        originalPosition = transform.position;

        // Move to canvas root so it's on top
        transform.SetParent(transform.root);
        transform.SetAsLastSibling();

        canvasGroup.alpha = 0.7f;
        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (GameManager.Instance.currentPhase != GameManager.GamePhase.Planning)
            return;

        // Follow mouse with offset above
        transform.position = Input.mousePosition + dragOffset;

        // Calculate rotation based on x position
        float screenWidth = Screen.width;
        float normalizedX = (transform.position.x / screenWidth) * 2f - 1f; // -1 to 1
        float targetRotation = normalizedX * 30f; // -30 to 30 degrees
        transform.rotation = Quaternion.Euler(0, 0, -targetRotation);

        // Highlight drop zones
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

        // Check what's under the CARD (not the cursor)
        PointerEventData cardPointerData = new PointerEventData(EventSystem.current);
        cardPointerData.position = transform.position;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(cardPointerData, results);

        bool droppedInArea = false;
        foreach (RaycastResult result in results)
        {
            if (result.gameObject.name == "LeftNeighborArea")
            {
                StartCoroutine(SpinAndLandOnSlot(true, cardIndex));
                droppedInArea = true;
                break;
            }
            else if (result.gameObject.name == "RightNeighborArea")
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

        // Reset dragging flag after a short delay to avoid click trigger
        Invoke("ResetDragFlag", 0.1f);
    }

    void ResetDragFlag()
    {
        isDragging = false;
    }

    void CheckDropZones(PointerEventData eventData)
    {
        // Check what's under the card, not the cursor
        PointerEventData cardPointerData = new PointerEventData(EventSystem.current);
        cardPointerData.position = transform.position;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(cardPointerData, results);

        bool overLeft = false, overRight = false;

        foreach (RaycastResult result in results)
        {
            if (result.gameObject.name == "LeftNeighborArea") overLeft = true;
            if (result.gameObject.name == "RightNeighborArea") overRight = true;
        }

        GameObject left = GameObject.Find("LeftNeighborArea");
        GameObject right = GameObject.Find("RightNeighborArea");

        if (left) left.GetComponent<Image>().color = overLeft ?
            new Color(0, 1, 0, 0.4f) : new Color(0, 0, 0, 0.3f);
        if (right) right.GetComponent<Image>().color = overRight ?
            new Color(0, 1, 0, 0.4f) : new Color(0, 0, 0, 0.3f);
    }

    IEnumerator SpinAndLandOnSlot(bool isLeftSlot, int cardIdx)
    {
        // 360 spin and move to slot position
        float spinDuration = 0.5f;
        float elapsed = 0f;
        Quaternion startRotation = transform.rotation;
        Vector3 startPosition = transform.position;

        // Get target position
        GameObject targetSlot = isLeftSlot ?
            GameManager.Instance.playerLeftCard.gameObject :
            GameManager.Instance.playerRightCard.gameObject;
        Vector3 targetPosition = targetSlot.transform.position;

        // Spin and move simultaneously
        while (elapsed < spinDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / spinDuration;

            // Spin 360 degrees
            float angle = Mathf.Lerp(0, 360, progress);
            transform.rotation = Quaternion.Euler(0, 0, startRotation.eulerAngles.z + angle);

            // Move to target position
            transform.position = Vector3.Lerp(startPosition, targetPosition, progress);

            yield return null;
        }

        // Ensure final state
        transform.rotation = Quaternion.identity;
        transform.position = targetPosition;

        // NOW update the GameManager to show the card in the slot
        if (isLeftSlot)
        {
            GameManager.Instance.SelectCardForLeft(cardIdx);
        }
        else
        {
            GameManager.Instance.SelectCardForRight(cardIdx);
        }

        // Hide the dragged card
        gameObject.SetActive(false);
    }

    void ResetCard()
    {
        StopAllCoroutines();
        StartCoroutine(SmoothReturnToHand());
    }

    IEnumerator SmoothReturnToHand()
    {
        float duration = 0.3f;
        float elapsed = 0f;
        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;

            transform.position = Vector3.Lerp(startPosition, originalPosition, progress);
            transform.rotation = Quaternion.Lerp(startRotation, Quaternion.identity, progress);

            yield return null;
        }

        transform.SetParent(originalParent);
        transform.position = originalPosition;
        transform.rotation = Quaternion.identity;
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.UpdateHandDisplay();
        }
    }

    void ResetDropZoneColors()
    {
        GameObject left = GameObject.Find("LeftNeighborArea");
        GameObject right = GameObject.Find("RightNeighborArea");
        if (left) left.GetComponent<Image>().color = new Color(0, 0, 0, 0.3f);
        if (right) right.GetComponent<Image>().color = new Color(0, 0, 0, 0.3f);
    }
}