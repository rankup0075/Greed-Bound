using UnityEngine;

// 타이틀·일시정지·설정 화면이 함께 쓰는 임시 UI 그리기 도구 (OnGUI).
// 1080p 기준 크기로 그린 뒤 화면 높이에 맞춰 확대·축소하므로 해상도가 달라도 같은 비율로 보임.
public static class MenuGui
{
    public static GUIStyle TitleStyle { get; private set; }
    public static GUIStyle SubtitleStyle { get; private set; }
    public static GUIStyle ItemStyle { get; private set; }
    public static GUIStyle HintStyle { get; private set; }
    public static Texture2D DimTexture { get; private set; }

    static readonly Color SelectedColor = new Color(1f, 0.85f, 0.35f);
    static readonly Color NormalColor = new Color(0.85f, 0.85f, 0.85f);

    public static void Ensure()
    {
        if (TitleStyle != null && DimTexture != null) return;

        TitleStyle = Style(64, FontStyle.Bold);
        SubtitleStyle = Style(26, FontStyle.Normal);
        ItemStyle = Style(36, FontStyle.Normal);
        HintStyle = Style(24, FontStyle.Normal);

        DimTexture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        DimTexture.SetPixel(0, 0, Color.white);
        DimTexture.Apply();
    }

    // 마우스 커서가 글씨 위에 있어도 색이 바뀌지 않게 모든 상태에 같은 색 (WorldGui와 같은 이유)
    static GUIStyle Style(int fontSize, FontStyle fontStyle)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            fontStyle = fontStyle,
            alignment = TextAnchor.MiddleCenter,
        };
        foreach (GUIStyleState state in new[] { style.normal, style.hover, style.active, style.focused, style.onNormal, style.onHover })
            state.textColor = Color.white;
        return style;
    }

    // 1080p 기준으로 그리기 시작. 돌려받은 폭으로 Rect를 잡고, 끝나면 End로 되돌림
    public static float Begin(out Matrix4x4 previous)
    {
        Ensure();
        float scale = Screen.height / 1080f;
        previous = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        return Screen.width / scale;
    }

    public static void End(Matrix4x4 previous) => GUI.matrix = previous;

    public static void Fill(float width, float height, Color color)
    {
        Color old = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(0, 0, width, height), DimTexture);
        GUI.color = old;
    }

    public static void DrawItem(float width, float y, bool selected, string text)
    {
        Color old = GUI.color;
        GUI.color = selected ? SelectedColor : NormalColor;
        GUI.Label(new Rect(0, y, width, 60f), (selected ? "▶  " : "") + text, ItemStyle);
        GUI.color = old;
    }
}
