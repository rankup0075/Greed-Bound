using UnityEngine;

// 상점·로비처럼 걸어다니는 맵에서 함께 쓰는 임시 그리기 도구 (스프라이트·정식 UI 교체 전까지).
// 월드 좌표 색 사각형(코드 메시)과 월드 좌표 위 말풍선(OnGUI).
public static class WorldGui
{
    public const float VirtualHeight = 720f;   // OnGUI를 화면 높이 720 기준으로 맞춤

    // 아래 가운데를 기준으로 한 색 사각형
    public static MeshFilter CreateQuad(string name, Vector2 bottomCenter, Vector2 size, Color color, int sortingOrder)
    {
        GameObject go = new GameObject(name);
        go.transform.position = new Vector3(bottomCenter.x, bottomCenter.y, 0f);

        float half = size.x * 0.5f;
        Mesh mesh = new Mesh();
        mesh.vertices = new[]
        {
            new Vector3(-half, 0f, 0f), new Vector3(-half, size.y, 0f),
            new Vector3(half, size.y, 0f), new Vector3(half, 0f, 0f),
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.colors = new[] { color, color, color, color };
        mesh.RecalculateBounds();

        MeshFilter filter = go.AddComponent<MeshFilter>();
        filter.mesh = mesh;
        MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = RuntimeMaterials.SpriteUnlit;
        meshRenderer.sortingOrder = sortingOrder;
        return filter;
    }

    public static void SetQuadColor(MeshFilter filter, Color color)
    {
        filter.mesh.colors = new[] { color, color, color, color };
    }

    // 아래 가운데를 기준으로 한 스프라이트 (소품·NPC).
    // size 를 주면 그 크기로 **이어 붙인다**(벽·단처럼 길게 늘려야 하는 것).
    // 안 주면 그림 원래 크기 그대로 — 도트는 늘리면 픽셀이 뭉개지므로 이쪽이 기본이다.
    public static SpriteRenderer CreateSprite(string name, Vector2 bottomCenter, Sprite sprite,
                                              int sortingOrder, Vector2 size = default,
                                              string animator = null)
    {
        GameObject go = new GameObject(name);
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;

        bool stretched = size.x > 0f && size.y > 0f;
        if (stretched)
        {
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;
            renderer.size = size;
        }

        // 그림이 그려지는 칸은 **기준점(pivot)** 을 중심으로 잡힌다. 임포트 규칙이 소품은
        // 아래 가운데, 배경은 가운데로 잡으므로 어느 쪽이든 맞게 기준점 비율로 계산한다
        float pivotFraction = 0f;
        if (sprite != null && sprite.rect.height > 0f) pivotFraction = sprite.pivot.y / sprite.rect.height;
        float height = stretched ? size.y : (sprite != null ? sprite.bounds.size.y : 0f);
        go.transform.position = new Vector3(bottomCenter.x, bottomCenter.y + pivotFraction * height, 0f);

        // 움직이는 소품·NPC — 컨트롤러가 있으면 붙인다 (에디터 메뉴 "NPC 애니메이터 생성"이 만듦).
        // 없으면 첫 칸이 그대로 서 있으므로 게임은 그대로 돌아간다
        if (animator != null)
        {
            RuntimeAnimatorController controller =
                Resources.Load<RuntimeAnimatorController>("NpcAnimators/" + animator);
            if (controller != null)
            {
                Animator component = go.AddComponent<Animator>();
                component.runtimeAnimatorController = controller;
                component.applyRootMotion = false;
                component.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
        }
        return renderer;
    }

    // OnGUI 시작 시 호출: 화면 높이 720 기준 행렬을 걸고 그 기준의 화면 폭을 돌려줌
    public static float BeginVirtual(out float scale)
    {
        scale = Screen.height / VirtualHeight;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        return Screen.width / scale;
    }

    // 월드 좌표 anchor 바로 위에 말풍선 (아래 가운데가 anchor)
    public static void DrawBubble(Camera cam, float scale, Vector2 anchor, string text, float bubbleWidth, GUIStyle style, Color background)
    {
        Vector3 screen = cam.WorldToScreenPoint(anchor);
        if (screen.z < 0f) return;
        float x = screen.x / scale;
        float y = (Screen.height - screen.y) / scale;

        const float padding = 8f;
        float textHeight = style.CalcHeight(new GUIContent(text), bubbleWidth);
        Rect box = new Rect(x - bubbleWidth * 0.5f - padding, y - textHeight - padding * 2f, bubbleWidth + padding * 2f, textHeight + padding * 2f);

        // 말풍선은 물체를 따라 화면 밖으로 자연스럽게 나감 (화면 안으로 밀어 넣으면 스크롤할 때 화면 끝에 달라붙어 보임).
        // 맵 끝 출구 이름표가 잘리지 않는 것은 맵 양끝 여유 공간으로 해결. 완전히 화면 밖이면 그리지 않음
        float screenWidth = Screen.width / scale;
        if (box.xMax < 0f || box.x > screenWidth) return;

        DrawRect(box, background);
        GUI.Label(new Rect(box.x + padding, box.y + padding, bubbleWidth, textHeight), text, style);
    }

    public static void DrawRect(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    // 가운데 정렬·줄바꿈·리치 텍스트 라벨 스타일
    public static GUIStyle MakeStyle(int size, Color textColor)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = size,
            alignment = TextAnchor.UpperCenter,
            wordWrap = true,
            richText = true,
        };
        // 모든 상태에 같은 색 — normal만 바꾸면 마우스 커서가 글씨 위에 있을 때 기본 hover 색(흰색)으로 바뀜
        style.normal.textColor = textColor;
        style.hover.textColor = textColor;
        style.active.textColor = textColor;
        style.focused.textColor = textColor;
        style.onNormal.textColor = textColor;
        style.onHover.textColor = textColor;
        style.onActive.textColor = textColor;
        style.onFocused.textColor = textColor;
        return style;
    }
}
