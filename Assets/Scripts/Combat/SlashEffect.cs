using UnityEngine;

// 근접 공격 검격 이펙트. 공격 판정과 같은 반지름의 전방 반원을 초승달 모양으로 그림.
// 스프라이트 없이 코드로 메시를 만들어서, 반지름이 판정과 어긋날 수 없게 함 (Spec 5장 경고 사항).
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SlashEffect : MonoBehaviour
{
    const int Segments = 24;
    const float SweepTime = 0.07f;  // 칼날이 반원을 쓸고 지나가는 시간
    const float FadeTime = 0.10f;   // 다 그린 뒤 사라지는 시간

    private Mesh mesh;
    private readonly Vector3[] vertices = new Vector3[(Segments + 1) * 2];
    private readonly Color[] colors = new Color[(Segments + 1) * 2];
    private float radius;
    private float innerRadius;
    private int facing;
    private bool upward;            // true = 올려베기(아래 → 위), false = 내려베기(위 → 아래)
    private Color color;
    private float age;
    private Transform follow;       // 이동하면서 공격해도 이펙트가 뒤처지지 않게 따라갈 대상

    // stage: 0 = 1타 내려베기, 1 = 2타 올려베기, 2 = 3타 크게 내려찍기
    // 자식으로 붙이지 않고 위치만 따라감 — 플레이어가 좌우 반전(scale.x = -1)돼도 이펙트 방향은 공격한 방향 그대로
    public static void Spawn(Transform follow, float radius, int facing, int stage)
    {
        Color color = stage == 2 ? new Color(1f, 0.85f, 0.4f) : new Color(0.9f, 0.95f, 1f);
        Spawn(follow, radius, facing, stage, color);
    }

    // 색을 직접 지정하는 버전 — 적 공격 이펙트에 사용
    public static void Spawn(Transform follow, float radius, int facing, int stage, Color color)
    {
        GameObject go = new GameObject("SlashEffect");
        go.transform.position = new Vector3(follow.position.x, follow.position.y, 0f);

        SlashEffect effect = go.AddComponent<SlashEffect>();
        effect.follow = follow;
        effect.radius = radius;
        effect.innerRadius = radius * (stage == 2 ? 0.35f : 0.6f);  // 3타는 두꺼운 칼날
        effect.facing = facing;
        effect.upward = stage == 1;
        effect.color = color;
    }

    void Awake()
    {
        mesh = new Mesh();
        mesh.MarkDynamic();
        GetComponent<MeshFilter>().mesh = mesh;

        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = RuntimeMaterials.SpriteUnlit;
        meshRenderer.sortingOrder = 100;  // 캐릭터·적보다 앞에
    }

    void Start()
    {
        // 인덱스는 한 번만 설정 (칸마다 사각형 = 삼각형 2개)
        int[] triangles = new int[Segments * 6];
        for (int i = 0; i < Segments; i++)
        {
            int outer = i * 2, inner = i * 2 + 1, nextOuter = outer + 2, nextInner = inner + 2;
            int t = i * 6;
            triangles[t] = outer; triangles[t + 1] = nextOuter; triangles[t + 2] = inner;
            triangles[t + 3] = inner; triangles[t + 4] = nextOuter; triangles[t + 5] = nextInner;
        }

        Rebuild();
        mesh.triangles = triangles;
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= SweepTime + FadeTime)
        {
            Destroy(gameObject);
            return;
        }
        Rebuild();
    }

    // 플레이어 이동(물리)이 반영된 뒤에 위치를 맞춰야 떨림이 없음
    void LateUpdate()
    {
        if (follow != null) transform.position = new Vector3(follow.position.x, follow.position.y, 0f);
    }

    void Rebuild()
    {
        float sweep = Mathf.Clamp01(age / SweepTime);                       // 0 → 1 동안 반원을 쓸어감
        float alpha = 1f - Mathf.Clamp01((age - SweepTime) / FadeTime);    // 다 그린 뒤 투명해짐

        float startAngle = upward ? -90f : 90f;
        float endAngle = Mathf.Lerp(startAngle, -startAngle, sweep);

        for (int i = 0; i <= Segments; i++)
        {
            float t = i / (float)Segments;
            float angle = Mathf.Lerp(startAngle, endAngle, t) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle) * facing, Mathf.Sin(angle));

            // 칼끝(최근에 쓸고 지나간 쪽)일수록 진하게
            Color c = color;
            c.a = alpha * Mathf.Lerp(0.25f, 0.9f, t);

            vertices[i * 2] = dir * radius;          // 바깥 테두리 = 판정 반지름 그대로
            vertices[i * 2 + 1] = dir * innerRadius;
            colors[i * 2] = c;
            colors[i * 2 + 1] = c;
        }

        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.RecalculateBounds();
    }

    void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }
}
