using UnityEngine;

// 보복의 가시 발동 표시 (Spec 6-1장: 시각 효과 필수 — 없으면 원인 불명 피해로 보임).
// 플레이어 중심에서 판정 반경까지 뻗는 가시 모양이 번쩍이고 사라짐. 가시 끝 = 판정 반경.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ThornsBurst : MonoBehaviour
{
    const int Spikes = 12;
    const float Lifetime = 0.3f;
    const int SortingOrder = 165;  // 어둠(150) 위
    static readonly Color SpikeColor = new Color(0.75f, 1f, 0.4f);

    private Mesh mesh;
    private float age;

    public static void Spawn(Vector2 center, float radius)
    {
        GameObject go = new GameObject("ThornsBurst");
        go.transform.position = new Vector3(center.x, center.y, 0f);
        go.AddComponent<ThornsBurst>().Build(radius);
    }

    // 중심 + 가시마다 (끝점, 양쪽 밑동) 삼각형
    void Build(float radius)
    {
        Vector3[] vertices = new Vector3[1 + Spikes * 3];
        int[] triangles = new int[Spikes * 6];
        vertices[0] = Vector3.zero;

        float halfWidth = Mathf.PI / Spikes * 0.45f;
        float baseRadius = radius * 0.3f;
        for (int i = 0; i < Spikes; i++)
        {
            float angle = i / (float)Spikes * Mathf.PI * 2f;
            int tip = 1 + i * 3;
            vertices[tip] = Direction(angle) * radius;
            vertices[tip + 1] = Direction(angle - halfWidth) * baseRadius;
            vertices[tip + 2] = Direction(angle + halfWidth) * baseRadius;

            int t = i * 6;
            triangles[t] = tip + 1; triangles[t + 1] = tip; triangles[t + 2] = tip + 2;
            triangles[t + 3] = 0; triangles[t + 4] = tip + 1; triangles[t + 5] = tip + 2;
        }

        mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        ApplyAlpha(1f);

        GetComponent<MeshFilter>().mesh = mesh;
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = RuntimeMaterials.SpriteUnlit;
        meshRenderer.sortingOrder = SortingOrder;
    }

    static Vector3 Direction(float angle) => new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

    void Update()
    {
        age += Time.deltaTime;
        if (age >= Lifetime)
        {
            Destroy(gameObject);
            return;
        }
        ApplyAlpha(1f - age / Lifetime);
    }

    void ApplyAlpha(float alpha)
    {
        Color c = SpikeColor;
        c.a = 0.85f * alpha;
        Color[] colors = new Color[mesh.vertexCount];
        for (int i = 0; i < colors.Length; i++) colors[i] = c;
        mesh.colors = colors;
    }

    void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }
}
