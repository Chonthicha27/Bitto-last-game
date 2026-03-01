using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "PlayerEventLibrary", menuName = "Scriptable Objects/Player Event Library")]
public class PlayerEventLibrary : ScriptableObject
{
    public List<PlayerEventSO> events = new List<PlayerEventSO>();
}
