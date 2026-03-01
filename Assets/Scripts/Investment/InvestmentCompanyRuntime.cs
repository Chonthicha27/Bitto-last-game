using UnityEngine;

public class InvestmentCompanyRuntime
{
    public InvestmentCompany data;   // SO บริษัทหลัก
    public SubAssetData subAsset;    // SubAsset ของบริษัท
    public float currentPrice;

    // กันสุ่มซ้ำ/โดนอีเวนต์ซ้ำในวันเดียวกัน
    public int lastPriceUpdateDay = -1;
    public int lastEventDay = -1;

    // ===== แยกเปอร์เซ็นต์เป็น 2 ส่วน =====
    public float lastDailyPct = 0f;   // % จากการเคลื่อนไหวปกติของ “วันนี้”
    public float lastEventPct = 0f;   // % จากอีเวนต์ของ “วันนี้”

    // % รวม (รักษาไว้เพื่อความเข้ากันได้กับโค้ดเดิมที่อาจอ่าน field นี้)
    public float lastChangePct = 0f;  // lastDailyPct + lastEventPct

    // ✅ % ที่ “พรีโรลไว้สำหรับวันถัดไป”
    public float nextDayPct = 0f;
    public bool hasNextDayPct = false;

    // อ้างอิง UI (อย่าให้ UI ไปสุ่มเอง)
    public SubAssetUI ui;

    public InvestmentCompanyRuntime(InvestmentCompany data, SubAssetData subAsset)
    {
        this.data = data;
        this.subAsset = subAsset;

        currentPrice = Mathf.Max(0.01f, subAsset.currentPrice);
        currentPrice = Floor2(currentPrice);

        lastDailyPct = 0f;
        lastEventPct = 0f;
        lastChangePct = 0f;

        nextDayPct = 0f;
        hasNextDayPct = false;
    }

    // ------------------------------------------------------------
    // ใช้ตอน "เริ่มวัน" เท่านั้น
    // ------------------------------------------------------------

    /// <summary>
    /// สุ่มราคาโดยใช้ช่วง % จาก SubAsset (minPrice..maxPrice) — ทำได้ "ครั้งเดียว/วัน"
    /// </summary>
    public void ApplyDailyRandomMoveForDay(int dayIndex)
    {
        // ⭐ FIX: ถ้ามี NetworkManager และไม่ใช่ Server → ไม่ควรสุ่มเอง
        if (NetworkManager.Instance != null && !NetworkManager.Instance.isServer)
        {
            Debug.LogWarning($"[InvestmentRuntime] ApplyDailyRandomMoveForDay ถูกเรียกจาก Client → ข้าม (ให้ Host เป็นคนจัดการเท่านั้น)");
            return;
        }

        if (lastPriceUpdateDay == dayIndex) return; // ⛔ เคยอัปเดตวันนี้แล้ว

        GetSafeRange(out float min, out float max);
        float pct = RandPct(min, max);
        ApplyDailyPct(dayIndex, pct, logTag: "PriceTick");
    }

    /// <summary>
    /// ใช้ Event ที่เป็น % คงที่ (evt.priceChange) — ทำได้ "ครั้งเดียว/วัน" ต่อบริษัท
    /// </summary>
    public void ApplyEventForDay(EventData evt, int dayIndex)
    {
        // ⭐ FIX: Event ฝั่งราคาให้ Host เป็นคนใช้เท่านั้น
        if (NetworkManager.Instance != null && !NetworkManager.Instance.isServer)
        {
            Debug.LogWarning($"[InvestmentRuntime] ApplyEventForDay ถูกเรียกจาก Client → ข้าม (ให้ Host เป็นคนจัดการเท่านั้น)");
            return;
        }

        if (evt == null) return;
        if (lastEventDay == dayIndex) return; // ⛔ วันนี้โดนไปแล้ว

        float pct = Floor2(evt.priceChange);

        // ปรับราคา
        currentPrice *= 1f + (pct / 100f);
        if (!float.IsFinite(currentPrice) || currentPrice < 0.01f) currentPrice = 0.01f;
        currentPrice = Floor2(currentPrice);

        // สะสม % อีเวนต์ของวันนี้
        lastEventDay = dayIndex;
        lastEventPct += pct;

        // อัปเดตค่า “รวม” เพื่อความเข้ากันได้
        lastChangePct = lastDailyPct + lastEventPct;

        // อัปเดต UI รวมอีเวนต์เข้าไปด้วย
        ui?.UpdatePrice(currentPrice, lastDailyPct, lastEventPct);

        Debug.Log($"[Event D{dayIndex}] {subAsset.assetName} {evt.description} {pct:+0.##;-0.##}% → {currentPrice:N2}");
    }

    // ------------------------------------------------------------
    // ✅ พรีโรล/โอเวอร์ไรด์ % สำหรับ "วันถัดไป" + คอมมิตตอนเริ่มวัน
    // ------------------------------------------------------------

    /// <summary>สุ่ม % สำหรับ "วันถัดไป" เก็บไว้ที่ nextDayPct</summary>
    public void PreRollNextDayPct()
    {
        // ⭐ FIX: ให้สุ่มเฉพาะ Host หรือโหมด Offline เท่านั้น
        if (NetworkManager.Instance != null && !NetworkManager.Instance.isServer)
        {
            // Client จะได้ค่า nextDayPct ผ่าน SyncEventsFromHost แทน
            return;
        }

        GetSafeRange(out float min, out float max);

        if (Mathf.Approximately(min, max) && !Mathf.Approximately(min, 0f))
            nextDayPct = Floor2(min);
        else
            nextDayPct = RandPct(min, max);

        hasNextDayPct = true;
    }

    /// <summary>โอเวอร์ไรด์ % ของวันถัดไป (ใช้ตอนมี Market Event วันถัดไป)</summary>
    public void OverrideNextDayPct(float pct)
    {
        // ⭐ FIX: ตรงนี้ให้ทั้ง Host และ Client ใช้ได้
        // - Host: เรียกจาก PreRollNextDayPercents เพื่อเซ็ตค่าแท้จริง
        // - Client: เรียกจาก SyncEventsFromHost เพื่อ sync ให้เหมือน Host

        nextDayPct = Floor2(pct);
        hasNextDayPct = true;
    }

    /// <summary>
    /// ตอนเริ่มวัน: ใช้ nextDayPct ที่พรีโรลไว้ (ถ้าไม่มีให้สุ่มสด) แล้วอัปเดตราคา/เปอร์เซ็นต์
    /// </summary>
    public float ConsumeNextDayPctOrRollNow(int dayIndex)
    {
        // ⭐ FIX: ให้ Host/Offline เท่านั้นเป็นคน "คอมมิต" ราคา
        if (NetworkManager.Instance != null && !NetworkManager.Instance.isServer)
        {
            Debug.LogWarning($"[InvestmentRuntime] ConsumeNextDayPctOrRollNow ถูกเรียกจาก Client → ข้าม (ราคา/เปอร์เซ็นต์ต้อง sync มาจาก Host)");
            return 0f;
        }

        float pct = hasNextDayPct ? nextDayPct : RandPctFromSO();

        // 🔄 เริ่มวันใหม่ → รีเซ็ต % อีเวนต์ของวันนี้ก่อน
        lastEventPct = 0f;

        ApplyDailyPct(dayIndex, pct, logTag: $"Commit D{dayIndex}");

        // เคลียร์พรีโรลหลังใช้
        nextDayPct = 0f;
        hasNextDayPct = false;

        return pct;
    }

    /// <summary>อ่านค่า % วันถัดไปแบบปลอดภัย (ไม่มี = 0)</summary>
    public float PeekNextDayPct() => hasNextDayPct ? nextDayPct : 0f;

    // ------------------------------------------------------------
    // เมธอดเดิม (ไม่ผูกวัน) — คงไว้เพื่อความเข้ากันได้
    // ------------------------------------------------------------

    [System.Obsolete("เลิกใช้ ApplyDailyRandomMove(); ให้ใช้ ApplyDailyRandomMoveForDay(dayIndex) ตอนเริ่มวันแทน")]
    public void ApplyDailyRandomMove()
    {
        GetSafeRange(out float min, out float max);
        float pct = RandPct(min, max);

        // ถือว่าเป็นการเคลื่อนไหวประจำวัน
        ApplyDailyPct(dayIndex: lastPriceUpdateDay, pct, logTag: "Tick(legacy)");
    }

    [System.Obsolete("เลิกใช้ ApplyEvent(evt); ให้ใช้ ApplyEventForDay(evt, dayIndex) ตอนเริ่มวันแทน")]
    public void ApplyEvent(EventData evt)
    {
        if (evt == null) return;

        float pct = Floor2(evt.priceChange);
        currentPrice *= 1f + (pct / 100f);

        if (!float.IsFinite(currentPrice) || currentPrice < 0.01f) currentPrice = 0.01f;
        currentPrice = Floor2(currentPrice);

        // legacy: นับเป็นอีเวนต์ของวันนี้
        lastEventPct += pct;
        lastChangePct = lastDailyPct + lastEventPct;

        ui?.UpdatePrice(currentPrice, lastDailyPct, lastEventPct);
        Debug.Log($"{subAsset.assetName} EVENT(legacy) {pct:+0.##;-0.##}% → {currentPrice:N2}");
    }

    // ---------------------------
    // Helpers
    // ---------------------------

    private static float Floor2(float v) => Mathf.Floor(v * 100f) / 100f;

    private void GetSafeRange(out float min, out float max)
    {
        min = subAsset.minPrice;
        max = subAsset.maxPrice;

        if (Mathf.Approximately(min, 0f) && Mathf.Approximately(max, 0f))
        {
            min = -5f; max = 5f;
        }
    }

    private float RandPctFromSO()
    {
        GetSafeRange(out float min, out float max);
        return RandPct(min, max);
    }

    private float RandPct(float min, float max)
    {
        if (Mathf.Approximately(min, max))
            return Floor2(min);

        return Floor2(Random.Range(min, max));
    }

    /// <summary>
    /// ใช้เมื่อเป็น “การเคลื่อนไหวรายวัน” (ไม่รวมอีเวนต์)
    /// </summary>
    private void ApplyDailyPct(int dayIndex, float pct, string logTag)
    {
        currentPrice *= 1f + (pct / 100f);
        if (!float.IsFinite(currentPrice) || currentPrice < 0.01f) currentPrice = 0.01f;
        currentPrice = Floor2(currentPrice);

        lastPriceUpdateDay = dayIndex;

        // เก็บเป็น % รายวัน
        lastDailyPct = pct;

        // อัปเดตค่า “รวม” เพื่อความเข้ากันได้
        lastChangePct = lastDailyPct + lastEventPct;

        // แจ้ง UI โดยส่ง daily + event แยกกัน (UI จะรวมเอง)
        ui?.UpdatePrice(currentPrice, lastDailyPct, lastEventPct);

        Debug.Log($"[{logTag}] {subAsset.assetName} Δ{pct:+0.##;-0.##}% (daily), event {lastEventPct:+0.##;-0.##}% → {currentPrice:N2}");
    }
}
