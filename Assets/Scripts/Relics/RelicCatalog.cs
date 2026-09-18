using UnityEngine;

// 유물 종류 (Spec 6-1장). RunState에 숫자로 저장되므로 값 고정
public enum RelicId
{
    ForgedSeal = 0,      // 벼려진 인장
    SteelHeart = 1,      // 강철 심장
    VampireNecklace = 2, // 흡혈 목걸이
    FeatherBoots = 3,    // 깃털 부츠
    LongHilt = 4,        // 긴 자루
    LightWrist = 5,      // 가벼운 손목
    TwinPouch = 6,       // 쌍날 주머니
    ThornsOfVengeance = 7, // 보복의 가시
    GoldenScale = 8,     // 황금 저울
    GuardianSigil = 9,   // 수호의 문양
    PhantomCloak = 10,   // 유령 망토
    PiercingTip = 11,    // 관통의 촉
    PoisonTip = 12,      // 독 묻은 촉
}

// 유물 목록과 효과 (Spec 6-1장 + "유물 — Unity 구현 규칙").
// 효과가 전부 코드라 ScriptableObject 없이 여기서 관리.
public static class RelicCatalog
{
    public struct Relic
    {
        public readonly RelicId id;
        public readonly string name;
        public readonly string description;

        public Relic(RelicId id, string name, string description)
        {
            this.id = id;
            this.name = name;
            this.description = description;
        }
    }

    public const int GoldWhenExhausted = 120;   // 13종을 다 모은 뒤 구간마다

    public static readonly Relic[] All =
    {
        new Relic(RelicId.ForgedSeal,        "벼려진 인장", "근접 공격력 +20%"),
        new Relic(RelicId.SteelHeart,        "강철 심장",   "최대 체력 +30"),
        new Relic(RelicId.VampireNecklace,   "흡혈 목걸이", "적 처치 시 체력 +4"),
        new Relic(RelicId.FeatherBoots,      "깃털 부츠",   "이동 +14%, 점프 +8%"),
        new Relic(RelicId.LongHilt,          "긴 자루",     "공격 사거리 +25%"),
        new Relic(RelicId.LightWrist,        "가벼운 손목", "공격 속도 +20%"),
        new Relic(RelicId.TwinPouch,         "쌍날 주머니", "단검 최대치 +3"),
        new Relic(RelicId.ThornsOfVengeance, "보복의 가시", "피격 시 주변 적에게 12 피해"),
        new Relic(RelicId.GoldenScale,       "황금 저울",   "골드 획득 +30%"),
        new Relic(RelicId.GuardianSigil,     "수호의 문양", "받는 피해 -15%"),
        new Relic(RelicId.PhantomCloak,      "유령 망토",   "무적 시간 +50%"),
        new Relic(RelicId.PiercingTip,       "관통의 촉",   "단검이 적을 관통"),
        new Relic(RelicId.PoisonTip,         "독 묻은 촉",  "단검 공격력 +35%"),
    };

    public static Relic Get(RelicId id)
    {
        foreach (Relic relic in All) if (relic.id == id) return relic;
        return All[0];
    }

    // 획득 즉시 플레이어에게 적용 (런 끝까지 유지)
    public static void Apply(RelicId id, PlayerHealth health)
    {
        if (health == null) return;
        PlayerStats stats = health.GetComponent<PlayerStats>();
        DaggerThrower daggers = health.GetComponent<DaggerThrower>();
        if (stats == null) return;

        switch (id)
        {
            case RelicId.ForgedSeal: stats.meleeDamageMul *= 1.2f; break;
            case RelicId.SteelHeart:
                stats.bonusMaxHealth += 30f;
                health.Heal(30f);                       // 늘어난 만큼 현재 체력도
                break;
            case RelicId.VampireNecklace: stats.healOnKill += 4f; break;
            case RelicId.FeatherBoots:
                stats.moveSpeedMul *= 1.14f;
                stats.jumpMul *= 1.08f;
                break;
            case RelicId.LongHilt: stats.attackRangeMul *= 1.25f; break;
            case RelicId.LightWrist: stats.attackSpeedMul *= 1.2f; break;
            case RelicId.TwinPouch:
                stats.bonusDaggers += 3;
                if (daggers != null) daggers.AddDaggers(3);  // 획득 즉시 현재 단검도
                break;
            case RelicId.ThornsOfVengeance: stats.thornsDamage += 12f; break;
            case RelicId.GoldenScale: stats.goldMul *= 1.3f; break;
            case RelicId.GuardianSigil: stats.damageTakenMul *= 0.85f; break;
            case RelicId.PhantomCloak: stats.invincibleTimeMul *= 1.5f; break;
            case RelicId.PiercingTip: stats.daggerPierce = true; break;
            case RelicId.PoisonTip: stats.daggerDamageMul *= 1.35f; break;
            default:
                Debug.LogWarning($"유물 효과 미구현: {id}");
                break;
        }
    }
}
