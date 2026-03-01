using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class EndTurnButtonManager : MonoBehaviour
{
    [Header("🔘 endTurn")]
    public Button endTurnButton;        // ปุ่ม End of Turn

    [Header("🪟 Panel ยืนยัน")]
    public GameObject confirmPanel;     // Panel ที่มีปุ่มยืนยัน / ยกเลิก
    public Button confirmButton;        // ปุ่มยืนยัน End turn
    public Button cancelButton;         // ปุ่มยกเลิก

    [Header("🪄 Panel รอสิ้นสุดเทิร์น")]
    public GameObject waitingPanel;     // Panel เปล่าโชว์ตอนยืนยันแล้ว

    [Header("🚫 ปุ่มที่จะถูกปิดการใช้งานเมื่อยืนยัน")]
    public Button[] buttonsToDisable;   // ปุ่มที่ไม่ให้กดระหว่างรอ (ลากใส่ใน Inspector)

    [Header("⏰ ตัวจัดการเวลา")]
    public TurnManager turnManager;

    private bool hasConfirmed = false;
    
    public static EndTurnButtonManager Instance { get; private set; }
    
    void Awake()
    {
        // Singleton pattern
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
        if (endTurnButton != null)
        {
            endTurnButton.onClick.AddListener(OnEndTurnClicked);
        }
        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(OnConfirmClicked);
        }
        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(OnCancelClicked);
        }
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(false);
        }
        if (waitingPanel != null)
        {
            waitingPanel.SetActive(false);
        }
    }

    void Update()
    {
        // ถ้าเวลาหมดแล้ว ให้คืนสิทธิ์การกดปุ่ม
        if (turnManager != null && turnManager.dayDuration <= 0f)
        {
            EnableAllButtons();
            if (waitingPanel != null)
            {
                waitingPanel.SetActive(false);
            }
        }
    }

    // 🟢 เมื่อกดปุ่ม End Turn
    void OnEndTurnClicked()
    {
        if (hasConfirmed) 
        {
            return; // ป้องกันกดซ้ำ
        }
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(true);
        }
    }

    // ✅ ยืนยันจบเทิร์น
    private void OnConfirmClicked()
    {
        waitingPanel.SetActive(true);
        
        var turnManager = FindObjectOfType<TurnManager>();
        if (turnManager != null && turnManager.timerText != null)
        {
            // ตรวจสอบเพื่อป้องกัน Null Reference และอัปเดต Text
            turnManager.timerText.text = "0"; 
            Debug.Log("[EndTurnManager] ตั้งค่า Timer Text เป็น 0 เมื่อกดยืนยัน");
        }

        if (NetworkManager.Instance.isServer)
        {
            // Host กดเอง → บันทึกตัวเองก่อน
            string localName = PlayerDataManager.Instance.localPlayer.playerName;
            NetworkManager.Instance.MarkPlayerEndTurn(localName);
        }
        else
        {
            // Client กด → ส่งไป Host
            string localName = PlayerDataManager.Instance.localPlayer.playerName;
            NetworkManager.Instance.SendMessage($"EndTurn:{localName}");
        }
        
        confirmPanel.SetActive(false);
        
        
    }

    // ❌ ยกเลิกจบเทิร์น
    void OnCancelClicked()
    {
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(false);
        }
    }

    // 🟢 คืนสิทธิ์ให้ปุ่มทั้งหมด (เมื่อหมดเวลา)
    /*public void EnableAllButtons()
    {
        foreach (var btn in buttonsToDisable)
        {
            if (btn != null) btn.interactable = true;
        }

        hasConfirmed = false;
    }*/
    public void EnableAllButtons()
    {
        // 1. เปิดปุ่มทั้งหมดที่เคยถูกปิด
        foreach (var btn in buttonsToDisable)
        {
            if (btn != null) btn.interactable = true;
        }
    
        // 2. เปิดปุ่ม EndTurn หลักด้วย
        if (endTurnButton != null)
        {
            endTurnButton.interactable = true;
        }

        // 3. รีเซ็ตสถานะการยืนยัน
        hasConfirmed = false;
    
        // 4. ปิด Waiting Panel (ถ้ายังเปิดอยู่)
        if (waitingPanel != null)
        {
            waitingPanel.SetActive(false); 
        }
    
        Debug.Log("[EndTurnManager] ปุ่มทั้งหมดถูกคืนสิทธิ์การใช้งานแล้ว");
    }
    
    private HashSet<string> playersWaiting = new HashSet<string>();

    // เรียกจาก NetworkManager เมื่อ Client หรือ Host ใครกด EndTurn
    public void MarkPlayerWaiting(string playerName)
    {
        if (!playersWaiting.Contains(playerName))
            playersWaiting.Add(playerName);

        // แสดง waiting panel ถ้ามีผู้เล่นกดแล้ว
        /*if (waitingPanel != null)
            waitingPanel.SetActive(true);*/
    }

    // รีเซ็ต UI และ waiting list สำหรับวันถัดไป
    public void ResetForNextDay()
    {
        playersWaiting.Clear();
        if (waitingPanel != null)
            waitingPanel.SetActive(false);

        // คืนสิทธิ์ปุ่ม confirm
        if (confirmButton != null)
            confirmButton.interactable = true;
    }
    
    // 💡 NEW: เมธอดสำหรับปิดปุ่มทั้งหมดทันที (ใช้สำหรับ DayConfirm)
    public void DisableGameplayButtons()
    {
        foreach (var btn in buttonsToDisable)
        {
            if (btn != null) btn.interactable = false;
        }
        hasConfirmed = true; // มาร์คว่าผู้เล่นคนนี้จบเทิร์น/ยืนยันแล้ว
    
        // แสดง waiting panel ให้ผู้เล่นเห็นว่ากำลังรอคนอื่น
        if (waitingPanel != null)
        {
            waitingPanel.SetActive(true);
        }
    
        // ปิดปุ่ม EndTurn หลักด้วย
        if (endTurnButton != null)
        {
            endTurnButton.interactable = false;
        }
    }
    
}
