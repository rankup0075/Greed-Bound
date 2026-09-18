using UnityEngine;

// 지면 강타 이펙트. 발 위치에서 좌우로 지면 파동이 퍼짐. 최종 폭·높이 = 판정 범위 (Spec 5장: 판정과 이펙트 크기 일치).
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ShockwaveEffect : MonoBehaviour
{
    const float SpreadTime = 0.12f;
    const float FadeTime = 0.2f;
    static readonly Color WaveColor = new Color(1f, 0.8f, 0.45f);

    private Mesh mesh;
    private float range;
    private float height;
    private float age;

    // feet = 파동 중심(플레이어 발), range = 한쪽 거리
    public static void Spawn(Vector2 feet, float range, float height)
    {
        GameObject go = new GameObject("ShockwaveEffect");
        go.transform.position = new Vector3(feet.x, feet.y, 0f);
        ShockwaveEffect effect = go.AddComponent<ShockwaveEffect>();
        effect.range = range;
        effect.height = height;
    }

    void Awake()
    {
        mesh = new Mesh();
        mesh.MarkDynamic();
        GetComponent<MeshFilter>().mesh = mesh;
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = RuntimeMaterials.SpriteUnlit;
        meshRenderer.sortingOrder = 100;
    }

    void Start()
    {
        // 좌우 사각형 2개. 각 사각형은 가운데(발)가 아래쪽이 진하고 위로 옅어짐
        mesh.vertices = new Vector3[8];
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7 };
        Rebuild();
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= SpreadTime + FadeTime)
        {
            Destroy(gameObject);
            return;
        }
        Rebuild();
    }

    void Rebuild()
    {
        float reach = range * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / SpreadTime));
        float fade = 1f - Mathf.Clamp01((age - SpreadTime) / FadeTime);

        Color bottom = WaveColor;
        bottom.a = 0.75f * fade;
        Color top = WaveColor;
        top.a = 0f;

        mesh.vertices = new[]
        {
            new Vector3(0f, 0f, 0f), new Vector3(0f, height, 0f), new Vector3(reach, height, 0f), new Vector3(reach, 0f, 0f),
            new Vector3(0f, 0f, 0f), new Vector3(0f, height, 0f), new Vector3(-reach, height, 0f), new Vector3(-reach, 0f, 0f),
        };
        mesh.colors = new[] { bottom, top, top, bottom, bottom, top, top, bottom };
        mesh.RecalculateBounds();
    }

    void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }
}
