using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 런을 넘어 유지되는 메타 진행 (Spec 6-3장): 영혼 보유량, 영구 강화 레벨, 기록.
// Application.persistentDataPath/greedbound_save.json 에 JSON으로 저장. 값이 바뀔 때마다 즉시 저장.
public static class MetaProgress
{
    [System.Serializable]
    class SaveData
    {
        public int souls;
        public int totalSouls;          // 지금까지 모은 영혼 (사용분 포함)
        public int bestRound;
        public int runs;
        public List<int> levels = new List<int>();  // 인덱스 = MetaUpgradeId 값
        public List<int> unlockedSkills = new List<int>();  // SkillId 값 (기본 해금 스킬은 저장하지 않음)
        public int selectedSkill;                           // SkillId 값, 기본 0 = 회전베기
    }

    const string FileName = "greedbound_save.json";

    static SaveData data;

    public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

    // 에디터에서 도메인 리로드를 꺼도 플레이 시작마다 파일에서 다시 읽게
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => data = null;

    static SaveData Data
    {
        get
        {
            if (data == null) Load();
            return data;
        }
    }

    public static int Souls => Data.souls;
    public static int TotalSouls => Data.totalSouls;
    public static int BestRound => Data.bestRound;
    public static int Runs => Data.runs;

    public static int Level(MetaUpgradeId id)
    {
        int index = (int)id;
        return index < Data.levels.Count ? Data.levels[index] : 0;
    }

    public static bool IsMaxed(MetaUpgradeId id) => Level(id) >= MetaCatalog.Get(id).MaxLevel;

    // 다음 레벨 비용. 최대 레벨이면 -1
    public static int NextCost(MetaUpgradeId id)
    {
        MetaCatalog.Upgrade upgrade = MetaCatalog.Get(id);
        int level = Level(id);
        return level < upgrade.MaxLevel ? upgrade.costs[level] : -1;
    }

    public static bool TryBuy(MetaUpgradeId id)
    {
        int cost = NextCost(id);
        if (cost < 0 || Data.souls < cost) return false;

        int index = (int)id;
        while (Data.levels.Count <= index) Data.levels.Add(0);
        Data.levels[index]++;
        Data.souls -= cost;
        Save();
        return true;
    }

    // ───────────── 스킬 (Spec 5장 "스킬") ─────────────

    public static SkillId SelectedSkill
    {
        get
        {
            SkillId id = (SkillId)Data.selectedSkill;
            return System.Enum.IsDefined(typeof(SkillId), id) && IsSkillUnlocked(id) ? id : SkillId.Whirlwind;
        }
    }

    public static bool IsSkillUnlocked(SkillId id)
    {
        return SkillCatalog.Get(id).unlockCost <= 0 || Data.unlockedSkills.Contains((int)id);
    }

    public static bool TryUnlockSkill(SkillId id)
    {
        if (IsSkillUnlocked(id)) return false;
        int cost = SkillCatalog.Get(id).unlockCost;
        if (Data.souls < cost) return false;

        Data.souls -= cost;
        Data.unlockedSkills.Add((int)id);
        Save();
        return true;
    }

    public static void SelectSkill(SkillId id)
    {
        if (!IsSkillUnlocked(id)) return;
        Data.selectedSkill = (int)id;
        Save();
    }

    // 런 종료(사망) 시 한 번 호출
    public static void RecordRun(int souls, int round)
    {
        Data.souls += souls;
        Data.totalSouls += souls;
        Data.runs++;
        Data.bestRound = Mathf.Max(Data.bestRound, round);
        Save();
    }

    // 새 런 시작 시 영구 강화 적용 — 시작 물약·골드·리롤, 최대 체력·단검
    public static void ApplyRunStart(RunState run, PlayerHealth player)
    {
        run.potions += Level(MetaUpgradeId.EmergencyPotion) * MetaCatalog.PotionsPerLevel;
        run.gold += Level(MetaUpgradeId.TravelFunds) * MetaCatalog.GoldPerLevel;
        run.rerollsLeft += Level(MetaUpgradeId.FateReroll) * MetaCatalog.RerollsPerLevel;

        if (player == null) return;
        PlayerStats stats = player.GetComponent<PlayerStats>();
        if (stats != null)
        {
            stats.bonusMaxHealth += Level(MetaUpgradeId.VitalBody) * MetaCatalog.HealthPerLevel;
            stats.bonusDaggers += Level(MetaUpgradeId.SpareDagger) * MetaCatalog.DaggersPerLevel;
        }
        player.SetHealth(player.MaxHealth);   // 늘어난 최대 체력으로 시작
    }

    static void Load()
    {
        data = new SaveData();
        try
        {
            if (File.Exists(SavePath)) JsonUtility.FromJsonOverwrite(File.ReadAllText(SavePath), data);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"MetaProgress: 저장 파일을 읽지 못해 새로 시작합니다 ({SavePath})\n{e.Message}");
            data = new SaveData();
        }
    }

    static void Save()
    {
        try
        {
            File.WriteAllText(SavePath, JsonUtility.ToJson(Data, true));
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"MetaProgress: 저장 실패 ({SavePath})\n{e.Message}");
        }
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Greed Bound/저장 초기화 (영혼·영구 강화)")]
    static void ResetSaveMenu()
    {
        if (!UnityEditor.EditorUtility.DisplayDialog("저장 초기화", $"영혼·영구 강화·기록을 모두 지웁니다.\n{SavePath}", "초기화", "취소")) return;
        if (File.Exists(SavePath)) File.Delete(SavePath);
        data = null;
        Debug.Log("메타 진행 저장을 초기화했습니다.");
    }
#endif
}
