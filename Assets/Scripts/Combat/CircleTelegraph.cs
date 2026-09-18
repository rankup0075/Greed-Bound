using UnityEngine;

// 원형 예고 표시 (Spec 8장 예고 원칙: 즉발 피해 금지).
// 예고 시간 동안 판정 반경과 같은 크기의 원이 점점 진해지고, 끝나는 순간 밝게 번쩍인 뒤 사라짐.
// 피해 판정은 GameLoopQueue 쪽에서 같은 반경·같은 시간으로 처리 — 이 스크립트는 보이기만 함.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class CircleTelegraph : MonoBehaviour
{
    const int Segments = 40;
    const float FlashTime = 0.2f;

    private Mesh mesh;
    private Color color;
    private float warnTime;
    private float age;

    public static void Spawn(Vector2 center, float radius, float warnTime, Color color)
    {
        GameObject go = new GameObject("CircleTelegraph");
        go.transform.position = new Vector3(center.x, center.y, 0f);

        if (warnTime > 0f) SoundManager.Play(SoundId.Warn);   // 예고음은 시각 예고와 쌍으로 (Spec 8장)

        CircleTelegraph telegraph = go.AddComponent<CircleTelegraph>();
        telegraph.warnTime = warnTime;
        telegraph.color = color;
        telegraph.BuildMesh(radius);
    }

    void BuildMesh(float radius)
    {
        mesh = new Mesh();
        mesh.MarkDynamic();

        // 중심 + 테두리 점들로 부채꼴을 이어 붙인 원
        Vector3[] vertices = new Vector3[Segments + 1];
        int[] triangles = new int[Segments * 3];
        vertices[0] = Vector3.zero;
        for (int i = 0; i < Segments; i++)
        {
            float angle = i / (float)Segments * Mathf.PI * 2f;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;  // 반경 = 판정 반경
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = (i + 1) % Segments + 1;
        }
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.colors = new Color[vertices.Length];
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().mesh = mesh;
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = RuntimeMaterials.SpriteUnlit;
        meshRenderer.sortingOrder = 165;  // 어둠(150) 위 — 예고는 항상 보이게

        ApplyColor();
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= warnTime + FlashTime)
        {
            Destroy(gameObject);
            return;
        }
        ApplyColor();
    }

    void ApplyColor()
    {
        float alpha;
        if (age < warnTime)
        {
            // 예고: 점점 진해지고, 끝나갈수록 빠르게 깜빡임
            float t = age / warnTime;
            float blink = 0.75f + 0.25f * Mathf.Sin(age * Mathf.Lerp(10f, 40f, t));
            alpha = Mathf.Lerp(0.12f, 0.4f, t) * blink;
        }
        else
        {
            // 발동: 번쩍 → 사라짐
            alpha = Mathf.Lerp(0.85f, 0f, (age - warnTime) / FlashTime);
        }

        Color c = age < warnTime ? color : Color.Lerp(color, Color.white, 0.5f);
        c.a = alpha;
        Color[] colors = mesh.colors;
        for (int i = 0; i < colors.Length; i++) colors[i] = c;
        colors[0].a = alpha * 0.6f;  // 가운데는 조금 옅게
        mesh.colors = colors;
    }

    void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }
}
