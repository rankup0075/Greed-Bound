using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// A — 플레이어 스킬 (Spec 5장 "스킬 — Unity 구현 규칙"). 로비에서 고른 스킬 1개를 런 내내 사용.
// 선딜·후딜 없음, 이동·근접 공격과 겹쳐 쓸 수 있음. 봉인된 라운드에는 사용 불가.
// 기존 Player에도 PlayerHealth가 자동으로 붙여 줌.
[RequireComponent(typeof(PlayerMovement), typeof(PlayerStats))]
public class PlayerSkill : MonoBehaviour
{
    public SkillId Current { get; private set; }
    public float CooldownRemaining => Mathf.Max(0f, readyTime - Time.time);

    // 스킬 애니메이션(PlayerAnimation)용: 사용한 순간 / 지면 강타가 실제로 터진 순간(지상 즉시 또는 공중 낙하 후 착지)
    public event System.Action<SkillId> Used;
    public event System.Action SlamImpact;

    private PlayerMovement movement;
    private PlayerStats stats;
    private PlayerAttack attack;
    private InputAction skillAction;
    private CameraFollow cameraFollow;
    private float readyTime;
    private float slamFallStartTime;

    private readonly List<Collider2D> overlaps = new List<Collider2D>();
    private readonly HashSet<IDamageable> alreadyHit = new HashSet<IDamageable>();

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        stats = GetComponent<PlayerStats>();
        attack = GetComponent<PlayerAttack>();
        skillAction = InputSystem.actions.FindAction("Player/Skill", throwIfNotFound: true);
        Current = MetaProgress.SelectedSkill;
    }

    void Start()
    {
        if (Camera.main != null) cameraFollow = Camera.main.GetComponent<CameraFollow>();
    }

    // 라운드 시작 시 즉시 사용 가능하게 (RoundManager)
    public void ResetCooldown() => readyTime = 0f;

    void Update()
    {
        if (!skillAction.WasPressedThisFrame()) return;
        if (Time.time < readyTime) return;

        if (RunState.Instance != null && RunState.Instance.IsSealed)
        {
            if (RoundManager.Instance != null) RoundManager.Instance.ShowMessage("봉인: 이번 라운드는 스킬을 쓸 수 없습니다");
            SoundManager.Play(SoundId.Deny);
            return;
        }

        bool used = Current switch
        {
            SkillId.GroundSlam => GroundSlam(),
            SkillId.SwordWave => SwordWaveSkill(),
            _ => Whirlwind(),
        };
        if (!used) return;
        readyTime = Time.time + SkillCatalog.Get(Current).cooldown;
        Used?.Invoke(Current);
        SoundManager.Play(SoundId.Skill);
    }

    // 근접 기본 피해(16 + 0~7) × 스킬 배율 × 근접 공격력 배율
    float RollDamage(float skillMul)
    {
        float baseDamage = attack != null ? attack.baseDamage : 16f;
        return (baseDamage + Random.Range(0, 8)) * skillMul * stats.meleeDamageMul;
    }

    bool Whirlwind()
    {
        Vector2 center = transform.position;
        float radius = SkillCatalog.WhirlwindRadius;
        WhirlwindEffect.Spawn(transform, radius, movement.Facing);

        ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
        overlaps.Clear();
        Physics2D.OverlapCircle(center, radius, filter, overlaps);
        HitOverlaps(center, SkillCatalog.WhirlwindDamageMul, SkillCatalog.WhirlwindKnockback, cancelAttack: false);
        return true;
    }

    // 지상이면 즉시 강타, 공중이면 그 자리에서 수직 낙하 → 착지 순간 강타 (쿨은 누른 순간부터)
    bool GroundSlam()
    {
        if (movement.IsSlamFalling) return false;
        if (movement.IsGrounded)
        {
            SlamNow();
            return true;
        }

        slamFallStartTime = Time.time;
        movement.BeginSlamFall(SkillCatalog.SlamFallSpeed, SlamNow);
        return true;
    }

    void LateUpdate()
    {
        // 발판 없는 곳 등에서 너무 오래 떨어지면 취소
        if (movement.IsSlamFalling && Time.time - slamFallStartTime > SkillCatalog.SlamFallMaxTime) movement.CancelSlamFall();
    }

    void SlamNow()
    {
        Collider2D body = GetComponent<Collider2D>();
        float feetY = body != null ? body.bounds.min.y : transform.position.y - 0.5f;
        Vector2 feet = new Vector2(transform.position.x, feetY);
        ShockwaveEffect.Spawn(feet, SkillCatalog.SlamRange, SkillCatalog.SlamHeight);
        SlamImpact?.Invoke();

        ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
        overlaps.Clear();
        Vector2 boxCenter = feet + new Vector2(0f, SkillCatalog.SlamHeight * 0.5f);
        Physics2D.OverlapBox(boxCenter, new Vector2(SkillCatalog.SlamRange * 2f, SkillCatalog.SlamHeight), 0f, filter, overlaps);
        bool hitAny = HitOverlaps(feet, SkillCatalog.SlamDamageMul, SkillCatalog.SlamKnockback, cancelAttack: true);

        if (hitAny && cameraFollow != null) cameraFollow.Shake();
    }

    bool SwordWaveSkill()
    {
        int facing = movement.Facing;
        Vector2 spawn = (Vector2)transform.position + new Vector2(facing * 0.6f, 0f);
        SwordWave.Spawn(gameObject, spawn, facing, RollDamage(SkillCatalog.WaveDamageMul));
        return true;
    }

    // overlaps에 담긴 대상에게 피해. 밀어내는 방향은 center 기준 바깥쪽. 하나라도 맞으면 true
    bool HitOverlaps(Vector2 center, float damageMul, float knockback, bool cancelAttack)
    {
        alreadyHit.Clear();
        foreach (Collider2D col in overlaps)
        {
            if (col.transform.IsChildOf(transform)) continue;
            if (col.GetComponent<EnemyProjectile>() != null) continue;  // 마법구는 검기만 베어냄

            IDamageable target = col.GetComponentInParent<IDamageable>();
            if (target == null || !alreadyHit.Add(target)) continue;

            float side = col.bounds.center.x - center.x;
            float direction = Mathf.Abs(side) > 0.01f ? Mathf.Sign(side) : movement.Facing;
            target.TakeHit(new HitInfo
            {
                damage = RollDamage(damageMul),
                direction = new Vector2(direction, 0f),
                knockback = knockback,
                cancelAttack = cancelAttack,
                guardBreak = true,
                source = gameObject,
            });
        }
        return alreadyHit.Count > 0;
    }
}
