using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// 라운드 흐름 (Spec 1장 게임 루프, 5장 적 수·라운드 종료 판정·라운드 흐름 구현 규칙).
// 카드 선택·탐욕 → 전투 → 전멸 1초 유지 → 결과(골드) → Enter → 다음 라운드 카드 선택. 사망 시 게임 오버.
// 3라운드마다 결과 다음에 상점 씬으로 넘어가고, 상점 출구로 이 씬이 다시 열리면 이어서 카드 선택부터.
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
    public float baseClearGold = 80f;      // 보상 배수와 무관한 클리어 기본 골드 (Spec 2장)
    public string shopSceneName = "ShopScene";
    public string lobbySceneName = "LobbyScene";  // 게임 오버 후 Enter (없으면 전투 씬을 새로 시작)

    [Header("적 배치 (Spec 5장 라운드 흐름)")]
    public float spawnMinX = 6f;
    public float spawnMaxX = 29f;
    public float spawnHeight = 1.5f;       // 지면 위 이 높이에서 떨어뜨림

    [Header("적 등장 순서·보스 (Spec 5장)")]
    public int shieldFromRound = 2;
    public int chargerFromRound = 4;
    public int bossInterval = 5;           // 5라운드마다 보스
    public int bossGoldBase = 100;         // 보스 처치 골드 = 100 + 20 × 라운드 (임시값)
    public int bossGoldPerRound = 20;

    [Header("안내 문구")]
    public float messageDuration = 2.5f;

    public Phase CurrentPhase { get; private set; }
    public int StartEnemyCount { get; private set; }
    public int ReinforcementBudget { get; private set; }
    public int LastGoldReward { get; private set; }
    public int LastSoulReward { get; private set; }   // 게임 오버 시 얻은 영혼
    public bool LobbyAvailable => Application.CanStreamedLevelBeLoaded(lobbySceneName);

    // 이번 라운드 통계 — 광전사 사망률, 군단 발동 조건에서 사용
    public int SpawnedThisRound { get; private set; }   // 부활은 포함하지 않음
    public int KilledThisRound { get; private set; }
    public float DeathRate => SpawnedThisRound > 0 ? Mathf.Clamp01((float)KilledThisRound / SpawnedThisRound) : 0f;

    // 전투가 시작된 직후 (초기 적 생성 후) — 전투 중 카드 효과 초기화용
    public event System.Action BattleStarted;
    public bool NextIsShop => RunState.Instance != null && RunState.Instance.round % shopInterval == 0;
    public bool ShopAvailable => Application.CanStreamedLevelBeLoaded(shopSceneName);  // 빌드 설정에 상점 씬이 있는지

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
            // 상점에서 돌아왔으면 강화·체력 복원, 새 런이면 로비 영구 강화 적용
            if (run.ContinuedFromPreviousScene) run.RestorePlayer(player);
            else MetaProgress.ApplyRunStart(run, player);
        }
        else
        {
            Debug.LogWarning("RoundManager: 씬에 PlayerHealth가 없습니다.", this);
        }
        if (Camera.main != null) cameraFollow = Camera.main.GetComponent<CameraFollow>();

        BeginCards();  // 라운드 1 전에도, 상점에서 돌아온 뒤에도 카드 선택
    }

    void Update()
    {
        messageTimer -= Time.deltaTime;

        if (player != null && player.IsDead && CurrentPhase != Phase.GameOver)
        {
            CurrentPhase = Phase.GameOver;
            SoundManager.Play(SoundId.GameOver);
            SettleRun();
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
                if (confirmAction.WasPressedThisFrame())
                {
                    if (LobbyAvailable) SceneManager.LoadScene(lobbySceneName);
                    else SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                }
                break;
        }
    }

    // 런 종료: 영혼 정산 후 즉시 저장 (Spec 6-3장). 게임 오버에 들어가는 순간 한 번
    void SettleRun()
    {
        queue.Clear();
        EnemyProjectile.DestroyAll();
        LastSoulReward = MetaCatalog.SoulsForRun(run.round, run.Risk, run.RelicCount);
        MetaProgress.RecordRun(LastSoulReward, run.round);
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
        int extra = run.enemyModifiers.extraStartEnemies;
        int wantedCount = 4 + Mathf.FloorToInt(run.round * 0.8f) + extra;
        StartEnemyCount = Mathf.Min(maxStartEnemies, wantedCount);
        bool bossRound = IsBossRound;
        if (bossRound) StartEnemyCount = Mathf.CeilToInt(StartEnemyCount * 0.5f);  // 보스 라운드는 절반

        // 증원 카드 몫 중 16 상한에 막힌 만큼, 2마리마다 일반 적 1마리를 정예로 교체 (라운드 자연 증가분은 제외)
        int blockedByCap = Mathf.Clamp(wantedCount - maxStartEnemies, 0, extra);
        // 추가 증원 총량 상한 = 초기 적 수 × 1.5 + 5
        ReinforcementBudget = Mathf.FloorToInt(StartEnemyCount * 1.5f) + 5;
        SpawnedThisRound = 0;
        KilledThisRound = 0;

        // 초기 적: 1/3(내림, 최소 1) 원거리, 라운드 2부터 1/6(최소 1) 방패병, 라운드 4부터 같은 수 돌진병,
        // 나머지는 일반 적 (그중 일부는 증원 초과분만큼 정예)
        int rangedCount = Mathf.Max(1, StartEnemyCount / 3);
        int sixth = Mathf.Max(1, StartEnemyCount / 6);
        int shieldCount = run.round >= shieldFromRound ? Mathf.Min(sixth, StartEnemyCount - rangedCount) : 0;
        int chargerCount = run.round >= chargerFromRound ? Mathf.Min(sixth, StartEnemyCount - rangedCount - shieldCount) : 0;
        int normalCount = StartEnemyCount - rangedCount - shieldCount - chargerCount;
        int eliteConversions = Mathf.Min(normalCount, blockedByCap / 2);

        SpawnMany(EnemyType.Ranged, rangedCount);
        SpawnMany(EnemyType.Shield, shieldCount);
        SpawnMany(EnemyType.Charger, chargerCount);
        SpawnMany(EnemyType.Elite, eliteConversions);
        SpawnMany(EnemyType.Normal, normalCount - eliteConversions);
        if (bossRound) SpawnEnemy(EnemyType.Boss, NextSpawnX());
        if (run.IsOverloaded) SpawnEnemy(EnemyType.Elite, NextSpawnX());  // 돌파: 매 라운드 정예 1마리 추가
        for (int i = 0; i < run.enemyModifiers.extraElites; i++) SpawnEnemy(EnemyType.Elite, NextSpawnX());  // 지옥문
        int abyssElites = Synergy.AbyssElites(run.SynergyLevel(CardCategory.Curse));
        for (int i = 0; i < abyssElites; i++) SpawnEnemy(EnemyType.Elite, NextSpawnX());  // 심연 시너지

        // Inspector 등으로 위험도가 바뀌었으면 밀린 유물 지급
        System.Collections.Generic.List<string> rewards = run.GrantRelics(player);
        if (rewards.Count > 0) SoundManager.Play(SoundId.Relic);
        if (rewards.Count > 0) ShowMessage(HasMessage ? $"{Message}\n{string.Join("\n", rewards)}" : string.Join("\n", rewards));

        // 탐욕 안내 등 직전에 띄운 문구가 있으면 함께 표시
        string title = bossRound ? $"라운드 {run.round} — 보스: {Enemy.BossName}" : $"라운드 {run.round}";
        if (bossRound) SoundManager.Play(SoundId.BossAppear);
        ShowMessage(HasMessage ? $"{title}\n{Message}" : title);
        BattleStarted?.Invoke();
    }

    // round > 0 조건이 없으면 **시작 라운드를 0으로 두고 시험할 때 0 % 5 == 0 이라 보스가 나온다**
    public bool IsBossRound => bossInterval > 0 && run != null && run.round > 0 && run.round % bossInterval == 0;

    void SpawnMany(EnemyType type, int count)
    {
        for (int i = 0; i < count; i++) SpawnEnemy(type, NextSpawnX());
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
                 { player.GetComponent<PlayerMovement>(), player.GetComponent<PlayerAttack>(), daggers, player.GetComponent<PlayerPotion>(), player.GetComponent<PlayerSkill>() })
        {
            if (control != null) control.enabled = allowed;
        }
        if (!allowed) player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
    }

    // 광기의 축복: 처치 1마리마다 장수 × (6 + 라운드 × 0.8) 골드, 반올림해서 즉시 지급
    void OnEnemyKilled(Enemy enemy)
    {
        if (CurrentPhase == Phase.Battle || CurrentPhase == Phase.Clearing) KilledThisRound++;

        // 흡혈 목걸이: 처치마다 체력 회복
        if (player != null && playerStats != null && playerStats.healOnKill > 0f) player.Heal(playerStats.healOnKill);

        int stacks = run.enemyModifiers.killGoldStacks;
        if (stacks > 0) run.gold += Mathf.RoundToInt(stacks * (6f + run.round * 0.8f));

        // 보스 처치: 즉시 골드 (배수·골드획득배율 미적용)
        if (enemy.IsBoss && (CurrentPhase == Phase.Battle || CurrentPhase == Phase.Clearing))
        {
            int bossGold = bossGoldBase + bossGoldPerRound * run.round;
            run.gold += bossGold;
            ShowMessage($"{Enemy.BossName} 처치!  +{bossGold} G");
        }
    }

    // 플레이어를 맵 중앙으로, 단검 보충, 카메라 즉시 이동. 체력은 회복하지 않음
    void ResetPlayer()
    {
        if (player != null)
        {
            Vector2 start = ArenaLayout.GroundStart(player.gameObject, 0f);
            PlayerMovement movement = player.GetComponent<PlayerMovement>();
            if (movement != null)
            {
                movement.CancelDash();
                movement.CancelSlamFall();
            }

            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
            rb.position = start;
            rb.linearVelocity = Vector2.zero;
            player.transform.position = start;
        }
        if (daggers != null) daggers.RefillDaggers();
        PlayerSkill skill = player != null ? player.GetComponent<PlayerSkill>() : null;
        if (skill != null) skill.ResetCooldown();  // 라운드 시작마다 스킬 준비 완료
        if (cameraFollow != null) cameraFollow.SnapToTarget();
    }

    void FinishRound()
    {
        queue.Clear();
        EnemyProjectile.DestroyAll();  // 날아가던 마법구가 결과 화면에서 맞지 않게

        // 라운드 클리어 골드 = (20 × 라운드 × 보상배수 + 기본 80) × 골드획득배율
        float goldMul = playerStats != null ? playerStats.goldMul : 1f;
        LastGoldReward = Mathf.RoundToInt((20f * run.round * run.RewardMultiplier + baseClearGold) * goldMul);
        run.gold += LastGoldReward;

        CurrentPhase = Phase.Result;
        SoundManager.Play(SoundId.RoundClear);
    }

    void NextRound()
    {
        // 3라운드마다 상점(라운드 N.5). 라운드 번호는 상점 출구에서 올림
        if (NextIsShop && ShopAvailable)
        {
            run.CarryToNextScene(player);
            SceneManager.LoadScene(shopSceneName);
            return;
        }
        if (NextIsShop) Debug.LogWarning($"RoundManager: 상점 씬 \"{shopSceneName}\"이 빌드 설정에 없어 건너뜁니다. (Greed Bound > 상점 씬 생성)", this);

        run.round++;
        BeginCards();
    }

    // 적 1마리 생성. 소환술·군단·범람 등 증원은 먼저 TryConsumeReinforcement()로 예산 확인
    public Enemy SpawnEnemy(EnemyType type, float x)
    {
        return SpawnEnemyAt(type, new Vector2(x, ArenaLayout.GroundTop + spawnHeight), countAsSpawn: true);
    }

    // 원하는 위치에 적 생성 (분열·부활은 죽은 자리). 부활은 광전사 사망률 계산에서 "생성"으로 치지 않음
    public Enemy SpawnEnemyAt(EnemyType type, Vector2 position, bool countAsSpawn)
    {
        if (enemyPrefab == null)
        {
            Debug.LogWarning("RoundManager: Enemy Prefab이 연결되지 않았습니다.", this);
            return null;
        }

        Enemy enemy = Instantiate(enemyPrefab, new Vector3(position.x, position.y, 0f), Quaternion.identity);
        enemy.name = $"Enemy_{type}";
        enemy.Initialize(type, run.enemyModifiers, player);
        if (countAsSpawn) SpawnedThisRound++;
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
        return ArenaBounds.Clamp(x, 1f);  // 죽음의 영역으로 좁아졌으면 그 안으로
    }
}
