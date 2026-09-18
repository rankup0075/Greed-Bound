using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Z — 3단 콤보 근접 공격 (Spec 5장 3단 콤보).
// 판정: 플레이어 중심, 바라보는 방향 전방 반원. 검격 이펙트도 같은 반지름을 그대로 넘겨받아 그림.
[RequireComponent(typeof(PlayerMovement), typeof(PlayerStats))]
public class PlayerAttack : MonoBehaviour
{
    // 단계별 수치 (Spec 표 그대로). 인덱스 0 = 1타
    static readonly float[] DamageMul   = { 1.00f, 1.15f, 1.75f };
    static readonly float[] RangeMul    = { 1.00f, 1.05f, 1.35f };
    static readonly float[] CooldownMul = { 0.80f, 0.85f, 1.60f };

    [Header("기본 수치 (Spec 5장)")]
    public float baseDamage = 16f;           // + 0~7 랜덤
    public float baseRange = 1.91f;          // 86px ÷ 45
    public float baseCooldown = 15f / 60f;   // 15프레임 = 0.25초
    public float comboWindow = 52f / 60f;    // 52프레임 = 0.867초
    public float finisherKnockback = 1.02f;  // 3타 밀어내기 46px ÷ 45

    [Header("조작감")]
    public float inputBuffer = 0.2f;         // 쿨이 끝나기 이 시간 전부터 누른 Z는 저장했다가 쿨이 끝나는 즉시 발동

    [Header("판정 대상")]
    public LayerMask hitLayers = ~0;         // 기본: 모든 레이어. 적 레이어를 만들면 좁히기

    // 공격 쿨 동안 true. 이동은 막지 않고, 다음 근접 공격과 단검 투척(DaggerThrower)만 막음
    public bool IsLocked => Time.time < lockUntil;

    // 휘두르는 순간 (단계 0~2) — 프레임 애니메이션(PlayerAnimation)용
    public event System.Action<int> Attacked;

    // 방금 공격의 쿨(다음 공격까지 시간) — 공격 모션 재생 길이를 여기에 맞춤
    public float LastLockDuration { get; private set; } = 0.25f;

    private PlayerMovement movement;
    private PlayerStats stats;
    private InputAction attackAction;
    private CameraFollow cameraFollow;

    private int lastStage = -1;              // 직전 공격 단계 (0~2), -1 = 없음
    private float lastAttackTime = float.NegativeInfinity;
    private float lockUntil;
    private float bufferedPressTime = float.NegativeInfinity;  // 아직 처리하지 않은 Z 입력 시각

    private readonly List<Collider2D> overlaps = new List<Collider2D>();
    private readonly HashSet<IDamageable> alreadyHit = new HashSet<IDamageable>();

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        stats = GetComponent<PlayerStats>();
        attackAction = InputSystem.actions.FindAction("Player/Attack", throwIfNotFound: true);
    }

    void Start()
    {
        if (Camera.main != null) cameraFollow = Camera.main.GetComponent<CameraFollow>();
    }

    void Update()
    {
        // 누른 시각을 저장해두고, 쿨이 끝났을 때 최근 inputBuffer초 안의 입력이면 발동 (선입력)
        if (attackAction.WasPressedThisFrame()) bufferedPressTime = Time.time;

        if (IsLocked) return;
        if (Time.time - bufferedPressTime > inputBuffer) return;  // 처리할 입력 없음 (또는 너무 일찍 누름)

        bufferedPressTime = float.NegativeInfinity;               // 입력은 한 번만 소모

        // 직전 공격 시작 후 콤보 창 안이면 다음 단계, 아니면 1타. 3타 다음은 1타
        bool continues = Time.time - lastAttackTime <= comboWindow && lastStage < 2;
        int stage = continues ? lastStage + 1 : 0;

        LastLockDuration = baseCooldown * CooldownMul[stage] / stats.attackSpeedMul;
        PerformAttack(stage);

        lastStage = stage;
        lastAttackTime = Time.time;
        lockUntil = Time.time + LastLockDuration;
    }

    void PerformAttack(int stage)
    {
        Vector2 center = transform.position;
        int facing = movement.Facing;
        float radius = CurrentRange(stage);

        SlashEffect.Spawn(transform, radius, facing, stage);
        SoundManager.Play(SoundId.Swing);
        Attacked?.Invoke(stage);

        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = true;
        filter.SetLayerMask(hitLayers);

        overlaps.Clear();
        alreadyHit.Clear();
        Physics2D.OverlapCircle(center, radius, filter, overlaps);
        bool finisher = stage == 2;

        foreach (Collider2D col in overlaps)
        {
            if (col.transform.IsChildOf(transform)) continue;  // 자기 자신 제외

            // 전방 반원 판정: 대상 몸의 앞쪽 끝이 플레이어 중심보다 바라보는 쪽에 있어야 함.
            // ClosestPoint는 플레이어 중심이 적 몸 안에 들어가면(딱 붙으면) 뒤쪽 가장자리를 돌려줄 수 있어 쓰지 않음
            Bounds bounds = col.bounds;
            float frontEdge = facing > 0 ? bounds.max.x : bounds.min.x;
            if ((frontEdge - center.x) * facing < 0f) continue;

            // 원거리 적의 마법구는 검격에 닿으면 파괴 (피해 대상·3타 흔들림 집계에는 넣지 않음)
            EnemyProjectile projectile = col.GetComponent<EnemyProjectile>();
            if (projectile != null)
            {
                projectile.Shatter();
                continue;
            }

            IDamageable target = col.GetComponentInParent<IDamageable>();
            if (target == null || !alreadyHit.Add(target)) continue;  // 콜라이더가 여러 개여도 한 번만

            target.TakeHit(new HitInfo
            {
                damage = (baseDamage + Random.Range(0, 8)) * DamageMul[stage] * stats.meleeDamageMul,
                direction = new Vector2(facing, 0f),
                knockback = finisher ? finisherKnockback : 0f,
                cancelAttack = finisher,
                guardBreak = finisher,
                source = gameObject,
            });
        }

        // 3타가 하나라도 맞았으면 화면 흔들림 (여러 마리를 맞혀도 한 번)
        if (finisher && alreadyHit.Count > 0 && cameraFollow != null) cameraFollow.Shake();
    }

    float CurrentRange(int stage) => baseRange * RangeMul[stage] * stats.attackRangeMul;

    // Scene 뷰에서 Player를 선택하면 1타 사거리 반원을 표시 (노란색)
    void OnDrawGizmosSelected()
    {
        if (movement == null) movement = GetComponent<PlayerMovement>();
        if (stats == null) stats = GetComponent<PlayerStats>();
        if (movement == null || stats == null) return;

        float radius = CurrentRange(0);
        int facing = Application.isPlaying ? movement.Facing : 1;  // 루트는 반전하지 않음 (그림만 반전)
        Gizmos.color = Color.yellow;

        const int segments = 24;
        Vector3 center = transform.position;
        Vector3 prev = center + new Vector3(0f, radius, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = Mathf.Lerp(90f, -90f, i / (float)segments) * Mathf.Deg2Rad;
            Vector3 point = center + new Vector3(Mathf.Cos(angle) * radius * facing, Mathf.Sin(angle) * radius, 0f);
            Gizmos.DrawLine(prev, point);
            prev = point;
        }
        Gizmos.DrawLine(center + Vector3.up * radius, center + Vector3.down * radius);
    }
}
