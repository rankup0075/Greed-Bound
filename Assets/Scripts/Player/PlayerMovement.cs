using UnityEngine;
using UnityEngine.InputSystem;

// Player 오브젝트에 붙이는 스크립트. Rigidbody2D가 반드시 같이 있어야 함.
[RequireComponent(typeof(Rigidbody2D), typeof(PlayerStats))]
public class PlayerMovement : MonoBehaviour
{
    [Header("이동 설정")]
    public float moveSpeed = 7.47f;    // Spec 5.6 px/frame × 60 ÷ 45 (1 unit = 45px)

    [Header("점프 설정")]
    public float jumpSpeed = 19.44f;   // 최고점 3.61u(163px)에 맞춘 보정값 (환산값 18.93은 3.42u)
    public float gravityScale = 5.06f; // Spec 0.62 px/frame² → 49.6 u/s² ÷ 9.81

    [Header("대쉬 설정 (Left Shift)")]
    public float dashDistance = 3f;    // 한 번에 이동하는 거리
    public float dashDuration = 0.12f; // 그 거리를 가는 데 걸리는 시간
    public float dashCooldown = 3f;    // 대쉬 시작 시점부터
    public float dashJumpWindow = 0.1f; // 점프 후 이 시간 안에 대쉬하면 점프 궤적 유지 (대쉬 점프)
    public float afterimageInterval = 0.03f;

    [Header("발판 내려가기 (↓ + C)")]
    public float dropStartSpeed = 2f;  // 뚫고 내려가기 시작하는 낙하 속도
    public float dropMaxTime = 0.5f;   // 이 시간이 지나면 발판 충돌 복구

    [Header("디버그")]
    public bool logJumpHeight = false; // 착지할 때 점프 최고점을 Console에 출력

    // 다른 스크립트(공격·단검·HUD)가 읽는 상태
    public bool IsGrounded => isGrounded;
    public int Facing => facingRight ? 1 : -1;  // 오른쪽 = 1, 왼쪽 = -1
    public bool IsDashing => dashStepsRemaining > 0;
    public float DashCooldownRemaining => Mathf.Max(0f, dashReadyTime - Time.time);
    public bool IsSlamFalling => slamFallSpeed > 0f;

    private Rigidbody2D rb;
    private PlayerStats stats;
    private CharacterVisual visual;
    private SpriteRenderer spriteRenderer;
    private InputAction moveAction;    // InputSystem_Actions의 Player/Move (←→)
    private InputAction jumpAction;    // InputSystem_Actions의 Player/Jump (C)
    private InputAction dashAction;    // InputSystem_Actions의 Player/Dash (Left Shift)
    private InputAction downAction;    // InputSystem_Actions의 Player/Down (↓) — ↓ + C로 발판 내려가기

    // 발판 내려가기
    private Collider2D collider2d;
    private Collider2D droppingThrough;
    private float dropUntil;
    private readonly System.Collections.Generic.List<ContactPoint2D> contacts = new System.Collections.Generic.List<ContactPoint2D>();
    private float moveInput;           // -1(왼쪽) ~ 1(오른쪽) 사이 값
    private bool jumpRequested;        // Update에서 누른 점프를 FixedUpdate까지 전달하는 표시
    private bool dashRequested;
    private bool facingRight = true;   // 캐릭터가 현재 오른쪽을 보고 있는지

    // 바닥 판정: 아래쪽(법선 각도 45~135도)에서 닿은 충돌만 "바닥"으로 인정.
    // 벽이나 천장에 닿은 건 제외되고, one-way 발판을 아래에서 통과하는 중에는 충돌 자체가 없어서 제외됨
    private ContactFilter2D groundFilter;
    private bool isGrounded;

    // 대쉬 상태. 시간 대신 물리 스텝 수로 세서 이동 거리가 항상 dashDistance와 같게 함
    private int dashStepsRemaining;
    private int dashDirection;
    private float dashSpeed;
    private float dashReadyTime;
    private bool dashKeepsVertical;    // true = 대쉬 점프(중력·세로 속도 유지), false = 공중 수평 대쉬
    private float lastJumpTime = float.NegativeInfinity;
    private float afterimageTimer;

    // 공중 지면 강타: 가로 0, 일정 속도로 수직 낙하. 착지하면 콜백 (PlayerSkill)
    private float slamFallSpeed;
    private System.Action onSlamLanded;

    // 점프 최고점 측정용 (C로 점프했을 때만 측정. 걸어서 떨어진 건 제외)
    private bool measuringJump;
    private float takeoffY;
    private float peakY;

    void Awake()
    {
        // 시작할 때 딱 한 번, Player에 붙어있는 컴포넌트들을 찾아서 저장해둠
        rb = GetComponent<Rigidbody2D>();
        // RequireComponent는 새로 붙일 때만 자동 추가되므로, 이미 씬에 있던 Player를 위해 없으면 직접 추가
        stats = GetComponent<PlayerStats>();
        if (stats == null) stats = gameObject.AddComponent<PlayerStats>();
        visual = CharacterVisual.Ensure(gameObject);  // 그림은 자식 Visual, 루트는 판정만
        spriteRenderer = visual.Renderer;

        rb.gravityScale = gravityScale;
        // 낙하 속도가 빨라 얇은 발판을 뚫고 지나가지 않도록 연속 충돌 검사
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        // 물리(50Hz)와 화면 주사율 차이로 카메라 추적 시 떨려 보이지 않게 보간
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        // 공중에서 벽을 향해 이동키를 누르고 있어도 벽에 붙지 않게 마찰 0
        RuntimeMaterials.ApplyNoFriction(gameObject);

        // 프로젝트 전역 입력 에셋(InputSystem_Actions)에서 액션을 찾아둠
        moveAction = InputSystem.actions.FindAction("Player/Move", throwIfNotFound: true);
        jumpAction = InputSystem.actions.FindAction("Player/Jump", throwIfNotFound: true);
        dashAction = InputSystem.actions.FindAction("Player/Dash", throwIfNotFound: true);
        downAction = InputSystem.actions.FindAction("Player/Down", throwIfNotFound: true);
        collider2d = GetComponent<Collider2D>();

        groundFilter.useTriggers = false;
        groundFilter.SetNormalAngle(45f, 135f);
    }

    void OnDisable()
    {
        // 사망 등으로 꺼질 때 대쉬 도중이었어도 중력이 돌아오게
        CancelDash();
        CancelSlamFall();
    }

    // 공중 지면 강타 시작: 대쉬를 끊고 수직 낙하. 착지하는 물리 스텝에 onLanded 호출
    public void BeginSlamFall(float speed, System.Action onLanded)
    {
        CancelDash();
        slamFallSpeed = speed;
        onSlamLanded = onLanded;
        measuringJump = false;
    }

    public void CancelSlamFall()
    {
        slamFallSpeed = 0f;
        onSlamLanded = null;
    }

    void Update()
    {
        // 입력 감지는 Update에서 (매 프레임 키보드 확인)
        moveInput = moveAction.ReadValue<float>(); // ← = -1, → = 1, 안 누르면 0

        // 누른 순간만 기록. 실제 점프·대쉬는 물리 단계인 FixedUpdate에서 처리
        if (jumpAction.WasPressedThisFrame()) jumpRequested = true;
        if (dashAction.WasPressedThisFrame()) dashRequested = true;

        // 방향 바꿀 때 스프라이트 좌우 반전 (나중에 실제 그림 넣으면 자연스럽게 보임). 강타 낙하 중엔 고정
        if (IsSlamFalling) return;
        if (moveInput > 0 && !facingRight) Flip();
        else if (moveInput < 0 && facingRight) Flip();

        if (IsDashing) SpawnAfterimages();
    }

    void FixedUpdate()
    {
        bool wasGrounded = isGrounded;
        isGrounded = rb.IsTouching(groundFilter);
        // 착지음 (떨어지던 중에 바닥에 닿은 순간만)
        if (isGrounded && !wasGrounded && rb.linearVelocity.y <= 0.1f) SoundManager.Play(SoundId.Land);

        // 공중 지면 강타 낙하 중: 좌우 이동·점프·대쉬 입력 무시, 착지하면 강타
        if (IsSlamFalling)
        {
            jumpRequested = false;
            dashRequested = false;
            if (isGrounded)
            {
                System.Action landed = onSlamLanded;
                CancelSlamFall();
                rb.linearVelocity = Vector2.zero;
                landed?.Invoke();
                return;
            }
            rb.linearVelocity = new Vector2(0f, -slamFallSpeed);
            return;
        }

        // 실제 물리 이동은 FixedUpdate에서 (물리 연산은 여기서 하는 게 정석)
        Vector2 velocity = new Vector2(moveInput * moveSpeed * stats.moveSpeedMul, rb.linearVelocity.y);

        // 바닥에 서 있을 때만 점프. 공중에서 누른 입력은 버림 (이중 점프 없음). 대쉬 중에도 점프는 즉시 반영
        // ↓ + C: 밟고 있는 one-way 발판을 뚫고 내려감 (발판 위가 아니면 평소처럼 점프)
        if (jumpRequested && isGrounded && downAction.IsPressed() && TryDropThrough())
        {
            jumpRequested = false;
            isGrounded = false;
            velocity.y = -dropStartSpeed;
        }
        UpdateDropThrough();

        bool jumpedNow = jumpRequested && isGrounded;
        jumpRequested = false;
        if (jumpedNow)
        {
            velocity.y = jumpSpeed * stats.jumpMul;
            isGrounded = false;
            lastJumpTime = Time.time;
            SoundManager.Play(SoundId.Jump);

            measuringJump = true;
            takeoffY = rb.position.y;
            peakY = takeoffY;
        }

        // 대쉬: 쿨이 끝났으면 누른 즉시 시작 (선딜 없음). 방향은 누른 순간 바라보는 쪽
        if (dashRequested && Time.time >= dashReadyTime) StartDash(jumpedNow);
        dashRequested = false;

        if (IsDashing)
        {
            // 대쉬 도중 점프하면 그때부터 대쉬 점프 (대쉬는 끊지 않음)
            if (jumpedNow) SetDashKeepsVertical(true);

            // 대쉬는 가로 속도만 덮어씀. 공중 대쉬만 세로를 0으로 고정해 수평 이동
            velocity.x = dashDirection * dashSpeed;
            if (!dashKeepsVertical) velocity.y = 0f;

            dashStepsRemaining--;
            // 마지막 스텝이면 중력 복구 — 다음 스텝부터 평소 이동 (후딜 없음)
            if (dashStepsRemaining == 0) rb.gravityScale = gravityScale;
        }

        rb.linearVelocity = velocity;

        if (logJumpHeight) TrackJumpHeight();
    }

    void StartDash(bool jumpedNow)
    {
        dashStepsRemaining = Mathf.Max(1, Mathf.RoundToInt(dashDuration / Time.fixedDeltaTime));
        dashSpeed = dashDistance / (dashStepsRemaining * Time.fixedDeltaTime);
        dashDirection = Facing;
        dashReadyTime = Time.time + dashCooldown;
        afterimageTimer = 0f;
        SoundManager.Play(SoundId.Dash);

        // 지상 대쉬 / 점프 직후 대쉬 → 대쉬 점프(세로 유지). 그 외 공중 대쉬 → 수평 고정
        bool justJumped = Time.time - lastJumpTime <= dashJumpWindow;
        SetDashKeepsVertical(isGrounded || jumpedNow || justJumped);
    }

    void SetDashKeepsVertical(bool keepsVertical)
    {
        dashKeepsVertical = keepsVertical;
        rb.gravityScale = keepsVertical ? gravityScale : 0f;
        if (!keepsVertical) measuringJump = false;
    }

    // 발 아래 닿아 있는 one-way 발판과만 충돌을 끔. 없으면 false
    bool TryDropThrough()
    {
        if (collider2d == null) return false;
        contacts.Clear();
        rb.GetContacts(groundFilter, contacts);
        foreach (ContactPoint2D contact in contacts)
        {
            Collider2D platform = contact.collider;
            if (platform == null || platform.GetComponent<OneWayPlatform>() == null) continue;

            if (droppingThrough != null) Physics2D.IgnoreCollision(collider2d, droppingThrough, false);
            Physics2D.IgnoreCollision(collider2d, platform, true);
            droppingThrough = platform;
            dropUntil = Time.time + dropMaxTime;
            return true;
        }
        return false;
    }

    // 발판 아래로 빠져나갔거나 시간이 지나면 충돌 복구
    void UpdateDropThrough()
    {
        if (droppingThrough == null) return;
        if (collider2d.bounds.max.y >= droppingThrough.bounds.min.y && Time.time < dropUntil) return;
        Physics2D.IgnoreCollision(collider2d, droppingThrough, false);
        droppingThrough = null;
    }

    // 대쉬를 즉시 끝냄 (라운드 시작 시 위치 초기화, 비활성화). 쿨은 그대로 유지
    public void CancelDash()
    {
        dashStepsRemaining = 0;
        if (rb != null) rb.gravityScale = gravityScale;
    }

    void SpawnAfterimages()
    {
        if (spriteRenderer == null) return;

        afterimageTimer -= Time.deltaTime;
        if (afterimageTimer > 0f) return;
        afterimageTimer = afterimageInterval;
        DashAfterimage.Spawn(spriteRenderer);
    }

    void TrackJumpHeight()
    {
        if (!measuringJump) return;

        float y = rb.position.y;
        peakY = Mathf.Max(peakY, y);

        // 막 뛰어오른 직후(아직 상승 중)엔 바닥 접촉이 남아 있을 수 있으니, 떨어지기 시작한 뒤의 착지만 인정
        if (isGrounded && rb.linearVelocity.y <= 0f)
        {
            measuringJump = false;
            // 같은 높이에 착지했을 때 기준. 발판 위로 올라갔으면 착지 높이 차가 함께 표시됨
            Debug.Log($"점프 최고점: {peakY - takeoffY:F2}u (Spec 목표 3.61u), 착지 높이 차 {y - takeoffY:F2}u");
        }
    }

    void Flip()
    {
        facingRight = !facingRight;
        visual.SetFacing(Facing);  // 그림만 반전. 루트(판정) 스케일은 건드리지 않음
    }
}
