using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;

//     หมวดหมู่             ความหมาย
// 🟢 Host	โค้ดฝั่งคนสร้างห้อง	OnClickHost, OnPlayButtonClicked
// 🔵 Join	โค้ดฝั่งคนเข้าห้อง	OnClickJoin, OnClickJoinConfirm, OnConnectedToRoom
// 🟡 Bot	เพิ่มผู้เล่นจำลอง (AI/Mock)	OnAddMockPlayer
// ⚪ Shared	ใช้ทั้ง 2 ฝั่ง	OnPlayerJoined, UI helper, Sync lobby, Temp message

public class LobbyManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mainPanel;
    public GameObject hostPanel;
    public GameObject joinPanel;

    [Header("Host UI")]
    public Image[] playerIcons;
    public TMP_Text[] playerNameTexts;
    public TMP_Text hostIPText;
    public Button playButton;
    public Button cancelAsHost;  // ปุ่มของฝั่ง Host

    // 🆕 ช่องกรอกจำนวนวันและเวลาต่อวันของเกม (Host ตั้งค่า)
    [Header("Host Game Settings")]
    [Tooltip("จำนวนวันทั้งหมดของเกม (เช่น 5 วัน)")]
    public TMP_InputField totalDaysInput;

    [Tooltip("ความยาว 1 วัน (วินาที) เช่น 60 = 1 นาทีต่อวัน")]
    public TMP_InputField dayDurationInput;

    [Header("Join UI")]
    public TMP_InputField joinIPInput;
    public Button joinButton;
    public GameObject joinErrorPanel;
    public Button leaveRoomButton; // 🆕 ปุ่ม Leave Room // ปุ่มของฝั่ง Join

    [Header("Join UI Keypad")]
    public TMP_Text joinCodeText;   // แสดงเลข IP
    private string joinIP = "";     // เก็บ IP เช่น 192.168.1.10

    public Button[] numberButtons;  // 0–9
    public Button dotButton;        // ปุ่ม .
    public Button clearButton;      // ลบตัวเลขสุดท้าย

    [Header("Lobby Info")]
    public TMP_Text playerCountText;

    [Header("Bot Player")]
    public Button addMockButton;

    private int maxPlayers = 6;
    
    [HideInInspector]
    public  int currentPlayerCount = 0;

    private bool isHost = false;

    // =============================================================
    // ⚪ Shared : เริ่มต้น Lobby
    // =============================================================
    void Start()
    {
        // Subscribe events
        NetworkManager.Instance.OnConnectionResult += OnConnectionResult;
        NetworkManager.Instance.OnPlayerJoined += OnPlayerJoined;
        NetworkManager.Instance.OnLobbyDataReceived += OnLobbyDataReceived;

        ShowMainPanel();
        ClearLobbySlots();

        // Listener ปุ่ม Join
        joinButton.onClick.AddListener(OnClickJoinConfirm);

        leaveRoomButton.onClick.AddListener(OnClickLeaveRoom);

        // Listener ปุ่ม Play และ Bot
        playButton.onClick.AddListener(OnPlayButtonClicked);
        addMockButton.onClick.AddListener(OnAddMockPlayer);

        // เริ่มต้นซ่อนปุ่ม Play
        playButton.gameObject.SetActive(false);

        // Button Cancel as Host
        cancelAsHost.onClick.AddListener(OnClickcancelAsHost);

        // เริ่มต้น: ปิดทั้งสองปุ่มก่อน (ใครจะเห็นต้องเรียก SetRole เมื่อเข้าห้อง)
        leaveRoomButton.gameObject.SetActive(false);
        cancelAsHost.gameObject.SetActive(false);
        // ถ้าต้องการ: ถ้า NetworkManager บอกเป็น server ณ ตอนนี้ ให้ตั้ง role
        if (NetworkManager.Instance != null && NetworkManager.Instance.isServer)
        {
            SetRole(true);
        }
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnDisconnectedFromServer += HandleDisconnectedFromHost;
        }
        
        // bind number buttons
        for (int i = 0; i < numberButtons.Length; i++)
        {
            int index = i; 
            numberButtons[i].onClick.AddListener(() => OnNumberButtonClicked(index));
        }

        // bind clear button
        clearButton.onClick.AddListener(OnClearButtonClicked);
        
        dotButton.onClick.AddListener(OnDotButtonClicked);
        
        if (joinErrorPanel != null)
        {
            joinErrorPanel.SetActive(false);
        }
    }

    public void SetRole(bool host)
    {
        isHost = host;

        if (isHost)
        {
            // 👉 ถ้าเป็น Host
            cancelAsHost.gameObject.SetActive(true);
            leaveRoomButton.gameObject.SetActive(false);
            
            if (totalDaysInput != null) totalDaysInput.gameObject.SetActive(true);
            if (dayDurationInput != null) dayDurationInput.gameObject.SetActive(true);
            
        }
        else
        {
            // 👉 ถ้าเป็น Join
            cancelAsHost.gameObject.SetActive(false);
            leaveRoomButton.gameObject.SetActive(true);
            
            totalDaysInput.gameObject.SetActive(false);
            dayDurationInput.gameObject.SetActive(false);
        }
    }

    // =============================================================
    // ⚪ Shared : เมื่อมีผู้เล่น Join เข้ามา (ทั้ง Host / Client)
    // =============================================================
    public void OnPlayerJoined(string playerName, int spriteIndex)
    {
        Sprite characterSprite = PlayerDataManager.Instance.GetCharacterSprite(spriteIndex);
        if (characterSprite == null)
        {
            Debug.LogError("spriteIndex ไม่ถูกต้อง");
            return;
        }

        playerIcons[currentPlayerCount].sprite = characterSprite;
        playerIcons[currentPlayerCount].gameObject.SetActive(true);
        playerNameTexts[currentPlayerCount].text = playerName;

        currentPlayerCount++;
        UpdatePlayerCountText();
    }

    // =============================================================
    // 🟢 Host : เริ่มสร้างห้อง / เป็น Host
    // =============================================================
    public void OnClickHost()
    {
        mainPanel.SetActive(false);
        hostPanel.SetActive(true);
        joinPanel.SetActive(false);

        hostIPText.text = "Your IP: " + GetLocalIPAddress();

        NetworkManager.Instance.StartServer(7777);
        SetRole(true);
        playButton.gameObject.SetActive(true);

        var pdm = PlayerDataManager.Instance;
        if (pdm == null)
        {
            Debug.LogError("[LobbyManager] PlayerDataManager.Instance is null!");
            return;
        }

        // ✅ สร้าง localPlayer ถ้ายังไม่มี
        if (pdm.localPlayer == null)
        {
            pdm.localPlayer = new PlayerData("PlayerName", 0);
        }

        // ✅ สร้าง currentRoom ใหม่ทุกครั้ง
        if (pdm.currentRoom == null)
        {
            pdm.currentRoom = new RoomData("Room_" + Random.Range(100000, 999999));
            Debug.Log("[LobbyManager] สร้าง currentRoom ใหม่สำหรับ Host");
        }

        // ✅ เคลียร์ List ก่อน
        if (pdm.players == null) pdm.players = new List<PlayerData>();
        else pdm.players.Clear();

        // ✅ เพิ่ม localPlayer
        pdm.AddPlayer(pdm.localPlayer);

        // ✅ แสดง UI
        var sprite = pdm.GetCharacterSprite(pdm.localPlayer.characterSpriteIndex);
        playerIcons[0].sprite = sprite;
        playerIcons[0].gameObject.SetActive(true);
        playerNameTexts[0].text = pdm.localPlayer.playerName;

        currentPlayerCount = 1;
        UpdatePlayerCountText();
    }

    //Button Cancel as Host
    public void OnClickcancelAsHost()
    {
        Debug.Log("Host ยกเลิกห้อง");

        if (NetworkManager.Instance.isServer)
        {
            NetworkManager.Instance.CloseRoom();
        }

        // ✅ Reset UI กลับไป mainPanel
        ShowMainPanel();

        // ✅ ปิดปุ่ม Host
        SetRole(false);

        // ✅ เคลียร์ Slot UI ด้วย
        ClearLobbySlots();

        Debug.Log("[LobbyManager] ยกเลิกห้องสำเร็จ (ไม่ล้าง localPlayer)");
    }

    // =============================================================
    // 🔵 Join : ปุ่มกดเข้าห้อง
    // =============================================================
    public void OnClickJoin()
    {
        mainPanel.SetActive(false);
        hostPanel.SetActive(false);
        joinPanel.SetActive(true);
    }

    // 🔵 Join : ยืนยันการเชื่อมต่อ IP ของ Host
    private void OnClickJoinConfirm()
    {
        EventSystem.current.SetSelectedGameObject(null);

        string ip = joinIP.Trim();
        Debug.Log($"[Join] Trying IP: {ip}");

        if (!IsValidIP(ip))
        {
            StartCoroutine(ShowJoinError("Invalid IP Address"));
            return;
        }

        NetworkManager.Instance.ConnectToServer(ip, 7777);
    }
    
    public void OnClickBackFromJoin()
    {
        mainPanel.SetActive(true);
        hostPanel.SetActive(false);
        joinPanel.SetActive(false);
        
        joinIP = "";
        if (joinCodeText != null)
            joinCodeText.text = "";
    }
    
    private bool IsValidIP(string ip)
    {
        System.Net.IPAddress address;
        return System.Net.IPAddress.TryParse(ip, out address);
    }
    public void OnDotButtonClicked()
    {
        // ไม่ให้ขึ้นจุดเป็นตัวแรก
        if (joinIP.Length == 0) return;

        // ไม่ให้ขึ้น .. ซ้อนกัน
        if (joinIP[joinIP.Length - 1] == '.') return;

        joinIP += ".";
        joinCodeText.text = joinIP;
    }
    
    // กดเลข 0-9
    public void OnNumberButtonClicked(int number)
    {
        if (joinIP.Length >= 20) return;

        joinIP += number.ToString();
        joinCodeText.text = joinIP;
    }

    // ลบเลขสุดท้าย
    public void OnClearButtonClicked()
    {
        // ไม่ให้ขึ้น .. หรือ ขึ้นจุดตัวแรกแบบผิดตำแหน่ง
        if (joinIP.Length == 0) return;

        joinIP = joinIP.Substring(0, joinIP.Length - 1);
        joinCodeText.text = joinIP;
    }

    // 🔵 Join : เมื่อเชื่อมต่อสำเร็จ / ไม่สำเร็จ
    public void OnConnectedToRoom(bool success)
    {
        if (success)
        {
            hostPanel.SetActive(true);
            mainPanel.SetActive(false);
            joinPanel.SetActive(false);

            // เป็น client → แสดงปุ่ม Leave
            SetRole(false);

            // ✅ ขอ sync Lobby data จาก Host
            if (!NetworkManager.Instance.isServer)
            {
                NetworkManager.Instance.SendMessage("RequestLobbyData");
            }

            // ✅ เพิ่ม localPlayer ของตัวเองลง list
            var local = PlayerDataManager.Instance.localPlayer;
            if (local != null && !PlayerDataManager.Instance.players.Exists(p => p.playerName == local.playerName))
            {
                PlayerDataManager.Instance.AddPlayer(local);
            }
        }
        else
        {
            Debug.LogWarning("IP ของ Host ไม่ถูกต้อง หรือ เชื่อมต่อไม่สำเร็จ");
            // ไม่เปลี่ยน panel ให้ยังอยู่ joinPanel เหมือนเดิม
            // แสดงข้อความ Error 2 วินาที
            StartCoroutine(ShowJoinError("Host not found"));
        }
    }
    
    private IEnumerator ShowJoinError(string message)
    {
        if (joinErrorPanel == null)
        {
            Debug.LogError("[LobbyManager] joinErrorPanel หรือ joinErrorText ไม่ได้ถูกผูกใน Inspector!");
            yield break;
        }
        
        //joinErrorText.text = message;
        joinErrorPanel.SetActive(true); 
        yield return new WaitForSeconds(1f);
        joinErrorPanel.SetActive(false); 
    }

    public void OnClickLeaveRoom()
    {
        Debug.Log("Client ออกจากห้อง");

        // ล้างข้อมูลผู้เล่นทั้งหมด
        PlayerDataManager.Instance.players.Clear();
        
        PlayerDataManager.Instance.currentRoom = null;

        if (NetworkManager.Instance.isServer)
        {
            // ถ้าเป็น Host — จะไม่มาทางนี้ (กันไว้)
            NetworkManager.Instance.CloseRoom();
            SetRole(false);
        }
        else
        {
            // แจ้ง Host ว่าผู้เล่นนี้ออกจากห้อง
            var name = PlayerDataManager.Instance.localPlayer.playerName;
            NetworkManager.Instance.SendMessage($"LeaveRoom:{name}");

            // ปิดการเชื่อมต่อ
            NetworkManager.Instance.DisconnectFromServer();

            // กลับหน้า mainPanel
            ShowMainPanel();

            // ล้างรายชื่อผู้เล่นของตัวเอง
            PlayerDataManager.Instance.players.Clear();
            ClearLobbySlots();
        }
        
        // ล้างค่า IP ที่กรอกไว้
        joinIP = "";
        if (joinCodeText != null)
            joinCodeText.text = "";
    }

    // =============================================================
    // ⚪ Shared : Sync Lobby UI หลัง Join
    // =============================================================
    private void OnLobbyDataReceived(string data)
    {
        ClearLobbySlots();
        PlayerDataManager.Instance.players.Clear(); // ล้าง list ก่อน sync ใหม่

        int validCount = 0;

        if (!string.IsNullOrEmpty(data))
        {
            string[] players = data.Split('|');
            for (int i = 0; i < players.Length && i < playerIcons.Length; i++)
            {
                string[] parts = players[i].Split(':');
                if (parts.Length >= 2 && int.TryParse(parts[1], out int spriteIndex))
                {
                    string playerName = parts[0];

                    // ✅ sync ผู้เล่นจาก Host
                    PlayerData player = new PlayerData(playerName, spriteIndex);
                    PlayerDataManager.Instance.AddPlayer(player);

                    // ✅ update UI
                    playerNameTexts[i].text = playerName;
                    playerIcons[i].sprite = PlayerDataManager.Instance.GetCharacterSprite(spriteIndex);
                    playerIcons[i].gameObject.SetActive(true);

                    validCount++;
                }
            }
        }

        currentPlayerCount = validCount;
        UpdatePlayerCountText();

        // ✅ เพิ่ม localPlayer ของตัวเองกลับเข้า list (ถ้ายังไม่มี)
        var local = PlayerDataManager.Instance.localPlayer;
        if (local != null && !PlayerDataManager.Instance.players.Exists(p => p.playerName == local.playerName))
        {
            PlayerDataManager.Instance.AddPlayer(local);

            if (currentPlayerCount < playerIcons.Length)
            {
                playerNameTexts[currentPlayerCount].text = local.playerName;
                playerIcons[currentPlayerCount].sprite = PlayerDataManager.Instance.GetCharacterSprite(local.characterSpriteIndex);
                playerIcons[currentPlayerCount].gameObject.SetActive(true);
                currentPlayerCount++;
                UpdatePlayerCountText();
            }
        }
    }

    private void OnConnectionResult(bool success)
    {
        StartCoroutine(CallOnConnectedToRoom(success));
    }

    private IEnumerator CallOnConnectedToRoom(bool success)
    {
        yield return null;
        OnConnectedToRoom(success);
    }

    // =============================================================
    // ⚪ Shared : Lobby UI Helper
    // =============================================================
    public void ShowMainPanel()
    {
        mainPanel.SetActive(true);
        hostPanel.SetActive(false);
        joinPanel.SetActive(false);

        // ล้างข้อมูลผู้เล่น
        ClearLobbySlots();
        PlayerDataManager.Instance.players.Clear();
    }

    public void UpdatePlayerCountText()
    {
        playerCountText.text = $"Player: {currentPlayerCount} / {maxPlayers}";
    }

    private void ClearLobbySlots()
    {
        for (int i = 0; i < playerIcons.Length; i++)
        {
            playerIcons[i].gameObject.SetActive(false);
            playerNameTexts[i].text = "";
        }
        currentPlayerCount = 0;
        UpdatePlayerCountText();
    }

    private string GetLocalIPAddress()
    {
        foreach (var ip in System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList)
        {
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                string ipString = ip.ToString();
            
                // 💡 กรอง IP ที่มักใช้กับ Hotspot/Virtual Adapter
                // สมมติว่า 192.168.2.x คือ LAN ปกติที่คุณต้องการ
                if (ipString.StartsWith("192.168.137."))
                {
                    Debug.LogWarning($"[IP Filter] Skipping Hotspot IP: {ipString}");
                    continue; // ข้าม IP นี้ ไปดู IP ถัดไป
                }

                // ✅ ถ้า IP ไม่ใช่ IP ที่ถูกกรอง ให้คืนค่าทันที
                return ipString;
            }
        }
        return "127.0.0.1"; // ถ้าหาไม่เจอจริงๆ
    }

    // =============================================================
    // 🟢 Host : กดปุ่มเริ่มเกม
    // =============================================================
    private void OnPlayButtonClicked()
    {
        if (!NetworkManager.Instance.isServer) return;

        // ✅ แก้ไข: ดึงค่า Default จาก NetworkManager โดยตรง (ค่าที่ Set ไว้ใน Inspector)
        int finalTotalDays = NetworkManager.Instance.gameTotalDays;
        float finalDayDuration = NetworkManager.Instance.gameDayDuration;

        // ตรวจสอบ InputField: ถ้า Host กรอกค่าใหม่ ให้ใช้ค่าจาก InputField แทน
        if (totalDaysInput != null && int.TryParse(totalDaysInput.text, out int d) && d > 0)
        {
            finalTotalDays = d;
        }

        if (dayDurationInput != null && float.TryParse(dayDurationInput.text, out float dur) && dur > 0f)
        {
            finalDayDuration = dur;
        }

        NetworkManager.Instance.gameTotalDays = finalTotalDays;
        NetworkManager.Instance.gameDayDuration = finalDayDuration;

        Debug.Log($"[Lobby] Host ตั้งค่าเกม: totalDays={finalTotalDays}, dayDuration={finalDayDuration} วินาที");

        NetworkManager.Instance.SendMessageToAll("StartGame");
        UnityEngine.SceneManagement.SceneManager.LoadScene("Game");
    }

    // =============================================================
    // 🟡 Bot : เพิ่มบอทเข้า Lobby
    // =============================================================
    // Bot Player
    private void OnAddMockPlayer()
    {
        if (currentPlayerCount >= maxPlayers) return;

        string mockName = "Bot_" + Random.Range(1, 999);
        int spriteIndex = Random.Range(0, PlayerDataManager.Instance.characterSprites.Length);

        BotData bot = new BotData(mockName, spriteIndex);
        PlayerDataManager.Instance.AddPlayer(bot);

        OnPlayerJoined(mockName, spriteIndex);
    }
    private void HandleDisconnectedFromHost()
    {
        Debug.Log("💥 หลุดจาก Host แล้ว → กลับหน้าหลัก");

        ShowMainPanel();
        PlayerDataManager.Instance.players.Clear();
        ClearLobbySlots();
        SetRole(false);
    }
    
    public void OnPlayerLeaveRoom(string playerName)
    {
        Debug.Log($"[Host] Player {playerName} กด Leave Room");

        // 1. ลบจาก PlayerDataManager.players
        var pdm = PlayerDataManager.Instance;
        pdm.players.RemoveAll(p => p.playerName == playerName);

        // 2. ลบจาก currentRoom.Players
        if (pdm.currentRoom != null && pdm.currentRoom.Players != null)
        {
            pdm.currentRoom.Players.RemoveAll(p => p.playerName == playerName);
        }

        // 3. ลบจาก UI Lobby
        for (int i = 0; i < playerNameTexts.Length; i++)
        {
            if (playerNameTexts[i].text == playerName)
            {
                playerNameTexts[i].text = "";
                playerIcons[i].gameObject.SetActive(false);
            }
        }

        // 4. อัปเดต player count
        currentPlayerCount = pdm.players.Count;
        UpdatePlayerCountText();
    }

}
