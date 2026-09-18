using UnityEngine;

// 플레이어 체력·피격·무적 시간 (Spec 5장 플레이어 피격).
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerHealth : MonoBehaviour, IDamageable
{
    const float BlinkInterval = 0.12f;  // 무적 중 깜빡임 주기
    const float ThornsRadius = 90f / 45f;  // 보복의 가시 반경 2u

    [Header("Spec 5장")]
    public float baseMaxHealth = 100f;
    public float invincibleTime = 46f / 60f;  // 46프레임 = 0.767초

    // 최대 체력 = (기본 + 강철 심장 등 고정 증가) × 피의 계약 등 배율
    public float MaxHealth => stats != null ? (baseMaxHealth + stats.bonusMaxHealth) * stats.maxHealthMul : baseMaxHealth;
    public float CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }
    public bool IsInvincible => Time.time < invincibleUntil;

    // 실제 피해를 받은 순간 (지속 피해 제외) — 피격 애니메이션(PlayerAnimation)용
    public event System.Action Damaged;

    private Rigidbody2D rb;
    private PlayerStats stats;
    private SpriteRenderer spriteRenderer;
    private CameraFollow cameraFollow;
    private float invincibleUntil;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        stats = GetComponent<PlayerStats>();
        spriteRenderer = CharacterVisual.Ensure(gameObject).Renderer;  // 그림은 자식 Visual
        CurrentHealth = MaxHealth;

        // 물약(1)은 이후에 추가된 기능 — 이미 씬·프리팹에 있던 Player에도 붙도록
        if (GetComponent<PlayerPotion>() == null) gameObject.AddComponent<PlayerPotion>();
        if (GetComponent<PlayerSkill>() == null && GetComponent<PlayerMovement>() != null) gameObject.AddComponent<PlayerSkill>();  // 스킬(A)도 같은 이유
        if (GetComponent<PlayerAnimation>() == null && GetComponent<PlayerMovement>() != null) gameObject.AddComponent<PlayerAnimation>();    // 프레임 애니메이션 선택도 같은 이유
    }

    // 씬 전환 후 이전 체력으로 되돌릴 때 (RunState.RestorePlayer)
    public void SetHealth(float value)
    {
        if (IsDead) return;
        CurrentHealth = Mathf.Clamp(value, 1f, MaxHealth);
    }

    // 즉사 (죽음의 시계)
    public void Kill()
    {
        if (IsDead) return;
        CurrentHealth = 0f;
        Die();
    }

    // 회복 (최대 체력 초과 없음)
    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
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
        if (IsDead) return 0f;
        if (IsInvincible && !hit.damageOverTime) return 0f;

        float before = CurrentHealth;
        float damage = hit.damage * (stats != null ? stats.damageTakenMul : 1f);  // 수호의 문양 (지속 피해 포함)
        CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
        float dealt = before - CurrentHealth;

        if (!hit.damageOverTime)
        {
            invincibleUntil = Time.time + invincibleTime * (stats != null ? stats.invincibleTimeMul : 1f);  // 유령 망토
            if (cameraFollow != null) cameraFollow.Shake();
            if (dealt > 0f)
            {
                Retaliate();
                Damaged?.Invoke();
                SoundManager.Play(SoundId.Hurt);
            }
        }

        if (CurrentHealth <= 0f) Die();
        return dealt;
    }

    // 보복의 가시: 피해를 실제로 받을 때 반경 안 적 전원에게 피해 + 가시 모양 번쩍임
    void Retaliate()
    {
        if (stats == null || stats.thornsDamage <= 0f) return;

        Vector2 center = transform.position;
        ThornsBurst.Spawn(center, ThornsRadius);
        foreach (Enemy enemy in Enemy.Active.ToArray())
        {
            if (enemy.IsDead) continue;
            Vector2 toEnemy = (Vector2)enemy.transform.position - center;
            if (toEnemy.sqrMagnitude > ThornsRadius * ThornsRadius) continue;
            enemy.TakeHit(new HitInfo { damage = stats.thornsDamage, direction = new Vector2(Mathf.Sign(toEnemy.x), 0f), source = gameObject });
        }
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
                 { GetComponent<PlayerMovement>(), GetComponent<PlayerAttack>(), GetComponent<DaggerThrower>(), GetComponent<PlayerPotion>(), GetComponent<PlayerSkill>() })
        {
            if (control != null) control.enabled = false;
        }
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        if (spriteRenderer != null) spriteRenderer.color = Color.gray;
    }
}
