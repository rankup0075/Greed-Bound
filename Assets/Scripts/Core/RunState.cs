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
// 지금은 전투 씬 안에만 있음. 상점 씬을 분리할 때 씬 전환에도 살아남도록 바꿀 예정.
public class RunState : MonoBehaviour
{
    public const float OverloadRisk = 150f;        // 돌파 기준
    public const float OverloadRewardCap = 8f;     // 돌파 시 보상 배수 상한
    public const float GreedMultiplier = 1.3f;     // 탐욕 카드의 위험도·보상 배율

    public static RunState Instance { get; private set; }

    [Header("진행")]
    public int round = 1;
    public int gold = 0;
    public int relicCount = 0;

    [Header("보상 (카드 선택 시 누적)")]
    public float rewardPercent = 0f;               // 누적 보상%

    [Header("적 강화 (위험도 포함) — 테스트 중엔 여기서 직접 조절")]
    public EnemyModifiers enemyModifiers = new EnemyModifiers();

    [Header("고른 카드 (지금까지 적에게 건 강화 목록)")]
    public List<CardPick> picks = new List<CardPick>();

    public float Risk => enemyModifiers.risk;
    public bool IsOverloaded => Risk >= OverloadRisk;

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
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // 카드 획득: 위험도·보상 누적(탐욕이면 ×1.3) 후 효과 적용
    public void AddCard(CardData card, bool greed, PlayerHealth player)
    {
        float multiplier = greed ? GreedMultiplier : 1f;
        enemyModifiers.risk += card.risk * multiplier;
        rewardPercent += card.reward * multiplier;

        picks.Add(new CardPick { card = card, greed = greed, round = round });
        CardEffects.Apply(card, this, player);
    }
}
