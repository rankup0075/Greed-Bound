using UnityEngine;

// 환경/규칙 카드 + 죽음의 시계 (Spec 3장 "환경·죽음의 시계 카드 동작", 중첩은 "중복 검토 원칙").
// 전투 시작(BattleStarted)에 타이머를 초기화하고, 전투 중에만 위협을 만들며, 전투가 끝나면 전부 정리.
// 봉인은 RunState.IsSealed를 DaggerThrower가 직접 확인.
[RequireComponent(typeof(RoundManager), typeof(RunState), typeof(GameLoopQueue))]
public class EnvironmentCardEffects : MonoBehaviour
{
    public static EnvironmentCardEffects Instance { get; private set; }

    [Header("어둠")]
    public float darknessInner = 100f / 45f;       // 2.22u까지 완전히 보임
    public float darknessOuter = 320f / 45f;       // 7.11u에서 완전히 어두움
    public float darknessShrinkPerStack = 0.8f;
    public float darknessMinScale = 0.5f;

    [Header("불타는 대지")]
    public float fireInterval = 4.5f;
    public float fireWarnTime = 1.2f;
    public float fireBurnTime = 4.5f;
    public float fireTickInterval = 0.5f;
    public float fireWidth = 3f;
    public float fireHeight = 1f;           // 불기둥 높이 = 피해 높이 (발이 이 높이 안이면 피해)
    public float fireSpread = 4f;                  // 플레이어 x ± 이 범위

    [Header("마력 폭풍")]
    public float stormInterval = 4f;
    public float stormWarnTime = 1.1f;
    public float stormRadius = 62f / 45f;          // 1.38u
    public float stormSpread = 3f;                 // 2장째부터 추가 낙뢰 위치

    [Header("죽음의 영역")]
    public float zoneDuration = 25f;
    public float zoneMinDuration = 12f;
    public float zoneShrinkPerSide = 520f / 45f;   // 11.56u
    public float zoneMinWidth = 260f / 45f;        // 5.78u

    [Header("죽음의 시계")]
    public float clockTime = 45f;
    public float clockReductionPerStack = 8f;
    public float clockMinTime = 25f;
    public float clockWarningTime = 10f;

    static readonly Color StormColor = new Color(0.45f, 0.75f, 1f);

    // HUD용
    public bool DeathClockActive { get; private set; }
    public float DeathClockRemaining { get; private set; }
    public float DarknessOuterRadius { get; private set; }   // 어둠이 완전히 어두워지는 반경 (어둠 없으면 0)

    private RoundManager rounds;
    private RunState run;
    private GameLoopQueue queue;
    private PlayerHealth player;
    private DarknessOverlay darkness;
    private DeathZoneWalls zoneWalls;

    private bool battleRunning;
    private float battleStartTime;
    private float nextFireTime;
    private float nextStormTime;
    private float clockLimit;
    private float nextCollapseTime;
    private OneWayPlatform[] platforms;

    void Awake()
    {
        Instance = this;
        rounds = GetComponent<RoundManager>();
        run = GetComponent<RunState>();
        queue = GetComponent<GameLoopQueue>();

        // 카드 단계 없이 바로 전투가 시작되면 Start보다 BattleStarted가 먼저 올 수 있어서 Awake에서 준비
        player = FindFirstObjectByType<PlayerHealth>();
        darkness = DarknessOverlay.Create();
        zoneWalls = DeathZoneWalls.Create();
        ArenaBounds.Reset();
        platforms = FindObjectsByType<OneWayPlatform>(FindObjectsSortMode.None);
    }

    void OnEnable() => rounds.BattleStarted += OnBattleStarted;
    void OnDisable() => rounds.BattleStarted -= OnBattleStarted;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        ArenaBounds.Reset();
    }

    EnemyModifiers Mods => run.enemyModifiers;

    void OnBattleStarted()
    {
        battleRunning = true;
        battleStartTime = Time.time;
        nextFireTime = Time.time + fireInterval;
        nextStormTime = Time.time + stormInterval;
        nextCollapseTime = Time.time + CollapseInterval();

        // 죽음의 영역: 25초 ÷ 장수 (최소 12초)
        if (Mods.deathZoneStacks > 0)
        {
            float duration = Mathf.Max(zoneMinDuration, zoneDuration / Mods.deathZoneStacks);
            zoneWalls.Begin(duration, zoneShrinkPerSide, zoneMinWidth);
        }

        // 죽음의 시계: 45초 - 8초 × (장수 - 1) (최소 25초)
        DeathClockActive = Mods.deathClockStacks > 0;
        clockLimit = Mathf.Max(clockMinTime, clockTime - clockReductionPerStack * (Mods.deathClockStacks - 1));
        DeathClockRemaining = clockLimit;
    }

    void Update()
    {
        RoundManager.Phase phase = rounds.CurrentPhase;
        bool inCombat = phase == RoundManager.Phase.Battle || phase == RoundManager.Phase.Clearing;
        if (!inCombat)
        {
            if (battleRunning) EndBattle();
            return;
        }

        float elapsed = Time.time - battleStartTime;
        UpdateDarkness();
        zoneWalls.Tick(elapsed);

        // 새 위협과 시계는 전투 단계에서만 (전멸 1초 유지 중에는 멈춤)
        if (phase != RoundManager.Phase.Battle) return;
        UpdateFire();
        UpdateStorm();
        UpdateCollapse();
        UpdateDeathClock(elapsed);
    }

    void EndBattle()
    {
        battleRunning = false;
        DeathClockActive = false;
        DarknessOuterRadius = 0f;
        darkness.Hide();
        FireZone.DestroyAll();
        zoneWalls.Stop();
        foreach (OneWayPlatform platform in platforms) if (platform != null) platform.Restore();
    }

    // ───────────── 어둠 ─────────────

    void UpdateDarkness()
    {
        int stacks = Mods.darknessStacks;
        if (stacks <= 0 || player == null)
        {
            DarknessOuterRadius = 0f;
            darkness.Hide();
            return;
        }

        float scale = Mathf.Max(darknessMinScale, Mathf.Pow(darknessShrinkPerStack, stacks - 1));
        DarknessOuterRadius = darknessOuter * scale;
        darkness.Show(player.transform.position, darknessInner * scale, DarknessOuterRadius);
    }

    // ───────────── 불타는 대지 ─────────────

    void UpdateFire()
    {
        int stacks = Mods.burningGroundStacks;
        if (stacks <= 0 || Time.time < nextFireTime) return;
        nextFireTime = Time.time + fireInterval;

        float damage = 4f + run.round * 0.3f;
        float playerX = player != null ? player.transform.position.x : 0f;
        for (int i = 0; i < stacks; i++)
        {
            float x = ArenaBounds.Clamp(playerX + Random.Range(-fireSpread, fireSpread), fireWidth * 0.5f);
            FireZone.Spawn(x, fireWidth, fireHeight, fireWarnTime, fireBurnTime, fireTickInterval, damage, player);
        }
    }

    // ───────────── 마력 폭풍 ─────────────

    void UpdateStorm()
    {
        int stacks = Mods.manaStormStacks;
        if (stacks <= 0 || player == null || Time.time < nextStormTime) return;
        nextStormTime = Time.time + stormInterval;

        float damage = 10f + run.round * 0.5f;
        Vector2 target = player.transform.position;  // 예고 시작 순간의 플레이어 위치 — 움직이면 피함
        for (int i = 0; i < stacks; i++)
        {
            Vector2 point = target;
            if (i > 0) point.x = ArenaBounds.Clamp(target.x + Random.Range(-stormSpread, stormSpread), stormRadius);

            CircleTelegraph.Spawn(point, stormRadius, stormWarnTime, StormColor);
            queue.Schedule(stormWarnTime, () => Strike(point, damage), blocksRoundEnd: false);
        }
    }

    void Strike(Vector2 point, float damage)
    {
        RoundManager.Phase phase = rounds.CurrentPhase;
        if (phase != RoundManager.Phase.Battle && phase != RoundManager.Phase.Clearing) return;

        LightningBeam.Spawn(point);
        SoundManager.Play(SoundId.Explosion);

        // 폭풍의 격노 조합: 낙뢰 반경 안의 적이 폭주 (피해 없음)
        if (CardCombo.IsActive(ComboId.StormFury))
        {
            foreach (Enemy enemy in Enemy.Active)
            {
                if (!enemy.IsDead && ((Vector2)enemy.transform.position - point).sqrMagnitude <= stormRadius * stormRadius)
                    enemy.ApplyFrenzy(CardCombo.StormFuryDuration);
            }
        }

        if (player == null || player.IsDead) return;

        Vector2 toPlayer = (Vector2)player.transform.position - point;
        if (toPlayer.sqrMagnitude > stormRadius * stormRadius) return;
        player.TakeHit(new HitInfo { damage = damage, direction = new Vector2(Mathf.Sign(toPlayer.x), 0f) });
    }

    // ───────────── 붕괴 시너지 ─────────────

    // Lv2 6초·Lv3 3.5초마다 서 있는 발판 중 하나를 무작위로 붕괴 (1초 예고 깜빡임 → 무너짐 → 복구)
    void UpdateCollapse()
    {
        int level = run.SynergyLevel(CardCategory.Env);
        if (level < 2 || Time.time < nextCollapseTime) return;
        nextCollapseTime = Time.time + CollapseInterval();

        System.Collections.Generic.List<OneWayPlatform> candidates = new System.Collections.Generic.List<OneWayPlatform>();
        foreach (OneWayPlatform platform in platforms)
        {
            if (platform != null && !platform.IsCollapsing) candidates.Add(platform);
        }
        if (candidates.Count == 0) return;

        float downTime = level >= 3 ? Synergy.CollapseDownLv3 : Synergy.CollapseDownLv2;
        candidates[Random.Range(0, candidates.Count)].Collapse(Synergy.CollapseWarnTime, downTime);
    }

    float CollapseInterval() => run.SynergyLevel(CardCategory.Env) >= 3 ? Synergy.CollapseIntervalLv3 : Synergy.CollapseIntervalLv2;

    // ───────────── 죽음의 시계 ─────────────

    void UpdateDeathClock(float elapsed)
    {
        if (!DeathClockActive) return;
        DeathClockRemaining = Mathf.Max(0f, clockLimit - elapsed);

        // 시간이 다 됐을 때 살아있는 적이 있으면 즉사 (그 뒤 부활한 적이 생겨도 즉사)
        if (DeathClockRemaining <= 0f && Enemy.AliveCount > 0 && player != null && !player.IsDead)
        {
            rounds.ShowMessage("죽음의 시계가 멈췄습니다");
            player.Kill();
        }
    }

    public bool IsClockWarning => DeathClockActive && DeathClockRemaining <= clockWarningTime;
}
