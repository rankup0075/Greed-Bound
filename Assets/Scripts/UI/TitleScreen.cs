using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// 타이틀 화면 (Spec 9장 "타이틀 화면"). 빌드 설정 맨 앞 = 게임 시작 씬.
// 게임 시작 → 로비 씬(영구 강화·스킬 선택 → 출전). 임시 UI(OnGUI, MenuGui 공용 스타일).
// 조작: ↑↓ 선택, Enter 확인, ESC 뒤로 (설정에서만). 씬에 이 컴포넌트 하나만 두면 됨 — 메뉴 "Greed Bound > 타이틀 씬 생성"
public class TitleScreen : MonoBehaviour
{
    public static bool IsActive => instance != null;

    static TitleScreen instance;

    [Header("이동할 씬")]
    public string lobbySceneName = "LobbyScene";
    public string battleSceneName = "MainScene";   // 로비가 없을 때 바로 전투로
    public string tutorialSceneName = "TutorialScene";

    [Header("표시")]
    public string titleText = "그리드 바운드";
    public string subtitleText = "GREED BOUND";

    static readonly string[] Items = { "게임 시작", "튜토리얼", "조작법", "설정", "게임 종료" };
    static readonly Color Background = new Color(0.06f, 0.05f, 0.09f);

    private InputAction pauseAction, navigateAction, submitAction;
    private readonly SettingsPanel settings = new SettingsPanel();
    private readonly ControlsPanel controls = new ControlsPanel();
    private bool inSettings, inControls;
    private int cursor;
    private Vector2 lastNavigate;
    private string message;

    void Awake()
    {
        instance = this;
        InputActionAsset actions = InputSystem.actions;
        pauseAction = actions.FindAction("UI/Pause", throwIfNotFound: true);
        navigateAction = actions.FindAction("UI/Navigate", throwIfNotFound: true);
        submitAction = actions.FindAction("UI/Submit", throwIfNotFound: true);
    }

    void OnEnable()
    {
        instance = this;
        Time.timeScale = 1f;   // 일시정지 중에 타이틀로 돌아오는 길이 생겨도 멈춘 채로 시작하지 않게
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Update()
    {
        bool cancel = pauseAction.WasPressedThisFrame();
        Vector2 nav = navigateAction.ReadValue<Vector2>();
        int vertical = PauseMenu.Pressed(nav.y, lastNavigate.y);
        int horizontal = PauseMenu.Pressed(nav.x, lastNavigate.x);
        lastNavigate = nav;
        bool submit = submitAction.WasPressedThisFrame();

        if (inControls)
        {
            if (!controls.HandleInput(cancel, submit)) { inControls = false; cursor = 2; }
            return;
        }

        if (inSettings)
        {
            if (cancel || !settings.HandleInput(vertical, horizontal, submit))
            {
                settings.Close();
                inSettings = false;
                cursor = 3;
#if UNITY_EDITOR
                message = "에디터에서는 해상도·화면 모드가 적용되지 않습니다 (빌드에서 적용)";
#endif
            }
            return;
        }

        if (vertical != 0) cursor = (cursor - vertical + Items.Length) % Items.Length;
        if (!submit) return;
        switch (cursor)
        {
            case 0: StartGame(); break;
            case 1: StartTutorial(); break;
            case 2: inControls = true; break;
            case 3: inSettings = true; settings.Open(); break;
            case 4: Quit(); break;
        }
    }

    void StartGame()
    {
        string scene = Application.CanStreamedLevelBeLoaded(lobbySceneName) ? lobbySceneName : battleSceneName;
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            message = $"씬 \"{scene}\"이 빌드 설정에 없습니다 (Greed Bound > 로비 씬 생성)";
            return;
        }
        SceneManager.LoadScene(scene);
    }

    void StartTutorial()
    {
        if (!Application.CanStreamedLevelBeLoaded(tutorialSceneName))
        {
            message = $"씬 \"{tutorialSceneName}\"이 빌드 설정에 없습니다 (Greed Bound > 튜토리얼 씬 생성)";
            return;
        }
        SceneManager.LoadScene(tutorialSceneName);
    }

    static void Quit()
    {
        GameSettings.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void OnGUI()
    {
        float width = MenuGui.Begin(out Matrix4x4 previous);
        MenuGui.Fill(width, 1080f, Background);

        if (inControls) controls.Draw(width);
        else if (inSettings) DrawSettings(width);
        else DrawMain(width);

        if (!string.IsNullOrEmpty(message)) GUI.Label(new Rect(0, 1000f, width, 40f), message, MenuGui.HintStyle);
        MenuGui.End(previous);
    }

    void DrawMain(float width)
    {
        GUI.Label(new Rect(0, 210f, width, 100f), titleText, MenuGui.TitleStyle);
        GUI.Label(new Rect(0, 310f, width, 40f), subtitleText, MenuGui.SubtitleStyle);

        for (int i = 0; i < Items.Length; i++) MenuGui.DrawItem(width, 470f + i * 66f, i == cursor, Items[i]);

        GUI.Label(new Rect(0, 820f, width, 40f), "↑↓ 선택   Enter 확인", MenuGui.HintStyle);

        // 저장된 기록 (첫 실행이면 비어 있음)
        string record = MetaProgress.Runs > 0
            ? $"영혼 {MetaProgress.Souls}   ·   최고 라운드 {MetaProgress.BestRound}   ·   도전 {MetaProgress.Runs}회"
            : "첫 도전";
        GUI.Label(new Rect(0, 900f, width, 40f), record, MenuGui.HintStyle);
    }

    void DrawSettings(float width)
    {
        GUI.Label(new Rect(0, 200f, width, 90f), "설정", MenuGui.TitleStyle);
        string[] items = settings.Items();
        for (int i = 0; i < items.Length; i++) MenuGui.DrawItem(width, 330f + i * 64f, i == settings.Cursor, items[i]);
        GUI.Label(new Rect(0, 810f, width, 40f), "↑↓ 선택   ←→ 변경   Enter 확인   ESC 돌아가기 (화면 설정은 취소)", MenuGui.HintStyle);
    }
}
