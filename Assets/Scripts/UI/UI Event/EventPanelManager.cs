using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EventPanelManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject eventPanel;            // แผงใหญ่
    public GameObject marketEventPanel;      // Panel สำหรับ Market
    public GameObject playerEventPanel;      // Panel สำหรับ Player

    [Header("Close Buttons")]
    public Button marketCloseButton; // ปุ่มที่อยู่บน Market Event Panel
    public Button playerCloseButton; // ปุ่มที่อยู่บน Player Event Panel

    [Header("Market UI")]
    public Image marketEventImage;
    public TMP_Text marketEventTopicText;
    public TMP_Text marketEventDescriptionText;

    [Header("Player UI")]
    public Image playerEventImage;
    public TMP_Text playerEventTotalText;
    public TMP_Text playerEventDescriptionText;

    [Header("LeanTween Settings")]
    [SerializeField] public float slideDuration; // เวลาในการเลื่อน (ค่าใน Inspector คือ 3.0)
    [SerializeField] public float slideDistance; // ระยะเลื่อนลง (คุณอาจต้องปรับใน Inspector เป็น 800-1000)
    public AnimationCurve slideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f); // AnimationCurve

    private Vector3 originalPos;      // ตำแหน่งแสดงผลสมบูรณ์
    private Vector3 hiddenStartPos;   // ตำแหน่งเริ่มต้นที่ซ่อนอยู่

    public static EventPanelManager Instance;

    // 🆕 จำ Event ล่าสุดทั้งสองแบบ
    private EventData lastMarketEvent;
    private PlayerEventSO lastPlayerEvent;

    void Awake()
    {
        if (eventPanel != null)
        {
            originalPos = eventPanel.transform.localPosition;
            hiddenStartPos = originalPos - new Vector3(0, slideDistance, 0);
            eventPanel.transform.localPosition = hiddenStartPos;
            eventPanel.SetActive(false);
        }

        if (marketCloseButton != null)
        {
            marketCloseButton.onClick.AddListener(HideAllPanels);
        }
        if (playerCloseButton != null)
        {
            playerCloseButton.onClick.AddListener(HideAllPanels);
        }

        Instance = this;
    }

    /// แสดง Event ของผู้เล่น (เลื่อนขึ้น)
    public void ShowPlayerEvent(PlayerEventSO playerEvent, string playerName = "")
    {
        if (playerEvent == null) return;

        // 🆕 จำ player event ล่าสุด
        lastPlayerEvent = playerEvent;

        if (eventPanel != null)
        {
            eventPanel.SetActive(true);
            ShowPanelAnimated();
        }

        // ❗ ตามเดิม: ถ้าเรียก ShowPlayerEvent แสดงว่ามี player event → เปิด panel ผู้เล่นแน่นอน
        if (playerEventPanel != null) playerEventPanel.SetActive(true);

        // จะให้ market panel เปิดด้วยหรือไม่ แล้วแต่ดีไซน์ ตอนนี้ตามเดิมของนัทคือเปิดทั้งคู่
        if (marketEventPanel != null) marketEventPanel.SetActive(true);

        if (playerEventImage != null) playerEventImage.sprite = playerEvent.icon;
        if (playerEventDescriptionText != null) playerEventDescriptionText.text = playerEvent.description;
        if (playerEventTotalText != null)
        {
            string sign = playerEvent.price >= 0 ? "+" : "-";
            playerEventTotalText.text = $"{sign}{Mathf.Abs(playerEvent.price):N0}";
        }
    }

    /// เปิด Market Event Panel (เลื่อนขึ้น)
    public void ShowMarketEvent(EventData evt)
    {
        Debug.Log($"ShowMarketEvent called with evt={evt?.description}");
        if (evt == null) return;

        // 🆕 จำ market event ล่าสุด
        lastMarketEvent = evt;

        // 🔊 NEW: เล่นเสียงกระดาษทุกครั้งที่มี Market Event โผล่
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(AudioManager.SoundType.Paper);
        }

        if (eventPanel != null)
        {
            eventPanel.SetActive(true);
            ShowPanelAnimated();
        }

        // ✅ Market event เกิดทุกวัน → เปิดเฉพาะ panel ตลาดก่อน
        if (marketEventPanel != null) marketEventPanel.SetActive(true);

        // ✅ วันไหนไม่มี player event เรา *อย่า* เปิด panel ผู้เล่น
        // ให้ panel ผู้เล่นถูกเปิดเฉพาะตอนมี ShowPlayerEvent() เท่านั้น
        if (playerEventPanel != null) playerEventPanel.SetActive(false);

        if (marketEventImage != null) marketEventImage.sprite = evt.eventIcon;
        if (marketEventTopicText != null) marketEventTopicText.text = evt.marketEventTopicText;
        if (marketEventDescriptionText != null) marketEventDescriptionText.text = evt.description;
    }

    // 🆕 เรียกจากปุ่มบน PlayerIcon → เปิด event ล่าสุด (ถ้ามี)
    public void ShowLastEvents()
    {
        if (lastMarketEvent == null && lastPlayerEvent == null)
        {
            Debug.Log("[EventPanelManager] ยังไม่มี Event ล่าสุดให้แสดง");
            return;
        }

        if (eventPanel != null)
        {
            eventPanel.SetActive(true);
            ShowPanelAnimated();
        }

        // ถ้ามี market event ล่าสุด → เติม UI ให้
        if (lastMarketEvent != null && marketEventPanel != null)
        {
            marketEventPanel.SetActive(true);

            if (marketEventImage != null) marketEventImage.sprite = lastMarketEvent.eventIcon;
            if (marketEventTopicText != null) marketEventTopicText.text = lastMarketEvent.marketEventTopicText;
            if (marketEventDescriptionText != null) marketEventDescriptionText.text = lastMarketEvent.description;
        }
        else if (marketEventPanel != null)
        {
            marketEventPanel.SetActive(false);
        }

        // ถ้ามี player event ล่าสุด → เติม UI ให้
        if (lastPlayerEvent != null && playerEventPanel != null)
        {
            playerEventPanel.SetActive(true);

            if (playerEventImage != null) playerEventImage.sprite = lastPlayerEvent.icon;
            if (playerEventDescriptionText != null) playerEventDescriptionText.text = lastPlayerEvent.description;
            if (playerEventTotalText != null)
            {
                string sign = lastPlayerEvent.price >= 0 ? "+" : "-";
                playerEventTotalText.text = $"{sign}{Mathf.Abs(lastPlayerEvent.price):N0}";
            }
        }
        else if (playerEventPanel != null)
        {
            playerEventPanel.SetActive(false);
        }
    }

    /// สั่ง Animation เลื่อนลง (ถูกเรียกเมื่อกดปุ่ม)
    private void HidePanelAnimated()
    {
        if (eventPanel == null) return;

        LeanTween.cancel(eventPanel, true); // true คือยกเลิกทุก Tween บน GameObject

        LeanTween
            .moveLocal(eventPanel, hiddenStartPos, slideDuration)
            .setEase(slideCurve)
            .setOnComplete(CompleteHide);
    }

    /// ปิด Panel และรีเซ็ตเมื่อ Animation จบ
    private void CompleteHide()
    {
        Debug.Log($"[CompleteHide] EventPanel localPos: {eventPanel.transform.localPosition} | hiddenStartPos: {hiddenStartPos}");
        if (eventPanel != null)
        {
            eventPanel.transform.localPosition = hiddenStartPos;
            eventPanel.SetActive(false);
        }

        if (marketEventPanel != null) marketEventPanel.SetActive(false);
        if (playerEventPanel != null) playerEventPanel.SetActive(false);

        var flavorUI = DailyFlavorTextUI.Instance;
        if (flavorUI != null)
        {
            flavorUI.ShowRandomFlavorText();   // ยังเหมือนเดิม
        }
    }

    public void HideAllPanels()
    {
        if (eventPanel == null || !eventPanel.activeSelf) return;
        HidePanelAnimated();
    }

    private void ShowPanelAnimated()
    {
        if (eventPanel == null) return;
        LeanTween.moveLocal(eventPanel, originalPos, slideDuration).setEase(slideCurve);
    }

    public void ShowPanelOnly()
    {
        if (eventPanel != null)
        {
            eventPanel.SetActive(true);
            ShowPanelAnimated();
        }

        // ตอนนี้ให้เปิดแต่ market panel ไว้
        if (marketEventPanel != null) marketEventPanel.SetActive(true);
        if (playerEventPanel != null) playerEventPanel.SetActive(false);
    }
}
