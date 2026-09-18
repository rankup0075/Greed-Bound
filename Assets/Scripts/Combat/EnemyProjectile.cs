using System.Collections.Generic;
using UnityEngine;

// 원거리 적의 마법구 (Spec 5장 원거리 적 AI).
// 직선 비행, 지형 통과, 플레이어에게 닿으면 피해 후 소멸. 근접 공격(검격 반원)에 닿으면 파괴됨.
// 판정 반지름과 보이는 원의 반지름은 같음.
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class EnemyProjectile : MonoBehaviour
{
    const int Segments = 16;
    static readonly Color CoreColor = new Color(1f, 0.85f, 0.9f);
    static readonly Color EdgeColor = new Color(0.85f, 0.25f, 0.9f);

    // 날아가는 중인 마법구 — 라운드가 끝날 때 한꺼번에 정리
    static readonly List<EnemyProjectile> Active = new List<EnemyProjectile>();

    private Rigidbody2D rb;
    private Enemy owner;
    private float damage;
    private float maxDistance;
    private Vector2 startPosition;
    private Mesh mesh;

    public static void Spawn(Enemy owner, Vector2 position, Vector2 velocity, float damage, float radius, float maxDistance)
    {
        GameObject go = new GameObject("EnemyProjectile");
        go.transform.position = new Vector3(position.x, position.y, 0f);

        EnemyProjectile projectile = go.AddComponent<EnemyProjectile>();
        projectile.owner = owner;
        projectile.damage = damage;
        projectile.maxDistance = maxDistance;
        projectile.startPosition = position;
        projectile.Setup(velocity, radius);
    }

    // 라운드 종료·재시작 시 남은 마법구 제거
    public static void DestroyAll()
    {
        foreach (EnemyProjectile projectile in Active.ToArray()) Destroy(projectile.gameObject);
    }

    void OnEnable() => Active.Add(this);
    void OnDisable() => Active.Remove(this);

    void Setup(Vector2 velocity, float radius)
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;  // 중력·충돌 반응 없이 직선
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.linearVelocity = velocity;

        CircleCollider2D circle = GetComponent<CircleCollider2D>();
        circle.isTrigger = true;
        circle.radius = radius;

        // 판정과 같은 반지름의 원: 가운데 밝고 가장자리 보라
        mesh = new Mesh();
        Vector3[] vertices = new Vector3[Segments + 1];
        Color[] colors = new Color[Segments + 1];
        int[] triangles = new int[Segments * 3];
        vertices[0] = Vector3.zero;
        colors[0] = CoreColor;
        for (int i = 0; i < Segments; i++)
        {
            float angle = i / (float)Segments * Mathf.PI * 2f;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            colors[i + 1] = EdgeColor;
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = (i + 1) % Segments + 1;
        }
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().mesh = mesh;
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = RuntimeMaterials.SpriteUnlit;
        meshRenderer.sortingOrder = 175;  // 어둠(150) 위 — 날아오는 마법구는 항상 보이게
    }

    void FixedUpdate()
    {
        bool outOfMap = Mathf.Abs(rb.position.x) > ArenaLayout.MapHalfWidth + 1f;
        if (outOfMap || Vector2.Distance(startPosition, rb.position) >= maxDistance) Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // 플레이어만 맞음. 지형·적·허수아비는 통과
        PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
        if (player == null || player.IsDead) return;

        float dealt = player.TakeHit(new HitInfo
        {
            damage = damage,
            direction = new Vector2(Mathf.Sign(rb.linearVelocity.x), 0f),
            source = owner != null ? owner.gameObject : null,
        });
        if (owner != null) owner.OnProjectileHit(dealt);  // 탐식 흡혈

        Destroy(gameObject);
    }

    // 근접 공격에 맞아 파괴 — 작은 번쩍임을 남기고 사라짐
    public void Shatter()
    {
        CircleTelegraph.Spawn(rb.position, GetComponent<CircleCollider2D>().radius * 1.8f, 0f, EdgeColor);
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }
}
