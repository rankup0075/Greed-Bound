using UnityEngine;
using UnityEngine.UI;

// 정식 UI 공통 도구 (Spec 9장 "HUD — Unity 구현 규칙").
// 계층을 코드로 만들기 때문에 씬·프리팹을 고치지 않아도 되고, 도트 UI 스프라이트가 들어오면 Image의 sprite만 채우면 됨.
// 좌표계는 게임과 같은 640×360 — UI도 같은 도트 격자에 맞음. 위치는 왼쪽 위 기준(y는 음수로 내려감).
public static class PixelUi
{
    public const int RefWidth = 640, RefHeight = 360;

    public static readonly Color TextColor = new Color(0.92f, 0.92f, 0.95f);
    public static readonly Color Dark = new Color(0.08f, 0.06f, 0.1f, 0.85f);   // 게이지·아이콘·판 바탕

    private static Sprite whiteSprite;
    private static Font font;

    // 한글이 나오는 폰트를 우선 (도트 한글 폰트가 준비되면 여기만 바꾸면 됨)
    public static Font Font
    {
        get
        {
            if (font != null) return font;
            foreach (string name in new[] { "Malgun Gothic", "맑은 고딕", "Gulim", "Arial" })
            {
                Font osFont = Font.CreateDynamicFontFromOSFont(name, 16);
                if (osFont != null) return font = osFont;
            }
            return font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }

    // 게이지·판·부채꼴용 흰색 스프라이트 (도트 UI가 들어오면 Image.sprite만 교체)
    public static Sprite White()
    {
        if (whiteSprite != null) return whiteSprite;
        Texture2D texture = new Texture2D(4, 4) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
        Color[] pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        texture.SetPixels(pixels);
        texture.Apply();
        whiteSprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        return whiteSprite;
    }

    // 640×360 기준으로 늘어나는 오버레이 캔버스
    public static RectTransform CreateCanvas(Transform parent, string name, int sortingOrder)
    {
        GameObject canvasObject = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(parent, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
        scaler.matchWidthOrHeight = 1f;   // 16:9 고정이라 세로 기준

        return canvasObject.GetComponent<RectTransform>();
    }

    public static Image MakeImage(RectTransform parent, string name, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.sprite = White();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    public static Text Label(RectTransform parent, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment, bool bold = false)
    {
        GameObject textObject = new GameObject("Text", typeof(Text), typeof(Outline));
        textObject.transform.SetParent(parent, false);
        Anchor(textObject.GetComponent<RectTransform>(), position, size);

        Text text = textObject.GetComponent<Text>();
        text.font = Font;
        text.fontSize = fontSize;
        text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        text.alignment = alignment;
        text.color = TextColor;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        // 어두운 배경이 아니어도 읽히게 1px 외곽선 (도트 화면과도 어울림)
        Outline outline = textObject.GetComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1f, -1f);
        return text;
    }

    // 여러 줄로 접히는 글 (카드 설명처럼 칸을 넘치면 안 되는 곳)
    public static Text Wrapped(RectTransform parent, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
    {
        Text text = Label(parent, position, size, fontSize, alignment);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    // 왼쪽 위 기준 좌표 (y는 음수로 내려감)
    public static void Anchor(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    // 부모를 가득 채움 (margin만큼 안쪽으로)
    public static void Stretch(RectTransform rect, float margin = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(margin, margin);
        rect.offsetMax = new Vector2(-margin, -margin);
    }
}
