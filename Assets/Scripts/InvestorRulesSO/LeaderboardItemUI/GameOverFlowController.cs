// GameOverFlowController.cs
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverFlowController : MonoBehaviour
{
    [Header("Refs in Gameplay Scene")]
    public InvestmentManager investmentManager;  // ตัวเดียวกับที่ใช้ในเกม

    [Header("Ranking Scene")]
    public string rankingSceneName = "Ranking";

    public void EndMatchAndShowRanking()
    {
        if (PlayerDataManager.Instance?.players == null || PlayerDataManager.Instance.players.Count == 0)
        {
            Debug.LogWarning("[GameOverFlow] ไม่มีผู้เล่นในห้อง");
            return;
        }

        SceneManager.sceneLoaded += OnRankingSceneLoaded;
        SceneManager.LoadScene(rankingSceneName);
    }

    private void OnRankingSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnRankingSceneLoaded;

        var ui = FindObjectOfType<GameOverSummaryUI>(includeInactive: true);
        if (ui == null) { Debug.LogError("[GameOverFlow] ไม่พบ GameOverSummaryUI ในหน้า Ranking"); return; }

        // หา InvestmentManager ในซีนใหม่ ถ้าไม่มี ใช้ตัวที่โยงมาจาก Gameplay
        var invInScene = FindObjectOfType<InvestmentManager>() ?? investmentManager;
        if (invInScene == null) { Debug.LogError("[GameOverFlow] ไม่พบ InvestmentManager"); return; }

        ui.ShowWithLeaderboard(PlayerDataManager.Instance.players, invInScene);
    }
}
