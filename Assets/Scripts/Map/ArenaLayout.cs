using UnityEngine;

// 전투 맵 좌표 (Spec 5장 "전투 맵 좌표"). 맵 생성 도구·카메라·스포너가 같은 값을 쓰도록 한 곳에 모음.
// Spec을 바꾸면 여기도 같이 바꿀 것.
public static class ArenaLayout
{
    public const float GroundTop = 0f;          // 지면 윗면 y
    public const float MapHalfWidth = 31.11f;   // 맵 x 범위 -31.11 ~ 31.11 (2800px ÷ 45 ÷ 2)
    public const float GroundBottomMargin = 1.11f;  // 지면이 화면 하단에서 떨어진 거리
    public const float CameraY = 3.89f;         // 지면이 화면 하단 1.11u 위에 오는 카메라 높이 (size 5 기준)
    public const float PlatformThickness = 0.4f;

    // 발판 아래를 지나갈 수 있는 가장 낮은 높이 (아래 바닥 윗면 → 위 발판 아랫면).
    // 이보다 키가 큰 적은 발판에 끼므로 발판과 충돌하지 않게 한다 (Enemy). 지금 배치에서는 2.27u
    public static float MinPlatformClearance()
    {
        float min = float.MaxValue;
        foreach (PlatformSpec upper in Platforms)
        {
            float bottom = upper.top - PlatformThickness;
            float floor = GroundTop;                       // 아래에 발판이 없으면 지면 기준
            foreach (PlatformSpec lower in Platforms)
            {
                if (lower.top >= bottom || lower.top <= floor) continue;
                // **가로로 겹치는 발판만** — 다른 자리에 있는 발판은 이 발판 아래를 지나가는 것과 무관.
                // (이 조건이 없으면 -14의 3.8 발판 아래에 -5.5의 2.9 발판을 넣어 0.5u로 계산되어
                //  모든 적이 "너무 크다"고 판정돼 발판 추격을 못 하게 됨 — 2026-09-18 버그)
                if (Mathf.Abs(lower.x - upper.x) >= (lower.width + upper.width) * 0.5f) continue;
                floor = lower.top;
            }
            min = Mathf.Min(min, bottom - floor);
        }
        return min;
    }

    // 캐릭터를 지면 위에 세우는 좌표 (몸 크기가 바뀌어도 발이 지면에 닿게 — Spec 8장 "캐릭터 크기 배율")
    public static Vector2 GroundStart(GameObject character, float x)
    {
        float halfHeight = 0.5f;
        if (character != null && character.TryGetComponent(out BoxCollider2D box)) halfHeight = box.size.y * 0.5f;
        return new Vector2(x, GroundTop + halfHeight + 0.02f);
    }

    public struct PlatformSpec
    {
        public readonly float x;      // 중심 x
        public readonly float top;    // 윗면 y (지면 기준 높이)
        public readonly float width;

        public PlatformSpec(float x, float top, float width)
        {
            this.x = x;
            this.top = top;
            this.width = width;
        }
    }

    // 좌우 대칭 8개. 하단 6개(2.67 ~ 3.8), 상단 2개(6.67 / 6.78)
    public static readonly PlatformSpec[] Platforms =
    {
        new PlatformSpec(-24f,  2.67f, 4f),
        new PlatformSpec(-14f,  3.8f,  4f),
        new PlatformSpec(-5.5f, 2.9f,  3.5f),
        new PlatformSpec(5.5f,  2.9f,  3.5f),
        new PlatformSpec(14f,   3.8f,  4f),
        new PlatformSpec(24f,   2.67f, 4f),
        new PlatformSpec(-9.5f, 6.67f, 4.5f),
        new PlatformSpec(9.5f,  6.78f, 4.5f),
    };
}
