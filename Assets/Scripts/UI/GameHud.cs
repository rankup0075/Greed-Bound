using UnityEngine;
using UnityEngine.UI;

// 전투·상점 HUD (Spec 9장 "HUD — Unity 구현 규칙"). 임시 OnGUI(DebugHud)를 대신하는 정식 UI.
// 계층을 코드로 만들기 때문에 씬·프리팹을 고치지 않아도 되고, 도트 UI 스프라이트가 들어오면 Image의 sprite만 채우면 됨.
// 좌표계는 게임과 같은 640×360 — UI도 같은 도트 격자에 맞음.
public class GameHud : MonoBehaviour
{
    const int Pad = 6;              // 화면 가장자리 여백
    const int Line = 11;            // 좌상단 글줄 간격
    const int SmallFont = 9;
    const int TinyFont = 8;
    const int BannerFont = 18;
    const int CooldownBox = 22;     // 쿨타임 아이콘 한 변

    static readonly Color HealthColor = new Color(0.85f, 0.22f, 0.27f);
    static readonly Color BossColor = new Color(0.95f, 0.72f, 0.25f);
    static readonly Color ReadyColor = new Color(0.35f, 0.55f, 0.85f);      // 쿨이 끝난 아이콘
    static readonly Color SweepColor = new Color(0f, 0f, 0f, 0.65f);        // 쿨 도는 동안 덮는 부채꼴

    // 롤처럼 네모 아이콘 + 시계방향으로 도는 덮개 + 남은 초 숫자
    class CooldownIcon
    {
        public RectTransform root;
        public Image background;
        public Image sweep;
        public Text remaining;
        public Text name;
    }

    private PlayerHealth health;
    private DaggerThrower daggers;
    private PlayerMovement movement;
    private PlayerSkill skill;

    private RectTransform healthFill, bossFill, healthGroup, bossGroup;
    private Text healthText, daggerText, potionText, runText, bossName, banner, footer;
    private CooldownIcon dashIcon, skillIcon;

    void Awake() => Build();

    void Start() => FindPlayer();



    void FindPlayer()
    {
        health = FindFirstObjectByType<PlayerHealth>();
        daggers = FindFirstObjectByType<DaggerThrower>();
        movement = FindFirstObjectByType<PlayerMovement>();
        skill = FindFirstObjectByType<PlayerSkill>();
    }

    // ───────────── 계층 만들기 ─────────────

    void Build()
    {
        RectTransform root = PixelUi.CreateCanvas(transform, "HUD Canvas", 100);

        // 좌상단 — 체력·단검·물약
        healthFill = Gauge(root, new Vector2(Pad, -Pad), new Vector2(96, 9), HealthColor, out healthGroup);
        healthText = PixelUi.Label(root, new Vector2(Pad + 3, -Pad - 1), new Vector2(120, 9), SmallFont, TextAnchor.MiddleLeft);
        daggerText = PixelUi.Label(root, new Vector2(Pad, -Pad - Line - 2), new Vector2(120, Line), SmallFont, TextAnchor.MiddleLeft);
        potionText = PixelUi.Label(root, new Vector2(Pad, -Pad - Line * 2 - 2), new Vector2(120, Line), SmallFont, TextAnchor.MiddleLeft);

        // 좌상단 아래 — 쿨타임 아이콘 2개 (대쉬·스킬)
        float iconY = -Pad - Line * 3 - 2;
        dashIcon = MakeCooldownIcon(root, new Vector2(Pad, iconY), "대쉬", "Shift");
        skillIcon = MakeCooldownIcon(root, new Vector2(Pad + CooldownBox + 5, iconY), "스킬", "A");

        // 우상단 — 라운드·골드·위험도·유물·영혼
        runText = PixelUi.Label(root, new Vector2(-Pad, -Pad), new Vector2(240, 80), SmallFont, TextAnchor.UpperRight);
        runText.rectTransform.anchorMin = runText.rectTransform.anchorMax = new Vector2(1f, 1f);
        runText.rectTransform.pivot = new Vector2(1f, 1f);

        // 상단 가운데 — 보스
        bossFill = Gauge(root, new Vector2(-110, -22), new Vector2(220, 8), BossColor, out bossGroup);
        bossGroup.anchorMin = bossGroup.anchorMax = new Vector2(0.5f, 1f);
        bossGroup.pivot = new Vector2(0f, 1f);
        bossName = PixelUi.Label(root, new Vector2(0, -10), new Vector2(240, 12), SmallFont, TextAnchor.MiddleCenter);
        bossName.rectTransform.anchorMin = bossName.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        bossName.rectTransform.pivot = new Vector2(0.5f, 1f);

        // 가운데 — 라운드 안내·결과·게임 오버
        banner = PixelUi.Label(root, new Vector2(0, 40), new Vector2(560, 120), BannerFont, TextAnchor.MiddleCenter);
        banner.rectTransform.anchorMin = banner.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        banner.rectTransform.pivot = new Vector2(0.5f, 0.5f);

        // 하단 — 봉인·죽음의 시계 같은 상태 알림
        footer = PixelUi.Label(root, new Vector2(0, Pad + 10), new Vector2(560, 14), SmallFont, TextAnchor.LowerCenter);
        footer.rectTransform.anchorMin = footer.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        footer.rectTransform.pivot = new Vector2(0.5f, 0f);
    }

    // 바탕 + 채움 게이지. 채움은 anchorMax.x로 줄여서 스프라이트 유무와 상관없이 정확히 깎임
    RectTransform Gauge(RectTransform parent, Vector2 position, Vector2 size, Color color, out RectTransform group)
    {
        Image background = PixelUi.MakeImage(parent, "Gauge", PixelUi.Dark);
        PixelUi.Anchor(background.rectTransform, position, size);
        group = background.rectTransform;

        Image fill = PixelUi.MakeImage(group, "Fill", color);
        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = new Vector2(1f, 1f);
        fillRect.offsetMax = new Vector2(-1f, -1f);
        return fillRect;
    }

    // 채움 비율 (anchorMax.x). 바탕 안쪽 1px 여백을 유지한 채로 줄어듦
    static void SetGauge(RectTransform fill, float ratio)
    {
        ratio = Mathf.Clamp01(ratio);
        fill.anchorMax = new Vector2(ratio, 1f);
        fill.offsetMax = new Vector2(ratio > 0f ? -1f : 0f, -1f);
    }

    CooldownIcon MakeCooldownIcon(RectTransform parent, Vector2 position, string name, string key)
    {
        CooldownIcon icon = new CooldownIcon();

        icon.background = PixelUi.MakeImage(parent, $"Cooldown_{name}", ReadyColor);
        icon.root = icon.background.rectTransform;
        PixelUi.Anchor(icon.root, position, new Vector2(CooldownBox, CooldownBox));

        // 테두리 대신 안쪽에 어두운 판을 깔아 아이콘 자리를 만듦 (도트 아이콘이 들어오면 이 Image의 sprite를 채움)
        Image inner = PixelUi.MakeImage(icon.root, "Icon", PixelUi.Dark);
        inner.rectTransform.anchorMin = Vector2.zero;
        inner.rectTransform.anchorMax = Vector2.one;
        inner.rectTransform.offsetMin = new Vector2(1f, 1f);
        inner.rectTransform.offsetMax = new Vector2(-1f, -1f);

        // 쿨 도는 동안 덮는 부채꼴 — 12시에서 시작해 시계방향으로 줄어듦 (롤과 같은 방식)
        icon.sweep = PixelUi.MakeImage(inner.rectTransform, "Sweep", SweepColor);
        icon.sweep.rectTransform.anchorMin = Vector2.zero;
        icon.sweep.rectTransform.anchorMax = Vector2.one;
        icon.sweep.rectTransform.offsetMin = Vector2.zero;
        icon.sweep.rectTransform.offsetMax = Vector2.zero;
        icon.sweep.type = Image.Type.Filled;
        icon.sweep.fillMethod = Image.FillMethod.Radial360;
        icon.sweep.fillOrigin = (int)Image.Origin360.Top;
        icon.sweep.fillClockwise = true;
        icon.sweep.fillAmount = 0f;

        // 남은 초 (아이콘 가운데)
        icon.remaining = PixelUi.Label(icon.root, Vector2.zero, new Vector2(CooldownBox, CooldownBox), SmallFont, TextAnchor.MiddleCenter);
        icon.remaining.rectTransform.anchorMin = Vector2.zero;
        icon.remaining.rectTransform.anchorMax = Vector2.one;
        icon.remaining.rectTransform.offsetMin = Vector2.zero;
        icon.remaining.rectTransform.offsetMax = Vector2.zero;

        // 키 (아이콘 오른쪽 아래)
        Text keyLabel = PixelUi.Label(icon.root, new Vector2(-1, -CooldownBox + 8), new Vector2(CooldownBox, 8), TinyFont, TextAnchor.LowerRight);
        keyLabel.text = key;

        // 이름 (아이콘 아래)
        icon.name = PixelUi.Label(parent, position + new Vector2(0, -CooldownBox - 1), new Vector2(CooldownBox + 6, 9), TinyFont, TextAnchor.UpperLeft);
        icon.name.text = name;
        return icon;
    }




    // ───────────── 갱신 ─────────────

    void LateUpdate()
    {
        if (health == null) FindPlayer();   // 씬 전환·라운드 시작으로 플레이어가 새로 생긴 경우

        UpdatePlayer();
        UpdateRun();
        UpdateBoss();
        UpdateBanner();
    }

    void UpdatePlayer()
    {
        bool hasPlayer = health != null;
        healthGroup.gameObject.SetActive(hasPlayer);
        healthText.gameObject.SetActive(hasPlayer);
        if (hasPlayer)
        {
            float max = Mathf.Max(1f, health.MaxHealth);
            SetGauge(healthFill, health.CurrentHealth / max);
            healthText.text = $"{Mathf.CeilToInt(health.CurrentHealth)} / {max:F0}";
        }

        daggerText.gameObject.SetActive(daggers != null);
        if (daggers != null) daggerText.text = $"단검 (X)  {daggers.CurrentDaggers} / {daggers.MaxDaggers}";

        RunState run = RunState.Instance;
        potionText.gameObject.SetActive(run != null);
        if (run != null) potionText.text = $"물약 (1)  {run.potions}";

        dashIcon.root.gameObject.SetActive(movement != null);
        dashIcon.name.gameObject.SetActive(movement != null);
        if (movement != null) SetCooldown(dashIcon, movement.DashCooldownRemaining, movement.dashCooldown);

        bool hasSkill = skill != null && skill.enabled;
        skillIcon.root.gameObject.SetActive(hasSkill);
        skillIcon.name.gameObject.SetActive(hasSkill);
        if (hasSkill)
        {
            SkillCatalog.Skill info = SkillCatalog.Get(skill.Current);
            skillIcon.name.text = info.name;
            SetCooldown(skillIcon, skill.CooldownRemaining, info.cooldown);
        }
    }

    // 남은 시간 비율만큼 부채꼴이 남아 시계방향으로 줄어들고, 가운데에 남은 초를 표시
    static void SetCooldown(CooldownIcon icon, float remaining, float total)
    {
        bool ready = remaining <= 0.01f || total <= 0f;
        icon.sweep.fillAmount = ready ? 0f : Mathf.Clamp01(remaining / total);
        icon.background.color = ready ? ReadyColor : Color.Lerp(ReadyColor, PixelUi.Dark, 0.5f);
        icon.remaining.text = ready ? "" : (remaining >= 1f ? Mathf.CeilToInt(remaining).ToString() : remaining.ToString("0.0"));
    }

    void UpdateRun()
    {
        RunState run = RunState.Instance;
        runText.gameObject.SetActive(run != null);
        if (run == null) return;

        string synergies = "";
        foreach (CardCategory category in System.Enum.GetValues(typeof(CardCategory)))
        {
            int level = run.SynergyLevel(category);
            if (level >= 2) synergies += $"{Synergy.Name(category)} Lv{level}  ";
        }

        string text = $"라운드 {run.round}    골드 {run.gold}\n";
        text += $"위험도 {run.Risk:F0}{(run.IsOverloaded ? " (돌파)" : "")}    보상 ×{run.RewardMultiplier:F2}\n";
        text += $"카드 {run.picks.Count}장    유물 {run.RelicCount}개\n";
        text += $"영혼 {MetaProgress.Souls} (+{MetaCatalog.SoulsForRun(run.round, run.Risk, run.RelicCount)})";
        if (synergies.Length > 0) text += $"\n{synergies.TrimEnd()}";
        runText.text = text;
    }

    void UpdateBoss()
    {
        Enemy boss = null;
        foreach (Enemy enemy in Enemy.Active)
        {
            if (enemy.IsBoss && !enemy.IsDead) { boss = enemy; break; }
        }

        bool show = boss != null;
        bossGroup.gameObject.SetActive(show);
        bossName.gameObject.SetActive(show);
        if (!show) return;

        SetGauge(bossFill, boss.MaxHealth > 0f ? boss.CurrentHealth / boss.MaxHealth : 0f);
        bossName.text = Enemy.BossName;
    }

    void UpdateBanner()
    {
        banner.text = BannerText() ?? "";
        banner.gameObject.SetActive(banner.text.Length > 0);
        footer.text = FooterText() ?? "";
        footer.gameObject.SetActive(footer.text.Length > 0);
    }

    string BannerText()
    {
        RoundManager rounds = RoundManager.Instance;
        RunState run = RunState.Instance;
        if (rounds == null || run == null) return null;

        switch (rounds.CurrentPhase)
        {
            case RoundManager.Phase.Result:
                string next = !rounds.NextIsShop ? "Enter로 다음 라운드"
                    : rounds.ShopAvailable ? "Enter로 상점 입장"
                    : "(상점 씬이 빌드 설정에 없어 건너뜀)\nEnter로 다음 라운드";
                return $"라운드 {run.round} 클리어!   +{rounds.LastGoldReward} G\n{next}";
            case RoundManager.Phase.GameOver:
                string after = rounds.LobbyAvailable ? "Enter로 로비" : "Enter로 다시 시작";
                return $"게임 오버 — 라운드 {run.round}\n영혼 +{rounds.LastSoulReward}  (보유 {MetaProgress.Souls})\n{after}";
            case RoundManager.Phase.Cards:
                return null;   // 카드 선택 화면이 따로 그림
            default:
                return rounds.HasMessage ? rounds.Message : null;
        }
    }

    string FooterText()
    {
        RunState run = RunState.Instance;
        string text = "";
        if (run != null && run.IsSealed && RoundManager.Instance != null) text += "봉인 — 이번 라운드 단검·물약·스킬 사용 불가";

        EnvironmentCardEffects env = EnvironmentCardEffects.Instance;
        if (env != null && env.DeathClockActive)
        {
            if (text.Length > 0) text += "\n";
            text += $"죽음의 시계  {Mathf.CeilToInt(env.DeathClockRemaining)}초";
        }
        return text;
    }
}
