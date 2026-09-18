using UnityEngine;

// 적 사망 분해 파티클 (Spec 8장 "사망 파티클"). 도트에 맞게 작은 사각 조각이 튀어 흩어지며 사라짐.
// 파티클 시스템 대신 코드 메시 한 장 — 다른 이펙트(검격·가시)와 같은 방식이라 빌드·머티리얼이 늘지 않음.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class DeathBurst : MonoBehaviour
{
    const int SortingOrder = 100;          // 검격 이펙트와 같은 층 (캐릭터 위, 어둠 150 아래 — 어둠 밖에서는 안 보임)
    const float Gravity = 30f;             // 조각이 떨어지는 가속도 (게임 중력보다 가볍게)
    const float FadeFrom = 0.6f;           // 수명의 이 비율부터 흐려짐
    const float PixelSize = 1f / 36f;      // 1px (Spec 8장 PPU 36)

    struct Piece
    {
        public Vector2 position;
        public Vector2 velocity;
        public float half;                 // 조각 반 크기
        public float lifetime;
    }

    private Mesh mesh;
    private Piece[] pieces;
    private Vector3[] vertices;
    private Color[] colors;
    private Color color;
    private float age;
    private float maxLifetime;

    // center: 몸 가운데, bodySize: 판정 크기(조각 수·퍼지는 범위), color: 적 색
    public static void Spawn(Vector2 center, Vector2 bodySize, Color color)
    {
        GameObject go = new GameObject("DeathBurst");
        go.transform.position = new Vector3(center.x, center.y, 0f);
        go.AddComponent<DeathBurst>().Build(bodySize, color);
    }

    void Build(Vector2 bodySize, Color color)
    {
        this.color = color;
        float area = Mathf.Max(0.1f, bodySize.x * bodySize.y);
        int count = Mathf.Clamp(Mathf.RoundToInt(area * 16f), 10, 48);      // 일반 적 약 22개, 보스 48개
        float sizeScale = Mathf.Clamp(bodySize.y / 1.38f, 0.8f, 2f);        // 큰 적일수록 조각도 크게

        pieces = new Piece[count];
        vertices = new Vector3[count * 4];
        colors = new Color[count * 4];
        int[] triangles = new int[count * 6];

        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float speed = Random.Range(1.8f, 5f);
            pieces[i] = new Piece
            {
                // 몸 안 아무 곳에서 시작해 사방으로 튐 (위쪽으로 조금 더)
                position = new Vector2(Random.Range(-0.5f, 0.5f) * bodySize.x, Random.Range(-0.5f, 0.5f) * bodySize.y),
                velocity = new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed + 2f),
                half = PixelSize * Random.Range(1f, 2.5f) * sizeScale,
                lifetime = Random.Range(0.45f, 0.75f),
            };
            maxLifetime = Mathf.Max(maxLifetime, pieces[i].lifetime);

            int v = i * 4, t = i * 6;
            triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
            triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
        }

        mesh = new Mesh();
        WriteVertices();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.colors = colors;
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().mesh = mesh;
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = RuntimeMaterials.SpriteUnlit;
        meshRenderer.sortingOrder = SortingOrder;
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= maxLifetime)
        {
            Destroy(gameObject);
            return;
        }

        float dt = Time.deltaTime;
        for (int i = 0; i < pieces.Length; i++)
        {
            pieces[i].velocity.y -= Gravity * dt;
            pieces[i].position += pieces[i].velocity * dt;
        }

        WriteVertices();
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.RecalculateBounds();
    }

    // 조각마다 사각형 4점 + 남은 수명에 따른 알파. 수명이 끝난 조각은 크기 0으로 접어 감춤
    void WriteVertices()
    {
        for (int i = 0; i < pieces.Length; i++)
        {
            Piece piece = pieces[i];
            float remaining = 1f - age / piece.lifetime;
            float half = remaining > 0f ? piece.half : 0f;
            Color c = color;
            c.a = remaining <= 0f ? 0f : Mathf.Clamp01(remaining / (1f - FadeFrom));

            int v = i * 4;
            vertices[v] = new Vector3(piece.position.x - half, piece.position.y - half, 0f);
            vertices[v + 1] = new Vector3(piece.position.x - half, piece.position.y + half, 0f);
            vertices[v + 2] = new Vector3(piece.position.x + half, piece.position.y + half, 0f);
            vertices[v + 3] = new Vector3(piece.position.x + half, piece.position.y - half, 0f);
            for (int k = 0; k < 4; k++) colors[v + k] = c;
        }
    }

    void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }
}
