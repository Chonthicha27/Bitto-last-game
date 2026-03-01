using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class DailyFlavorTextUI : MonoBehaviour
{
    public static DailyFlavorTextUI Instance;

    [Header("UI")]
    [Tooltip("Prefab / Panel กล่องข้อความคำโปรย (มี Background + Text)")]
    public GameObject panelRoot;

    [Tooltip("Text ที่ใช้แสดงคำโปรย")]
    public TMP_Text flavorTextLabel;

    [Header("Data")]
    [Tooltip("SO ที่เก็บคำโปรยทั้งหมด")]
    public FlavorTextLibrarySO flavorLibrary;

    // 🔹 ตั้งค่าอนิเมชัน Popup / Hide
    [Header("Popup Animation")]
    [Tooltip("เวลาอนิเมชันตอนเด้งขึ้น")]
    public float popupDuration = 0.3f;

    [Tooltip("เวลาอนิเมชันตอนหุบลง")]
    public float hideDuration = 0.2f;

    [Tooltip("Curve ตอนเด้งขึ้น")]
    public AnimationCurve popupCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("Curve ตอนหุบลง")]
    public AnimationCurve hideCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Behavior")]
    [Tooltip("ถ้าใช้ครบทุกอันแล้ว จะยอมวนกลับไปสุ่มซ้ำได้ไหม (เผื่อเกมเล่นยาวกว่าจำนวนข้อความ)")]
    public bool allowRepeatWhenExhausted = false;

    // เก็บ index ที่ยังไม่ถูกใช้ในรอบนี้
    private readonly List<int> unusedIndexes = new List<int>();

    void Awake()
    {
        // ทำเป็น singleton แบบง่าย ๆ ไว้ให้ TurnManager หรือปุ่มอื่นเรียกได้
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (panelRoot != null)
        {
            panelRoot.SetActive(false);                    // เริ่มเกมให้ซ่อนไว้ก่อน
            panelRoot.transform.localScale = Vector3.zero; // เตรียมสเกลไว้สำหรับป๊อปอัป
        }
    }

    void Start()
    {
        RefillIndexes(); // เติม index รอบแรกตอนเริ่มเกม
    }

    /// <summary>
    /// เติมลิสต์ index ใหม่ทั้งหมด (ใช้ตอนเริ่มเกม หรือถ้าต้อง reset รอบใหม่)
    /// </summary>
    private void RefillIndexes()
    {
        unusedIndexes.Clear();

        if (flavorLibrary == null || flavorLibrary.flavorTexts == null)
        {
            Debug.LogWarning("[DailyFlavorTextUI] ยังไม่ได้เซ็ต FlavorTextLibrarySO");
            return;
        }

        for (int i = 0; i < flavorLibrary.flavorTexts.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(flavorLibrary.flavorTexts[i]))
                unusedIndexes.Add(i);
        }

        if (unusedIndexes.Count == 0)
        {
            Debug.LogWarning("[DailyFlavorTextUI] ไม่มีคำโปรยให้ใช้เลยใน FlavorTextLibrary");
        }
    }

    /// <summary>
    /// เรียกโชว์คำโปรยใหม่ 1 อันแบบสุ่ม (ไม่ซ้ำจนกว่าจะใช้ครบ)
    /// </summary>
    public void ShowRandomFlavorText()
    {
        if (flavorLibrary == null || flavorLibrary.flavorTexts == null || flavorLibrary.flavorTexts.Count == 0)
        {
            Debug.LogWarning("[DailyFlavorTextUI] ไม่มีข้อมูลคำโปรยใน Library");
            return;
        }

        // ✅ ถ้าคำโปรยหมดแล้ว
        if (unusedIndexes.Count == 0)
        {
            if (allowRepeatWhenExhausted)
            {
                Debug.Log("[DailyFlavorTextUI] ใช้คำโปรยครบทุกอันแล้ว → refill ใหม่ (อนุญาตให้ซ้ำได้)");
                RefillIndexes();
            }
            else
            {
                Debug.Log("[DailyFlavorTextUI] ใช้คำโปรยครบทุกอันแล้วในรอบนี้ จะไม่สุ่มซ้ำจนกว่าจะ Reset / เริ่มเกมใหม่");
                return;
            }
        }

        if (unusedIndexes.Count == 0)
        {
            // กันเคสที่ยังไม่มีข้อมูลจริง ๆ
            return;
        }

        // สุ่ม index จาก unusedIndexes
        int listIndex = Random.Range(0, unusedIndexes.Count);
        int msgIndex = unusedIndexes[listIndex];
        unusedIndexes.RemoveAt(listIndex);

        string msg = flavorLibrary.flavorTexts[msgIndex];

        if (flavorTextLabel != null)
            flavorTextLabel.text = msg;

        if (panelRoot != null)
        {
            // ปิด Tween เก่าก่อน กันเด้งซ้อน
            LeanTween.cancel(panelRoot);

            panelRoot.SetActive(true);
            panelRoot.transform.localScale = Vector3.zero;

            LeanTween.scale(panelRoot, Vector3.one, popupDuration)
                     .setEase(popupCurve);
        }
    }

    /// <summary>
    /// ซ่อน Panel คำโปรย (ไว้เรียกจากปุ่ม Close)
    /// </summary>
    public void Hide()
    {
        if (panelRoot == null || !panelRoot.activeSelf)
            return;

        LeanTween.cancel(panelRoot);
        LeanTween.scale(panelRoot, Vector3.zero, hideDuration)
                 .setEase(hideCurve)
                 .setOnComplete(() =>
                 {
                     panelRoot.SetActive(false);
                 });
    }

    /// <summary>
    /// ใช้รีเซ็ต “รอบใหม่” ตอนเริ่มเกมใหม่ หรือกลับมาหน้า Lobby แล้วเริ่ม match ใหม่
    /// </summary>
    public void ResetFlavorCycle()
    {
        RefillIndexes();
    }

    // ⭐ ใหม่: ใช้ข้อความจาก Event โดยตรง
    public void ShowFlavorForEvent(EventData evt)
    {
        if (evt == null || panelRoot == null || flavorTextLabel == null)
            return;

        // ถ้า flavorText ว่าง ให้ fallback เป็น description
        string msg = string.IsNullOrWhiteSpace(evt.flavorText)
            ? evt.description
            : evt.flavorText;

        flavorTextLabel.text = msg;

        // เล่นอนิเมชันเด้งขึ้น เหมือนใน ShowRandomFlavorText
        LeanTween.cancel(panelRoot);
        panelRoot.SetActive(true);
        panelRoot.transform.localScale = Vector3.zero;

        LeanTween.scale(panelRoot, Vector3.one, popupDuration)
                 .setEase(popupCurve);
    }
}
