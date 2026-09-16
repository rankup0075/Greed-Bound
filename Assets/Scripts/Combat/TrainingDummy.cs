using UnityEngine;

// 공격 테스트용 허수아비. 적 AI를 만들기 전까지 콤보·단검·밀어내기를 확인하는 용도.
// 맞으면 붉게 깜빡이고 Console에 피해를 출력. 체력은 무한.
[RequireComponent(typeof(BoxCollider2D))]
public class TrainingDummy : MonoBehaviour, IDamageable
{
    const float FlashTime = 0.12f;
    const float KnockbackTime = 0.1f;

    private SpriteRenderer spriteRenderer;
    private Color baseColor;
    private float flashTimer;

    private Vector3 knockbackFrom;
    private Vector3 knockbackTo;
    private float knockbackTimer = -1f;

    void Reset()
    {
        // 플레이어가 부딪히지 않고 지나갈 수 있게 트리거로 (적과 플레이어의 몸 충돌 여부는 적 AI 단계에서 결정)
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) baseColor = spriteRenderer.color;
    }

    public float TakeHit(HitInfo hit)
    {
        string extra = hit.cancelAttack ? " (공격 캔슬)" : "";
        Debug.Log($"{name} 피격: {hit.damage:F1} 피해, 밀어내기 {hit.knockback:F2}u{extra}");

        flashTimer = FlashTime;

        if (hit.knockback > 0f)
        {
            knockbackFrom = transform.position;
            knockbackTo = knockbackFrom + (Vector3)(hit.direction.normalized * hit.knockback);
            knockbackTimer = 0f;
        }
        return hit.damage;
    }

    void Update()
    {
        if (spriteRenderer != null)
        {
            flashTimer -= Time.deltaTime;
            spriteRenderer.color = flashTimer > 0f ? Color.red : baseColor;
        }

        if (knockbackTimer >= 0f)
        {
            knockbackTimer += Time.deltaTime;
            float t = Mathf.Clamp01(knockbackTimer / KnockbackTime);
            transform.position = Vector3.Lerp(knockbackFrom, knockbackTo, 1f - (1f - t) * (1f - t));
            if (t >= 1f) knockbackTimer = -1f;
        }
    }
}
