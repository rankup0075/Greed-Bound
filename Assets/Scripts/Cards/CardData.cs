using UnityEngine;

// 카드 계열 (Spec 3장). 에셋에 숫자로 저장되므로 값을 바꾸거나 순서를 끼워 넣지 말 것
public enum CardCategory
{
    Stat = 0,      // ① 능력 강화
    Behavior = 1,  // ② 행동 변화
    Swarm = 2,     // ③ 증식/군세
    Env = 3,       // ④ 환경/규칙
    Curse = 4,     // ⑤ 저주/특수
}

// 카드 효과 종류. 실제 동작은 CardEffects.Apply. 에셋에 숫자로 저장되므로 값 고정
public enum CardEffect
{
    Giant = 0, Rage = 1, Sprint = 2, IronSkin = 3, Indomitable = 4,
    Revenge = 10, ExplodingCorpse = 11, Regeneration = 12, Hunter = 13, Berserker = 14,
    Reinforcement = 20, Split = 21, Legion = 22, Summoning = 23, UndeadArmy = 24,
    Darkness = 30, BurningGround = 31, ManaStorm = 32, Seal = 33, DeathZone = 34,
    BloodPact = 40, DeathClock = 41, Gluttony = 42, MadBlessing = 43, HellGate = 44,
}

// 카드 1장 데이터 (Spec 9장). 25종 에셋은 메뉴 "Greed Bound > 카드 데이터 생성"으로 만듦
[CreateAssetMenu(menuName = "Greed Bound/Card Data", fileName = "Card")]
public class CardData : ScriptableObject
{
    public string id;
    public CardCategory category;
    public string displayName;
    [TextArea(2, 4)] public string description;
    public float risk;          // 위험도 증가
    public float reward;        // 보상 % 증가
    public CardEffect effect;

    public static string CategoryName(CardCategory category)
    {
        switch (category)
        {
            case CardCategory.Stat: return "능력 강화";
            case CardCategory.Behavior: return "행동 변화";
            case CardCategory.Swarm: return "증식/군세";
            case CardCategory.Env: return "환경/규칙";
            default: return "저주/특수";
        }
    }

    public static Color CategoryColor(CardCategory category)
    {
        switch (category)
        {
            case CardCategory.Stat: return new Color(0.95f, 0.55f, 0.3f);
            case CardCategory.Behavior: return new Color(0.7f, 0.5f, 0.95f);
            case CardCategory.Swarm: return new Color(0.5f, 0.85f, 0.45f);
            case CardCategory.Env: return new Color(0.4f, 0.7f, 1f);
            default: return new Color(0.95f, 0.3f, 0.4f);
        }
    }
}
