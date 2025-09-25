using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;

public class GameManager : MonoBehaviourPunCallbacks
{
    public static GameManager Instance;

    [Header("Game State")]
    public GamePhase currentPhase = GamePhase.Waiting;
    public int playerPosition; // 0, 1, or 2
    public float turnTimer = 60f;
    private float currentTimer;

    [Header("UI References")]
    public TextMeshProUGUI phaseText;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI turnipsText;
    public TextMeshProUGUI relicProgress;
    public TextMeshProUGUI versionText;

    [Header("Bot Settings")]
    public int botCount = 0;
    private bool[] isBot = new bool[3];

    [Header("Player Hand")]
    public List<Image> handCards; // Card_0, Card_1, etc.
    public List<Card> playerHand;

    [Header("Neighbor Areas")]
    public TextMeshProUGUI leftPlayerName;
    public TextMeshProUGUI leftPlayerStats;
    public Image leftCardPlayed;
    public Image leftCardPlayed2;

    public TextMeshProUGUI rightPlayerName;
    public TextMeshProUGUI rightPlayerStats;
    public Image rightCardPlayed;
    public Image rightCardPlayed2;

    [Header("Resources")]
    public int turnips = 1;
    public int bank = 1;
    public int bankLimit = 5;
    public int relics = 0;

    private Card selectedLeftCard;
    private Card selectedRightCard;

    public enum GamePhase
    {
        Waiting,
        Planning,
        Revealing,
        Resolving,
        Refresh
    }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        versionText.text = $"v{Application.version}";
        UpdateUI();
    }

    public void InitializeGame()
    {
        // Assign player positions based on actor number
        playerPosition = PhotonNetwork.LocalPlayer.ActorNumber - 1; // 0, 1, or 2

        // Initialize starting hand
        playerHand = new List<Card>();
        playerHand.Add(CardData.Instance.CreateCardCopy(1)); // Farmer
        playerHand.Add(CardData.Instance.CreateCardCopy(2)); // Wall
        playerHand.Add(CardData.Instance.CreateCardCopy(3)); // Raider
        playerHand.Add(CardData.Instance.CreateCardCopy(4)); // Merchant

        // Setup hand buttons
        SortHand();
        UpdateHandDisplay();

        // Setup neighbor names
        SetupNeighborDisplay();

        // Start game
        if (PhotonNetwork.IsMasterClient)
        {
            photonView.RPC("StartPlanningPhase", RpcTarget.All);
        }
    }

    void SetupNeighborDisplay()
    {
        int leftPlayer = (playerPosition + 2) % 3; // Previous player in circle
        int rightPlayer = (playerPosition + 1) % 3; // Next player in circle

        leftPlayerName.text = "Player " + (leftPlayer + 1);
        rightPlayerName.text = "Player " + (rightPlayer + 1);
    }

    public void UpdateHandDisplay()
    {
        for (int i = 0; i < handCards.Count; i++)
        {
            if (i < playerHand.Count)
            {
                handCards[i].gameObject.SetActive(true);
                handCards[i].sprite = playerHand[i].cardSprite;

                // Set the correct sibling index to maintain sorted order
                handCards[i].transform.SetSiblingIndex(i);

                // Check if card is selected or exhausted
                if (playerHand[i].isExhausted)
                {
                    handCards[i].color = Color.gray;
                }
                else if (selectedLeftCard == playerHand[i] || selectedRightCard == playerHand[i])
                {
                    handCards[i].color = new Color(1, 1, 1, 0.3f); // Dimmed if selected
                }
                else
                {
                    handCards[i].color = Color.white; // Normal
                }
            }
            else
            {
                handCards[i].gameObject.SetActive(false);
            }
        }
    }

    void UpdateUI()
    {
        turnipsText.text = $"Turnips: {turnips} | Bank: {bank}/{bankLimit}";
        relicProgress.text = $"Relics: {relics}/3";

        if (currentPhase == GamePhase.Planning)
        {
            timerText.text = Mathf.Ceil(currentTimer).ToString();
        }
    }

    [PunRPC]
    void StartPlanningPhase()
    {
        currentPhase = GamePhase.Planning;
        phaseText.text = "PLANNING PHASE";
        phaseText.color = Color.yellow;
        currentTimer = turnTimer;

        selectedLeftCard = null;
        selectedRightCard = null;

        UpdateHandDisplay();
        StartCoroutine(PlanningTimer());
    }

    IEnumerator PlanningTimer()
    {
        while (currentTimer > 0 && currentPhase == GamePhase.Planning)
        {
            currentTimer -= Time.deltaTime;
            UpdateUI();
            if (currentTimer <= 25f && PhotonNetwork.IsMasterClient)
            {
                SimulateBotMoves();
            }
            yield return null;
        }

        // Time's up - select random cards if not selected
        if (selectedLeftCard == null || selectedRightCard == null)
        {
            SelectRandomCards();
        }

        SubmitCards();
    }

    void SelectRandomCards()
    {
        List<Card> available = new List<Card>();
        foreach (Card c in playerHand)
        {
            if (!c.isExhausted) available.Add(c);
        }

        if (available.Count > 0 && selectedLeftCard == null)
            selectedLeftCard = available[Random.Range(0, available.Count)];

        if (available.Count > 0 && selectedRightCard == null)
            selectedRightCard = available[Random.Range(0, available.Count)];
    }

    void SubmitCards()
    {
        if (selectedLeftCard == null || selectedRightCard == null)
            return;

        int leftCardId = selectedLeftCard.id;
        int rightCardId = selectedRightCard.id;

        photonView.RPC("ReceiveCardSelection", RpcTarget.All,
            PhotonNetwork.LocalPlayer.ActorNumber, leftCardId, rightCardId);
    }

    public void SelectCardForLeft(int cardIndex)
    {
        if (cardIndex >= playerHand.Count || playerHand[cardIndex].isExhausted)
            return;

        // If clicking the already selected card, deselect it
        if (selectedLeftCard == playerHand[cardIndex])
        {
            selectedLeftCard = null;
            leftCardPlayed2.gameObject.SetActive(false);
            handCards[cardIndex].color = Color.white;
            Debug.Log("Deselected left card");
            return;
        }

        // Can't use same card for both neighbors
        if (selectedRightCard == playerHand[cardIndex])
        {
            // Swap: move right card to left
            selectedLeftCard = selectedRightCard;
            selectedRightCard = null;

            // Update visuals
            leftCardPlayed2.sprite = selectedLeftCard.cardSprite;
            leftCardPlayed2.gameObject.SetActive(true);
            rightCardPlayed2.gameObject.SetActive(false);

            Debug.Log($"Swapped {selectedLeftCard.cardName} from right to left");
            return;
        }

        // Clear previous selection if different card
        if (selectedLeftCard != null)
        {
            for (int i = 0; i < playerHand.Count; i++)
            {
                if (playerHand[i] == selectedLeftCard)
                {
                    handCards[i].color = Color.white;
                    break;
                }
            }
        }

        selectedLeftCard = playerHand[cardIndex];
        leftCardPlayed2.sprite = selectedLeftCard.cardSprite;
        leftCardPlayed2.color = Color.white;
        leftCardPlayed2.gameObject.SetActive(true);
        handCards[cardIndex].color = new Color(1, 1, 1, 0.3f);

        Debug.Log($"Selected {selectedLeftCard.cardName} for LEFT neighbor");
    }

    public void SelectCardForRight(int cardIndex)
    {
        if (cardIndex >= playerHand.Count || playerHand[cardIndex].isExhausted)
            return;

        // If clicking the already selected card, deselect it
        if (selectedRightCard == playerHand[cardIndex])
        {
            selectedRightCard = null;
            rightCardPlayed2.gameObject.SetActive(false);
            handCards[cardIndex].color = Color.white;
            Debug.Log("Deselected right card");
            return;
        }

        // Can't use same card for both neighbors - offer to swap
        if (selectedLeftCard == playerHand[cardIndex])
        {
            // Swap: move left card to right
            selectedRightCard = selectedLeftCard;
            selectedLeftCard = null;

            // Update visuals
            rightCardPlayed2.sprite = selectedRightCard.cardSprite;
            rightCardPlayed2.gameObject.SetActive(true);
            leftCardPlayed2.gameObject.SetActive(false);

            Debug.Log($"Swapped {selectedRightCard.cardName} from left to right");
            return;
        }

        // Clear previous selection if different card
        if (selectedRightCard != null)
        {
            for (int i = 0; i < playerHand.Count; i++)
            {
                if (playerHand[i] == selectedRightCard)
                {
                    handCards[i].color = Color.white;
                    break;
                }
            }
        }

        selectedRightCard = playerHand[cardIndex];
        rightCardPlayed2.sprite = selectedRightCard.cardSprite;
        rightCardPlayed2.color = Color.white;
        rightCardPlayed2.gameObject.SetActive(true);
        handCards[cardIndex].color = new Color(1, 1, 1, 0.3f);

        Debug.Log($"Selected {selectedRightCard.cardName} for RIGHT neighbor");
    }

    public bool HasSelectedBothCards()
    {
        return selectedLeftCard != null && selectedRightCard != null;
    }

    void Update()
    {
        // TEMPORARY: Press Space to start game for testing
        if (Input.GetKeyDown(KeyCode.Space) && currentPhase == GamePhase.Waiting)
        {
            InitializeGame();
        }

        // Update phase text based on card selection
        if (currentPhase == GamePhase.Planning)
        {
            if (HasSelectedBothCards())
            {
                phaseText.text = "WAITING FOR OTHERS...";
                phaseText.color = Color.cyan;
            }
            else
            {
                phaseText.text = "PLANNING PHASE";
                phaseText.color = Color.yellow;
            }
        }
    }

    public void InitializeGameWithBots(int bots)
    {
        botCount = bots;

        // Mark which players are bots
        int humanCount = 3 - botCount;
        for (int i = 0; i < 3; i++)
        {
            isBot[i] = i >= humanCount;
        }

        InitializeGame();
    }

    void SimulateBotMoves()
    {
        // Simulate bot card selections
        for (int i = 0; i < 3; i++)
        {
            if (isBot[i])
            {
                // Random card selection for bots
                int leftCard = Random.Range(1, 5);
                int rightCard = Random.Range(1, 5);
                photonView.RPC("ReceiveCardSelection", RpcTarget.All, i + 1, leftCard, rightCard);
            }
        }
    }

    [PunRPC]
    void ReceiveCardSelection(int playerActorNumber, int leftCardId, int rightCardId)
    {
        int playerPos = playerActorNumber - 1; // Convert to 0,1,2

        Debug.Log($"Player {playerPos} played Left:{leftCardId} Right:{rightCardId}");

        // Store selections for resolution phase
        // We'll implement this next
    }

    public void SortHand()
    {
        playerHand.Sort((a, b) =>
        {
            // First sort by card type (Green=0, Blue=1, Red=2, Yellow=3)
            int typeCompare = a.type.CompareTo(b.type);
            if (typeCompare != 0) return typeCompare;

            // Then sort by ID
            return a.id.CompareTo(b.id);
        });
    }

    public void DeselectCard(bool isLeft)
    {
        if (isLeft && selectedLeftCard != null)
        {
            // Reset visual for the card in hand
            for (int i = 0; i < playerHand.Count; i++)
            {
                if (playerHand[i] == selectedLeftCard)
                {
                    handCards[i].color = Color.white;
                    break;
                }
            }

            selectedLeftCard = null;
            leftCardPlayed2.gameObject.SetActive(false);
            Debug.Log("Returned left card to hand");
        }
        else if (!isLeft && selectedRightCard != null)
        {
            // Reset visual for the card in hand
            for (int i = 0; i < playerHand.Count; i++)
            {
                if (playerHand[i] == selectedRightCard)
                {
                    handCards[i].color = Color.white;
                    break;
                }
            }

            selectedRightCard = null;
            rightCardPlayed2.gameObject.SetActive(false);
            Debug.Log("Returned right card to hand");
        }

        if (!HasSelectedBothCards() && currentPhase == GamePhase.Planning)
        {
            phaseText.text = "PLANNING PHASE";
            phaseText.color = Color.yellow;
        }
    }

    public void ReturnCardToHand(int cardIndex)
    {
        if (cardIndex >= playerHand.Count)
            return;

        Card card = playerHand[cardIndex];

        // Check if this card is selected for left
        if (selectedLeftCard == card)
        {
            selectedLeftCard = null;
            leftCardPlayed2.gameObject.SetActive(false);
            handCards[cardIndex].color = Color.white;
            Debug.Log("Returned left card to hand via click");
        }
        // Check if this card is selected for right
        else if (selectedRightCard == card)
        {
            selectedRightCard = null;
            rightCardPlayed2.gameObject.SetActive(false);
            handCards[cardIndex].color = Color.white;
            Debug.Log("Returned right card to hand via click");
        }

        // Update phase text
        if (!HasSelectedBothCards() && currentPhase == GamePhase.Planning)
        {
            phaseText.text = "PLANNING PHASE";
            phaseText.color = Color.yellow;
        }

        SortHand();
        UpdateHandDisplay();
    }
}