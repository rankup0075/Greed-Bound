using UnityEngine;

// 플레이어 프레임 애니메이션 선택 (Spec 8장 "플레이어 애니메이션 상태").
// Animator(Visual 자식)의 전환선은 쓰지 않고, 이 스크립트가 우선순위대로 상태를 골라 Play.
// Controller에 없는 상태는 건너뛰므로 클립이 하나씩 추가되는 동안에도 동작함.
// 상태 이름 = 클립 이름에서 "player_"를 뺀 것 (메뉴 Greed Bound > 플레이어 애니메이터 생성).
[RequireComponent(typeof(PlayerMovement))]
public class PlayerAnimation : MonoBehaviour
{
    public const string Idle = "idle", Run = "run", Jump = "jump", Fall = "fall", Dash = "dash",
        Hurt = "hurt", Death = "death", Throw = "throw", Potion = "potion",
        Whirlwind = "whirlwind", Slam = "slam", Wave = "sword_wave";
    static readonly string[] AttackStates = { "attack1", "attack2", "attack3" };

    [Tooltip("달리기 재생 속도를 실제 이동 속도에 맞춤 (질주 유물 등으로 빨라지면 발도 빨라짐)")]
    public bool scaleRunWithSpeed = true;

    [Header("한 번 재생 동작 길이 (초) — 판정·쿨과 무관한 그림 길이")]
    public float hurtTime = 0.2f;
    public float potionTime = 0.5f;
    public float whirlwindTime = 0.3f;
    public float slamTime = 0.35f;
    public float swordWaveTime = 0.3f;

    [Header("공중 지면 강타")]
    [Tooltip("낙하 중 멈춰 둘 프레임 위치 (클립 전체 0~1). 칼끝을 아래로 든 자세")]
    [Range(0f, 1f)] public float slamFallPose = 0.2f;
    [Tooltip("착지 순간 이어서 재생할 시작 위치 (0~1). 웅크리며 꽂는 자세부터")]
    [Range(0f, 1f)] public float slamImpactFrom = 0.4f;

    public Animator Animator { get; private set; }

    private PlayerMovement movement;
    private PlayerHealth health;
    private PlayerAttack attack;
    private DaggerThrower dagger;
    private PlayerPotion potion;
    private PlayerSkill skill;
    private Rigidbody2D rb;

    private string current;
    private string oneShot;              // 공격·피격처럼 끝날 때까지 우선하는 상태
    private float oneShotUntil;
    private float oneShotSpeed = 1f;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        health = GetComponent<PlayerHealth>();
        attack = GetComponent<PlayerAttack>();
        rb = GetComponent<Rigidbody2D>();
        CharacterVisual visual = CharacterVisual.Ensure(gameObject);
        Animator = visual.Renderer != null ? visual.Renderer.GetComponent<Animator>() : null;
    }

    // 물약·스킬은 PlayerHealth.Awake가 나중에 붙일 수 있으므로 Start에서 한 번 더 찾아 연결
    void Start() => Bind();

    void OnEnable()
    {
        if (attack != null) attack.Attacked += OnAttacked;
        if (health != null) health.Damaged += OnDamaged;
        Bind();
    }

    void OnDisable()
    {
        if (attack != null) attack.Attacked -= OnAttacked;
        if (health != null) health.Damaged -= OnDamaged;
        if (dagger != null) dagger.Thrown -= OnThrown;
        if (potion != null) potion.Drank -= OnDrank;
        if (skill != null)
        {
            skill.Used -= OnSkillUsed;
            skill.SlamImpact -= OnSlamImpact;
        }
        dagger = null;
        potion = null;
        skill = null;
    }

    // 아직 연결 안 된 것만 찾아 연결 (여러 번 불러도 중복 구독 없음)
    void Bind()
    {
        if (dagger == null && (dagger = GetComponent<DaggerThrower>()) != null) dagger.Thrown += OnThrown;
        if (potion == null && (potion = GetComponent<PlayerPotion>()) != null) potion.Drank += OnDrank;
        if (skill == null && (skill = GetComponent<PlayerSkill>()) != null)
        {
            skill.Used += OnSkillUsed;
            skill.SlamImpact += OnSlamImpact;
        }
    }

    public bool HasState(string state)
    {
        return Animator != null && Animator.runtimeAnimatorController != null && Animator.HasState(0, Animator.StringToHash(state));
    }

    void OnAttacked(int stage)
    {
        // 없는 단계는 있는 공격 클립으로 대신 (2타 없으면 1타)
        string state = AttackStates[Mathf.Clamp(stage, 0, 2)];
        if (!HasState(state)) state = HasState(AttackStates[0]) ? AttackStates[0] : null;
        // 모션 길이 = 이번 공격의 쿨 (연타해도 끊기지 않고, 공격 속도 유물에 맞춰 빨라짐)
        if (state != null) StartOneShot(state, attack.LastLockDuration);
    }

    void OnDamaged()
    {
        if (HasState(Hurt)) StartOneShot(Hurt, hurtTime);
    }

    // 투척 모션 길이 = 투척 쿨 (다음 단검을 던질 수 있는 순간 끝남)
    void OnThrown()
    {
        if (HasState(Throw)) StartOneShot(Throw, dagger.cooldown);
    }

    void OnDrank()
    {
        if (HasState(Potion)) StartOneShot(Potion, potionTime);
    }

    void OnSkillUsed(SkillId id)
    {
        switch (id)
        {
            case SkillId.Whirlwind:
                if (HasState(Whirlwind)) StartOneShot(Whirlwind, whirlwindTime);
                break;
            case SkillId.SwordWave:
                if (HasState(Wave)) StartOneShot(Wave, swordWaveTime);
                break;
            case SkillId.GroundSlam:
                // 지상: 강타가 이미 터졌으므로 처음부터 끝까지. 공중: 낙하 중엔 LateUpdate가 자세 고정, 착지 때 OnSlamImpact
                if (!movement.IsSlamFalling && HasState(Slam)) StartOneShot(Slam, slamTime);
                break;
        }
    }

    // 공중 강타가 착지해 터진 순간 — 웅크림부터 이어서 재생 (지상 강타는 OnSkillUsed가 덮어씀)
    void OnSlamImpact()
    {
        if (current == Slam && HasState(Slam))
            StartOneShot(Slam, slamTime * (1f - slamImpactFrom), slamImpactFrom);
    }

    // 클립 저장 길이와 상관없이 duration초 동안 from(0~1)부터 끝까지 한 번 재생.
    // 끝에서 반복 클립이 첫 프레임으로 돌아가 보이지 않게 조금(3%) 일찍 끝나도록 속도를 맞춤
    void StartOneShot(string state, float duration, float from = 0f)
    {
        duration = Mathf.Max(0.05f, duration);
        oneShot = state;
        oneShotUntil = Time.time + duration;
        oneShotSpeed = ClipLength(state) * (1f - from) / (duration * 0.97f);
        PlayState(state, restart: true, from);
        Animator.speed = oneShotSpeed;
    }

    // 사망은 PlayerHealth가 조작 스크립트를 끄므로 Update 대신 LateUpdate에서도 확인
    void LateUpdate()
    {
        if (Animator == null || Animator.runtimeAnimatorController == null) return;

        if (health != null && health.IsDead)
        {
            if (HasState(Death)) PlayState(Death, restart: false);
            Animator.speed = 1f;
            return;
        }

        // 공중 지면 강타 낙하 중: 칼끝을 아래로 든 프레임에 멈춤
        if (movement.IsSlamFalling && HasState(Slam))
        {
            oneShot = null;
            PlayState(Slam, restart: false, slamFallPose);
            Animator.speed = 0f;
            return;
        }

        if (oneShot != null && Time.time < oneShotUntil)
        {
            Animator.speed = oneShotSpeed;
            return;
        }
        oneShot = null;

        Vector2 velocity = rb.linearVelocity;
        string next = Idle;
        float speed = 1f;
        // 조작이 잠긴 동안(카드 선택·결과 화면)은 제자리에 서 있음.
        // PlayerMovement가 꺼져 있으면 접지 판정이 갱신되지 않아 공중으로 남고,
        // 점프 클립이 없어 아래 공중 대체가 달리기로 나오던 문제도 여기서 막힘
        if (!movement.enabled) next = Idle;
        else if (movement.IsDashing && HasState(Dash)) next = Dash;
        else if (!movement.IsGrounded)
        {
            if (velocity.y < 0f && HasState(Fall)) next = Fall;
            else if (HasState(Jump)) next = Jump;
            else next = HasState(Run) ? Run : Idle;  // 점프 클립이 없으면 달리기 자세 유지
        }
        else if (Mathf.Abs(velocity.x) > 0.5f && HasState(Run))
        {
            next = Run;
            if (scaleRunWithSpeed && movement.moveSpeed > 0f)
                speed = Mathf.Clamp(Mathf.Abs(velocity.x) / movement.moveSpeed, 0.6f, 1.6f);
        }

        PlayState(next, restart: false);
        Animator.speed = speed;
    }

    void PlayState(string state, bool restart, float from = 0f)
    {
        if (!HasState(state)) return;
        if (state == current && !restart) return;
        current = state;
        Animator.Play(state, 0, from);
    }

    float ClipLength(string state)
    {
        if (Animator.runtimeAnimatorController == null) return 0.25f;
        foreach (AnimationClip clip in Animator.runtimeAnimatorController.animationClips)
            if (clip.name == "player_" + state) return clip.length;
        return 0.25f;
    }
}
