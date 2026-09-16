using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 라운드 전 카드 선택 → 적용 안내 → 탐욕 (Spec 1장, 3장 "선택 화면 흐름").
// RoundManager가 Begin()으로 열고, 끝나면 콜백으로 전투를 시작함.
// 화면은 임시 UI(OnGUI). 정식 UI로 바꿀 때 흐름 로직은 그대로 두고 그리는 부분만 교체.
[RequireComponent(typeof(RunState))]
public class CardSelection : MonoBehaviour
{
    enum Step { Hidden, Choose, Applied, Greed }

    [Header("연결")]
    public CardLibrary library;

    [Header("Spec 1장")]
    public int offerCount = 3;
    public float appliedScreenTime = 10f;
    public float greedTime = 30f;

    public bool IsOpen => step != Step.Hidden;

    private RunState run;
    private PlayerHealth player;
    private InputAction moveAction;
    private InputAction confirmAction;

    private Step step = Step.Hidden;
    private readonly List<CardData> offered = new List<CardData>();
    private int cursor;
    private float timer;
    private int openedFrame;
    private float previousMove;
    private Action onFinished;

    // 적용 안내 화면용
    private CardData chosen;
    private float riskBefore;
    private float multiplierBefore;

    // 임시 UI 스타일
    private GUIStyle titleStyle, nameStyle, bodyStyle, smallStyle, centerStyle;

    void Awake()
    {
        run = GetComponent<RunState>();
        moveAction = InputSystem.actions.FindAction("Player/Move", throwIfNotFound: true);
        confirmAction = InputSystem.actions.FindAction("Player/Interact", throwIfNotFound: true);
    }

    void Start()
    {
        player = FindFirstObjectByType<PlayerHealth>();
    }

    public void Begin(Action onFinished)
    {
        this.onFinished = onFinished;
        BuildOffer();

        if (offered.Count == 0)
        {
            Debug.LogWarning("CardSelection: 제시할 카드가 없습니다. (Card Library 연결·카드 데이터 생성 확인)", this);
            Finish();
            return;
        }

        step = Step.Choose;
        cursor = offered.Count / 2;
        openedFrame = Time.frameCount;  // 결과 화면을 넘긴 Enter가 카드 선택까지 눌러버리지 않게
        previousMove = moveAction.ReadValue<float>();
    }

    // 구현된 카드 중에서 서로 다른 카드 offerCount장
    void BuildOffer()
    {
        offered.Clear();
        if (library == null) return;

        List<CardData> pool = new List<CardData>();
        foreach (CardData card in library.cards)
        {
            if (card != null && CardEffects.IsImplemented(card.effect)) pool.Add(card);
        }

        while (offered.Count < offerCount && pool.Count > 0)
        {
            int index = UnityEngine.Random.Range(0, pool.Count);
            offered.Add(pool[index]);
            pool.RemoveAt(index);
        }
    }

    void Update()
    {
        if (step == Step.Hidden) return;

        int navigation = ReadNavigation();
        bool confirm = Time.frameCount != openedFrame && confirmAction.WasPressedThisFrame();

        switch (step)
        {
            case Step.Choose:
                cursor = Wrap(cursor + navigation, offered.Count);
                if (confirm)
                {
                    chosen = offered[cursor];
                    riskBefore = run.Risk;
                    multiplierBefore = run.RewardMultiplier;
                    run.AddCard(chosen, false, player);
                    offered.RemoveAt(cursor);

                    step = Step.Applied;
                    timer = appliedScreenTime;
                }
                break;

            case Step.Applied:
                timer -= Time.deltaTime;
                if (confirm || timer <= 0f)
                {
                    if (offered.Count == 0)
                    {
                        Finish();
                        break;
                    }
                    step = Step.Greed;
                    cursor = offered.Count;  // 기본 선택은 "그만한다" — 실수로 탐욕을 고르지 않게
                    timer = greedTime;
                }
                break;

            case Step.Greed:
                cursor = Wrap(cursor + navigation, offered.Count + 1);
                timer -= Time.deltaTime;
                if (confirm)
                {
                    if (cursor < offered.Count) TakeGreed(offered[cursor]);
                    Finish();
                }
                else if (timer <= 0f)
                {
                    Finish();  // 시간 초과 = 그만한다
                }
                break;
        }
    }

    void TakeGreed(CardData card)
    {
        run.AddCard(card, true, player);
        if (RoundManager.Instance != null)
            RoundManager.Instance.ShowMessage($"탐욕: {card.displayName} (위험도·보상 ×{RunState.GreedMultiplier})");
    }

    void Finish()
    {
        step = Step.Hidden;
        offered.Clear();
        Action callback = onFinished;
        onFinished = null;
        callback?.Invoke();
    }

    // ← → 를 누른 순간만 한 칸 이동 (누르고 있어도 계속 넘어가지 않게)
    int ReadNavigation()
    {
        float value = moveAction.ReadValue<float>();
        int result = 0;
        if (value < -0.5f && previousMove >= -0.5f) result = -1;
        else if (value > 0.5f && previousMove <= 0.5f) result = 1;
        previousMove = value;
        return result;
    }

    static int Wrap(int value, int count)
    {
        if (count <= 0) return 0;
        return ((value % count) + count) % count;
    }

    // ───────────── 임시 UI ─────────────

    const float VirtualHeight = 720f;
    const float CardWidth = 250f;
    const float CardHeight = 330f;
    const float CardGap = 28f;

    void OnGUI()
    {
        if (step == Step.Hidden) return;
        EnsureStyles();

        // 화면 높이 720 기준으로 크기를 맞춤
        float scale = Screen.height / VirtualHeight;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float width = Screen.width / scale;

        DrawRect(new Rect(0, 0, width, VirtualHeight), new Color(0f, 0f, 0f, 0.7f));

        switch (step)
        {
            case Step.Choose: DrawChoose(width); break;
            case Step.Applied: DrawApplied(width); break;
            case Step.Greed: DrawGreed(width); break;
        }

        GUI.matrix = Matrix4x4.identity;
    }

    void DrawChoose(float width)
    {
        GUI.Label(new Rect(0, 60, width, 50), $"라운드 {run.round} — 적에게 걸 강화를 고르세요", titleStyle);
        DrawCardRow(width, offered, 1f, includeStop: false);
        GUI.Label(new Rect(0, 620, width, 40), "← → 선택     Enter 결정", centerStyle);
    }

    void DrawApplied(float width)
    {
        Rect box = new Rect(width * 0.5f - 300, 150, 600, 400);
        DrawRect(box, new Color(0.12f, 0.1f, 0.14f, 0.95f));
        DrawRect(new Rect(box.x, box.y, box.width, 6), CardData.CategoryColor(chosen.category));

        float y = box.y + 30;
        GUI.Label(new Rect(box.x, y, box.width, 30), $"카드 적용 — {CardData.CategoryName(chosen.category)}", centerStyle); y += 40;
        GUI.Label(new Rect(box.x, y, box.width, 50), chosen.displayName, titleStyle); y += 60;
        GUI.Label(new Rect(box.x + 40, y, box.width - 80, 70), chosen.description, bodyStyle); y += 90;

        string overload = riskBefore < RunState.OverloadRisk && run.IsOverloaded ? "   돌파!" : "";
        GUI.Label(new Rect(box.x, y, box.width, 30), $"위험도  {riskBefore:F0} → {run.Risk:F0}{overload}", centerStyle); y += 36;
        GUI.Label(new Rect(box.x, y, box.width, 30), $"보상 배수  x{multiplierBefore:F2} → x{run.RewardMultiplier:F2}", centerStyle);

        GUI.Label(new Rect(box.x, box.yMax - 50, box.width, 30), $"{Mathf.CeilToInt(timer)}초 뒤 자동 진행     Enter 넘기기", smallStyle);
    }

    void DrawGreed(float width)
    {
        GUI.Label(new Rect(0, 50, width, 50), "탐욕 — 한 장 더 가져가시겠습니까?", titleStyle);
        GUI.Label(new Rect(0, 100, width, 30),
            $"탐욕으로 가져간 카드는 위험도·보상 ×{RunState.GreedMultiplier}, 시너지 집계 제외     남은 시간 {Mathf.CeilToInt(timer)}초",
            centerStyle);
        DrawCardRow(width, offered, RunState.GreedMultiplier, includeStop: true);
        GUI.Label(new Rect(0, 620, width, 40), "← → 선택     Enter 결정", centerStyle);
    }

    // 카드 여러 장 가로 배치. includeStop이면 맨 오른쪽에 "그만한다"
    void DrawCardRow(float width, List<CardData> cards, float multiplier, bool includeStop)
    {
        int count = cards.Count + (includeStop ? 1 : 0);
        float totalWidth = count * CardWidth + (count - 1) * CardGap;
        float x = width * 0.5f - totalWidth * 0.5f;
        float y = 170f;

        for (int i = 0; i < count; i++)
        {
            Rect rect = new Rect(x + i * (CardWidth + CardGap), y, CardWidth, CardHeight);
            bool selected = i == cursor;
            if (i < cards.Count) DrawCard(rect, cards[i], multiplier, selected);
            else DrawStop(rect, selected);
        }
    }

    void DrawCard(Rect rect, CardData card, float multiplier, bool selected)
    {
        Color categoryColor = CardData.CategoryColor(card.category);
        if (selected)
        {
            rect.y -= 14;  // 선택된 카드는 살짝 위로
            DrawRect(new Rect(rect.x - 5, rect.y - 5, rect.width + 10, rect.height + 10), Color.white);
        }
        DrawRect(rect, new Color(0.14f, 0.12f, 0.16f, 1f));
        DrawRect(new Rect(rect.x, rect.y, rect.width, 8), categoryColor);

        float y = rect.y + 22;
        Color previous = GUI.contentColor;
        GUI.contentColor = categoryColor;
        GUI.Label(new Rect(rect.x, y, rect.width, 26), CardData.CategoryName(card.category), centerStyle);
        GUI.contentColor = previous;
        y += 34;

        GUI.Label(new Rect(rect.x, y, rect.width, 40), card.displayName, nameStyle); y += 56;
        GUI.Label(new Rect(rect.x, y, rect.width, 28), $"위험도 +{card.risk * multiplier:0.#}", centerStyle); y += 30;
        GUI.Label(new Rect(rect.x, y, rect.width, 28), $"보상 +{card.reward * multiplier:0.#}%", centerStyle); y += 44;
        GUI.Label(new Rect(rect.x + 16, y, rect.width - 32, rect.yMax - y - 12), card.description, bodyStyle);
    }

    void DrawStop(Rect rect, bool selected)
    {
        if (selected)
        {
            rect.y -= 14;
            DrawRect(new Rect(rect.x - 5, rect.y - 5, rect.width + 10, rect.height + 10), Color.white);
        }
        DrawRect(rect, new Color(0.2f, 0.2f, 0.22f, 1f));
        GUI.Label(new Rect(rect.x, rect.y + rect.height * 0.5f - 40, rect.width, 40), "그만한다", nameStyle);
        GUI.Label(new Rect(rect.x, rect.y + rect.height * 0.5f + 10, rect.width, 30), "이대로 전투 시작", centerStyle);
    }

    static void DrawRect(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    void EnsureStyles()
    {
        if (titleStyle != null) return;

        titleStyle = MakeStyle(34, TextAnchor.MiddleCenter, FontStyle.Bold);
        nameStyle = MakeStyle(30, TextAnchor.MiddleCenter, FontStyle.Bold);
        centerStyle = MakeStyle(20, TextAnchor.MiddleCenter, FontStyle.Normal);
        smallStyle = MakeStyle(17, TextAnchor.MiddleCenter, FontStyle.Normal);
        bodyStyle = MakeStyle(19, TextAnchor.UpperCenter, FontStyle.Normal);
        bodyStyle.wordWrap = true;
    }

    static GUIStyle MakeStyle(int size, TextAnchor anchor, FontStyle fontStyle)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = anchor, fontStyle = fontStyle };
        style.normal.textColor = Color.white;
        return style;
    }
}
