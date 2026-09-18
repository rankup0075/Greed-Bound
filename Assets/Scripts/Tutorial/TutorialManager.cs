using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// 튜토리얼 씬 (Spec 9장 "튜토리얼 씬 — Unity 구현 규칙"). 타이틀 메뉴 "튜토리얼"로 들어온다.
// 왼쪽에서 오른쪽으로 네 구역을 걸어가며 과제를 하나씩 끝내는 구간 코스.
//   ① 이동·점프 → ② 대쉬(틈 7u) → ③ 근접 3연타(허수아비 3대) → ④ 단검·스킬·예고(공격이 통하지 않는 적) → 출구 포탈
// 과제를 끝내도 막히는 곳은 없어 언제든 되돌아가 연습할 수 있다. 실패·사망이 없으므로 연습용 적은 피해 0.
// 허수아비·표적·기둥·포탈은 시작할 때 코드 메시로 만듦(스프라이트 교체 전 임시). 안내는 임시 UI(OnGUI).
public class TutorialManager : MonoBehaviour
{
    [Header("연결")]
    public Enemy enemyPrefab;                   // 씬 생성 도구가 전투 씬에서 가져와 넣어 줌
    public string titleSceneName = "TitleScene";

    [Header("상호작용")]
    public float portalReach = 1.2f;

    const float MoveDistance = 3f;              // ① 이걸 넘게 움직이면 이동을 익힌 것으로
    const float PortalHeight = 2.4f;
    const float DummyHealth = 60f;              // 3연타(약 20+26+42)면 한 번에 부서짐
    const float DaggerTargetHealth = 13f;       // 단검 한 방
    const float SkillTargetHealth = 400f;       // 부수는 게 목적이 아니라 스킬 판정을 보여 주는 표적

    static readonly Color ZoneColor = new Color(0.22f, 0.24f, 0.35f, 0.35f);
    static readonly Color DummyColor = new Color(0.72f, 0.6f, 0.42f);
    static readonly Color TargetColor = new Color(0.85f, 0.75f, 0.35f);
    static readonly Color PillarColor = new Color(0.32f, 0.3f, 0.36f);
    static readonly Color PortalColor = new Color(0.45f, 0.75f, 0.95f);
    static readonly Color DoneColor = new Color(0.45f, 0.95f, 0.6f);
    static readonly Color TitleColor = new Color(1f, 0.85f, 0.45f);

    public enum Step { Move, Jump, Dash, Melee, Dagger, Skill, Telegraph, Exit, Done }

    // 구역(TutorialLayout.Zones)마다 마지막 과제 — 이 과제를 넘기면 구역 제목에 ✔
    static readonly int[] ZoneLastStep = { (int)Step.Jump, (int)Step.Dash, (int)Step.Melee, (int)Step.Telegraph };

    struct Task
    {
        public string title;
        public string detail;
    }

    static readonly Task[] Tasks =
    {
        new Task { title = "← → 로 걸어 보세요", detail = "왼쪽·오른쪽 방향키로 움직입니다" },
        new Task { title = "C 로 점프해 발판을 오르세요", detail = "발판은 아래에서 위로 통과합니다.  ↓ + C 로 다시 내려올 수 있습니다" },
        new Task { title = "Left Shift 로 대쉬해 틈을 건너세요", detail = "틈은 7u — 점프만으로는 닿지 않습니다.  대쉬는 3u, 쿨 3초, 무적은 없습니다" },
        new Task { title = "Z 로 허수아비 3대를 부수세요", detail = "Z를 연달아 세 번 누르면 3연타.  마지막 타가 가장 세고 적을 밀어냅니다" },
        new Task { title = "X 로 기둥 위 표적을 맞히세요", detail = "단검은 앞으로 날아갑니다.  라운드마다 5개까지 다시 채워집니다" },
        new Task { title = "A 로 스킬을 써 보세요", detail = "스킬은 로비에서 고른 1개.  판정 범위는 이펙트 크기와 같습니다" },
        new Task { title = "붉게 멈춘 적의 공격을 피하고 처치하세요", detail = "모든 공격에는 예고가 있습니다.  이 적은 피해를 주지 않습니다" },
        new Task { title = "오른쪽 포탈에서 Enter — 튜토리얼 끝", detail = "언제든 돌아가서 다시 연습해도 됩니다" },
    };

    private PlayerHealth player;
    private PlayerMovement movement;
    private PlayerAttack attack;
    private DaggerThrower daggers;
    private PlayerSkill skill;
    private InputAction interactAction;

    private Step step;
    private float startX;
    private bool dashSeen, daggerThrown, skillUsed;
    private int dummiesBroken;
    private bool daggerTargetBroken;
    private Enemy practiceEnemy;
    private bool enemySpawned;
    private MeshFilter portal;
    private bool leaving;

    private GUIStyle taskStyle, detailStyle, hintStyle, zoneStyle, doneStyle;

    void Awake()
    {
        interactAction = InputSystem.actions.FindAction("Player/Interact", throwIfNotFound: true);
    }

    void Start()
    {
        player = FindFirstObjectByType<PlayerHealth>();
        if (player == null)
        {
            Debug.LogWarning("TutorialManager: 씬에 PlayerHealth가 없습니다.", this);
            enabled = false;
            return;
        }

        movement = player.GetComponent<PlayerMovement>();
        attack = player.GetComponent<PlayerAttack>();
        daggers = player.GetComponent<DaggerThrower>();
        skill = player.GetComponent<PlayerSkill>();

        // 전투 조작을 모두 켜되, 물약은 런 상태(RunState)가 없으므로 끈다
        foreach (MonoBehaviour control in new MonoBehaviour[] { movement, attack, daggers, skill })
            if (control != null) control.enabled = true;
        PlayerPotion potion = player.GetComponent<PlayerPotion>();
        if (potion != null) potion.enabled = false;

        if (daggers != null)
        {
            daggers.RefillDaggers();
            daggers.Thrown += () => daggerThrown = true;
        }
        if (skill != null) skill.Used += _ => skillUsed = true;

        PlacePlayer();
        BuildProps();
    }

    void PlacePlayer()
    {
        Vector2 start = ArenaLayout.GroundStart(player.gameObject, TutorialLayout.PlayerStartX);
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        body.position = start;
        body.linearVelocity = Vector2.zero;
        player.transform.position = start;
        startX = start.x;
    }

    void BuildProps()
    {
        foreach ((string _, float centerX, float width) in TutorialLayout.Zones)
        {
            WorldGui.CreateQuad("Zone", new Vector2(centerX, ArenaLayout.GroundTop),
                new Vector2(width, TutorialLayout.ZoneBackdropHeight), ZoneColor, -8);
        }

        foreach (float x in TutorialLayout.DummyX)
        {
            TutorialTarget dummy = TutorialTarget.Create("Dummy", new Vector2(x, ArenaLayout.GroundTop),
                new Vector2(0.9f, 1.4f), DummyColor, DummyHealth);
            dummy.Broken += _ => dummiesBroken++;
        }

        // 단검 표적: 기둥 위라 근접 사거리(1.91u)로는 닿지 않는다
        WorldGui.CreateQuad("Pillar", new Vector2(TutorialLayout.DaggerPillarX, ArenaLayout.GroundTop),
            new Vector2(0.8f, TutorialLayout.DaggerPillarHeight), PillarColor, -4);
        TutorialTarget daggerTarget = TutorialTarget.Create("DaggerTarget",
            new Vector2(TutorialLayout.DaggerPillarX, TutorialLayout.DaggerPillarHeight),
            new Vector2(0.8f, 0.8f), TargetColor, DaggerTargetHealth);
        daggerTarget.Broken += _ => daggerTargetBroken = true;

        TutorialTarget.Create("SkillTarget", new Vector2(TutorialLayout.SkillTargetX, ArenaLayout.GroundTop),
            new Vector2(1f, 1.6f), TargetColor, SkillTargetHealth);

        portal = WorldGui.CreateQuad("ExitPortal", TutorialLayout.PortalFeet, new Vector2(1.2f, PortalHeight), PortalColor, -4);
    }

    void Update()
    {
        if (player == null || leaving) return;

        if (movement != null && movement.IsDashing) dashSeen = true;
        Advance();
        Animate();

        if (step == Step.Exit && NearPortal() && interactAction.WasPressedThisFrame()) Leave();
    }

    // 과제가 끝났으면 다음 단계로. 한 프레임에 여러 단계가 넘어갈 수 있다(이미 해 둔 경우)
    void Advance()
    {
        while (step != Step.Done && Cleared(step))
        {
            step++;
            SoundManager.Play(SoundId.Buy);
            if (step == Step.Telegraph) SpawnPracticeEnemy();
        }
    }

    bool Cleared(Step current)
    {
        float x = player.transform.position.x;
        switch (current)
        {
            case Step.Move: return Mathf.Abs(x - startX) >= MoveDistance;
            case Step.Jump: return x >= TutorialLayout.JumpGateX;
            case Step.Dash: return dashSeen && x >= TutorialLayout.DashGateX;
            case Step.Melee: return dummiesBroken >= TutorialLayout.DummyX.Length;
            case Step.Dagger: return daggerThrown && daggerTargetBroken;
            case Step.Skill: return skillUsed;
            case Step.Telegraph: return enemySpawned && (practiceEnemy == null || practiceEnemy.IsDead);
            default: return false;   // Exit는 포탈에서 Enter로만 끝남
        }
    }

    // 피해를 주지 않는 연습용 적 — 공격력 배율 0이면 예비동작·공격 동작은 그대로이고 피해만 들어가지 않는다
    void SpawnPracticeEnemy()
    {
        if (enemyPrefab == null)
        {
            Debug.LogWarning("TutorialManager: Enemy Prefab이 연결되지 않았습니다 (메뉴 \"튜토리얼 씬 생성\"을 다시 실행하세요).", this);
            enemySpawned = true;   // 막히지 않게 통과 처리
            return;
        }

        Vector2 position = new Vector2(TutorialLayout.EnemySpawnX, ArenaLayout.GroundTop + 1.5f);
        practiceEnemy = Instantiate(enemyPrefab, position, Quaternion.identity);
        practiceEnemy.name = "PracticeEnemy";
        practiceEnemy.Initialize(EnemyType.Normal, new EnemyModifiers { attackMul = 0f }, player);
        enemySpawned = true;
    }

    void Animate()
    {
        Color color = step == Step.Exit || step == Step.Done ? PortalColor : Color.Lerp(PortalColor, PillarColor, 0.6f);
        color.a = 0.6f + 0.25f * Mathf.Sin(Time.time * 3f);
        WorldGui.SetQuadColor(portal, color);
    }

    bool NearPortal()
    {
        Vector2 center = TutorialLayout.PortalFeet + new Vector2(0f, 0.5f);
        return Vector2.Distance(player.transform.position, center) <= portalReach;
    }

    void Leave()
    {
        string scene = Application.CanStreamedLevelBeLoaded(titleSceneName) ? titleSceneName : null;
        if (scene == null)
        {
            Debug.LogWarning($"TutorialManager: 씬 \"{titleSceneName}\"이 빌드 설정에 없습니다.", this);
            return;
        }
        leaving = true;
        SceneManager.LoadScene(scene);
    }

    // ───────────── 임시 UI ─────────────

    void EnsureStyles()
    {
        if (taskStyle != null) return;
        taskStyle = WorldGui.MakeStyle(30, new Color(1f, 0.95f, 0.85f));
        detailStyle = WorldGui.MakeStyle(20, new Color(0.8f, 0.8f, 0.85f));
        hintStyle = WorldGui.MakeStyle(18, new Color(0.75f, 0.75f, 0.8f));
        zoneStyle = WorldGui.MakeStyle(18, TitleColor);
        doneStyle = WorldGui.MakeStyle(26, DoneColor);
    }

    void OnGUI()
    {
        if (player == null) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        EnsureStyles();

        float width = WorldGui.BeginVirtual(out float scale);

        // 구역 제목 — 어디까지가 한 과제인지
        for (int i = 0; i < TutorialLayout.Zones.Length; i++)
        {
            (string title, float centerX, float _) = TutorialLayout.Zones[i];
            bool cleared = (int)step > ZoneLastStep[i];
            WorldGui.DrawBubble(cam, scale, new Vector2(centerX, TutorialLayout.ZoneBackdropHeight + 0.2f),
                cleared ? $"{title}  ✔" : title, 220f, zoneStyle, new Color(0f, 0f, 0f, 0.45f));
        }

        if (step == Step.Exit || step == Step.Done)
        {
            WorldGui.DrawBubble(cam, scale, TutorialLayout.PortalFeet + new Vector2(0f, PortalHeight + 0.3f),
                "출구\nEnter — 타이틀로", 200f, zoneStyle, new Color(0.08f, 0.07f, 0.1f, 0.85f));
        }

        // 지금 할 일 — 화면 위 가운데
        int index = Mathf.Min((int)step, Tasks.Length - 1);
        Task task = Tasks[index];
        WorldGui.DrawRect(new Rect(width * 0.5f - 400f, 24f, 800f, 86f), new Color(0f, 0f, 0f, 0.55f));
        GUI.Label(new Rect(width * 0.5f - 390f, 32f, 780f, 40f), task.title, taskStyle);
        GUI.Label(new Rect(width * 0.5f - 390f, 74f, 780f, 30f), task.detail, detailStyle);

        if (step == Step.Done)
            GUI.Label(new Rect(0, 130f, width, 40f), "모든 과제를 끝냈습니다", doneStyle);

        GUI.Label(new Rect(0, WorldGui.VirtualHeight - 44f, width, 30f),
            "← → 이동   C 점프   ↓+C 내려가기   Left Shift 대쉬   Z 공격   X 단검   A 스킬   Enter 상호작용   ESC 일시정지·조작법",
            hintStyle);

        GUI.matrix = Matrix4x4.identity;
    }
}
