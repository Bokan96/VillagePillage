using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

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
    private bool botsSimulated = false;

    [Header("Player Area")]
    public GameObject handCardPrefab; // Prefab for hand card UI
    public Transform handContainer; // Parent container for hand cards
    private List<GameObject> handCardObjects = new List<GameObject>(); // Instantiated card objects
    public List<Card> playerHand;
    public Image playerLeftCard;   // Card YOU played to left neighbor
    public Image playerRightCard;  // Card YOU played to right neighbor

    [Header("Left Neighbor Display")]
    public TextMeshProUGUI leftPlayerName;
    public TextMeshProUGUI leftPlayerStats;
    public Image leftPlayerLeftCard;   // Left neighbor's card to THEIR left
    public Image leftPlayerRightCard;

    [Header("Right Neighbor Display")]
    public TextMeshProUGUI rightPlayerName;
    public TextMeshProUGUI rightPlayerStats;
    public Image rightPlayerLeftCard;   // Right neighbor's card to THEIR left (facing you)
    public Image rightPlayerRightCard;

    [Header("Market")]
    public List<Image> marketCardImages; // 4 card slots in UI
    private List<int> marketDeck; // Deck of market card IDs
    private List<int> displayedMarket; // Currently displayed 4 cards
    private Vector3[] originalMarketScales;
    private Vector3[] originalMarketPositions;
    private HashSet<int> playersPurchasedThisMarket = new HashSet<int>(); // Track who bought this phase

    [Header("Resources")]
    public int turnips = 1;
    public int bank = 1;
    public int bankLimit = 5;
    public int relics = 0;
    private int[] relicCosts = new int[] { 8, 9, 10 };

    [Header("Card Selections")]
    private Dictionary<int, CardSelection> allPlayerSelections = new Dictionary<int, CardSelection>();

    [System.Serializable]
    public class CardSelection
    {
        public int playerId;
        public int leftCardId;
        public int rightCardId;
        public Card leftCard;
        public Card rightCard;
    }

    [Header("Player Resources")]
    private int[] playerTurnips = new int[3] { 1, 1, 1 };  // All start with 1 turnip
    private int[] playerBank = new int[3] { 1, 1, 1 };     // All start with 1 in bank
    private int[] playerRelics = new int[3] { 0, 0, 0 };

    private Card selectedLeftCard;
    private Card selectedRightCard;

    public enum GamePhase
    {
        Waiting,
        Planning,
        Revealing,
        Resolving,
        Market,
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

        // Initialize market
        InitializeMarket();

        // Start game
        if (PhotonNetwork.IsMasterClient)
        {
            photonView.RPC("StartPlanningPhase", RpcTarget.All);
        }
    }

    void InitializeMarket()
    {
        // Only master client creates and shuffles the deck
        if (PhotonNetwork.IsMasterClient)
        {
            // Create market deck with 2 copies of each market card (IDs 5-9)
            List<int> deck = new List<int>();
            for (int cardId = 5; cardId <= 9; cardId++)
            {
                for (int copy = 0; copy < 2; copy++)
                {
                    deck.Add(cardId);
                }
            }

            // Shuffle
            for (int i = deck.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int temp = deck[i];
                deck[i] = deck[j];
                deck[j] = temp;
            }

            // Send shuffled deck to all clients
            photonView.RPC("ReceiveMarketDeck", RpcTarget.All, deck.ToArray());
        }

        // Store original scales and positions for market cards
        originalMarketScales = new Vector3[marketCardImages.Count];
        originalMarketPositions = new Vector3[marketCardImages.Count];
        for (int i = 0; i < marketCardImages.Count; i++)
        {
            originalMarketScales[i] = marketCardImages[i].transform.localScale;
            originalMarketPositions[i] = marketCardImages[i].transform.localPosition;
        }
    }

    [PunRPC]
    void ReceiveMarketDeck(int[] deckArray)
    {
        marketDeck = new List<int>(deckArray);

        // Draw initial 4 cards
        displayedMarket = new List<int>();
        for (int i = 0; i < 4; i++)
        {
            DrawMarketCard();
        }

        UpdateMarketDisplay();
        Debug.Log($"Market initialized with cards: {string.Join(", ", displayedMarket)}");
    }

    void DrawMarketCard()
    {
        if (marketDeck.Count > 0)
        {
            int cardId = marketDeck[0];
            marketDeck.RemoveAt(0);
            displayedMarket.Add(cardId);
        }
    }

    void UpdateMarketDisplay()
    {
        for (int i = 0; i < marketCardImages.Count; i++)
        {
            if (i < displayedMarket.Count)
            {
                marketCardImages[i].gameObject.SetActive(true);
                marketCardImages[i].sprite = CardData.Instance.GetCard(displayedMarket[i]).cardSprite;
            }
            else
            {
                marketCardImages[i].gameObject.SetActive(false);
            }
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
        // Destroy all existing card objects
        foreach (GameObject cardObj in handCardObjects)
        {
            Destroy(cardObj);
        }
        handCardObjects.Clear();

        // Create new card objects for each card in hand
        for (int i = 0; i < playerHand.Count; i++)
        {
            // Instantiate card prefab
            GameObject cardObj = Instantiate(handCardPrefab, handContainer);
            cardObj.name = $"Card_{i}";
            handCardObjects.Add(cardObj);

            // Get Image component and set sprite
            Image cardImage = cardObj.GetComponent<Image>();
            cardImage.sprite = playerHand[i].cardSprite;

            // Set color based on state
            if (playerHand[i].isExhausted)
            {
                cardImage.color = Color.gray;
            }
            else if (selectedLeftCard == playerHand[i] || selectedRightCard == playerHand[i])
            {
                cardImage.color = new Color(1, 1, 1, 0.3f); // Dimmed if selected
            }
            else
            {
                cardImage.color = Color.white; // Normal
            }

            // Ensure CardInteractionHandler is present
            CardInteractionHandler handler = cardObj.GetComponent<CardInteractionHandler>();
            if (handler == null)
            {
                handler = cardObj.AddComponent<CardInteractionHandler>();
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
        botsSimulated = false; // Reset bot flag

        UpdateHandDisplay();
        StartCoroutine(PlanningTimer());
    }

    IEnumerator PlanningTimer()
    {
        while (currentTimer > 0 && currentPhase == GamePhase.Planning)
        {
            currentTimer -= Time.deltaTime;
            UpdateUI();
            if (currentTimer <= 25f && PhotonNetwork.IsMasterClient && !botsSimulated)
            {
                SimulateBotMoves();
                botsSimulated = true;
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
            playerLeftCard.gameObject.SetActive(false);
            if (cardIndex < handCardObjects.Count)
                handCardObjects[cardIndex].GetComponent<Image>().color = Color.white;
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
            playerLeftCard.sprite = selectedLeftCard.cardSprite;
            playerLeftCard.gameObject.SetActive(true);
            playerRightCard.gameObject.SetActive(false);

            Debug.Log($"Swapped {selectedLeftCard.cardName} from right to left");
            UpdateHandDisplay();
            return;
        }

        // Clear previous selection if different card
        if (selectedLeftCard != null)
        {
            for (int i = 0; i < playerHand.Count; i++)
            {
                if (playerHand[i] == selectedLeftCard && i < handCardObjects.Count)
                {
                    handCardObjects[i].GetComponent<Image>().color = Color.white;
                    break;
                }
            }
        }

        selectedLeftCard = playerHand[cardIndex];
        playerLeftCard.sprite = selectedLeftCard.cardSprite;
        playerLeftCard.color = Color.white;
        playerLeftCard.gameObject.SetActive(true);
        if (cardIndex < handCardObjects.Count)
            handCardObjects[cardIndex].GetComponent<Image>().color = new Color(1, 1, 1, 0.3f);

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
            playerRightCard.gameObject.SetActive(false);
            if (cardIndex < handCardObjects.Count)
                handCardObjects[cardIndex].GetComponent<Image>().color = Color.white;
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
            playerRightCard.sprite = selectedRightCard.cardSprite;
            playerRightCard.gameObject.SetActive(true);
            playerLeftCard.gameObject.SetActive(false);

            Debug.Log($"Swapped {selectedRightCard.cardName} from left to right");
            UpdateHandDisplay();
            return;
        }

        // Clear previous selection if different card
        if (selectedRightCard != null)
        {
            for (int i = 0; i < playerHand.Count; i++)
            {
                if (playerHand[i] == selectedRightCard && i < handCardObjects.Count)
                {
                    handCardObjects[i].GetComponent<Image>().color = Color.white;
                    break;
                }
            }
        }

        selectedRightCard = playerHand[cardIndex];
        playerRightCard.sprite = selectedRightCard.cardSprite;
        playerRightCard.color = Color.white;
        playerRightCard.gameObject.SetActive(true);
        if (cardIndex < handCardObjects.Count)
            handCardObjects[cardIndex].GetComponent<Image>().color = new Color(1, 1, 1, 0.3f);

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

        if (Input.GetKeyDown(KeyCode.R) && currentPhase == GamePhase.Planning && HasSelectedBothCards())
        {
            // First, submit our own cards
            SubmitCards();

            // Then simulate the OTHER two players only
            if (playerPosition == 0)
            {
                // We are player 0, simulate players 1 and 2
                photonView.RPC("ReceiveCardSelection", RpcTarget.All, 2, 2, 4); // Player 1: Wall left, Merchant right
                photonView.RPC("ReceiveCardSelection", RpcTarget.All, 3, 3, 1); // Player 2: Raider left, Farmer right
            }
            else if (playerPosition == 1)
            {
                // We are player 1, simulate players 0 and 2
                photonView.RPC("ReceiveCardSelection", RpcTarget.All, 1, 1, 3); // Player 0: Farmer left, Raider right
                photonView.RPC("ReceiveCardSelection", RpcTarget.All, 3, 3, 1); // Player 2: Raider left, Farmer right
            }
            else
            {
                // We are player 2, simulate players 0 and 1
                photonView.RPC("ReceiveCardSelection", RpcTarget.All, 1, 1, 3); // Player 0: Farmer left, Raider right
                photonView.RPC("ReceiveCardSelection", RpcTarget.All, 2, 2, 4); // Player 1: Wall left, Merchant right
            }
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
                // Random card selection for bots (1-3: Farmer, Wall, Raider)
                int leftCard = Random.Range(1, 4);
                int rightCard = Random.Range(1, 4);

                // Ensure different cards
                while (rightCard == leftCard)
                {
                    rightCard = Random.Range(1, 4);
                }

                photonView.RPC("ReceiveCardSelection", RpcTarget.All, i + 1, leftCard, rightCard);
            }
        }
    }

    [PunRPC]
    void ReceiveCardSelection(int playerActorNumber, int leftCardId, int rightCardId)
    {
        int playerPos = playerActorNumber - 1; // Convert to 0,1,2

        Debug.Log($"Player {playerPos} played Left:{CardData.Instance.GetCard(leftCardId).cardName} Right:{CardData.Instance.GetCard(rightCardId).cardName}");

        // Store selection
        CardSelection selection = new CardSelection();
        selection.playerId = playerPos;
        selection.leftCardId = leftCardId;
        selection.rightCardId = rightCardId;
        selection.leftCard = CardData.Instance.GetCard(leftCardId);
        selection.rightCard = CardData.Instance.GetCard(rightCardId);

        allPlayerSelections[playerPos] = selection;

        // Display opponent cards in UI
        DisplayOpponentCards(playerPos, leftCardId, rightCardId);

        // Check if all players have submitted
        if (allPlayerSelections.Count == 3)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                photonView.RPC("StartRevealPhase", RpcTarget.All);
            }
        }
    }

    void DisplayOpponentCards(int playerPos, int leftCardId, int rightCardId)
    {
        // Determine which neighbor areas to update based on who played
        int leftNeighborPos = (playerPosition + 2) % 3;
        int rightNeighborPos = (playerPosition + 1) % 3;

        if (playerPos == leftNeighborPos)
        {
            // This is our left neighbor
            // They played rightCardId against us (their right is our left)
            leftPlayerRightCard.sprite = CardData.Instance.cardBackSprite;
            leftPlayerRightCard.gameObject.SetActive(true);

            // They played leftCardId against the other player
            leftPlayerLeftCard.sprite = CardData.Instance.cardBackSprite;
            leftPlayerLeftCard.gameObject.SetActive(true);
        }
        else if (playerPos == rightNeighborPos)
        {
            // This is our right neighbor  
            // They played leftCardId against us (their left is our right)
            rightPlayerLeftCard.sprite = CardData.Instance.cardBackSprite;
            rightPlayerLeftCard.gameObject.SetActive(true);

            // They played rightCardId against the other player
            rightPlayerRightCard.sprite = CardData.Instance.cardBackSprite;
            rightPlayerRightCard.gameObject.SetActive(true);
        }
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
                if (playerHand[i] == selectedLeftCard && i < handCardObjects.Count)
                {
                    handCardObjects[i].GetComponent<Image>().color = Color.white;
                    break;
                }
            }

            selectedLeftCard = null;
            playerLeftCard.gameObject.SetActive(false);
            Debug.Log("Returned left card to hand");
        }
        else if (!isLeft && selectedRightCard != null)
        {
            // Reset visual for the card in hand
            for (int i = 0; i < playerHand.Count; i++)
            {
                if (playerHand[i] == selectedRightCard && i < handCardObjects.Count)
                {
                    handCardObjects[i].GetComponent<Image>().color = Color.white;
                    break;
                }
            }

            selectedRightCard = null;
            playerRightCard.gameObject.SetActive(false);
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
            playerLeftCard.gameObject.SetActive(false);
            if (cardIndex < handCardObjects.Count)
                handCardObjects[cardIndex].GetComponent<Image>().color = Color.white;
            Debug.Log("Returned left card to hand via click");
        }
        // Check if this card is selected for right
        else if (selectedRightCard == card)
        {
            selectedRightCard = null;
            playerRightCard.gameObject.SetActive(false);
            if (cardIndex < handCardObjects.Count)
                handCardObjects[cardIndex].GetComponent<Image>().color = Color.white;
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

    [PunRPC]
    void StartRevealPhase()
    {
        Debug.Log("___StartRevealPhase called");
        currentPhase = GamePhase.Revealing;
        phaseText.text = "REVEALING...";
        phaseText.color = Color.white;

        StartCoroutine(RevealAndResolve());
    }

    IEnumerator RevealAndResolve()
    {

        yield return new WaitForSeconds(1f);

        // Reveal all cards
        RevealAllCards();

        yield return new WaitForSeconds(2f);

        // Start resolution
        currentPhase = GamePhase.Resolving;
        phaseText.text = "RESOLVING...";
        phaseText.color = Color.magenta;

        // Resolve in order: Green → Blue → Red → Yellow
        ResolveCardEffects();

        // Exhaust played cards
        ExhaustPlayedCards();

        yield return new WaitForSeconds(4f); // Increased from 2s to 4s

        // Start market phase
        if (PhotonNetwork.IsMasterClient)
        {
            photonView.RPC("StartMarketPhase", RpcTarget.All);
        }
    }

    void ExhaustPlayedCards()
    {
        // Mark the cards we played as exhausted
        if (selectedLeftCard != null)
            selectedLeftCard.isExhausted = true;
        if (selectedRightCard != null)
            selectedRightCard.isExhausted = true;

        UpdateHandDisplay();
    }

    void RevealAllCards()
    {
        // Show actual cards instead of backs
        int leftNeighborPos = (playerPosition + 2) % 3;
        int rightNeighborPos = (playerPosition + 1) % 3;

        if (allPlayerSelections.ContainsKey(leftNeighborPos))
        {
            var leftNeighborCards = allPlayerSelections[leftNeighborPos];
            // Their right card was played against us
            leftPlayerRightCard.sprite = CardData.Instance.GetCard(leftNeighborCards.rightCardId).cardSprite;
            // Their left card was played against the other player
            leftPlayerLeftCard.sprite = CardData.Instance.GetCard(leftNeighborCards.leftCardId).cardSprite;
        }

        if (allPlayerSelections.ContainsKey(rightNeighborPos))
        {
            var rightNeighborCards = allPlayerSelections[rightNeighborPos];
            // Their left card was played against us
            rightPlayerLeftCard.sprite = CardData.Instance.GetCard(rightNeighborCards.leftCardId).cardSprite;
            // Their right card was played against the other player
            rightPlayerRightCard.sprite = CardData.Instance.GetCard(rightNeighborCards.rightCardId).cardSprite;
        }
    }

    void ResolveCardEffects()
    {
        Debug.Log("ResolveCardEffects called - resolving by card type");

        // Resolve in order: Green → Blue → Red → Yellow
        CardType[] resolutionOrder = { CardType.Green, CardType.Blue, CardType.Red, CardType.Yellow };

        foreach (CardType currentType in resolutionOrder)
        {
            Debug.Log($"Resolving all {currentType} cards");

            // For each player, check if they have cards of this type to resolve
            for (int playerId = 0; playerId < 3; playerId++)
            {
                if (!allPlayerSelections.ContainsKey(playerId)) continue;

                var playerCards = allPlayerSelections[playerId];
                int leftTarget = (playerId + 2) % 3;  // Player to the left
                int rightTarget = (playerId + 1) % 3; // Player to the right

                // Check left card
                if (playerCards.leftCard.type == currentType && allPlayerSelections.ContainsKey(leftTarget))
                {
                    var opponentCard = allPlayerSelections[leftTarget].rightCard;
                    ApplyCardEffect(playerId, leftTarget, playerCards.leftCard, opponentCard);
                }

                // Check right card
                if (playerCards.rightCard.type == currentType && allPlayerSelections.ContainsKey(rightTarget))
                {
                    var opponentCard = allPlayerSelections[rightTarget].leftCard;
                    ApplyCardEffect(playerId, rightTarget, playerCards.rightCard, opponentCard);
                }
            }
        }

        // Update UI for local player
        UpdateResourceDisplay();
    }

    void ApplyCardEffect(int playerId, int targetId, Card myCard, Card opponentCard)
    {
        if (myCard == null || opponentCard == null) return;

        var effect = myCard.effects[opponentCard.type];

        // Apply gains
        if (effect.gain > 0)
        {
            playerTurnips[playerId] += effect.gain;
            Debug.Log($"Player {playerId} gains {effect.gain} turnips with {myCard.cardName}");
        }

        // Apply steals
        if (effect.steal > 0)
        {
            int stolen = Mathf.Min(effect.steal, playerTurnips[targetId]);
            playerTurnips[targetId] -= stolen;
            playerTurnips[playerId] += stolen;
            Debug.Log($"Player {playerId} steals {stolen} from Player {targetId} with {myCard.cardName}");
        }

        // Apply banking
        if (effect.bank > 0)
        {
            int toBank = Mathf.Min(effect.bank, playerTurnips[playerId]);
            toBank = Mathf.Min(toBank, bankLimit - playerBank[playerId]);
            playerTurnips[playerId] -= toBank;
            playerBank[playerId] += toBank;
            Debug.Log($"Player {playerId} banks {toBank} with {myCard.cardName}");
        }
    }

    void UpdateResourceDisplay()
    {
        turnips = playerTurnips[playerPosition];
        bank = playerBank[playerPosition];
        relics = playerRelics[playerPosition];
        UpdateUI();
    }

    [PunRPC]
    void StartMarketPhase()
    {
        currentPhase = GamePhase.Market;
        phaseText.text = "MARKET PHASE";
        phaseText.color = new Color(1f, 0.65f, 0f); // Orange

        // Clear purchase tracking
        playersPurchasedThisMarket.Clear();

        // Scale up market cards
        for (int i = 0; i < marketCardImages.Count; i++)
        {
            marketCardImages[i].transform.localScale = originalMarketScales[i] * 1.2f;
        }

        StartCoroutine(ProcessMarketPurchases());
    }

    IEnumerator ProcessMarketPurchases()
    {
        // Find all players who played Yellow cards
        List<int> yellowPlayers = new List<int>();

        for (int i = 0; i < 3; i++)
        {
            if (allPlayerSelections.ContainsKey(i))
            {
                var selection = allPlayerSelections[i];
                if (selection.leftCard.type == CardType.Yellow || selection.rightCard.type == CardType.Yellow)
                {
                    yellowPlayers.Add(i);
                }
            }
        }

        // Sort by priority: fewest relics → fewest turnips → random
        yellowPlayers.Sort((a, b) =>
        {
            if (playerRelics[a] != playerRelics[b])
                return playerRelics[a].CompareTo(playerRelics[b]);
            if (playerTurnips[a] != playerTurnips[b])
                return playerTurnips[a].CompareTo(playerTurnips[b]);
            return Random.Range(-1, 2);
        });

        // Process each yellow player's purchase
        foreach (int playerId in yellowPlayers)
        {
            yield return StartCoroutine(ProcessPlayerPurchase(playerId));
        }

        // Reset market card scales
        for (int i = 0; i < marketCardImages.Count; i++)
        {
            marketCardImages[i].transform.localScale = originalMarketScales[i];
        }

        // Check for victory
        CheckVictoryCondition();

        // Start next round
        if (PhotonNetwork.IsMasterClient)
        {
            photonView.RPC("StartRefreshPhase", RpcTarget.All);
        }
    }

    IEnumerator ProcessPlayerPurchase(int playerId)
    {
        // Check if already purchased
        if (playersPurchasedThisMarket.Contains(playerId))
        {
            yield break;
        }

        // Check if player can buy a relic (turnips + bank combined)
        int nextRelicCost = relicCosts[playerRelics[playerId]];
        int totalResources = playerTurnips[playerId] + playerBank[playerId];

        if (totalResources >= nextRelicCost)
        {
            // Auto-buy relic
            phaseText.text = $"Player {playerId + 1} buys Relic {playerRelics[playerId] + 1}!";

            // Deduct from turnips first, then bank
            int fromTurnips = Mathf.Min(nextRelicCost, playerTurnips[playerId]);
            int fromBank = nextRelicCost - fromTurnips;
            playerTurnips[playerId] -= fromTurnips;
            playerBank[playerId] -= fromBank;

            playerRelics[playerId]++;
            playersPurchasedThisMarket.Add(playerId);
            Debug.Log($"Player {playerId} bought relic for {nextRelicCost} ({fromTurnips} turnips + {fromBank} bank)");

            UpdateResourceDisplay();
            yield return new WaitForSeconds(2f);
        }
        else if (totalResources >= 1) // Can buy market card if has at least 1 resource
        {
            // Offer market purchase
            if (playerId == playerPosition)
            {
                // Local player - enable market card clicking
                phaseText.text = "Choose a card to buy (1 turnip)";
                phaseText.color = Color.green;

                // Wait for player to click a market card
                while (!playersPurchasedThisMarket.Contains(playerId) && currentPhase == GamePhase.Market)
                {
                    yield return null;
                }
            }
            else
            {
                // Other player - show their purchase
                phaseText.text = $"Player {playerId + 1} is shopping...";
                yield return new WaitForSeconds(2f);
                // Bot/other player purchase would be synced via RPC
            }
        }
    }

    public void OnMarketCardClicked(int marketIndex)
    {
        if (currentPhase != GamePhase.Market) return;
        int totalResources = turnips + bank;
        if (totalResources < 1) return;
        if (marketIndex >= displayedMarket.Count) return;
        if (playersPurchasedThisMarket.Contains(playerPosition)) return; // Already purchased

        int cardId = displayedMarket[marketIndex];

        // Purchase card
        photonView.RPC("PurchaseMarketCard", RpcTarget.All, playerPosition, cardId, marketIndex);
    }

    [PunRPC]
    void PurchaseMarketCard(int buyerId, int cardId, int marketIndex)
    {
        // Mark player as purchased
        playersPurchasedThisMarket.Add(buyerId);

        // Deduct 1 resource (turnips first, then bank)
        if (playerTurnips[buyerId] >= 1)
        {
            playerTurnips[buyerId] -= 1;
        }
        else
        {
            playerBank[buyerId] -= 1;
        }

        // Add card to buyer's hand (if local player)
        if (buyerId == playerPosition)
        {
            Card newCard = CardData.Instance.CreateCardCopy(cardId);
            playerHand.Add(newCard);
            SortHand();
            UpdateHandDisplay();
            Debug.Log($"Bought {newCard.cardName} from market. Hand size: {playerHand.Count}");
        }

        // Remove from market and replace
        displayedMarket.RemoveAt(marketIndex);
        DrawMarketCard();
        UpdateMarketDisplay();

        UpdateResourceDisplay();

        Debug.Log($"Player {buyerId} bought card ID {cardId}");
    }

    void CheckVictoryCondition()
    {
        // Check if any player has 3 relics
        for (int i = 0; i < 3; i++)
        {
            if (playerRelics[i] >= 3)
            {
                if (PhotonNetwork.IsMasterClient)
                {
                    photonView.RPC("GameOver", RpcTarget.All, i);
                }
                return;
            }
        }
    }

    [PunRPC]
    void GameOver(int winnerId)
    {
        currentPhase = GamePhase.Waiting;
        phaseText.text = winnerId == playerPosition ? "YOU WIN!" : $"Player {winnerId + 1} Wins!";
        phaseText.color = winnerId == playerPosition ? Color.green : Color.red;

        // TODO: Show final scores, rematch button, etc.
    }

    [PunRPC]
    void StartRefreshPhase()
    {
        currentPhase = GamePhase.Refresh;
        phaseText.text = "PREPARING NEXT ROUND...";
        phaseText.color = Color.gray;

        // Clear selections for next round
        allPlayerSelections.Clear();

        // Reset card visuals
        leftPlayerLeftCard.gameObject.SetActive(false);
        leftPlayerRightCard.gameObject.SetActive(false);
        rightPlayerLeftCard.gameObject.SetActive(false);
        rightPlayerRightCard.gameObject.SetActive(false);
        playerLeftCard.gameObject.SetActive(false);
        playerRightCard.gameObject.SetActive(false);

        selectedLeftCard = null;
        selectedRightCard = null;

        // Un-exhaust all cards for next turn
        foreach (Card card in playerHand)
        {
            card.isExhausted = false;
        }

        // Update hand display
        UpdateHandDisplay();

        // Start next planning phase after delay
        if (PhotonNetwork.IsMasterClient)
        {
            Invoke("StartNextRound", 2f);
        }
    }

    void StartNextRound()
    {
        photonView.RPC("StartPlanningPhase", RpcTarget.All);
    }
}