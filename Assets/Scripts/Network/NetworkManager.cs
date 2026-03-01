using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using System.Linq;
using System.Globalization;
using UnityEngine.SceneManagement;

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance;
    [HideInInspector] 
    public bool isServer;

    // 🟢 NEW: ค่า Setting เกม ที่ Host ตั้งจากหน้า Lobby
    [Header("Game Settings (from Lobby)")]
    [SerializeField] public int gameTotalDays;         // default เผื่อยังไม่ได้กรอก
    [SerializeField] public float gameDayDuration;   // วินาทีต่อวัน default

    private TcpListener server;
    private TcpClient client;
    
    private readonly List<TcpClient> clients = new List<TcpClient>();
    private TcpClient myClient;
    private NetworkStream stream;

    private readonly Queue<Action> mainThreadActions = new Queue<Action>();
    private readonly HashSet<string> joinedNames = new HashSet<string>();
    private readonly List<PlayerInfo> players = new List<PlayerInfo>();
    private const int maxPlayers = 6;

    public System.Action OnDisconnectedFromServer;
    
    private HashSet<string> playersEndTurn = new HashSet<string>();
    
    #region 📢 EVENT / CALLBACKS
    
    public event Action<bool> OnConnectionResult;
    public event Action<string, int> OnPlayerJoined;
    public event Action<string> OnLobbyDataReceived;
    public event Action<int, float> OnSyncGameTime;
    public event Action OnReceiveDayEnded;

    //private readonly HashSet<string> dayConfirmedPlayers = new HashSet<string>();
    private int currentSyncedDay = 1;
    private float currentSyncedTimer = 0f;

    private class PlayerInfo
    {
        public string name;
        public int spriteIndex;
    }

    // 🌐 EVENT ใหม่
    public Action<int, float> OnStartTimerReceived;
    public event Action OnShowRankingPanelReceived;
    private readonly HashSet<string> timeUpPlayers = new HashSet<string>();
    
    #endregion

   
    #region 🧠 UNITY LIFE CYCLE
   
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        if (!isServer && stream != null && stream.DataAvailable)
        {
            try
            {
                byte[] buffer = new byte[4096];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);

                if (bytesRead == 0)
                {
                    Debug.LogWarning("[Client] Host closed connection.");
                    DisconnectFromServer();
                    OnDisconnectedFromServer?.Invoke();
                }
                else
                {
                    string msg = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    //RunOnMainThread(() => ParseMessage(msg));
                    // ✅ แยกหลายบรรทัด
                    foreach (var line in msg.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        RunOnMainThread(() => ParseMessage(line.Trim()));
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Client] Disconnected: " + e.Message);
                DisconnectFromServer();
                OnDisconnectedFromServer?.Invoke();
            }
        }

        lock (mainThreadActions)
        {
            while (mainThreadActions.Count > 0)
                mainThreadActions.Dequeue().Invoke();
        }

        CheckIncomingDataFromClients();
    }
    
    #endregion

    
    #region 📨 PARSE MESSAGE (CLIENT RECEIVE)
    
    public void ParseMessage(string msg)
    {
        // 💡 NEW LOGIC: รับอันดับจาก Host
        if (msg.StartsWith("RANK_SYNC|"))
        {
            string data = msg.Substring("RANK_SYNC|".Length);
            var playerRanks = new Dictionary<string, int>();

            foreach (var pair in data.Split('|'))
            {
                var parts = pair.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[1], out int rank))
                {
                    playerRanks[parts[0]] = rank;
                }
            }

            // 💡 ส่งข้อมูลนี้ไปให้ TurnManager/DailyReportUI จัดการบน Main Thread
            RunOnMainThread(() =>
            {
                // ใช้วิธีส่งผ่าน TurnManager เพื่อให้ TurnManager อัปเดต UI 
                var tm = FindObjectOfType<TurnManager>();
                tm.ReceiveRanksFromHost(playerRanks);
            });
        }

        // รับผลลัพธ์การซื้อขายจาก Host
        if (msg.StartsWith("BUY_RESULT|") || msg.StartsWith("SELL_RESULT|"))
        {
            var parts = msg.Split('|');
            // ต้องมี 6 ส่วน: COMMAND|PlayerName|Asset|Shares|MoneyChange|Result
            if (parts.Length >= 6)
            {
                bool isBuy = parts[0] == "BUY_RESULT";
                string playerName = parts[1];
                string assetName = parts[2];

                // Shares หลังเทรด (Host ส่งมา)
                if (!float.TryParse(
                        parts[3],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out float sharesAfterTrade))
                {
                    sharesAfterTrade = 0f;
                }

                // parts[4] = มูลค่าดีลที่ Host ส่งมา (อาจมี - นำหน้าในเคส BUY)
                // เราดึง “ตัวเลขบวก” มาเป็นฐาน
                if (!decimal.TryParse(
                        parts[4].Replace("-", ""),
                        NumberStyles.Any,
                        CultureInfo.InvariantCulture,
                        out decimal moneyChangeBase))
                {
                    moneyChangeBase = 0m;
                }

                // ซื้อ = เงินลด, ขาย = เงินเพิ่ม (ตัวนี้เอาไป + กับ player.money)
                decimal moneyChange = isBuy ? -moneyChangeBase : moneyChangeBase;

                // มูลค่าดีลแบบบวกเสมอ (ใช้กับ totalCost / spendBuckets)
                decimal tradeGross = moneyChangeBase;

                string result = parts[5].Trim();
                bool success = result == "SUCCESS";

                // หา Player / Asset ที่เกี่ยวข้อง
                var affectedPlayer = PlayerDataManager.Instance.players
                    .Find(p => p.playerName == playerName);
                var subAsset = FindSubAssetByName(assetName);

                bool isLocalPlayer =
                    PlayerDataManager.Instance.localPlayer != null &&
                    PlayerDataManager.Instance.localPlayer.playerName == playerName;

                if (affectedPlayer != null && subAsset != null)
                {
                    RunOnMainThread(() =>
                    {
                        // ✅ อัปเดตสถานะจริง (เงิน, holding, totalCost, spendBuckets) เฉพาะตอนเทรดสำเร็จ
                        if (success)
                        {
                            affectedPlayer.SyncTradeResult(
                                subAsset,
                                sharesAfterTrade,
                                moneyChange,
                                tradeGross,
                                isBuy
                            );
                        }

                        // ✅ UI popup สำหรับ local player ทั้งสำเร็จ/ไม่สำเร็จ
                        if (isLocalPlayer)
                        {
                            var buyManager = FindObjectOfType<BuyManager>();
                            if (buyManager != null)
                            {
                                float costIncome = (float)moneyChangeBase;
                                buyManager.ReceiveTradeResultFromHost(
                                    success,
                                    isBuy,
                                    assetName,
                                    costIncome
                                );
                            }
                        }
                    });
                }
            }

            // 💡 จบเคสนี้เลย ไม่ต้องปล่อยให้ if อื่นด้านล่างมาจับซ้ำ
            return;
        }


        // ⭐ NEW: Client sync ค่า nextDayPct จาก Host (ใช้ใน Daily Report)
        if (msg.StartsWith("NEXTDAY_SYNC|"))
        {
            string data = msg.Substring("NEXTDAY_SYNC|".Length);

            RunOnMainThread(() =>
            {
                var inv = FindObjectOfType<InvestmentManager>();
                if (inv == null || inv.activeCompanies == null)
                {
                    Debug.LogWarning("[Client] ได้รับ NEXTDAY_SYNC แต่ยังไม่มี InvestmentManager");
                    return;
                }

                foreach (var entry in data.Split('|', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = entry.Split(',');
                    if (parts.Length < 3) continue;

                    string companyName = parts[0];
                    string assetName = parts[1];

                    if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float pct))
                        continue;

                    var rt = inv.activeCompanies.FirstOrDefault(r =>
                        r != null &&
                        r.data != null && r.data.companyName == companyName &&
                        r.subAsset != null && r.subAsset.assetName == assetName);

                    if (rt != null)
                    {
                        // ให้ใช้ OverrideNextDayPct เพื่อให้ hasNextDayPct เป็น true ด้วย
                        rt.OverrideNextDayPct(pct);
                    }
                }

                // ⭐ บอก TurnManager ว่า sync nextDayPct เสร็จแล้ว
                var tm = FindObjectOfType<TurnManager>();
                if (tm != null)
                {
                    tm.MarkNextDayPctSynced();
                }

                Debug.Log("[Client] Sync nextDayPct จาก Host ครบแล้ว");
            });

            return; // เคสนี้จบ แยกจากข้อความอื่น
        }

        // ⭐ NEW: Client sync ราคาจาก Host
        if (msg.StartsWith("PRICE_SYNC|"))
        {
            string data = msg.Substring("PRICE_SYNC|".Length);
            RunOnMainThread(() =>
            {
                var inv = FindObjectOfType<InvestmentManager>();
                if (inv == null || inv.activeCompanies == null)
                {
                    Debug.LogWarning("[Client] ได้รับ PRICE_SYNC แต่ยังไม่มี InvestmentManager");
                    return;
                }

                foreach (var entry in data.Split('|', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = entry.Split(',');
                    if (parts.Length < 5) continue;

                    string companyName = parts[0];
                    string assetName = parts[1];

                    if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float price))
                        continue;
                    if (!float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float dailyPct))
                        dailyPct = 0f;
                    if (!float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float eventPct))
                        eventPct = 0f;

                    var rt = inv.activeCompanies.FirstOrDefault(r =>
                        r != null &&
                        r.data != null && r.data.companyName == companyName &&
                        r.subAsset != null && r.subAsset.assetName == assetName);

                    if (rt != null)
                    {
                        rt.currentPrice = price;
                        rt.lastDailyPct = dailyPct;
                        rt.lastEventPct = eventPct;
                        rt.lastChangePct = dailyPct + eventPct;

                        rt.ui?.UpdatePrice(price, dailyPct, eventPct);
                        // ❌ อย่าไปยุ่ง nextDayPct / hasNextDayPct ตรงนี้
                    }
                }

                Debug.Log("[Client] Sync ราคา/เปอร์เซ็นต์จาก Host เรียบร้อย");
            });

            return; // จบเคสนี้ไม่ต้องไหลลงไปข้างล่าง
        }
        if (msg == "StartGame")
        {
            Debug.Log("[Client] ได้รับ StartGame → เปลี่ยนฉากเป็น Game");
            UnityEngine.SceneManagement.SceneManager.LoadScene("Game");
        }

        if (msg.StartsWith("StartTimer"))
        {
            var parts = msg.Split(':');
            // รูปแบบ: StartTimer:Day:Timer:MarketEventData:PlayerEventData
            if (parts.Length >= 5)
            {
                int day = int.Parse(parts[1]);
                float t = float.Parse(parts[2], CultureInfo.InvariantCulture);
                string marketData = parts[3];
                string playerData = parts[4];

                Debug.Log($"[Client] ได้รับ StartTimer -> day={day}, timer={t}, market={marketData}");

                // ✅ ทุกอย่างทำบน Main Thread (ParseMessage ถูกเรียกบน Main อยู่แล้ว แต่ทำแบบนี้ชัวร์สุด)
                RunOnMainThread(() =>
                {
                    var tm = FindObjectOfType<TurnManager>();
                    if (tm != null)
                    {
                        // 1) sync วัน/เวลา ให้ Client เริ่มวันนี้
                        tm.ReceiveStartTimerFromHost(day, t);

                        // 2) ใช้ marketData + playerData ที่ Host พรีโรลไว้ล่วงหน้า 
                        //    ไปบอก TurnManager ให้ไปหา EventData ตัวจริง แล้วโชว์ Flavor ของ "วันพรุ่งนี้"
                        tm.SyncEventsFromHost(marketData, playerData);
                    }
                    else
                    {
                        Debug.LogWarning("[Client] ได้ StartTimer แล้ว แต่หา TurnManager ไม่เจอ");
                    }
                });
            }

            return;
        }
        if (msg == "ShowRankingPanel")
        {
            Debug.Log("[Client] ได้รับ ShowRankingPanel → เปิด Ranking Panel");
            OnShowRankingPanelReceived?.Invoke();
        }
        if (msg == "ForceLeaveRoom")
        {
            Debug.Log("[Client] ได้รับ ForceLeaveRoom → รีเซ็ตและออกจากเกม");

            // Client ทำ reset
            LocalResetAndReturnToMenu(); 

            return;
        }
        // 📢 NEW: Client รับข้อมูล Net Worth สุดท้ายของทุกคนจาก Host
        else if (msg.StartsWith("FINAL_DATA|"))
        {
            string data = msg.Substring("FINAL_DATA|".Length);
            
            RunOnMainThread(() =>
            {
                var players = PlayerDataManager.Instance.players;
                
                foreach (var pair in data.Split('|', System.StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = pair.Split(':');
                    
                    if (parts.Length == 5 && 
                        decimal.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal netWorth) &&
                        decimal.TryParse(parts[2], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal profit) &&
                        int.TryParse(parts[3], out int spriteIndex))
                    {
                        string playerName = parts[0];
                        string topAssetAbbr = parts[4];
                        
                        var p = players.Find(x => x.playerName == playerName);
                        
                        // ถ้าไม่พบผู้เล่น (Client ยังไม่มีข้อมูล Host หรือเพื่อน) ให้สร้าง
                        if (p == null)
                        {
                            p = new PlayerData(playerName, spriteIndex);
                            PlayerDataManager.Instance.AddPlayer(p); 
                        }
                        
                        // ✅ อัปเดตข้อมูลสรุปที่ Host ส่งมา
                        p.TotalWealth = netWorth; 
                        p.characterSpriteIndex = spriteIndex;
                        p.FinalTopAssetAbbreviation = topAssetAbbr;
                        Debug.Log($"[Client] Synced final data: {playerName} NW={netWorth:N0}");
                    }
                }
            });
        }
        // 📢 NEW: Client รับสัญญาณจบเกม
        else if (msg == "GameOverUI")
        {
            Debug.Log("[Client] ได้รับสัญญาณ GameOverUI → แสดงผลสรุปจบเกม");

            // ต้องทำบน Main Thread เสมอ
            RunOnMainThread(() =>
            {
                var investmentManager = FindAnyObjectByType<InvestmentManager>();
                
                // หา GameOverSummaryUI แล้วโชว์ของ local player
                var finalUI = FindAnyObjectByType<GameOverSummaryUI>();
                var local = PlayerDataManager.Instance.localPlayer ?? PlayerDataManager.Instance.players.FirstOrDefault();

                // ⚠️ ต้องแน่ใจว่า local player และ InvestmentManager พร้อมก่อนคำนวณ
                if (local == null || investmentManager == null)
                {
                    Debug.LogWarning("[Client] ข้อมูลผู้เล่น (Local) หรือ InvestmentManager ไม่พร้อมสำหรับการแสดงผลสรุป");
                    return;
                }

                // ✅ หา Rank ล่าสุด
                // ฝั่ง Client ใช้ข้อมูลผู้เล่นที่ถูกซิงค์มาจาก Host ล่าสุด
                var sorted = PlayerDataManager.Instance.players
                    .OrderByDescending(x => x.GetTotalAssets(investmentManager))
                    .ToList();
                    
                // FindIndex() จะคืนค่า -1 ถ้าหาไม่พบ, เมื่อ +1 จะกลายเป็น 0 
                int finalRank = sorted.IndexOf(local) + 1;
                
                // ถ้า finalRank เป็น 0 แสดงว่าไม่พบ local player ใน list
                if (finalRank == 0) finalRank = -1; 
                
                if (finalUI != null)
                {
                    // เรียก Show เพื่อแสดง Risk Breakdown, Title, Description, และ Rank ของผู้เล่น
                    finalUI.Show(local, investmentManager, finalRank);
                    // วาด Leaderboard
                    finalUI.ShowWithLeaderboard(PlayerDataManager.Instance.players, investmentManager);
                }
                else
                {
                    Debug.LogWarning("[Client] GameOverSummaryUI ไม่พบใน Scene");
                }
            });
        }
        // Client รับข้อความว่า Player คนอื่นกด EndTurn แล้ว (optional)
        if (msg.StartsWith("PlayerEndedTurn:"))
        {
            string playerName = msg.Split(':')[1];
            Debug.Log($"[Client] {playerName} กด EndTurn แล้ว");
            Debug.Log($"[NetworkManager] ได้รับสัญญาณ EndTurn จาก {playerName}. อัปเดต Timer UI เป็น 0");

            // ถ้าอยากอัปเดต UI waiting panel หรือ waiting list สามารถเรียกได้ที่นี่
            var endTurnUI = FindObjectOfType<EndTurnButtonManager>();
            if (endTurnUI != null)
            {
                // ตัวอย่าง: เพิ่มชื่อผู้เล่นลง waiting panel (คุณต้องเขียน method เพิ่มเอง)
                endTurnUI.MarkPlayerWaiting(playerName);
            }
            var turnManager = FindObjectOfType<TurnManager>();
            if (turnManager != null && turnManager.timerText != null)
            {
                // ทำการตั้งค่า UI บน Main Thread
                RunOnMainThread(() =>
                {
                    turnManager.timerText.text = "0";
                });
            }
        }
        // Client รับข้อความว่า Host สั่ง EndDay
        else if (msg == "EndDay")
        {
            Debug.Log("[Client] Host สั่ง EndDay");

            var turnManager = FindObjectOfType<TurnManager>();
            if (turnManager != null)
            {
                // 💡 เพิ่มโค้ด: ตั้งค่า Timer เป็น 0 ก่อนเรียก EndDay()
                if (turnManager.timerText != null)
                {
                    RunOnMainThread(() => turnManager.timerText.text = "0"); 
                    Debug.Log("[Client] ตั้งค่า Timer Text เป็น 0 เมื่อได้รับสัญญาณ EndDay");
                }
                
                turnManager.EndDay();
            }

            var endTurnUI = FindObjectOfType<EndTurnButtonManager>();
            if (endTurnUI != null)
            {
                endTurnUI.waitingPanel.SetActive(false);
                // ✅ reset UI ปุ่มกดสำหรับวันถัดไป
                endTurnUI.ResetForNextDay();
            }
        }
        else if (msg == "NextDay")
        {
            Debug.Log("[Client] ได้รับ NextDay (Ignore) → รอ StartTimer...");
        }
        else if (msg == "HostClosedRoom")
        {
            Debug.Log("[Client] Host ปิดห้อง → กลับหน้า Lobby");
            DisconnectFromServer();

            if (PlayerDataManager.Instance != null)
            {
                var myLocal = PlayerDataManager.Instance.localPlayer;
                PlayerDataManager.Instance.players.Clear();
                if (myLocal != null)
                    PlayerDataManager.Instance.players.Add(myLocal);
                PlayerDataManager.Instance.currentRoom = null;
            }

            var lobby = FindObjectOfType<LobbyManager>();
            if (lobby != null)
                lobby.ShowMainPanel();
        }
        // 📢 NEW: Client รับสัญญาณปิด Waiting Panel
        else if (msg == "CloseWaitingPanel")
        {
            Debug.Log("[Client] ได้รับสัญญาณ CloseWaitingPanel → ปิด Panel รอ");
    
            // ต้องทำบน Main Thread เสมอ
            RunOnMainThread(() =>
            {
                var endTurnUI = FindObjectOfType<EndTurnButtonManager>();
                if (endTurnUI != null && endTurnUI.waitingPanel != null)
                {
                    endTurnUI.waitingPanel.SetActive(false);
                }
            });
        }
        else if (msg.StartsWith("PlayerLeft:"))
        {
            string playerName = msg.Split(':')[1];
            Debug.Log($"[Client] {playerName} ออกจากห้อง (Host แจ้ง)");

            // ลบผู้เล่นจาก PlayerDataManager
            var pdm = PlayerDataManager.Instance;
            if (pdm != null)
            {
                pdm.players.RemoveAll(p => p.playerName == playerName);
            }

            // ลบ UI ของ Lobby
            var lobby = FindObjectOfType<LobbyManager>();
            if (lobby != null)
            {
                for (int i = 0; i < lobby.playerNameTexts.Length; i++)
                {
                    if (lobby.playerNameTexts[i].text == playerName)
                    {
                        lobby.playerNameTexts[i].text = "";
                        lobby.playerIcons[i].gameObject.SetActive(false);
                    }
                }

                lobby.currentPlayerCount = pdm?.players.Count ?? 0;
                lobby.UpdatePlayerCountText();
            }
        }
        else
        {
            //OnLobbyDataReceived?.Invoke(msg);
            
            var lobby = FindObjectOfType<LobbyManager>();
            if (lobby != null)
            {
                OnLobbyDataReceived?.Invoke(msg);
            }
            else
            {
                // ถ้าอยู่ในฉาก Game และได้รับข้อความที่ไม่รู้จัก/Lobby Data ให้ละเลย
                Debug.LogWarning($"[Client] Received unknown/lobby message '{msg}' but LobbyManager is not active. Ignoring.");
            }
        }
    }

    #endregion

    #region 🧑 HOST (SERVER FUNCTIONS)

    public void StartServer(int port)
    {
        isServer = true;
        server = new TcpListener(IPAddress.Any, port);
        server.Start();
        server.BeginAcceptTcpClient(OnClientConnected, null);

        var local = PlayerDataManager.Instance.localPlayer;
        if (!joinedNames.Contains(local.playerName))
        {
            joinedNames.Add(local.playerName);
            players.Add(new PlayerInfo { name = local.playerName, spriteIndex = local.characterSpriteIndex });
            PlayerDataManager.Instance.AddPlayer(local);
        }

        Debug.Log("Server started.");
    }
    
    public void StopServer()
    {
        if (server != null)
        {
            server.Stop();   // ปิด TcpListener
            server = null;
        }
        // ปิด client ทั้งหมด
        foreach (var c in clients)
        {
            c.Close();
        }
        clients.Clear();
        
        /*// ปิด client ทั้งหมด
        if (clients != null)
        {
            foreach (var c in clients)
            {
                try
                {
                    c.Close();
                }
                catch { }
            }
            clients.Clear();
            Debug.Log("[NetworkManager] Client ทั้งหมดถูกปิด");
        }
        */

        Debug.Log("[NetworkManager] Server stopped");
    }
    public void DisconnectClient()
    {
        if (myClient != null)
        {
            myClient.Close();
            myClient = null;
            Debug.Log("[NetworkManager] Client disconnected");
        }
        /*if (myClient != null)
        {
            try
            {
                myClient.Close();
            }
            catch { }
            myClient = null;
            Debug.Log("[NetworkManager] Client ของตัวเองถูกตัดการเชื่อมต่อ");
        }*/
    }
    private void OnClientConnected(IAsyncResult ar)
    {
        if (server == null) return;

        TcpClient newClient = null;
        try
        {
            newClient = server.EndAcceptTcpClient(ar);
            if (newClient == null) return;

            clients.Add(newClient);
            NetworkStream newStream = newClient.GetStream();

            byte[] playerBuffer = new byte[1024];
            int bytesRead = newStream.Read(playerBuffer, 0, playerBuffer.Length);
            if (bytesRead <= 0)
            {
                clients.Remove(newClient);
                newClient.Close();
                return;
            }

            string playerData = Encoding.UTF8.GetString(playerBuffer, 0, bytesRead);
            var parts = playerData.Split(':');

            if (parts.Length >= 3 && parts[0] == "PlayerData")
            {
                string playerName = parts[1];
                if (!int.TryParse(parts[2], out int spriteIndex))
                    spriteIndex = 0;

                if (!joinedNames.Contains(playerName))
                {
                    joinedNames.Add(playerName);
                    players.Add(new PlayerInfo { name = playerName, spriteIndex = spriteIndex });

                    RunOnMainThread(() =>
                    {
                        var newPlayer = new PlayerData(playerName, spriteIndex);
                        PlayerDataManager.Instance.AddPlayer(newPlayer);
                        OnPlayerJoined?.Invoke(playerName, spriteIndex);
                    });

                    BroadcastLobbyData();

                    if (currentSyncedTimer > 0)
                    {
                        string syncMsg = $"StartTimer:{currentSyncedDay}:{currentSyncedTimer}";
                        byte[] syncData = Encoding.UTF8.GetBytes(syncMsg);
                        try
                        {
                            NetworkStream s = newClient.GetStream();
                            s.Write(syncData, 0, syncData.Length);
                            Debug.Log($"[Server] ส่งข้อมูล Sync วันล่าสุดให้ Client -> day={currentSyncedDay}, timer={currentSyncedTimer}");
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[Server] ส่งข้อมูล sync ให้ client ล้มเหลว: {ex.Message}");
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("OnClientConnected Exception: " + e.Message);
            if (newClient != null)
            {
                clients.Remove(newClient);
                try { newClient.Close(); } catch { }
            }
        }
        finally
        {
            if (server != null)
                server.BeginAcceptTcpClient(OnClientConnected, null);
        }
    }

    private void BroadcastLobbyData()
    {
        string lobbyData = BuildLobbyData();
        byte[] data = Encoding.UTF8.GetBytes(lobbyData);

        foreach (var client in clients)
        {
            try
            {
                NetworkStream s = client.GetStream();
                s.Write(data, 0, data.Length);
            }
            catch { }
        }
    }

    private string BuildLobbyData()
    {
        StringBuilder sb = new StringBuilder();
        foreach (var p in players)
            sb.Append($"{p.name}:{p.spriteIndex}|");
        return sb.ToString().TrimEnd('|');
    }

    private void CheckIncomingDataFromClients()
    {
        if (!isServer || clients.Count == 0) return;

        List<TcpClient> disconnectedClients = new List<TcpClient>();

        // ใช้ for loop แทน foreach เพื่อความปลอดภัยในการแก้ไขลิสต์
        for (int i = 0; i < clients.Count; i++)
        {
            var client = clients[i];
            try
            {
                NetworkStream clientStream = client.GetStream();
                if (clientStream.DataAvailable)
                {
                    byte[] buffer = new byte[1024];
                    int bytesRead = clientStream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                    {
                        disconnectedClients.Add(client);
                        continue;
                    }

                    string msg = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    HandleReceivedMessage(msg, client, disconnectedClients); // ส่งลิสต์ disconnectedClients ไปด้วย
                }
            }
            catch
            {
                disconnectedClients.Add(client);
            }
        }

        // ลบ client ที่ออกจากลิสต์หลังจากวนเสร็จ
        foreach (var dc in disconnectedClients)
        {
            if (clients.Contains(dc))
            {
                clients.Remove(dc);
                dc.Close();
            }
        }
        
        foreach (var client in clients)
        {
            try
            {
                NetworkStream clientStream = client.GetStream();
                if (clientStream.DataAvailable)
                {
                    byte[] buffer = new byte[1024];
                    int bytesRead = clientStream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                    {
                        disconnectedClients.Add(client);
                        continue;
                    }

                    string msg = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    HandleReceivedMessage(msg, client, disconnectedClients);
                }
            }
            catch
            {
                disconnectedClients.Add(client);
            }
        }

        foreach (var dc in disconnectedClients)
        {
            clients.Remove(dc);
            dc.Close();
        }
    }

    private void HandleReceivedMessage(string msg, TcpClient client, List<TcpClient> disconnectedClients) //private void HandleReceivedMessage(string msg, TcpClient client)
    {
        if (msg.StartsWith("PlayerData:"))
        {
            var parts = msg.Split(':');
            if (parts.Length >= 3)
            {
                string name = parts[1];
                int spriteIndex = int.Parse(parts[2]);

                if (!joinedNames.Contains(name))
                {
                    joinedNames.Add(name);
                    players.Add(new PlayerInfo { name = name, spriteIndex = spriteIndex });
                    RunOnMainThread(() =>
                    {
                        var newPlayer = new PlayerData(name, spriteIndex);
                        PlayerDataManager.Instance.AddPlayer(newPlayer);
                        OnPlayerJoined?.Invoke(name, spriteIndex);
                    });
                    BroadcastLobbyData();
                }
            }
        }
        if (msg == "RequestForceLeaveRoom")
        {
            Debug.Log("[Host] รับ RequestForceLeaveRoom จาก Client → Broadcast ForceLeaveRoom");

            // 1. ส่ง ForceLeaveRoom ไป Client ทุกคน
            SendMessageToAllExceptHost("ForceLeaveRoom");

            // 2. Host รีเซ็ตตัวเอง
            LocalResetAndReturnToMenu(); 
            return;
        }
        if (msg == "ForceLeaveRoom")
        {
            Debug.Log("[Client] ได้รับ ForceLeaveRoom → รีเซ็ตและออกจากเกม");

            // Client ทำ reset
            LocalResetAndReturnToMenu();

            return;
        }
        if (msg == "ResetGame")
        {
            Debug.Log("[Client] ได้รับ ResetGame → รีเซ็ตข้อมูลและกลับเมนู");
            LocalResetAndReturnToMenu();
            return;
        }
        if (msg.StartsWith("LeaveRoom:"))
        {
            string playerName = msg.Split(':')[1];
            Debug.Log($"[Server] {playerName} ออกจากห้อง");

            // ลบผู้เล่นออกจาก list ของ server
            players.RemoveAll(p => p.name == playerName);
            joinedNames.Remove(playerName);

            // ลบจาก PlayerDataManager ของ Host
            var pdm = PlayerDataManager.Instance;
            if (pdm != null)
            {
                // ลบจาก list รวม
                pdm.players.RemoveAll(p => p.playerName == playerName);

                // ลบจาก currentRoom
                if (pdm.currentRoom != null && pdm.currentRoom.Players != null)
                {
                    pdm.currentRoom.Players.RemoveAll(p => p.playerName == playerName);
                }

                // ลบจาก Lobby UI
                var lobby = FindObjectOfType<LobbyManager>();
                if (lobby != null)
                {
                    for (int i = 0; i < lobby.playerNameTexts.Length; i++)
                    {
                        if (lobby.playerNameTexts[i].text == playerName)
                        {
                            lobby.playerNameTexts[i].text = "";
                            lobby.playerIcons[i].gameObject.SetActive(false);
                        }
                    }

                    // อัปเดตจำนวนผู้เล่น
                    lobby.currentPlayerCount = pdm.players.Count;
                    lobby.UpdatePlayerCountText();
                }
            }

            // **ห้ามลบ client ตรง ๆ ในลูปหลัก**
            if (!disconnectedClients.Contains(client))
                disconnectedClients.Add(client);

            BroadcastLobbyData();
        }
        else if (msg.StartsWith("EndTurn:"))
        {
            string playerName = msg.Split(':')[1];
            Debug.Log($"📩 Client {playerName} แจ้งว่า End Turn แล้ว");
            MarkPlayerEndTurn(playerName); //return;
        }
        if (msg.StartsWith("TimeUp:"))
        {
            string playerName = msg.Split(':')[1];
            if (!timeUpPlayers.Contains(playerName))
            {
                timeUpPlayers.Add(playerName);
                Debug.Log($"[Server] {playerName} หมดเวลาแล้ว ({timeUpPlayers.Count}/{players.Count})");
            }
            if (timeUpPlayers.Count >= players.Count)
            {
                Debug.Log("[Server] 🏁 ผู้เล่นทุกคนหมดเวลาแล้ว → สั่ง EndDay เหมือนกรณี EndTurn");
                EndDayHostSide();          // Host ทำ EndDay + BroadcastNextDayPercents + BroadcastRanks
                SendMessageToAll("EndDay"); // ให้ client เรียก EndDay ตาม
            }
        }
        else if (msg.StartsWith("DayConfirm:"))
        {
            string playerName = msg.Split(':')[1];
            
            //ConfirmDayFromPlayer(playerName);
            
            // ✅ แทนที่ด้วย: ให้ TurnManager เป็นคนจัดการ (ผ่าน main thread)
            RunOnMainThread(() => { 
                var tm = FindObjectOfType<TurnManager>();
                if (tm != null)
                {
                    // 💡 ต้องแน่ใจว่าได้เพิ่ม public void MarkPlayerConfirmed(string playerName) ใน TurnManager แล้ว
                    tm.MarkPlayerConfirmed(playerName); 
                }
            });
        }
        else if (msg.StartsWith("BUY|"))
        {
            string[] parts = msg.Split('|');
            if (parts.Length >= 4)
            {
                string playerName = parts[1];
                string assetName = parts[2];
                int amount = int.Parse(parts[3]);
                var subAsset = FindSubAssetByName(assetName);
                if (subAsset != null)
                {
                    var inv = FindObjectOfType<InvestmentManager>();
                    inv.ExecuteBuy(subAsset, amount, playerName);
                }
            }
        }
        else if (msg.StartsWith("SELL|"))
        {
            string[] parts = msg.Split('|');
            // Format: SELL|playerName|assetName|amount (fixed int)
            if (parts.Length >= 4)
            {
                string playerName = parts[1];
                string assetName = parts[2];
                
                if (!int.TryParse(parts[3], out int amount))
                {
                    Debug.LogError($"[Server] Error parsing SELL amount for {playerName}: {parts[3]}");
                    return;
                }

                var subAsset = FindSubAssetByName(assetName);
                if (subAsset != null)
                {
                    var inv = FindObjectOfType<InvestmentManager>();
                    if (inv != null)
                    {
                        // ✅ เรียก Host Logic: ExecuteSell
                        inv.ExecuteSell(subAsset, amount, playerName);
                        Debug.Log($"[Server] ประมวลผลคำสั่ง SELL จาก {playerName} สำหรับ {assetName} x{amount}");
                    }
                    else
                    {
                        Debug.LogError("[Server] InvestmentManager ไม่พบ");
                    }
                }
                else
                {
                    Debug.LogError($"[Server] ไม่พบ SubAssetByName: {assetName}");
                }
            }
        }
    }

    public void SendMessageToAll(string msg)
    {
        if (!isServer) return;

        byte[] data = Encoding.UTF8.GetBytes(msg + "\n"); //byte[] data = Encoding.UTF8.GetBytes(msg);
        foreach (var client in clients)
        {
            try
            {
                NetworkStream s = client.GetStream();
                s.Write(data, 0, data.Length);
            }
            catch (Exception e)
            {
                Debug.LogWarning("SendMessageToAll failed: " + e.Message);
            }
        }
    }
    public void CloseRoom()
    {
        Debug.Log("[CloseRoom] เคลียร์ PlayerDataManager แล้ว (คง localPlayer ไว้)");
        
        if (!isServer) return;

        Debug.Log("[Server] ห้องถูกปิดแล้ว");
        SendMessageToAll("HostClosedRoom");

        foreach (var client in clients)
            client.Close();
        clients.Clear();

        server?.Stop();
        server = null;

        /*if (PlayerDataManager.Instance != null)
        {
            var myLocal = PlayerDataManager.Instance.localPlayer;
            PlayerDataManager.Instance.players.Clear();
            if (myLocal != null)
            {
                PlayerDataManager.Instance.players.Add(myLocal);
            }
            PlayerDataManager.Instance.currentRoom = null;
            Debug.Log("[CloseRoom] เคลียร์ PlayerDataManager แล้ว (คง localPlayer ไว้)");
        }*/
        // เคลียร์ PlayerDataManager อย่างปลอดภัย
        var pdm = PlayerDataManager.Instance;
        if (pdm != null)
        {
            var myLocal = pdm.localPlayer;

            // ✅ เช็ค players ก่อน
            if (pdm.players == null)
            {
                pdm.players = new List<PlayerData>();
            }
            else
            {
                pdm.players.Clear();
            }

            // ✅ เพิ่ม localPlayer กลับถ้ามี
            if (myLocal != null)
            {
                pdm.players.Add(myLocal);
            }

            // ✅ รีเซ็ต currentRoom
            pdm.currentRoom = null;

            Debug.Log("[CloseRoom] เคลียร์ PlayerDataManager แล้ว (คง localPlayer ไว้)");
        }

        var lobby = FindObjectOfType<LobbyManager>();
        if (lobby != null)
        {
            lobby.ShowMainPanel();
        }

        isServer = false;
    }

    public void BroadcastStartTimer(int day, float timer)
    {
        Debug.Log($"[Server] ส่ง StartTimer -> Client ทุกคน | day={day}, timer={timer}");
        timeUpPlayers.Clear();
        currentSyncedDay = day;
        currentSyncedTimer = timer;

        var tm = FindObjectOfType<TurnManager>();
        string marketEventData = (tm != null) ? tm.GetMarketEventSyncData() : "MARKET_NONE";
        string playerEventData = (tm != null) ? tm.GetPlayerEventSyncData() : "PLAYER_NONE";

        string msg = $"StartTimer:{day}:{timer}:{marketEventData}:{playerEventData}";
        SendMessageToAll(msg);
        OnStartTimerReceived?.Invoke(day, timer);

        // ⭐ NEW: ส่ง snapshot ราคาหุ้นของวันนั้นต่อเลย
        var inv = FindObjectOfType<InvestmentManager>();
        BroadcastPriceSnapshot(inv);
    }


    // ฟังก์ชันแปลง Room Code → IP
    public void ConnectToServerWithCode(string roomCode, int port)
    {
        // 1. ถอดรหัส Room Code เป็น IP
        string hostIP = DecodeRoomCodeToIP(roomCode);

        if (string.IsNullOrEmpty(hostIP))
        {
            RunOnMainThread(() => OnConnectionResult?.Invoke(false)); 
            return;
        }

        // 2. ข้าม Dictionary และเชื่อมต่อ IP ที่ถอดรหัสได้โดยตรง
        Debug.Log($"[Client DEBUG] Room Code '{roomCode}' decoded to IP: {hostIP}. Attempting direct connect...");
    
        // ✅ เรียก ConnectToServer ตรง ๆ
        ConnectToServer(hostIP, port);
        
    }
    
    #endregion

    
    #region 👥 CLIENT / JOIN FUNCTIONS
    
    public void ConnectToServer(string ip, int port)
    {
        isServer = false;
        myClient = new TcpClient();
        myClient.BeginConnect(ip, port, OnConnected, null);
    }

    private void OnConnected(IAsyncResult ar)
    {
        try
        {
            myClient.EndConnect(ar);
            stream = myClient.GetStream();
            SendPlayerData();
            RunOnMainThread(() => OnConnectionResult?.Invoke(true));
        }
        catch
        {
            RunOnMainThread(() => OnConnectionResult?.Invoke(false));
        }
    }

    public void SendPlayerData()
    {
        if (stream == null) return;
        var local = PlayerDataManager.Instance.localPlayer;
        string msg = $"PlayerData:{local.playerName}:{local.characterSpriteIndex}";
        byte[] data = Encoding.UTF8.GetBytes(msg);
        stream.Write(data, 0, data.Length);
    }

    public void SendMessage(string msg)
    {
        if (stream == null) return;
        byte[] data = Encoding.UTF8.GetBytes(msg);
        stream.Write(data, 0, data.Length);
    }

    public void NotifyTimeUp()
    {
        var local = PlayerDataManager.Instance.localPlayer;
        string msg = $"TimeUp:{local.playerName}";
        SendMessage(msg);
    }

    public void DisconnectFromServer()
    {
        if (myClient != null)
        {
            myClient.Close();
            myClient = null;
            Debug.Log("❌ Disconnected from server.");
        }

        if (stream != null)
        {
            try { stream.Close(); } catch { }
            stream = null;
        }

        Debug.Log("[Client] ออกจากห้องเรียบร้อย");
    }
    
   
    /// ถอดรหัส 6 หลัก (BBBCDD) กลับเป็น IP 10.B.C.53 
    private string DecodeRoomCodeToIP(string roomCode)
    {
        if (!int.TryParse(roomCode, out int code))
        {
            Debug.LogError("Room Code ไม่ใช่ตัวเลข 6 หลัก");
            return null;
        }
    
        // Octet C คือ 3 หลักสุดท้าย (DD.D)
        int c = code % 1000; 
    
        // Octet B คือ 3 หลักแรก (BBB)
        int b = code / 1000; 

        // 💡 เนื่องจาก IP ของคุณคือ 10.x.x.53
        // เราจึง hardcode 10 และ 53 เข้าไป
        return $"10.{b}.{c}.53"; 
    }
    
    #endregion
    
    
    #region 🧰 UTILITY FUNCTIONS
    
    public void RunOnMainThread(Action action)
    {
        lock (mainThreadActions)
            mainThreadActions.Enqueue(action);
    }

    public void OnMessageReceived(string message)
    {
        if (message == "StartGame")
            UnityEngine.SceneManagement.SceneManager.LoadScene("Game");
    }
    
    public void CloseServerAndClients()
    {
        if (isServer)
        {
            StopServer();
        }
        else
        {
            DisconnectClient();
        }
    }

    private SubAssetData FindSubAssetByName(string name)
    {
        var inv = FindObjectOfType<InvestmentManager>();
        foreach (var rt in inv.activeCompanies)
            if (rt.subAsset.assetName == name)
                return rt.subAsset;
        return null;
    }

    public void Broadcast(string message)
    {
        foreach (var client in clients)
            SendToClient(client, message);
    }

    public bool SendToServer(string message)//public void SendToServer(string message)
    {
        if (myClient != null && myClient.Connected)
        {
            var stream = myClient.GetStream();
            byte[] data = Encoding.UTF8.GetBytes(message + "\n");
            stream.Write(data, 0, data.Length);
        }
        else
        {
            Debug.LogWarning("SendToServer: client ยังไม่เชื่อมต่อ");
        }
        return true;
    }

    public void SendToClient(string playerName, string message)
    {
        var client = clients.Find(c => c.Client.RemoteEndPoint.ToString() == playerName);
        if (client != null)
            SendToClient(client, message);
    }

    private void SendToClient(TcpClient client, string message)
    {
        if (client != null && client.Connected)
        {
            var stream = client.GetStream();
            var data = Encoding.UTF8.GetBytes(message + "\n");
            stream.Write(data, 0, data.Length);
        }
    }
    
    #endregion
    
    // 💡 เมธอดใหม่: Host ใช้คำนวณและส่งอันดับไปให้ Client ทุกคน
    public void BroadcastRanks(InvestmentManager inv)
    {
        if (!isServer) return;

        var rankData = PlayerDataManager.Instance.CalculateRanks(inv);

        if (rankData.Count == 0) return;

        // สร้างข้อความ RANK_SYNC
        StringBuilder sb = new StringBuilder("RANK_SYNC|");
        foreach (var pair in rankData)
        {
            sb.Append($"{pair.Key}:{pair.Value}|");
        }

        string msg = sb.ToString().TrimEnd('|');
        SendMessageToAll(msg);
        // ✅ Host ก็ต้อง Parse ข้อความตัวเองด้วย (แต่ในกรณีนี้เราจะให้ TurnManager จัดการ)
    }

    // Host side: mark player that ended turn
    public void MarkPlayerEndTurn(string playerName)
    {
        if (!playersEndTurn.Contains(playerName))
        {
            playersEndTurn.Add(playerName);
            Debug.Log($"[Host] {playerName} กด EndTurn แล้ว ({playersEndTurn.Count}/{players.Count})");

            // ส่งไปทุกคนว่าใครกดแล้ว (optional)
            SendMessageToAll($"PlayerEndedTurn:{playerName}");

            if (playersEndTurn.Count >= players.Count)
            {
                Debug.Log("[Host] ทุกคนกด EndTurn → เริ่มกระบวนการ EndDay (Host ก่อน แล้วค่อยสั่ง Client)");

                // 1) Host ทำ EndDay ก่อน (ข้างในจะมี PreRoll + BroadcastNextDayPercents)
                EndDayHostSide();

                // 2) ค่อยบอก Client ให้เรียก EndDay (ตอนนี้ NEXTDAY_SYNC น่าจะถึงแล้ว)
                SendMessageToAll("EndDay");

                playersEndTurn.Clear();
            }
        }
    }


    // ตัว Host เองเรียก EndDay logic
    private void EndDayHostSide()
    {
        // 💡 แก้ไข: ทำให้ Host ทำงานบน Main Thread เสมอ เมื่อเริ่มกระบวนการ EndDay
        RunOnMainThread(() =>
        {
            var turnManager = FindObjectOfType<TurnManager>();
            if (turnManager != null)
            {
                // 1. ตั้งค่า Timer Text เป็น 0 ก่อน (สำคัญมากสำหรับ Host)
                if (turnManager.timerText != null)
                {
                    turnManager.timerText.text = "0"; 
                    Debug.Log("[Host] UI Fix: ตั้งค่า Timer Text เป็น 0 ใน EndDayHostSide");
                }
            
                // 2. เรียก EndDay
                turnManager.EndDay();
            }
        });
    }
   // ใน NetworkManager.cs (Host Side)
public void BroadcastFinalPlayerData(InvestmentManager inv)
{
    if (!isServer) return;

    if (PlayerDataManager.Instance.players == null || !PlayerDataManager.Instance.players.Any()) return;

    // 1. Host สร้างโครงสร้างข้อมูลสรุป (Rows) เหมือนกับที่ UI ทำ
    var rows = new List<GameOverSummaryUI.Row>();

    // 2. วนลูปคำนวณและเก็บข้อมูลของทุกคน
    foreach (var p in PlayerDataManager.Instance.players)
    {
        // 💡 เราต้องใช้ GetNetWorth() ของ Host เพื่อให้ได้ค่าที่แน่นอน
        var nw = GameOverSummaryUI.GetNetWorth(p, inv); // เงินสด + มูลค่าถือครองปัจจุบัน
        var profit = nw - p.startingCapital;            // กำไรสุทธิ
        var (topSub, topPnl) = GameOverSummaryUI.GetTopEarningSub(p, inv); // Top Earning Asset

        // อัปเดต TotalWealth ใน PlayerData ของ Host ด้วย (เผื่อไว้)
        p.TotalWealth = nw; 

        rows.Add(new GameOverSummaryUI.Row
        {
            player = p,
            displayName = p.playerName,
            netWorth = nw,
            profit = profit,
            // ไม่สามารถส่ง Object SubAssetData ผ่าน Network ได้ง่ายๆ
            // เราจะส่งค่าที่เป็น Primitive (string/decimal) แทน
            topSubAsset = topSub, 
            topSubPnl = topPnl 
        });
    }


    // 3. จัดเรียงตามเกณฑ์เดียวกับ UI เพื่อให้ Host และ Client มีลำดับที่ตรงกัน
    rows.Sort((a, b) =>
    {
        // เรียงตาม Net Worth ก่อน
        int cmp = b.netWorth.CompareTo(a.netWorth); 
        if (cmp != 0) return cmp;
        // เรียงตาม Profit เป็นเกณฑ์รอง
        cmp = b.profit.CompareTo(a.profit);
        if (cmp != 0) return cmp;
        // เรียงตามชื่อเป็นเกณฑ์สุดท้าย
        return string.Compare(a.displayName, b.displayName, StringComparison.Ordinal);
    });

    // 4. สร้างข้อความส่งออก
    StringBuilder sb = new StringBuilder("FINAL_DATA|");
    foreach (var r in rows)
    {
        string assetAbbr = r.topSubAsset != null ? r.topSubAsset.assetNameAbbreviation : "-";
        
        // รูปแบบ: Rank|PlayerName:NetWorth:Profit:SpriteIndex:TopAssetAbbr|
        sb.Append($"{r.displayName}:{r.netWorth.ToString(System.Globalization.CultureInfo.InvariantCulture)}:" +
                  $"{r.profit.ToString(System.Globalization.CultureInfo.InvariantCulture)}:" +
                  $"{r.player.characterSpriteIndex}:" +
                  $"{assetAbbr}|");
    }

    string msg = sb.ToString().TrimEnd('|');
    SendMessageToAll(msg);
    Debug.Log("[Host] Broadcasted final data summary to all clients.");
}
    public void BroadcastNextDayPercents(InvestmentManager inv)
    {
        if (!isServer) return;
        if (inv == null || inv.activeCompanies == null) return;

        var sb = new StringBuilder("NEXTDAY_SYNC|");

        foreach (var rt in inv.activeCompanies)
        {
            if (rt == null || rt.data == null || rt.subAsset == null) continue;

            // ใช้ PeekNextDayPct() หรือ rt.nextDayPct ตรง ๆ ก็ได้
            float pct = rt.PeekNextDayPct();

            sb.Append(rt.data.companyName).Append(',')
              .Append(rt.subAsset.assetName).Append(',')
              .Append(pct.ToString(CultureInfo.InvariantCulture)).Append('|');
        }

        string msg = sb.ToString().TrimEnd('|');
        SendMessageToAll(msg);
        Debug.Log("[Host] Broadcast NEXTDAY_SYNC (nextDayPct) ให้ทุก Client แล้ว");
    }
    public void BroadcastPriceSnapshot(InvestmentManager inv)
    {
        if (!isServer) return;
        if (inv == null || inv.activeCompanies == null) return;

        var sb = new StringBuilder("PRICE_SYNC|");

        foreach (var rt in inv.activeCompanies)
        {
            if (rt == null || rt.data == null || rt.subAsset == null) continue;

            sb.Append(rt.data.companyName).Append(',')
              .Append(rt.subAsset.assetName).Append(',')
              .Append(rt.currentPrice.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(rt.lastDailyPct.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(rt.lastEventPct.ToString(CultureInfo.InvariantCulture)).Append('|');
        }

        string msg = sb.ToString().TrimEnd('|');
        SendMessageToAll(msg);
        Debug.Log("[Host] Broadcast PRICE_SYNC snapshot ให้ทุก Client แล้ว");
    }
    
    // 💡 NEW: สั่งให้ทุกคนปิด Waiting Panel
    public void BroadcastWaitingPanelOff()
    {
        // Host สั่งให้ Client ทุกคนปิด
        SendMessageToAll("CloseWaitingPanel");
    
        // Host ปิดของตัวเองด้วย
        var endTurnUI = FindObjectOfType<EndTurnButtonManager>();
        if (endTurnUI != null && endTurnUI.waitingPanel != null)
        {
            endTurnUI.waitingPanel.SetActive(false);
        }
    }
    /*public void BroadcastForceLeaveRoom()
    {
        Debug.Log("[NetworkManager] BroadcastForceLeaveRoom() ถูกเรียก");

        // 🟢 โหมด Host
        if (isServer)
        {
            // ส่ง ForceLeaveRoom ให้ Client ทุกคน
            SendMessageToAllExceptHost("ForceLeaveRoom");

            Debug.Log("[Host] ส่ง ForceLeaveRoom ไป Client ทุกคนแล้ว → Host เตรียมออก");

            // Host reset ตัวเอง (หลังจากส่งให้ Client แล้ว)
            LocalResetAndReturnToMenu();
            return;
        }

        // 🔵 โหมด Client กดปุ่มเอง
        // ส่งไปบอก Host ให้จัดการ
        Debug.Log("[Client] ส่ง RequestForceLeaveRoom ไป Host");
        SendMessage("RequestForceLeaveRoom");
    }*/
    public void BroadcastForceLeaveRoom()
    {
        Debug.Log("[NetworkManager] BroadcastForceLeaveRoom() ถูกเรียก");

        // 🟢 โหมด Host
        if (isServer)
        {
            // 1. ส่ง ForceLeaveRoom ให้ Client ทุกคน
            SendMessageToAllExceptHost("ForceLeaveRoom"); 
            Debug.Log("[Host] ส่ง ForceLeaveRoom ไป Client ทุกคนแล้ว → Host เตรียมออก");
        
            // 2. Host reset ตัวเอง
            LocalResetAndReturnToMenu();
        }
        // 🔵 โหมด Client กดปุ่มเอง
        else
        {
            // 1. ส่งไปบอก Host ให้จัดการ
            Debug.Log("[Client] ส่ง RequestForceLeaveRoom ไป Host");
            SendMessage("RequestForceLeaveRoom"); // หรือ SendToServer("RequestForceLeaveRoom")
        }
    }
    public void LocalResetAndReturnToMenu()
    {
        // ปิด Network
        if (isServer) StopServer();
        else DisconnectClient();

        // ล้าง PlayerDataManager
        if (PlayerDataManager.Instance != null)
        {
            PlayerDataManager.Instance.localPlayer = null;
            PlayerDataManager.Instance.players.Clear();
            PlayerDataManager.Instance.currentRoom = null;
        }

        // โหลด Scene CharacterSelect
        UnityEngine.SceneManagement.SceneManager.LoadScene("CharacterSelect");
    }
    public void BroadcastMessageToClients(string msg)
    {
        SendMessageToAll(msg);
    }
    public void SendMessageToAllExceptHost(string message)
    {
        foreach (var c in clients)
        {
            SendToClient(c, message);
        }
    }

}
