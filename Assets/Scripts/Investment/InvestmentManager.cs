using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;

public class InvestmentManager : MonoBehaviour
{
    [Header("Company Data")]
    public InvestmentCompany[] companyAssets; // ข้อมูลบริษัททั้งหมด
    public List<InvestmentCompanyRuntime> activeCompanies = new List<InvestmentCompanyRuntime>();

    [Header("UI Prefabs")]
    public GameObject companyUIPrefab;        // Prefab รายละเอียดบริษัท
    public GameObject bankGoldUIPrefab;    // ใช้กับ Bank / Gold
    public Transform companyDetailParent;     // ตำแหน่งที่จะวาง UI รายละเอียดบริษัท

    [Header("ปุ่มบริษัทที่มีใน Scene")]
    public Button[] companyButtons; // ใส่ปุ่มที่สร้างไว้ใน Scene ด้วยมือ

    // ✅ กัน “อีเวนต์หุ้นซ้ำทั้งเกม”
    private HashSet<EventData> usedMarketEvents = new HashSet<EventData>();
    public void ResetUsedMarketEvents() => usedMarketEvents.Clear();

    public static InvestmentManager Instance;

    [Header("Company Library")]
    public InvestmentCompanyLibrary companyLibrary;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // สร้าง runtime data สำหรับทุก SubAsset ของทุกบริษัท
        foreach (var companyData in companyAssets)
        {
            if (companyData != null && companyData.subAssets != null && companyData.subAssets.Count > 0)
            {
                foreach (var sa in companyData.subAssets)
                {
                    var rt = new InvestmentCompanyRuntime(companyData, sa);
                    activeCompanies.Add(rt);
                }
            }
        }

        SetupCompanyButtons();
    }

    // 🔊 helper: เล่นเสียงปุ่ม
    private void PlayButtonClick()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(AudioManager.SoundType.Button);
        }
    }

    public void OpenCompanyUI(InvestmentCompanyRuntime runtime)
    {
        GameObject prefab = companyUIPrefab;

        if (runtime.data.companyType == CompanyType.Bank || runtime.data.companyType == CompanyType.Gold)
            prefab = bankGoldUIPrefab;

        var uiObj = Instantiate(prefab, companyDetailParent);

        if (prefab == companyUIPrefab)
        {
            var ui = uiObj.GetComponent<CompanyUI>();
            ui.SetCompany(runtime);
        }
        else
        {
            var bankUI = uiObj.GetComponent<BankGoldUI>();

            // ตั้งค่า text เดิม
            bankUI.descriptionText.text = runtime.data.descriptionAssets;

            // 🔗 ผูกข้อมูล Bank เข้ากับ UI
            bankUI.bankSubAsset = runtime.subAsset; // subAsset ของ Bank
            bankUI.investmentManager = this;             // อ้างถึง InvestmentManager ตัวใน scene
        }
    }



    private void SetupCompanyButtons()
    {
        // ✅ ผูกปุ่มกับ "รายชื่อบริษัท" โดยตรง (1 ปุ่ม = 1 Company SO)
        for (int i = 0; i < companyButtons.Length; i++)
        {
            if (i < companyAssets.Length && companyAssets[i] != null)
            {
                int idx = i;
                var company = companyAssets[idx];

                companyButtons[idx].onClick.RemoveAllListeners();
                companyButtons[idx].onClick.AddListener(() =>
                {
                    // 🔊 เล่นเสียงก่อนเปิดหน้าบริษัท
                    PlayButtonClick();
                    SelectCompanyByAsset(company);
                });

                var btnText = companyButtons[idx].GetComponentInChildren<Text>();
                if (btnText != null) btnText.text = company.companyName;

                Debug.Log($"ผูกปุ่ม {idx} กับบริษัท {company.companyName}");
            }
            else
            {
                companyButtons[i].onClick.RemoveAllListeners();
                companyButtons[i].interactable = false;
                var t = companyButtons[i].GetComponentInChildren<Text>();
                if (t != null) t.text = "ไม่มีบริษัท";
            }
        }
    }

    // ----------------------------------------------------------------
    // สุ่ม “บริษัทหนึ่ง + อีเวนต์หนึ่ง” โดยข้ามอีเวนต์ที่ถูกใช้ไปแล้ว (ไม่ซ้ำทั้งเกม)
    // จากนั้น apply ไปยัง sub-assets ตาม TargetMode ของ EventData
    // ----------------------------------------------------------------
    public EventData TriggerRandomEventNoRepeat(
    HashSet<EventData> usedEvents,
    out InvestmentCompany company,
    out List<InvestmentCompanyRuntime> affectedRuntimes
)
    {
        company = null;
        affectedRuntimes = null;

        // ถ้าไม่มี company เลย ก็ไม่ต้องทำอะไร
        if (companyAssets == null || companyAssets.Length == 0)
            return null;

        // คัดเฉพาะบริษัทที่ยังมีอีเวนต์เหลือ (ทั้ง good / bad ที่ยังไม่อยู่ใน usedEvents)
        var availableCompanies = companyAssets.Where(c =>
        {
            if (c == null) return false;

            bool hasGood = (c.goodEvents != null) &&
                           c.goodEvents.Any(e => e != null && !usedEvents.Contains(e));

            bool hasBad = (c.badEvents != null) &&
                          c.badEvents.Any(e => e != null && !usedEvents.Contains(e));

            return hasGood || hasBad;
        }).ToList();

        if (availableCompanies.Count == 0)
            return null;

        // สุ่มบริษัทจากกลุ่มที่ยังมีอีเวนต์เหลือ
        company = availableCompanies[Random.Range(0, availableCompanies.Count)];

        // สร้าง pool อีเวนต์ที่ยังไม่ถูกใช้ (แยก good / bad)
        var goodRemain = (company.goodEvents ?? new EventData[0])
            .Where(e => e != null && !usedEvents.Contains(e))
            .ToList();

        var badRemain = (company.badEvents ?? new EventData[0])
            .Where(e => e != null && !usedEvents.Contains(e))
            .ToList();

        if (goodRemain.Count == 0 && badRemain.Count == 0)
            return null;

        // เลือกจาก good/bad ที่ยังเหลือแบบสุ่ม
        List<EventData> pool;
        if (goodRemain.Count > 0 && badRemain.Count > 0)
        {
            pool = (Random.value > 0.5f) ? goodRemain : badRemain;
        }
        else
        {
            pool = (goodRemain.Count > 0) ? goodRemain : badRemain;
        }

        var selectedEvent = pool[Random.Range(0, pool.Count)];

        // มาร์คว่าใช้ event นี้แล้ว (กันซ้ำทั้งเกม)
        usedEvents.Add(selectedEvent);

        // ❗ จุดสำคัญ: ตอนนี้ "ยังไม่ apply ผลอะไรกับราคา"
        // เราแค่จำว่า event นี้จะไปโดน runtime ไหนบ้าง (ของบริษัทที่ถูกเลือก)
        affectedRuntimes = GetAffectedRuntimesForCompany(company);

        // คืน EventData ให้คนเรียก (TurnManager จะเอาไปเก็บเป็น nextDayMarketEvent)
        return selectedEvent;
    }



    // ✅ เลือกบริษัทด้วยตัว SO โดยตรง
    public void SelectCompanyByAsset(InvestmentCompany companyAsset)
    {
        if (companyAsset == null)
        {
            Debug.LogWarning("SelectCompanyByAsset: companyAsset เป็น null");
            return;
        }

        // หา runtime ตัวใดตัวหนึ่งของบริษัทนี้ (เพื่อส่งเข้าหน้า CompanyUI ที่รับ runtime)
        var anyRt = activeCompanies.FirstOrDefault(r => r != null && r.data == companyAsset);

        if (anyRt == null)
        {
            // ถ้ายังไม่มี runtime (กรณีพิเศษ) สร้าง temp runtime จาก sub-asset ตัวแรก
            if (companyAsset.subAssets != null && companyAsset.subAssets.Count > 0)
            {
                anyRt = new InvestmentCompanyRuntime(companyAsset, companyAsset.subAssets[0]);
            }
            else
            {
                Debug.LogWarning($"บริษัท {companyAsset.companyName} ไม่มี SubAssets");
                return;
            }
        }

        OnCompanyButtonClicked(anyRt);
    }

    // (ยังรองรับวิธีเก่า ถ้าตรง index มาจากที่อื่น)
    public void SelectCompany(int activeIndex)
    {
        if (activeIndex < 0 || activeIndex >= activeCompanies.Count)
        {
            Debug.LogWarning($"Index {activeIndex} ไม่ถูกต้อง!");
            return;
        }
        OnCompanyButtonClicked(activeCompanies[activeIndex]);
    }

    private void OnCompanyButtonClicked(InvestmentCompanyRuntime companyRt)
    {
        // ลบ UI เดิมก่อน
        foreach (Transform child in companyDetailParent)
        {
            Destroy(child.gameObject);
        }

        // สร้าง UI ใหม่
        /*GameObject uiObj = Instantiate(companyUIPrefab, companyDetailParent);
        CompanyUI ui = uiObj.GetComponent<CompanyUI>();
        ui.SetCompany(companyRt); // CompanyUI ของคุณอ่าน subAssets ผ่าน companyRt.data อยู่แล้ว*/
        
        // สร้าง UI ใหม่โดยเรียกฟังก์ชัน OpenCompanyUI
        OpenCompanyUI(companyRt);


        Debug.Log($"แสดงข้อมูลบริษัท: {companyRt.data.companyName}");
    }

    // ----------------------------------------------------------------
    // อีเวนต์ตลาด (คงไว้เพื่อความเข้ากันได้)
    // ----------------------------------------------------------------

    public void TriggerRandomEvent()
    {
        InvestmentCompanyRuntime _;
        TriggerRandomEvent(out _);
    }

    public EventData TriggerRandomEvent(out InvestmentCompanyRuntime targetCompany)
    {
        targetCompany = null;
        if (activeCompanies == null || activeCompanies.Count == 0) return null;

        var anyRt = activeCompanies[Random.Range(0, activeCompanies.Count)];
        var evt = ApplyRandomEventToCompany(anyRt.data, out var affected);

        targetCompany = affected != null && affected.Count > 0 ? affected[0] : null;
        return evt;
    }

    // ✅ ใหม่: สุ่ม “บริษัทหนึ่ง + event หนึ่ง” แบบไม่ซ้ำทั้งเกม
    public EventData TriggerRandomEvent(out InvestmentCompany company, out List<InvestmentCompanyRuntime> affectedRuntimes)
    {
        company = null;
        affectedRuntimes = null;

        if (companyAssets == null || companyAssets.Length == 0) return null;

        // เลือกบริษัทที่ยังมีอีเวนต์เหลือ
        var availableCompanies = companyAssets.Where(c =>
        {
            if (c == null) return false;
            bool hasGood = (c.goodEvents != null) && c.goodEvents.Any(e => e != null && !usedMarketEvents.Contains(e));
            bool hasBad = (c.badEvents != null) && c.badEvents.Any(e => e != null && !usedMarketEvents.Contains(e));
            return hasGood || hasBad;
        }).ToList();

        if (availableCompanies.Count == 0) return null;

        company = availableCompanies[Random.Range(0, availableCompanies.Count)];

        // เลือก pool ที่ยังเหลือ
        var goodRemain = (company.goodEvents ?? new EventData[0]).Where(e => e != null && !usedMarketEvents.Contains(e)).ToList();
        var badRemain = (company.badEvents ?? new EventData[0]).Where(e => e != null && !usedMarketEvents.Contains(e)).ToList();

        if (goodRemain.Count == 0 && badRemain.Count == 0) return null;

        List<EventData> pool = (goodRemain.Count > 0 && badRemain.Count > 0)
            ? (Random.value > 0.5f ? goodRemain : badRemain)
            : (goodRemain.Count > 0 ? goodRemain : badRemain);

        var selectedEvent = pool[Random.Range(0, pool.Count)];
        usedMarketEvents.Add(selectedEvent); // ✅ กันซ้ำ

        return ApplyEventToCompany(company, selectedEvent, out affectedRuntimes);
    }

    // ใช้อีเวนต์ “ตัวที่กำหนด” กับบริษัทที่กำหนด (กระทบ sub-assets ตาม TargetMode)
    public EventData ApplyEventToCompany(InvestmentCompany company, EventData selectedEvent, out List<InvestmentCompanyRuntime> affectedRuntimes)
    {
        affectedRuntimes = new List<InvestmentCompanyRuntime>();
        if (company == null || selectedEvent == null) return null;

        var targets = ResolveTargets(company, selectedEvent);

        foreach (var sa in targets)
        {
            var rt = activeCompanies.FirstOrDefault(r => r.data == company && r.subAsset == sa);
            if (rt != null)
            {
                rt.ApplyEvent(selectedEvent);
                affectedRuntimes.Add(rt);
            }
        }

        if (affectedRuntimes.Count > 0)
        {
            var names = string.Join(", ", affectedRuntimes.Select(r => r.subAsset.assetName));
            Debug.Log($"{selectedEvent.description} → {company.companyName} [{names}] Δ{selectedEvent.priceChange:+0.##;-0.##}%");
        }
        else
        {
            Debug.Log($"อีเวนต์ {selectedEvent.description} ไม่พบเป้าหมายใน {company.companyName}");
        }

        return selectedEvent;
    }

    // เดิม: สุ่ม good/bad (ยังสุ่มซ้ำได้ ใช้ภายใน)
    private EventData ApplyRandomEventToCompany(InvestmentCompany company, out List<InvestmentCompanyRuntime> affectedRuntimes)
    {
        affectedRuntimes = new List<InvestmentCompanyRuntime>();
        if (company == null) return null;

        bool isGood = Random.value > 0.5f;
        EventData[] events = isGood ? company.goodEvents : company.badEvents;
        if (events == null || events.Length == 0) return null;

        EventData selectedEvent = events[Random.Range(0, events.Length)];

        var targets = ResolveTargets(company, selectedEvent);
        foreach (var sa in targets)
        {
            var rt = activeCompanies.FirstOrDefault(r => r.data == company && r.subAsset == sa);
            if (rt != null)
            {
                rt.ApplyEvent(selectedEvent);
                affectedRuntimes.Add(rt);
            }
        }

        if (affectedRuntimes.Count > 0)
        {
            var names = string.Join(", ", affectedRuntimes.Select(r => r.subAsset.assetName));
            Debug.Log($"{(isGood ? "ข่าวดี" : "ข่าวร้าย")} {selectedEvent.description} → {company.companyName} [{names}] Δ{selectedEvent.priceChange:+0.##;-0.##}%");
        }

        return selectedEvent;
    }

    // เลือก SubAssets ตาม EventData.TargetMode
    private List<SubAssetData> ResolveTargets(InvestmentCompany company, EventData evt)
    {
        var list = new List<SubAssetData>();
        if (company.subAssets == null || company.subAssets.Count == 0) return list;

        switch (evt.targetMode)
        {
            case EventData.TargetMode.AllSubAssets:
                list.AddRange(company.subAssets);
                break;

            case EventData.TargetMode.SpecificIndices:
                foreach (var idx in evt.targetIndices)
                    if (idx >= 0 && idx < company.subAssets.Count)
                        list.Add(company.subAssets[idx]);
                break;

            case EventData.TargetMode.SpecificNames:
                {
                    var set = new HashSet<string>(evt.targetNames.Where(n => !string.IsNullOrEmpty(n)));
                    foreach (var sa in company.subAssets)
                        if (set.Contains(sa.assetName))
                            list.Add(sa);
                    break;
                }

            case EventData.TargetMode.RandomN:
                {
                    int n = Mathf.Clamp(evt.randomCount, 1, company.subAssets.Count);
                    var pool = new List<SubAssetData>(company.subAssets);
                    for (int i = 0; i < n; i++)
                    {
                        int pick = Random.Range(0, pool.Count);
                        list.Add(pool[pick]);
                        pool.RemoveAt(pick);
                    }
                    break;
                }
        }
        return list;
    }

    // Utilities เดิม
    public void RandomEventForEachCompany()
    {
        foreach (var company in activeCompanies)
            ApplyRandomEventToCompany(company.data, out _);
    }

    public void SetButtonsInteractable(bool state)
    {
        foreach (var btn in companyButtons)
            btn.interactable = state;
    }

    public void TickAllPricesByPercent()
    {
        foreach (var c in activeCompanies)
            c.ApplyDailyRandomMove();
    }

    // ----------------------
    // ซื้อหุ้นแบบ Host-authoritative
    // ----------------------
    public void TryBuyStock(SubAssetData subAsset, int amount, string playerName)
    {
        if (NetworkManager.Instance != null && !NetworkManager.Instance.isServer)
        {
            // Client ส่งคำสั่งซื้อไปให้ Host
            string msg = $"BUY|{playerName}|{subAsset.assetName}|{amount}";
            NetworkManager.Instance.SendToServer(msg);
            Debug.Log($"[Client] ส่งคำสั่งซื้อไป Host: {msg}");
            return;
        }

        // Host ซื้อหุ้นทันที
        ExecuteBuy(subAsset, amount, playerName);
    }

    // BuySharesByCountFloat + ราคา runtime (เพื่อให้ตั้ง entryPrice)
    public void ExecuteBuy(SubAssetData subAsset, int amount, string playerName)
    {
        var player = PlayerDataManager.Instance.players.Find(p => p.playerName == playerName);
        if (player == null) { Debug.LogWarning($"[Host] ไม่พบ player {playerName}"); return; }

        var rt = GetOrCreateRuntime(subAsset);
        if (rt == null)
        {
            Debug.LogWarning("[Host] ไม่พบ runtime ของสินทรัพย์ (รวม bank/gold) ");
            return;
        }


        float sharesToBuy = (float)amount / 100f;
        float priceNow = rt.currentPrice;

        // คำนวณต้นทุนที่ต้องจ่าย (ใช้ PriceMath.Floor2 เพื่อปัดเศษราคา)
        decimal qty = (decimal)sharesToBuy;
        decimal price = (decimal)PriceMath.Floor2(priceNow);
        decimal cost = qty * price;

        // ตรวจสอบเงินคงเหลือ (HOST CHECK)
        if (player.money >= cost)
        {
            // 1. ดึงจำนวนหุ้นที่เหลืออยู่ (ก่อนซื้อ)
            float sharesBeforeTrade = player.GetOwnedAmount(subAsset);
            float sharesAfterTrade = sharesBeforeTrade + sharesToBuy; // คำนวณแบบจำลอง

            string moneyChangeStr = $"-{cost:0.00}"; // ส่ง cost เป็นลบ
            Debug.Log($"[Host: BUY SUCCESS] {playerName} ซื้อ {subAsset.assetName} x{sharesToBuy:0.00} | Cost: {cost:N2}");

            if (NetworkManager.Instance != null && NetworkManager.Instance.isServer)
            {
                string successMsg = $"BUY_RESULT|{playerName}|{subAsset.assetName}|{sharesAfterTrade:0.00}|{moneyChangeStr}|SUCCESS";

                // 1. Broadcast ให้ Clients ทั้งหมด
                NetworkManager.Instance.Broadcast(successMsg);

                // 2. บังคับ Host ให้ Parse ข้อความตัวเองทันที
                NetworkManager.Instance.RunOnMainThread(() =>
                {
                    NetworkManager.Instance.ParseMessage(successMsg);
                });
            }
        }
        else
        {
            Debug.Log($"[Host: BUY FAILED] {playerName} ซื้อไม่สำเร็จ (เงินไม่พอ/จำนวนไม่ถูกต้อง)");
            if (NetworkManager.Instance != null && NetworkManager.Instance.isServer)
            {
                string failMsg = $"BUY_RESULT|{playerName}|{subAsset.assetName}|0.00|0.00|FAILED";

                NetworkManager.Instance.Broadcast(failMsg);

                NetworkManager.Instance.RunOnMainThread(() =>
                {
                    NetworkManager.Instance.ParseMessage(failMsg);
                });
            }
        }
    }

    // ----------------------
    // ขายหุ้นแบบ Host-authoritative
    // ----------------------
    public void TrySellStock(SubAssetData subAsset, int amount, string playerName)
    {
        if (NetworkManager.Instance != null && !NetworkManager.Instance.isServer)
        {
            // 🚩 CLIENT LOGIC: ส่งคำสั่งขายไปให้ Host
            string msg = $"SELL|{playerName}|{subAsset.assetName}|{amount}";

            bool sendOK = NetworkManager.Instance.SendToServer(msg);

            if (sendOK)
            {
                Debug.Log($"[Client: {playerName}] SUCCESS: ส่งคำสั่งขาย {subAsset.assetName} x{amount} ไป Host");
            }
            else
            {
                Debug.LogError($"[Client: {playerName}] FAILED: ไม่สามารถส่งคำสั่งขาย {subAsset.assetName} ไป Host ได้ (Network connection error)");
            }

            return;
        }

        // HOST LOGIC: Host ขายหุ้นทันที
        ExecuteSell(subAsset, amount, playerName);
    }

    // SellSharesByCountFloat + ราคา runtime (เพื่อให้รีเซ็ตล็อต/entry ได้ถูก)
    public void ExecuteSell(SubAssetData subAsset, int amount, string playerName)
    {
        var player = PlayerDataManager.Instance.players.Find(p => p.playerName == playerName);
        if (player == null) { Debug.LogWarning($"[Host] ไม่พบ player {playerName}"); return; }

        var rt = GetOrCreateRuntime(subAsset);
        if (rt == null)
        {
            Debug.LogWarning("[Host] ไม่พบ runtime ของสินทรัพย์ (รวม bank/gold) ");
            return;
        }


        float sharesToSell = (float)amount / 100f;
        float priceNow = rt.currentPrice;

        // ตรวจสอบจำนวนหุ้นที่ถือครอง (HOST CHECK)
        if (player.GetOwnedAmount(subAsset) >= sharesToSell)
        {
            // 1. คำนวณรายรับที่ใช้จริง
            decimal qty = (decimal)sharesToSell;
            decimal price = (decimal)PriceMath.Floor2(priceNow);
            decimal income = qty * price;

            // 2. ดึงจำนวนหุ้นที่เหลืออยู่ (ก่อนขาย)
            float sharesBeforeTrade = player.GetOwnedAmount(subAsset);
            float sharesAfterTrade = sharesBeforeTrade - sharesToSell; // คำนวณแบบจำลอง

            string moneyChangeStr = $"{income:0.00}"; // ส่ง income เป็นบวก
            Debug.Log($"[Host: SELL SUCCESS] {playerName} ขาย {subAsset.assetName} x{sharesToSell:0.00} | Income: {income:N2}");

            if (NetworkManager.Instance != null && NetworkManager.Instance.isServer)
            {
                string successMsg = $"SELL_RESULT|{playerName}|{subAsset.assetName}|{sharesAfterTrade:0.00}|{moneyChangeStr}|SUCCESS";

                // 1. Broadcast ให้ Clients ทั้งหมด
                NetworkManager.Instance.Broadcast(successMsg);

                // 2. บังคับ Host ให้ Parse ข้อความตัวเองทันที
                NetworkManager.Instance.RunOnMainThread(() =>
                {
                    NetworkManager.Instance.ParseMessage(successMsg);
                });
            }
        }
        else
        {
            Debug.Log($"[Host: SELL FAILED] {playerName} ไม่มีหุ้นพอ/จำนวนไม่ถูกต้อง");
            if (NetworkManager.Instance != null && NetworkManager.Instance.isServer)
            {
                NetworkManager.Instance.SendToClient(playerName, $"SELL_RESULT|{playerName}|{subAsset.assetName}|0.00|0.00|FAILED");
            }
        }
    }

    // ⚠️ Note: คุณต้องเข้าถึง PriceMath.Floor2 ได้ ซึ่งคาดว่าคุณได้สร้าง Class Helper นี้ไว้แล้ว
    public static class PriceMath
    {
        public static float Floor2(float v) => Mathf.Floor(v * 100f) / 100f;
    }
    private InvestmentCompanyRuntime GetOrCreateRuntime(SubAssetData subAsset)
    {
        if (subAsset == null) return null;

        // 1. ลองหาจาก activeCompanies ก่อน
        var rt = activeCompanies.FirstOrDefault(r => r != null && r.subAsset == subAsset);
        if (rt != null) return rt;

        // 2. หา company ที่มี subAsset ตัวนี้อยู่ใน companyAssets
        var company = companyAssets
            .FirstOrDefault(c => c != null &&
                                 c.subAssets != null &&
                                 c.subAssets.Contains(subAsset));

        if (company != null)
        {
            // 3. สร้าง runtime ใหม่ให้ subAsset นี้ แล้วเก็บลง activeCompanies
            rt = new InvestmentCompanyRuntime(company, subAsset);
            activeCompanies.Add(rt);

            Debug.Log($"[InvestmentManager] สร้าง runtime ใหม่ให้บริษัท {company.companyName} / สินทรัพย์ {subAsset.assetName}");
            return rt;
        }

        // 4. เคสสุดท้าย: ไม่เจอ company ที่ถือ subAsset นี้เลย
        Debug.LogWarning($"[InvestmentManager] ไม่พบ company ที่มี subAsset: {subAsset.assetName}");
        return null;
    }
    // 🔍 คืน list ของ Runtime ที่ belong กับบริษัทนี้
    private List<InvestmentCompanyRuntime> GetAffectedRuntimesForCompany(InvestmentCompany company)
    {
        if (activeCompanies == null || company == null)
            return new List<InvestmentCompanyRuntime>();

        // ตอนนี้เราให้ event กระทบ "ทุก sub-asset ของบริษัทนั้น"
        // ถ้าทีหลังอยากแยกตาม TargetMode ค่อยขยาย logic ตรงนี้ต่อได้
        return activeCompanies
            .Where(rt => rt != null && rt.data == company)
            .ToList();
    }


}
