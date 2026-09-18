using System.Collections.Generic;
using UnityEngine;

// 고른 카드 1장 기록
[System.Serializable]
public struct CardPick
{
    public CardData card;
    public bool greed;     // 탐욕으로 가져온 카드 — 시너지 집계에서 제외
    public int round;
}

// 런 1회 동안 유지되는 진행 상태 (Spec 2장 위험도·보상, 9장 런 상태).
// 전투 씬·상점 씬에 각각 하나씩 있고, 씬을 넘어갈 때 CarryToNextScene()으로 맡긴 값을 다음 씬의 RunState가 Awake에서 받아감.
// 플레이어 강화(PlayerStats)·현재 체력도 함께 넘기고, 다음 씬에서 RestorePlayer()로 되돌림.
public class RunState : MonoBehaviour
{
    // 씬 전환 중 잠깐 보관하는 값
    class Carried
    {
        public int round, gold, sealedRound, potions, rerollsLeft, relicThresholdsClaimed;
        public float rewardPercent;
        public EnemyModifiers enemyModifiers;
        public List<CardPick> picks;
        public List<RelicId> relics;
        public bool hasPlayer;
        public float playerHealth;
        public string playerStatsJson;
    }

    static Carried carried;

    // 에디터에서 도메인 리로드를 끈 채 플레이를 다시 시작해도 이전 런이 섞이지 않게
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => carried = null;

    public const float OverloadRisk = 150f;        // 돌파 기준
    public const float OverloadRewardCap = 8f;     // 돌파 시 보상 배수 상한
    public const float GreedMultiplier = 1.3f;     // 탐욕 카드의 위험도·보상 배율

    public static RunState Instance { get; private set; }

    [Header("진행")]
    public int round = 1;
    public int gold = 0;
    public const float RiskPerRelic = 50f;         // 위험도 50마다 유물 1개
    public const float OverloadRiskPerRelic = 35f; // 돌파(150) 이후에는 35마다
    public int sealedRound = 0;                    // 봉인을 고른 라운드 — 그 라운드에만 단검·물약 사용 불가
    public int potions = 0;                        // 체력 물약 보유 수 (상점에서 구매, 1로 사용)
    public int rerollsLeft = 0;                    // 이번 런에 남은 카드 리롤(R) 횟수 (영구 강화 운명 뒤집기)

    [Header("보상 (카드 선택 시 누적)")]
    public float rewardPercent = 0f;               // 누적 보상%

    [Header("적 강화 (위험도 포함) — 테스트 중엔 여기서 직접 조절")]
    public EnemyModifiers enemyModifiers = new EnemyModifiers();

    [Header("고른 카드 (지금까지 적에게 건 강화 목록)")]
    public List<CardPick> picks = new List<CardPick>();

    [Header("유물")]
    public List<RelicId> relics = new List<RelicId>();
    public int relicThresholdsClaimed = 0;          // 지금까지 지급한 위험도 구간 수 (유물이 다 떨어져 골드로 받은 것 포함)

    public int RelicCount => relics.Count;

    // true = 상점 등 이전 씬에서 이어진 런, false = 새 런 (영구 강화는 새 런에만 적용)
    public bool ContinuedFromPreviousScene { get; private set; }

    public float Risk => enemyModifiers.risk;
    public bool IsOverloaded => Risk >= OverloadRisk;
    public bool IsSealed => sealedRound == round;

    // 보상 배수 = 1 + 누적보상% / 100 (돌파 시 x8.0 상한)
    public float RewardMultiplier
    {
        get
        {
            float multiplier = 1f + rewardPercent / 100f;
            return IsOverloaded ? Mathf.Min(multiplier, OverloadRewardCap) : multiplier;
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("RunState가 씬에 두 개 이상 있습니다. 하나만 남기세요.", this);
            Destroy(this);
            return;
        }
        Instance = this;

        // 이전 씬에서 넘어온 런이면 Inspector 값 대신 그 값을 사용
        if (carried != null)
        {
            round = carried.round;
            gold = carried.gold;
            sealedRound = carried.sealedRound;
            potions = carried.potions;
            rerollsLeft = carried.rerollsLeft;
            relicThresholdsClaimed = carried.relicThresholdsClaimed;
            rewardPercent = carried.rewardPercent;
            enemyModifiers = carried.enemyModifiers;
            picks = carried.picks;
            relics = carried.relics;
            playerToRestore = carried;
            carried = null;
            ContinuedFromPreviousScene = true;
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private Carried playerToRestore;

    // 씬을 넘어가기 직전에 호출. 다음 씬의 RunState가 이어받음
    public void CarryToNextScene(PlayerHealth player)
    {
        carried = new Carried
        {
            round = round,
            gold = gold,
            sealedRound = sealedRound,
            potions = potions,
            rerollsLeft = rerollsLeft,
            relicThresholdsClaimed = relicThresholdsClaimed,
            rewardPercent = rewardPercent,
            enemyModifiers = enemyModifiers,
            picks = picks,
            relics = relics,
        };

        PlayerStats stats = player != null ? player.GetComponent<PlayerStats>() : null;
        if (stats != null)
        {
            carried.hasPlayer = true;
            carried.playerHealth = player.CurrentHealth;
            carried.playerStatsJson = JsonUtility.ToJson(stats);
        }
    }

    // 새 씬의 플레이어에게 넘어온 강화·체력을 적용 (씬의 모든 Awake가 끝난 뒤, Start에서 호출)
    public void RestorePlayer(PlayerHealth player)
    {
        if (playerToRestore == null || !playerToRestore.hasPlayer || player == null) return;

        PlayerStats stats = player.GetComponent<PlayerStats>();
        if (stats != null) JsonUtility.FromJsonOverwrite(playerToRestore.playerStatsJson, stats);  // 최대 체력 배율이 먼저 반영돼야 함
        player.SetHealth(playerToRestore.playerHealth);
        playerToRestore = null;
    }

    // 카드를 고른 결과 — 적용 안내 화면에 표시
    public struct CardPickResult
    {
        public CardCategory category;
        public int synergyLevelBefore;
        public int synergyLevelAfter;
        public float bonusRisk;          // 시너지 장수 보너스
        public float bonusReward;
        public List<string> rewards;     // 새로 얻은 유물(또는 유물 소진 골드) 안내 문구
        public List<CardCombo.Combo> combos;  // 이번에 새로 발동한 조합 시너지
    }

    // 카드 획득: 위험도·보상 누적(탐욕이면 ×1.3) → 효과 적용 → 시너지 보너스 → 유물 지급
    public CardPickResult AddCard(CardData card, bool greed, PlayerHealth player)
    {
        CardPickResult result = new CardPickResult
        {
            category = card.category,
            synergyLevelBefore = SynergyLevel(card.category),
        };

        float multiplier = greed ? GreedMultiplier : 1f;
        enemyModifiers.risk += RiskFor(card, multiplier);
        rewardPercent += card.reward * multiplier;

        // 조합 시너지 (탐욕 카드 포함): 새로 완성되면 1회 보너스
        result.combos = CardCombo.Completing(this, card.effect);
        foreach (CardCombo.Combo combo in result.combos)
        {
            enemyModifiers.risk += CardCombo.BonusRisk;
            rewardPercent += CardCombo.BonusReward;
        }

        picks.Add(new CardPick { card = card, greed = greed, round = round });
        CardEffects.Apply(card, this, player);

        // 시너지 보너스 (탐욕 카드는 집계 제외): 2장째 +10/+25%p, 3장째 +8/+20%p, 4장째부터 +5/+12%p
        if (!greed)
        {
            int count = SynergyCount(card.category);
            if (count >= 2)
            {
                result.bonusRisk = count == 2 ? 10f : count == 3 ? 8f : 5f;
                result.bonusReward = count == 2 ? 25f : count == 3 ? 20f : 12f;
                enemyModifiers.risk += result.bonusRisk;
                rewardPercent += result.bonusReward;
            }
        }
        result.synergyLevelAfter = SynergyLevel(card.category);

        result.rewards = GrantRelics(player);
        return result;
    }

    // 카드가 올리는 위험도 — 정수로 반올림(0.5는 올림). 탐욕 ×1.3에서 생기는 소수를 없앰 (예: 13 × 1.3 = 16.9 → 17)
    // float 곱셈 오차(15 × 1.3 = 19.4999…)로 반올림이 틀리지 않게 decimal로 계산
    public static int RiskFor(CardData card, float multiplier)
    {
        decimal exact = (decimal)card.risk * (decimal)multiplier;
        return (int)System.Math.Floor(exact + 0.5m);
    }

    // ───────────── 시너지 (Spec 3장 "시너지 — Unity 구현 규칙") ─────────────

    // 탐욕이 아닌 카드 중 해당 계열 장수 (같은 카드 중복도 각각 셈)
    public int SynergyCount(CardCategory category)
    {
        int count = 0;
        foreach (CardPick pick in picks)
        {
            if (!pick.greed && pick.card != null && pick.card.category == category) count++;
        }
        return count;
    }

    // 0 = 없음, 2 = Lv2, 3 = Lv3 (4장 이상도 3)
    public int SynergyLevel(CardCategory category)
    {
        int count = SynergyCount(category);
        return count >= 3 ? 3 : count >= 2 ? 2 : 0;
    }

    // ───────────── 유물 (Spec 6-1장 + "유물 — Unity 구현 규칙") ─────────────

    // 위험도가 넘은 유물 구간 수: 150까지 50마다(50·100·150), 그 뒤로 35마다(185·220·255…)
    public static int RelicThresholdsReached(float risk)
    {
        if (risk < OverloadRisk) return Mathf.FloorToInt(risk / RiskPerRelic);
        return Mathf.FloorToInt(OverloadRisk / RiskPerRelic) + Mathf.FloorToInt((risk - OverloadRisk) / OverloadRiskPerRelic);
    }

    // 누적 위험도가 넘은 구간만큼 유물 지급. 13종을 다 모았으면 구간마다 골드 +120
    public List<string> GrantRelics(PlayerHealth player)
    {
        List<string> gained = new List<string>();
        int earned = RelicThresholdsReached(Risk);

        while (relicThresholdsClaimed < earned)
        {
            relicThresholdsClaimed++;

            List<RelicId> remaining = new List<RelicId>();
            foreach (RelicCatalog.Relic relic in RelicCatalog.All)
            {
                if (!relics.Contains(relic.id)) remaining.Add(relic.id);
            }

            if (remaining.Count == 0)
            {
                gold += RelicCatalog.GoldWhenExhausted;
                gained.Add($"유물을 모두 모아 골드 +{RelicCatalog.GoldWhenExhausted}");
                continue;
            }

            RelicId id = remaining[Random.Range(0, remaining.Count)];
            relics.Add(id);
            RelicCatalog.Apply(id, player);
            RelicCatalog.Relic info = RelicCatalog.Get(id);
            gained.Add($"유물 획득: {info.name} — {info.description}");
        }
        return gained;
    }

}
