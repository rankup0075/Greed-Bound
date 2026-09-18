using UnityEngine;

// 적 머리 위 체력바 (Spec 8장 적 체력바).
// 최대 체력이면 숨기고, 깎이면 표시. 재생으로 회복 중에는 초록, 정예는 금색, 그 외 붉은색.
public class EnemyHealthBar : MonoBehaviour
{
    const float Height = 0.1f;
    const float Gap = 0.25f;            // 몸 윗면과의 간격
    const float Padding = 0.3f;         // 몸 폭보다 이만큼 넓게
    const float MinWidth = 0.8f;
    const int SortingOrder = 70;

    static readonly Color BackColor = new Color(0.08f, 0.06f, 0.1f, 0.85f);
    static readonly Color HealthColor = new Color(0.9f, 0.2f, 0.2f);
    static readonly Color EliteColor = new Color(1f, 0.8f, 0.25f);
    static readonly Color RegenColor = new Color(0.35f, 1f, 0.45f);

    private Enemy enemy;
    private Transform back;
    private Transform fill;
    private Mesh backMesh;
    private Mesh fillMesh;
    private float width = MinWidth;
    private Color currentFillColor;

    public static EnemyHealthBar Create(Enemy enemy)
    {
        GameObject go = new GameObject("HealthBar");
        go.transform.SetParent(enemy.transform, false);

        EnemyHealthBar bar = go.AddComponent<EnemyHealthBar>();
        bar.enemy = enemy;
        bar.back = bar.CreateQuad("Back", BackColor, SortingOrder, out bar.backMesh);
        bar.fill = bar.CreateQuad("Fill", HealthColor, SortingOrder + 1, out bar.fillMesh);
        bar.currentFillColor = HealthColor;
        bar.SetVisible(false);
        return bar;
    }

    // 적 크기가 정해진 뒤 호출. 루트 스케일은 1이라 월드 단위 그대로 배치
    public void Layout(Vector2 bodySize)
    {
        width = Mathf.Max(MinWidth, bodySize.x + Padding);
        transform.localScale = Vector3.one;
        transform.localPosition = new Vector3(0f, bodySize.y * 0.5f + Gap + Height * 0.5f, 0f);

        back.localPosition = new Vector3(-width * 0.5f, 0f, 0f);
        back.localScale = new Vector3(width, Height, 1f);
        fill.localPosition = new Vector3(-width * 0.5f, 0f, 0f);
    }

    void LateUpdate()
    {
        if (enemy == null) return;

        float ratio = enemy.MaxHealth > 0f ? Mathf.Clamp01(enemy.CurrentHealth / enemy.MaxHealth) : 1f;
        bool visible = !enemy.IsDead && ratio < 0.999f;
        SetVisible(visible);
        if (!visible) return;

        fill.localScale = new Vector3(width * ratio, Height * 0.7f, 1f);

        Color color = enemy.IsRegenerating ? RegenColor
            : enemy.Type == EnemyType.Elite || enemy.IsBoss ? EliteColor
            : HealthColor;
        if (color != currentFillColor)
        {
            currentFillColor = color;
            SetMeshColor(fillMesh, color);
        }
    }

    void SetVisible(bool visible)
    {
        if (back.gameObject.activeSelf == visible) return;
        back.gameObject.SetActive(visible);
        fill.gameObject.SetActive(visible);
    }

    // 왼쪽 끝이 원점인 1×1 사각형 — x 스케일로 채움 비율을 표현
    Transform CreateQuad(string name, Color color, int order, out Mesh mesh)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);

        mesh = new Mesh();
        mesh.vertices = new[]
        {
            new Vector3(0f, -0.5f, 0f), new Vector3(0f, 0.5f, 0f),
            new Vector3(1f, 0.5f, 0f), new Vector3(1f, -0.5f, 0f),
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        SetMeshColor(mesh, color);
        mesh.RecalculateBounds();

        go.AddComponent<MeshFilter>().mesh = mesh;
        MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = RuntimeMaterials.SpriteUnlit;
        meshRenderer.sortingOrder = order;
        return go.transform;
    }

    static void SetMeshColor(Mesh mesh, Color color)
    {
        mesh.colors = new[] { color, color, color, color };
    }

    void OnDestroy()
    {
        if (backMesh != null) Destroy(backMesh);
        if (fillMesh != null) Destroy(fillMesh);
    }
}
