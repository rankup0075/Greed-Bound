using UnityEngine;

// 카드를 골랐을 때 실제로 바뀌는 것 (Spec 3장 효과 + "카드 — Unity 구현 규칙" 중첩 방식).
// 여기서는 누적 수치만 바꾸고, 전투 중 동작은 Enemy·RoundManager 등이 그 수치를 읽어서 처리.
public static class CardEffects
{
    // 구현이 끝난 카드만 제시 대상 (Spec 3장). 단계별로 구현하면서 여기에 추가
    public static bool IsImplemented(CardEffect effect)
    {
        switch (effect)
        {
            case CardEffect.Giant:
            case CardEffect.Rage:
            case CardEffect.Sprint:
            case CardEffect.IronSkin:
            case CardEffect.Indomitable:
            case CardEffect.Reinforcement:
            case CardEffect.BloodPact:
            case CardEffect.Gluttony:
            case CardEffect.MadBlessing:
            case CardEffect.HellGate:
                return true;
            default:
                return false;
        }
    }

    public static void Apply(CardData card, RunState run, PlayerHealth player)
    {
        EnemyModifiers enemies = run.enemyModifiers;
        PlayerStats stats = player != null ? player.GetComponent<PlayerStats>() : null;

        switch (card.effect)
        {
            // ① 능력 강화 — 배율 곱
            case CardEffect.Giant: enemies.healthMul *= 1.5f; break;
            case CardEffect.Rage: enemies.attackMul *= 1.4f; break;
            case CardEffect.Sprint: enemies.speedMul *= 1.3f; break;
            case CardEffect.IronSkin: enemies.damageTakenMul *= 0.7f; break;
            case CardEffect.Indomitable: enemies.lowHealthDamageTakenMul *= 0.5f; break;

            // ③ 증식/군세
            case CardEffect.Reinforcement: enemies.extraStartEnemies += 2; break;

            // ⑤ 저주/특수
            case CardEffect.BloodPact:
                if (stats != null) stats.maxHealthMul *= 0.7f;
                if (player != null) player.ClampHealthToMax();
                break;
            case CardEffect.Gluttony: enemies.lifesteal += 0.4f; break;
            case CardEffect.MadBlessing:
                enemies.attackMul *= 2f;
                enemies.killGoldStacks++;
                break;
            case CardEffect.HellGate: enemies.extraElites++; break;

            default:
                Debug.LogWarning($"카드 효과 미구현: {card.displayName} ({card.effect}) — 위험도·보상만 누적됨");
                break;
        }
    }
}
