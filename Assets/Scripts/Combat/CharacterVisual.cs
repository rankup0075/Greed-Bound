using UnityEngine;

// 캐릭터 몸 판정(루트)과 그림(자식 "Visual") 분리 (Spec 8장 "캐릭터 판정·그림 분리").
// - 루트: Rigidbody2D·BoxCollider2D. 스케일은 항상 1, 판정 크기는 collider.size
// - Visual: SpriteRenderer(나중에 Animator도 여기에). 좌우 반전은 Visual만
// 그림 배치는 스프라이트 pivot으로 자동 결정
// - 가운데 pivot(임시 1×1 사각형): 몸 크기로 늘려서 루트 가운데에
// - 발 pivot(도트 스프라이트, 임포트 규칙 Bottom Center): 스케일 1로 몸 발 위치에 — 몸 크기와 무관하게 원래 픽셀 크기
public class CharacterVisual : MonoBehaviour
{
    public const string VisualName = "Visual";

    // 캐릭터 크기 배율 (Spec 8장 "캐릭터 크기 배율", 2026-09-18 사용자 결정 ×1.35).
    // 맵·물리는 그대로 두고 캐릭터만 크게 보이게 하는 값 — 판정 크기와 그림 px에 함께 적용.
    // 플레이어 판정은 프리팹에 저장된 값(1.3875u = 37px × 1.35), 적은 Enemy가 이 값을 곱함
    public const float SizeScale = 1.35f;

    // 보스만 ×1.05 (2026-09-18 사용자 결정). ×1.35면 키 2.84u가 되어 발판 아래 통과 높이(가장 낮은 곳 2.27u)를
    // 넘겨 발판에 끼임 — 발판을 높이면 플레이어 점프(3.42u)로 못 올라가므로 보스 쪽을 낮춤
    public const float BossSizeScale = 1.05f;

    public SpriteRenderer Renderer { get; private set; }
    public Vector2 BodySize { get; private set; } = Vector2.one;
    public int Facing { get; private set; } = 1;
    public float ArtScale { get; private set; } = 1f;
    public bool IsPixelArt => Renderer != null && IsFootPivot(Renderer.sprite);  // 임시 사각형이 아닌 도트 스프라이트

    private Transform visual;
    private BoxCollider2D body;
    private Sprite laidOutSprite;

    // 루트에 붙이고(이미 있으면 그대로) 구조를 맞춰 돌려줌. Awake 순서와 상관없이 누가 먼저 불러도 같은 결과
    public static CharacterVisual Ensure(GameObject root)
    {
        CharacterVisual cv = root.GetComponent<CharacterVisual>();
        if (cv == null) cv = root.AddComponent<CharacterVisual>();
        cv.Setup();
        return cv;
    }

    void Awake() => Setup();

    void Setup()
    {
        if (visual != null) return;

        Migrate(gameObject, immediate: false);
        visual = transform.Find(VisualName);
        Renderer = visual.GetComponent<SpriteRenderer>();
        body = GetComponent<BoxCollider2D>();
        BodySize = body != null ? body.size : Vector2.one;
        Layout();
    }

    // 예전 구조(루트에 SpriteRenderer, 스케일로 크기 표현)를 새 구조로 옮김.
    // 실행 중에는 인스턴스마다, 에디터 메뉴(캐릭터 프리팹 구조 변환)에서는 프리팹 자체에 적용
    public static bool Migrate(GameObject root, bool immediate)
    {
        bool changed = false;
        Transform rootTransform = root.transform;

        // 루트 스케일 → 콜라이더 크기로 옮기고 스케일 1
        Vector3 scale = rootTransform.localScale;
        if (scale != Vector3.one)
        {
            BoxCollider2D box = root.GetComponent<BoxCollider2D>();
            if (box != null)
            {
                Vector2 abs = new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
                box.size = Vector2.Scale(box.size, abs);
                box.offset = Vector2.Scale(box.offset, abs);
            }
            rootTransform.localScale = Vector3.one;
            changed = true;
        }

        Transform visual = rootTransform.Find(VisualName);
        if (visual == null)
        {
            GameObject go = new GameObject(VisualName);
            go.transform.SetParent(rootTransform, false);
            go.layer = root.layer;
            visual = go.transform;
            changed = true;
        }

        SpriteRenderer target = visual.GetComponent<SpriteRenderer>();
        SpriteRenderer source = root.GetComponent<SpriteRenderer>();
        if (target == null)
        {
            target = visual.gameObject.AddComponent<SpriteRenderer>();
            changed = true;
        }
        if (source != null)
        {
            target.sprite = source.sprite;
            target.color = source.color;
            target.sharedMaterial = source.sharedMaterial;
            target.sortingLayerID = source.sortingLayerID;
            target.sortingOrder = source.sortingOrder;
            if (immediate) Object.DestroyImmediate(source, true);
            else
            {
                source.enabled = false;  // 파괴는 프레임 끝이라 이번 프레임에 겹쳐 그려지지 않게
                Object.Destroy(source);
            }
            changed = true;
        }
        return changed;
    }

    // 판정 크기 변경 (적 종류·거인화). 콜라이더와 그림을 같이 맞춤.
    // artScale: 도트 스프라이트 확대 배율 (거인화처럼 원래 그림보다 커 보여야 할 때)
    public void SetBodySize(Vector2 size, float artScale = 1f)
    {
        Setup();
        BodySize = size;
        ArtScale = artScale;
        if (body != null)
        {
            body.size = size;
            body.offset = Vector2.zero;
        }
        Layout();
    }

    public void SetFacing(int facing)
    {
        Setup();
        facing = facing < 0 ? -1 : 1;
        if (Facing == facing) return;
        Facing = facing;
        Layout();
    }

    void LateUpdate()
    {
        // Animator가 pivot이 다른 스프라이트로 바꾸면 다시 배치
        if (Renderer != null && Renderer.sprite != laidOutSprite) Layout();
    }

    void Layout()
    {
        if (visual == null) return;
        Sprite sprite = Renderer != null ? Renderer.sprite : null;
        laidOutSprite = sprite;

        if (IsFootPivot(sprite))
        {
            visual.localScale = new Vector3(Facing * ArtScale, ArtScale, 1f);
            visual.localPosition = new Vector3(0f, -BodySize.y * 0.5f, 0f);
        }
        else
        {
            // 임시 사각형: 스프라이트 크기(Square = 1u)로 나눠 몸 크기에 맞춤
            Vector2 spriteSize = sprite != null ? (Vector2)sprite.bounds.size : Vector2.one;
            if (spriteSize.x <= 0f || spriteSize.y <= 0f) spriteSize = Vector2.one;
            visual.localScale = new Vector3(Facing * BodySize.x / spriteSize.x, BodySize.y / spriteSize.y, 1f);
            visual.localPosition = Vector3.zero;
        }
    }

    // pivot이 스프라이트 아래쪽 끝(1px 이내)이면 도트 스프라이트로 봄
    static bool IsFootPivot(Sprite sprite)
    {
        return sprite != null && sprite.pivot.y <= 1f;
    }
}
