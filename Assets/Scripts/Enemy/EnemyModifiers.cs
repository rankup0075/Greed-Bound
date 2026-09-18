// 런 전체에 걸쳐 적에게 누적되는 강화 (Spec 9장). 카드를 고를 때마다 값이 곱해지거나 더해지고, 적 스폰 시 일괄 적용.
// 중첩 방식은 Spec 3장 "카드 — Unity 구현 규칙" 표를 따름.
[System.Serializable]
public class EnemyModifiers
{
    public float risk = 0f;                     // 누적 위험도

    // 능력 강화 (곱)
    public float healthMul = 1f;                // 거대화 ×1.5
    public float attackMul = 1f;                // 광폭화 ×1.4, 광기의 축복 ×2
    public float speedMul = 1f;                 // 질주 ×1.3
    public float damageTakenMul = 1f;           // 강철 피부 ×0.7
    public float lowHealthDamageTakenMul = 1f;  // 불굴 ×0.5 (체력 30% 이하일 때)

    // 행동 변화
    public int revengeStacks = 0;               // 복수 — 공격력 ×1.25^장수, 4초
    public int explodingCorpseStacks = 0;       // 폭발하는 시체 — 폭발 피해 × 장수
    public float regenPerSecond = 0f;           // 재생 +0.108 (초당 최대 체력 비율)
    public float hunterSpeedMul = 1f;           // 사냥꾼 ×1.4 (가까울 때만)
    public int berserkerStacks = 0;             // 광전사 — 공격력 × (1 + 장수 × 사망률)

    // 수 (합)
    public int extraStartEnemies = 0;           // 증원 +2 (16 상한 초과분은 2마리당 정예 교체)
    public int extraElites = 0;                 // 지옥문 +1
    public int splitStacks = 0;                 // 분열 — 죽을 때 소형 적 장수만큼
    public int legionStacks = 0;                // 군단 — 지원군 수 × 장수
    public int summoningStacks = 0;             // 소환술 — 한 번에 장수만큼
    public int undeadArmyStacks = 0;            // 죽음의 군세 — 확률 30% × 장수(최대 100%), 5장부터 부활 가능 횟수 +1

    // 환경/규칙 (장수) — 봉인은 라운드 한정이라 RunState.sealedRound
    public int darknessStacks = 0;              // 어둠 — 시야 반경 ×0.8^(장수-1), 최소 ×0.5
    public int burningGroundStacks = 0;         // 불타는 대지 — 한 번에 화염 지대 장수만큼
    public int manaStormStacks = 0;             // 마력 폭풍 — 한 번에 낙뢰 장수만큼
    public int deathZoneStacks = 0;             // 죽음의 영역 — 축소 시간 32초 ÷ 장수 (최소 12초)
    public int deathClockStacks = 0;            // 죽음의 시계 — 45초 - 8초 × (장수-1) (최소 25초)

    // 저주·특수 (합)
    public float lifesteal = 0f;                // 탐식 +0.4
    public int killGoldStacks = 0;              // 광기의 축복 장수 — 처치당 골드 = 장수 × (6 + 라운드 × 0.8)

    // Spec 2장: 위험도 배율 = 1 + 위험도 × 0.004 (체력·공격력에만 적용, Spec 5장)
    public float RiskMul => 1f + risk * 0.004f;
}
