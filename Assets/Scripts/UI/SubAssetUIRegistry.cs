using System.Collections.Generic;

public static class SubAssetUIRegistry
{
    private static readonly Dictionary<SubAssetData, SubAssetUI> map = new();

    public static void Register(SubAssetData sa, SubAssetUI ui)
    {
        if (sa == null || ui == null) return;
        map[sa] = ui; // อัปเดตตัวล่าสุดเสมอ
    }

    public static void Unregister(SubAssetData sa, SubAssetUI ui)
    {
        if (sa == null || ui == null) return;
        if (map.TryGetValue(sa, out var cur) && cur == ui)
            map.Remove(sa);
    }

    public static bool TryGetPrice(SubAssetData sa, out float price)
    {
        price = 0f;
        if (sa == null) return false;
        if (map.TryGetValue(sa, out var ui) && ui != null)
        {
            price = ui.CurrentDisplayedPrice; // ✅ ราคาเดียวกับที่แสดงบนจอจริง
            return true;
        }
        return false;
    }

    public static bool TryGetUI(SubAssetData sa, out SubAssetUI ui) => map.TryGetValue(sa, out ui);
}
