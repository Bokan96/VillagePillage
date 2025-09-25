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

        roomCodeInput.characterLimit = 5;
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
        ShowScreen("connecting");
        connectingText.text = "Leaving...";
    }

    string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        string code = "";
        for (int i = 0; i < 5; i++)
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
        ShowScreen("game");
        roomCodeText.text = "Room: " + roomCode;

        // Check if we should start with bots
        int botsNeeded = 0;
        if (useBot1) botsNeeded++;
        if (useBot2) botsNeeded++;

        int humansNeeded = 3 - botsNeeded;

        if (PhotonNetwork.CurrentRoom.PlayerCount >= humansNeeded)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                photonView.RPC("StartGameWithBots", RpcTarget.All, botsNeeded);
            }
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

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (PhotonNetwork.CurrentRoom.PlayerCount == 3 && PhotonNetwork.IsMasterClient)
        {
            photonView.RPC("StartGame", RpcTarget.All);
        }
    }

    [PunRPC]
    void StartGame()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.InitializeGame();
    }

    [PunRPC]
    void StartGameWithBots(int botCount)
    {
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