using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class PlayerDataManager : MonoBehaviour
{
    public static PlayerDataManager Instance;

    [Header("Local Player Data")]
    public PlayerData localPlayer;
    
    [Header("Room Data")]
    public RoomData currentRoom;

    [Header("Character Sprites")]
    public Sprite[] characterSprites;

    [Header("Skill Tree")]
    public SkillTreeData localPlayerSkillTree = new SkillTreeData();

    [HideInInspector] 
    public List<PlayerData> players = new List<PlayerData>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadCharacterSprites();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void LoadCharacterSprites()
    {
        characterSprites = Resources.LoadAll<Sprite>("CharacterSprites");
        if (characterSprites == null || characterSprites.Length == 0)
        {
            Debug.LogError("ไม่พบ Sprite ใน Resources/CharacterSprites/");
        }
    }

    public void InitializeLocalPlayer(string name, int spriteIndex)
    {
        localPlayer = new PlayerData(name, spriteIndex);
        localPlayerSkillTree = new SkillTreeData();

        Debug.Log($"LocalPlayer Created: {name}, SpriteIndex={spriteIndex}");
        AddPlayer(localPlayer);
    }

    public Sprite GetCharacterSprite(int index)
    {
        if (index < 0 || index >= characterSprites.Length)
            return null;
        return characterSprites[index];
    }
    
    // ✅ ให้เรียกใช้ AddPlayer ได้ทั้ง Server/Client
    public void AddPlayer(PlayerData player)
    {
        Debug.Log($"[PlayerDataManager] AddPlayer: {player.playerName}");
        
        if (player == null)
        {
            Debug.LogError("[PlayerDataManager] AddPlayer: player เป็น null!");
            return;
        }
        // สร้าง List ถ้ายังเป็น null
        if (players == null)
        {
            players = new List<PlayerData>();
        }
        
        if (!players.Exists(p => p.playerName == player.playerName))
        {
            players.Add(player);
            Debug.Log("[PlayerDataManager] AddPlayer (players): " + player.playerName);
        }
        // เพิ่ม player เข้า currentRoom.Players ถ้ายังมี currentRoom และยังไม่มี player
        if (currentRoom != null)
        {
            if (currentRoom.Players == null)
                currentRoom.Players = new List<PlayerData>();

            if (!currentRoom.Players.Exists(p => p.playerName == player.playerName))
            {
                currentRoom.Players.Add(player);
                Debug.Log("[PlayerDataManager] AddPlayer (currentRoom): " + player.playerName);
            }
        }
    }
    public Dictionary<string, int> CalculateRanks(InvestmentManager inv) // (พารามิเตอร์ inv อาจไม่จำเป็นแล้ว)
    {
        if (players == null || !players.Any())
        {
            return new Dictionary<string, int>();
        }

        // 1. จัดเรียงผู้เล่นตาม TotalWealth (ความมั่งคั่งสุทธิ) จากมากไปน้อย
        // 💡 TotalWealth ใน PlayerData.cs = player.money (decimal) + TotalInvestmentAssetValue (decimal)
        var sortedPlayers = players
            .OrderByDescending(p => p.TotalWealth) // ⬅️ แก้ไข: ใช้ TotalWealth แทน GetTotalAssets(inv)
            .ToList();

        var ranks = new Dictionary<string, int>();

        // 2. กำหนดอันดับ (Rank)
        for (int i = 0; i < sortedPlayers.Count; i++)
        {
            int rank = i + 1;
            ranks[sortedPlayers[i].playerName] = rank;

            // ✅ อัปเดตอันดับใน PlayerData โดยตรง (สำคัญสำหรับ Host)
            sortedPlayers[i].rank = rank; 
        }

        return ranks;
    }
    public void ResetAll()
    {
        localPlayer = null;
        players.Clear();
        currentRoom = null;
    }

}
