using UnityEngine;

// 상점 맵 좌표 (Spec 7장 "상점 맵 — Unity 구현 규칙"). 상점 씬 생성 도구·ShopManager가 같은 값을 쓰도록 한 곳에 모음.
// 넓은 화면(960×540 = 26.67 × 15u)으로 보며 가로 추적. 폭 48u(양끝 여유 공간 포함), 왼쪽에서 오른쪽으로:
//   입구(플레이어) → 모험가 NPC → 잡화점(가운데 x 0) → 출구 포탈
// 잡화점: 뒤 진열장 → 가운데 단 위의 상점 주인 → 그 앞에 물건 좌판 한 줄 (상점 주인 기준 가운데 정렬, 가격 오름차순으로 왼쪽부터)
// 지면 높이는 전투 맵(ArenaLayout)과 같음. Spec을 바꾸면 여기도 같이 바꿀 것.
// 폭을 바꾸면 메뉴 "Greed Bound > 상점 씬 생성"을 다시 실행해야 벽·카메라 경계가 맞음.
public static class ShopLayout
{
    public const int ViewWidth = 960;           // 상점 화면 기준 해상도 (PPU 36 → 26.67 × 15u)
    public const int ViewHeight = 540;

    public const float HalfWidth = 24f;         // x -24 ~ 24
    public const float PlayerStartX = -20f;     // 왼쪽 입구 (뒤로 4u 여유)
    public const float PortalX = 19f;           // 오른쪽 출구, 폭 1.2 (뒤로 약 4.4u 여유)

    public const int MaxStalls = 9;
    public const float StallSpacing = 2.8f;     // 멀리 있는 이름표 폭(약 2.6u)보다 넓게

    // 1층 한 줄이라 발판 없음
    public static readonly ArenaLayout.PlatformSpec[] Platforms = { };

    // 모험가 (입구 옆)
    public static readonly Vector2 AdventurerFeet = new Vector2(-17.5f, ArenaLayout.GroundTop);

    // 잡화점 가운데. 상점 주인은 좌판 이름표보다 높은 단 위에 서서 앞 좌판에 가려지지 않음
    public const float ShopCenterX = 0f;
    public const float KeeperStageHeight = 3.4f;
    public static readonly Vector2 ShopkeeperFeet = new Vector2(ShopCenterX, KeeperStageHeight);

    // total개 중 index번째(가격 오름차순) 좌판의 발 위치 — 상점 주인 기준 가운데 정렬
    public static Vector2 StallFeet(int index, int total)
    {
        return new Vector2(ShopCenterX + StallSpacing * (index - (total - 1) * 0.5f), ArenaLayout.GroundTop);
    }

    // 진열장 폭: 좌판 줄 양끝보다 2u씩 넓게
    public static float ShelfWidth(int total) => StallSpacing * Mathf.Max(0, total - 1) + 4f;
}
