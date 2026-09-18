using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 라운드 전 카드 선택 → 적용 안내 → 탐욕 (Spec 1장, 3장 "선택 화면 흐름").
// RoundManager가 Begin()으로 열고, 끝나면 콜백으로 전투를 시작함.
// 화면은 CardSelectionUI(정식 uGUI)가 그림 — 이 파일은 흐름·상태만 가지고, 아래 읽기 전용 속성으로 넘겨줌.
[RequireComponent(typeof(RunState))]
public class CardSelection : MonoBehaviour
{
    public enum Step { Hidden, Choose, Applied, Greed }

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
    private InputAction rerollAction;

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
    private RunState.CardPickResult pickResult;   // 시너지 변화·유물 획득 안내

    void Awake()
    {
        run = GetComponent<RunState>();
        moveAction = InputSystem.actions.FindAction("Player/Move", throwIfNotFound: true);
        confirmAction = InputSystem.actions.FindAction("Player/Interact", throwIfNotFound: true);
        rerollAction = InputSystem.actions.FindAction("Player/Reroll", throwIfNotFound: true);

        // íë©´ì ì¬ì ê³ ì¹ì§ ììë ëëë¡ ì¤ì¤ë¡ ë¶ì
        if (GetComponent<CardSelectionUI>() == null) gameObject.AddComponent<CardSelectionUI>();
    }

    // 영구 강화 넓은 시야: 3장 → 4장
    int OfferCount => offerCount + (MetaProgress.Level(MetaUpgradeId.WideSight) > 0 ? 1 : 0);

    // 리롤 가능 여부: 남은 횟수가 있고, 탐욕 단계는 영구 강화 탐욕의 눈이 있을 때만
    bool CanReroll => run.rerollsLeft > 0
        && (step == Step.Choose || (step == Step.Greed && offered.Count > 0 && MetaProgress.Level(MetaUpgradeId.GreedEye) > 0));

    void Start()
    {
        player = FindFirstObjectByType<PlayerHealth>();
    }

    public void Begin(Action onFinished)
    {
        this.onFinished = onFinished;
        BuildOffer(OfferCount);

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

    // 구현된 카드 중에서 서로 다른 카드 count장 (리롤도 같은 방식으로 새로 뽑음)
    void BuildOffer(int count)
    {
        offered.Clear();
        if (library == null) return;

        List<CardData> pool = new List<CardData>();
        foreach (CardData card in library.cards)
        {
            if (card != null && CardEffects.IsImplemented(card.effect)) pool.Add(card);
        }

        while (offered.Count < count && pool.Count > 0)
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

        // R — 제시된 카드를 같은 장수만큼 새로 뽑음 (선택 중인 칸 위치는 유지)
        if (rerollAction.WasPressedThisFrame() && CanReroll)
        {
            run.rerollsLeft--;
            BuildOffer(offered.Count);
            return;
        }

        switch (step)
        {
            case Step.Choose:
                cursor = Wrap(cursor + navigation, offered.Count);
                if (confirm)
                {
                    chosen = offered[cursor];
                    riskBefore = run.Risk;
                    multiplierBefore = run.RewardMultiplier;
                    pickResult = run.AddCard(chosen, false, player);
                    SoundManager.Play(SoundId.CardPick);
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
        RunState.CardPickResult result = run.AddCard(card, true, player);
        SoundManager.Play(SoundId.CardPick);
        if (RoundManager.Instance == null) return;

        string text = $"탐욕: {card.displayName} (위험도·보상 ×{RunState.GreedMultiplier})";
        foreach (CardCombo.Combo combo in result.combos) text += $"\n조합 발동: {combo.name} — {combo.description}";
        if (result.rewards.Count > 0) text += "\n" + string.Join("\n", result.rewards);
        RoundManager.Instance.ShowMessage(text);
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

    // ───────────── 화면(CardSelectionUI)이 읽는 상태 ─────────────

    public Step CurrentStep => step;
    public IReadOnlyList<CardData> Offered => offered;
    public int Cursor => cursor;
    public float Timer => timer;                       // 적용 안내 남은 시간 / 탐욕 제한 시간
    public bool StopSelected => step == Step.Greed && cursor >= offered.Count;

    public CardData Chosen => chosen;
    public float RiskBefore => riskBefore;
    public float MultiplierBefore => multiplierBefore;
    public RunState.CardPickResult PickResult => pickResult;

    // 카드에 적용되는 배율 — 탐욕 단계에서 고르면 위험도·보상이 ×1.3
    public float StepMultiplier => step == Step.Greed ? RunState.GreedMultiplier : 1f;

    public string RerollHint()
    {
        if (CanReroll) return $"     R 리롤 ({run.rerollsLeft}회 남음)";
        if (run.rerollsLeft > 0 && step == Step.Greed) return "     (탐욕 단계 리롤: 로비 강화 '탐욕의 눈' 필요)";
        return "";
    }
}
