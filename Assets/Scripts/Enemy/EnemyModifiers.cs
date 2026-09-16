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

    // 수 (합)
    public int extraStartEnemies = 0;           // 증원 +2
    public int extraElites = 0;                 // 지옥문 +1

    // 저주·특수 (합)
    public float lifesteal = 0f;                // 탐식 +0.4
    public int killGoldStacks = 0;              // 광기의 축복 장수 — 처치당 골드 = 장수 × (6 + 라운드 × 0.8)

    // Spec 2장: 위험도 배율 = 1 + 위험도 × 0.004 (체력·공격력에만 적용, Spec 5장)
    public float RiskMul => 1f + risk * 0.004f;
}
