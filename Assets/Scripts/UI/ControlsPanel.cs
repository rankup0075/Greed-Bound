using UnityEngine;
using UnityEngine.UI;

// 조작법 화면 (Spec 9장 "튜토리얼·조작 안내 — Unity 구현 규칙").
// 타이틀(TitleScreen)과 일시정지(PauseMenu)가 같은 화면을 쓰도록 분리한 부분.
// 정식 UI(`PixelUi`, 640×360 좌표) — **한 번 만들고 켜고 끄기만 한다.**
//
// 내용은 Spec 5장 "키 배치"와 같은 순서 — 키가 늘거나 바뀌면 아래 표만 고치면 된다.
public class ControlsPanel : MonoBehaviour
{
    // 칸 나누기 — 전부 640×360 기준 (옛 OnGUI 판의 1080 기준 수치를 3으로 나눈 값)
    const float BlockWidth = 520f;          // 표 전체 폭
    const float ColumnWidth = 247f;         // 한 칸 폭
    const float KeyWidth = 63f;             // 키 이름 칸 (오른쪽 정렬) — "Left Shift"가 들어가는 폭
    const float Gap = 8f;                   // 키와 설명 사이
    const float RowHeight = 17f;
    const float GroupHeadHeight = 18f;      // 묶음 제목이 차지하는 높이
    const float GroupGap = 11f;             // 묶음 사이 여백
    const float GroupsY = 93f;              // 묶음이 시작하는 y
    const float RulesY = 284f;         // 왼쪽 칸(이동+전투)이 276 에서 끝난다 — 그 아래
    const float RuleStep = 13f;

    const int TitleSize = 21, GroupSize = 11, RowSize = 10, HintSize = 8;

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

    private RectTransform root;
    private Image background;

    // 주인(타이틀·일시정지) 밑에 매단다. 일시정지는 DontDestroyOnLoad 라 부모가 꼭 필요하다
    // — 루트로 두면 씬이 바뀔 때 캔버스만 파괴된다 (MenuUi 와 같은 이유)
    public static ControlsPanel Create(Transform parent, int sortingOrder)
    {
        RectTransform canvas = PixelUi.CreateCanvas(parent, "ControlsPanel", sortingOrder);
        ControlsPanel panel = canvas.gameObject.AddComponent<ControlsPanel>();
        panel.Build(canvas);
        panel.SetVisible(false);
        return panel;
    }

    void Build(RectTransform canvas)
    {
        root = canvas;
        background = PixelUi.MakeImage(root, "Background", new Color(0.06f, 0.05f, 0.09f));
        PixelUi.Stretch(background.rectTransform);

        PixelUi.Label(root, new Vector2(0f, -43f), new Vector2(PixelUi.RefWidth, 30f),
            TitleSize, TextAnchor.MiddleCenter, bold: true).text = "조작법";

        // 왼쪽 칸: 이동 + 전투 / 오른쪽 칸: 그 외
        float left = (PixelUi.RefWidth - BlockWidth) * 0.5f;
        float right = left + BlockWidth - ColumnWidth;

        float y = Group(left, GroupsY, "이동", MoveKeys);
        Group(left, y + GroupGap, "전투", CombatKeys);
        Group(right, GroupsY, "그 외", SystemKeys);

        for (int i = 0; i < Rules.Length; i++)
            PixelUi.Label(root, new Vector2(0f, -(RulesY + i * RuleStep)), new Vector2(PixelUi.RefWidth, RuleStep),
                HintSize, TextAnchor.MiddleCenter).text = Rules[i];

        PixelUi.Label(root, new Vector2(0f, -327f), new Vector2(PixelUi.RefWidth, RuleStep),
            HintSize, TextAnchor.MiddleCenter).text = "Enter 또는 ESC 로 돌아가기";
    }

    // 묶음 하나를 만들고 다음 y를 돌려줌
    float Group(float x, float y, string title, string[,] keys)
    {
        Text head = PixelUi.Label(root, new Vector2(x, -y), new Vector2(ColumnWidth, GroupHeadHeight),
            GroupSize, TextAnchor.MiddleLeft, bold: true);
        head.text = title;
        head.color = GroupColor;
        y += GroupHeadHeight;

        for (int i = 0; i < keys.GetLength(0); i++)
        {
            Text key = PixelUi.Label(root, new Vector2(x, -y), new Vector2(KeyWidth, RowHeight),
                RowSize, TextAnchor.MiddleRight);
            key.text = keys[i, 0];
            key.color = KeyColor;

            Text desc = PixelUi.Label(root, new Vector2(x + KeyWidth + Gap, -y),
                new Vector2(ColumnWidth - KeyWidth - Gap, RowHeight), RowSize, TextAnchor.MiddleLeft);
            desc.text = keys[i, 1];
            desc.color = DescColor;
            y += RowHeight;
        }
        return y;
    }

    // 타이틀은 불투명한 배경, 일시정지는 게임 화면이 살짝 비치는 어둡기
    public void SetBackground(Color color) => background.color = color;

    public void SetVisible(bool on)
    {
        if (root != null) root.gameObject.SetActive(on);
    }

    // ESC·Enter로 닫음. 닫아야 하면 false
    public bool HandleInput(bool cancel, bool submit) => !cancel && !submit;
}
