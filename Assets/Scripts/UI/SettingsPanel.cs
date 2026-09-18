using System.Collections.Generic;
using UnityEngine;

// 설정 화면 내용 (Spec 9장 "일시정지·설정 — Unity 구현 규칙").
// 일시정지(PauseMenu)와 타이틀(TitleScreen)이 같은 화면을 쓰도록 분리한 부분. 임시 UI(OnGUI).
// 음량·수직 동기화는 즉시, 화면 모드·해상도는 "적용하고 돌아가기"에서 (바꾸는 도중 화면이 깜빡이지 않게)
public class SettingsPanel
{
    const float VolumeStep = 0.1f;
    const int ItemCount = 7;
    const int ApplyItem = 6;

    public int Cursor { get; private set; }

    private List<Vector2Int> resolutions;
    private int resolutionIndex;
    private bool pendingFullscreen;

    public void Open()
    {
        Cursor = 0;
        resolutions = GameSettings.ResolutionOptions();
        resolutionIndex = Mathf.Max(0, resolutions.IndexOf(GameSettings.ScreenSize));
        pendingFullscreen = GameSettings.Fullscreen;
    }

    // 돌아갈 때 저장. 화면 모드·해상도는 적용하지 않은 채 두면 취소됨(다시 열면 지금 값으로 시작)
    public void Close() => GameSettings.Save();

    // 입력 처리. 설정 화면을 닫아야 하면 false를 돌려줌
    public bool HandleInput(int vertical, int horizontal, bool submit)
    {
        if (vertical != 0) Cursor = (Cursor - vertical + ItemCount) % ItemCount;
        if (horizontal != 0) Change(horizontal);
        if (!submit) return true;

        if (Cursor == ApplyItem)
        {
            ApplyDisplay();
            return false;
        }
        Change(1);   // 켬/끔·다음 값
        return true;
    }

    void Change(int direction)
    {
        switch (Cursor)
        {
            case 0: GameSettings.SetMasterVolume(Step(GameSettings.MasterVolume, direction)); break;
            case 1: GameSettings.SetMusicVolume(Step(GameSettings.MusicVolume, direction)); break;
            case 2: GameSettings.SetSfxVolume(Step(GameSettings.SfxVolume, direction)); break;
            case 3: pendingFullscreen = !pendingFullscreen; break;
            case 4: resolutionIndex = (resolutionIndex + direction + resolutions.Count) % resolutions.Count; break;
            case 5: GameSettings.SetVSync(!GameSettings.VSync); break;
        }
    }

    static float Step(float value, int direction) => Mathf.Clamp01(Mathf.Round((value + direction * VolumeStep) * 10f) / 10f);

    // 에디터 Game 뷰에는 적용되지 않음 — 안내 문구는 부른 쪽이 표시
    public bool ApplyDisplay()
    {
        Vector2Int size = resolutions[resolutionIndex];
        if (size == GameSettings.ScreenSize && pendingFullscreen == GameSettings.Fullscreen) return false;
        GameSettings.SetDisplay(size, pendingFullscreen);
        return true;
    }

    public string[] Items()
    {
        Vector2Int size = resolutions[resolutionIndex];
        string displayNote = size != GameSettings.ScreenSize || pendingFullscreen != GameSettings.Fullscreen ? "  (적용 필요)" : "";
        return new[]
        {
            $"전체 음량   ◀ {Percent(GameSettings.MasterVolume)} ▶" + (GameSettings.Muted ? "  (음소거 중 — M)" : ""),
            $"배경음   ◀ {Percent(GameSettings.MusicVolume)} ▶",
            $"효과음   ◀ {Percent(GameSettings.SfxVolume)} ▶",
            $"화면 모드   ◀ {(pendingFullscreen ? "전체화면" : "창 모드")} ▶{displayNote}",
            $"해상도   ◀ {size.x} × {size.y} ▶{(pendingFullscreen ? "  (창 모드에서만)" : "")}",
            $"수직 동기화   ◀ {(GameSettings.VSync ? "켬" : "끔")} ▶",
            "적용하고 돌아가기",
        };
    }

    static string Percent(float value) => $"{Mathf.RoundToInt(value * 100f)}%";
}
