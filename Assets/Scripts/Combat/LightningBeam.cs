using UnityEngine;

// 마력 폭풍 낙뢰가 떨어지는 순간의 번쩍임. 화면 위에서 낙뢰 지점까지 세로 빛기둥. 보이기만 함.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class LightningBeam : MonoBehaviour
{
    const float Lifetime = 0.25f;
    const float Width = 0.35f;
    const int SortingOrder = 168;  // 어둠(150) 위
    static readonly Color BeamColor = new Color(0.75f, 0.9f, 1f);

    private Mesh mesh;
    private float age;

    public static void Spawn(Vector2 strikePoint)
    {
        GameObject go = new GameObject("LightningBeam");
        go.transform.position = new Vector3(strikePoint.x, strikePoint.y, 0f);
        go.AddComponent<LightningBeam>().Build(ArenaLayout.CameraY + 6f - strikePoint.y);
    }

    void Build(float height)
    {
        mesh = new Mesh();
        mesh.vertices = new[]
        {
            new Vector3(-Width * 0.5f, 0f, 0f), new Vector3(-Width * 0.5f, height, 0f),
            new Vector3(Width * 0.5f, height, 0f), new Vector3(Width * 0.5f, 0f, 0f),
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.colors = new[] { BeamColor, BeamColor, BeamColor, BeamColor };
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().mesh = mesh;
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = RuntimeMaterials.SpriteUnlit;
        meshRenderer.sortingOrder = SortingOrder;
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= Lifetime)
        {
            Destroy(gameObject);
            return;
        }

        Color c = BeamColor;
        c.a = 1f - age / Lifetime;
        mesh.colors = new[] { c, c, c, c };
        transform.localScale = new Vector3(1f + age * 4f, 1f, 1f);  // 퍼지면서 사라짐
    }

    void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }
}
