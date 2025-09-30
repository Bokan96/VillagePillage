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

    [Header("Bot Settings")]
    public bool useBot1 = false;
    public bool useBot2 = false;

    // Add UI elements for bot buttons in Menu
    [Header("Menu UI - Bots")]
    public Button addBot1Btn;
    public Button addBot2Btn;
    public TextMeshProUGUI bot1StatusText;
    public TextMeshProUGUI bot2StatusText;

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

        ShowScreen("connecting");
        connectingText.text = "Connecting to server...";
        PhotonNetwork.ConnectUsingSettings();

        if (addBot1Btn != null)
            addBot1Btn.onClick.AddListener(() => ToggleBot(1));
        if (addBot2Btn != null)
            addBot2Btn.onClick.AddListener(() => ToggleBot(2));

        UpdateBotUI();
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
        PhotonNetwork.LeaveRoom();
        PhotonNetwork.Disconnect();
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

        // Calculate how many human players we need
        int botsNeeded = 0;
        if (useBot1) botsNeeded++;
        if (useBot2) botsNeeded++;
        int humansNeeded = 3 - botsNeeded;

        // Show waiting screen with status
        ShowScreen("connecting");
        UpdateWaitingStatus(humansNeeded);

        // Start game if enough players
        if (PhotonNetwork.CurrentRoom.PlayerCount >= humansNeeded)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                photonView.RPC("StartGameWithBots", RpcTarget.All, botsNeeded);
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
        ShowScreen("connecting");
        connectingText.text = "Reconnecting...";
        PhotonNetwork.ConnectUsingSettings();
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

        // Update waiting status
        int botsNeeded = 0;
        if (useBot1) botsNeeded++;
        if (useBot2) botsNeeded++;
        int humansNeeded = 3 - botsNeeded;

        UpdateWaitingStatus(humansNeeded);

        // Start game if enough players
        if (PhotonNetwork.CurrentRoom.PlayerCount >= humansNeeded && PhotonNetwork.IsMasterClient)
        {
            // Small delay before starting
            Invoke("StartGameDelayed", 1f);
        }
    }

    void StartGameDelayed()
    {
        int botsNeeded = 0;
        if (useBot1) botsNeeded++;
        if (useBot2) botsNeeded++;

        photonView.RPC("StartGameWithBots", RpcTarget.All, botsNeeded);
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

    void ToggleBot(int botNumber)
    {
        if (botNumber == 1)
        {
            useBot1 = !useBot1;
        }
        else if (botNumber == 2)
        {
            useBot2 = !useBot2;
            if (useBot2) useBot1 = true; // Force bot 1 if bot 2 is enabled
        }
        UpdateBotUI();
    }

    void UpdateBotUI()
    {
        if (bot1StatusText)
            bot1StatusText.text = useBot1 ? "Bot 1: ON" : "Bot 1: OFF";
        if (bot2StatusText)
            bot2StatusText.text = useBot2 ? "Bot 2: ON" : "Bot 2: OFF";

        if (addBot1Btn)
            addBot1Btn.GetComponentInChildren<TextMeshProUGUI>().text = useBot1 ? "Remove Bot 1" : "Add Bot 1";
        if (addBot2Btn)
            addBot2Btn.GetComponentInChildren<TextMeshProUGUI>().text = useBot2 ? "Remove Bot 2" : "Add Bot 2";
    }
}