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
            case CardEffect.Revenge:
            case CardEffect.ExplodingCorpse:
            case CardEffect.Regeneration:
            case CardEffect.Hunter:
            case CardEffect.Berserker:
            case CardEffect.Reinforcement:
            case CardEffect.Split:
            case CardEffect.Legion:
            case CardEffect.Summoning:
            case CardEffect.UndeadArmy:
            case CardEffect.Darkness:
            case CardEffect.BurningGround:
            case CardEffect.ManaStorm:
            case CardEffect.Seal:
            case CardEffect.DeathZone:
            case CardEffect.DeathClock:
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

            // ② 행동 변화 — 전투 중 동작은 Enemy·BattleCardEffects
            case CardEffect.Revenge: enemies.revengeStacks++; break;
            case CardEffect.ExplodingCorpse: enemies.explodingCorpseStacks++; break;
            case CardEffect.Regeneration: enemies.regenPerSecond += 0.108f; break;
            case CardEffect.Hunter: enemies.hunterSpeedMul *= 1.4f; break;
            case CardEffect.Berserker: enemies.berserkerStacks++; break;

            // ③ 증식/군세
            case CardEffect.Reinforcement: enemies.extraStartEnemies += 2; break;
            case CardEffect.Split: enemies.splitStacks++; break;
            case CardEffect.Legion: enemies.legionStacks++; break;
            case CardEffect.Summoning: enemies.summoningStacks++; break;
            case CardEffect.UndeadArmy: enemies.undeadArmyStacks++; break;

            // ④ 환경/규칙 — 전투 중 동작은 EnvironmentCardEffects
            case CardEffect.Darkness: enemies.darknessStacks++; break;
            case CardEffect.BurningGround: enemies.burningGroundStacks++; break;
            case CardEffect.ManaStorm: enemies.manaStormStacks++; break;
            case CardEffect.Seal: run.sealedRound = run.round; break;  // 고른 라운드에만
            case CardEffect.DeathZone: enemies.deathZoneStacks++; break;

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
            case CardEffect.DeathClock: enemies.deathClockStacks++; break;

            default:
                Debug.LogWarning($"카드 효과 미구현: {card.displayName} ({card.effect}) — 위험도·보상만 누적됨");
                break;
        }
    }
}
