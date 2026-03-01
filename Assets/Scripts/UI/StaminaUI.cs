using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class StaminaUI : MonoBehaviour
{
    [Header("Dot Images")]
    public List<Image> dots = new List<Image>(); // ใส่ 3 dots ใน inspector

    [Header("Colors")]
    public Color fullColor = Color.green;
    public Color emptyColor = Color.gray;

    [Header("Stamina Alert UI")]
    public GameObject staminaPanel; // panel แจ้งเตือน (ซ่อนอยู่ตอนเริ่ม)
    public Button panelStamina;

    private Coroutine alertCoroutine;
    private Coroutine flashCoroutine;

    private void Start()
    {
        if (panelStamina != null)
        {
            panelStamina.onClick.AddListener(CloseStaminaAlert);
        }
    }

    // อัปเดต UI ตาม Stamina ปัจจุบัน
    public void UpdateStaminaUI(int currentStamina, int maxStamina)
    {
        for (int i = 0; i < dots.Count; i++)
        {
            if (dots[i] == null) continue;

            // เปลี่ยนสีแทนการเปลี่ยน sprite
            dots[i].color = (i < currentStamina) ? fullColor : emptyColor;

            // ซ่อน dot ที่เกิน maxStamina
            dots[i].enabled = i < maxStamina;
        }
    }

    // ✅ เรียกตอนกดปุ่มซื้อ/ขาย เพื่อเช็ค Stamina (และตัดด้วย)
    public bool TryUseStamina(int amount, System.Action onFail = null)
    {
        var player = PlayerDataManager.Instance.localPlayer;
        if (player.currentStamina >= amount)
        {
            player.currentStamina -= amount;
            UpdateStaminaUI(player.currentStamina, player.maxStamina);

            // ถ้าใช้จนเหลือ 0 → กระพิบครั้งนึงเตือนว่าหมดแล้ว
            if (player.currentStamina <= 0)
            {
                FlashNoStamina();
            }

            return true;
        }
        else
        {
            // stamina ไม่พอ → โชว์ panel + กระพิบทุกครั้งที่ยังดื้อกดซื้อ/ขาย
            ShowStaminaAlert();
            onFail?.Invoke();
            return false;
        }
    }

    // ✅ เรียกตอนเริ่มวันใหม่ เพื่อรีเซ็ต Stamina
    public void ResetStamina()
    {
        var player = PlayerDataManager.Instance.localPlayer;
        player.currentStamina = player.maxStamina;
        UpdateStaminaUI(player.currentStamina, player.maxStamina);

        // ปิด Panel ถ้าเปิดอยู่
        if (staminaPanel != null)
        {
            staminaPanel.SetActive(false);
        }

        // หยุด effect กระพิบถ้ามี
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
        }

        // รีเซ็ตสี dot กลับเป็นปกติ
        foreach (var img in dots)
        {
            if (img == null) continue;
            img.color = fullColor;
        }
    }

    public void ShowStaminaAlert()
    {
        if (staminaPanel == null)
            return;

        // ถ้ามี coroutine เดิมอยู่ ให้หยุดก่อน
        if (alertCoroutine != null)
        {
            StopCoroutine(alertCoroutine);
        }

        staminaPanel.SetActive(true);

        // 🎯 จุดสำคัญ: ทุกครั้งที่ panel โผล่ → ให้ dots กระพิบแดง
        FlashNoStamina();

        alertCoroutine = StartCoroutine(HideAlertAfterDelay(1.2f));
    }

    public void CloseStaminaAlert()
    {
        if (staminaPanel != null)
        {
            staminaPanel.SetActive(false);
        }

        if (alertCoroutine != null)
        {
            StopCoroutine(alertCoroutine);
            alertCoroutine = null;
        }
    }

    private IEnumerator HideAlertAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (staminaPanel != null)
        {
            staminaPanel.SetActive(false);
        }
        alertCoroutine = null;
    }

    // ✨ กระพิบจุด stamina สีแดง (ไม่ใช้ LeanTween จะได้ไม่พึ่ง lib ภายนอก)
    public void FlashNoStamina()
    {
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
        }

        flashCoroutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        // เก็บสีเดิมไว้
        var originalColors = new Dictionary<Image, Color>();
        foreach (var img in dots)
        {
            if (img == null) continue;
            originalColors[img] = img.color;
        }

        int loops = 3;          // กระพิบ 3 ครั้ง
        float interval = 0.12f; // เวลาแต่ละจังหวะ

        for (int i = 0; i < loops; i++)
        {
            // เป็นแดง
            foreach (var img in dots)
            {
                if (img == null) continue;
                img.color = Color.red;
            }
            yield return new WaitForSeconds(interval);

            // กลับสีเดิม
            foreach (var img in dots)
            {
                if (img == null) continue;
                if (originalColors.TryGetValue(img, out var c))
                    img.color = c;
            }
            yield return new WaitForSeconds(interval);
        }

        flashCoroutine = null;
    }
}
