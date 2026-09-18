using UnityEngine;

// 죽음의 영역: 전투 중 양쪽에서 조여 오는 벽 (Spec 3장 환경 카드 동작).
// 움직이는 Kinematic 벽이 플레이어·적을 밀어내고, 벽 바깥은 붉게 표시. 현재 범위는 ArenaBounds에 반영.
public class DeathZoneWalls : MonoBehaviour
{
    const float WallThickness = 1f;
    const float WallHeight = 30f;
    const float OverlayBottom = -3f;
    const float OverlayTop = 12f;
    const int SortingOrder = 155;  // 어둠(150) 위 — 경계는 항상 보이게
    static readonly Color OverlayColor = new Color(0.7f, 0.05f, 0.1f, 0.35f);

    private Rigidbody2D leftWall;
    private Rigidbody2D rightWall;
    private Transform leftOverlay;
    private Transform rightOverlay;
    private Mesh overlayMesh;

    private bool active;
    private float duration;
    private float shrinkPerSide;
    private float minWidth;
    private float targetMinX;
    private float targetMaxX;

    public static DeathZoneWalls Create()
    {
        GameObject go = new GameObject("DeathZoneWalls");
        DeathZoneWalls walls = go.AddComponent<DeathZoneWalls>();
        walls.Build();
        go.SetActive(false);
        return walls;
    }

    void Build()
    {
        leftWall = CreateWall("Wall_Left");
        rightWall = CreateWall("Wall_Right");

        // 왼쪽 끝이 원점인 1×1 사각형 (가로 스케일로 폭 조절)
        overlayMesh = new Mesh();
        overlayMesh.vertices = new[]
        {
            new Vector3(0f, 0f, 0f), new Vector3(0f, 1f, 0f),
            new Vector3(1f, 1f, 0f), new Vector3(1f, 0f, 0f),
        };
        overlayMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        overlayMesh.colors = new[] { OverlayColor, OverlayColor, OverlayColor, OverlayColor };
        overlayMesh.RecalculateBounds();

        leftOverlay = CreateOverlay("Overlay_Left");
        rightOverlay = CreateOverlay("Overlay_Right");
    }

    Rigidbody2D CreateWall(string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        Rigidbody2D body = go.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;  // 움직이면서 다른 물체를 밀어냄
        body.useFullKinematicContacts = true;
        BoxCollider2D box = go.AddComponent<BoxCollider2D>();
        box.size = new Vector2(WallThickness, WallHeight);
        return body;
    }

    Transform CreateOverlay(string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = overlayMesh;
        MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = RuntimeMaterials.SpriteUnlit;
        meshRenderer.sortingOrder = SortingOrder;
        return go.transform;
    }

    // 전투 시작: duration초에 걸쳐 양쪽을 shrinkPerSide씩 조임 (최종 폭은 minWidth 이상)
    public void Begin(float duration, float shrinkPerSide, float minWidth)
    {
        this.duration = Mathf.Max(0.01f, duration);
        this.shrinkPerSide = shrinkPerSide;
        this.minWidth = minWidth;
        active = true;
        gameObject.SetActive(true);
        Apply(0f, snap: true);
    }

    // 경과 시간에 맞춰 목표 범위 갱신 (실제 벽 이동은 FixedUpdate)
    public void Tick(float elapsed)
    {
        if (active) Apply(elapsed, snap: false);
    }

    public void Stop()
    {
        active = false;
        ArenaBounds.Reset();
        gameObject.SetActive(false);
    }

    void Apply(float elapsed, bool snap)
    {
        float half = ArenaLayout.MapHalfWidth;
        float maxInset = Mathf.Max(0f, half - minWidth * 0.5f);
        float inset = Mathf.Min(shrinkPerSide * Mathf.Clamp01(elapsed / duration), maxInset);

        targetMinX = -half + inset;
        targetMaxX = half - inset;
        ArenaBounds.Set(targetMinX, targetMaxX);

        if (snap)
        {
            leftWall.position = new Vector2(targetMinX - WallThickness * 0.5f, ArenaLayout.GroundTop + WallHeight * 0.5f - 2f);
            rightWall.position = new Vector2(targetMaxX + WallThickness * 0.5f, ArenaLayout.GroundTop + WallHeight * 0.5f - 2f);
        }

        // 벽 바깥 붉은 영역: 맵 끝에서 벽까지
        float overlayHeight = OverlayTop - OverlayBottom;
        leftOverlay.position = new Vector3(-half - 2f, OverlayBottom, 0f);
        leftOverlay.localScale = new Vector3(Mathf.Max(0.001f, targetMinX - (-half - 2f)), overlayHeight, 1f);
        rightOverlay.position = new Vector3(targetMaxX, OverlayBottom, 0f);
        rightOverlay.localScale = new Vector3(Mathf.Max(0.001f, half + 2f - targetMaxX), overlayHeight, 1f);
    }

    void FixedUpdate()
    {
        if (!active) return;
        float y = ArenaLayout.GroundTop + WallHeight * 0.5f - 2f;
        leftWall.MovePosition(new Vector2(targetMinX - WallThickness * 0.5f, y));
        rightWall.MovePosition(new Vector2(targetMaxX + WallThickness * 0.5f, y));
    }

    void OnDestroy()
    {
        if (overlayMesh != null) Destroy(overlayMesh);
    }
}
