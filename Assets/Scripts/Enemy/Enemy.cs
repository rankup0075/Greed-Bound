using System.Collections.Generic;
using UnityEngine;

public enum EnemyType { Normal, Small, Elite }

// 적 1마리 (Spec 5장 적 기본 수치 / 적 AI).
// 추격 → 사거리 안이면 예비동작 0.4초(정지, 붉게 + 방향 표시) → 그 순간 다시 판정해서 공격 → 쿨.
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class Enemy : MonoBehaviour, IDamageable
{
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

    public float MaxHealth { get; private set; }
    public float CurrentHealth { get; private set; }
    public float Attack { get; private set; }
    public float Speed { get; private set; }
    public float AttackRange { get; private set; }
    public float AttackCooldown { get; private set; }
    public bool IsDead { get; private set; }

    // 스폰 시점의 카드 효과 (Spec 9장: 스폰 시 일괄 적용)
    private float damageTakenMul = 1f;
    private float lowHealthDamageTakenMul = 1f;
    private float lifesteal;

    enum State { Chase, Windup }

    private Rigidbody2D rb;
    private BoxCollider2D col;
    private SpriteRenderer spriteRenderer;
    private GameObject telegraph;               // 예비동작 중 바라보는 방향 표시 삼각형
    private Color baseColor = Color.white;

    private PlayerHealth player;
    private PlayerMovement playerMovement;
    private Collider2D playerCollider;

    private bool initialized;
    private State state = State.Chase;
    private int facing = 1;
    private float windupTimer;
    private float attackReadyTime;
    private float nextJumpTime;
    private float playerOnPlatformTime;
    private float knockbackTimer;
    private float knockbackVelocity;
    private float flashTimer;
    private ContactFilter2D groundFilter;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<BoxCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        rb.gravityScale = gravityScale;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;  // 카메라가 움직일 때 떨려 보이지 않게

        groundFilter.useTriggers = false;
        groundFilter.SetNormalAngle(45f, 135f);

        telegraph = CreateTelegraph();
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
            default:
                AttackRange = 38f / 45f;
                AttackCooldown = 92f / 60f;
                size = new Vector2(0.75f, 1.02f);
                baseColor = new Color(0.5f, 0.45f, 0.6f);
                break;
        }

        MaxHealth = baseHealth * healthRatio * modifiers.healthMul * modifiers.RiskMul;
        Attack = baseAttack * attackRatio * modifiers.attackMul * modifiers.RiskMul;
        Speed = baseSpeed * speedRatio * modifiers.speedMul;   // 위험도는 속도에 미적용
        CurrentHealth = MaxHealth;

        damageTakenMul = modifiers.damageTakenMul;
        lowHealthDamageTakenMul = modifiers.lowHealthDamageTakenMul;
        lifesteal = modifiers.lifesteal;

        // Square 스프라이트(1×1) 기준: 스케일이 곧 크기
        transform.localScale = new Vector3(size.x, size.y, 1f);
        col.size = Vector2.one;
        col.offset = Vector2.zero;
        if (spriteRenderer != null) spriteRenderer.color = baseColor;
        telegraph.transform.localScale = new Vector3(1f / size.x, 1f / size.y, 1f);

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

        bool hasTarget = player != null && !player.IsDead;
        Vector2 toPlayer = hasTarget ? (Vector2)player.transform.position - rb.position : Vector2.zero;
        TrackPlayerOnPlatform(hasTarget, dt);

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
        else if (hasTarget)
        {
            if (Mathf.Abs(toPlayer.x) > 0.05f) facing = toPlayer.x > 0f ? 1 : -1;

            bool closeEnough = Mathf.Abs(toPlayer.x) <= AttackRange * StopDistanceRatio;
            velocity.x = closeEnough ? 0f : facing * Speed;

            if (grounded && Time.time >= attackReadyTime && InAttackZone(toPlayer))
            {
                state = State.Windup;
                windupTimer = windupTime;
                velocity.x = 0f;
            }
            else if (grounded)
            {
                TryPlatformJump(ref velocity, toPlayer);
            }
        }
        else
        {
            velocity.x = 0f;
        }

        rb.linearVelocity = velocity;
        SeparateFromOthers();
    }

    void Update()
    {
        flashTimer -= Time.deltaTime;

        if (spriteRenderer != null)
        {
            if (flashTimer > 0f) spriteRenderer.color = Color.white;
            else if (state == State.Windup) spriteRenderer.color = WindupColor;
            else spriteRenderer.color = baseColor;
        }

        telegraph.SetActive(state == State.Windup && !IsDead);
        if (telegraph.activeSelf)
        {
            // 부모 스케일을 상쇄한 로컬 좌표. 몸 가장자리(0.5) 바로 바깥에 방향 삼각형 표시
            Vector3 scale = transform.localScale;
            telegraph.transform.localPosition = new Vector3(facing * (0.5f + 0.08f / scale.x), 0f, 0f);
            telegraph.transform.localScale = new Vector3(facing / scale.x, 1f / scale.y, 1f);
        }
    }

    // 적 중심 ↔ 플레이어 중심 거리가 사거리 이내이고, 바라보는 쪽(전방 반원)인지
    bool InAttackZone(Vector2 toPlayer)
    {
        return toPlayer.sqrMagnitude <= AttackRange * AttackRange && toPlayer.x * facing >= -0.05f;
    }

    void FinishAttack(bool hasTarget)
    {
        state = State.Chase;
        attackReadyTime = Time.time + AttackCooldown;

        SlashEffect.Spawn(transform, AttackRange, facing, 0, AttackEffectColor);

        // 예비동작 동안 빠져나갔으면 헛스윙
        if (!hasTarget) return;
        Vector2 toPlayer = (Vector2)player.transform.position - rb.position;
        if (!InAttackZone(toPlayer)) return;

        float dealt = player.TakeHit(new HitInfo
        {
            damage = Attack,
            direction = new Vector2(facing, 0f),
            knockback = 0f,
            cancelAttack = false,
            source = gameObject,
        });

        // 탐식: 실제로 들어간 피해의 비율만큼 회복 (최대 체력 초과 없음)
        if (dealt > 0f && lifesteal > 0f) CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + dealt * lifesteal);
    }

    // 플레이어가 나보다 높은 곳에 착지해 있는 시간을 잰다. 점프 중이거나 내려오면 0으로 리셋
    void TrackPlayerOnPlatform(bool hasTarget, float dt)
    {
        bool onHigherGround = hasTarget && playerMovement != null && playerCollider != null
            && playerMovement.IsGrounded
            && playerCollider.bounds.min.y - col.bounds.min.y >= PlatformHeightThreshold;
        playerOnPlatformTime = onHigherGround ? playerOnPlatformTime + dt : 0f;
    }

    void TryPlatformJump(ref Vector2 velocity, Vector2 toPlayer)
    {
        if (Time.time < nextJumpTime) return;
        if (playerOnPlatformTime < platformChaseDelay) return;
        if (Mathf.Abs(toPlayer.x) > jumpTriggerDistance) return;

        // 플레이어 발 높이 + 여유까지 한 번에 도달하는 초속도.
        // 물리 스텝(0.02초) 때문에 최고점이 낮아지는 만큼 g·dt/2를 더해 보정
        float height = playerCollider.bounds.min.y - col.bounds.min.y + jumpExtraHeight;
        float gravity = Mathf.Abs(Physics2D.gravity.y) * rb.gravityScale;
        velocity.y = Mathf.Sqrt(2f * gravity * height) + gravity * Time.fixedDeltaTime * 0.5f;

        nextJumpTime = Time.time + Random.Range(jumpCooldownMin, jumpCooldownMax);
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
            rb.position += new Vector2(dir * overlap * 0.5f, 0f);
        }
    }

    public float TakeHit(HitInfo hit)
    {
        if (IsDead) return 0f;

        // 강철 피부: 모든 피해에 곱. 불굴: 맞기 직전 체력이 30% 이하일 때 추가로 곱
        float damage = hit.damage * damageTakenMul;
        if (CurrentHealth <= MaxHealth * LowHealthRatio) damage *= lowHealthDamageTakenMul;

        float dealt = Mathf.Min(damage, CurrentHealth);
        CurrentHealth -= damage;
        flashTimer = FlashTime;

        if (hit.knockback > 0f)
        {
            knockbackTimer = KnockbackTime;
            knockbackVelocity = Mathf.Sign(hit.direction.x) * hit.knockback / KnockbackTime;
        }

        // 3타: 예비동작 취소 → 쿨 처음부터
        if (hit.cancelAttack && state == State.Windup)
        {
            state = State.Chase;
            attackReadyTime = Time.time + AttackCooldown;
        }

        if (CurrentHealth <= 0f) Die();
        return dealt;
    }

    void Die()
    {
        IsDead = true;
        Killed?.Invoke(this);
        // TODO: 사망 분해 파티클, 분열·부활·시체 폭발은 카드 시스템에서 (게임 루프 큐 사용)
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
}
