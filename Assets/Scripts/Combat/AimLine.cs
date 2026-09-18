using UnityEngine;

// 원거리 적의 조준선 예고 (Spec 5장 원거리 적 AI, 8장 예고 원칙).
// 마법구가 날아갈 방향과 사격 거리를 그대로 보여줌. 발사가 가까울수록 진해지고 빠르게 깜빡임.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class AimLine : MonoBehaviour
{
    const float Width = 0.07f;
    static readonly Color LineColor = new Color(1f, 0.25f, 0.2f);

    private Mesh mesh;
    private float blinkTime;

    public static AimLine Create()
    {
        GameObject go = new GameObject("AimLine");
        AimLine line = go.AddComponent<AimLine>();
        line.Build();
        go.SetActive(false);
        return line;
    }

    void Build()
    {
        // 길이 1, 폭 1짜리 사각형 — 시작점이 원점. 스케일로 길이·폭을 맞춤
        mesh = new Mesh();
        mesh.vertices = new[]
        {
            new Vector3(0f, -0.5f, 0f), new Vector3(0f, 0.5f, 0f),
            new Vector3(1f, 0.5f, 0f), new Vector3(1f, -0.5f, 0f),
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.colors = new Color[4];
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().mesh = mesh;
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = RuntimeMaterials.SpriteUnlit;
        meshRenderer.sortingOrder = 170;  // 어둠(150) 위 — 예고는 항상 보이게
    }

    // progress: 0 = 조준 시작, 1 = 발사 직전
    public void Show(Vector2 origin, Vector2 direction, float length, float progress)
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            blinkTime = 0f;
        }

        transform.position = new Vector3(origin.x, origin.y, 0f);
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        transform.localScale = new Vector3(length, Width, 1f);

        progress = Mathf.Clamp01(progress);
        blinkTime += Time.deltaTime;
        float blink = 0.7f + 0.3f * Mathf.Sin(blinkTime * Mathf.Lerp(12f, 45f, progress));

        // 시작점은 진하고 끝으로 갈수록 옅게
        Color near = LineColor;
        near.a = Mathf.Lerp(0.25f, 0.85f, progress) * blink;
        Color far = near;
        far.a *= 0.3f;
        mesh.colors = new[] { near, near, far, far };
    }

    public void Hide()
    {
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }
}
