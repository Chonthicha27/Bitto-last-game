using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// คุม End Credit Panel:
/// - เปิด panel
/// - หน่วง delay ตามที่ตั้ง
/// - เลื่อนทั้ง panel จากล่างขึ้นบน
/// - พอจบแล้วเฟดหาย และปิด panel
/// - รองรับปุ่ม Skip เพื่อข้ามเครดิตแล้วไปหน้า Home
/// </summary>
public class EndCreditController : MonoBehaviour
{
    public static EndCreditController Instance;

    [Header("Root / UI")]
    [Tooltip("Panel หลักของ End Credit (ทั้งก้อนที่จะเลื่อนขึ้น)")]
    public GameObject panelRoot;

    [Tooltip("CanvasGroup สำหรับเฟดหาย (ควรอยู่บน panelRoot)")]
    public CanvasGroup canvasGroup;

    [Header("Skip Button")]
    [Tooltip("ปุ่ม Skip (เช่น Button-Skip ที่อยู่ใต้ EndCredit)")]
    public Button skipButton;

    [Header("Timing")]
    [Tooltip("หน่วงเวลาก่อนเริ่มเลื่อน (เช่น 5 วินาทีหลัง Rank Panel ขึ้น)")]
    public float delayBeforeStart = 5f;

    [Tooltip("เวลาในการเลื่อนเครดิตจากล่างขึ้นบน")]
    public float scrollDuration = 15f;

    [Tooltip("เวลาในการเฟดหายหลังเลื่อนจบ")]
    public float fadeDuration = 1.5f;

    [Header("Movement")]
    [Tooltip("ตำแหน่ง Y ตอนเริ่ม (อยู่ล่างจอ)")]
    public float startOffsetY = -600f;

    [Tooltip("ตำแหน่ง Y ตอนสุดท้าย (อยู่บนจอ / พ้นจอ)")]
    public float endOffsetY = 600f;

    [Header("Tween Curve")]
    [Tooltip("โค้งตอนเลื่อนเครดิต")]
    public AnimationCurve scrollCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("โค้งตอนเฟดหาย")]
    public AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Next Scene")]
    [Tooltip("ชื่อซีนหน้า Home (เปลี่ยนได้ใน Inspector)")]
    public string homeSceneName = "Home";
    
    [Tooltip("รายชื่อ Scene ที่มีข้อมูลเกมที่ต้องถูกเคลียร์ (เช่น Game)")]
    public string[] scenesToCleanup = { "Game" }; 

    private RectTransform panelRect;
    private Vector2 originalAnchoredPos;

    private void Awake()
    {
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
            panelRect = panelRoot.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                originalAnchoredPos = panelRect.anchoredPosition;
            }

            // ซ่อน Panel ตอนเริ่มเกม
            panelRoot.SetActive(false);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }

        // ผูกปุ่ม Skip และซ่อนไว้ตอนเริ่มเกม
        if (skipButton != null)
        {
            skipButton.onClick.RemoveAllListeners();
            skipButton.onClick.AddListener(OnSkipPressed);
            skipButton.gameObject.SetActive(false);   // ⬅ ซ่อนปุ่ม Skip ตอนเริ่ม
        }
    }

    /// <summary>
    /// เริ่มเล่น End Credit (จะจัดการ delay / เลื่อน / เฟดหายให้เอง)
    /// </summary>
    public void PlayCredits()
    {
        if (panelRoot == null)
        {
            Debug.LogWarning("[EndCreditController] panelRoot ยังไม่ถูกเซ็ต");
            return;
        }

        if (panelRect == null)
        {
            panelRect = panelRoot.GetComponent<RectTransform>();
            if (panelRect == null)
            {
                Debug.LogWarning("[EndCreditController] ไม่เจอ RectTransform บน panelRoot");
                return;
            }
        }

        // กันเคสถูกเรียกซ้ำ
        LeanTween.cancel(panelRoot);

        // เปิด panel + ปุ่ม Skip
        panelRoot.SetActive(true);
        if (skipButton != null)
            skipButton.gameObject.SetActive(true);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }

        float x = panelRect.anchoredPosition.x;
        Vector2 startPos = new Vector2(x, startOffsetY);  // อยู่ล่าง
        Vector2 endPos = new Vector2(x, endOffsetY);      // เลื่อนไปบน

        panelRect.anchoredPosition = startPos;

        // delay ก่อนเริ่มเลื่อน
        LeanTween.delayedCall(panelRoot, delayBeforeStart, () =>
        {
            // 1) เลื่อนทั้ง panel ขึ้น
            LeanTween.value(panelRoot, 0f, 1f, scrollDuration)
                     .setEase(scrollCurve)
                     .setOnUpdate((float t) =>
                     {
                         panelRect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
                     })
                     .setOnComplete(() =>
                     {
                         // 2) เลื่อนจบ → เฟดหายแล้วไปหน้า Home
                         StartFadeOutAndFinish();
                     });
        });
    }

    /// <summary>
    /// กดปุ่ม Skip → ข้ามเครดิตทันที (เฟดออกเร็วๆ แล้วเปลี่ยนซีน)
    /// </summary>
    private void OnSkipPressed()
    {
        if (panelRoot == null) return;

        // ยกเลิก Tween ที่กำลังเล่น
        LeanTween.cancel(panelRoot);

        // เฟดแล้วเปลี่ยนซีน
        StartFadeOutAndFinish();
    }

    /// <summary>
    /// เฟดหายแล้วปิด panel + reset ตำแหน่ง + โหลดซีน Home
    /// </summary>
    private void StartFadeOutAndFinish()
    {
        if (panelRoot == null)
        {
            GoToHomeScene();
            return;
        }

        if (canvasGroup != null)
        {
            // กัน Tween ซ้อน
            LeanTween.cancel(canvasGroup.gameObject);

            LeanTween.value(canvasGroup.gameObject, canvasGroup.alpha, 0f, fadeDuration)
                     .setEase(fadeCurve)
                     .setOnUpdate(a =>
                     {
                         canvasGroup.alpha = a;
                     })
                     .setOnComplete(() =>
                     {
                         CleanupPanelState();
                         GoToHomeScene();
                     });
        }
        else
        {
            CleanupPanelState();
            GoToHomeScene();
        }
    }

    private void CleanupPanelState()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (panelRect != null)
            panelRect.anchoredPosition = originalAnchoredPos;

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        if (skipButton != null)
            skipButton.gameObject.SetActive(false);   // ⬅ ซ่อนปุ่ม Skip หลังจบ/Skip
    }
    private void GoToHomeScene()
    {
        // 0. ปิด Server / Host ก่อน
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.StopServer(); // ต้องมีฟังก์ชัน StopServer() ที่ปิด TcpListener และ client ทุกตัว
            NetworkManager.Instance.DisconnectClient(); // optional ถ้าเป็น client ก็ disconnect
            
            // 💥 ต้อง Destroy NetworkManager ด้วย
            Destroy(NetworkManager.Instance.gameObject);
            NetworkManager.Instance = null;
            
            Debug.Log("[EndCreditController] ปิด Server/Client เรียบร้อย");
        }
        // 1. Destroy PlayerDataManager
        if (PlayerDataManager.Instance != null)
        {
            Destroy(PlayerDataManager.Instance.gameObject);
            PlayerDataManager.Instance = null;
            Debug.Log("[EndCreditController] Destroy PlayerDataManager แล้ว");
        }
        // 2. (Optional) การวนลูปเพื่อ debug/จัดการ Scene เฉพาะ
        foreach (string sceneName in scenesToCleanup)
        {
            Debug.Log($"[EndCreditController] Scene '{sceneName}' ถือว่าถูกเคลียร์ข้อมูลแล้ว");
        }
        // 3. โหลด Scene Homepage (Scene แรกของเกม)
        if (!string.IsNullOrEmpty(homeSceneName))
        {
            SceneManager.LoadScene(homeSceneName);
            Debug.Log($"[EndCreditController] กำลังโหลด Scene: {homeSceneName}");
        }
        else
        {
            Debug.LogWarning("[EndCreditController] homeSceneName ว่างอยู่ ยังไม่ได้ตั้งชื่อซีน");
        }
    }

    /// <summary>
    /// ใช้หยุดเครดิตกลางทางด้วยโค้ด (ไม่เปลี่ยนซีน)
    /// </summary>
    public void StopCredits()
    {
        if (panelRoot == null) return;

        LeanTween.cancel(panelRoot);
        CleanupPanelState();
        // ไม่เรียก GoToHomeScene() เพราะไว้ใช้ debug / editor
    }
}
