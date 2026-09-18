using System.Collections.Generic;
using UnityEngine;

// 불타는 대지의 화염 지대 (Spec 3장 환경 카드 동작).
// 예고(주황 깜빡임) → 불탐(붉게 일렁임). 불타는 동안 몸이 지대에 닿으면 그 순간 바로 피해(일반 피격 — 무적 시간·피격 표시 있음).
// 보이는 사각형과 피해 범위는 같은 크기. 라운드 종료를 막지 않는 환경 위협이라 자체 타이머로 동작.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FireZone : MonoBehaviour
{
    const int SortingOrder = 160;  // 어둠(150) 위
    static readonly Color WarnColor = new Color(1f, 0.6f, 0.1f);
    static readonly Color BurnColor = new Color(1f, 0.3f, 0.05f);

    static readonly List<FireZone> Active = new List<FireZone>();

    private Mesh mesh;
    private PlayerHealth player;
    private Collider2D playerBody;   // 매 프레임 GetComponent 하지 않게 캐시
    private float left, right, top;
    private float warnTime, burnTime, tickInterval, damage;
    private float age;
    private float nextHitTime;       // 이 시각 뒤에 다시 닿으면 또 맞음 (무적 시간이 더 길면 그쪽이 기준)
    private bool feedsEnemies;   // 불꽃 포식 조합 (생성 시 확정)

    public static void Spawn(float centerX, float width, float height, float warnTime, float burnTime,
        float tickInterval, float damage, PlayerHealth player)
    {
        SoundManager.Play(SoundId.Warn);
        GameObject go = new GameObject("FireZone");
        go.transform.position = new Vector3(centerX - width * 0.5f, ArenaLayout.GroundTop, 0f);

        FireZone zone = go.AddComponent<FireZone>();
        zone.player = player;
        zone.left = centerX - width * 0.5f;
        zone.right = centerX + width * 0.5f;
        zone.top = ArenaLayout.GroundTop + height;
        zone.warnTime = warnTime;
        zone.burnTime = burnTime;
        zone.tickInterval = tickInterval;
        zone.damage = damage;
        zone.feedsEnemies = CardCombo.IsActive(ComboId.FlameFeast);
        zone.Build(width, height);
    }

    public static void DestroyAll()
    {
        foreach (FireZone zone in Active.ToArray()) Destroy(zone.gameObject);
    }

    void OnEnable() => Active.Add(this);
    void OnDisable() => Active.Remove(this);

    void Build(float width, float height)
    {
        mesh = new Mesh();
        mesh.MarkDynamic();
        mesh.vertices = new[]
        {
            new Vector3(0f, 0f, 0f), new Vector3(0f, height, 0f),
            new Vector3(width, height, 0f), new Vector3(width, 0f, 0f),
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.colors = new Color[4];
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().mesh = mesh;
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = RuntimeMaterials.SpriteUnlit;
        meshRenderer.sortingOrder = SortingOrder;
        ApplyColor();
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= warnTime + burnTime)
        {
            Destroy(gameObject);
            return;
        }

        if (age >= warnTime) Burn();

        ApplyColor();
    }

    // 불타는 동안 몸이 지대에 닿으면 **그 순간 바로** 피해.
    // 지속 피해가 아니라 일반 피격이라 피격음·깜빡임·무적 시간이 생기고, 무적이 풀린 채로 계속 닿아 있으면 다시 맞음.
    // 무적에 막힌 프레임에는 아무 일도 없으므로 다음 프레임에 다시 시도함
    void Burn()
    {
        if (PlayerInside() && Time.time >= nextHitTime)
        {
            float dealt = player.TakeHit(new HitInfo { damage = damage });
            if (dealt > 0f) nextHitTime = Time.time + tickInterval;
        }

        // 불꽃 포식 조합: 불타는 지대 안 지면의 적은 회복
        if (feedsEnemies)
        {
            foreach (Enemy enemy in Enemy.Active)
            {
                if (enemy.IsDead) continue;
                Collider2D body = enemy.GetComponent<Collider2D>();
                if (body != null && Inside(body.bounds)) enemy.HealOverTime(CardCombo.FlameFeastHealPerSecond, Time.deltaTime);
            }
        }
    }

    // 플레이어 몸이 보이는 사각형과 실제로 겹치는지 (가로로 걸치고, 발이 지대 높이 안)
    bool PlayerInside()
    {
        if (player == null) player = FindFirstObjectByType<PlayerHealth>();   // 라운드 중 플레이어가 새로 잡힌 경우
        if (player == null || player.IsDead) return false;

        if (playerBody == null || playerBody.gameObject != player.gameObject)
            playerBody = player.GetComponent<Collider2D>();
        if (playerBody == null) return false;

        return Inside(playerBody.bounds);
    }

    bool Inside(Bounds b) => b.max.x >= left && b.min.x <= right && b.min.y <= top && b.max.y >= ArenaLayout.GroundTop;

    void ApplyColor()
    {
        Color bottom, upper;
        if (age < warnTime)
        {
            float t = age / warnTime;
            float blink = 0.6f + 0.4f * Mathf.Sin(age * Mathf.Lerp(8f, 30f, t));
            bottom = WarnColor;
            bottom.a = Mathf.Lerp(0.15f, 0.4f, t) * blink;
        }
        else
        {
            bottom = BurnColor;
            bottom.a = 0.6f + 0.15f * Mathf.Sin(age * 20f);
        }
        upper = bottom;
        upper.a *= 0.25f;  // 위로 갈수록 옅게 — 불꽃처럼
        mesh.colors = new[] { bottom, upper, upper, bottom };
    }

    void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }
}
