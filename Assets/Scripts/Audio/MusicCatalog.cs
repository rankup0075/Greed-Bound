using UnityEngine;

// 배경음 종류 (Spec 8장 "사운드"). 효과음과 같은 방식으로, 음원 에셋이 들어오기 전까지는
// 코드로 합성한 8비트풍 루프를 쓴다. 교체는 MusicManager의 clips 배열만 채우면 됨.
public enum MusicId
{
    Menu,     // 타이틀
    Battle,   // 전투
    Town,     // 로비 · 상점 · 튜토리얼
}

// 임시 배경음 합성. 한 마디씩 음을 찍어 만드는 단순한 시퀀서 —
// 리드(사각파) + 베이스(한 옥타브 아래) + 전투에만 하이햇(잡음).
// 각 음은 스텝 안에서 감쇠하므로 루프 이음매에서 딱 소리가 나지 않는다.
public static class MusicCatalog
{
    const int SampleRate = 22050;
    const int Rest = -999;              // 쉼표

    // 배경음은 효과음을 덮지 않게 낮게 (설정의 배경음 음량이 여기에 곱해짐)
    public static float Volume(MusicId id) => id switch
    {
        MusicId.Battle => 0.22f,
        MusicId.Town => 0.18f,
        _ => 0.2f,
    };

    // 음 = A4(440Hz) 기준 반음 수. 리드는 스텝 하나, 베이스는 스텝 둘을 차지한다
    public static AudioClip Build(MusicId id)
    {
        switch (id)
        {
            // 타이틀 — 느리고 차분한 단조 아르페지오 (스텝 0.5초 × 16 = 8초)
            case MusicId.Menu:
                return Make(id, 0.5f,
                    lead: new[] { -12, -5, 0, 3, 0, -5, -12, Rest, -10, -3, 2, 5, 2, -3, -10, Rest },
                    bass: new[] { -24, -24, -24, -24, -22, -22, -22, -22 },
                    leadDecay: 2.6f, noise: 0f);

            // 전투 — 8분음표 베이스 + 빠른 아르페지오 (스텝 0.25초 × 16 = 4초)
            case MusicId.Battle:
                return Make(id, 0.25f,
                    lead: new[] { 0, 3, 7, 3, 0, 3, 7, 10, -2, 1, 5, 1, -2, 1, 5, 8 },
                    bass: new[] { -24, -24, -24, -24, -26, -26, -26, -26 },
                    leadDecay: 7f, noise: 0.06f);

            // 로비·상점·튜토리얼 — 느긋한 장조 (스텝 0.4초 × 12 = 4.8초)
            default:
                return Make(id, 0.4f,
                    lead: new[] { 0, 4, 7, 4, 2, 5, 9, 5, 0, 4, 7, 12 },
                    bass: new[] { -24, -24, -20, -20, -24, -24 },
                    leadDecay: 3.2f, noise: 0f);
        }
    }

    static AudioClip Make(MusicId id, float stepSeconds, int[] lead, int[] bass, float leadDecay, float noise)
    {
        float loopSeconds = lead.Length * stepSeconds;
        float bassStep = loopSeconds / bass.Length;

        int samples = Mathf.RoundToInt(loopSeconds * SampleRate);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)SampleRate;

            float value = Voice(t, lead, stepSeconds, 0, leadDecay) * 0.45f;
            value += Voice(t, bass, bassStep, 0, 1.6f) * 0.5f;

            // 하이햇 — 스텝마다 아주 짧은 잡음
            if (noise > 0f)
            {
                float p = Mathf.Repeat(t, stepSeconds) / stepSeconds;
                value += Noise(t) * Mathf.Exp(-p * 26f) * noise;
            }

            data[i] = Mathf.Clamp(value, -1f, 1f) * 0.8f;
        }

        AudioClip clip = AudioClip.Create($"BGM_{id}", samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // 스텝 시퀀스 한 줄을 사각파로
    static float Voice(float t, int[] notes, float stepSeconds, int transpose, float decay)
    {
        int index = Mathf.FloorToInt(t / stepSeconds) % notes.Length;
        int note = notes[index];
        if (note == Rest) return 0f;

        float progress = Mathf.Repeat(t, stepSeconds) / stepSeconds;
        float envelope = Mathf.Exp(-progress * decay) * Mathf.Clamp01(progress * 80f);   // 시작 클릭음 방지
        return Square(t, Frequency(note + transpose)) * envelope;
    }

    static float Frequency(int semitonesFromA4) => 440f * Mathf.Pow(2f, semitonesFromA4 / 12f);

    static float Square(float t, float frequency) => Mathf.Sin(t * frequency * 2f * Mathf.PI) >= 0f ? 1f : -1f;

    static float Noise(float t)
    {
        float x = Mathf.Sin(t * 12543.7f) * 43758.5453f;
        return (x - Mathf.Floor(x)) * 2f - 1f;
    }
}
