using UnityEngine;

// 어둠 카드의 원형 시야 (Spec 3장 환경 카드 동작).
// 플레이어 중심에서 안쪽 반경까지는 투명, 바깥 반경까지 점점 어두워지고, 그 밖은 완전히 검정.
// 예고 표시(조준선·예고 원·마법구 등)는 이보다 높은 sortingOrder라 항상 보임.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class DarknessOverlay : MonoBehaviour
{
    const int Segments = 64;
    const float FarRadius = 60f;        // 화면 전체를 덮을 만큼
    const int SortingOrder = 150;

    private Mesh mesh;
    private Vector3[] vertices;
    private float builtInner = -1f;
    private float builtOuter = -1f;

    public static DarknessOverlay Create()
    {
        GameObject go = new GameObject("DarknessOverlay");
        DarknessOverlay overlay = go.AddComponent<DarknessOverlay>();
        overlay.Build();
        go.SetActive(false);
        return overlay;
    }

    // 정점 3줄: 안쪽 원(투명) / 바깥 원(검정) / 먼 원(검정)
    void Build()
    {
        mesh = new Mesh();
        mesh.MarkDynamic();

        int ringCount = 3;
        vertices = new Vector3[Segments * ringCount];
        Color[] colors = new Color[vertices.Length];
        int[] triangles = new int[Segments * (ringCount - 1) * 6];

        for (int i = 0; i < Segments; i++)
        {
            colors[i] = new Color(0f, 0f, 0f, 0f);
            colors[i + Segments] = Color.black;
            colors[i + Segments * 2] = Color.black;
        }

        int t = 0;
        for (int ring = 0; ring < ringCount - 1; ring++)
        {
            for (int i = 0; i < Segments; i++)
            {
                int next = (i + 1) % Segments;
                int a = ring * Segments + i, b = ring * Segments + next;
                int c = (ring + 1) * Segments + i, d = (ring + 1) * Segments + next;
                triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
                triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
            }
        }

        SetRadii(1f, 2f);
        mesh.colors = colors;
        mesh.triangles = triangles;

        GetComponent<MeshFilter>().mesh = mesh;
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = RuntimeMaterials.SpriteUnlit;
        meshRenderer.sortingOrder = SortingOrder;
    }

    public void Show(Vector2 center, float innerRadius, float outerRadius)
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        transform.position = new Vector3(center.x, center.y, 0f);
        if (!Mathf.Approximately(innerRadius, builtInner) || !Mathf.Approximately(outerRadius, builtOuter))
            SetRadii(innerRadius, outerRadius);
    }

    public void Hide()
    {
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    void SetRadii(float inner, float outer)
    {
        builtInner = inner;
        builtOuter = outer;
        for (int i = 0; i < Segments; i++)
        {
            float angle = i / (float)Segments * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            vertices[i] = dir * inner;
            vertices[i + Segments] = dir * outer;
            vertices[i + Segments * 2] = dir * FarRadius;
        }
        mesh.vertices = vertices;
        mesh.RecalculateBounds();
    }

    void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }
}
