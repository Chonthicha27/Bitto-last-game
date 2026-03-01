using System;
using System.Collections.Generic;

[Serializable]
public class SkillTreeData
{
    public int skillPoints = 0;
    public Dictionary<string, int> unlockedSkills = new Dictionary<string, int>();

    public bool UnlockSkill(string skillId, int costPoints)
    {
        if (IsSkillUnlocked(skillId))
        {
            return false;
        }

        if (skillPoints < costPoints)
        {
            return false;
        }

        skillPoints -= costPoints;
        unlockedSkills[skillId] = 1;
        return true;
    }

    public bool IsSkillUnlocked(string skillId)
    {
        return unlockedSkills.ContainsKey(skillId);
    }
}