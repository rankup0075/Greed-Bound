using UnityEngine;
using UnityEngine.InputSystem;

// ESC 일시정지·설정 화면 + M 음소거 (Spec 9장 "일시정지·설정 — Unity 구현 규칙"). 임시 UI(OnGUI).
// 게임 시작 시 스스로 생겨 씬이 바뀌어도 유지 — 씬·프리팹 수정 불필요. 타이틀 씬에서는 열리지 않음.
// 열려 있는 동안 Time.timeScale 0, 입력 "Player" 맵을 꺼서 공격·Enter 등 게임 입력이 새지 않게 함.
// 조작: ↑↓ 항목, ←→ 값 변경, Enter 선택, ESC 뒤로/닫기
public class PauseMenu : MonoBehaviour
{
    public static bool IsOpen => instance != null && instance.open;

    static PauseMenu instance;

    static readonly string[] MainItems = { "계속하기", "조작법", "설정", "게임 종료" };

    private InputAction pauseAction, navigateAction, submitAction, muteAction;
    private InputActionMap playerMap;
    private readonly SettingsPanel settings = new SettingsPanel();
    private readonly ControlsPanel controls = new ControlsPanel();
    private bool open;
    private bool inSettings, inControls;
    private int cursor;
    private Vector2 lastNavigate;
    private float toastUntil;
    private string toast;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (instance != null) return;
        GameObject go = new GameObject("PauseMenu");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<PauseMenu>();
    }

    void Awake()
    {
        InputActionAsset actions = InputSystem.actions;
        playerMap = actions.FindActionMap("Player", throwIfNotFound: true);
        pauseAction = actions.FindAction("UI/Pause", throwIfNotFound: true);
        navigateAction = actions.FindAction("UI/Navigate", throwIfNotFound: true);
        submitAction = actions.FindAction("UI/Submit", throwIfNotFound: true);
        muteAction = actions.FindAction("Player/Mute", throwIfNotFound: true);
    }

    void OnDestroy()
    {
        if (open) Close();
        if (instance == this) instance = null;
    }

    void Update()
    {
        if (!open)
        {
            // 음소거는 타이틀에서도 되게 (타이틀 자체 메뉴는 TitleScreen이 처리)
            if (muteAction.WasPressedThisFrame())
            {
                GameSettings.SetMuted(!GameSettings.Muted);
                ShowToast(GameSettings.Muted ? "음소거" : "소리 켬");
            }
            if (pauseAction.WasPressedThisFrame() && !TitleScreen.IsActive) Open();
            return;
        }

        bool cancel = pauseAction.WasPressedThisFrame();
        if (cancel && !inSettings && !inControls)
        {
            Close();
            return;
        }

        // Navigate는 누르고 있는 동안 계속 값이 들어오므로 새로 누른 방향만 처리
        Vector2 nav = navigateAction.ReadValue<Vector2>();
        int vertical = Pressed(nav.y, lastNavigate.y);
        int horizontal = Pressed(nav.x, lastNavigate.x);
        lastNavigate = nav;
        bool submit = submitAction.WasPressedThisFrame();

        if (inControls)
        {
            if (!controls.HandleInput(cancel, submit)) { inControls = false; cursor = 1; }
            return;
        }

        if (inSettings)
        {
            if (cancel || !settings.HandleInput(vertical, horizontal, submit)) LeaveSettings();
            return;
        }

        if (vertical != 0) cursor = (cursor - vertical + MainItems.Length) % MainItems.Length;  // 위(+1)가 목록에서는 앞쪽
        if (!submit) return;
        switch (cursor)
        {
            case 0: Close(); break;
            case 1: inControls = true; break;
            case 2: EnterSettings(); break;
            case 3: Quit(); break;
        }
    }

    public static int Pressed(float now, float before)
    {
        int n = Mathf.Abs(now) > 0.5f ? (int)Mathf.Sign(now) : 0;
        int b = Mathf.Abs(before) > 0.5f ? (int)Mathf.Sign(before) : 0;
        return n != b ? n : 0;
    }

    void Open()
    {
        open = true;
        inSettings = false;
        inControls = false;
        cursor = 0;
        lastNavigate = navigateAction.ReadValue<Vector2>();
        Time.timeScale = 0f;
        playerMap.Disable();
    }

    void Close()
    {
        open = false;
        inSettings = false;
        inControls = false;
        Time.timeScale = 1f;
        playerMap.Enable();
    }

    void EnterSettings()
    {
        inSettings = true;
        settings.Open();
    }

    void LeaveSettings()
    {
        settings.Close();
        inSettings = false;
        inControls = false;
        cursor = 2;
#if UNITY_EDITOR
        ShowToast("에디터에서는 해상도·화면 모드가 적용되지 않습니다 (빌드에서 적용)");
#endif
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

    void ShowToast(string text)
    {
        toast = text;
        toastUntil = Time.unscaledTime + 1.5f;
    }

    void OnGUI()
    {
        GUI.depth = -1000;  // 다른 임시 UI 위에
        float width = MenuGui.Begin(out Matrix4x4 previous);

        if (open)
        {
            MenuGui.Fill(width, 1080f, new Color(0f, 0f, 0f, 0.7f));
            if (inControls) controls.Draw(width);
            else if (inSettings) DrawSettings(width);
            else DrawMain(width);
        }

        if (toast != null && Time.unscaledTime < toastUntil)
            GUI.Label(new Rect(0, 960f, width, 60f), toast, MenuGui.HintStyle);

        MenuGui.End(previous);
    }

    void DrawMain(float width)
    {
        GUI.Label(new Rect(0, 300f, width, 90f), "일시정지", MenuGui.TitleStyle);
        for (int i = 0; i < MainItems.Length; i++) MenuGui.DrawItem(width, 440f + i * 70f, i == cursor, MainItems[i]);
        GUI.Label(new Rect(0, 760f, width, 40f), "↑↓ 선택   Enter 확인   ESC 닫기", MenuGui.HintStyle);
        if (cursor == 3) GUI.Label(new Rect(0, 800f, width, 40f), "진행 중인 런은 저장되지 않습니다", MenuGui.HintStyle);
    }

    void DrawSettings(float width)
    {
        GUI.Label(new Rect(0, 200f, width, 90f), "설정", MenuGui.TitleStyle);
        string[] items = settings.Items();
        for (int i = 0; i < items.Length; i++) MenuGui.DrawItem(width, 330f + i * 64f, i == settings.Cursor, items[i]);
        GUI.Label(new Rect(0, 810f, width, 40f), "↑↓ 선택   ←→ 변경   Enter 확인   ESC 돌아가기 (화면 설정은 취소)", MenuGui.HintStyle);
    }
}
