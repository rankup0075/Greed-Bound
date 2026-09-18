using System.Collections.Generic;
using UnityEngine;

public enum EnemyType { Normal, Small, Elite, Ranged, Shield, Charger, Boss }

// 적 1마리 (Spec 5장 적 기본 수치 / 적 AI / 원거리·방패병·돌진병·보스).
// 근접: 추격 → 사거리 안이면 예비동작 0.4초(정지, 붉게 + 방향 표시) → 그 순간 다시 판정해서 공격 → 쿨.
// 원거리: 거리 유지 → 10u 안이면 조준 0.6초(정지, 붉게 + 조준선, 방향 고정) → 조준선 방향으로 마법구 발사 → 쿨.
// 방패병: 근접 + 정면 피해 감소, 느린 방향 전환. 돌진병: 근접 + 경로선 예고 후 직선 돌진 → 지침.
// 보스: 내려찍기·돌진·마력 파동, 밀어내기·공격 취소 안 받음.
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class Enemy : MonoBehaviour, IDamageable
{
    public const string BossName = "타락한 기사단장";

    // 살아 있는 적 목록. 겹침 방지·충돌 무시·라운드 종료 판정에서 사용
    public static readonly List<Enemy> Active = new List<Enemy>();

    // 이번 프레임에 죽어서 아직 파괴되지 않은 적은 제외하고 셈 (라운드 종료 판정용)
    public static int AliveCount
    {
        get
        {
            int count = 0;
            foreach (Enemy enemy in Active) if (!enemy.IsDead) count++;
            return count;
        }
    }

    // 적이 죽는 순간 알림 (광기의 축복 처치 골드, 이후 복수·분열·부활 등)
    public static event System.Action<Enemy> Killed;

    const float KnockbackTime = 0.1f;
    const float FlashTime = 0.1f;
    const float LowHealthRatio = 0.3f;          // 불굴 발동 기준
    const float PlatformHeightThreshold = 1f;   // 플레이어 발이 이만큼 높아야 "발판 위"
    const float StopDistanceRatio = 0.8f;       // 사거리의 80% 안으로는 더 붙지 않음

    static readonly Color WindupColor = new Color(1f, 0.2f, 0.15f);
    static readonly Color AttackEffectColor = new Color(1f, 0.35f, 0.3f);
    static readonly Color BlockFlashColor = new Color(0.55f, 0.9f, 1f);
    const float HitFlashAmount = 0.85f;         // 피격: 흰색으로 거의 덮음 (윤곽은 살짝 남김)
    const float BlockFlashAmount = 0.6f;        // 방패 막힘: 하늘색으로 반쯤
    static readonly Color RecoverColor = new Color(0.45f, 0.45f, 0.45f);
    static readonly Color ShieldColor = new Color(0.75f, 0.82f, 0.92f);

    [Header("종류 (스포너가 생성하면 스포너 설정으로 덮어씀)")]
    public EnemyType type = EnemyType.Normal;

    [Header("일반 적 기본 수치 (Spec 5장)")]
    public float baseHealth = 60f;
    public float baseAttack = 10f;
    public float baseSpeed = 2.5f;

    [Header("AI (Spec 5장)")]
    public float windupTime = 24f / 60f;        // 예비동작 0.4초 — 제거 금지
    public float platformChaseDelay = 0.9f;
    public float jumpCooldownMin = 1.5f;
    public float jumpCooldownMax = 2.5f;
    public float jumpTriggerDistance = 2.5f;
    public float jumpExtraHeight = 0.5f;
    public float gravityScale = 5.06f;          // 플레이어와 같은 중력

    [Header("원거리 적 (Spec 5장 원거리 적 AI)")]
    public float rangedWindupTime = 0.6f;       // 조준선 예고
    public float rangedRange = 10f;             // 사격 거리 (조준선 길이)
    public float rangedCooldown = 2.5f;
    public float rangedKeepMin = 5f;            // 이보다 가까우면 물러남
    public float rangedKeepMax = 8f;            // 이보다 멀면 다가감
    public float projectileSpeed = 7f;
    public float projectileRadius = 0.25f;
    public float projectileMaxDistance = 14f;

    [Header("방패병 (Spec 5장 방패병 AI)")]
    public float shieldDamageMul = 0.2f;        // 정면 피해 배율
    public float shieldBreakDuration = 2.5f;    // 3타·스킬에 맞으면 방패 내림
    public float shieldTurnDelay = 0.6f;        // 플레이어가 뒤로 넘어가도 이 시간 동안 방향 유지

    [Header("돌진병 (Spec 5장 돌진병 AI)")]
    public float chargerWindupTime = 0.7f;
    public float chargerSpeed = 18f;
    public float chargerDistance = 9f;
    public float chargerMinTrigger = 3f;        // 가로 거리 3 ~ 9u에서 돌진
    public float chargerDamageMul = 1.3f;
    public float chargerRecoverTime = 1f;
    public float chargerCooldown = 4f;

    [Header("보스 (Spec 5장 보스)")]
    public float bossFirstPatternDelay = 1.5f;
    public float bossPatternCooldown = 1.6f;
    public float bossPatternCooldownEnraged = 1.1f;   // 체력 50% 이하
    public float bossSlamWindup = 0.8f;
    public float bossChargeWindup = 1f;
    public float bossChargeSpeed = 20f;
    public float bossChargeDistance = 14f;
    public float bossChargeDamageMul = 1.2f;
    public float bossChargeRecover = 1.2f;
    public float bossFarMin = 4f;               // 이보다 멀면 돌진·파동
    public float bossVolleyWindup = 0.9f;
    public float bossVolleySpread = 14f;        // 부채꼴 각도 (도)
    public float bossVolleyDamageMul = 0.7f;

    [Header("카드 효과 (Spec 3장)")]
    public float regenPauseAfterHit = 2f;       // 재생: 맞은 뒤 이 시간 동안 회복 중지

    [Header("라운드 성장 (Spec 5장)")]
    public float healthAttackPerRound = 0.08f;  // 체력·공격력 × (1 + 0.08 × (라운드 - 1))

    public float MaxHealth { get; private set; }
    public float CurrentHealth { get; private set; }
    public float Attack { get; private set; }
    public float Speed { get; private set; }
    public float AttackRange { get; private set; }
    public float AttackCooldown { get; private set; }
    public bool IsDead { get; private set; }

    public EnemyType Type => type;
    public bool IsRanged => type == EnemyType.Ranged;
    public bool IsBoss => type == EnemyType.Boss;
    public bool IsGuardBroken => Time.time < guardBrokenUntil;
    public bool IsSplitChild { get; private set; }   // 분열로 생긴 적 — 다시 분열하지 않음
    public int RevivesUsed { get; private set; }     // 죽음의 군세로 부활한 횟수 — 가능 횟수를 다 쓰면 다시 부활하지 않음
    public bool IsRevengeActive => Time.time < revengeUntil;
    public bool IsFrenzied => Time.time < frenzyUntil;          // 광란 시너지
    public bool IsRegenerating => regenPerSecond > 0f && !IsDead && CurrentHealth < MaxHealth
        && Time.time - lastDamagedTime >= RegenPause;

    // 불멸의 의지 조합: 저체력이면 재생 중지 시간이 짧아짐
    float RegenPause => comboUndyingWill && CurrentHealth <= MaxHealth * LowHealthRatio ? CardCombo.UndyingRegenPause : regenPauseAfterHit;

    const float HunterRange = 340f / 45f;        // 사냥꾼 감지 거리 7.56u
    static readonly Color RevengeColor = new Color(1f, 0.55f, 0.15f);
    static readonly Color RegenColor = new Color(0.35f, 1f, 0.45f);
    static readonly Color FrenzyColor = new Color(1f, 0.3f, 0.65f);

    // 스폰 시점의 카드 효과 (Spec 9장: 스폰 시 일괄 적용)
    private float damageTakenMul = 1f;
    private float lowHealthDamageTakenMul = 1f;
    private float lifesteal;
    private float regenPerSecond;
    private float lastDamagedTime = float.NegativeInfinity;  // 재생 중지 판정용
    private float hunterSpeedMul = 1f;
    private int berserkerStacks;
    private bool comboUndyingWill, comboDarkStalker, comboLastStand;  // 조합 시너지 (스폰 시 확정)

    // 복수 버프 (죽은 동료가 생길 때 BattleCardEffects가 걸어줌)
    private float revengeMul = 1f;
    private float revengeUntil;
    private float frenzyUntil;

    enum State { Chase, Windup, Charge, Recover }
    enum AttackKind { Melee, Shot, Volley, Charge }

    private Rigidbody2D rb;
    private BoxCollider2D col;
    private CharacterVisual visual;             // 그림(자식 Visual). 루트는 판정만
    private SpriteRenderer spriteRenderer;
    private HitFlash hitFlash;                  // 피격 번쩍임 (셰이더가 없으면 곱하기 색으로 대신)
    private GameObject telegraph;               // 예비동작 중 바라보는 방향 표시 삼각형
    private GameObject shieldVisual;            // 방패병 방패
    private readonly List<AimLine> aimLines = new List<AimLine>();  // 조준선·돌진 경로선 (필요할 때 생성)
    private EnemyHealthBar healthBar;
    private Vector2 aimDirection = Vector2.right;
    private Color baseColor = Color.white;
    private Color flashColor = Color.white;

    private PlayerHealth player;
    private PlayerMovement playerMovement;
    private Collider2D playerCollider;

    private bool initialized;
    private State state = State.Chase;
    private AttackKind pendingAttack;
    private int facing = 1;
    private float windupTimer;
    private float windupDuration;
    private float attackReadyTime;
    private float nextJumpTime;
    private float playerOnPlatformTime;
    private float playerBelowTime;
    private float playerLandedFeetY;            // 플레이어가 마지막으로 착지한 발 높이
    private bool playerLandedKnown;
    private bool platformJumpActive;            // 발판 추격 점프 중 (착지하면 끝)
    private float platformJumpStartTime;
    private float platformJumpTargetX;
    private float platformJumpSpeedX;
    private bool ignoresPlatforms;              // 발판 아래 통과 높이보다 키가 커서 발판과 아예 충돌하지 않는 적 (거인화 보스 등)
    private Collider2D droppingThrough;         // 하강 중 충돌을 끈 발판
    // (발판 충돌 복구는 시간이 아니라 "겹치지 않게 되었을 때" — UpdateDropThrough)
    private readonly List<ContactPoint2D> groundContacts = new List<ContactPoint2D>();
    private float knockbackTimer;
    private float knockbackVelocity;
    private float flashTimer;
    private ContactFilter2D groundFilter;

    // 방패병
    private float guardBrokenUntil;
    private float turnTimer;

    // 돌진 (돌진병·보스)
    private int chargeDirection = 1;
    private float chargeSpeed;
    private float chargeDistance;
    private float chargeDamageMul;
    private float chargeRecover;
    private float chargeTravelled;
    private bool chargeHitPlayer;
    private float chargeReadyTime;
    private float recoverTimer;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<BoxCollider2D>();
        visual = CharacterVisual.Ensure(gameObject);
        spriteRenderer = visual.Renderer;
        hitFlash = HitFlash.Ensure(spriteRenderer);
        rb.gravityScale = gravityScale;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;  // 카메라가 움직일 때 떨려 보이지 않게
        RuntimeMaterials.ApplyNoFriction(gameObject);             // 벽·죽음의 영역 벽에 붙어 공중에 멈추지 않게

        groundFilter.useTriggers = false;
        groundFilter.SetNormalAngle(45f, 135f);

        telegraph = CreateTelegraph();
        shieldVisual = CreateShieldVisual();
        healthBar = EnemyHealthBar.Create(this);
    }

    void OnEnable() => Active.Add(this);
    void OnDisable() => Active.Remove(this);

    // 스포너가 생성 직후 호출. 종류·강화·위험도를 곱해서 이 적의 수치를 확정
    public void Initialize(EnemyType type, EnemyModifiers modifiers, PlayerHealth player)
    {
        this.type = type;
        this.player = player;

        float healthRatio = 1f, attackRatio = 1f, speedRatio = 1f;
        Vector2 size;
        switch (type)
        {
            case EnemyType.Small:
                healthRatio = 0.4f; attackRatio = 0.7f; speedRatio = 1.15f;
                AttackRange = 28f / 45f;
                AttackCooldown = 92f / 60f;
                size = new Vector2(0.55f, 0.72f);
                baseColor = new Color(0.55f, 0.65f, 0.4f);
                break;
            case EnemyType.Elite:
                healthRatio = 2.4f; attackRatio = 1.7f;
                AttackRange = 50f / 45f;
                AttackCooldown = 78f / 60f;
                size = new Vector2(1.0f, 1.4f);
                baseColor = new Color(0.35f, 0.25f, 0.45f);
                break;
            case EnemyType.Ranged:
                // 원거리: 체력 40 / 공격 8 / 속도 2.2 (일반 60 / 10 / 2.5 기준 비율)
                healthRatio = 40f / 60f; attackRatio = 0.8f; speedRatio = 2.2f / 2.5f;
                AttackRange = rangedRange;
                AttackCooldown = rangedCooldown;
                size = new Vector2(0.7f, 1.1f);
                baseColor = new Color(0.3f, 0.45f, 0.8f);
                // 생성 직후 여러 마리가 동시에 쏘지 않게 첫 사격을 1~2초 무작위로 늦춤
                attackReadyTime = Time.time + Random.Range(1f, 2f);
                break;
            case EnemyType.Shield:
                // 방패병: 체력 80 / 공격 12 / 속도 1.9
                healthRatio = 80f / 60f; attackRatio = 1.2f; speedRatio = 1.9f / 2.5f;
                AttackRange = 38f / 45f;
                AttackCooldown = 1.8f;
                size = new Vector2(0.85f, 1.15f);
                baseColor = new Color(0.4f, 0.48f, 0.55f);
                break;
            case EnemyType.Charger:
                // 돌진병: 체력 55 / 공격 14 / 속도 2.3
                healthRatio = 55f / 60f; attackRatio = 1.4f; speedRatio = 2.3f / 2.5f;
                AttackRange = 38f / 45f;
                AttackCooldown = 92f / 60f;
                size = new Vector2(0.8f, 0.95f);
                baseColor = new Color(0.6f, 0.32f, 0.22f);
                chargeReadyTime = Time.time + Random.Range(1f, 2f);  // 생성 직후 바로 돌진하지 않게
                break;
            case EnemyType.Boss:
                // 보스: 일반의 체력 ×12 / 공격 ×1.8 / 속도 ×0.8
                healthRatio = 12f; attackRatio = 1.8f; speedRatio = 0.8f;
                AttackRange = 2.2f;
                AttackCooldown = bossPatternCooldown;
                size = new Vector2(1.5f, 2.1f);
                baseColor = new Color(0.3f, 0.12f, 0.3f);
                attackReadyTime = Time.time + bossFirstPatternDelay;
                break;
            default:
                AttackRange = 38f / 45f;
                AttackCooldown = 92f / 60f;
                size = new Vector2(0.75f, 1.02f);
                baseColor = new Color(0.5f, 0.45f, 0.6f);
                break;
        }

        // 거인화 시너지 (Lv2: 크기·받는 피해, Lv3: + 체력·공격력)
        // 캐릭터 크기 배율 (Spec 8장) — 몸과 근접 사거리를 함께 키움.
        // 원거리 적의 사거리는 세계 거리(10u)라 그대로
        float sizeScale = IsBoss ? CharacterVisual.BossSizeScale : CharacterVisual.SizeScale;
        size *= sizeScale;
        if (type != EnemyType.Ranged) AttackRange *= sizeScale;

        int giantLevel = RunState.Instance != null ? RunState.Instance.SynergyLevel(CardCategory.Stat) : 0;
        float giantStats = giantLevel >= 3 ? Synergy.GiantStatsLv3 : 1f;
        float giantSize = giantLevel >= 3 ? Synergy.GiantSizeLv3 : giantLevel >= 2 ? Synergy.GiantSizeLv2 : 1f;
        size *= giantSize;

        // 라운드 성장: 카드와 별개로 라운드마다 체력·공격력 상승
        int round = RunState.Instance != null ? RunState.Instance.round : 1;
        float roundMul = 1f + healthAttackPerRound * (round - 1);

        MaxHealth = baseHealth * healthRatio * modifiers.healthMul * modifiers.RiskMul * roundMul * giantStats;
        Attack = baseAttack * attackRatio * modifiers.attackMul * modifiers.RiskMul * roundMul * giantStats;
        Speed = baseSpeed * speedRatio * modifiers.speedMul;   // 위험도는 속도에 미적용
        CurrentHealth = MaxHealth;

        damageTakenMul = modifiers.damageTakenMul * (giantLevel >= 2 ? Synergy.GiantDamageTaken : 1f);
        lowHealthDamageTakenMul = modifiers.lowHealthDamageTakenMul;
        lifesteal = modifiers.lifesteal;
        regenPerSecond = modifiers.regenPerSecond;
        hunterSpeedMul = modifiers.hunterSpeedMul;
        berserkerStacks = modifiers.berserkerStacks;
        comboUndyingWill = CardCombo.IsActive(ComboId.UndyingWill);
        comboDarkStalker = CardCombo.IsActive(ComboId.DarkStalker);
        comboLastStand = CardCombo.IsActive(ComboId.LastStand);

        // 판정 크기 = 콜라이더 크기 (루트 스케일은 1 고정, 그림은 CharacterVisual이 맞춤)
        visual.SetBodySize(size, giantSize);  // 도트 그림도 거인화 배율만큼 크게

        // 발판 아래를 지나갈 수 없을 만큼 큰 적(거인화 보스 등)은 발판과 아예 충돌하지 않게 — 끼임 방지
        ignoresPlatforms = size.y > ArenaLayout.MinPlatformClearance();
        if (ignoresPlatforms) IgnoreAllPlatforms();
        if (spriteRenderer != null) spriteRenderer.color = baseColor;
        shieldVisual.transform.localScale = new Vector3(0.14f, 0.9f * size.y, 1f);
        healthBar.Layout(size);

        initialized = true;
    }

    void Start()
    {
        // 씬에 직접 배치한 적은 스포너가 없으니 기본값으로 초기화
        if (player == null) player = FindFirstObjectByType<PlayerHealth>();
        if (!initialized) Initialize(type, new EnemyModifiers(), player);

        if (player != null)
        {
            playerMovement = player.GetComponent<PlayerMovement>();
            playerCollider = player.GetComponent<Collider2D>();
            // 몸 충돌 없음: 플레이어·다른 적과 서로 통과 (Spec 5장)
            if (playerCollider != null) Physics2D.IgnoreCollision(col, playerCollider);
        }
        foreach (Enemy other in Active)
        {
            if (other != this) Physics2D.IgnoreCollision(col, other.col);
        }
    }

    void FixedUpdate()
    {
        if (IsDead) return;

        float dt = Time.fixedDeltaTime;
        bool grounded = rb.IsTouching(groundFilter);
        Vector2 velocity = rb.linearVelocity;

        // 재생: 초당 최대 체력의 일정 비율 회복. 맞은 뒤 regenPauseAfterHit초 동안은 멈춤
        if (IsRegenerating) CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + MaxHealth * regenPerSecond * dt);

        bool hasTarget = player != null && !player.IsDead;
        Vector2 toPlayer = hasTarget ? (Vector2)player.transform.position - rb.position : Vector2.zero;
        TrackPlayerOnPlatform(hasTarget, dt);
        UpdateDropThrough();

        if (state == State.Charge)
        {
            // 돌진: 밀어내기·겹침 방지·플레이어 최소 간격 모두 무시
            UpdateCharge(ref velocity, dt, hasTarget);
            rb.linearVelocity = velocity;
            return;
        }

        if (knockbackTimer > 0f)
        {
            // 3타 밀어내기: 0.1초 동안 AI보다 우선
            knockbackTimer -= dt;
            velocity.x = knockbackVelocity;
        }
        else if (state == State.Windup)
        {
            velocity.x = 0f;  // 예비동작 중 정지
            windupTimer -= dt;
            if (windupTimer <= 0f) FinishAttack(hasTarget);
        }
        else if (state == State.Recover)
        {
            velocity.x = 0f;  // 돌진 후 지침
            recoverTimer -= dt;
            if (recoverTimer <= 0f) state = State.Chase;
        }
        else if (hasTarget)
        {
            UpdateFacing(toPlayer, dt);
            int moveDirection = Mathf.Abs(toPlayer.x) > 0.05f ? (toPlayer.x > 0f ? 1 : -1) : facing;

            // 사냥꾼: 플레이어가 가까우면 더 빠르게
            float speed = toPlayer.sqrMagnitude <= HunterRange * HunterRange ? Speed * hunterSpeedMul : Speed;
            if (IsFrenzied) speed *= Synergy.FrenzySpeed;  // 광란
            // 어둠의 추적자 조합: 어둠 시야 밖이면 더 빠르게
            EnvironmentCardEffects env = EnvironmentCardEffects.Instance;
            if (comboDarkStalker && env != null && env.DarknessOuterRadius > 0f
                && toPlayer.sqrMagnitude > env.DarknessOuterRadius * env.DarknessOuterRadius) speed *= CardCombo.StalkerSpeedMul;

            if (IsRanged)
            {
                // 거리 유지: 5u보다 가까우면 물러나고, 8u보다 멀면 다가감. 발판 추격 없음
                float distanceX = Mathf.Abs(toPlayer.x);
                if (distanceX < rangedKeepMin) velocity.x = -facing * speed;
                else if (distanceX > rangedKeepMax) velocity.x = facing * speed;
                else velocity.x = 0f;

                if (grounded && Time.time >= attackReadyTime && toPlayer.sqrMagnitude <= rangedRange * rangedRange)
                {
                    StartWindup(AttackKind.Shot);
                    velocity.x = 0f;
                }
            }
            else
            {
                bool closeEnough = Mathf.Abs(toPlayer.x) <= AttackRange * StopDistanceRatio;
                velocity.x = closeEnough ? 0f : moveDirection * speed;
                UpdatePlatformJump(ref velocity, grounded, dt);

                AttackKind? attack = grounded ? ChooseAttack(toPlayer) : (AttackKind?)null;
                if (attack.HasValue)
                {
                    StartWindup(attack.Value);
                    velocity.x = 0f;
                }
                else if (grounded)
                {
                    if (!ignoresPlatforms)   // 발판을 통과하는 큰 적은 발판 추격/하강을 하지 않음
                    {
                        TryPlatformJump(ref velocity, toPlayer);
                        TryDropThrough(toPlayer);
                    }
                }
            }
        }
        else
        {
            velocity.x = 0f;
        }

        // 이동으로 플레이어 최소 간격 안까지 파고들지 않게 속도를 자름 (발판 추격 점프 중에는 발판 위로 넘어가야 해서 제외)
        if (!platformJumpActive)
        {
            float nextX = ClampTowardPlayer(rb.position.x, rb.position.x + velocity.x * dt);
            velocity.x = (nextX - rb.position.x) / dt;
        }

        rb.linearVelocity = velocity;
        SeparateFromOthers();
    }

    void Update()
    {
        flashTimer -= Time.deltaTime;

        if (spriteRenderer != null)
        {
            // 도트 스프라이트는 자체 색이 있으므로 흰색 기준으로 물들임 (임시 사각형만 종류별 색)
            // 피격 번쩍임은 셰이더(HitFlash)로 상태 색 위에 덮음 — 곱하기 색으로는 도트가 하얗게 안 됨
            Color body = visual.IsPixelArt ? Color.white : baseColor;
            bool flashing = flashTimer > 0f;
            if (hitFlash != null && hitFlash.Supported)
                hitFlash.Set(flashColor, flashing ? (flashColor == BlockFlashColor ? BlockFlashAmount : HitFlashAmount) : 0f);

            if (flashing && (hitFlash == null || !hitFlash.Supported)) spriteRenderer.color = flashColor;
            else if (state == State.Windup) spriteRenderer.color = WindupColor;
            else if (state == State.Recover) spriteRenderer.color = Color.Lerp(body, RecoverColor, 0.6f);
            else if (IsFrenzied) spriteRenderer.color = Color.Lerp(body, FrenzyColor, 0.65f);
            else if (IsRevengeActive) spriteRenderer.color = Color.Lerp(body, RevengeColor, 0.6f);
            else if (IsRegenerating) spriteRenderer.color = Color.Lerp(body, RegenColor, 0.35f + 0.15f * Mathf.Sin(Time.time * 6f));
            else spriteRenderer.color = body;
        }

        UpdateAimLines();

        // 방향 삼각형: 근접 공격·돌진 예고
        bool showTriangle = state == State.Windup && !IsDead && (pendingAttack == AttackKind.Melee || pendingAttack == AttackKind.Charge);
        telegraph.SetActive(showTriangle);
        float halfWidth = visual.BodySize.x * 0.5f;
        if (showTriangle)
        {
            // 몸 가장자리 바로 바깥에 방향 삼각형 표시
            telegraph.transform.localPosition = new Vector3(facing * (halfWidth + 0.08f), 0f, 0f);
            telegraph.transform.localScale = new Vector3(facing, 1f, 1f);
        }

        bool showShield = type == EnemyType.Shield && !IsDead && !IsGuardBroken;
        shieldVisual.SetActive(showShield);
        if (showShield) shieldVisual.transform.localPosition = new Vector3(facing * (halfWidth + 0.07f), 0f, 0f);
        visual.SetFacing(facing);
    }

    // 조준선(원거리 1개·보스 파동 3개)·돌진 경로선 표시
    void UpdateAimLines()
    {
        int shown = 0;
        if (state == State.Windup && !IsDead && pendingAttack != AttackKind.Melee)
        {
            float progress = windupDuration > 0f ? 1f - windupTimer / windupDuration : 1f;
            switch (pendingAttack)
            {
                case AttackKind.Shot:
                    GetAimLine(shown++).Show(AimOrigin(), aimDirection, rangedRange, progress);
                    break;
                case AttackKind.Volley:
                    for (int i = -1; i <= 1; i++)
                        GetAimLine(shown++).Show(AimOrigin(), Rotate(aimDirection, i * bossVolleySpread), projectileMaxDistance, progress);
                    break;
                case AttackKind.Charge:
                    GetAimLine(shown++).Show(rb.position, new Vector2(chargeDirection, 0f), ChargeReach(), progress);
                    break;
            }
        }
        for (int i = shown; i < aimLines.Count; i++) aimLines[i].Hide();
    }

    AimLine GetAimLine(int index)
    {
        while (aimLines.Count <= index) aimLines.Add(AimLine.Create());
        return aimLines[index];
    }

    static Vector2 Rotate(Vector2 v, float degrees) => Quaternion.Euler(0f, 0f, degrees) * v;

    // 방패병은 플레이어가 등 뒤로 넘어가도 잠깐 원래 방향 유지 (뒤잡기 공략)
    void UpdateFacing(Vector2 toPlayer, float dt)
    {
        if (Mathf.Abs(toPlayer.x) <= 0.05f) return;
        int want = toPlayer.x > 0f ? 1 : -1;
        if (want == facing)
        {
            turnTimer = 0f;
            return;
        }
        if (type == EnemyType.Shield)
        {
            turnTimer += dt;
            if (turnTimer < shieldTurnDelay) return;
        }
        facing = want;
        turnTimer = 0f;
    }

    // 지상에서 이번 프레임에 시작할 공격. 없으면 null
    AttackKind? ChooseAttack(Vector2 toPlayer)
    {
        if (IsBoss)
        {
            if (Time.time < attackReadyTime) return null;
            if (InAttackZone(toPlayer)) return AttackKind.Melee;

            float distanceX = Mathf.Abs(toPlayer.x);
            if (distanceX < bossFarMin) return null;
            bool canCharge = SameLayerAsPlayer() && distanceX <= bossChargeDistance;
            bool canVolley = toPlayer.sqrMagnitude <= projectileMaxDistance * projectileMaxDistance;
            if (canCharge && (!canVolley || Random.value < 0.5f)) return AttackKind.Charge;
            return canVolley ? AttackKind.Volley : null;
        }

        if (type == EnemyType.Charger && Time.time >= chargeReadyTime && SameLayerAsPlayer())
        {
            float distanceX = Mathf.Abs(toPlayer.x);
            if (distanceX >= chargerMinTrigger && distanceX <= chargerDistance) return AttackKind.Charge;
        }

        if (Time.time >= attackReadyTime && InAttackZone(toPlayer)) return AttackKind.Melee;
        return null;
    }

    // 적 중심 ↔ 플레이어 중심 거리가 사거리 이내이고, 바라보는 쪽(전방 반원)인지
    bool InAttackZone(Vector2 toPlayer)
    {
        return toPlayer.sqrMagnitude <= AttackRange * AttackRange && toPlayer.x * facing >= -0.05f;
    }

    // 플레이어와 발 높이 차가 1u 미만 (돌진 조건)
    bool SameLayerAsPlayer()
    {
        return playerCollider != null && Mathf.Abs(playerCollider.bounds.min.y - col.bounds.min.y) < PlatformHeightThreshold;
    }

    void StartWindup(AttackKind kind)
    {
        state = State.Windup;
        pendingAttack = kind;
        // 예고음: 원거리 사격·마력 파동·돌진만 (근접은 적이 많아 시끄러워짐)
        if (kind != AttackKind.Melee) SoundManager.Play(SoundId.Warn);
        Vector2 toPlayer = (Vector2)player.transform.position - rb.position;

        switch (kind)
        {
            case AttackKind.Melee:
                windupDuration = IsBoss ? bossSlamWindup : windupTime;
                break;
            case AttackKind.Shot:
            case AttackKind.Volley:
                windupDuration = kind == AttackKind.Shot ? rangedWindupTime : bossVolleyWindup;
                // 조준 방향은 예고 시작 순간 고정 — 조준선 밖으로 벗어나면 피할 수 있음
                Vector2 toTarget = (Vector2)player.transform.position - AimOrigin();
                aimDirection = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : new Vector2(facing, 0f);
                break;
            case AttackKind.Charge:
                windupDuration = IsBoss ? bossChargeWindup : chargerWindupTime;
                chargeDirection = toPlayer.x >= 0f ? 1 : -1;   // 좌우만, 예고 시작 순간 고정
                facing = chargeDirection;
                chargeSpeed = IsBoss ? bossChargeSpeed : chargerSpeed;
                chargeDistance = IsBoss ? bossChargeDistance : chargerDistance;
                chargeDamageMul = IsBoss ? bossChargeDamageMul : chargerDamageMul;
                chargeRecover = IsBoss ? bossChargeRecover : chargerRecoverTime;
                break;
        }
        windupTimer = windupDuration;
    }

    // 마법구가 나가는 위치: 몸 중심에서 조준 방향으로 살짝 앞
    Vector2 AimOrigin() => rb.position + new Vector2(0f, 0.15f);

    // 보스는 체력 50% 이하에서 패턴 간격이 짧아짐
    float PatternCooldown() => !IsBoss ? AttackCooldown
        : CurrentHealth <= MaxHealth * 0.5f ? bossPatternCooldownEnraged : bossPatternCooldown;

    void FinishAttack(bool hasTarget)
    {
        switch (pendingAttack)
        {
            case AttackKind.Shot:
                state = State.Chase;
                attackReadyTime = Time.time + AttackCooldown;
                FireProjectile(aimDirection, 1f);
                return;

            case AttackKind.Volley:
                state = State.Chase;
                attackReadyTime = Time.time + PatternCooldown();
                for (int i = -1; i <= 1; i++) FireProjectile(Rotate(aimDirection, i * bossVolleySpread), bossVolleyDamageMul);
                return;

            case AttackKind.Charge:
                state = State.Charge;
                chargeTravelled = 0f;
                chargeHitPlayer = false;
                knockbackTimer = 0f;
                return;
        }

        state = State.Chase;
        attackReadyTime = Time.time + PatternCooldown();

        SlashEffect.Spawn(transform, AttackRange, facing, 0, AttackEffectColor);

        // 예비동작 동안 빠져나갔으면 헛스윙
        if (!hasTarget) return;
        Vector2 toPlayer = (Vector2)player.transform.position - rb.position;
        if (!InAttackZone(toPlayer)) return;

        float dealt = player.TakeHit(new HitInfo
        {
            damage = CurrentAttack(),
            direction = new Vector2(facing, 0f),
            knockback = 0f,
            cancelAttack = false,
            source = gameObject,
        });
        ApplyLifesteal(dealt);
    }

    void FireProjectile(Vector2 direction, float damageMul)
    {
        Vector2 origin = AimOrigin() + direction * 0.5f;
        EnemyProjectile.Spawn(this, origin, direction * projectileSpeed, CurrentAttack() * damageMul,
            projectileRadius, projectileMaxDistance);
    }

    // 돌진이 전장 경계에 막히기 전까지 갈 수 있는 거리 (경로선 길이)
    float ChargeReach()
    {
        float halfWidth = col.bounds.extents.x;
        float limit = chargeDirection > 0 ? ArenaBounds.MaxX - halfWidth : ArenaBounds.MinX + halfWidth;
        return Mathf.Clamp((limit - rb.position.x) * chargeDirection, 0f, chargeDistance);
    }

    void UpdateCharge(ref Vector2 velocity, float dt, bool hasTarget)
    {
        float step = Mathf.Min(chargeSpeed * dt, ChargeReach(), chargeDistance - chargeTravelled);
        if (step <= 0.0001f)
        {
            EndCharge(ref velocity);
            return;
        }

        float fromX = rb.position.x;
        float toX = fromX + chargeDirection * step;
        velocity.x = chargeDirection * step / dt;
        chargeTravelled += step;

        // 이번 스텝에 몸이 쓸고 지나가는 범위에 플레이어가 있으면 1회 피해
        if (hasTarget && !chargeHitPlayer && playerCollider != null)
        {
            Bounds body = col.bounds;
            Bounds target = playerCollider.bounds;
            float minX = Mathf.Min(fromX, toX) - body.extents.x;
            float maxX = Mathf.Max(fromX, toX) + body.extents.x;
            bool overlapX = target.max.x >= minX && target.min.x <= maxX;
            bool overlapY = target.max.y >= body.min.y && target.min.y <= body.max.y;
            if (overlapX && overlapY)
            {
                chargeHitPlayer = true;
                float dealt = player.TakeHit(new HitInfo
                {
                    damage = CurrentAttack() * chargeDamageMul,
                    direction = new Vector2(chargeDirection, 0f),
                    source = gameObject,
                });
                ApplyLifesteal(dealt);
            }
        }
    }

    void EndCharge(ref Vector2 velocity)
    {
        velocity.x = 0f;
        state = State.Recover;
        recoverTimer = chargeRecover;
        if (IsBoss) attackReadyTime = Time.time + chargeRecover + PatternCooldown();
        else chargeReadyTime = Time.time + chargeRecover + chargerCooldown;
    }

    // 탐식: 실제로 들어간 피해의 비율만큼 회복 (최대 체력 초과 없음)
    void ApplyLifesteal(float dealt)
    {
        if (dealt > 0f && lifesteal > 0f) CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + dealt * lifesteal);
    }

    // 마법구가 플레이어에게 맞았을 때 (탐식 흡혈)
    public void OnProjectileHit(float dealt)
    {
        if (IsDead) return;
        ApplyLifesteal(dealt);
    }

    // 공격 순간의 공격력: 기본(스폰 시 확정) × 복수 버프 × 광란 × 광전사(1 + 장수 × 이번 라운드 사망률)
    float CurrentAttack()
    {
        float attack = Attack;
        if (IsRevengeActive) attack *= revengeMul;
        if (IsFrenzied) attack *= Synergy.FrenzyAttack;
        if (berserkerStacks > 0 && RoundManager.Instance != null) attack *= 1f + berserkerStacks * RoundManager.Instance.DeathRate;
        return attack;
    }

    // 광란: 폭주(속도·공격력 증가). 다시 걸리면 시간 갱신
    public void ApplyFrenzy(float duration)
    {
        frenzyUntil = Mathf.Max(frenzyUntil, Time.time + duration);
    }

    public void ApplyRevenge(float multiplier, float duration)
    {
        revengeMul = multiplier;                 // 배율은 쌓이지 않고, 시간만 갱신
        revengeUntil = Time.time + duration;
    }

    // 불꽃 포식 조합: 초당 최대 체력 비율만큼 회복
    public void HealOverTime(float ratioPerSecond, float dt)
    {
        if (IsDead) return;
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + MaxHealth * ratioPerSecond * dt);
    }

    public void MarkSplitChild() => IsSplitChild = true;
    public void SetRevivesUsed(int count) => RevivesUsed = count;
    public void SetHealthRatio(float ratio) => CurrentHealth = MaxHealth * ratio;

    // 플레이어가 마지막으로 착지한 높이가 나보다 높은(낮은) 상태로 지난 시간을 잰다.
    // 그 자리에서 점프해도 착지 높이는 그대로라 초기화되지 않음 — 아래층에서 점프 공격을 반복해도 결국 내려옴.
    // 땅에서 점프로 피하는 것은 착지 높이가 낮아서 발판 추격을 부르지 않음
    void TrackPlayerOnPlatform(bool hasTarget, float dt)
    {
        if (hasTarget && playerMovement != null && playerCollider != null && playerMovement.IsGrounded)
        {
            playerLandedFeetY = playerCollider.bounds.min.y;
            playerLandedKnown = true;
        }

        bool known = hasTarget && playerLandedKnown;
        float heightDiff = known ? playerLandedFeetY - col.bounds.min.y : 0f;
        playerOnPlatformTime = known && heightDiff >= PlatformHeightThreshold ? playerOnPlatformTime + dt : 0f;
        playerBelowTime = known && -heightDiff >= PlatformHeightThreshold ? playerBelowTime + dt : 0f;
    }

    // 발판 하강: 플레이어가 아래층에 0.9초 이상 서 있고 가로로 가까우면, 밟고 있는 발판을 뚫고 내려감.
    // 가로로 멀면 추격하다 발판 끝에서 자연히 떨어짐. 원거리 적은 위에서 사격하므로 내려오지 않음
    void TryDropThrough(Vector2 toPlayer)
    {
        if (droppingThrough != null || Time.time < nextJumpTime) return;
        if (playerBelowTime < platformChaseDelay) return;
        if (Mathf.Abs(toPlayer.x) > Mathf.Max(jumpTriggerDistance, AttackRange * StopDistanceRatio + 0.5f)) return;

        groundContacts.Clear();
        rb.GetContacts(groundFilter, groundContacts);
        foreach (ContactPoint2D contact in groundContacts)
        {
            Collider2D platform = contact.collider;
            if (platform == null || platform.GetComponent<OneWayPlatform>() == null) continue;

            Physics2D.IgnoreCollision(col, platform, true);
            droppingThrough = platform;
            nextJumpTime = Time.time + Random.Range(jumpCooldownMin, jumpCooldownMax);
            return;
        }
    }

    // 몸이 큰 적은 발판을 통과 (지면·벽만 밟음). 발판은 전투 중 생기지 않으므로 한 번만 훑음
    void IgnoreAllPlatforms()
    {
        foreach (OneWayPlatform platform in FindObjectsByType<OneWayPlatform>(FindObjectsSortMode.None))
        {
            Collider2D platformCollider = platform.GetComponent<Collider2D>();
            if (platformCollider != null) Physics2D.IgnoreCollision(col, platformCollider, true);
        }
    }

    // 발판과 겹치지 않게 된 순간 충돌 복구.
    // **겹쳐 있는 동안에는 시간이 지나도 복구하지 않음** — 겹친 채 되살리면 발판에 낌 (키 큰 적)
    void UpdateDropThrough()
    {
        if (droppingThrough == null) return;
        if (col.bounds.Intersects(droppingThrough.bounds)) return;
        Physics2D.IgnoreCollision(col, droppingThrough, false);
        droppingThrough = null;
    }

    void TryPlatformJump(ref Vector2 velocity, Vector2 toPlayer)
    {
        if (Time.time < nextJumpTime) return;
        if (playerOnPlatformTime < platformChaseDelay) return;
        if (Mathf.Abs(toPlayer.x) > jumpTriggerDistance) return;

        // 플레이어 발 높이 + 여유까지 한 번에 도달하는 초속도.
        // 물리 스텝(0.02초) 때문에 최고점이 낮아지는 만큼 g·dt/2를 더해 보정
        float height = playerLandedFeetY - col.bounds.min.y + jumpExtraHeight;  // 플레이어가 점프 중이어도 착지해 있던 발판 높이 기준
        float gravity = Mathf.Abs(Physics2D.gravity.y) * rb.gravityScale;
        velocity.y = Mathf.Sqrt(2f * gravity * height) + gravity * Time.fixedDeltaTime * 0.5f;

        // 공중에서 플레이어가 서 있던 x로 이동 — 제자리 수직 점프면 발판 끝 바깥에서 뛸 때 발판에 못 올라섬.
        // 최고점에 닿을 때까지 도착하는 속도(최소 이동 속도). one-way 발판이라 아래·옆에서는 통과하고 내려오며 착지
        float apexTime = Mathf.Max(0.05f, velocity.y / gravity);
        platformJumpActive = true;
        platformJumpStartTime = Time.time;
        platformJumpTargetX = rb.position.x + toPlayer.x;
        platformJumpSpeedX = Mathf.Max(Speed, Mathf.Abs(toPlayer.x) / apexTime);

        nextJumpTime = Time.time + Random.Range(jumpCooldownMin, jumpCooldownMax);
    }

    // 발판 추격 점프 중 가로 이동. 착지하면 끝 (점프 직후 몇 프레임은 아직 바닥에 닿아 있어 제외)
    void UpdatePlatformJump(ref Vector2 velocity, bool grounded, float dt)
    {
        if (!platformJumpActive) return;
        if (grounded && Time.time - platformJumpStartTime > 0.15f)
        {
            platformJumpActive = false;
            return;
        }
        float remaining = platformJumpTargetX - rb.position.x;
        velocity.x = Mathf.Clamp(remaining / dt, -platformJumpSpeedX, platformJumpSpeedX);
    }

    // 겹침 방지: 같은 층에 있는 적끼리 최소 간격 (w1+w2)×0.55. 서로 절반씩 밀어냄
    void SeparateFromOthers()
    {
        Vector2 myHalf = col.bounds.extents;
        foreach (Enemy other in Active)
        {
            if (other == this || other.IsDead) continue;

            Vector2 delta = rb.position - other.rb.position;
            Vector2 otherHalf = other.col.bounds.extents;
            if (Mathf.Abs(delta.y) > myHalf.y + otherHalf.y) continue;  // 다른 층

            float minDistance = (myHalf.x + otherHalf.x) * 2f * 0.55f;
            float overlap = minDistance - Mathf.Abs(delta.x);
            if (overlap <= 0f) continue;

            float dir = Mathf.Abs(delta.x) > 0.0001f ? Mathf.Sign(delta.x)
                : (GetInstanceID() < other.GetInstanceID() ? -1f : 1f);
            float x = rb.position.x;
            rb.position = new Vector2(ClampTowardPlayer(x, x + dir * overlap * 0.5f), rb.position.y);
        }
    }

    // 플레이어 최소 간격: 적이 스스로(추격·겹침 방지 밀림) 이 거리 안으로는 들어가지 않음.
    // 근접은 공격이 닿도록 멈추는 거리(사거리 80%), 원거리는 몸 가장자리가 닿는 거리
    float PlayerGap => IsRanged ? col.bounds.extents.x + playerCollider.bounds.extents.x
                                : AttackRange * StopDistanceRatio;

    // x를 from → to로 옮길 때 플레이어 쪽으로 최소 간격 안까지 파고드는 부분을 잘라냄.
    // 멀어지는 이동은 그대로, 이미 간격 안이면 더 다가가지만 않음. 플레이어가 파고드는 건 막지 않음(몸 충돌 없음)
    float ClampTowardPlayer(float from, float to)
    {
        if (player == null || player.IsDead || playerCollider == null) return to;

        Bounds playerBounds = playerCollider.bounds;
        if (Mathf.Abs(playerBounds.center.y - rb.position.y) >= col.bounds.extents.y + playerBounds.extents.y) return to;  // 다른 층

        float side = from - playerBounds.center.x;
        if (Mathf.Abs(side) < 0.0001f) return to;
        float sign = Mathf.Sign(side);
        if ((to - from) * sign >= 0f) return to;       // 멀어지는 쪽

        float gap = PlayerGap;
        if (Mathf.Abs(side) <= gap) return from;       // 이미 간격 안
        float limit = playerBounds.center.x + sign * gap;
        return sign > 0f ? Mathf.Max(to, limit) : Mathf.Min(to, limit);
    }

    public float TakeHit(HitInfo hit)
    {
        if (IsDead) return 0f;

        // 강철 피부: 모든 피해에 곱. 불굴: 맞기 직전 체력이 30% 이하일 때 추가로 곱
        float damage = hit.damage * damageTakenMul;
        if (CurrentHealth <= MaxHealth * LowHealthRatio) damage *= lowHealthDamageTakenMul;

        // 최후의 저항 조합: 죽음의 시계가 얼마 남지 않으면 덜 아프게
        EnvironmentCardEffects env = EnvironmentCardEffects.Instance;
        if (comboLastStand && env != null && env.DeathClockActive && env.DeathClockRemaining <= CardCombo.LastStandClockTime)
            damage *= CardCombo.LastStandDamageTaken;

        // 방패: 정면(피해 방향이 바라보는 방향과 반대)에서 온 피해 감소. 3타·스킬은 방패를 뚫고 내리게 함
        flashColor = Color.white;
        if (type == EnemyType.Shield && !IsGuardBroken && !hit.damageOverTime && hit.direction.x * facing < -0.01f)
        {
            if (hit.guardBreak)
            {
                guardBrokenUntil = Time.time + shieldBreakDuration;
            }
            else
            {
                damage *= shieldDamageMul;
                flashColor = BlockFlashColor;
            }
        }

        float dealt = Mathf.Min(damage, CurrentHealth);
        CurrentHealth -= damage;
        flashTimer = FlashTime;        if (damage > 0f) lastDamagedTime = Time.time;
        if (!hit.damageOverTime) SoundManager.Play(flashColor == BlockFlashColor ? SoundId.Block : SoundId.Hit);

        // 보스와 돌진 중인 적은 밀리지 않고, 보스는 패턴도 끊기지 않음
        bool staggerable = !IsBoss && state != State.Charge;
        if (hit.knockback > 0f && staggerable)
        {
            knockbackTimer = KnockbackTime;
            knockbackVelocity = Mathf.Sign(hit.direction.x) * hit.knockback / KnockbackTime;
        }

        // 3타: 예비동작 취소 → 쿨 처음부터
        if (hit.cancelAttack && staggerable && state == State.Windup)
        {
            state = State.Chase;
            if (pendingAttack == AttackKind.Charge) chargeReadyTime = Time.time + chargerCooldown;
            else attackReadyTime = Time.time + AttackCooldown;
        }

        if (CurrentHealth <= 0f) Die();
        return dealt;
    }

    void OnDestroy()
    {
        // 조준선은 따로 떨어진 오브젝트라 함께 정리 (씬을 닫는 중이면 Unity가 알아서 지움)
        foreach (AimLine line in aimLines)
        {
            if (line != null && line.gameObject.scene.isLoaded) Destroy(line.gameObject);
        }
    }

    void Die()
    {
        IsDead = true;
        Killed?.Invoke(this);
        // 사망 분해 파티클 (Spec 8장). 색은 종류별 색 — 도트 적이 들어오면 스프라이트 평균색으로 바꿀지 검토
        DeathBurst.Spawn(transform.position, visual.BodySize, baseColor);
        SoundManager.Play(SoundId.Kill);
        Destroy(gameObject);
    }

    GameObject CreateTelegraph()
    {
        GameObject go = new GameObject("AttackTelegraph");
        go.transform.SetParent(transform, false);

        Mesh mesh = new Mesh();
        mesh.vertices = new[] { new Vector3(0f, 0.18f, 0f), new Vector3(0f, -0.18f, 0f), new Vector3(0.28f, 0f, 0f) };
        mesh.colors = new[] { WindupColor, WindupColor, WindupColor };
        mesh.triangles = new[] { 0, 2, 1 };
        mesh.RecalculateBounds();

        go.AddComponent<MeshFilter>().mesh = mesh;
        MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = RuntimeMaterials.SpriteUnlit;
        meshRenderer.sortingOrder = 50;

        go.SetActive(false);
        return go;
    }

    // 방패병의 방패: 가운데가 원점인 1×1 사각형. 크기는 Initialize, 위치는 Update에서 맞춤
    GameObject CreateShieldVisual()
    {
        GameObject go = new GameObject("Shield");
        go.transform.SetParent(transform, false);

        Mesh mesh = new Mesh();
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
        };
        mesh.colors = new[] { ShieldColor, ShieldColor, ShieldColor, ShieldColor };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();

        go.AddComponent<MeshFilter>().mesh = mesh;
        MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = RuntimeMaterials.SpriteUnlit;
        meshRenderer.sortingOrder = 40;

        go.SetActive(false);
        return go;
    }
}
