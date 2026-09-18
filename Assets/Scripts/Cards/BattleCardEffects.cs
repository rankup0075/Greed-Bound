using UnityEngine;

// 전투 중에 일어나는 카드 효과 (Spec 3장 "행동 변화·증식 카드 동작").
// 적 사망 시: 복수 → 분열 → 폭발 예약 → 부활 판정 / 전투 중 매 프레임: 군단 발동 확인, 소환술 타이머.
// 재생·사냥꾼·광전사처럼 적 한 마리 안에서 끝나는 효과는 Enemy가 직접 처리.
// 지연되는 효과(폭발·부활)는 GameLoopQueue에 "라운드 종료 차단"으로 예약 — 끝나기 전엔 라운드가 끝나지 않음.
[RequireComponent(typeof(RoundManager), typeof(RunState), typeof(GameLoopQueue))]
public class BattleCardEffects : MonoBehaviour
{
    [Header("복수")]
    public float revengeMultiplier = 1.25f;
    public float revengeDuration = 4f;

    [Header("폭발하는 시체")]
    public float explosionDelay = 0.95f;
    public float explosionRadius = 105f / 45f;      // 2.33u
    public float explosionPlayerRatio = 1.1f;
    public float explosionEnemyRatio = 0.5f;       // 다른 적에게 — 적 최대 체력의 약 8%/장 (1.4였을 땐 23%라 연쇄로 전멸)

    [Header("죽음의 군세")]
    public float reviveChancePerStack = 0.3f;
    public float reviveDelay = 2f;
    public float reviveHealthRatio = 0.5f;

    [Header("소환술")]
    public float summonInterval = 4.5f;
    public int summonAliveLimit = 18;
    public float summonMinDistance = 6f;

    [Header("분열·부활 생성 위치")]
    public float respawnLift = 0.2f;                // 죽은 자리보다 살짝 위에서 생성

    static readonly Color ExplosionColor = new Color(1f, 0.45f, 0.15f);

    private RoundManager rounds;
    private RunState run;
    private GameLoopQueue queue;
    private PlayerHealth player;

    private bool legionTriggered;
    private float nextSummonTime;
    private float nextFloodTime;

    void Awake()
    {
        rounds = GetComponent<RoundManager>();
        run = GetComponent<RunState>();
        queue = GetComponent<GameLoopQueue>();
    }

    void OnEnable()
    {
        Enemy.Killed += OnEnemyKilled;
        rounds.BattleStarted += OnBattleStarted;
    }

    void OnDisable()
    {
        Enemy.Killed -= OnEnemyKilled;
        rounds.BattleStarted -= OnBattleStarted;
    }

    void Start()
    {
        player = FindFirstObjectByType<PlayerHealth>();
    }

    bool InBattle => rounds.CurrentPhase == RoundManager.Phase.Battle || rounds.CurrentPhase == RoundManager.Phase.Clearing;

    void OnBattleStarted()
    {
        legionTriggered = false;
        nextSummonTime = Time.time + summonInterval;
        nextFloodTime = Time.time + FloodInterval();
    }

    void Update()
    {
        if (!InBattle) return;
        UpdateLegion();
        UpdateSummoning();
        UpdateFlood();
    }

    // ───────────── 적 사망 시 ─────────────

    void OnEnemyKilled(Enemy enemy)
    {
        if (!InBattle) return;

        EnemyModifiers m = run.enemyModifiers;
        Vector2 position = enemy.transform.position;

        // 복수: 살아있는 다른 적 전원 공격력 버프 (장수만큼 배율 곱, 시간 갱신)
        if (m.revengeStacks > 0)
        {
            float multiplier = Mathf.Pow(revengeMultiplier, m.revengeStacks);
            float duration = CardCombo.IsActive(ComboId.Vanguard) ? CardCombo.VanguardRevengeDuration : revengeDuration;  // 결사대 조합
            foreach (Enemy other in Enemy.Active.ToArray())
            {
                if (other != enemy && !other.IsDead) other.ApplyRevenge(multiplier, duration);
            }
        }

        // 광란 시너지: 죽은 자리 반경 안 적이 폭주 (Lv2 6.22u·3초, Lv3 9.33u·4.5초)
        int frenzyLevel = run.SynergyLevel(CardCategory.Behavior);
        if (frenzyLevel >= 2)
        {
            float radius = frenzyLevel >= 3 ? Synergy.FrenzyRadiusLv3 : Synergy.FrenzyRadiusLv2;
            float duration = frenzyLevel >= 3 ? Synergy.FrenzyDurationLv3 : Synergy.FrenzyDurationLv2;
            foreach (Enemy other in Enemy.Active.ToArray())
            {
                if (other == enemy || other.IsDead) continue;
                if (((Vector2)other.transform.position - position).sqrMagnitude <= radius * radius) other.ApplyFrenzy(duration);
            }
        }

        // 분열: 소형 적 장수만큼. 분열로 생긴 적은 다시 분열하지 않음. 증원 예산 제외
        if (m.splitStacks > 0 && !enemy.IsSplitChild)
        {
            for (int i = 0; i < m.splitStacks; i++)
            {
                Vector2 offset = new Vector2((i - (m.splitStacks - 1) * 0.5f) * 0.4f, respawnLift);
                Enemy child = rounds.SpawnEnemyAt(EnemyType.Small, position + offset, countAsSpawn: true);
                if (child != null) child.MarkSplitChild();
            }
        }

        // 폭발하는 시체: 예고 원 → 0.95초 뒤 폭발. 피해 기준 = 죽은 적 공격력 × 장수
        if (m.explodingCorpseStacks > 0)
        {
            float baseDamage = enemy.Attack * m.explodingCorpseStacks;
            CircleTelegraph.Spawn(position, explosionRadius, explosionDelay, ExplosionColor);
            queue.Schedule(explosionDelay, () => Explode(position, baseDamage), blocksRoundEnd: true);
            queue.Schedule(explosionDelay, () => SoundManager.Play(SoundId.Explosion), blocksRoundEnd: false);
        }

        // 죽음의 군세: 확률 30% × 장수(최대 100%), 5장부터 부활 가능 횟수 증가. 2초 뒤 같은 종류로 체력 50% 부활. 보스는 제외
        int undeadStacks = m.undeadArmyStacks;
        if (undeadStacks > 0 && !enemy.IsBoss && enemy.RevivesUsed < MaxRevives(undeadStacks))
        {
            float chance = Mathf.Min(1f, reviveChancePerStack * undeadStacks);
            if (chance >= 1f || Random.value < chance)
            {
                EnemyType type = enemy.Type;
                bool wasSplitChild = enemy.IsSplitChild;
                int revivesUsed = enemy.RevivesUsed + 1;
                ReviveMarker.Spawn(enemy, reviveDelay);
                queue.Schedule(reviveDelay, () => Revive(type, position, wasSplitChild, revivesUsed), blocksRoundEnd: true);
            }
        }
    }

    void Explode(Vector2 center, float baseDamage)
    {
        if (!InBattle) return;
        float radiusSqr = explosionRadius * explosionRadius;

        if (player != null && !player.IsDead)
        {
            Vector2 toPlayer = (Vector2)player.transform.position - center;
            if (toPlayer.sqrMagnitude <= radiusSqr)
            {
                player.TakeHit(new HitInfo
                {
                    damage = baseDamage * explosionPlayerRatio,
                    direction = new Vector2(Mathf.Sign(toPlayer.x), 0f),
                });
            }
        }

        // 폭발로 죽은 적이 또 폭발·분열할 수 있으므로 목록 복사본으로 순회 (연쇄)
        foreach (Enemy target in Enemy.Active.ToArray())
        {
            if (target.IsDead) continue;
            Vector2 toTarget = (Vector2)target.transform.position - center;
            if (toTarget.sqrMagnitude > radiusSqr) continue;

            target.TakeHit(new HitInfo
            {
                damage = baseDamage * explosionEnemyRatio,
                direction = new Vector2(Mathf.Sign(toTarget.x), 0f),
            });
        }

        // 시체 소환 조합: 폭발 자리에 확률로 소형 적 (증원 예산 소모, 분열하지 않음)
        if (CardCombo.IsActive(ComboId.CorpseSummon) && Random.value < CardCombo.CorpseSummonChance && rounds.TryConsumeReinforcement())
        {
            Enemy summoned = rounds.SpawnEnemyAt(EnemyType.Small, center + Vector2.up * respawnLift, countAsSpawn: true);
            if (summoned != null) summoned.MarkSplitChild();
        }
    }

    // 같은 적의 부활 가능 횟수: 4장까지 1회, 5장부터 장당 +1 (확률이 100%에 닿은 뒤에도 중첩이 의미 있게)
    static int MaxRevives(int stacks) => 1 + Mathf.Max(0, stacks - 4);

    void Revive(EnemyType type, Vector2 position, bool wasSplitChild, int revivesUsed)
    {
        if (!InBattle) return;

        Enemy revived = rounds.SpawnEnemyAt(type, position + Vector2.up * respawnLift, countAsSpawn: false);
        if (revived == null) return;
        revived.SetRevivesUsed(revivesUsed);
        if (wasSplitChild) revived.MarkSplitChild();
        revived.SetHealthRatio(reviveHealthRatio);
        rounds.ShowMessage("적이 되살아났습니다!");
    }

    // ───────────── 전투 중 매 프레임 ─────────────

    // 군단: 처치 수가 초기 적 수의 절반에 닿으면 1회, 맵 양끝에서 지원군
    void UpdateLegion()
    {
        int stacks = run.enemyModifiers.legionStacks;
        if (stacks <= 0 || legionTriggered) return;

        int half = Mathf.CeilToInt(rounds.StartEnemyCount * 0.5f);
        if (rounds.KilledThisRound < half) return;

        legionTriggered = true;
        int count = half * stacks;
        int spawned = 0;
        bool hellLegion = CardCombo.IsActive(ComboId.HellLegion);  // 지옥의 군단 조합: 첫 지원군이 정예
        for (int i = 0; i < count; i++)
        {
            if (!rounds.TryConsumeReinforcement()) break;
            float side = i % 2 == 0 ? -1f : 1f;
            float edge = side < 0f ? ArenaBounds.MinX : ArenaBounds.MaxX;  // 죽음의 영역이면 좁아진 끝
            float x = ArenaBounds.Clamp(edge - side * (1.5f + (i / 2) * 0.8f), 1f);
            rounds.SpawnEnemy(hellLegion && i == 0 ? EnemyType.Elite : EnemyType.Normal, x);
            spawned++;
        }
        if (spawned > 0) rounds.ShowMessage("지원군이 몰려옵니다!");
    }

    // 소환술: 4.5초마다, 살아있는 적이 1 이상 18 미만이면 플레이어와 떨어진 곳에 장수만큼
    void UpdateSummoning()
    {
        int stacks = run.enemyModifiers.summoningStacks;
        if (stacks <= 0 || Time.time < nextSummonTime) return;
        nextSummonTime = Time.time + summonInterval;

        int alive = Enemy.AliveCount;
        for (int i = 0; i < stacks; i++)
        {
            if (alive <= 0 || alive >= summonAliveLimit) break;  // 마지막 적을 잡으면 소환 멈춤
            if (!rounds.TryConsumeReinforcement()) break;
            rounds.SpawnEnemy(EnemyType.Normal, SummonX());
            alive++;
        }
    }

    // 범람 시너지: Lv2 8초·Lv3 5초마다 현재 전장 양끝에서 1마리씩. 살아있는 적이 있을 때만, 증원 예산 소모
    void UpdateFlood()
    {
        int level = run.SynergyLevel(CardCategory.Swarm);
        if (level < 2 || Time.time < nextFloodTime) return;
        nextFloodTime = Time.time + FloodInterval();

        if (Enemy.AliveCount <= 0) return;  // 마지막 적을 잡으면 멈춤
        for (int i = 0; i < 2; i++)
        {
            if (!rounds.TryConsumeReinforcement()) break;
            float x = i == 0 ? ArenaBounds.MinX + 1.5f : ArenaBounds.MaxX - 1.5f;
            rounds.SpawnEnemy(EnemyType.Normal, x);
        }
    }

    float FloodInterval() => run.SynergyLevel(CardCategory.Swarm) >= 3 ? Synergy.FloodIntervalLv3 : Synergy.FloodIntervalLv2;

    // 플레이어와 summonMinDistance 이상 떨어진 맵 안의 무작위 x
    float SummonX()
    {
        float min = ArenaBounds.MinX + 1f;
        float max = ArenaBounds.MaxX - 1f;
        float playerX = player != null ? player.transform.position.x : 0f;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            float x = Random.Range(min, max);
            if (Mathf.Abs(x - playerX) >= summonMinDistance) return x;
        }
        // 못 찾으면 플레이어에게서 더 먼 쪽 끝
        return playerX - min > max - playerX ? min : max;
    }
}
