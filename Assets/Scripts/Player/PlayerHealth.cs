using UnityEngine;

// 플레이어 체력·피격·무적 시간 (Spec 5장 플레이어 피격).
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerHealth : MonoBehaviour, IDamageable
{
    const float BlinkInterval = 0.12f;  // 무적 중 깜빡임 주기

    [Header("Spec 5장")]
    public float baseMaxHealth = 100f;
    public float invincibleTime = 46f / 60f;  // 46프레임 = 0.767초

    // TODO: 강철 심장(+30) 등 고정 증가는 유물·상점 구현 시 추가
    public float MaxHealth => baseMaxHealth * (stats != null ? stats.maxHealthMul : 1f);
    public float CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }
    public bool IsInvincible => Time.time < invincibleUntil;

    private Rigidbody2D rb;
    private PlayerStats stats;
    private SpriteRenderer spriteRenderer;
    private CameraFollow cameraFollow;
    private float invincibleUntil;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        stats = GetComponent<PlayerStats>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        CurrentHealth = MaxHealth;
    }

    // 최대 체력이 줄었을 때(피의 계약 등) 현재 체력이 넘치지 않게
    public void ClampHealthToMax()
    {
        CurrentHealth = Mathf.Min(CurrentHealth, MaxHealth);
    }

    void Start()
    {
        if (Camera.main != null) cameraFollow = Camera.main.GetComponent<CameraFollow>();
    }

    public float TakeHit(HitInfo hit)
    {
        if (IsDead || IsInvincible) return 0f;

        float before = CurrentHealth;
        CurrentHealth = Mathf.Max(0f, CurrentHealth - hit.damage);
        invincibleUntil = Time.time + invincibleTime;
        if (cameraFollow != null) cameraFollow.Shake();

        if (CurrentHealth <= 0f) Die();
        return before - CurrentHealth;
    }

    void Update()
    {
        if (spriteRenderer == null) return;
        // 무적 중에는 깜빡임. 사망 시에는 계속 보이게
        spriteRenderer.enabled = IsDead || !IsInvincible || Mathf.Repeat(Time.time, BlinkInterval) < BlinkInterval * 0.5f;
    }

    void Die()
    {
        IsDead = true;
        Debug.Log("플레이어 사망");

        // 조작 중지. 런 종료 처리는 라운드 시스템에서
        foreach (MonoBehaviour control in new MonoBehaviour[]
                 { GetComponent<PlayerMovement>(), GetComponent<PlayerAttack>(), GetComponent<DaggerThrower>() })
        {
            if (control != null) control.enabled = false;
        }
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        if (spriteRenderer != null) spriteRenderer.color = Color.gray;
    }
}
