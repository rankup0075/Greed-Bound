using UnityEngine;
using UnityEngine.SceneManagement;

// 배경음 재생 (Spec 8장 "사운드 — Unity 구현 규칙"). 게임 시작 시 스스로 생겨 씬이 바뀌어도 유지 — 씬·프리팹 수정 불필요.
// 씬에 있는 것으로 트랙을 고른다(씬 이름에 기대지 않음): TitleScreen → 메뉴 / RoundManager → 전투 / 그 밖(로비·상점·튜토리얼) → 마을.
// AudioSource 두 개를 번갈아 쓰며 1초 교차 페이드. 음량은 설정의 배경음 음량 × 트랙 기본 음량이고,
// 전체 음량·음소거(M)는 AudioListener가 처리한다 (GameSettings).
// 지금은 MusicCatalog가 코드로 합성한 임시 루프를 씀. 에셋이 들어오면 clips에 MusicId 순서대로 넣으면 그쪽이 우선.
public class MusicManager : MonoBehaviour
{
    const float FadeTime = 1f;

    public static MusicManager Instance { get; private set; }

    [Tooltip("에셋으로 교체할 때 여기에 MusicId 순서대로 넣으면 합성 루프 대신 재생됨")]
    public AudioClip[] clips;

    private AudioSource[] sources;      // 0·1을 번갈아 씀
    private readonly AudioClip[] built = new AudioClip[System.Enum.GetValues(typeof(MusicId)).Length];
    private int active;
    private MusicId current;
    private bool playing;
    private float fade = 1f;            // 0 → 1로 진행. 1이면 교차 페이드 끝

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (Instance != null) return;
        GameObject go = new GameObject("MusicManager");
        DontDestroyOnLoad(go);
        go.AddComponent<MusicManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        sources = new AudioSource[2];
        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            sources[i] = source;
        }
    }

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start() => Play(TrackForScene());

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Play(TrackForScene());

    // 씬에 있는 것으로 트랙 결정 — 씬 이름을 바꿔도 따라옴
    static MusicId TrackForScene()
    {
        if (FindFirstObjectByType<TitleScreen>() != null) return MusicId.Menu;
        if (FindFirstObjectByType<RoundManager>() != null) return MusicId.Battle;
        return MusicId.Town;
    }

    public static void Play(MusicId id)
    {
        if (Instance != null) Instance.PlayTrack(id);
    }

    public void PlayTrack(MusicId id)
    {
        if (sources == null) return;
        if (playing && current == id) return;

        AudioClip clip = Clip(id);
        if (clip == null) return;

        int next = playing ? 1 - active : active;
        sources[next].clip = clip;
        sources[next].volume = 0f;
        sources[next].Play();

        active = next;
        current = id;
        fade = playing ? 0f : 1f;   // 처음 재생은 바로 들어옴
        playing = true;
        ApplyVolume();
    }

    void Update()
    {
        if (!playing) return;

        // 일시정지(timeScale 0) 중에도 페이드가 이어지도록 unscaled
        if (fade < 1f) fade = Mathf.Min(1f, fade + Time.unscaledDeltaTime / FadeTime);
        ApplyVolume();   // 설정에서 음량을 바꾸면 바로 반영
    }

    void ApplyVolume()
    {
        float target = MusicCatalog.Volume(current) * GameSettings.MusicVolume;
        sources[active].volume = target * fade;

        AudioSource other = sources[1 - active];
        other.volume = target * (1f - fade);
        if (fade >= 1f && other.isPlaying) other.Stop();
    }

    AudioClip Clip(MusicId id)
    {
        int index = (int)id;
        if (clips != null && index < clips.Length && clips[index] != null) return clips[index];

        if (built[index] == null) built[index] = MusicCatalog.Build(id);
        return built[index];
    }
}
