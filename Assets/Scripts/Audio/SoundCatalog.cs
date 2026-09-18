using UnityEngine;

// 효과음 종류 (Spec 8장 "사운드"). 에셋이 들어오기 전까지는 코드로 합성한 임시 소리를 씀
// (프로토타입도 Web Audio 합성이었음). 에셋 교체는 SoundManager의 clip 배열만 채우면 됨.
public enum SoundId
{
    Swing,        // 검 휘두르기
    Hit,          // 타격 적중
    Block,        // 방패에 막힘
    Kill,         // 적 처치
    Hurt,         // 플레이어 피격
    Dagger,       // 단검 투척
    Jump,
    Land,
    Dash,
    Potion,
    Skill,
    Warn,         // 예고(화염·낙뢰·폭발·조준) 공용 경고음
    Explosion,    // 시체 폭발·낙뢰 착탄
    CardPick,     // 카드 선택
    Relic,        // 유물 획득
    Buy,          // 구매 성공
    Deny,         // 구매 거부·사용 불가
    RoundClear,
    BossAppear,
    GameOver,
}

// 임시 효과음 합성 (8비트풍 사각파 + 잡음). 첫 재생 때 한 번 만들어 두고 다시 씀.
public static class SoundCatalog
{
    const int SampleRate = 22050;

    // 소리별 기본 음량 (설정의 효과음 음량이 여기에 곱해짐)
    public static float Volume(SoundId id) => id switch
    {
        SoundId.Swing => 0.35f,
        SoundId.RoundClear => 0.3f,
        SoundId.Hit => 0.5f,
        SoundId.Kill => 0.5f,
        SoundId.Hurt => 0.6f,
        SoundId.Warn => 0.4f,
        SoundId.Explosion => 0.55f,
        SoundId.GameOver => 0.6f,
        SoundId.BossAppear => 0.6f,
        _ => 0.45f,
    };

    public static AudioClip Build(SoundId id)
    {
        return id switch
        {
            SoundId.Swing => Make(id, 0.16f, (t, n) => Noise(t) * Env(n, 4f) * Sweep(n, 0.4f, 1f)),
            SoundId.Hit => Make(id, 0.12f, (t, n) => Square(t, Mathf.Lerp(200f, 70f, n)) * Env(n, 9f) + Noise(t) * Env(n, 18f) * 0.5f),
            SoundId.Block => Make(id, 0.16f, (t, n) => (Square(t, 1250f) + Square(t, 1650f)) * 0.4f * Env(n, 10f)),
            SoundId.Kill => Make(id, 0.3f, (t, n) => Square(t, Mathf.Lerp(320f, 60f, n)) * Env(n, 5f) + Noise(t) * Env(n, 9f) * 0.6f),
            SoundId.Hurt => Make(id, 0.28f, (t, n) => Square(t, Mathf.Lerp(240f, 110f, n) * (1f + 0.06f * Mathf.Sin(t * 60f))) * Env(n, 4f)),
            SoundId.Dagger => Make(id, 0.09f, (t, n) => Square(t, Mathf.Lerp(950f, 700f, n)) * Env(n, 14f)),
            SoundId.Jump => Make(id, 0.13f, (t, n) => Square(t, Mathf.Lerp(300f, 720f, n)) * Env(n, 7f)),
            SoundId.Land => Make(id, 0.1f, (t, n) => (Square(t, 90f) * 0.7f + Noise(t) * 0.6f) * Env(n, 16f)),
            SoundId.Dash => Make(id, 0.2f, (t, n) => Noise(t) * Env(n, 6f) * Sweep(n, 1f, 0.3f)),
            SoundId.Potion => Make(id, 0.32f, (t, n) => Square(t, Steps(n, 523f, 659f, 784f)) * Env(n, 3f)),
            SoundId.Skill => Make(id, 0.38f, (t, n) => (Square(t, 220f) + Square(t, 330f) * 0.7f) * 0.55f * Env(n, 3.5f)),
            SoundId.Warn => Make(id, 0.16f, (t, n) => Square(t, 1200f) * Env(Mathf.Repeat(n * 2f, 1f), 10f) * 0.8f),
            SoundId.Explosion => Make(id, 0.45f, (t, n) => (Noise(t) * 0.9f + Square(t, Mathf.Lerp(120f, 40f, n)) * 0.5f) * Env(n, 4f)),
            SoundId.CardPick => Make(id, 0.22f, (t, n) => Square(t, Steps(n, 660f, 880f)) * Env(n, 4f)),
            SoundId.Relic => Make(id, 0.5f, (t, n) => Square(t, Steps(n, 523f, 784f, 1046f, 1318f)) * Env(n, 2.5f)),
            SoundId.Buy => Make(id, 0.2f, (t, n) => Square(t, Steps(n, 880f, 1320f)) * Env(n, 5f)),
            SoundId.Deny => Make(id, 0.22f, (t, n) => Square(t, 140f * (1f + 0.2f * Mathf.Sin(t * 90f))) * Env(n, 5f)),
            SoundId.RoundClear => Make(id, 0.6f, (t, n) => Square(t, Steps(n, 523f, 659f, 784f, 1046f)) * Env(n, 1.8f)),
            SoundId.BossAppear => Make(id, 1f, (t, n) => (Square(t, Mathf.Lerp(70f, 55f, n)) * 0.8f + Noise(t) * 0.35f) * Env(n, 1.5f)),
            SoundId.GameOver => Make(id, 0.9f, (t, n) => Square(t, Steps(n, 392f, 330f, 262f)) * Env(n, 1.5f)),
            _ => Make(id, 0.1f, (t, n) => Square(t, 440f) * Env(n, 8f)),
        };
    }

    // t = 초, n = 0~1 진행도
    static AudioClip Make(SoundId id, float duration, System.Func<float, float, float> wave)
    {
        int samples = Mathf.Max(1, Mathf.RoundToInt(duration * SampleRate));
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)SampleRate;
            data[i] = Mathf.Clamp(wave(t, i / (float)samples), -1f, 1f) * 0.8f;
        }

        AudioClip clip = AudioClip.Create(id.ToString(), samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static float Square(float t, float frequency) => Mathf.Sin(t * frequency * 2f * Mathf.PI) >= 0f ? 1f : -1f;

    // 같은 소리는 항상 같게 들리도록 시간 기반 의사 난수
    static float Noise(float t)
    {
        float x = Mathf.Sin(t * 12543.7f) * 43758.5453f;
        return (x - Mathf.Floor(x)) * 2f - 1f;
    }

    static float Env(float n, float decay) => Mathf.Exp(-n * decay) * Mathf.Clamp01(n * 40f);  // 시작 클릭음 방지용 짧은 어택

    static float Sweep(float n, float from, float to) => Mathf.Lerp(from, to, n);

    // 진행도에 따라 음을 차례로 바꿈 (아르페지오)
    static float Steps(float n, params float[] frequencies)
    {
        int index = Mathf.Clamp(Mathf.FloorToInt(n * frequencies.Length), 0, frequencies.Length - 1);
        return frequencies[index];
    }
}
