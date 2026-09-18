// 로비 영구 강화 종류 (Spec 6-3장). 저장 파일에 숫자로 기록되므로 값 고정
public enum MetaUpgradeId
{
    VitalBody = 0,        // 단련된 몸
    EmergencyPotion = 1,  // 비상 물약
    TravelFunds = 2,      // 여비
    SpareDagger = 3,      // 예비 단검
    FateReroll = 4,       // 운명 뒤집기
    GreedEye = 5,         // 탐욕의 눈
    WideSight = 6,        // 넓은 시야
    MerchantEye = 7,      // 상인의 안목
    ShopRefresh = 8,      // 상점 갱신
}

// 영구 강화 목록·레벨별 비용 (Spec 6-3장 "영혼 — Unity 구현 규칙").
// 소폭 강화 4종 + 편의 5종. 효과는 각 시스템이 MetaProgress.Level()로 조회.
public static class MetaCatalog
{
    public struct Upgrade
    {
        public readonly MetaUpgradeId id;
        public readonly string name;
        public readonly string description;   // 레벨당 효과
        public readonly int[] costs;          // 레벨 1, 2, ... 로 올리는 비용. 길이 = 최대 레벨

        public int MaxLevel => costs.Length;

        public Upgrade(MetaUpgradeId id, string name, string description, params int[] costs)
        {
            this.id = id;
            this.name = name;
            this.description = description;
            this.costs = costs;
        }
    }

    public static readonly Upgrade[] All =
    {
        new Upgrade(MetaUpgradeId.VitalBody,       "단련된 몸",   "시작 최대 체력 +10",                    40, 60, 80, 100, 120),
        new Upgrade(MetaUpgradeId.EmergencyPotion, "비상 물약",   "시작 물약 +1",                          60, 120),
        new Upgrade(MetaUpgradeId.TravelFunds,     "여비",       "시작 골드 +40",                         50, 90, 130),
        new Upgrade(MetaUpgradeId.SpareDagger,     "예비 단검",   "단검 최대치 +1",                        70, 130),
        new Upgrade(MetaUpgradeId.FateReroll,      "운명 뒤집기", "런마다 카드 리롤(R) +1회",               80, 140, 200),
        new Upgrade(MetaUpgradeId.GreedEye,        "탐욕의 눈",   "탐욕 단계에서도 리롤 사용 가능",         150),
        new Upgrade(MetaUpgradeId.WideSight,       "넓은 시야",   "카드 제시 3장 → 4장",                   300),
        new Upgrade(MetaUpgradeId.MerchantEye,     "상인의 안목", "상점 진열 5종 → 6종",                   250),
        new Upgrade(MetaUpgradeId.ShopRefresh,     "상점 갱신",   "상점마다 진열 갱신(R) 1회",              200),
    };

    public static Upgrade Get(MetaUpgradeId id)
    {
        foreach (Upgrade upgrade in All) if (upgrade.id == id) return upgrade;
        return All[0];
    }

    // 레벨당 수치
    public const float HealthPerLevel = 10f;
    public const int PotionsPerLevel = 1;
    public const int GoldPerLevel = 40;
    public const int DaggersPerLevel = 1;
    public const int RerollsPerLevel = 1;

    // 런 종료 시 영혼 = 라운드 × 5 + 위험도 ÷ 8(내림) + 유물 수 × 10 (Spec 6-3장)
    public static int SoulsForRun(int round, float risk, int relicCount)
    {
        return round * 5 + UnityEngine.Mathf.FloorToInt(risk / 8f) + relicCount * 10;
    }
}
