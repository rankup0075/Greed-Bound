using UnityEngine;

// 회전베기 이펙트. 판정 반지름을 그대로 받아 원형 고리를 그림 (Spec 5장: 판정과 이펙트 크기 일치).
// 밝은 칼끝이 한 바퀴 돌며 지나가고 사라짐. 플레이어 위치를 따라감.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class WhirlwindEffect : MonoBehaviour
{
    const int Segments = 48;
    const float SpinTime = 0.18f;
    const float FadeTime = 0.12f;
    static readonly Color BladeColor = new Color(0.85f, 0.95f, 1f);

    private Mesh mesh;
    private readonly Vector3[] vertices = new Vector3[(Segments + 1) * 2];
    private readonly Color[] colors = new Color[(Segments + 1) * 2];
    private float radius;
    private int facing;
    private float age;
    private Transform follow;

    public static void Spawn(Transform follow, float radius, int facing)
    {
        GameObject go = new GameObject("WhirlwindEffect");
        go.transform.position = new Vector3(follow.position.x, follow.position.y, 0f);
        WhirlwindEffect effect = go.AddComponent<WhirlwindEffect>();
        effect.follow = follow;
        effect.radius = radius;
        effect.facing = facing;
    }

    void Awake()
    {
        mesh = new Mesh();
        mesh.MarkDynamic();
        GetComponent<MeshFilter>().mesh = mesh;
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = RuntimeMaterials.SpriteUnlit;
        meshRenderer.sortingOrder = 100;  // 검격 이펙트와 같은 층
    }

    void Start()
    {
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
        if (age >= SpinTime + FadeTime)
        {
            Destroy(gameObject);
            return;
        }
        Rebuild();
    }

    void LateUpdate()
    {
        if (follow != null) transform.position = new Vector3(follow.position.x, follow.position.y, 0f);
    }

    void Rebuild()
    {
        float spin = Mathf.Clamp01(age / SpinTime);                        // 칼끝이 한 바퀴 도는 진행도
        float fade = 1f - Mathf.Clamp01((age - SpinTime) / FadeTime);
        float head = spin * Mathf.PI * 2f;
        float inner = radius * 0.62f;

        for (int i = 0; i <= Segments; i++)
        {
            float angle = i / (float)Segments * Mathf.PI * 2f;
            Vector2 dir = new Vector2(Mathf.Cos(angle) * facing, Mathf.Sin(angle));  // 바라보는 방향으로 회전

            // 칼끝이 지나간 부분만 보이고, 칼끝에 가까울수록 진함
            float behind = head - angle;
            float alpha = behind < 0f ? 0f : Mathf.Lerp(0.9f, 0.25f, Mathf.Clamp01(behind / (Mathf.PI * 2f)));
            Color c = BladeColor;
            c.a = alpha * fade;

            vertices[i * 2] = dir * radius;   // 바깥 테두리 = 판정 반지름
            vertices[i * 2 + 1] = dir * inner;
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
