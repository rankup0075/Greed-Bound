using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 타이틀·설정·일시정지가 함께 쓰는 정식 메뉴 UI (Spec 9장 "HUD — Unity 구현 규칙").
// `MenuGui`(OnGUI) 를 대신한다. OnGUI 는 매 프레임 다시 그리지만 이쪽은 **한 번 만들고 값만 바꾼다**.
//
// 좌표는 게임과 같은 640×360. `MenuGui` 는 1080 기준이었으므로 그쪽 수치를 옮길 때는 3으로 나눈다.
// 글자 크기도 같은 비율이라 화면에 보이는 크기는 전과 같다.
public class MenuUi : MonoBehaviour
{
    public static readonly Color Selected = new Color(1f, 0.85f, 0.35f);
    public static readonly Color Normal = new Color(0.85f, 0.85f, 0.85f);

    const int TitleSize = 21, SubtitleSize = 9, ItemSize = 12, HintSize = 8;
    const float ItemSpacing = 22f, ItemHeight = 20f;

    private RectTransform root;
    private Image background;
    private Text title, subtitle, hint, record, message;
    private readonly List<Text> items = new List<Text>();
    private float itemsY = 157f;

    // parent 를 주면 그 아래에 매달린다. **주인이 DontDestroyOnLoad 면 반드시 넘겨야 한다** —
    // 루트로 두면 씬이 바뀔 때 캔버스만 파괴돼 메뉴가 통째로 안 보인다 (2026-09-20에 실제로 겪음)
    public static MenuUi Create(string name, int sortingOrder, Transform parent = null)
    {
        RectTransform canvas = PixelUi.CreateCanvas(parent, name, sortingOrder);
        MenuUi menu = canvas.gameObject.AddComponent<MenuUi>();
        menu.Build(canvas);
        return menu;
    }

    void Build(RectTransform canvas)
    {
        root = canvas;
        background = PixelUi.MakeImage(root, "Background", new Color(0.06f, 0.05f, 0.09f));
        PixelUi.Stretch(background.rectTransform);

        title = PixelUi.Label(root, new Vector2(0f, -70f), new Vector2(PixelUi.RefWidth, 33f), TitleSize, TextAnchor.MiddleCenter, bold: true);
        subtitle = PixelUi.Label(root, new Vector2(0f, -103f), new Vector2(PixelUi.RefWidth, 13f), SubtitleSize, TextAnchor.MiddleCenter);
        hint = PixelUi.Label(root, new Vector2(0f, -273f), new Vector2(PixelUi.RefWidth, 13f), HintSize, TextAnchor.MiddleCenter);
        record = PixelUi.Label(root, new Vector2(0f, -300f), new Vector2(PixelUi.RefWidth, 13f), HintSize, TextAnchor.MiddleCenter);
        message = PixelUi.Label(root, new Vector2(0f, -333f), new Vector2(PixelUi.RefWidth, 13f), HintSize, TextAnchor.MiddleCenter);
    }

    public void SetBackground(Color color) => background.color = color;

    public void SetTitle(string text, string sub = "")
    {
        title.text = text;
        subtitle.text = sub;
    }

    // 항목 줄은 개수가 바뀔 때만 다시 만든다 (매 프레임 만들면 쓰레기가 쌓인다)
    public void SetItems(IReadOnlyList<string> texts, int cursor, float startY)
    {
        if (!Mathf.Approximately(itemsY, startY))
        {
            itemsY = startY;
            for (int i = 0; i < items.Count; i++)
                PixelUi.Anchor(items[i].rectTransform, new Vector2(0f, -(itemsY + i * ItemSpacing)), new Vector2(PixelUi.RefWidth, ItemHeight));
        }

        while (items.Count < texts.Count)
        {
            int index = items.Count;
            items.Add(PixelUi.Label(root, new Vector2(0f, -(itemsY + index * ItemSpacing)),
                new Vector2(PixelUi.RefWidth, ItemHeight), ItemSize, TextAnchor.MiddleCenter));
        }
        for (int i = 0; i < items.Count; i++)
        {
            bool used = i < texts.Count;
            items[i].gameObject.SetActive(used);
            if (!used) continue;
            bool selected = i == cursor;
            items[i].text = (selected ? "▶  " : "") + texts[i];
            items[i].color = selected ? Selected : Normal;
        }
    }

    public void SetHint(string text) => hint.text = text ?? "";
    public void SetRecord(string text) => record.text = text ?? "";
    public void SetMessage(string text) => message.text = text ?? "";

    public void SetVisible(bool on)
    {
        if (root != null) root.gameObject.SetActive(on);
    }
}
