using System.Collections.Generic;
using UnityEngine;

// 설정 값 저장·적용 (Spec 9장 "설정 — Unity 구현 규칙"). PlayerPrefs에 저장, 게임 시작 시 자동 적용.
// 음량: 전체는 AudioListener.volume에 바로 적용. 배경음·효과음은 사운드가 들어오면 재생 코드가 곱해서 씀
// (MusicVolume, SfxVolume). 음소거(M)는 전체 음량만 0으로 — 슬라이더 값은 유지.
public static class GameSettings
{
    const string MasterKey = "settings.masterVolume";
    const string MusicKey = "settings.musicVolume";
    const string SfxKey = "settings.sfxVolume";
    const string MutedKey = "settings.muted";
    const string FullscreenKey = "settings.fullscreen";
    const string WidthKey = "settings.width";
    const string HeightKey = "settings.height";
    const string VSyncKey = "settings.vsync";

    // 기준 해상도 640×360의 정수배만 제공 (픽셀아트가 번지지 않게, Spec 8장)
    public static readonly Vector2Int BaseResolution = new Vector2Int(640, 360);

    public static float MasterVolume { get; private set; } = 1f;
    public static float MusicVolume { get; private set; } = 0.8f;
    public static float SfxVolume { get; private set; } = 1f;
    public static bool Muted { get; private set; }
    public static bool Fullscreen { get; private set; } = true;
    public static Vector2Int ScreenSize { get; private set; } = new Vector2Int(1920, 1080);
    public static bool VSync { get; private set; } = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void LoadOnStart()
    {
        MasterVolume = PlayerPrefs.GetFloat(MasterKey, 1f);
        MusicVolume = PlayerPrefs.GetFloat(MusicKey, 0.8f);
        SfxVolume = PlayerPrefs.GetFloat(SfxKey, 1f);
        Muted = PlayerPrefs.GetInt(MutedKey, 0) == 1;
        Fullscreen = PlayerPrefs.GetInt(FullscreenKey, 1) == 1;
        VSync = PlayerPrefs.GetInt(VSyncKey, 1) == 1;

        // 처음 실행이면 모니터에 들어가는 가장 큰 정수배
        Vector2Int fallback = LargestFitting();
        ScreenSize = new Vector2Int(PlayerPrefs.GetInt(WidthKey, fallback.x), PlayerPrefs.GetInt(HeightKey, fallback.y));

        ApplyAudio();
        ApplyVSync();
#if !UNITY_EDITOR
        ApplyDisplay();  // 에디터 Game 뷰에는 해상도·전체화면이 적용되지 않음
#endif
    }

    public static void SetMasterVolume(float value) { MasterVolume = Mathf.Clamp01(value); ApplyAudio(); }
    public static void SetMusicVolume(float value) { MusicVolume = Mathf.Clamp01(value); }
    public static void SetSfxVolume(float value) { SfxVolume = Mathf.Clamp01(value); }
    public static void SetMuted(bool muted) { Muted = muted; ApplyAudio(); Save(); }
    public static void SetVSync(bool on) { VSync = on; ApplyVSync(); }

    public static void SetDisplay(Vector2Int resolution, bool fullscreen)
    {
        ScreenSize = resolution;
        Fullscreen = fullscreen;
        ApplyDisplay();
    }

    public static void Save()
    {
        PlayerPrefs.SetFloat(MasterKey, MasterVolume);
        PlayerPrefs.SetFloat(MusicKey, MusicVolume);
        PlayerPrefs.SetFloat(SfxKey, SfxVolume);
        PlayerPrefs.SetInt(MutedKey, Muted ? 1 : 0);
        PlayerPrefs.SetInt(FullscreenKey, Fullscreen ? 1 : 0);
        PlayerPrefs.SetInt(WidthKey, ScreenSize.x);
        PlayerPrefs.SetInt(HeightKey, ScreenSize.y);
        PlayerPrefs.SetInt(VSyncKey, VSync ? 1 : 0);
        PlayerPrefs.Save();
    }

    static void ApplyAudio() => AudioListener.volume = Muted ? 0f : MasterVolume;

    static void ApplyVSync() => QualitySettings.vSyncCount = VSync ? 1 : 0;

    static void ApplyDisplay()
    {
        Screen.SetResolution(ScreenSize.x, ScreenSize.y, Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
    }

    // 640×360의 ×2 ~ 모니터에 들어가는 최대 배수 (창 모드에서 ×1은 너무 작아 제외)
    public static List<Vector2Int> ResolutionOptions()
    {
        List<Vector2Int> options = new List<Vector2Int>();
        Vector2Int max = LargestFitting();
        for (int scale = 2; BaseResolution.x * scale <= max.x; scale++)
            options.Add(BaseResolution * scale);
        if (options.Count == 0) options.Add(max);
        return options;
    }

    static Vector2Int LargestFitting()
    {
        Resolution display = Screen.currentResolution;
        int scale = Mathf.Max(1, Mathf.Min(display.width / BaseResolution.x, display.height / BaseResolution.y));
        return BaseResolution * scale;
    }
}
