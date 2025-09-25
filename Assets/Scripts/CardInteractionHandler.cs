using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
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
        cardIndex = int.Parse(name.Split('_')[1]);
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
        // Create a new pointer event at the card's position
        PointerEventData cardPointerData = new PointerEventData(EventSystem.current);
        cardPointerData.position = transform.position; // Card's actual position

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(cardPointerData, results);

        foreach (RaycastResult result in results)
        {
            if (result.gameObject.name == "LeftNeighborArea")
            {
                GameManager.Instance.SelectCardForLeft(cardIndex);
                break;
            }
            else if (result.gameObject.name == "RightNeighborArea")
            {
                GameManager.Instance.SelectCardForRight(cardIndex);
                break;
            }
        }

        ResetCard();
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
        cardPointerData.position = transform.position; // Card's position

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

    void ResetCard()
    {
        transform.SetParent(originalParent);
        transform.position = originalPosition;
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