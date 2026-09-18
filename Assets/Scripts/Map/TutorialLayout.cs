using UnityEngine;

// 튜토리얼 맵 좌표 (Spec 9장 "튜토리얼 씬 — Unity 구현 규칙"). 씬 생성 도구·TutorialManager가 같은 값을 씀.
// 폭 68u를 왼쪽에서 오른쪽으로 네 구역: ① 이동·점프 ② 대쉬 ③ 근접 ④ 단검·스킬·예고 → 출구 포탈.
// 지면 높이·물리·화면은 전투 맵과 같음(ArenaLayout) — 여기서 익힌 거리 감각이 실전과 같아야 한다.
// 값을 바꾸면 메뉴 "Greed Bound > 튜토리얼 씬 생성"을 다시 실행해야 벽·발판·카메라 경계가 맞는다.
public static class TutorialLayout
{
    public const float HalfWidth = 34f;          // x -34 ~ 34
    public const float PlayerStartX = -31f;

    // 구역을 통과했다고 보는 x (이 지점을 넘으면 다음 과제로)
    public const float JumpGateX = -17.5f;
    public const float DashGateX = -1.5f;

    // ① 이동·점프 — 계단 발판 3개. 한 칸 상승 0.9~1.0u (점프 최고점 3.42u 안쪽)
    // ② 대쉬 — 같은 높이 발판 두 개 사이 틈 7u. 점프 수평 거리 약 5.9u라 대쉬 없이는 못 건넌다
    public static readonly ArenaLayout.PlatformSpec[] Platforms =
    {
        new ArenaLayout.PlatformSpec(-26f, 1.3f, 3.4f),
        new ArenaLayout.PlatformSpec(-22.5f, 2.3f, 3.4f),
        new ArenaLayout.PlatformSpec(-19f, 3.2f, 3.4f),

        new ArenaLayout.PlatformSpec(-14f, 2.6f, 4f),   // 오른쪽 끝 -12
        new ArenaLayout.PlatformSpec(-3f, 2.6f, 4f),    // 왼쪽 끝 -5 → 틈 7u
    };

    // ③ 근접 — 허수아비 3대
    public static readonly float[] DummyX = { 2f, 5f, 8f };

    // ④ 단검 표적은 기둥 위(근접 사거리 밖), 스킬 표적은 지면
    public const float DaggerPillarX = 19f;
    public const float DaggerPillarHeight = 1.8f;
    public const float SkillTargetX = 24f;

    // 예고 연습용 적이 나오는 자리 (플레이어가 구역에 들어오면 등장)
    public const float EnemySpawnX = 29f;

    public static readonly Vector2 PortalFeet = new Vector2(31.5f, ArenaLayout.GroundTop);

    // 구역 뒤 바닥 표시 { 가운데 x, 폭 } — 어디까지가 한 과제인지 눈에 보이게
    public static readonly (string title, float centerX, float width)[] Zones =
    {
        ("① 이동 · 점프", -24f, 15f),
        ("② 대쉬", -8.5f, 14f),
        ("③ 근접 공격", 5f, 12f),
        ("④ 단검 · 스킬 · 예고", 23f, 18f),
    };

    public const float ZoneBackdropHeight = 4.2f;
}
