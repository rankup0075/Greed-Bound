using UnityEngine;

// 상점 품목 종류 (Spec 6-2장). 값 고정
public enum ShopItemId
{
    HealthPotion = 0,     // 체력 물약
    HealingSpring = 1,    // 치유의 샘물
    DaggerBundle = 2,     // 단검 다발
    ForgedDaggerTip = 3,  // 벼려진 단검 촉
    HeavyBlade = 4,       // 묵직한 검신
    LightWrist = 5,       // 가벼운 손목
    SteelHeart = 6,       // 강철 심장
    LeatherBoots = 7,     // 가죽 장화
    WardCharm = 8,        // 수호 부적
    GoldenScale = 9,      // 황금 저울
}

// 상점 품목 목록·가격·효과 (Spec 6-2장 + 7장 "상점 맵 — Unity 구현 규칙").
// 유물과 같이 효과가 전부 코드라 ScriptableObject 없이 여기서 관리.
public static class ShopCatalog
{
    public struct Item
    {
        public readonly ShopItemId id;
        public readonly string name;
        public readonly string description;
        public readonly int basePrice;
        public readonly Color color;     // 좌판 위에 떠 있는 아이콘 색 (스프라이트 교체 전 임시)

        public Item(ShopItemId id, string name, string description, int basePrice, Color color)
        {
            this.id = id;
            this.name = name;
            this.description = description;
            this.basePrice = basePrice;
            this.color = color;
        }
    }

    // 진열 수는 좌판 수(ShopLayout.StallX 5개, 상인의 안목이면 StallX6 6개)
    public const float PriceGrowthPerRound = 0.27f;  // 가격 = 기본가 × (1 + 0.27 × (라운드 - 3))
    public const int FirstShopRound = 3;             // 첫 상점은 기본가

    public static readonly Item[] All =
    {
        new Item(ShopItemId.HealthPotion,    "체력 물약",     "보유 +1 (전투 중 1로 사용, +45 회복). 방문당 5개까지", 60,  new Color(0.9f, 0.25f, 0.3f)),
        new Item(ShopItemId.HealingSpring,   "치유의 샘물",   "즉시 전체 회복",                     125, new Color(0.35f, 0.85f, 0.95f)),
        new Item(ShopItemId.DaggerBundle,    "단검 다발",     "단검 최대치 +2",                     165, new Color(0.75f, 0.78f, 0.82f)),
        new Item(ShopItemId.ForgedDaggerTip, "벼려진 단검 촉", "단검 공격력 +30%",                   275, new Color(0.55f, 0.6f, 0.7f)),
        new Item(ShopItemId.HeavyBlade,      "묵직한 검신",   "근접 공격력 +18%",                   295, new Color(0.85f, 0.55f, 0.3f)),
        new Item(ShopItemId.LightWrist,      "가벼운 손목",   "공격 속도 +15%",                     270, new Color(0.95f, 0.9f, 0.5f)),
        new Item(ShopItemId.SteelHeart,      "강철 심장",     "최대 체력 +25, 즉시 25 회복",         290, new Color(0.8f, 0.2f, 0.45f)),
        new Item(ShopItemId.LeatherBoots,    "가죽 장화",     "이동 +10%, 점프 +6%",                235, new Color(0.6f, 0.42f, 0.25f)),
        new Item(ShopItemId.WardCharm,       "수호 부적",     "받는 피해 -10%",                     340, new Color(0.4f, 0.55f, 0.95f)),
        new Item(ShopItemId.GoldenScale,     "황금 저울",     "골드 획득 +20%",                     320, new Color(1f, 0.8f, 0.2f)),
    };

    // 반올림한 정수 골드. round = 방금 클리어한 라운드
    public static int Price(Item item, int round)
    {
        int roundsAfterFirstShop = Mathf.Max(0, round - FirstShopRound);
        return Mathf.RoundToInt(item.basePrice * (1f + PriceGrowthPerRound * roundsAfterFirstShop));
    }

    // 한 방문에 같은 좌판에서 살 수 있는 수: 체력 물약 5, 그 외 1
    public const int PotionStockPerVisit = 5;
    public static int StockPerVisit(ShopItemId id) => id == ShopItemId.HealthPotion ? PotionStockPerVisit : 1;

    // 사도 의미가 없을 때 이유 (구매 거부 안내), 살 수 있으면 null
    public static string BlockReason(ShopItemId id, PlayerHealth health)
    {
        if (id == ShopItemId.HealingSpring && health != null && health.CurrentHealth >= health.MaxHealth)
            return "체력이 이미 가득 찼습니다";
        return null;
    }

    // 구매 즉시 적용 (런 끝까지 유지). 유물과 같은 PlayerStats 배율에 곱으로 누적
    public static void Apply(ShopItemId id, PlayerHealth health, RunState run)
    {
        if (health == null) return;
        PlayerStats stats = health.GetComponent<PlayerStats>();
        if (stats == null) return;

        switch (id)
        {
            case ShopItemId.HealthPotion: run.potions++; break;
            case ShopItemId.HealingSpring: health.Heal(health.MaxHealth); break;
            case ShopItemId.DaggerBundle: stats.bonusDaggers += 2; break;       // 다음 라운드 시작 때 채워짐
            case ShopItemId.ForgedDaggerTip: stats.daggerDamageMul *= 1.3f; break;
            case ShopItemId.HeavyBlade: stats.meleeDamageMul *= 1.18f; break;
            case ShopItemId.LightWrist: stats.attackSpeedMul *= 1.15f; break;
            case ShopItemId.SteelHeart:
                stats.bonusMaxHealth += 25f;
                health.Heal(25f);                                                // 유물 강철 심장과 같은 방식
                break;
            case ShopItemId.LeatherBoots:
                stats.moveSpeedMul *= 1.1f;
                stats.jumpMul *= 1.06f;
                break;
            case ShopItemId.WardCharm: stats.damageTakenMul *= 0.9f; break;
            case ShopItemId.GoldenScale: stats.goldMul *= 1.2f; break;
            default:
                Debug.LogWarning($"상점 품목 효과 미구현: {id}");
                break;
        }
    }
}
