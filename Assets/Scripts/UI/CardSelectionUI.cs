using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 카드 선택 · 적용 안내 · 탐욕 화면 (Spec 1장·3장 "선택 화면 흐름", Spec 9장 "카드 선택 화면 — Unity 구현 규칙").
// 흐름·상태는 CardSelection이 가지고 이 파일은 그리기만 함 — CardSelection이 실행 시 스스로 붙이므로 씬 수정은 필요 없음.
// 좌표계는 게임과 같은 640×360. 도트 카드 그림이 생기면 CardView.art의 sprite만 채우면 됨.
[RequireComponent(typeof(CardSelection))]
public class CardSelectionUI : MonoBehaviour
{
    const int MaxSlots = 6;           // 넓은 시야 4장 + "그만한다" + 여유
    const float CardW = 118f;
    const float CardH = 208f;
    const float Gap = 10f;
    const float RowTop = 62f;         // 카드 윗변 y (화면 위에서)
    const float RaiseSelected = 7f;   // 선택된 카드를 살짝 위로

    // 글씨 크기 — 카드의 주인공은 "무슨 효과인가"(설명), 위험도·보상은 거들기만 함
    const int TitleFont = 14;
    const int CardNameFont = 13;
    const int EffectFont = 11;
    const int TextFont = 9;
    const int SmallFont = 8;

    static readonly Color Overlay = new Color(0f, 0f, 0f, 0.72f);
    static readonly Color CardBack = new Color(0.14f, 0.12f, 0.16f, 1f);
    static readonly Color StopBack = new Color(0.2f, 0.2f, 0.22f, 1f);
    static readonly Color PanelBack = new Color(0.12f, 0.1f, 0.14f, 0.97f);
    static readonly Color SectionBack = new Color(1f, 1f, 1f, 0.07f);       // 적용 안내의 칸 바탕
    static readonly Color SynergyColor = new Color(1f, 0.85f, 0.35f);       // 시너지 — 금색
    static readonly Color ComboColor = new Color(1f, 0.62f, 0.32f);         // 조합 — 주황
    static readonly Color RelicColor = new Color(0.55f, 0.88f, 1f);         // 유물 — 하늘색
    static readonly Color MutedColor = new Color(0.70f, 0.68f, 0.74f);      // 위험도·보상 같은 곁들이 수치
    static readonly Color DividerColor = new Color(1f, 1f, 1f, 0.16f);

    // 카드 한 장 (또는 "그만한다" 칸)
    class CardView
    {
        public RectTransform root;
        public Image border;          // 선택 테두리
        public Image body;
        public Image topBar;          // 계열 색 띠
        public Image art;             // 도트 카드 그림 자리 (스프라이트가 없으면 꺼 둠)
        public Image divider;
        public Text category, name, effect, combo, risk, reward;
        public Text stopName, stopDescription;
    }

    // 적용 안내의 칸 하나 — 왼쪽 색 막대 + (제목) + 내용. 높이는 내용 줄 수에 맞춰 늘어남
    class Section
    {
        public RectTransform root;
        public Image background;
        public Image accent;
        public Text title;
        public Text body;
    }

    private CardSelection selection;
    private RectTransform root;
    private RectTransform panel;      // 적용 안내 판
    private Text header, subHeader, hint;
    private readonly CardView[] slots = new CardView[MaxSlots];

    private Image panelTopBar;
    private Text panelKicker, panelName, panelFooter;
    private Section[] sections;

    void Awake()
    {
        selection = GetComponent<CardSelection>();
        Build();
        root.gameObject.SetActive(false);
    }

    // ───────────── 계층 만들기 ─────────────

    void Build()
    {
        root = PixelUi.CreateCanvas(transform, "Card Selection Canvas", 200);   // HUD(100)보다 위

        Image dim = PixelUi.MakeImage(root, "Dim", Overlay);
        PixelUi.Stretch(dim.rectTransform);

        header = PixelUi.Label(root, new Vector2(0, -18), new Vector2(PixelUi.RefWidth, 20), TitleFont, TextAnchor.MiddleCenter, bold: true);
        subHeader = PixelUi.Label(root, new Vector2(0, -40), new Vector2(PixelUi.RefWidth, 14), TextFont, TextAnchor.MiddleCenter);
        hint = PixelUi.Label(root, new Vector2(0, -(RowTop + CardH + 10)), new Vector2(PixelUi.RefWidth, 14), TextFont, TextAnchor.MiddleCenter);

        for (int i = 0; i < MaxSlots; i++) slots[i] = MakeCard();
        BuildPanel();
    }

    // 위에서부터 계열 → 이름 → **효과 설명(가운데·크게)** → 조합 → 구분선 → 위험도·보상(아래·작게)
    CardView MakeCard()
    {
        CardView view = new CardView();

        GameObject slot = new GameObject("Card", typeof(RectTransform));
        slot.transform.SetParent(root, false);
        view.root = slot.GetComponent<RectTransform>();
        PixelUi.Anchor(view.root, Vector2.zero, new Vector2(CardW, CardH));

        view.border = PixelUi.MakeImage(view.root, "Border", Color.white);
        PixelUi.Stretch(view.border.rectTransform, -2f);

        view.body = PixelUi.MakeImage(view.root, "Body", CardBack);
        PixelUi.Stretch(view.body.rectTransform);

        RectTransform body = view.body.rectTransform;

        view.topBar = PixelUi.MakeImage(body, "CategoryBar", Color.white);
        SpanWidth(view.topBar.rectTransform, 0f, 3f, 0f);

        // 도트 카드 그림 자리 — 스프라이트가 들어오기 전까지는 꺼 둠 (그림이 생기면 효과 설명 위로 들어감)
        view.art = PixelUi.MakeImage(body, "Art", Color.white);
        SpanWidth(view.art.rectTransform, 48f, 46f, 9f);
        view.art.enabled = false;

        view.category = Stretched(body, -5, 10, SmallFont, TextAnchor.MiddleCenter, false);
        view.name = Stretched(body, -16, 30, CardNameFont, TextAnchor.UpperCenter, true, 4);
        view.effect = Stretched(body, -50, 100, EffectFont, TextAnchor.UpperCenter, true, 6);
        view.combo = Stretched(body, -152, 22, TextFont, TextAnchor.UpperCenter, true, 3);
        view.combo.color = SynergyColor;

        view.divider = PixelUi.MakeImage(body, "Divider", DividerColor);
        SpanWidth(view.divider.rectTransform, 172f, 1f, 10f);

        view.risk = Stretched(body, -176, 13, TextFont, TextAnchor.MiddleCenter, false);
        view.risk.color = MutedColor;
        view.reward = Stretched(body, -189, 13, TextFont, TextAnchor.MiddleCenter, false);
        view.reward.color = MutedColor;

        view.stopName = Stretched(body, -(CardH * 0.5f - 22), 20, CardNameFont, TextAnchor.MiddleCenter, false);
        view.stopName.fontStyle = FontStyle.Bold;
        view.stopDescription = Stretched(body, -(CardH * 0.5f + 6), 14, TextFont, TextAnchor.MiddleCenter, false);

        slot.SetActive(false);
        return view;
    }

    // 부모 폭이 줄어도 같이 줄어드는 가로 띠 (y는 부모 위에서부터의 거리)
    static void SpanWidth(RectTransform rect, float y, float height, float sideMargin)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(sideMargin, -(y + height));
        rect.offsetMax = new Vector2(-sideMargin, -y);
    }

    // 부모 폭이 줄어도 같이 줄어드는 글줄
    Text Stretched(RectTransform parent, float y, float height, int fontSize, TextAnchor alignment, bool wrap, float sideMargin = 0f)
    {
        Text text = wrap
            ? PixelUi.Wrapped(parent, Vector2.zero, Vector2.zero, fontSize, alignment)
            : PixelUi.Label(parent, Vector2.zero, Vector2.zero, fontSize, alignment);

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(sideMargin, y - height);
        rect.offsetMax = new Vector2(-sideMargin, y);
        return text;
    }

    // ───────────── 적용 안내 판 ─────────────
    // 항목마다 칸(왼쪽 색 막대 + 제목 + 내용)으로 나눠 무엇이 붙었는지 한눈에 구분되게 함.
    // 칸 높이·판 높이는 내용 줄 수에 맞춰 정해지고, 판은 화면 세로 가운데에 놓임.

    const float PanelWidth = 440f;
    const float SidePad = 16f;        // 판 좌우 여백
    const float SectionGap = 5f;      // 칸 사이 간격
    const float SectionPad = 6f;      // 칸 안쪽 위아래 여백
    const float SectionTextLeft = 10f;
    const int MaxSections = 6;

    void BuildPanel()
    {
        Image background = PixelUi.MakeImage(root, "Applied", PanelBack);
        panel = background.rectTransform;
        PixelUi.Anchor(panel, new Vector2((PixelUi.RefWidth - PanelWidth) * 0.5f, -50), new Vector2(PanelWidth, 240));

        panelTopBar = PixelUi.MakeImage(panel, "CategoryBar", Color.white);
        SpanWidth(panelTopBar.rectTransform, 0f, 3f, 0f);

        panelKicker = Stretched(panel, -9, 12, TextFont, TextAnchor.MiddleCenter, false);
        panelKicker.color = MutedColor;
        panelName = Stretched(panel, -23, 26, 18, TextAnchor.UpperCenter, true, 20);

        sections = new Section[MaxSections];
        for (int i = 0; i < MaxSections; i++) sections[i] = MakeSection();

        panelFooter = Stretched(panel, -220, 12, SmallFont, TextAnchor.MiddleCenter, false);
        panelFooter.color = MutedColor;

        panel.gameObject.SetActive(false);
    }

    Section MakeSection()
    {
        Section section = new Section();

        Image background = PixelUi.MakeImage(panel, "Section", SectionBack);
        section.root = background.rectTransform;
        section.background = background;
        PixelUi.Anchor(section.root, new Vector2(SidePad, 0), new Vector2(PanelWidth - SidePad * 2f, 40));

        // 왼쪽 색 막대 — 칸의 성격(시너지·조합·유물·수치)을 색으로 구분
        section.accent = PixelUi.MakeImage(section.root, "Accent", Color.white);
        RectTransform accent = section.accent.rectTransform;
        accent.anchorMin = new Vector2(0f, 0f);
        accent.anchorMax = new Vector2(0f, 1f);
        accent.pivot = new Vector2(0f, 0.5f);
        accent.offsetMin = Vector2.zero;
        accent.offsetMax = new Vector2(3f, 0f);

        section.title = Stretched(section.root, -SectionPad, 12, 10, TextAnchor.UpperCenter, false, SectionTextLeft);
        section.body = Stretched(section.root, -20, 14, 10, TextAnchor.UpperCenter, true, SectionTextLeft);

        section.root.gameObject.SetActive(false);
        return section;
    }

    // ───────────── 갱신 ─────────────

    void LateUpdate()
    {
        CardSelection.Step step = selection.CurrentStep;
        bool open = step != CardSelection.Step.Hidden;
        if (root.gameObject.activeSelf != open) root.gameObject.SetActive(open);
        if (!open) return;

        bool applied = step == CardSelection.Step.Applied;
        panel.gameObject.SetActive(applied);
        header.gameObject.SetActive(!applied);
        subHeader.gameObject.SetActive(!applied);
        hint.gameObject.SetActive(!applied);

        if (applied)
        {
            for (int i = 0; i < slots.Length; i++) slots[i].root.gameObject.SetActive(false);
            UpdatePanel();
            return;
        }

        RunState run = RunState.Instance;
        bool greed = step == CardSelection.Step.Greed;

        header.text = greed
            ? "탐욕 — 한 장 더 가져가시겠습니까?"
            : $"라운드 {(run != null ? run.round : 1)} — 적에게 걸 강화를 고르세요";
        subHeader.text = greed
            ? $"탐욕으로 가져간 카드는 위험도·보상 ×{RunState.GreedMultiplier}, 시너지 집계 제외     남은 시간 {Mathf.CeilToInt(selection.Timer)}초"
            : "";
        hint.text = "← → 선택     Enter 결정" + selection.RerollHint();

        UpdateRow(greed);
    }

    // 카드 여러 장 가로 배치. 탐욕 단계면 맨 오른쪽에 "그만한다"
    void UpdateRow(bool includeStop)
    {
        IReadOnlyList<CardData> cards = selection.Offered;
        int count = Mathf.Min(MaxSlots, cards.Count + (includeStop ? 1 : 0));

        // 넓은 시야(4장 + 그만한다)처럼 화면에 다 안 들어가면 카드 폭을 줄임
        float cardWidth = Mathf.Min(CardW, (PixelUi.RefWidth - 24f - (count - 1) * Gap) / Mathf.Max(1, count));
        float totalWidth = count * cardWidth + (count - 1) * Gap;
        float x = (PixelUi.RefWidth - totalWidth) * 0.5f;

        for (int i = 0; i < MaxSlots; i++)
        {
            CardView view = slots[i];
            if (i >= count)
            {
                view.root.gameObject.SetActive(false);
                continue;
            }

            bool selected = i == selection.Cursor;
            view.root.gameObject.SetActive(true);
            view.root.sizeDelta = new Vector2(cardWidth, CardH);
            view.root.anchoredPosition = new Vector2(x + i * (cardWidth + Gap), -RowTop + (selected ? RaiseSelected : 0f));
            view.border.gameObject.SetActive(selected);

            if (i < cards.Count) FillCard(view, cards[i], selection.StepMultiplier);
            else FillStop(view);
        }
    }

    void FillCard(CardView view, CardData card, float multiplier)
    {
        Color categoryColor = CardData.CategoryColor(card.category);
        view.body.color = CardBack;
        view.topBar.color = categoryColor;
        view.topBar.gameObject.SetActive(true);
        view.divider.gameObject.SetActive(true);

        view.category.gameObject.SetActive(true);
        view.category.text = CardData.CategoryName(card.category);
        view.category.color = categoryColor;

        view.name.gameObject.SetActive(true);
        view.name.text = card.displayName;

        view.effect.gameObject.SetActive(true);
        view.effect.text = card.description;

        view.risk.gameObject.SetActive(true);
        view.risk.text = $"위험도 +{RunState.RiskFor(card, multiplier)}";

        view.reward.gameObject.SetActive(true);
        view.reward.text = $"보상 +{card.reward * multiplier:0.#}%";

        // 이 카드로 완성되는 조합 시너지
        List<CardCombo.Combo> combos = CardCombo.Completing(RunState.Instance, card.effect);
        view.combo.gameObject.SetActive(combos.Count > 0);
        if (combos.Count > 0)
        {
            List<string> names = new List<string>();
            foreach (CardCombo.Combo combo in combos) names.Add(combo.name);
            view.combo.text = $"조합 완성: {string.Join(", ", names)}";
        }

        view.stopName.gameObject.SetActive(false);
        view.stopDescription.gameObject.SetActive(false);
    }

    void FillStop(CardView view)
    {
        view.body.color = StopBack;
        view.topBar.gameObject.SetActive(false);
        view.divider.gameObject.SetActive(false);
        view.category.gameObject.SetActive(false);
        view.name.gameObject.SetActive(false);
        view.effect.gameObject.SetActive(false);
        view.combo.gameObject.SetActive(false);
        view.risk.gameObject.SetActive(false);
        view.reward.gameObject.SetActive(false);

        view.stopName.gameObject.SetActive(true);
        view.stopName.text = "그만한다";
        view.stopDescription.gameObject.SetActive(true);
        view.stopDescription.text = "이대로 전투 시작";
    }

    // 주요 효과 / 시너지 / 조합 / 유물 / 위험도·보상을 각각 다른 칸으로 나눠 보여줌
    void UpdatePanel()
    {
        CardData card = selection.Chosen;
        RunState run = RunState.Instance;
        if (card == null || run == null) return;

        RunState.CardPickResult result = selection.PickResult;
        Color categoryColor = CardData.CategoryColor(card.category);
        panelTopBar.color = categoryColor;
        panelKicker.text = $"카드 적용 — {CardData.CategoryName(card.category)}";
        panelName.text = card.displayName;

        float y = 23f;
        MoveLine(panelName, y, Mathf.Max(24f, panelName.preferredHeight));
        y += Mathf.Max(24f, panelName.preferredHeight) + 7f;

        int index = 0;

        // 주요 내용 — 이 카드가 실제로 무엇을 하는지
        y = Layout(ref index, y, categoryColor, null, card.description, 13, false, PixelUi.TextColor);

        // 시너지 (계열 장수 보너스도 같은 칸)
        List<string> synergy = new List<string>();
        string synergyTitle = "시너지";
        if (result.synergyLevelAfter > result.synergyLevelBefore)
        {
            synergyTitle = result.synergyLevelBefore == 0 ? "시너지 발동" : "시너지 강화";
            synergy.Add($"{Synergy.Name(result.category)} Lv{result.synergyLevelAfter} — {Synergy.Describe(result.category, result.synergyLevelAfter)}");
        }
        if (result.bonusRisk > 0f)
            synergy.Add($"계열 {run.SynergyCount(result.category)}장 보너스: 위험도 +{result.bonusRisk:0} · 보상 +{result.bonusReward:0}%p");
        if (synergy.Count > 0)
            y = Layout(ref index, y, SynergyColor, synergyTitle, string.Join("\n", synergy), 10, false, PixelUi.TextColor);

        // 조합
        if (result.combos != null && result.combos.Count > 0)
        {
            List<string> combos = new List<string>();
            foreach (CardCombo.Combo combo in result.combos) combos.Add($"{combo.name} — {combo.description}");
            y = Layout(ref index, y, ComboColor, "조합 발동", string.Join("\n", combos), 10, false, PixelUi.TextColor);
        }

        // 유물 — 무엇을 얻었는지 바로 보이게 다른 색·굵게
        if (result.rewards != null && result.rewards.Count > 0)
            y = Layout(ref index, y, RelicColor, "유물 획득", string.Join("\n", result.rewards), 12, true, RelicColor);

        // 위험도·보상 배수 — 제목 없이 흐린 칸
        string overload = selection.RiskBefore < RunState.OverloadRisk && run.IsOverloaded ? "   돌파!" : "";
        string numbers = $"위험도  {selection.RiskBefore:F0} → {run.Risk:F0}{overload}\n보상 배수  x{selection.MultiplierBefore:F2} → x{run.RewardMultiplier:F2}";
        y = Layout(ref index, y, MutedColor, null, numbers, 10, false, MutedColor);

        for (int i = index; i < sections.Length; i++) sections[i].root.gameObject.SetActive(false);

        MoveLine(panelFooter, y + 3f, 12f);
        panelFooter.text = $"{Mathf.CeilToInt(selection.Timer)}초 뒤 자동 진행     Enter 넘기기";

        float height = y + 3f + 12f + 8f;
        panel.sizeDelta = new Vector2(PanelWidth, height);
        panel.anchoredPosition = new Vector2((PixelUi.RefWidth - PanelWidth) * 0.5f, -(PixelUi.RefHeight - height) * 0.5f);
    }

    // 칸 하나를 y 위치에 놓고, 다음 칸이 시작할 y를 돌려줌
    float Layout(ref int index, float y, Color accent, string title, string body, int fontSize, bool bold, Color bodyColor)
    {
        if (index >= sections.Length) return y;
        Section section = sections[index++];
        section.root.gameObject.SetActive(true);
        section.accent.color = accent;

        float top = SectionPad;
        bool hasTitle = !string.IsNullOrEmpty(title);
        section.title.gameObject.SetActive(hasTitle);
        if (hasTitle)
        {
            section.title.text = title;
            section.title.color = accent;
            MoveLine(section.title, top, 12f);
            top += 13f;
        }

        section.body.fontSize = fontSize;
        section.body.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        section.body.color = bodyColor;
        section.body.text = body;
        float bodyHeight = Mathf.Max(fontSize + 4f, section.body.preferredHeight);
        MoveLine(section.body, top, bodyHeight);
        top += bodyHeight + SectionPad;

        section.root.anchoredPosition = new Vector2(SidePad, -y);
        section.root.sizeDelta = new Vector2(PanelWidth - SidePad * 2f, top);
        return y + top + SectionGap;
    }

    // 위에서부터 y만큼 내려간 자리에 height 높이로 놓음 (좌우 여백은 그대로)
    static void MoveLine(Text label, float y, float height)
    {
        RectTransform rect = label.rectTransform;
        rect.offsetMin = new Vector2(rect.offsetMin.x, -(y + height));
        rect.offsetMax = new Vector2(rect.offsetMax.x, -y);
    }
}
