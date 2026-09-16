// 전투 맵 좌표 (Spec 5장 "전투 맵 좌표"). 맵 생성 도구·카메라·스포너가 같은 값을 쓰도록 한 곳에 모음.
// Spec을 바꾸면 여기도 같이 바꿀 것.
public static class ArenaLayout
{
    public const float GroundTop = 0f;          // 지면 윗면 y
    public const float MapHalfWidth = 31.11f;   // 맵 x 범위 -31.11 ~ 31.11 (2800px ÷ 45 ÷ 2)
    public const float CameraY = 3.89f;         // 지면이 화면 하단 1.11u 위에 오는 카메라 높이 (size 5 기준)
    public const float PlatformThickness = 0.4f;

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
