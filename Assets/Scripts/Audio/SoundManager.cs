using System.Collections.Generic;
using UnityEngine;

// 효과음 재생 (Spec 8장 "사운드"). 게임 시작 시 스스로 생겨 씬이 바뀌어도 유지 — 씬·프리팹 수정 불필요.
// 지금은 SoundCatalog가 코드로 합성한 임시 소리를 씀. 에셋이 들어오면 clips에 AudioClip을 넣으면 그쪽이 우선.
// 음량: 설정의 효과음 음량 × 소리별 기본 음량. 전체 음량·음소거(M)는 AudioListener가 처리 (GameSettings).
public class SoundManager : MonoBehaviour
{
    const int Voices = 10;              // 동시에 겹칠 수 있는 수
    const float RepeatGuard = 0.03f;    // 같은 소리가 이 시간 안에 또 나면 무시 (여러 마리 동시 타격 등)

    public static SoundManager Instance { get; private set; }

    [Tooltip("에셋으로 교체할 때 여기에 SoundId 순서대로 넣으면 합성 소리 대신 재생됨")]
    public AudioClip[] clips;

    private readonly Dictionary<SoundId, AudioClip> built = new Dictionary<SoundId, AudioClip>();
    private readonly Dictionary<SoundId, float> lastPlayed = new Dictionary<SoundId, float>();
    private AudioSource[] sources;
    private int next;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (Instance != null) return;
        GameObject go = new GameObject("SoundManager");
        DontDestroyOnLoad(go);
        go.AddComponent<SoundManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        sources = new AudioSource[Voices];
        for (int i = 0; i < Voices; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;   // 화면이 좁아 위치별 소리는 쓰지 않음
            sources[i] = source;
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // 어디서든 SoundManager.Play(SoundId.Hit) 한 줄로 호출
    public static void Play(SoundId id, float pitchVariance = 0.06f)
    {
        if (Instance != null) Instance.PlaySound(id, pitchVariance);
    }

    public void PlaySound(SoundId id, float pitchVariance)
    {
        if (sources == null) return;
        if (lastPlayed.TryGetValue(id, out float time) && Time.unscaledTime - time < RepeatGuard) return;
        lastPlayed[id] = Time.unscaledTime;

        AudioClip clip = Clip(id);
        if (clip == null) return;

        AudioSource source = sources[next];
        next = (next + 1) % sources.Length;
        source.clip = clip;
        source.volume = SoundCatalog.Volume(id) * GameSettings.SfxVolume;
        source.pitch = 1f + Random.Range(-pitchVariance, pitchVariance);   // 같은 소리가 반복돼도 덜 지루하게
        source.Play();
    }

    AudioClip Clip(SoundId id)
    {
        int index = (int)id;
        if (clips != null && index < clips.Length && clips[index] != null) return clips[index];

        if (!built.TryGetValue(id, out AudioClip clip))
        {
            clip = SoundCatalog.Build(id);
            built[id] = clip;
        }
        return clip;
    }
}
