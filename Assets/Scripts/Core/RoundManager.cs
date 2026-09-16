using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// 라운드 흐름 (Spec 1장 게임 루프, 5장 적 수·라운드 종료 판정·라운드 흐름 구현 규칙).
// 카드 선택·탐욕 → 전투 → 전멸 1초 유지 → 결과(골드) → Enter → 다음 라운드 카드 선택. 사망 시 게임 오버.
// 상점은 상점 씬을 만들 때 결과와 카드 선택 사이에 끼워 넣음.
[RequireComponent(typeof(RunState), typeof(GameLoopQueue))]
public class RoundManager : MonoBehaviour
{
    public enum Phase { Cards, Battle, Clearing, Result, GameOver }

    public static RoundManager Instance { get; private set; }

    [Header("연결")]
    public Enemy enemyPrefab;

    [Header("적 수 (Spec 5장)")]
    public int maxStartEnemies = 16;

    [Header("흐름 (Spec 1장)")]
    public float clearHoldTime = 1f;       // 전멸 후 결과까지 유지
    public int shopInterval = 3;           // 3라운드마다 상점

    [Header("적 배치 (Spec 5장 라운드 흐름)")]
    public float spawnMinX = 6f;
    public float spawnMaxX = 29f;
    public float spawnHeight = 1.5f;       // 지면 위 이 높이에서 떨어뜨림

    [Header("안내 문구")]
    public float messageDuration = 2.5f;

    public Phase CurrentPhase { get; private set; }
    public int StartEnemyCount { get; private set; }
    public int ReinforcementBudget { get; private set; }
    public int LastGoldReward { get; private set; }
    public bool NextIsShop => RunState.Instance != null && RunState.Instance.round % shopInterval == 0;

    // 잠깐 떴다 사라지는 안내 문구 (라운드 시작, 증원 소진, 부활 등)
    public string Message { get; private set; }
    public bool HasMessage => messageTimer > 0f;

    private RunState run;
    private GameLoopQueue queue;
    private CardSelection cardSelection;   // 없으면 카드 단계 없이 바로 전투
    private PlayerHealth player;
    private PlayerStats playerStats;
    private DaggerThrower daggers;
    private CameraFollow cameraFollow;
    private InputAction confirmAction;

    private float clearTimer;
    private float messageTimer;
    private int spawnSide = 1;
    private bool reinforcementExhaustedShown;

    void Awake()
    {
        Instance = this;
        run = GetComponent<RunState>();
        queue = GetComponent<GameLoopQueue>();
        cardSelection = GetComponent<CardSelection>();
        confirmAction = InputSystem.actions.FindAction("Player/Interact", throwIfNotFound: true);
    }

    void OnEnable() => Enemy.Killed += OnEnemyKilled;
    void OnDisable() => Enemy.Killed -= OnEnemyKilled;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        player = FindFirstObjectByType<PlayerHealth>();
        if (player != null)
        {
            playerStats = player.GetComponent<PlayerStats>();
            daggers = player.GetComponent<DaggerThrower>();
        }
        else
        {
            Debug.LogWarning("RoundManager: 씬에 PlayerHealth가 없습니다.", this);
        }
        if (Camera.main != null) cameraFollow = Camera.main.GetComponent<CameraFollow>();

        BeginCards();  // 라운드 1 전에도 카드 선택
    }

    void Update()
    {
        messageTimer -= Time.deltaTime;

        if (player != null && player.IsDead && CurrentPhase != Phase.GameOver)
        {
            CurrentPhase = Phase.GameOver;
        }

        switch (CurrentPhase)
        {
            case Phase.Cards:
                break;  // CardSelection이 진행하고 끝나면 OnCardsFinished 호출

            case Phase.Battle:
                if (IsBattleOver())
                {
                    CurrentPhase = Phase.Clearing;
                    clearTimer = clearHoldTime;
                }
                break;

            case Phase.Clearing:
                if (!IsBattleOver())
                {
                    // 유지 중에 분열·부활 등으로 적이 다시 생김
                    CurrentPhase = Phase.Battle;
                    ShowMessage("적이 되살아났습니다!");
                    break;
                }
                clearTimer -= Time.deltaTime;
                if (clearTimer <= 0f) FinishRound();
                break;

            case Phase.Result:
                if (confirmAction.WasPressedThisFrame()) NextRound();
                break;

            case Phase.GameOver:
                if (confirmAction.WasPressedThisFrame()) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                break;
        }
    }

    // 전멸 판정 (Spec 5장): 살아있는 적 0 + 부활·폭발 대기 0. 매 프레임 새로 계산
    bool IsBattleOver()
    {
        return Enemy.AliveCount == 0 && queue.PendingBlockingCount == 0;
    }

    void StartRound()
    {
        queue.Clear();
        CurrentPhase = Phase.Battle;
        reinforcementExhaustedShown = false;

        ResetPlayer();

        // 초기 적 수 = min(16, 4 + floor(라운드 × 0.8) + 증원카드)
        StartEnemyCount = Mathf.Min(maxStartEnemies,
            4 + Mathf.FloorToInt(run.round * 0.8f) + run.enemyModifiers.extraStartEnemies);
        // 추가 증원 총량 상한 = 초기 적 수 × 1.5 + 5
        ReinforcementBudget = Mathf.FloorToInt(StartEnemyCount * 1.5f) + 5;

        for (int i = 0; i < StartEnemyCount; i++) SpawnEnemy(EnemyType.Normal, NextSpawnX());
        if (run.IsOverloaded) SpawnEnemy(EnemyType.Elite, NextSpawnX());  // 돌파: 매 라운드 정예 1마리 추가
        for (int i = 0; i < run.enemyModifiers.extraElites; i++) SpawnEnemy(EnemyType.Elite, NextSpawnX());  // 지옥문

        // 탐욕 안내 등 직전에 띄운 문구가 있으면 함께 표시
        ShowMessage(HasMessage ? $"라운드 {run.round}\n{Message}" : $"라운드 {run.round}");
    }

    // 카드 선택 단계 시작: 플레이어를 중앙에 세우고 조작 잠금
    void BeginCards()
    {
        CurrentPhase = Phase.Cards;
        queue.Clear();
        ResetPlayer();
        SetPlayerControl(false);

        if (cardSelection == null || !cardSelection.enabled)
        {
            OnCardsFinished();
            return;
        }
        cardSelection.Begin(OnCardsFinished);
    }

    void OnCardsFinished()
    {
        SetPlayerControl(true);
        StartRound();
    }

    void SetPlayerControl(bool allowed)
    {
        if (player == null) return;
        if (allowed && player.IsDead) return;

        foreach (MonoBehaviour control in new MonoBehaviour[]
                 { player.GetComponent<PlayerMovement>(), player.GetComponent<PlayerAttack>(), daggers })
        {
            if (control != null) control.enabled = allowed;
        }
        if (!allowed) player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
    }

    // 광기의 축복: 처치 1마리마다 장수 × (6 + 라운드 × 0.8) 골드, 반올림해서 즉시 지급
    void OnEnemyKilled(Enemy enemy)
    {
        int stacks = run.enemyModifiers.killGoldStacks;
        if (stacks > 0) run.gold += Mathf.RoundToInt(stacks * (6f + run.round * 0.8f));
    }

    // 플레이어를 맵 중앙으로, 단검 보충, 카메라 즉시 이동. 체력은 회복하지 않음
    void ResetPlayer()
    {
        if (player != null)
        {
            Vector2 start = new Vector2(0f, ArenaLayout.GroundTop + 0.5f);
            PlayerMovement movement = player.GetComponent<PlayerMovement>();
            if (movement != null) movement.CancelDash();

            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
            rb.position = start;
            rb.linearVelocity = Vector2.zero;
            player.transform.position = start;
        }
        if (daggers != null) daggers.RefillDaggers();
        if (cameraFollow != null) cameraFollow.SnapToTarget();
    }

    void FinishRound()
    {
        queue.Clear();

        // 라운드 클리어 골드 = 20 × 라운드 × 보상배수 × 골드획득배율
        float goldMul = playerStats != null ? playerStats.goldMul : 1f;
        LastGoldReward = Mathf.RoundToInt(20f * run.round * run.RewardMultiplier * goldMul);
        run.gold += LastGoldReward;

        CurrentPhase = Phase.Result;
    }

    void NextRound()
    {
        // TODO: 상점 씬(라운드 N.5) — 지금은 결과 화면에 안내만 하고 넘어감
        run.round++;
        BeginCards();
    }

    // 적 1마리 생성. 소환술·군단·범람 등 증원은 먼저 TryConsumeReinforcement()로 예산 확인
    public Enemy SpawnEnemy(EnemyType type, float x)
    {
        if (enemyPrefab == null)
        {
            Debug.LogWarning("RoundManager: Enemy Prefab이 연결되지 않았습니다.", this);
            return null;
        }

        Vector3 position = new Vector3(x, ArenaLayout.GroundTop + spawnHeight, 0f);
        Enemy enemy = Instantiate(enemyPrefab, position, Quaternion.identity);
        enemy.name = $"Enemy_{type}";
        enemy.Initialize(type, run.enemyModifiers, player);
        return enemy;
    }

    // 증원 예산 1 소모. 소진됐으면 false (처음 소진 시 안내 문구 한 번)
    public bool TryConsumeReinforcement()
    {
        if (ReinforcementBudget > 0)
        {
            ReinforcementBudget--;
            return true;
        }
        if (!reinforcementExhaustedShown)
        {
            ShowMessage("적의 증원이 끊겼습니다");
            reinforcementExhaustedShown = true;
        }
        return false;
    }

    public void ShowMessage(string text)
    {
        Message = text;
        messageTimer = messageDuration;
    }

    // 좌우 번갈아 x = ±(6 ~ 29)
    float NextSpawnX()
    {
        float x = spawnSide * Random.Range(spawnMinX, spawnMaxX);
        spawnSide = -spawnSide;
        return Mathf.Clamp(x, -ArenaLayout.MapHalfWidth + 1f, ArenaLayout.MapHalfWidth - 1f);
    }
}
