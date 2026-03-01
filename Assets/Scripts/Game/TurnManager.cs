using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

public class TurnManager : MonoBehaviour
{
    #region 🔸 Inspector Variables (ตั้งค่าใน Unity)
    [Header("ตั้งค่าเกม")]
    [SerializeField] public int totalDays;        // จำนวนวันทั้งหมด
    [SerializeField] public float dayDuration;    // เวลาต่อวัน (วินาที)

    [Header("UI")]
    public TMP_Text dayText;
    public TMP_Text timerText;
    public GameObject rankingPanel;               // ถ้าไม่มี DailyReportUI จะ fallback มาใช้ panel นี้
    public Button nextDayButton;                  // ปุ่ม Next Day (ของ rankingPanel)

    [Header("ระบบอื่น")]
    public InvestmentManager investmentManager;
    public BuyManager buyManager;

    [Header("Player Event")]
    [Tooltip("SO ที่เก็บ Pool ของ PlayerEvent ทั้งหมด")]
    public PlayerEventLibrary playerEventLibrary;

    [Header("กฎสุ่มอีเวนต์")]
    [Tooltip("เริ่มสุ่มอีเวนต์ตั้งแต่วันไหน (เช่น 2 = เริ่มวันถัดไปจากวันแรก)")]
    public int eventStartDay = 2;

    [Header("Global Event")]
    [Range(0f, 1f), Tooltip("โอกาสเกิดอีเวนต์ตลาดต่อวัน (0..1)")]
    public float marketEventChance = 0.75f;

    [Header("Daily Event")]
    [Range(0f, 1f), Tooltip("โอกาสเกิดอีเวนต์ผู้เล่นต่อวัน (0..1)")]
    public float playerEventChance = 0.9f;

    [Header("Panels ที่ให้ปิดตอน EndDay")]
    [Tooltip("ลากพวกหน้า Company / Portfolio / Tutorial หรือ popup อื่น ๆ มาใส่")]
    public List<GameObject> panelsToCloseOnEndDay = new List<GameObject>();
    #endregion

    #region 🧭 State Variables (ตัวแปรภายใน)
    private int currentDay = 1;
    private float timer;
    private bool isDayActive = false;

    // 🔒 ป้องกันข้าม 2 วัน / ปุ่มยิงซ้ำ
    private bool waitingNextDayConfirm = false;
    private bool nextDayCalled = false;

    // ✅ กัน “อีเวนต์ผู้เล่นซ้ำทั้งเกม” (ถ้าอยากใช้)
    private HashSet<PlayerEventSO> usedPlayerEvents = new HashSet<PlayerEventSO>();

    // ✅ กัน “อีเวนต์ตลาดซ้ำทั้งเกม” (good/bad ไม่ซ้ำ)
    private HashSet<EventData> usedMarketEvents = new HashSet<EventData>();

    // เก็บข้อมูล “อีเวนต์ของวันถัดไป” เพื่อแสดงต้นวัน/และใช้ override %
    private EventData nextDayMarketEvent;                 // อีเวนต์ตลาดของวันถัดไป (ถ้ามี)
    private InvestmentCompany nextDayMarketEventCompany;  // บริษัทที่โดน
    private List<InvestmentCompanyRuntime> nextDayAffected = new List<InvestmentCompanyRuntime>();

    // ✅ Player Event วันถัดไป: แยกตาม playerName
    private Dictionary<string, PlayerEventSO> nextDayPlayerEvents
        = new Dictionary<string, PlayerEventSO>();

    private List<PlayerData> players => PlayerDataManager.Instance.players;

    // 🧭 State Variables
    private Dictionary<PlayerData, bool> playersConfirmed = new Dictionary<PlayerData, bool>();
    private bool playerEventAppliedThisDay = false;
    private Dictionary<string, int> syncedRanks = new Dictionary<string, int>(); // เก็บอันดับที่ซิงค์แล้ว

    // ใช้รอว่า Host ส่ง NEXTDAY_SYNC มายัง (สำหรับ client)
    private bool nextDayPctSynced = false;

    // 🔊 กันเสียง Clock ซ้อน: เล่นแค่ครั้งเดียวต่อวัน
    private bool clockWarningPlayed = false;
    #endregion

    #region 🟩 Unity Lifecycle
    void Start()
    {
        // 🟢 ดึงจำนวนวัน / เวลาต่อวันจาก NetworkManager (ที่ Host ตั้งจากหน้า Lobby)
        if (NetworkManager.Instance != null)
        {
            totalDays = NetworkManager.Instance.gameTotalDays;
            dayDuration = NetworkManager.Instance.gameDayDuration;
            Debug.Log($"[TurnManager] ใช้ค่าเกมจาก Lobby → Days={totalDays}, DayDuration={dayDuration} วินาที");
        }

        // รีเซ็ตสถานะเริ่มเกม
        investmentManager?.ResetUsedMarketEvents();
        usedPlayerEvents.Clear();
        usedMarketEvents.Clear();

        if (NetworkManager.Instance != null)
        {
            // Client subscribe event
            NetworkManager.Instance.OnShowRankingPanelReceived += () =>
            {
                if (rankingPanel != null) rankingPanel.SetActive(true);
                if (nextDayButton != null) nextDayButton.gameObject.SetActive(true);
            };

            NetworkManager.Instance.OnStartTimerReceived += ReceiveStartTimerFromHost;
        }

        // ✅ ให้ Host เป็นคนเริ่มวัน (ป้องกัน StartDay() ซ้ำ)
        if (NetworkManager.Instance == null || NetworkManager.Instance.isServer)
        {
            StartDay(); // Host เริ่มวันแรก
            if (NetworkManager.Instance != null && NetworkManager.Instance.isServer)
            {
                NetworkManager.Instance.BroadcastStartTimer(currentDay, dayDuration);
            }
        }
        else
        {
            // Client: รอ Host ส่ง StartTimer
            isDayActive = false;
        }
    }

    void Update()
    {
        if (!isDayActive) return;

        timer -= Time.deltaTime;

        // 🔊 เล่นเสียงนาฬิกาเตือน 10 วิสุดท้าย (วันละ 1 ครั้ง)
        if (!clockWarningPlayed && timer > 0f && timer <= 10f)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.Play(AudioManager.SoundType.Clock);
            }
            clockWarningPlayed = true;
        }

        if (timerText != null) timerText.text = FormatTime(timer);
        if (timer <= 0f)
        {
            if (NetworkManager.Instance != null && !NetworkManager.Instance.isServer)
            {
                // 📨 แจ้ง Host ว่าตัวเองหมดเวลา
                NetworkManager.Instance.NotifyTimeUp();
            }

            EndDay(); // local client จะเรียกจบวันเอง (UI จะถูกเปิดโดย Host)
        }
    }
    #endregion

    private string FormatTime(float timeInSeconds)
    {
        if (timeInSeconds < 0)
        {
            timeInSeconds = 0;
        }

        int time = Mathf.FloorToInt(timeInSeconds);
        int minutes = time / 60;
        int seconds = time % 60;

        return $"{minutes:00}:{seconds:00}";
    }

    #region 🟥 Core Turn Logic (เริ่ม/จบ/ข้ามวัน)

    void StartDay()
    {
        // 👇 วันแรก: กันทุกอย่างค้างมาจากเกมก่อน / scene ก่อน
        if (currentDay == 1)
        {
            nextDayMarketEvent = null;
            nextDayMarketEventCompany = null;
            nextDayAffected.Clear();
            nextDayPlayerEvents.Clear();
            playerEventAppliedThisDay = false;

            if (investmentManager != null && investmentManager.activeCompanies != null)
            {
                foreach (var rt in investmentManager.activeCompanies)
                {
                    if (rt == null) continue;
                    rt.hasNextDayPct = false;
                    rt.nextDayPct = 0f;
                }
            }

            Debug.Log("[TurnManager] Reset all next-day event/percent state for Day 1");
        }

        // รีเซ็ต state ประจำวัน
        playerEventAppliedThisDay = false;
        EndTurnButtonManager.Instance?.EnableAllButtons();

        // 🔊 รีเซ็ต state ไม่ให้ Clock เล่นซ้ำมาจากวันก่อน
        clockWarningPlayed = false;

        Debug.Log($"[DEBUG] StartDay: DayDuration={dayDuration}, Timer ถูกตั้งค่าเป็น {dayDuration} วินาที");
        timer = dayDuration;
        isDayActive = true;

        // --- รีเซ็ต UI/สถานะก่อนเริ่มวัน ---
        waitingNextDayConfirm = false;
        nextDayCalled = false;
        if (rankingPanel != null) rankingPanel.SetActive(false);
        if (nextDayButton != null) nextDayButton.gameObject.SetActive(false);

        Debug.Log($"▶️ เริ่มวัน {currentDay}");

        if (dayText != null) dayText.text = "" + currentDay;

        // ✅ รีเซ็ต Stamina ของผู้เล่นทุกคน
        var staminaUI = FindAnyObjectByType<StaminaUI>();
        staminaUI?.ResetStamina();

        // ✅ เก็บ "สแน็ปช็อตต้นวัน" ทั้งราคาและจำนวนหุ้น เพื่อคำนวณ P/L รายวัน
        foreach (var p in PlayerDataManager.Instance.players)
        {
            foreach (var h in p.holdings)
            {
                var rt = investmentManager.activeCompanies.FirstOrDefault(c => c.subAsset == h.subAsset);
                if (rt != null)
                {
                    h.lastPrice = rt.currentPrice;      // baseline ราคา ณ ต้นวัน
                    h.sharesAtStartOfDay = h.shares;    // baseline จำนวนหุ้น ณ ต้นวัน
                }
            }
        }

        // ✅ ใช้ค่าที่ “พรีโรลไว้เมื่อจบวันก่อนหน้า” เฉพาะถ้า currentDay > 1
        //    *** ตรงนี้ Host/โหมด Local เป็นคนคอมมิตราคา แล้วให้ Network sync ไป Client ***
        if (currentDay > 1 && investmentManager != null && investmentManager.activeCompanies != null)
        {
            if (NetworkManager.Instance == null || NetworkManager.Instance.isServer)
            {
                foreach (var rt in investmentManager.activeCompanies)
                {
                    if (rt == null) continue;
                    float usedPct = rt.ConsumeNextDayPctOrRollNow(currentDay);

                    Debug.Log($"[Commit D{currentDay}] {rt.subAsset.assetName} ใช้พรีโรล {usedPct:+0.##;-0.##}% → ราคา {rt.currentPrice:N2}");
                }

                // แสดงข้อความ event ตลาด (ของ "วันนี้") ถ้ามี (Host/local เท่านั้น)
                if (nextDayMarketEvent != null && nextDayMarketEventCompany != null && nextDayAffected.Count > 0)
                {
                    var eventPanelMgr = FindAnyObjectByType<EventPanelManager>();
                    if (eventPanelMgr != null)
                        eventPanelMgr.ShowMarketEvent(nextDayMarketEvent);

                    string pctStr = nextDayMarketEvent.priceChange >= 0
                        ? $"+{nextDayMarketEvent.priceChange:0.##}%"
                        : $"{nextDayMarketEvent.priceChange:0.##}%";
                    string subList = string.Join(", ", nextDayAffected.Select(a => a.subAsset.assetName));
                    LogFeed($"[ตลาด] {nextDayMarketEventCompany.companyName}: {nextDayMarketEvent.description} ({pctStr}) → กระทบ [{subList}]");
                }
            }
        }

        // ✅ Apply อีเวนต์ผู้เล่นของ “วันนี้” (ถ้ามี) — เราสุ่มไว้ล่วงหน้าแล้วตอนจบวันก่อนหน้า (Host/Local)
        if (currentDay >= eventStartDay &&
            nextDayPlayerEvents != null &&
            nextDayPlayerEvents.Count > 0 &&
            !playerEventAppliedThisDay)
        {
            if (NetworkManager.Instance == null || NetworkManager.Instance.isServer)
            {
                foreach (var p in PlayerDataManager.Instance.players)
                {
                    if (p == null) continue;

                    if (nextDayPlayerEvents.TryGetValue(p.playerName, out var evt) && evt != null)
                    {
                        p.ApplyPlayerEvent(evt);
                        Debug.Log($"[PlayerEvent Host] Apply ให้ {p.playerName}: {evt.description} ({evt.price:+0;-0})");
                    }
                }

                playerEventAppliedThisDay = true;
            }

            // แสดง UI เฉพาะ event ของ local player (Host/Local)
            var local = PlayerDataManager.Instance.localPlayer ?? players.FirstOrDefault();
            if (local != null && nextDayPlayerEvents.TryGetValue(local.playerName, out var localEvt) && localEvt != null)
            {
                var eventPanelMgr = FindAnyObjectByType<EventPanelManager>();
                if (eventPanelMgr != null)
                {
                    eventPanelMgr.ShowPlayerEvent(localEvt, local.playerName);
                }

                var sign = localEvt.price >= 0 ? "+" : "-";
                LogFeed($"[ผู้เล่น] {local.playerName}: {localEvt.description}  ({sign}{Mathf.Abs(localEvt.price):N0})");
            }
        }

        // ⭐ พรีโรลอีเวนต์ของ "วันถัดไป" + โชว์ Flavor ล่วงหน้า 1 วัน
        //    - ทำเฉพาะ Host / โหมด Local
        //    - currentDay < totalDays กันไม่ให้พรีวิววันถัดไปในวันสุดท้าย
        if (currentDay < totalDays &&
            (NetworkManager.Instance == null || NetworkManager.Instance.isServer))
        {
            PreRollNextDayEvents();   // ในนี้จะเรียก DailyFlavorTextUI.ShowFlavorForEvent(nextDayMarketEvent) ให้เอง
        }

        // เริ่มจับเวลา/เทิร์นของวัน
        timer = dayDuration;
        isDayActive = true;

        // บอท
        StartCoroutine(BotActionsLoop());
    }

    // เรียกตอนเริ่ม EndDay()
    public void PrepareDailyConfirm()
    {
        playersConfirmed.Clear();
        foreach (var p in players)
        {
            playersConfirmed[p] = false; // ยังไม่กดยืนยัน
        }
    }

    public void EndDay()
    {
        if (!isDayActive) return;
        isDayActive = false;
        StopAllCoroutines();

        // 🔊 ตัดเสียงนาฬิกา 10 วิ (กันลากเสียงข้ามมาหน้า Report / วันถัดไป)
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopClockSFX();
        }

        // 🔁 จบวันใหม่ -> client จะรอ sync NEXTDAY ก่อนค่อยโชว์รายงาน
        if (NetworkManager.Instance != null && !NetworkManager.Instance.isServer)
        {
            nextDayPctSynced = false;
        }

        Debug.Log($"⏹ จบวัน {currentDay}");

        // 🔹 ปิด Player Helper Circle ด้วย
        var playerIcon = FindObjectOfType<PlayerIconButton>(true);
        if (playerIcon != null)
        {
            playerIcon.ForceClose();
            Debug.Log("[TurnManager] ปิด HelperCircle ของ PlayerIcon ตอนจบวัน");
        }

        // 🔹 ปิด PortfolioPanel ถ้าเปิดอยู่
        var portfolio = FindObjectOfType<PortfolioUI>(true);
        if (portfolio != null && portfolio.portfolioPanel != null && portfolio.portfolioPanel.activeSelf)
        {
            portfolio.portfolioPanel.SetActive(false);
            Debug.Log("[TurnManager] ปิด PortfolioPanel ตอนจบวัน");
        }

        // 🔹 ปิด CompanyUI ทั้งหมด
        var companyUIs = FindObjectsOfType<CompanyUI>(true);
        foreach (var c in companyUIs)
        {
            if (c != null && c.gameObject.activeSelf)
            {
                c.gameObject.SetActive(false);
            }
        }

        // 🔹 ปิดพวก panel เล่นเกมที่ค้างอยู่ก่อน
        ClosePanelsOnEndDay();

        // ✅ Host เท่านั้นที่สั่งปิดปุ่ม
        if (NetworkManager.Instance == null || NetworkManager.Instance.isServer)
        {
            if (EndTurnButtonManager.Instance != null)
            {
                foreach (var btn in EndTurnButtonManager.Instance.buttonsToDisable)
                {
                    if (btn != null) btn.interactable = false;
                }
                if (EndTurnButtonManager.Instance.waitingPanel != null)
                    EndTurnButtonManager.Instance.waitingPanel.SetActive(false);
            }
        }

        // ✅ สรุป Exposure ของวันนี้ (อัปเดต peak ด้วย) — ทำทุกเครื่อง (Host + Client)
        foreach (var p in PlayerDataManager.Instance.players)
        {
            p.AccumulateDailyRiskExposure(investmentManager, updatePeak: true);
        }

        // ✅ เข้าสู่โหมดรอยืนยัน (ทำทั้ง Host/Client)
        waitingNextDayConfirm = true;
        nextDayCalled = false;
        PrepareDailyConfirm();

        // ============================
        // ✅ PRE-ROLL เปอร์เซ็นต์ “วันถัดไป” (Host เท่านั้น)
        //    ใช้ Event ที่พรีโรลไว้แล้วใน StartDay()
        // ============================
        if (currentDay < totalDays &&
            (NetworkManager.Instance == null || NetworkManager.Instance.isServer))
        {
            PreRollNextDayPercents();

            // ⭐ ส่ง nextDayPct ให้ Client ทุกคน
            if (NetworkManager.Instance != null && NetworkManager.Instance.isServer)
            {
                NetworkManager.Instance.BroadcastNextDayPercents(investmentManager);
            }
        }

        // 💡 HOST คำนวณและ Broadcast อันดับ
        if (NetworkManager.Instance != null && NetworkManager.Instance.isServer)
        {
            NetworkManager.Instance.BroadcastRanks(investmentManager);
            syncedRanks = PlayerDataManager.Instance.CalculateRanks(investmentManager);
        }

        var localPlayer = PlayerDataManager.Instance.localPlayer;
        int rankToDisplay = 0;
        if (localPlayer != null && syncedRanks.TryGetValue(localPlayer.playerName, out int hostRank))
        {
            rankToDisplay = hostRank;
        }

        var reportUI = FindAnyObjectByType<DailyReportUI>();
        if (reportUI != null)
        {
            bool isHost = (NetworkManager.Instance == null || NetworkManager.Instance.isServer);

            if (isHost)
            {
                ShowReportNow(reportUI, localPlayer, rankToDisplay);
            }
            else
            {
                StartCoroutine(WaitAndShowReport(reportUI, localPlayer));
            }
        }
        else
        {
            if (rankingPanel != null) rankingPanel.SetActive(true);
            if (nextDayButton != null)
            {
                nextDayButton.onClick.RemoveAllListeners();
                nextDayButton.interactable = true;
                nextDayButton.gameObject.SetActive(true);
                nextDayButton.transform.SetAsLastSibling();
                nextDayButton.onClick.AddListener(OnRankingConfirmed);
            }
        }

        // สรุปบอท (console)
        foreach (var p in players)
            if (p is BotData) BotReport.ShowDailyBotReports(players, investmentManager, currentDay);
    }

    private void ShowReportNow(DailyReportUI reportUI, PlayerData localPlayer, int rankToDisplay)
    {
        if (rankingPanel != null) rankingPanel.SetActive(true);
        if (nextDayButton != null) nextDayButton.gameObject.SetActive(true);

        reportUI.ShowDailyReport(localPlayer, investmentManager, currentDay, rankToDisplay);

        if (reportUI.confirmButton != null)
        {
            reportUI.confirmButton.onClick.RemoveAllListeners();
            reportUI.confirmButton.onClick.AddListener(OnRankingConfirmed);
        }
    }

    // ============================
    // PRE-ROLL helpers
    // ============================
    private void PreRollNextDayEvents()
    {
        // Reset
        nextDayMarketEvent = null;
        nextDayMarketEventCompany = null;
        nextDayAffected.Clear();
        nextDayPlayerEvents.Clear();

        // เงื่อนไขวันสำหรับ MarketEvent (ของวันถัดไป: currentDay + 1)
        bool canMarket = (currentDay + 1) >= eventStartDay && Random.value < marketEventChance;

        // Market Event “วันถัดไป” (กันซ้ำทั้งเกม)
        if (canMarket && investmentManager != null)
        {
            var evt = investmentManager.TriggerRandomEventNoRepeat(
            usedMarketEvents,
            out var company,
            out var affectedList
);

            if (evt != null && company != null && affectedList != null && affectedList.Count > 0)
            {
                nextDayMarketEvent = evt;
                nextDayMarketEventCompany = company;
                nextDayAffected = new List<InvestmentCompanyRuntime>(affectedList);
                usedMarketEvents.Add(evt);
            }
        }

        // Player Event “วันถัดไป” แยกตามคน (โอกาสตาม playerEventChance)
        if (playerEventLibrary != null && playerEventLibrary.events != null && playerEventLibrary.events.Count > 0)
        {
            foreach (var p in PlayerDataManager.Instance.players)
            {
                if (p == null) continue;

                bool canPlayer = (currentDay + 1) >= eventStartDay && Random.value < playerEventChance;
                if (!canPlayer) continue;

                var pool = playerEventLibrary.events
                    .Where(e => e != null /* && !usedPlayerEvents.Contains(e) */)
                    .ToList();

                if (pool.Count == 0) break;

                var evt = pool[Random.Range(0, pool.Count)];
                nextDayPlayerEvents[p.playerName] = evt;
            }
        }

        // ⭐ โชว์ Flavor ของ "วันถัดไป" ล่วงหน้า 1 วัน
        if (nextDayMarketEvent != null && DailyFlavorTextUI.Instance != null)
        {
            DailyFlavorTextUI.Instance.ShowFlavorForEvent(nextDayMarketEvent);
        }

        string marketPart = nextDayMarketEvent != null ? $"| Market: {nextDayMarketEvent.description}" : "";
        string playerPart = nextDayPlayerEvents.Count > 0
            ? " | PlayerEvents: " + string.Join(", ",
                nextDayPlayerEvents.Select(kv => $"{kv.Key}:{kv.Value.description}"))
            : " | PlayerEvents: -";

        Debug.Log("[PreRoll] Next Day Events " + marketPart + playerPart);
    }

    private void PreRollNextDayPercents()
    {
        if (investmentManager == null || investmentManager.activeCompanies == null) return;

        // 1) พรีโรล % ปกติของทุก runtime
        foreach (var rt in investmentManager.activeCompanies)
        {
            if (rt == null || rt.subAsset == null) continue;
            rt.PreRollNextDayPct();
        }

        // 2) ถ้ามี Market Event วันถัดไป → override % ของตัวที่โดน
        if (nextDayMarketEvent != null && nextDayAffected != null && nextDayAffected.Count > 0)
        {
            foreach (var rt in nextDayAffected)
            {
                if (rt == null) continue;
                rt.OverrideNextDayPct(nextDayMarketEvent.priceChange);
            }
        }

        // Debug ดูว่าพรีโรลแล้วจริง
        foreach (var rt in investmentManager.activeCompanies)
        {
            if (rt == null || rt.subAsset == null) continue;
            Debug.Log($"[Check nextDayPct] {rt.subAsset.assetName} has={rt.hasNextDayPct} pct={rt.nextDayPct:+0.##;-0.##}%");
        }
    }

    void NextDay()
    {
        Debug.Log($"[NextDay] called | waiting={waitingNextDayConfirm}, nextCalled={nextDayCalled}");

        if (nextDayCalled) return; // กันยิงซ้ำ
        nextDayCalled = true;

        if (!waitingNextDayConfirm)
        {
            Debug.LogWarning("[TurnManager] Ignore NextDay(): ยังไม่ได้เข้าสู่โหมดยืนยันวันถัดไป");
            nextDayCalled = false;
            return;
        }

        // 💡 Host สั่งปิด Waiting Panel สำหรับทุกคน
        if (NetworkManager.Instance != null && NetworkManager.Instance.isServer)
        {
            var endTurnMgr = EndTurnButtonManager.Instance;
            if (endTurnMgr != null && endTurnMgr.waitingPanel != null)
            {
                Debug.Log("[Confirm] 🟢 Host: ปิด Waiting Panel และเตรียมเริ่มวันใหม่");
                endTurnMgr.waitingPanel.SetActive(false);
            }

            NetworkManager.Instance.SendMessageToAll("CloseWaitingPanel");
        }

        var ui = FindAnyObjectByType<DailyReportUI>();
        if (ui != null) ui.Hide();
        if (rankingPanel != null) rankingPanel.SetActive(false);
        if (nextDayButton != null) nextDayButton.gameObject.SetActive(false);

        currentDay++;
        Debug.Log($"➡️ ไปวันถัดไป: {currentDay}/{totalDays}");

        // 🎯 จบเกม → แสดงสรุปประเภทนักลงทุน
        if (currentDay > totalDays)
        {
            Debug.Log("🎉 เกมจบแล้ว!");

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.StopBGM();
                AudioManager.Instance.Play(AudioManager.SoundType.GameOver);
            }

            if (NetworkManager.Instance != null && NetworkManager.Instance.isServer)
            {
                foreach (var p in PlayerDataManager.Instance.players)
                    p.AccumulateDailyRiskExposure(investmentManager, updatePeak: true);

                NetworkManager.Instance.BroadcastFinalPlayerData(investmentManager);
                NetworkManager.Instance.SendMessageToAll("GameOverUI");
            }
            else if (NetworkManager.Instance == null)
            {
                foreach (var p in PlayerDataManager.Instance.players)
                    p.AccumulateDailyRiskExposure(investmentManager, updatePeak: true);
            }

            var finalUI = FindAnyObjectByType<GameOverSummaryUI>();
            var local = PlayerDataManager.Instance.localPlayer ?? PlayerDataManager.Instance.players.FirstOrDefault();

            var sorted = PlayerDataManager.Instance.players
                .OrderByDescending(x => x.GetTotalAssets(investmentManager))
                .ToList();
            int finalRank = sorted.IndexOf(local) + 1;

            if (finalUI != null && local != null)
            {
                finalUI.Show(local, investmentManager, finalRank);
                finalUI.ShowWithLeaderboard(PlayerDataManager.Instance.players, investmentManager);
            }
            else
            {
                Debug.LogWarning("GameOverSummaryUI หรือ Local Player ไม่พร้อม — ข้ามการแสดงผลสรุปจบเกม");
            }

            return;
        }

        StartDay(); // ▶️ เริ่มวันใหม่

        if (NetworkManager.Instance != null && NetworkManager.Instance.isServer)
        {
            NetworkManager.Instance.BroadcastStartTimer(currentDay, dayDuration);
        }
    }

    #endregion

    #region 🌐 Network / Multiplayer Sync

    public void MarkPlayerConfirmed(string playerName)
    {
        var confirmedPlayer = PlayerDataManager.Instance.players.FirstOrDefault(p => p.playerName == playerName);

        if (confirmedPlayer == null)
        {
            Debug.LogError($"[Confirm] ไม่พบ PlayerData ของ {playerName} ที่ส่งสัญญาณยืนยัน");
            return;
        }

        if (playersConfirmed.ContainsKey(confirmedPlayer))
        {
            playersConfirmed[confirmedPlayer] = true;
        }
        else
        {
            playersConfirmed[confirmedPlayer] = true;
            Debug.LogWarning($"[Confirm] Player {playerName} ไม่อยู่ใน Dictionary แต่ถูกยืนยันผ่าน Network!");
        }

        Debug.Log($"[Confirm] {playerName} ยืนยันแล้ว (ผ่าน Network)");

        if (playersConfirmed.Values.All(v => v))
        {
            int totalPlayers = PlayerDataManager.Instance.players.Count;
            int confirmedCount = playersConfirmed.Count(kv => kv.Value);

            if (confirmedCount == totalPlayers)
            {
                Debug.Log("[Confirm] ทุกคนกดยืนยันครบแล้ว (รวม Local Host) → NextDay");
                NextDay();
            }
        }
        else
        {
            int confirmed = playersConfirmed.Values.Count(v => v);
            int total = playersConfirmed.Count;
            Debug.Log($"[Confirm] รอกดครบทุกคน ({confirmed}/{total})");
        }
    }
    #endregion

    #region 🤖 Bot System
    IEnumerator BotActionsLoop()
    {
        while (isDayActive)
        {
            foreach (var p in players)
            {
                if (p is BotData bot)
                {
                    if (Random.value < 0.5f)
                        bot.PerformRandomBuy(buyManager, investmentManager);
                    else
                        bot.PerformRandomSell(buyManager, investmentManager);
                }
            }
            yield return new WaitForSeconds(Random.Range(3f, 8f));
        }
    }
    #endregion

    #region 🪄 Player & Market Event System
    void TriggerRandomPlayerEventForLocal()
    {
        var pool = (playerEventLibrary != null) ? playerEventLibrary.events : null;

        if (pool == null || pool.Count == 0)
        {
            Debug.Log("[PlayerEvent] ไม่มี PlayerEventSO ใน library");
            return;
        }

        var localPlayer = PlayerDataManager.Instance.localPlayer ?? players.FirstOrDefault();
        if (localPlayer == null)
        {
            Debug.LogWarning("[PlayerEvent] ไม่พบ localPlayer");
            return;
        }

        var remaining = pool.Where(e => e != null && !usedPlayerEvents.Contains(e)).ToList();
        if (remaining.Count == 0)
        {
            Debug.Log("[PlayerEvent] อีเวนต์ผู้เล่นหมดแล้ว (กันซ้ำทั้งเกม)");
            LogFeed("[ผู้เล่น] วันนี้ไม่มีเหตุการณ์ผู้เล่นใหม่ (หมดสต็อก)");
            return;
        }

        var evt = remaining[Random.Range(0, remaining.Count)];

        localPlayer.ApplyPlayerEvent(evt);
        usedPlayerEvents.Add(evt);

        var sign = evt.price >= 0 ? "+" : "-";
        LogFeed($"[ผู้เล่น] {localPlayer.playerName}: {evt.description}  ({sign}{Mathf.Abs(evt.price):N0})");

        Debug.Log($"[PlayerEvent] Apply กับ {localPlayer.playerName}: {evt.description} ({evt.price:+0;-0})");
    }
    #endregion

    #region 🛠 Helper & Debug
    private void LogFeed(string message)
    {
        if (EventDebugFeed.Instance != null)
        {
            EventDebugFeed.Instance.Log(message);
        }
        else
        {
            Debug.Log(message);
        }
    }
    #endregion

    // 🔹 เรียกเมื่อผู้เล่นกดยืนยัน RankingPanel
    public void OnRankingConfirmed()
    {
        var localPlayer = PlayerDataManager.Instance.localPlayer;
        var turnManager = FindAnyObjectByType<TurnManager>();

        if (localPlayer == null || !playersConfirmed.ContainsKey(localPlayer)) return;

        playersConfirmed[localPlayer] = true;
        Debug.Log($"[Confirm] {localPlayer.playerName} กดยืนยันแล้ว");

        bool isHost = NetworkManager.Instance != null && NetworkManager.Instance.isServer;
        bool everyoneConfirmed = playersConfirmed.Values.All(v => v);
        bool hostWillCallNextDay = isHost && everyoneConfirmed;

        var endTurnMgr = FindAnyObjectByType<EndTurnButtonManager>();
        if (endTurnMgr != null)
        {
            endTurnMgr.DisableGameplayButtons();
        }

        if (rankingPanel != null)
            rankingPanel.SetActive(false);
        if (nextDayButton != null)
            nextDayButton.gameObject.SetActive(false);

        if (turnManager != null && !hostWillCallNextDay)
        {
            turnManager.isDayActive = false;
            turnManager.waitingNextDayConfirm = true;
            Debug.Log("[Confirm] ⏸ หยุดเวลา รอ Host สั่งเริ่มวันใหม่");
        }

        if (NetworkManager.Instance != null && !NetworkManager.Instance.isServer)
        {
            NetworkManager.Instance.SendMessage($"DayConfirm:{localPlayer.playerName}");
        }

        if (hostWillCallNextDay)
        {
            Debug.Log("[Confirm] Host: ทุกคนกดยืนยันครบแล้ว → NextDay");
            NextDay();
        }
        else if (isHost)
        {
            int confirmed = playersConfirmed.Values.Count(v => v);
            int total = playersConfirmed.Count;
            Debug.Log($"[Confirm] Host รอกดครบทุกคน ({confirmed}/{total})");
        }
    }

    public void ReceiveStartTimerFromHost(int day, float t)
    {
        currentDay = day;
        timer = t;
        isDayActive = true;

        clockWarningPlayed = false;

        if (dayText != null) dayText.text = currentDay.ToString();
        if (timerText != null) timerText.text = FormatTime(timer);

        var staminaUI = FindAnyObjectByType<StaminaUI>();
        staminaUI?.ResetStamina();

        EndTurnButtonManager.Instance?.EnableAllButtons();

        Debug.Log($"[TurnManager] Client เริ่มวัน {currentDay} | timer={timer}");

        // 🔥 Client: Apply PlayerEvent ที่ถูก Sync มาจาก Host ให้ตรง "วันใหม่"
        if (currentDay >= eventStartDay &&
            nextDayPlayerEvents != null &&
            nextDayPlayerEvents.Count > 0)
        {
            foreach (var p in PlayerDataManager.Instance.players)
            {
                if (p == null) continue;

                if (nextDayPlayerEvents.TryGetValue(p.playerName, out var evt) && evt != null)
                {
                    p.ApplyPlayerEvent(evt);
                    Debug.Log($"[Client PlayerEvent] Apply ให้ {p.playerName}: {evt.description} ({evt.price:+0;-0})");
                }
            }

            var local = PlayerDataManager.Instance.localPlayer ?? players.FirstOrDefault();
            if (local != null && nextDayPlayerEvents.TryGetValue(local.playerName, out var localEvt) && localEvt != null)
            {
                var eventPanelMgr = FindAnyObjectByType<EventPanelManager>();
                if (eventPanelMgr != null)
                    eventPanelMgr.ShowPlayerEvent(localEvt, local.playerName);

                var sign = localEvt.price >= 0 ? "+" : "-";
                LogFeed($"[ผู้เล่น Sync Client] {local.playerName}: {localEvt.description} ({sign}{Mathf.Abs(localEvt.price):N0})");
            }

            // เคลียร์หลัง apply แล้ว
            nextDayPlayerEvents.Clear();
        }
    }

    // 💡 เมธอดใหม่: Client ใช้รับอันดับจาก Host
    public void ReceiveRanksFromHost(Dictionary<string, int> ranks)
    {
        syncedRanks = ranks;

        var localPlayer = PlayerDataManager.Instance.localPlayer;
        if (localPlayer != null && ranks.TryGetValue(localPlayer.playerName, out int rank))
        {
            Debug.Log($"[Client Sync] อัปเดตอันดับเป็น: {rank}");
            var reportUI = FindAnyObjectByType<DailyReportUI>();
            if (reportUI != null && reportUI.panel.activeInHierarchy)
            {
                reportUI.UpdateRankText(rank);
            }
        }
    }

    public void MarkNextDayPctSynced()
    {
        nextDayPctSynced = true;
        Debug.Log("[TurnManager] NEXTDAY_SYNC received → nextDayPctSynced = true");
    }

    // 🛠 Helper สำหรับ Network: แปลง Market Event เป็น string
    public string GetMarketEventSyncData()
    {
        if (nextDayMarketEvent == null) return "NONE";

        string affected = string.Join(",", nextDayAffected.Select(rt => rt.subAsset.assetName));
        string description = nextDayMarketEvent.description;

        return $"MARKET|{nextDayMarketEventCompany.companyName}|{nextDayMarketEvent.priceChange}|{description}|{affected}";
    }

    // 🛠 Helper สำหรับ Network: แปลง Player Event เป็น string (แบบ per-player)
    public string GetPlayerEventSyncData()
    {
        if (nextDayPlayerEvents == null || nextDayPlayerEvents.Count == 0)
            return "NONE";

        var invCulture = CultureInfo.InvariantCulture;
        var sb = new StringBuilder("PLAYER");

        foreach (var kv in nextDayPlayerEvents)
        {
            var playerName = kv.Key;
            var evt = kv.Value;
            if (evt == null) continue;

            sb.Append('|')
              .Append(playerName).Append(',')
              .Append(evt.price.ToString(invCulture)).Append(',')
              .Append(evt.description);
        }

        return sb.ToString();
    }

    private void ClosePanelsOnEndDay()
    {
        if (panelsToCloseOnEndDay == null) return;

        foreach (var p in panelsToCloseOnEndDay)
        {
            if (p != null && p.activeSelf)
                p.SetActive(false);
        }
    }

    // 🌐 เมธอด: Client ใช้รับข้อมูล Event ที่ถูก Pre-roll จาก Host (ตลาด + player per-player)
    public void SyncEventsFromHost(string marketData, string playerData)
    {
        var invCulture = CultureInfo.InvariantCulture;

        // ====== Market Event ======
        if (!string.IsNullOrEmpty(marketData) && marketData != "NONE")
        {
            var marketParts = marketData.Split('|');
            if (marketParts.Length >= 5)
            {
                string companyName = marketParts[1]?.Trim();

                if (float.TryParse(marketParts[2], NumberStyles.Float, invCulture, out float pctChange))
                {
                    string description = marketParts[3]?.Trim();
                    string affectedAssets = marketParts[4] ?? string.Empty;

                    var affectedNames = affectedAssets
                        .Split(new[] { ',', ';' }, System.StringSplitOptions.RemoveEmptyEntries)
                        .Select(n => n.Trim())
                        .Where(n => !string.IsNullOrEmpty(n))
                        .ToList();

                    var invMgr = investmentManager != null ? investmentManager : FindObjectOfType<InvestmentManager>();
                    EventData actualEvent = null;
                    InvestmentCompany companySO = null;
                    List<InvestmentCompanyRuntime> affectedRts = new List<InvestmentCompanyRuntime>();

                    if (invMgr != null && invMgr.companyLibrary != null && invMgr.companyLibrary.companies != null)
                    {
                        companySO = invMgr.companyLibrary.companies
                            .FirstOrDefault(c => c != null && c.companyName == companyName);

                        if (companySO != null)
                        {
                            actualEvent = companySO.goodEvents.Concat(companySO.badEvents)
                                .FirstOrDefault(e =>
                                    e != null &&
                                    e.description == description &&
                                    Mathf.Approximately(e.priceChange, pctChange)
                                );
                        }
                    }

                    if (invMgr != null && invMgr.activeCompanies != null)
                    {
                        bool MatchCompany(InvestmentCompanyRuntime r)
                            => r != null && r.data != null && r.data.companyName == companyName;

                        affectedRts = invMgr.activeCompanies
                            .Where(r => r != null
                                        && r.subAsset != null
                                        && !string.IsNullOrEmpty(r.subAsset.assetName)
                                        && affectedNames.Contains(r.subAsset.assetName)
                                        && MatchCompany(r))
                            .ToList();
                    }

                    // ✅ Client: เก็บเป็น "event ของวันถัดไป" + โชว์ Flavor/Panel ได้
                    nextDayMarketEvent = actualEvent;
                    nextDayMarketEventCompany = companySO;
                    nextDayAffected = affectedRts ?? new List<InvestmentCompanyRuntime>();

                    if (actualEvent != null)
                    {
                        var eventPanelMgr = FindAnyObjectByType<EventPanelManager>();
                        if (eventPanelMgr != null)
                        {
                            eventPanelMgr.ShowMarketEvent(actualEvent);
                        }

                        var flavorUI = DailyFlavorTextUI.Instance;
                        if (flavorUI != null)
                        {
                            flavorUI.ShowFlavorForEvent(actualEvent);
                        }

                        LogFeed($"[ตลาด Sync] {description}: กระทบ [{string.Join(", ", affectedNames)}]");
                    }
                    else
                    {
                        Debug.LogWarning($"[MarketEvent Sync] ไม่พบ EventData ต้นฉบับ | company='{companyName}' desc='{description}' pct={pctChange}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[MarketEvent Sync] parse %change ไม่ได้: '{marketParts[2]}'");
                }
            }
            else
            {
                Debug.LogWarning($"[MarketEvent Sync] รูปแบบข้อมูลไม่ครบ: '{marketData}'");
            }
        }

        // ====== Player Event (per-player) ======
        if (!string.IsNullOrEmpty(playerData) && playerData != "NONE")
        {
            Debug.Log($"[PlayerEvent Sync] raw='{playerData}'");

            var playerParts = playerData.Split('|');
            if (playerParts.Length >= 1)
            {
                if (!string.Equals(playerParts[0], "PLAYER", System.StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogWarning($"[PlayerEvent Sync] รูปแบบ header ไม่ถูกต้อง: '{playerParts[0]}'");
                }
                else
                {
                    // เคลียร์ก่อนเก็บของใหม่
                    nextDayPlayerEvents.Clear();

                    for (int i = 1; i < playerParts.Length; i++)
                    {
                        var line = playerParts[i];
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        var fields = line.Split(',');
                        if (fields.Length < 3)
                        {
                            Debug.LogWarning($"[PlayerEvent Sync] segment ไม่ครบ: '{line}'");
                            continue;
                        }

                        string playerName = fields[0].Trim();

                        if (!float.TryParse(fields[1],
                                            NumberStyles.Float,
                                            invCulture,
                                            out float priceChange))
                        {
                            Debug.LogWarning($"[PlayerEvent Sync] parse price ไม่ได้: '{fields[1]}'");
                            continue;
                        }

                        string description = string.Join(",", fields.Skip(2)).Trim();

                        var targetPlayer = PlayerDataManager.Instance.players
                            .FirstOrDefault(p => p.playerName == playerName);

                        if (targetPlayer == null)
                        {
                            Debug.LogWarning($"[PlayerEvent Sync] ไม่พบ PlayerData สำหรับ '{playerName}'");
                            continue;
                        }

                        PlayerEventSO actualPlayerEvent = null;
                        if (playerEventLibrary != null && playerEventLibrary.events != null)
                        {
                            actualPlayerEvent = playerEventLibrary.events.FirstOrDefault(
                                e => e != null
                                  && e.description == description
                                  && Mathf.Approximately(e.price, priceChange)
                            );
                        }

                        if (actualPlayerEvent != null)
                        {
                            // ✅ Client: แค่ "เก็บ" event ไว้ใช้ตอนเริ่มวันใหม่ (ReceiveStartTimerFromHost)
                            nextDayPlayerEvents[playerName] = actualPlayerEvent;
                            Debug.Log($"[PlayerEvent Sync] Store for next day -> {playerName}: {description} ({priceChange:+0;-0})");
                        }
                        else
                        {
                            Debug.LogWarning(
                                $"[PlayerEvent Sync] ไม่พบ PlayerEventSO | " +
                                $"name='{playerName}' desc='{description}' price={priceChange}"
                            );
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[PlayerEvent Sync] รูปแบบข้อมูลไม่ครบ: '{playerData}'");
            }
        }

    }

    private IEnumerator WaitAndShowReport(DailyReportUI reportUI, PlayerData localPlayer)
    {
        float timeout = 2f;
        float elapsed = 0f;

        while (!nextDayPctSynced && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        int rankToDisplay = 0;
        var local = PlayerDataManager.Instance.localPlayer;
        if (local != null && syncedRanks.TryGetValue(local.playerName, out int hostRank))
        {
            rankToDisplay = hostRank;
        }

        ShowReportNow(reportUI, localPlayer, rankToDisplay);
    }
}
