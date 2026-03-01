using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "FlavorTextLibrary", menuName = "Game/Flavor Text Library")]
public class FlavorTextLibrarySO : ScriptableObject
{
    [Header("รายการคำโปรยต่อวัน (วันละ 1 อัน)")]
    [TextArea(2, 4)]
    public List<string> flavorTexts = new List<string>();
}
