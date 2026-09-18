using UnityEngine;

// 조작법 화면 (Spec 9장 "튜토리얼·조작 안내 — Unity 구현 규칙").
// 타이틀(TitleScreen)과 일시정지(PauseMenu)가 같은 화면을 쓰도록 분리한 부분. 임시 UI(OnGUI, MenuGui 1080p 좌표).
// 내용은 Spec 5장 "키 배치"와 같은 순서 — 키가 늘거나 바뀌면 아래 표만 고치면 된다.
public class ControlsPanel
{
    const float KeyWidth = 190f;     // 키 이름 칸 (오른쪽 정렬) — "Left Shift"가 들어가는 폭
    const float Gap = 24f;           // 키와 설명 사이
    const float RowHeight = 50f;
    const float GroupGap = 34f;      // 묶음 사이 여백

    static readonly Color GroupColor = new Color(1f, 0.85f, 0.35f);
    static readonly Color KeyColor = new Color(1f, 1f, 1f);
    static readonly Color DescColor = new Color(0.82f, 0.82f, 0.85f);

    // { 키, 설명 }
    static readonly string[,] MoveKeys =
    {
        { "← →", "이동" },
        { "C", "점프" },
        { "↓ + C", "발판 뚫고 내려가기" },
        { "Left Shift", "대쉬 — 3u 돌진 (무적 아님)" },
    };

    static readonly string[,] CombatKeys =
    {
        { "Z", "근접 공격 — 3연타" },
        { "X", "단검 투척 — 라운드마다 보충" },
        { "A", "스킬 — 로비에서 고른 1개" },
        { "1", "체력 물약" },
    };

    static readonly string[,] SystemKeys =
    {
        { "Enter", "상호작용 · 확인" },
        { "Tab", "소지품 창 (상점)" },
        { "R", "카드 리롤 · 상점 진열 갱신" },
        { "ESC", "일시정지 · 설정" },
        { "M", "음소거" },
    };

    // 키만으로는 알 수 없는 규칙 — 처음 잡는 사람이 가장 먼저 막히는 부분
    static readonly string[] Rules =
    {
        "카드는 적을 강화합니다 — 위험도가 오를수록 골드 보상 배수도 함께 오릅니다",
        "위험도 50마다 유물을 받고, 3라운드마다 상점 · 5라운드마다 보스",
        "적의 공격에는 항상 예고가 있습니다 — 붉게 멈추면 사거리 밖으로",
    };

    static GUIStyle keyStyle, descStyle, groupStyle;

    static void Ensure()
    {
        if (keyStyle != null) return;
        MenuGui.Ensure();
        keyStyle = new GUIStyle(MenuGui.ItemStyle) { fontSize = 30, alignment = TextAnchor.MiddleRight };
        descStyle = new GUIStyle(MenuGui.ItemStyle) { fontSize = 30, alignment = TextAnchor.MiddleLeft };
        groupStyle = new GUIStyle(MenuGui.ItemStyle) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
    }

    // ESC·Enter로 닫음. 닫아야 하면 false
    public bool HandleInput(bool cancel, bool submit) => !cancel && !submit;

    public void Draw(float width)
    {
        Ensure();
        GUI.Label(new Rect(0, 130f, width, 90f), "조작법", MenuGui.TitleStyle);

        // 왼쪽 칸: 이동 + 전투 / 오른쪽 칸: 그 외
        float blockWidth = Mathf.Min(width - 160f, 1560f);
        float left = (width - blockWidth) * 0.5f;
        float columnWidth = blockWidth * 0.5f - 40f;
        float right = left + blockWidth * 0.5f + 40f;

        float y = Group(left, 280f, columnWidth, "이동", MoveKeys);
        Group(left, y + GroupGap, columnWidth, "전투", CombatKeys);
        Group(right, 280f, columnWidth, "그 외", SystemKeys);

        for (int i = 0; i < Rules.Length; i++)
            GUI.Label(new Rect(0, 830f + i * 40f, width, 40f), Rules[i], MenuGui.HintStyle);

        GUI.Label(new Rect(0, 980f, width, 40f), "Enter 또는 ESC 로 돌아가기", MenuGui.HintStyle);
    }

    // 묶음 하나를 그리고 다음 y를 돌려줌
    static float Group(float x, float y, float width, string title, string[,] keys)
    {
        Color old = GUI.color;

        GUI.color = GroupColor;
        GUI.Label(new Rect(x, y, width, 44f), title, groupStyle);
        y += 54f;

        for (int i = 0; i < keys.GetLength(0); i++)
        {
            GUI.color = KeyColor;
            GUI.Label(new Rect(x, y, KeyWidth, RowHeight), keys[i, 0], keyStyle);
            GUI.color = DescColor;
            GUI.Label(new Rect(x + KeyWidth + Gap, y, width - KeyWidth - Gap, RowHeight), keys[i, 1], descStyle);
            y += RowHeight;
        }

        GUI.color = old;
        return y;
    }
}
