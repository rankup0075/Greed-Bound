using System.Collections.Generic;
using UnityEngine;

// 날아가는 투척 단검 (Spec 5장 투척 단검). 프리팹에 붙이고 DaggerThrower가 생성함.
// 지형은 통과하고, IDamageable에 닿으면 피해. 관통이 아니면 첫 대상에게 맞고 사라짐.
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class Dagger : MonoBehaviour
{
    [Header("Spec 5장")]
    public float speed = 14.67f;      // 11 px/frame × 60 ÷ 45
    public float maxDistance = 20f;   // 임시값 — 맵 경계 구현 시 재검토

    private Rigidbody2D rb;
    private float damage;
    private bool pierce;
    private GameObject owner;
    private Vector2 startPosition;
    private readonly HashSet<IDamageable> alreadyHit = new HashSet<IDamageable>();

    void Reset()
    {
        Configure();
    }

    void Awake()
    {
        Configure();
    }

    void Configure()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;  // 중력·충돌 반응 없이 직선 비행
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    public void Launch(GameObject owner, int facing, float damage, bool pierce)
    {
        this.owner = owner;
        this.damage = damage;
        this.pierce = pierce;
        startPosition = transform.position;  // 막 생성된 직후라 rb.position보다 transform이 확실함
        rb.linearVelocity = new Vector2(facing * speed, 0f);

        // 스프라이트가 오른쪽을 향하도록 만들어 두면 왼쪽으로 던질 때 뒤집힘
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * facing;
        transform.localScale = scale;
    }

    void FixedUpdate()
    {
        bool outOfMap = Mathf.Abs(rb.position.x) > ArenaLayout.MapHalfWidth + 1f;
        if (outOfMap || Vector2.Distance(startPosition, rb.position) >= maxDistance) Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (owner != null && other.transform.IsChildOf(owner.transform)) return;  // 던진 사람 제외

        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target == null || !alreadyHit.Add(target)) return;  // 지형 등은 통과, 같은 대상은 한 번만

        target.TakeHit(new HitInfo
        {
            damage = damage,
            direction = new Vector2(Mathf.Sign(rb.linearVelocity.x), 0f),
            knockback = 0f,
            cancelAttack = false,
            source = owner,
        });

        if (!pierce) Destroy(gameObject);
    }
}
