using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Text;

public class EventDebugFeed : MonoBehaviour
{
    public static EventDebugFeed Instance { get; private set; }

    [Header("UI Hookups")]
    [Tooltip("คอนเทนต์ของ VerticalLayout (เช่น Content ของ ScrollView)")]
    public Transform contentParent;

    [Tooltip("Prefab ที่มี TMP_Text หนึ่งตัว")]
    public GameObject linePrefab;

    [Header("Behavior")]
    [Tooltip("สูงสุดกี่บรรทัดในหน้าจอ")]
    public int maxLines = 30;

    [Tooltip("เวลาที่จะแสดงแต่ละบรรทัดก่อนเริ่มจาง (วินาที)")]
    public float showDuration = 4f;

    [Tooltip("ระยะเวลาเฟดหาย (วินาที)")]
    public float fadeDuration = 1.2f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ---------------------------
    // API ที่เรียกใช้จากระบบอื่น
    // ---------------------------
    public void Log(string msg)
    {
        if (contentParent == null || linePrefab == null) return;

        // สร้างบรรทัด
        var go = Instantiate(linePrefab, contentParent);
        var text = go.GetComponentInChildren<TMP_Text>();
        if (text != null) text.text = msg;

        // ตัดบรรทัดเกิน
        while (contentParent.childCount > maxLines)
        {
            Destroy(contentParent.GetChild(0).gameObject);
        }

        // เริ่มเฟด
        StartCoroutine(FadeAndDestroy(go));
    }

    public void LogMarketEvent(InvestmentCompanyRuntime target, EventData evt, float newPrice)
    {
        if (target == null || evt == null) return;
        var sb = new StringBuilder();
        sb.Append("[MarketEvent] ");
        sb.Append(target.data.companyName);
        sb.Append(" : ");
        sb.Append(evt.description);
        sb.Append(" (");
        sb.Append(evt.priceChange >= 0 ? "+" : "");
        sb.Append(evt.priceChange.ToString("0.##"));
        sb.Append("%) -> ");
        sb.Append(newPrice.ToString("N2"));
        Log(sb.ToString());
    }

    public void LogPlayerEvent(PlayerData player, PlayerEventSO evt)
    {
        if (player == null || evt == null) return;
        string sign = evt.price >= 0 ? "+" : "";
        Log($"[PlayerEvent] {player.playerName}: {evt.description} ({sign}{evt.price:N0}฿) -> เงิน {player.money:N0}฿");
    }

    public void LogTradeBuy(int playerIndex, string playerName, string companyName, float shares, float pricePerShare, float amountTHB)
    {
        Log($"[BUY] P{playerIndex + 1} {playerName} ซื้อ {shares:N2} หุ้น {companyName} @ {pricePerShare:N2} = {amountTHB:N0}฿");
    }

    public void LogTradeSell(int playerIndex, string playerName, string companyName, float shares, float pricePerShare, float receiveTHB)
    {
        Log($"[SELL] P{playerIndex + 1} {playerName} ขาย {shares:N2} หุ้น {companyName} @ {pricePerShare:N2} = {receiveTHB:N0}฿");
    }

    // ---------------------------
    // Fade & Destroy
    // ---------------------------
    IEnumerator FadeAndDestroy(GameObject go)
    {
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 1f;

        // รอแสดงเต็มก่อน
        yield return new WaitForSeconds(showDuration);

        // เฟดหาย
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            cg.alpha = Mathf.Lerp(1f, 0f, t / fadeDuration);
            yield return null;
        }

        Destroy(go);
    }
}
