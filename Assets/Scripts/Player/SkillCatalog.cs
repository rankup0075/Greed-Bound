// 플레이어 스킬 종류 (Spec 5장 "스킬"). 저장 파일에 숫자로 기록되므로 값 고정
public enum SkillId
{
    Whirlwind = 0,   // 회전베기
    GroundSlam = 1,  // 지면 강타
    SwordWave = 2,   // 검기
}

// 스킬 목록·수치 (Spec 5장 "스킬 — Unity 구현 규칙").
// 로비에서 1개를 골라 런 내내 A로 사용. 첫 스킬(회전베기)은 기본 해금, 나머지는 영혼으로 해금.
// 피해는 모두 근접 기본 피해(16 + 0~7) × 스킬 배율 × 근접 공격력 배율.
public static class SkillCatalog
{
    public struct Skill
    {
        public readonly SkillId id;
        public readonly string name;
        public readonly string description;
        public readonly float cooldown;
        public readonly int unlockCost;   // 0 = 기본 해금

        public Skill(SkillId id, string name, string description, float cooldown, int unlockCost)
        {
            this.id = id;
            this.name = name;
            this.description = description;
            this.cooldown = cooldown;
            this.unlockCost = unlockCost;
        }
    }

    // 회전베기: 플레이어 중심 원 (공중 가능)
    public const float WhirlwindRadius = 3.38f;   // 2.5 × 캐릭터 크기 배율 1.35
    public const float WhirlwindDamageMul = 2f;
    public const float WhirlwindKnockback = 1f;

    // 지면 강타: 좌우 지면 파동. 적 공격 준비를 끊음. 공중에서 쓰면 그 자리에서 수직 낙하 → 착지 순간 강타
    public const float SlamFallSpeed = 28f;      // 낙하 속도 (u/s)
    public const float SlamFallMaxTime = 1.5f;   // 이 시간 안에 착지하지 못하면 취소 (쿨은 돌려받지 않음)
    public const float SlamRange = 5.4f;         // 한쪽 거리 (4 × 1.35)
    public const float SlamHeight = 1.62f;       // 발 높이부터 위로 (1.2 × 1.35)
    public const float SlamDamageMul = 2.5f;
    public const float SlamKnockback = 2f;

    // 검기: 바라보는 방향으로 날아가는 관통 원거리 공격 (지형 통과, 마법구 파괴)
    public const float WaveSpeed = 16f;
    public const float WaveRange = 12f;
    public const float WaveHeight = 3.24f;      // 이펙트 초승달 높이 = 판정 높이 (2.4 × 1.35)
    public const float WaveWidth = 1.35f;       // 이펙트 두께 = 판정 폭 (1 × 1.35)
    public const float WaveDamageMul = 2f;

    public static readonly Skill[] All =
    {
        new Skill(SkillId.Whirlwind,  "회전베기",  "주위를 크게 베어 반경 3.4 안의 적 전부에게 피해 ×2.0, 살짝 밀어냄 (공중 가능)", 5f, 0),
        new Skill(SkillId.GroundSlam, "지면 강타", "땅을 내리찍어 좌우 5.4 거리 지면의 적에게 피해 ×2.5, 크게 밀어내고 공격 준비를 끊음 (공중에서 쓰면 수직 낙하 후 강타)", 7f, 120),
        new Skill(SkillId.SwordWave,  "검기",     "바라보는 방향으로 검기를 날려 닿는 적을 모두 관통하며 피해 ×2.0, 마법구도 베어냄 (공중 가능)", 4f, 180),
    };

    public static Skill Get(SkillId id)
    {
        foreach (Skill skill in All) if (skill.id == id) return skill;
        return All[0];
    }
}
