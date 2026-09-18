using UnityEngine;

// 로비 맵 좌표 (Spec 6-3장 "로비 — Unity 구현 규칙"). 로비 씬 생성 도구·LobbyManager가 같은 값을 씀.
// 넓은 화면(960×540 = 26.67 × 15u)으로 보며 가로 추적. 폭 56u(양끝 여유 공간 포함), 왼쪽에서 오른쪽으로:
//   시작 → 「기술의 전당」 스킬 → 「보급소」 1층 시작 보급 / 2층 상점 강화 → 「운명의 서고」 카드 → 출전 포탈
// 구역마다 제단 6칸(층마다 6칸)을 잡아 두어, 스킬·강화가 늘어나도 구역 폭은 그대로. 제단은 구역 가운데 정렬.
// 지면 높이는 전투 맵(ArenaLayout)과 같음. Spec을 바꾸면 여기도 같이 바꿀 것.
// 폭·발판을 바꾸면 메뉴 "Greed Bound > 로비 씬 생성"을 다시 실행해야 벽·발판·카메라 경계가 맞음.
public static class LobbyLayout
{
    public const int ViewWidth = 960;           // 로비 화면 기준 해상도 (PPU 36 → 26.67 × 15u)
    public const int ViewHeight = 540;

    public const float HalfWidth = 28f;         // x -28 ~ 28 — 시작 왼쪽·출전 포탈 오른쪽에도 여유 공간
    public const float PlayerStartX = -22.5f;   // 왼쪽 끝
    public const float UpperFloorY = 3.1f;      // 보급소 2층 발판 윗면
    public const float Spacing = 2.3f;          // 제단 간격 (멀리 있는 이름표 폭 2.2u보다 넓게)
    public const int SlotsPerZone = 6;          // 구역(층)마다 잡아 둔 칸 수

    const float SkillFirstX = -20.5f;           // 기술의 전당 x -20.5 ~ -9
    const float SupplyFirstX = -5.2f;           // 보급소 x -5.2 ~ 6.3
    const float CardFirstX = 9.6f;              // 운명의 서고 x 9.6 ~ 21.1
    const float ZoneMargin = 1.1f;              // 구역 바닥 표시가 양끝 제단보다 넓은 만큼

    // 보급소 2층 발판 (구역 폭과 같게)
    public static readonly ArenaLayout.PlatformSpec[] Platforms =
    {
        new ArenaLayout.PlatformSpec(ZoneCenterX(Category.Supplies), UpperFloorY, ZoneWidth()),
    };

    // 출전 포탈 (오른쪽 끝)
    public static readonly Vector2 PortalFeet = new Vector2(23.2f, ArenaLayout.GroundTop);

    public enum Category { Skills, Supplies, Cards }

    public static string CategoryTitle(Category category)
    {
        switch (category)
        {
            case Category.Skills: return "기술의 전당";
            case Category.Supplies: return "보급소";
            default: return "운명의 서고";
        }
    }

    public static string CategorySubtitle(Category category)
    {
        switch (category)
        {
            case Category.Skills: return "스킬 해금·선택";
            case Category.Supplies: return "1층 시작 보급 · 2층 상점 강화";
            default: return "카드 선택 강화";
        }
    }

    static float FirstX(Category category) =>
        category == Category.Skills ? SkillFirstX : category == Category.Supplies ? SupplyFirstX : CardFirstX;

    public static float ZoneCenterX(Category category) => FirstX(category) + Spacing * (SlotsPerZone - 1) * 0.5f;
    public static float ZoneWidth() => Spacing * (SlotsPerZone - 1) + ZoneMargin * 2f;

    // 구역 제목 위치 (말풍선 아래 가운데 기준). 보급소는 2층 제단 위
    public static Vector2 CategoryTitleAnchor(Category category)
    {
        float y = category == Category.Supplies ? 6.6f : 3.6f;
        return new Vector2(ZoneCenterX(category), y);
    }

    // 구역 뒤 바닥 표시 높이 (보급소는 2층까지)
    public static float ZoneBackdropHeight(Category category) => category == Category.Supplies ? 6.3f : 3.2f;

    // count개 중 slot번째 제단의 x — 구역 가운데 정렬 (6칸 안에서 개수만큼 가운데로 모임)
    static float CenteredX(Category category, int slot, int count) => ZoneCenterX(category) + Spacing * (slot - (count - 1) * 0.5f);

    // 스킬 제단 발 위치 (SkillCatalog.All 순서)
    public static Vector2 SkillFeet(int index) =>
        new Vector2(CenteredX(Category.Skills, index, SkillCatalog.All.Length), ArenaLayout.GroundTop);

    // 영구 강화의 구역·층: 운명 뒤집기·탐욕의 눈·넓은 시야는 운명의 서고, 상인의 안목·상점 갱신은 보급소 2층, 나머지는 보급소 1층
    public static Category CategoryOf(MetaUpgradeId id)
    {
        return id == MetaUpgradeId.FateReroll || id == MetaUpgradeId.GreedEye || id == MetaUpgradeId.WideSight
            ? Category.Cards : Category.Supplies;
    }

    static bool IsUpperFloor(MetaUpgradeId id) => id == MetaUpgradeId.MerchantEye || id == MetaUpgradeId.ShopRefresh;

    // 영구 강화 제단 발 위치 (MetaCatalog.All 순서). 같은 구역·층 안에서 카탈로그 순서대로, 가운데 정렬
    public static Vector2 AltarFeet(int index)
    {
        MetaUpgradeId id = MetaCatalog.All[index].id;
        Category category = CategoryOf(id);
        bool upper = IsUpperFloor(id);

        int slot = 0, count = 0;
        for (int i = 0; i < MetaCatalog.All.Length; i++)
        {
            MetaUpgradeId other = MetaCatalog.All[i].id;
            if (CategoryOf(other) != category || IsUpperFloor(other) != upper) continue;
            if (i < index) slot++;
            count++;
        }
        return new Vector2(CenteredX(category, slot, count), upper ? UpperFloorY : ArenaLayout.GroundTop);
    }
}
