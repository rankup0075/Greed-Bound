using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// 로비 (Spec 6-3장 "로비 — Unity 구현 규칙"). 게임 오버 후 오는 걸어다니는 씬.
// 구역 3개(기술의 전당 스킬 / 보급소 1·2층 / 운명의 서고 카드) 제단에서 Enter로 해금·선택·강화, 오른쪽 끝 출전 포탈(Enter → 새 런).
// 제단·포탈 모양은 시작 시 코드 메시로 만듦(스프라이트 교체 전 임시). 말풍선·HUD는 임시 UI(OnGUI).
public class LobbyManager : MonoBehaviour
{
    [Header("연결")]
    public string battleSceneName = "MainScene";

    [Header("상호작용 거리 (플레이어 중심 기준)")]
    public float altarReach = 1.1f;     // 제단 간격 2.3의 절반보다 작게
    public float portalReach = 1.0f;    // 운명의 서고 마지막 칸과 겹치지 않게

    [Header("안내 문구")]
    public float messageDuration = 2.5f;

    const float PedestalHeight = 0.7f;

    // 소품 그림 (AI_Source/tools/build_props.py). 없으면 예전처럼 색 사각형으로 떨어진다
    static Sprite Prop(string name) => Resources.Load<Sprite>("Props/" + name);
    const float OrbBottom = 1.1f;
    const float OrbSize = 0.45f;
    const float PortalHeight = 2.4f;
    const float DetailLift = 1.2f;      // 가까운 제단 설명 말풍선을 이름표(2줄 약 1.1u) 위로
    const float FarLabelWidth = 90f;   // 넓은 화면(1u = 48 가상 px)에서 간격 2.3u 이름표가 겹치지 않는 폭 (여백 포함 2.2u)

    static readonly Color PedestalColor = new Color(0.3f, 0.28f, 0.34f);
    static readonly Color SkillPedestalColor = new Color(0.22f, 0.3f, 0.3f);
    static readonly Color SupplyColor = new Color(0.9f, 0.4f, 0.35f);     // 보급소 (체력·물약·골드·단검·상점)
    static readonly Color CardColor = new Color(0.4f, 0.65f, 0.95f);      // 운명의 서고 (리롤·탐욕의 눈·시야)
    static readonly Color TitleColor = new Color(1f, 0.85f, 0.45f);
    static readonly Color MaxedColor = new Color(1f, 0.82f, 0.3f);
    static readonly Color SkillColor = new Color(0.45f, 0.95f, 0.75f);    // 해금된 스킬
    static readonly Color SelectedSkillColor = new Color(1f, 1f, 1f);     // 지금 고른 스킬
    static readonly Color PortalColor = new Color(0.85f, 0.3f, 0.35f);

    // 가까이 있는 제단
    struct Target
    {
        public bool found;
        public bool isSkill;
        public int index;
    }

    private PlayerHealth player;
    private InputAction interactAction;
    private MeshFilter[] skillOrbs;
    private MeshFilter[] orbs;
    private SpriteRenderer portal;
    private MeshFilter portalQuad;   // 문 그림이 없을 때의 예전 색 사각형
    private bool leaving;
    private string message;
    private float messageTimer;

    private GUIStyle bubbleStyle, hintStyle, centerStyle, infoStyle, titleStyle;

    void Awake()
    {
        interactAction = InputSystem.actions.FindAction("Player/Interact", throwIfNotFound: true);
    }

    void Start()
    {
        player = FindFirstObjectByType<PlayerHealth>();
        if (player == null)
        {
            Debug.LogWarning("LobbyManager: 씬에 PlayerHealth가 없습니다.", this);
            return;
        }

        // 로비에서는 이동·점프·대쉬만
        foreach (MonoBehaviour control in new MonoBehaviour[]
                 { player.GetComponent<PlayerAttack>(), player.GetComponent<DaggerThrower>(), player.GetComponent<PlayerPotion>(), player.GetComponent<PlayerSkill>() })
        {
            if (control != null) control.enabled = false;
        }

        PlacePlayer();

        // 구역 뒤 바닥 표시 (늘어날 칸까지 포함한 구역 전체 폭)
        foreach (LobbyLayout.Category category in System.Enum.GetValues(typeof(LobbyLayout.Category)))
        {
            WorldGui.CreateQuad($"Zone_{category}", new Vector2(LobbyLayout.ZoneCenterX(category), ArenaLayout.GroundTop),
                new Vector2(LobbyLayout.ZoneWidth(), LobbyLayout.ZoneBackdropHeight(category)), ZoneColor(category), -8);
        }

        skillOrbs = new MeshFilter[SkillCatalog.All.Length];
        for (int i = 0; i < SkillCatalog.All.Length; i++)
        {
            Vector2 feet = LobbyLayout.SkillFeet(i);
            Sprite altarArt = Prop("prop_pedestal");
            if (altarArt != null) WorldGui.CreateSprite($"SkillAltar_{i + 1}", feet, altarArt, -3, new Vector2(1.2f, altarArt.bounds.size.y));
            else WorldGui.CreateQuad($"SkillAltar_{i + 1}", feet, new Vector2(1.2f, PedestalHeight), SkillPedestalColor, -3);
            skillOrbs[i] = WorldGui.CreateQuad($"SkillAltar_{i + 1}_Orb", feet + new Vector2(0f, OrbBottom), new Vector2(OrbSize, OrbSize), SkillOrbColor(i), -2);
        }

        orbs = new MeshFilter[MetaCatalog.All.Length];
        for (int i = 0; i < MetaCatalog.All.Length; i++)
        {
            Vector2 feet = LobbyLayout.AltarFeet(i);
            Sprite altarArt = Prop("prop_pedestal");
            if (altarArt != null) WorldGui.CreateSprite($"Altar_{i + 1}", feet, altarArt, -3, new Vector2(1.2f, altarArt.bounds.size.y));
            else WorldGui.CreateQuad($"Altar_{i + 1}", feet, new Vector2(1.2f, PedestalHeight), PedestalColor, -3);
            orbs[i] = WorldGui.CreateQuad($"Altar_{i + 1}_Orb", feet + new Vector2(0f, OrbBottom), new Vector2(OrbSize, OrbSize), OrbColor(i), -2);
        }
        Sprite doorArt = Prop("prop_door");
        portal = doorArt != null ? WorldGui.CreateSprite("DeployPortal", LobbyLayout.PortalFeet, doorArt, -4) : null;
        if (portal == null) portalQuad = WorldGui.CreateQuad("DeployPortal", LobbyLayout.PortalFeet, new Vector2(1.2f, PortalHeight), PortalColor, -4);
    }

    void Update()
    {
        messageTimer -= Time.deltaTime;
        if (player == null || leaving) return;

        Animate();
        if (!interactAction.WasPressedThisFrame()) return;

        if (NearPortal())
        {
            Deploy();
            return;
        }
        Target target = NearestTarget();
        if (!target.found) return;
        if (target.isSkill) UseSkillAltar(target.index);
        else TryBuy(target.index);
    }

    void TryBuy(int index)
    {
        MetaCatalog.Upgrade upgrade = MetaCatalog.All[index];
        if (MetaProgress.IsMaxed(upgrade.id)) return;
        if (!MetaProgress.TryBuy(upgrade.id))
        {
            ShowMessage("영혼이 부족합니다");
            SoundManager.Play(SoundId.Deny);
            return;
        }
        SoundManager.Play(SoundId.Buy);
        ShowMessage($"{upgrade.name} Lv{MetaProgress.Level(upgrade.id)} — {upgrade.description}");
    }

    // 해금 안 됐으면 해금(영혼), 해금됐으면 선택
    void UseSkillAltar(int index)
    {
        SkillCatalog.Skill skill = SkillCatalog.All[index];
        if (!MetaProgress.IsSkillUnlocked(skill.id))
        {
            if (!MetaProgress.TryUnlockSkill(skill.id))
            {
                ShowMessage("영혼이 부족합니다");
                SoundManager.Play(SoundId.Deny);
                return;
            }
            MetaProgress.SelectSkill(skill.id);
            SoundManager.Play(SoundId.Buy);
            ShowMessage($"스킬 해금: {skill.name} (선택됨)");
            return;
        }
        if (MetaProgress.SelectedSkill == skill.id) return;
        MetaProgress.SelectSkill(skill.id);
        ShowMessage($"스킬 선택: {skill.name}");
    }

    void Deploy()
    {
        if (string.IsNullOrEmpty(battleSceneName)) battleSceneName = "MainScene";
        if (!Application.CanStreamedLevelBeLoaded(battleSceneName))
        {
            ShowMessage($"전투 씬 \"{battleSceneName}\"이 빌드 설정에 없습니다");
            return;
        }
        leaving = true;
        SceneManager.LoadScene(battleSceneName);  // 넘기는 런 상태가 없으므로 새 런으로 시작
    }

    Target NearestTarget()
    {
        Vector2 position = player.transform.position;
        Target best = new Target();
        float bestDistance = altarReach;

        for (int i = 0; i < SkillCatalog.All.Length; i++) Consider(LobbyLayout.SkillFeet(i), true, i);
        for (int i = 0; i < MetaCatalog.All.Length; i++) Consider(LobbyLayout.AltarFeet(i), false, i);
        return best;

        void Consider(Vector2 feet, bool isSkill, int index)
        {
            float distance = Vector2.Distance(position, feet + new Vector2(0f, 0.5f));  // 같은 층에 서 있어야 닿음
            if (distance > bestDistance) return;
            best = new Target { found = true, isSkill = isSkill, index = index };
            bestDistance = distance;
        }
    }

    bool NearPortal()
    {
        Vector2 center = LobbyLayout.PortalFeet + new Vector2(0f, 0.5f);
        return Vector2.Distance(player.transform.position, center) <= portalReach;
    }

    void ShowMessage(string text)
    {
        message = text;
        messageTimer = messageDuration;
    }

    void PlacePlayer()
    {
        Vector2 start = ArenaLayout.GroundStart(player != null ? player.gameObject : null, LobbyLayout.PlayerStartX);
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        body.position = start;
        body.linearVelocity = Vector2.zero;
        player.transform.position = start;

        CameraFollow cameraFollow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        if (cameraFollow != null) cameraFollow.UseWideView(LobbyLayout.ViewWidth, LobbyLayout.ViewHeight, LobbyLayout.HalfWidth);  // 넓게 보며 가로 추적
    }

    static Color ZoneColor(LobbyLayout.Category category)
    {
        switch (category)
        {
            case LobbyLayout.Category.Skills: return new Color(0.2f, 0.32f, 0.3f, 0.35f);
            case LobbyLayout.Category.Supplies: return new Color(0.35f, 0.22f, 0.2f, 0.35f);
            default: return new Color(0.2f, 0.25f, 0.4f, 0.35f);
        }
    }

    // 최대 레벨 금색, 그 외 종류 색 (살 수 없으면 어둡게)
    Color OrbColor(int index)
    {
        MetaCatalog.Upgrade upgrade = MetaCatalog.All[index];
        if (MetaProgress.IsMaxed(upgrade.id)) return MaxedColor;
        Color color = LobbyLayout.CategoryOf(upgrade.id) == LobbyLayout.Category.Cards ? CardColor : SupplyColor;
        return MetaProgress.Souls >= MetaProgress.NextCost(upgrade.id) ? color : Color.Lerp(color, PedestalColor, 0.6f);
    }

    // 선택됨 흰색, 해금됨 초록, 잠김(살 수 있으면 흐리게, 없으면 더 어둡게)
    Color SkillOrbColor(int index)
    {
        SkillCatalog.Skill skill = SkillCatalog.All[index];
        if (MetaProgress.SelectedSkill == skill.id) return SelectedSkillColor;
        if (MetaProgress.IsSkillUnlocked(skill.id)) return SkillColor;
        float dim = MetaProgress.Souls >= skill.unlockCost ? 0.45f : 0.75f;
        return Color.Lerp(SkillColor, SkillPedestalColor, dim);
    }

    void Animate()
    {
        float bob(float floorY, int i) => floorY + OrbBottom + 0.08f * Mathf.Sin(Time.time * 2f + i);

        for (int i = 0; i < skillOrbs.Length; i++)
        {
            Vector3 p = skillOrbs[i].transform.position;
            p.y = bob(LobbyLayout.SkillFeet(i).y, i + 20);
            skillOrbs[i].transform.position = p;
            WorldGui.SetQuadColor(skillOrbs[i], SkillOrbColor(i));
        }
        for (int i = 0; i < orbs.Length; i++)
        {
            Vector3 p = orbs[i].transform.position;
            p.y = bob(LobbyLayout.AltarFeet(i).y, i);
            orbs[i].transform.position = p;
            WorldGui.SetQuadColor(orbs[i], OrbColor(i));  // 영혼이 바뀌면 살 수 있는지 표시도 바뀜
        }
        Color portalColor = PortalColor;
        portalColor.a = 0.6f + 0.25f * Mathf.Sin(Time.time * 3f);
        if (portal != null) portal.color = portalColor;
            else if (portalQuad != null) WorldGui.SetQuadColor(portalQuad, portalColor);
    }

    // ───────────── 임시 UI ─────────────

    void OnGUI()
    {
        // 일시정지 중에는 그리지 않는다. OnGUI(임시 UI)는 **정식 UI 캔버스 위에** 그려지기 때문에
        // 그대로 두면 월드 이름표·말풍선이 일시정지 메뉴를 가린다 (2026-09-20 사용자 보고).
        // 예전에는 일시정지도 OnGUI 라 GUI.depth 로 눌렀지만 이제는 캔버스라 그 방법을 못 쓴다
        if (PauseMenu.IsOpen) return;
        if (player == null) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        EnsureStyles();

        float width = WorldGui.BeginVirtual(out float scale);

        SkillCatalog.Skill selected = SkillCatalog.Get(MetaProgress.SelectedSkill);
        GUI.Label(new Rect(20, 16, 760, 100),
            $"<b>영혼 {MetaProgress.Souls}</b>     스킬: {selected.name}\n최고 라운드 {MetaProgress.BestRound}   런 {MetaProgress.Runs}회   누적 영혼 {MetaProgress.TotalSouls}", infoStyle);

        Target near = NearestTarget();
        Color nearBackground = new Color(0.08f, 0.07f, 0.1f, 0.92f);
        Color farBackground = new Color(0f, 0f, 0f, 0.45f);

        Vector2 bubbleLift = new Vector2(0f, OrbBottom + OrbSize + 0.25f);

        // 구역 제목
        foreach (LobbyLayout.Category category in System.Enum.GetValues(typeof(LobbyLayout.Category)))
        {
            string title = $"<b>{LobbyLayout.CategoryTitle(category)}</b>\n<size=13>{LobbyLayout.CategorySubtitle(category)}</size>";
            WorldGui.DrawBubble(cam, scale, LobbyLayout.CategoryTitleAnchor(category), title, 240f, titleStyle, new Color(0.1f, 0.08f, 0.12f, 0.7f));
        }

        // 모든 제단에 이름·Lv를 작게 (가까운 제단은 밝게). 설명은 이름표 줄 위에 따로 띄워 이웃 이름표를 가리지 않게
        Color nearLabelBackground = new Color(0.35f, 0.28f, 0.12f, 0.85f);
        for (int i = 0; i < SkillCatalog.All.Length; i++)
        {
            bool isNear = near.found && near.isSkill && near.index == i;
            WorldGui.DrawBubble(cam, scale, LobbyLayout.SkillFeet(i) + bubbleLift, SkillLabel(i), FarLabelWidth, hintStyle, isNear ? nearLabelBackground : farBackground);
        }
        for (int i = 0; i < MetaCatalog.All.Length; i++)
        {
            bool isNear = near.found && !near.isSkill && near.index == i;
            WorldGui.DrawBubble(cam, scale, LobbyLayout.AltarFeet(i) + bubbleLift, AltarLabel(i), FarLabelWidth, hintStyle, isNear ? nearLabelBackground : farBackground);
        }
        Vector2 detailLift = bubbleLift + new Vector2(0f, DetailLift);

        string portalText = NearPortal() ? "출전 포탈\nEnter — 새 런 시작" : "출전";
        WorldGui.DrawBubble(cam, scale, LobbyLayout.PortalFeet + new Vector2(0f, PortalHeight + 0.25f), portalText, 200f, bubbleStyle, new Color(0.08f, 0.07f, 0.1f, 0.85f));

        if (near.found && near.isSkill)
            WorldGui.DrawBubble(cam, scale, LobbyLayout.SkillFeet(near.index) + detailLift, SkillText(near.index), 300f, bubbleStyle, nearBackground);
        else if (near.found)
            WorldGui.DrawBubble(cam, scale, LobbyLayout.AltarFeet(near.index) + detailLift, AltarText(near.index), 280f, bubbleStyle, nearBackground);

        GUI.Label(new Rect(0, WorldGui.VirtualHeight - 44, width, 30), "← → 이동   C 점프   ↓+C 내려가기   Enter 해금·선택·강화·출전", hintStyle);
        if (messageTimer > 0f) GUI.Label(new Rect(0, 130, width, 40), message, centerStyle);

        GUI.matrix = Matrix4x4.identity;
    }

    string SkillLabel(int index)
    {
        SkillCatalog.Skill skill = SkillCatalog.All[index];
        string state = MetaProgress.SelectedSkill == skill.id ? "선택됨"
            : MetaProgress.IsSkillUnlocked(skill.id) ? "해금됨" : $"영혼 {skill.unlockCost}";
        return $"{skill.name}\n{state}";
    }

    string SkillText(int index)
    {
        SkillCatalog.Skill skill = SkillCatalog.All[index];
        string header = $"<b>스킬: {skill.name}</b>  (A, 쿨 {skill.cooldown:0}초)\n{skill.description}\n";
        if (MetaProgress.SelectedSkill == skill.id) return header + "<color=#ffffff>선택됨</color>";
        if (MetaProgress.IsSkillUnlocked(skill.id)) return header + "<color=#7df2bf>Enter 로 선택</color>";
        if (MetaProgress.Souls < skill.unlockCost) return header + $"<color=#ff5a5a>영혼 부족 ({skill.unlockCost})</color>";
        return header + $"<color=#ffd966>Enter 로 해금 (영혼 {skill.unlockCost})</color>";
    }

    // 멀리 있는 제단: 이름·레벨만
    string AltarLabel(int index)
    {
        MetaCatalog.Upgrade upgrade = MetaCatalog.All[index];
        return $"{upgrade.name}\nLv {MetaProgress.Level(upgrade.id)}/{upgrade.MaxLevel}";
    }

    string AltarText(int index)
    {
        MetaCatalog.Upgrade upgrade = MetaCatalog.All[index];
        int level = MetaProgress.Level(upgrade.id);
        string header = $"<b>{upgrade.name}</b>  Lv {level}/{upgrade.MaxLevel}\n{upgrade.description}\n";
        if (MetaProgress.IsMaxed(upgrade.id)) return header + "<color=#ffd24d>최대 레벨</color>";

        int cost = MetaProgress.NextCost(upgrade.id);
        if (MetaProgress.Souls < cost) return header + $"<color=#ff5a5a>영혼 부족 ({cost})</color>";
        return header + $"<color=#ffd966>Enter 로 강화 (영혼 {cost})</color>";
    }

    void EnsureStyles()
    {
        if (bubbleStyle != null) return;

        bubbleStyle = WorldGui.MakeStyle(17, Color.white);
        hintStyle = WorldGui.MakeStyle(15, new Color(0.9f, 0.9f, 0.9f));
        centerStyle = WorldGui.MakeStyle(24, Color.white);
        centerStyle.fontStyle = FontStyle.Bold;
        infoStyle = WorldGui.MakeStyle(22, Color.white);
        infoStyle.alignment = TextAnchor.UpperLeft;
        titleStyle = WorldGui.MakeStyle(22, TitleColor);
    }
}
