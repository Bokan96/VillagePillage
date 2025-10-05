using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;

public class NetworkManager : MonoBehaviourPunCallbacks
{
    [Header("UI Screens")]
    public GameObject menuScreen;
    public GameObject gameScreen;
    public GameObject connectingScreen;

    [Header("Menu UI")]
    public Button createRoomBtn;
    public Button joinRoomBtn;
    public TMP_InputField roomCodeInput;

    [Header("Game UI")]
    public TextMeshProUGUI roomCodeText;
    public Button leaveBtn;

    [Header("Connecting")]
    public TextMeshProUGUI connectingText;

    // Add UI elements for bot buttons in Menu
    [Header("Menu UI - Bots")]
    public Slider botCountSlider;
    public TextMeshProUGUI botCountText;
    private int botCount = 0;

    private string roomCode;

    void Start()
    {
        createRoomBtn.onClick.AddListener(CreateRoom);
        joinRoomBtn.onClick.AddListener(JoinRoom);
        leaveBtn.onClick.AddListener(LeaveRoom);

        roomCodeInput.characterLimit = 3;
        roomCodeInput.contentType = TMP_InputField.ContentType.Alphanumeric;
        roomCodeInput.onValueChanged.AddListener(delegate {
            roomCodeInput.text = roomCodeInput.text.ToUpper();
        });

        // Setup bot slider
        if (botCountSlider != null)
        {
            botCountSlider.minValue = 0;
            botCountSlider.maxValue = 2;
            botCountSlider.wholeNumbers = true;
            botCountSlider.value = 0;
            botCountSlider.onValueChanged.AddListener(OnBotSliderChanged);
        }

        UpdateBotCountText();

        ShowScreen("connecting");
        connectingText.text = "Connecting to server...";
        PhotonNetwork.ConnectUsingSettings();
    }

    void CreateRoom()
    {
        roomCode = GenerateRoomCode();
        RoomOptions options = new RoomOptions();
        options.MaxPlayers = 3; // Fixed 3 players
        options.IsVisible = false;

        ShowScreen("connecting");
        connectingText.text = "Creating room...";
        PhotonNetwork.CreateRoom(roomCode, options);
    }

    void JoinRoom()
    {
        roomCode = roomCodeInput.text.Trim().ToUpper();
        if (roomCode.Length < 3) return;

        ShowScreen("connecting");
        connectingText.text = "Joining room...";
        PhotonNetwork.JoinRoom(roomCode);
    }

    void LeaveRoom()
    {
        // Reset GameManager state before leaving
        if (GameManager.Instance != null)
        {
            // Clear all card selections
            GameManager.Instance.selectedLeftCard = null;
            GameManager.Instance.selectedRightCard = null;
            GameManager.Instance.selectedLeftCardObject = null;
            GameManager.Instance.selectedRightCardObject = null;

            // Clear player hand
            GameManager.Instance.playerHand.Clear();

            // Clear hand card objects
            foreach (GameObject cardObj in GameManager.Instance.handCardObjects)
            {
                Destroy(cardObj);
            }
            GameManager.Instance.handCardObjects.Clear();

            // Reset phase
            GameManager.Instance.currentPhase = GameManager.GamePhase.Waiting;

            // Hide all played cards
            GameManager.Instance.playerLeftCard.gameObject.SetActive(false);
            GameManager.Instance.playerRightCard.gameObject.SetActive(false);
            GameManager.Instance.leftPlayerLeftCard.gameObject.SetActive(false);
            GameManager.Instance.leftPlayerRightCard.gameObject.SetActive(false);
            GameManager.Instance.rightPlayerLeftCard.gameObject.SetActive(false);
            GameManager.Instance.rightPlayerRightCard.gameObject.SetActive(false);

            // Reset resources
            GameManager.Instance.turnips = 1;
            GameManager.Instance.bank = 1;
            GameManager.Instance.relics = 0;

            // Clear selections
            GameManager.Instance.allPlayerSelections.Clear();
        }

        PhotonNetwork.LeaveRoom();
        ShowScreen("connecting");
        connectingText.text = "Leaving...";
    }



    string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        string code = "";
        for (int i = 0; i < 3; i++)
            code += chars[Random.Range(0, chars.Length)];
        return code;
    }

    void ShowScreen(string screen)
    {
        menuScreen.SetActive(screen == "menu");
        gameScreen.SetActive(screen == "game");
        connectingScreen.SetActive(screen == "connecting");
    }

    // Photon Callbacks
    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Photon!");
        ShowScreen("menu");
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"Joined room: {PhotonNetwork.CurrentRoom.Name}");

        int humansNeeded = 3 - botCount;

        ShowScreen("connecting");
        UpdateWaitingStatus(humansNeeded);

        if (PhotonNetwork.CurrentRoom.PlayerCount >= humansNeeded)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                photonView.RPC("StartGameWithBots", RpcTarget.All, botCount);
            }
        }
    }

    void UpdateWaitingStatus(int humansNeeded)
    {
        int currentPlayers = PhotonNetwork.CurrentRoom.PlayerCount;
        int playersNeeded = humansNeeded - currentPlayers;

        if (playersNeeded > 0)
        {
            connectingText.text = $"Waiting for {playersNeeded} player{(playersNeeded > 1 ? "s" : "")}...\n\nRoom Code: {roomCode}";
        }
        else
        {
            connectingText.text = $"Starting game...\n\nRoom Code: {roomCode}";
        }
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        connectingText.text = "Room not found!";
        Invoke("BackToMenu", 2f);
    }

    void BackToMenu() => ShowScreen("menu");

    public override void OnLeftRoom()
    {
        ShowScreen("menu");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.Log($"Disconnected: {cause}");
        ShowScreen("connecting");
        connectingText.text = "Reconnecting...";
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"Player entered: {newPlayer.NickName}");

        int humansNeeded = 3 - botCount;
        UpdateWaitingStatus(humansNeeded);

        if (PhotonNetwork.CurrentRoom.PlayerCount >= humansNeeded && PhotonNetwork.IsMasterClient)
        {
            Invoke("StartGameDelayed", 1f);
        }
    }

    void StartGameDelayed()
    {
        photonView.RPC("StartGameWithBots", RpcTarget.All, botCount);
    }

    [PunRPC]
    void StartGame()
    {
        ShowScreen("game");
        roomCodeText.text = "Room: " + roomCode;

        if (GameManager.Instance != null)
            GameManager.Instance.InitializeGame();
    }

    [PunRPC]
    void StartGameWithBots(int botCount)
    {
        ShowScreen("game");
        roomCodeText.text = "Room: " + roomCode;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.InitializeGameWithBots(botCount);
        }
    }

    void OnBotSliderChanged(float value)
    {
        botCount = (int)value;
        UpdateBotCountText();
    }

    void UpdateBotCountText()
    {
        if (botCountText != null)
        {
            botCountText.text = $"Number of bots: {botCount}";
        }
    }
}