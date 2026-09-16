using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 에디터 메뉴 "Greed Bound > 카드 데이터 생성".
// Spec 3장 카드 표 25종을 Assets/Data/Cards/ 에 CardData 에셋으로 만들고(이미 있으면 값 갱신), CardLibrary에 모음.
// Spec 표를 바꾸면 아래 Rows도 같이 바꾸고 메뉴를 다시 실행할 것.
public static class CardDataGenerator
{
    const string Folder = "Assets/Data/Cards";
    const string LibraryPath = Folder + "/CardLibrary.asset";

    struct Row
    {
        public string id; public CardCategory category; public string name;
        public float risk; public float reward; public CardEffect effect; public string description;

        public Row(string id, CardCategory category, string name, float risk, float reward, CardEffect effect, string description)
        {
            this.id = id; this.category = category; this.name = name;
            this.risk = risk; this.reward = reward; this.effect = effect; this.description = description;
        }
    }

    // 설명은 플레이어에게 보이는 문장 (px 등 개발 단위 대신 체감 표현)
    static readonly Row[] Rows =
    {
        // ① 능력 강화
        new Row("stat_giant",        CardCategory.Stat, "거대화",    8, 25, CardEffect.Giant,       "적 최대 체력 ×1.5"),
        new Row("stat_rage",         CardCategory.Stat, "광폭화",   10, 25, CardEffect.Rage,        "적 공격력 ×1.4"),
        new Row("stat_sprint",       CardCategory.Stat, "질주",      7, 20, CardEffect.Sprint,      "적 이동속도 ×1.3"),
        new Row("stat_iron_skin",    CardCategory.Stat, "강철 피부", 8, 25, CardEffect.IronSkin,    "적이 받는 피해 ×0.7"),
        new Row("stat_indomitable",  CardCategory.Stat, "불굴",     10, 35, CardEffect.Indomitable, "체력 30% 이하인 적이 받는 피해 ×0.5"),

        // ② 행동 변화
        new Row("behavior_revenge",          CardCategory.Behavior, "복수",         11, 30, CardEffect.Revenge,         "적이 죽으면 남은 적의 공격력 ×1.25 (4초)"),
        new Row("behavior_exploding_corpse", CardCategory.Behavior, "폭발하는 시체", 14, 35, CardEffect.ExplodingCorpse, "적이 죽고 잠시 뒤 시체가 폭발. 주변 적에게는 더 큰 피해 (연쇄 폭발)"),
        new Row("behavior_regeneration",     CardCategory.Behavior, "재생",         10, 30, CardEffect.Regeneration,    "적이 초당 최대 체력의 10.8%를 회복"),
        new Row("behavior_hunter",           CardCategory.Behavior, "사냥꾼",        9, 25, CardEffect.Hunter,          "가까이 다가온 적의 이동속도 ×1.4"),
        new Row("behavior_berserker",        CardCategory.Behavior, "광전사",       15, 40, CardEffect.Berserker,       "동료가 죽은 비율만큼 적 공격력 증가"),

        // ③ 증식/군세
        new Row("swarm_reinforcement", CardCategory.Swarm, "증원",       12, 30, CardEffect.Reinforcement, "전투 시작 시 적 +2"),
        new Row("swarm_split",         CardCategory.Swarm, "분열",       14, 35, CardEffect.Split,         "적이 죽으면 소형 적 1마리가 나타남"),
        new Row("swarm_legion",        CardCategory.Swarm, "군단",       12, 25, CardEffect.Legion,        "적을 절반 처치하면 지원군이 한 번 몰려옴"),
        new Row("swarm_summoning",     CardCategory.Swarm, "소환술",     18, 40, CardEffect.Summoning,     "4.6초마다 적 1마리 소환"),
        new Row("swarm_undead_army",   CardCategory.Swarm, "죽음의 군세", 22, 50, CardEffect.UndeadArmy,    "죽은 적이 30% 확률로 2초 뒤 체력 50%로 부활 (1회)"),

        // ④ 환경/규칙
        new Row("env_darkness",       CardCategory.Env, "어둠",       13, 30, CardEffect.Darkness,      "시야가 좁아짐"),
        new Row("env_burning_ground", CardCategory.Env, "불타는 대지", 14, 35, CardEffect.BurningGround, "4.2초마다 예고 후 바닥에 화염 지대가 생김"),
        new Row("env_mana_storm",     CardCategory.Env, "마력 폭풍",   13, 35, CardEffect.ManaStorm,     "3.8초마다 예고 후 낙뢰가 떨어짐"),
        new Row("env_seal",           CardCategory.Env, "봉인",       16, 40, CardEffect.Seal,          "이번 라운드에만 물약·단검 사용 불가"),
        new Row("env_death_zone",     CardCategory.Env, "죽음의 영역", 20, 50, CardEffect.DeathZone,     "32초에 걸쳐 전장이 양쪽에서 좁아짐"),

        // ⑤ 저주/특수
        new Row("curse_blood_pact",   CardCategory.Curse, "피의 계약",   30,  70, CardEffect.BloodPact,   "플레이어 최대 체력 ×0.7 (영구)"),
        new Row("curse_death_clock",  CardCategory.Curse, "죽음의 시계", 34,  80, CardEffect.DeathClock,  "45초 안에 적을 모두 처치하지 못하면 즉사"),
        new Row("curse_gluttony",     CardCategory.Curse, "탐식",       24,  60, CardEffect.Gluttony,    "적이 가한 피해의 40%만큼 회복"),
        new Row("curse_mad_blessing", CardCategory.Curse, "광기의 축복", 45, 100, CardEffect.MadBlessing, "적 공격력 ×2.0. 대신 적 처치마다 골드 추가"),
        new Row("curse_hell_gate",    CardCategory.Curse, "지옥문",     50, 120, CardEffect.HellGate,    "정예 적 1마리 추가"),
    };

    [MenuItem("Greed Bound/카드 데이터 생성")]
    static void Generate()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Data", "Cards");

        CardLibrary library = AssetDatabase.LoadAssetAtPath<CardLibrary>(LibraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<CardLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }
        library.cards.Clear();

        int created = 0, updated = 0;
        foreach (Row row in Rows)
        {
            string path = $"{Folder}/{row.id}.asset";
            CardData card = AssetDatabase.LoadAssetAtPath<CardData>(path);
            if (card == null)
            {
                card = ScriptableObject.CreateInstance<CardData>();
                AssetDatabase.CreateAsset(card, path);
                created++;
            }
            else
            {
                updated++;
            }

            card.id = row.id;
            card.category = row.category;
            card.displayName = row.name;
            card.description = row.description;
            card.risk = row.risk;
            card.reward = row.reward;
            card.effect = row.effect;
            EditorUtility.SetDirty(card);

            library.cards.Add(card);
        }

        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();

        string linked = LinkLibraryInOpenScene(library) ? "\n열린 씬의 CardSelection에 CardLibrary 연결" : "";
        int implemented = 0;
        foreach (Row row in Rows) if (CardEffects.IsImplemented(row.effect)) implemented++;

        Debug.Log($"카드 데이터 생성 완료 — 새로 만듦 {created}, 갱신 {updated} (효과 구현 {implemented}/{Rows.Length}장){linked}");
    }

    static bool LinkLibraryInOpenScene(CardLibrary library)
    {
        CardSelection selection = Object.FindFirstObjectByType<CardSelection>();
        if (selection == null || selection.library == library) return false;

        Undo.RecordObject(selection, "카드 라이브러리 연결");
        selection.library = library;
        EditorSceneManager.MarkSceneDirty(selection.gameObject.scene);
        return true;
    }
}
