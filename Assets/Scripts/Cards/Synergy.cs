// 시너지 이름·설명과 수치 (Spec 4장 + 3장 "시너지 — Unity 구현 규칙").
// 단계(0/2/3)는 RunState.SynergyLevel(계열)로 조회하고, 실제 동작은 Enemy·BattleCardEffects·EnvironmentCardEffects·RoundManager가 처리.
public static class Synergy
{
    // 거인화 (능력 강화)
    public const float GiantSizeLv2 = 1.14f;
    public const float GiantSizeLv3 = 1.24f;
    public const float GiantDamageTaken = 0.82f;     // 받는 피해 -18% (Lv2·Lv3)
    public const float GiantStatsLv3 = 1.15f;        // Lv3: 체력·공격력 +15%

    // 광란 (행동 변화)
    public const float FrenzyRadiusLv2 = 280f / 45f; // 6.22u
    public const float FrenzyRadiusLv3 = 420f / 45f; // 9.33u
    public const float FrenzyDurationLv2 = 3f;
    public const float FrenzyDurationLv3 = 4.5f;
    public const float FrenzySpeed = 1.5f;
    public const float FrenzyAttack = 1.3f;

    // 범람 (증식/군세)
    public const float FloodIntervalLv2 = 8f;
    public const float FloodIntervalLv3 = 5f;

    // 붕괴 (환경/규칙)
    public const float CollapseIntervalLv2 = 6f;
    public const float CollapseIntervalLv3 = 3.5f;
    public const float CollapseDownLv2 = 6f;
    public const float CollapseDownLv3 = 4.5f;
    public const float CollapseWarnTime = 1f;        // 붕괴 1초 전부터 깜빡임 (예고 원칙)

    // 심연 (저주/특수): Lv2 정예 +1, Lv3 정예 +2
    public static int AbyssElites(int level) => level >= 3 ? 2 : level >= 2 ? 1 : 0;

    public static string Name(CardCategory category)
    {
        switch (category)
        {
            case CardCategory.Stat: return "거인화";
            case CardCategory.Behavior: return "광란";
            case CardCategory.Swarm: return "범람";
            case CardCategory.Env: return "붕괴";
            default: return "심연";
        }
    }

    public static string Describe(CardCategory category, int level)
    {
        bool lv3 = level >= 3;
        switch (category)
        {
            case CardCategory.Stat:
                return lv3 ? "적 크기 ×1.24, 받는 피해 -18%, 체력·공격력 +15%" : "적 크기 ×1.14, 받는 피해 -18%";
            case CardCategory.Behavior:
                return lv3 ? "적이 죽으면 넓은 범위의 적이 4.5초간 폭주" : "적이 죽으면 주변 적이 3초간 폭주 (속도·공격력 증가)";
            case CardCategory.Swarm:
                return lv3 ? "5초마다 전장 양끝에서 적 유입" : "8초마다 전장 양끝에서 적 유입";
            case CardCategory.Env:
                return lv3 ? "3.5초마다 발판이 무너짐 (4.5초 뒤 복구)" : "6초마다 발판이 무너짐 (6초 뒤 복구)";
            default:
                return lv3 ? "정예 적 2마리 추가" : "정예 적 1마리 추가";
        }
    }
}
