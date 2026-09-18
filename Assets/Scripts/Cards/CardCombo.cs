using System.Collections.Generic;

public enum ComboId { UndyingWill, DarkStalker, CorpseSummon, Vanguard, StormFury, FlameFeast, HellLegion, LastStand }

// 조합 시너지: 서로 다른 계열의 특정 카드 2장 (Spec 4장 "조합 시너지").
// 탐욕 카드도 포함, 발동 순간 1회 위험도·보상 보너스. 실제 동작은 Enemy·BattleCardEffects·EnvironmentCardEffects·FireZone이 처리.
public static class CardCombo
{
    public const float BonusRisk = 8f;
    public const float BonusReward = 20f;

    // 효과 수치
    public const float UndyingRegenPause = 1f;       // 불멸의 의지: 저체력 적의 재생 중지 시간
    public const float StalkerSpeedMul = 1.3f;       // 어둠의 추적자
    public const float CorpseSummonChance = 0.5f;    // 시체 소환
    public const float VanguardRevengeDuration = 6f; // 결사대
    public const float StormFuryDuration = 4f;       // 폭풍의 격노
    public const float FlameFeastHealPerSecond = 0.06f;  // 불꽃 포식
    public const float LastStandClockTime = 15f;     // 최후의 저항
    public const float LastStandDamageTaken = 0.8f;

    public struct Combo
    {
        public ComboId id;
        public string name;
        public CardEffect a, b;
        public string description;
    }

    public static readonly Combo[] All =
    {
        new Combo { id = ComboId.UndyingWill, name = "불멸의 의지", a = CardEffect.Regeneration, b = CardEffect.Indomitable,
            description = "체력 30% 이하인 적은 맞은 뒤 1초 만에 재생 재개" },
        new Combo { id = ComboId.DarkStalker, name = "어둠의 추적자", a = CardEffect.Hunter, b = CardEffect.Darkness,
            description = "시야 밖의 적 이동속도 ×1.3" },
        new Combo { id = ComboId.CorpseSummon, name = "시체 소환", a = CardEffect.ExplodingCorpse, b = CardEffect.Summoning,
            description = "시체가 폭발하면 50% 확률로 소형 적 소환" },
        new Combo { id = ComboId.Vanguard, name = "결사대", a = CardEffect.Revenge, b = CardEffect.Reinforcement,
            description = "복수 지속 시간 4초 → 6초" },
        new Combo { id = ComboId.StormFury, name = "폭풍의 격노", a = CardEffect.Rage, b = CardEffect.ManaStorm,
            description = "낙뢰에 닿은 적이 4초간 폭주" },
        new Combo { id = ComboId.FlameFeast, name = "불꽃 포식", a = CardEffect.BurningGround, b = CardEffect.Gluttony,
            description = "화염 지대 안의 적은 초당 체력 6% 회복" },
        new Combo { id = ComboId.HellLegion, name = "지옥의 군단", a = CardEffect.HellGate, b = CardEffect.Legion,
            description = "군단 지원군 중 1마리가 정예" },
        new Combo { id = ComboId.LastStand, name = "최후의 저항", a = CardEffect.IronSkin, b = CardEffect.DeathClock,
            description = "죽음의 시계 15초 이하에서 적이 받는 피해 ×0.8" },
    };

    public static Combo Get(ComboId id)
    {
        foreach (Combo combo in All) if (combo.id == id) return combo;
        return All[0];
    }

    static bool HasCard(RunState run, CardEffect effect)
    {
        foreach (CardPick pick in run.picks)
        {
            if (pick.card != null && pick.card.effect == effect) return true;
        }
        return false;
    }

    public static bool Has(RunState run, ComboId id)
    {
        if (run == null) return false;
        Combo combo = Get(id);
        return HasCard(run, combo.a) && HasCard(run, combo.b);
    }

    // 현재 런에서 발동 중인지 (RunState가 없으면 false)
    public static bool IsActive(ComboId id) => Has(RunState.Instance, id);

    public static List<Combo> ActiveList(RunState run)
    {
        List<Combo> list = new List<Combo>();
        foreach (Combo combo in All) if (Has(run, combo.id)) list.Add(combo);
        return list;
    }

    // 이 카드를 새로 고르면 완성되는 조합 (이미 발동 중인 조합은 제외)
    public static List<Combo> Completing(RunState run, CardEffect effect)
    {
        List<Combo> list = new List<Combo>();
        if (run == null || HasCard(run, effect)) return list;
        foreach (Combo combo in All)
        {
            if (combo.a == effect && HasCard(run, combo.b)) list.Add(combo);
            else if (combo.b == effect && HasCard(run, combo.a)) list.Add(combo);
        }
        return list;
    }
}
