using System.Collections.Generic;
using UnityEngine;

// 검기 (Spec 5장 "스킬"). 바라보는 방향으로 직선 비행하며 닿는 적을 모두 관통(대상마다 1회), 지형 통과,
// 적 마법구를 베어냄. 보이는 초승달 높이·두께 = 판정 박스. 사거리를 다 날거나 맵 밖으로 나가면 소멸.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SwordWave : MonoBehaviour
{
    const int Segments = 16;
    static readonly Color WaveColor = new Color(0.7f, 0.9f, 1f);

    private GameObject owner;
    private int facing;
    private float damage;
    private float travelled;
    private Mesh mesh;
    private readonly HashSet<IDamageable> alreadyHit = new HashSet<IDamageable>();
    private readonly List<Collider2D> overlaps = new List<Collider2D>();

    public static void Spawn(GameObject owner, Vector2 center, int facing, float damage)
    {
        GameObject go = new GameObject("SwordWave");
        go.transform.position = new Vector3(center.x, center.y, 0f);
        SwordWave wave = go.AddComponent<SwordWave>();
        wave.owner = owner;
        wave.facing = facing;
        wave.damage = damage;
        go.transform.localScale = new Vector3(facing, 1f, 1f);  // 초승달 볼록한 쪽이 날아가는 방향
        wave.HitAlong(center.x, center.x);  // 붙어 있는 적도 발사 순간 맞음
    }

    void Awake()
    {
        mesh = new Mesh();
        BuildMesh();
        GetComponent<MeshFilter>().mesh = mesh;
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = RuntimeMaterials.SpriteUnlit;
        meshRenderer.sortingOrder = 100;
    }

    // 폭 WaveWidth·높이 WaveHeight 안에 들어가는 초승달 (앞쪽이 볼록). 방향은 스케일로 뒤집음
    void BuildMesh()
    {
        float halfHeight = SkillCatalog.WaveHeight * 0.5f;
        float halfWidth = SkillCatalog.WaveWidth * 0.5f;
        Vector3[] vertices = new Vector3[(Segments + 1) * 2];
        Color[] colors = new Color[vertices.Length];
        int[] triangles = new int[Segments * 6];

        for (int i = 0; i <= Segments; i++)
        {
            float t = i / (float)Segments;                  // 0 = 아래 끝, 1 = 위 끝
            float y = Mathf.Lerp(-halfHeight, halfHeight, t);
            float bulge = Mathf.Sin(t * Mathf.PI);          // 가운데가 가장 앞으로
            vertices[i * 2] = new Vector3(-halfWidth + bulge * SkillCatalog.WaveWidth, y, 0f);          // 앞 테두리
            vertices[i * 2 + 1] = new Vector3(-halfWidth + bulge * SkillCatalog.WaveWidth * 0.45f, y, 0f); // 뒤 테두리

            Color front = WaveColor;
            front.a = 0.35f + 0.6f * bulge;
            Color back = WaveColor;
            back.a = 0.1f;
            colors[i * 2] = front;
            colors[i * 2 + 1] = back;
        }
        for (int i = 0; i < Segments; i++)
        {
            int a = i * 2, b = i * 2 + 1, c = a + 2, d = b + 2, k = i * 6;
            triangles[k] = a; triangles[k + 1] = c; triangles[k + 2] = b;
            triangles[k + 3] = b; triangles[k + 4] = c; triangles[k + 5] = d;
        }

        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }

    void Update()
    {
        float step = SkillCatalog.WaveSpeed * Time.deltaTime;
        float fromX = transform.position.x;
        float toX = fromX + facing * step;
        transform.position = new Vector3(toX, transform.position.y, 0f);
        travelled += step;

        HitAlong(fromX, toX);  // 프레임 사이에 지나친 구간까지 판정 (빠른 비행에도 누락 없음)

        bool outOfMap = Mathf.Abs(toX) > ArenaLayout.MapHalfWidth + 1f;
        if (outOfMap || travelled >= SkillCatalog.WaveRange) Destroy(gameObject);
    }

    // fromX ~ toX 구간을 판정 폭만큼 넓힌 박스 안의 대상에게 피해
    void HitAlong(float fromX, float toX)
    {
        float minX = Mathf.Min(fromX, toX) - SkillCatalog.WaveWidth * 0.5f;
        float maxX = Mathf.Max(fromX, toX) + SkillCatalog.WaveWidth * 0.5f;
        Vector2 center = new Vector2((minX + maxX) * 0.5f, transform.position.y);
        Vector2 size = new Vector2(maxX - minX, SkillCatalog.WaveHeight);

        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = true;
        filter.useLayerMask = false;

        overlaps.Clear();
        Physics2D.OverlapBox(center, size, 0f, filter, overlaps);
        foreach (Collider2D col in overlaps)
        {
            if (owner != null && col.transform.IsChildOf(owner.transform)) continue;

            EnemyProjectile projectile = col.GetComponent<EnemyProjectile>();
            if (projectile != null)
            {
                projectile.Shatter();
                continue;
            }

            IDamageable target = col.GetComponentInParent<IDamageable>();
            if (target == null || !alreadyHit.Add(target)) continue;
            target.TakeHit(new HitInfo
            {
                damage = damage,
                direction = new Vector2(facing, 0f),
                guardBreak = true,
                source = owner,
            });
        }
    }

    void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }
}
