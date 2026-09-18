using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// 상점 맵 (Spec 7장 상점 맵, 6-2장 골드 → 상점 영구 강화). 라운드 N 클리어 후 들어오는 별도 씬(라운드 N.5).
// 좌판 5개(로비 강화 상인의 안목이면 6개) 실물 진열 — 가까이 가면 말풍선 → Enter 구매,
// R 진열 갱신(로비 강화 상점 갱신, 방문당 1회), 입구 모험가·가운데 단 위 상점 주인 대사, 오른쪽 출구 포탈(Enter → 다음 라운드), Tab 소지품 창.
// 좌판·진열장·단·NPC·포탈 모양은 시작 시 코드 메시로 만듦(스프라이트 교체 전 임시). 말풍선·안내는 임시 UI(OnGUI).
[RequireComponent(typeof(RunState))]
public class ShopManager : MonoBehaviour
{
    [Header("연결")]
    public string battleSceneName = "MainScene";

    [Header("상호작용 거리 (플레이어 중심 기준)")]
    public float stallReach = 1.3f;     // 좌판 간격 2.8의 절반보다 작게
    public float portalReach = 1.0f;

    [Header("NPC 대사 교체 간격")]
    public float npcLineMinTime = 4f;
    public float npcLineMaxTime = 7f;

    [Header("안내 문구")]
    public float messageDuration = 2.5f;

    const float CounterHeight = 0.9f;
    const float IconBottom = 1.45f;
    const float IconSize = 0.55f;
    const float LabelY = IconBottom + IconSize + 0.1f;   // 좌판 이름표 아래끝 높이
    const float ShopkeeperHeight = 1.5f;
    const float AdventurerHeight = 1.15f;
    const float NpcBubbleLift = 0.95f;      // 대사 말풍선 아래끝 = 머리 위 + 이만큼 (이름표 위)
    const float PortalHeight = 2.4f;
    const float FarLabelWidth = 110f;       // 넓은 화면(1u = 48 가상 px)에서 간격 2.8u 이름표가 겹치지 않는 폭
    const float DetailBubbleY = LabelY + 1.2f;  // 가까운 좌판 설명 말풍선 아래끝 — 이름표(2줄 약 1.1u) 위라 다른 좌판 이름을 가리지 않음

    // 잡화점 진열장·상점 주인 단 (ShopLayout.ShopCenterX 기준, 진열장 폭은 좌판 수에 맞춤)
    const float KeeperStageWidth = 2.4f;
    const float ShelfHeight = 5.2f;         // 단 위 상점 주인(머리 4.9) 뒤까지

    static readonly Color CounterColor = new Color(0.36f, 0.26f, 0.18f);
    static readonly Color ShopCounterColor = new Color(0.45f, 0.32f, 0.2f);
    static readonly Color ShelfColor = new Color(0.22f, 0.16f, 0.12f, 0.9f);
    static readonly Color ShelfBoardColor = new Color(0.4f, 0.29f, 0.18f);
    static readonly Color SoldColor = new Color(0.38f, 0.38f, 0.4f);
    static readonly Color PortalColor = new Color(0.6f, 0.35f, 0.95f);
    static readonly Color ShopkeeperColor = new Color(0.62f, 0.48f, 0.28f);
    static readonly Color AdventurerColor = new Color(0.35f, 0.5f, 0.45f);
    static readonly Color[] ShelfGoodsColors =
    {
        new Color(0.8f, 0.3f, 0.3f), new Color(0.35f, 0.6f, 0.85f), new Color(0.85f, 0.75f, 0.35f), new Color(0.45f, 0.75f, 0.45f),
    };

    // 상점 주인: 장사·물건·골드 이야기 + 손님들에게 들은 전투 요령. 전부 Spec에 있는 규칙만 말함
    static readonly string[] ShopkeeperLines =
    {
        "어서 오게! 물건은 좌판 앞에서 Enter면 된다네.",
        "물약은 한 번 들를 때 다섯 병까지 팔지. 넉넉히 챙겨 가게.",
        "값은 라운드가 지날수록 오른다네. 살 거면 지금 사게.",
        "치유의 샘물은 다친 손님한테만 판다네. 멀쩡한 몸엔 아까운 물건이지.",
        "골드는 쌓아두는 게 아니야. 살아남는 데 쓰는 거지.",
        "Tab을 누르면 자네 짐과 지금까지 건 저주가 한눈에 보일 걸세.",
        "진열이 마음에 안 드나? 로비 제단에 영혼을 바친 단골은 R로 바꿔 가더군.",
        "여기서 산 물건은 이번 런이 끝날 때까지 자네 몸에 남는다네.",
    };

    // 모험가 (입구): 던전 전투·위험도 이야기
    static readonly string[] AdventurerLines =
    {
        "붉게 달아오른 놈은 곧 휘두른다는 뜻이야. 그 틈에 빠지면 돼.",
        "세 번째 내려찍기는 놈들 공격 준비를 끊어버리지.",
        "조준선이 보이면 옆으로 비켜. 마법구는 검으로 베어낼 수도 있고.",
        "위험도가 50을 넘을 때마다 유물이 하나씩 굴러들어오더군. 150을 넘기면 35마다고.",
        "숫자만 올리는 저주는 위험도가 높고, 피할 수 있는 저주는 골드가 후해.",
        "같은 계열 저주를 모으면 시너지가, 궁합 맞는 두 장이면 조합이 깨어나.",
        "탐욕으로 한 장 더 챙기면 위험도도 보상도 1.3배야. 욕심은 적당히.",
        "시체가 번쩍이면 곧 터진다. 잠깐 물러서.",
        "방패병은 뒤를 잡거나 세 번째 내려찍기로 방패를 내리게 해.",
        "쓰러져도 영혼은 남아. 로비 제단에 바치면 다음엔 조금 더 버틸 수 있지.",
    };

    class Stall
    {
        public ShopCatalog.Item item;
        public int price;
        public bool sold;
        public int remaining;       // 이번 방문에 더 살 수 있는 수 (물약 5, 그 외 1)
        public Vector2 feet;        // 좌대 아래 가운데 (층 포함)
        public MeshFilter icon;
    }

    class Npc
    {
        public string name;
        public string[] lines;
        public Vector2 feet;
        public float height;
        public int line;
        public float timer;
    }

    private RunState run;
    private PlayerHealth player;
    private InputAction interactAction;
    private InputAction inventoryAction;
    private InputAction rerollAction;

    private readonly List<Stall> stalls = new List<Stall>();
    private readonly List<Npc> npcs = new List<Npc>();
    private MeshFilter portal;
    private bool inventoryOpen;
    private bool leaving;
    private int refreshesLeft;          // 로비 강화 상점 갱신 — 방문당 1회
    private float shelfWidth;           // 진열장 폭 (좌판 수에 맞춤)
    private string message;
    private float messageTimer;

    private GUIStyle bubbleStyle, npcStyle, centerStyle, hintStyle;

    void Awake()
    {
        run = GetComponent<RunState>();
        interactAction = InputSystem.actions.FindAction("Player/Interact", throwIfNotFound: true);
        inventoryAction = InputSystem.actions.FindAction("Player/Inventory", throwIfNotFound: true);
        rerollAction = InputSystem.actions.FindAction("Player/Reroll", throwIfNotFound: true);
    }

    void Start()
    {
        player = FindFirstObjectByType<PlayerHealth>();
        if (player == null)
        {
            Debug.LogWarning("ShopManager: 씬에 PlayerHealth가 없습니다.", this);
            return;
        }

        run.RestorePlayer(player);  // 전투 씬에서 넘어온 강화·체력

        // 상점에서는 전투 입력을 끔 (이동·점프·대쉬·물약만)
        foreach (MonoBehaviour combat in new MonoBehaviour[] { player.GetComponent<PlayerAttack>(), player.GetComponent<DaggerThrower>(), player.GetComponent<PlayerSkill>() })
        {
            if (combat != null) combat.enabled = false;
        }

        refreshesLeft = MetaProgress.Level(MetaUpgradeId.ShopRefresh) > 0 ? 1 : 0;

        PlacePlayer();
        StockStalls();
        CreateNpcs();
        portal = WorldGui.CreateQuad("ExitPortal", new Vector2(ShopLayout.PortalX, ArenaLayout.GroundTop), new Vector2(1.2f, PortalHeight), PortalColor, -4);

        ShowMessage($"상점 — 라운드 {run.round}.5");
    }

    void Update()
    {
        messageTimer -= Time.deltaTime;
        if (player == null || leaving) return;

        Animate();

        if (inventoryAction.WasPressedThisFrame()) SetInventoryOpen(!inventoryOpen);
        if (inventoryOpen) return;

        if (rerollAction.WasPressedThisFrame()) TryRefresh();
        if (!interactAction.WasPressedThisFrame()) return;

        if (NearPortal())
        {
            Leave();
            return;
        }
        Stall stall = NearestStall();
        if (stall != null) TryBuy(stall);
    }

    // ───────────── 구매·갱신·출구 ─────────────

    void TryBuy(Stall stall)
    {
        if (stall.sold) return;
        if (run.gold < stall.price)
        {
            ShowMessage("골드가 부족합니다");
            SoundManager.Play(SoundId.Deny);
            return;
        }
        string blocked = ShopCatalog.BlockReason(stall.item.id, player);
        if (blocked != null)
        {
            ShowMessage(blocked);
            SoundManager.Play(SoundId.Deny);
            return;
        }

        run.gold -= stall.price;
        stall.remaining--;
        ShopCatalog.Apply(stall.item.id, player, run);
        SoundManager.Play(SoundId.Buy);

        string left = stall.remaining > 0 ? $" (남은 수량 {stall.remaining})" : "";
        ShowMessage($"구매: {stall.item.name} — {stall.item.description}{left}");
        if (stall.remaining > 0) return;

        stall.sold = true;
        WorldGui.SetQuadColor(stall.icon, SoldColor);
    }

    // 상점 갱신: 아직 안 팔린 좌판만, 지금 진열에 없는 품목으로 교체
    void TryRefresh()
    {
        if (refreshesLeft <= 0) return;

        List<ShopItemId> displayed = new List<ShopItemId>();
        foreach (Stall stall in stalls) displayed.Add(stall.item.id);

        List<ShopCatalog.Item> pool = new List<ShopCatalog.Item>();
        foreach (ShopCatalog.Item item in ShopCatalog.All) if (!displayed.Contains(item.id)) pool.Add(item);

        List<Stall> unsold = new List<Stall>();
        foreach (Stall stall in stalls) if (!stall.sold) unsold.Add(stall);

        List<ShopCatalog.Item> picked = new List<ShopCatalog.Item>();
        while (picked.Count < unsold.Count && pool.Count > 0)
        {
            int index = Random.Range(0, pool.Count);
            picked.Add(pool[index]);
            pool.RemoveAt(index);
        }
        // 새 품목도 싼 것부터 왼쪽 좌판에. 품목이 모자라면 왼쪽 좌판부터 바꿈
        SortByPrice(picked);
        int replaced = picked.Count;
        for (int i = 0; i < replaced; i++) SetStallItem(unsold[i], picked[i]);

        if (replaced == 0)
        {
            ShowMessage("바꿀 수 있는 진열이 없습니다");
            return;
        }
        refreshesLeft--;
        ShowMessage("진열을 새로 바꿨습니다");
    }

    void Leave()
    {
        if (string.IsNullOrEmpty(battleSceneName)) battleSceneName = "MainScene";  // Inspector 값이 비어 있어도 기본 전투 씬으로
        if (!Application.CanStreamedLevelBeLoaded(battleSceneName))
        {
            ShowMessage($"전투 씬 \"{battleSceneName}\"이 빌드 설정에 없습니다");
            return;
        }

        leaving = true;
        run.round++;                      // 라운드 N.5 → N+1
        run.CarryToNextScene(player);
        SceneManager.LoadScene(battleSceneName);
    }

    // Tab 소지품 창: 열린 동안 이동·물약·구매 잠금
    void SetInventoryOpen(bool open)
    {
        inventoryOpen = open;
        foreach (MonoBehaviour control in new MonoBehaviour[] { player.GetComponent<PlayerMovement>(), player.GetComponent<PlayerPotion>() })
        {
            if (control != null) control.enabled = !open;
        }
        if (open)
        {
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);  // 공중이면 그대로 떨어짐
        }
    }

    // 지면 위에 서서 좌판 가까이 있을 때 가장 가까운 좌판 (발판 위에서는 해당 없음)
    Stall NearestStall()
    {
        Vector2 position = player.transform.position;
        Stall best = null;
        float bestDistance = stallReach;
        foreach (Stall stall in stalls)
        {
            float distance = Vector2.Distance(position, stall.feet + new Vector2(0f, 0.5f));
            if (distance > bestDistance) continue;
            best = stall;
            bestDistance = distance;
        }
        return best;
    }

    bool NearPortal()
    {
        Vector2 center = new Vector2(ShopLayout.PortalX, ArenaLayout.GroundTop + 0.5f);
        return Vector2.Distance(player.transform.position, center) <= portalReach;
    }

    void ShowMessage(string text)
    {
        message = text;
        messageTimer = messageDuration;
    }

    // ───────────── 배치 ─────────────

    void PlacePlayer()
    {
        Vector2 start = ArenaLayout.GroundStart(player != null ? player.gameObject : null, ShopLayout.PlayerStartX);
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        body.position = start;
        body.linearVelocity = Vector2.zero;
        player.transform.position = start;

        CameraFollow cameraFollow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        if (cameraFollow != null) cameraFollow.UseWideView(ShopLayout.ViewWidth, ShopLayout.ViewHeight, ShopLayout.HalfWidth);  // 넓게 보며 가로 추적
    }

    // 10종 중 서로 다른 5종(상인의 안목이면 6종) 무작위. 가격은 이번에 클리어한 라운드 기준
    void StockStalls()
    {
        bool merchantEye = MetaProgress.Level(MetaUpgradeId.MerchantEye) > 0;
        int total = Mathf.Min(merchantEye ? 6 : 5, ShopLayout.MaxStalls, ShopCatalog.All.Length);

        List<ShopCatalog.Item> pool = new List<ShopCatalog.Item>(ShopCatalog.All);
        List<ShopCatalog.Item> picked = new List<ShopCatalog.Item>();
        for (int i = 0; i < total; i++)
        {
            int index = Random.Range(0, pool.Count);
            picked.Add(pool[index]);
            pool.RemoveAt(index);
        }
        SortByPrice(picked);  // 싼 품목이 왼쪽, 비싼 품목이 카운터 옆

        for (int i = 0; i < total; i++)
        {
            ShopCatalog.Item item = picked[i];
            Vector2 feet = ShopLayout.StallFeet(i, total);
            WorldGui.CreateQuad($"Stall_{i + 1}", feet, new Vector2(2f, CounterHeight), CounterColor, -3);
            MeshFilter icon = WorldGui.CreateQuad($"Stall_{i + 1}_Item", feet + new Vector2(0f, IconBottom), new Vector2(IconSize, IconSize), item.color, -2);

            Stall stall = new Stall { feet = feet, icon = icon };
            SetStallItem(stall, item);
            stalls.Add(stall);
        }
    }

    void SortByPrice(List<ShopCatalog.Item> items)
    {
        items.Sort((a, b) => ShopCatalog.Price(a, run.round).CompareTo(ShopCatalog.Price(b, run.round)));
    }

    void SetStallItem(Stall stall, ShopCatalog.Item item)
    {
        stall.item = item;
        stall.price = ShopCatalog.Price(item, run.round);
        stall.remaining = ShopCatalog.StockPerVisit(item.id);
        stall.sold = false;
        WorldGui.SetQuadColor(stall.icon, item.color);
    }

    // 잡화점: 뒤 진열장(선반 4단 + 물건) → 가운데 단과 그 위의 상점 주인 → 앞 좌판(StockStalls) 순으로 겹쳐 그림.
    // 진열장 폭은 좌판 줄에 맞춤 (StockStalls 다음에 호출)
    void CreateNpcs()
    {
        float x = ShopLayout.ShopCenterX;
        shelfWidth = ShopLayout.ShelfWidth(stalls.Count);
        WorldGui.CreateQuad("Shelf", new Vector2(x, ArenaLayout.GroundTop), new Vector2(shelfWidth, ShelfHeight), ShelfColor, -7);

        int goodsPerRow = Mathf.Max(1, Mathf.FloorToInt((shelfWidth - 1f) / 1.25f));
        for (int row = 0; row < 4; row++)
        {
            float boardY = ArenaLayout.GroundTop + 1.3f + row * 0.95f;
            WorldGui.CreateQuad($"Shelf_Board_{row + 1}", new Vector2(x, boardY), new Vector2(shelfWidth - 0.3f, 0.1f), ShelfBoardColor, -6);
            for (int i = 0; i < goodsPerRow; i++)
            {
                Color color = ShelfGoodsColors[(row + i) % ShelfGoodsColors.Length];
                float goodsX = x - (goodsPerRow - 1) * 0.5f * 1.25f + i * 1.25f;
                WorldGui.CreateQuad($"Shelf_Goods_{row + 1}_{i + 1}", new Vector2(goodsX, boardY + 0.1f), new Vector2(0.35f, 0.4f + 0.1f * ((row + i) % 2)), color, -6);
            }
        }

        // 상점 주인은 좌판 이름표보다 높은 단 위에 서서 앞 좌판에 가려지지 않음
        WorldGui.CreateQuad("KeeperStage", new Vector2(x, ArenaLayout.GroundTop), new Vector2(KeeperStageWidth, ShopLayout.KeeperStageHeight), ShopCounterColor, -5);
        AddNpc("상점 주인", ShopkeeperLines, ShopLayout.ShopkeeperFeet, ShopkeeperHeight, ShopkeeperColor, firstLine: 0);  // 들어오자마자 인사

        AddNpc("모험가", AdventurerLines, ShopLayout.AdventurerFeet, AdventurerHeight, AdventurerColor, firstLine: Random.Range(0, AdventurerLines.Length));
    }

    void AddNpc(string name, string[] lines, Vector2 feet, float height, Color color, int firstLine)
    {
        WorldGui.CreateQuad($"Npc_{name}", feet, new Vector2(0.7f, height), color, -2);  // 진열장 앞, 카운터 뒤
        // 두 NPC가 동시에 대사를 바꾸지 않게 첫 교체 시각을 흩뜨림
        npcs.Add(new Npc { name = name, lines = lines, feet = feet, height = height, line = firstLine, timer = Random.Range(npcLineMinTime, npcLineMaxTime) });
    }

    void Animate()
    {
        // 좌판 아이콘은 위아래로 떠 있음, 포탈은 일렁임
        foreach (Stall stall in stalls)
        {
            Vector3 p = stall.icon.transform.position;
            p.y = stall.feet.y + IconBottom + 0.08f * Mathf.Sin(Time.time * 2f + stall.feet.x);
            stall.icon.transform.position = p;
        }
        Color portalColor = PortalColor;
        portalColor.a = 0.6f + 0.25f * Mathf.Sin(Time.time * 3f);
        WorldGui.SetQuadColor(portal, portalColor);

        foreach (Npc npc in npcs)
        {
            npc.timer -= Time.deltaTime;
            if (npc.timer > 0f) continue;
            npc.timer = Random.Range(npcLineMinTime, npcLineMaxTime);
            npc.line = (npc.line + Random.Range(1, npc.lines.Length)) % npc.lines.Length;  // 같은 대사 연속 방지
        }
    }

    // ───────────── 임시 UI ─────────────

    void OnGUI()
    {
        if (player == null) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        EnsureStyles();

        GUI.depth = -10;  // DebugHud보다 위에
        float width = WorldGui.BeginVirtual(out float scale);

        // 잡화점 간판 (진열장 위)
        // 간판은 진열장 왼쪽 위 — 가운데 상점 주인 대사와 겹치지 않게
        WorldGui.DrawBubble(cam, scale, new Vector2(ShopLayout.ShopCenterX - shelfWidth * 0.5f + 1.5f, ShelfHeight + 0.1f), "<b>잡화점</b>", 90f, bubbleStyle, new Color(0.3f, 0.2f, 0.12f, 0.95f));

        foreach (Npc npc in npcs)
        {
            // 머리 위엔 이름표, 대사는 그 위에
            WorldGui.DrawBubble(cam, scale, npc.feet + new Vector2(0f, npc.height + 0.2f), npc.name, 80f, hintStyle, new Color(0f, 0f, 0f, 0.55f));
            WorldGui.DrawBubble(cam, scale, npc.feet + new Vector2(0f, npc.height + NpcBubbleLift), npc.lines[npc.line], 200f, npcStyle, new Color(0.95f, 0.93f, 0.85f, 0.9f));
        }

        if (!inventoryOpen)
        {
            Stall near = NearestStall();
            foreach (Stall stall in stalls)
            {
                // 모든 좌판에 이름·가격을 작게 (가까운 좌판은 밝게 표시하고 설명은 그 위에 따로)
                string count = stall.remaining > 1 ? $" ({stall.remaining}개)" : "";
                string label = stall.sold ? "판매됨" : $"{stall.item.name}\n{stall.price} G{count}";
                Color background = stall == near ? new Color(0.35f, 0.28f, 0.12f, 0.85f) : new Color(0f, 0f, 0f, 0.45f);
                WorldGui.DrawBubble(cam, scale, stall.feet + new Vector2(0f, LabelY), label, FarLabelWidth, hintStyle, background);
            }

            string portalText = NearPortal() ? "출구 포탈\nEnter — 라운드 " + (run.round + 1) : "출구";
            WorldGui.DrawBubble(cam, scale, new Vector2(ShopLayout.PortalX, PortalHeight + 0.25f), portalText, 200f, bubbleStyle, new Color(0.08f, 0.07f, 0.1f, 0.85f));

            // 가까운 좌판 설명은 이름표 줄보다 위에 띄워 다른 좌판 이름을 가리지 않게
            if (near != null) WorldGui.DrawBubble(cam, scale, near.feet + new Vector2(0f, DetailBubbleY), StallText(near), 300f, bubbleStyle, new Color(0.08f, 0.07f, 0.1f, 0.92f));

            string refresh = refreshesLeft > 0 ? "   R 진열 갱신 (1회)" : "";
            GUI.Label(new Rect(0, WorldGui.VirtualHeight - 44, width, 30), "← → 이동   C 점프   ↓+C 내려가기   Enter 구매·출구   1 물약   Tab 소지품" + refresh, hintStyle);
        }

        if (messageTimer > 0f) GUI.Label(new Rect(0, 120, width, 40), message, centerStyle);
        if (inventoryOpen) InventoryWindow.Draw(width, WorldGui.VirtualHeight, run, player);

        GUI.matrix = Matrix4x4.identity;
    }

    string StallText(Stall stall)
    {
        string count = ShopCatalog.StockPerVisit(stall.item.id) > 1 && !stall.sold ? $"  (남은 수량 {stall.remaining})" : "";
        string header = $"<b>{stall.item.name}</b>{count}\n{stall.item.description}\n";
        if (stall.sold) return header + "<color=#999999>판매됨</color>";
        if (run.gold < stall.price) return header + $"<color=#ff5a5a>골드 부족 ({stall.price} G)</color>";
        return header + $"<color=#ffd966>Enter 로 구매 ({stall.price} G)</color>";
    }

    void EnsureStyles()
    {
        if (bubbleStyle != null) return;

        bubbleStyle = WorldGui.MakeStyle(17, Color.white);
        npcStyle = WorldGui.MakeStyle(15, new Color(0.15f, 0.12f, 0.1f));
        hintStyle = WorldGui.MakeStyle(15, new Color(0.9f, 0.9f, 0.9f));
        centerStyle = WorldGui.MakeStyle(24, Color.white);
        centerStyle.fontStyle = FontStyle.Bold;
    }
}
