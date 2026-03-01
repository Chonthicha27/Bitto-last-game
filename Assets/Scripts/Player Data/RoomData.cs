using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RoomData
{
    public string roomCode;
    public List<PlayerData> Players = new List<PlayerData>();

    public RoomData(string code)
    {
        roomCode = code;
    }

    public void AddPlayer(PlayerData player)
    {
        if (!Players.Contains(player))
            Players.Add(player);
    }

    public void RemovePlayer(PlayerData player)
    {
        if (Players.Contains(player))
            Players.Remove(player);
    }
}
