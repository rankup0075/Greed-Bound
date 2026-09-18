using System.Collections.Generic;
using UnityEngine;

// Tab 소지품 창 (Spec 7장). 좌측: 스탯 배율 + 보유 유물 / 우측: 지금까지 적에게 건 강화 전체 + 발동 중인 시너지.
// 임시 UI(OnGUI). 호출하는 쪽이 GUI.matrix를 화면 높이 720 기준으로 맞춘 뒤 Draw를 부름.
public static class InventoryWindow
{
    static GUIStyle titleStyle, headerStyle, bodyStyle, hintStyle;

    public static void Draw(float width, float height, RunState run, PlayerHealth health)
    {
        EnsureStyles();
        DrawRect(new Rect(0, 0, width, height), new Color(0f, 0f, 0f, 0.75f));

        Rect panel = new Rect(width * 0.5f - 520, 40, 1040, 640);
        DrawRect(panel, new Color(0.12f, 0.1f, 0.14f, 0.97f));
        GUI.Label(new Rect(panel.x, panel.y + 14, panel.width, 40), "소지품", titleStyle);

        float columnWidth = panel.width * 0.5f - 40;
        DrawPlayerColumn(new Rect(panel.x + 30, panel.y + 64, columnWidth, panel.height - 110), run, health);
        DrawEnemyColumn(new Rect(panel.x + panel.width * 0.5f + 10, panel.y + 64, columnWidth, panel.height - 110), run);

        GUI.Label(new Rect(panel.x, panel.yMax - 40, panel.width, 30), "Tab 닫기", hintStyle);
    }

    static void DrawPlayerColumn(Rect rect, RunState run, PlayerHealth health)
    {
        GUI.Label(new Rect(rect.x, rect.y, rect.width, 30), "플레이어 강화", headerStyle);

        List<string> lines = new List<string>();
        PlayerStats stats = health != null ? health.GetComponent<PlayerStats>() : null;
        DaggerThrower daggers = health != null ? health.GetComponent<DaggerThrower>() : null;
        if (health != null) lines.Add($"체력 {Mathf.CeilToInt(health.CurrentHealth)} / {health.MaxHealth:F0}     물약 {run.potions}     골드 {run.gold}     카드 리롤 {run.rerollsLeft}회");
        lines.Add($"영혼 {MetaProgress.Souls}  (이번 런이 끝나면 +{MetaCatalog.SoulsForRun(run.round, run.Risk, run.RelicCount)})");
        PlayerSkill skill = health != null ? health.GetComponent<PlayerSkill>() : null;
        if (skill != null)
        {
            SkillCatalog.Skill info = SkillCatalog.Get(skill.Current);
            lines.Add($"스킬(A) {info.name} — 쿨 {info.cooldown:0}초");
        }
        if (stats != null)
        {
            lines.Add($"근접 공격력 ×{stats.meleeDamageMul:0.00}   사거리 ×{stats.attackRangeMul:0.00}   공격 속도 ×{stats.attackSpeedMul:0.00}");
            string pierce = stats.daggerPierce ? "   관통" : "";
            string maxDaggers = daggers != null ? $"   최대 {daggers.MaxDaggers}개" : "";
            lines.Add($"단검 공격력 ×{stats.daggerDamageMul:0.00}{maxDaggers}{pierce}");
            lines.Add($"이동 ×{stats.moveSpeedMul:0.00}   점프 ×{stats.jumpMul:0.00}");
            lines.Add($"받는 피해 ×{stats.damageTakenMul:0.00}   무적 시간 ×{stats.invincibleTimeMul:0.00}   골드 획득 ×{stats.goldMul:0.00}");
            if (stats.healOnKill > 0f || stats.thornsDamage > 0f)
                lines.Add($"처치 시 회복 +{stats.healOnKill:0}   피격 시 가시 {stats.thornsDamage:0}");
        }

        lines.Add("");
        lines.Add($"<b>유물 {run.RelicCount}개</b>");
        foreach (RelicId id in run.relics)
        {
            RelicCatalog.Relic relic = RelicCatalog.Get(id);
            lines.Add($"· {relic.name} — {relic.description}");
        }

        GUI.Label(new Rect(rect.x, rect.y + 40, rect.width, rect.height - 40), string.Join("\n", lines), bodyStyle);
    }

    static void DrawEnemyColumn(Rect rect, RunState run)
    {
        GUI.Label(new Rect(rect.x, rect.y, rect.width, 30), "적에게 건 강화", headerStyle);

        List<string> lines = new List<string>();
        string overload = run.IsOverloaded ? " (돌파)" : "";
        lines.Add($"위험도 {run.Risk:F0}{overload}   보상 x{run.RewardMultiplier:F2}   카드 {run.picks.Count}장");

        // 같은 카드는 한 줄로 묶어 장수 표시 (탐욕 장수는 따로)
        List<CardData> order = new List<CardData>();
        Dictionary<CardData, int> normal = new Dictionary<CardData, int>();
        Dictionary<CardData, int> greed = new Dictionary<CardData, int>();
        foreach (CardPick pick in run.picks)
        {
            if (pick.card == null) continue;
            if (!order.Contains(pick.card)) order.Add(pick.card);
            Dictionary<CardData, int> counts = pick.greed ? greed : normal;
            counts[pick.card] = counts.TryGetValue(pick.card, out int n) ? n + 1 : 1;
        }
        foreach (CardData card in order)
        {
            normal.TryGetValue(card, out int n);
            greed.TryGetValue(card, out int g);
            string countText = n + g > 1 ? $" ×{n + g}" : "";
            string greedText = g > 0 ? $" (탐욕 {g})" : "";
            string color = ColorUtility.ToHtmlStringRGB(CardData.CategoryColor(card.category));
            lines.Add($"<color=#{color}>■</color> {card.displayName}{countText}{greedText}");
        }

        lines.Add("");
        lines.Add("<b>발동 중인 시너지</b>");
        bool any = false;
        foreach (CardCategory category in System.Enum.GetValues(typeof(CardCategory)))
        {
            int level = run.SynergyLevel(category);
            if (level < 2) continue;
            any = true;
            lines.Add($"· {Synergy.Name(category)} Lv{level} — {Synergy.Describe(category, level)}");
        }
        if (!any) lines.Add("없음 (같은 계열 2장부터)");

        List<CardCombo.Combo> combos = CardCombo.ActiveList(run);
        lines.Add("");
        lines.Add("<b>발동 중인 조합</b>");
        foreach (CardCombo.Combo combo in combos) lines.Add($"· {combo.name} — {combo.description}");
        if (combos.Count == 0) lines.Add("없음 (특정 카드 2장)");

        GUI.Label(new Rect(rect.x, rect.y + 40, rect.width, rect.height - 40), string.Join("\n", lines), bodyStyle);
    }

    static void DrawRect(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    static void EnsureStyles()
    {
        if (titleStyle != null) return;

        titleStyle = MakeStyle(30, TextAnchor.MiddleCenter, FontStyle.Bold);
        headerStyle = MakeStyle(22, TextAnchor.MiddleLeft, FontStyle.Bold);
        bodyStyle = MakeStyle(16, TextAnchor.UpperLeft, FontStyle.Normal);
        bodyStyle.wordWrap = true;
        bodyStyle.richText = true;
        hintStyle = MakeStyle(16, TextAnchor.MiddleCenter, FontStyle.Normal);
    }

    static GUIStyle MakeStyle(int size, TextAnchor anchor, FontStyle fontStyle)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = anchor, fontStyle = fontStyle };
        style.normal.textColor = Color.white;
        return style;
    }
}
