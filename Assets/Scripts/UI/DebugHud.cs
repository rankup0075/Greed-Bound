using UnityEngine;

// 개발용 임시 HUD. 정식 UI를 만들기 전까지 사용.
// 좌상단: 체력·단검·라운드·골드·위험도·보상 배수·적 수 / 화면 중앙: 라운드 안내·결과·게임 오버
public class DebugHud : MonoBehaviour
{
    private PlayerHealth health;
    private DaggerThrower daggers;
    private PlayerMovement movement;
    private GUIStyle infoStyle;
    private GUIStyle bannerStyle;

    void Start()
    {
        health = FindFirstObjectByType<PlayerHealth>();
        daggers = FindFirstObjectByType<DaggerThrower>();
        movement = FindFirstObjectByType<PlayerMovement>();
    }

    void OnGUI()
    {
        if (infoStyle == null)
        {
            infoStyle = new GUIStyle(GUI.skin.label) { fontSize = 22 };
            infoStyle.normal.textColor = Color.white;
            bannerStyle = new GUIStyle(GUI.skin.label) { fontSize = 40, alignment = TextAnchor.MiddleCenter };
            bannerStyle.normal.textColor = Color.white;
        }

        GUI.Label(new Rect(16, 12, 500, 260), BuildInfoText(), infoStyle);

        string banner = BuildBannerText();
        if (!string.IsNullOrEmpty(banner))
        {
            GUI.Label(new Rect(0, Screen.height * 0.25f, Screen.width, 200), banner, bannerStyle);
        }
    }

    string BuildInfoText()
    {
        string text = "";
        if (health != null)
        {
            text += health.IsDead ? "체력 0 — 사망\n" : $"체력 {Mathf.CeilToInt(health.CurrentHealth)} / {health.MaxHealth:F0}\n";
        }
        if (daggers != null) text += $"단검 {daggers.CurrentDaggers} / {daggers.MaxDaggers}\n";
        if (movement != null)
        {
            float cooldown = movement.DashCooldownRemaining;
            text += cooldown > 0f ? $"대쉬 {cooldown:F1}초\n" : "대쉬 준비\n";
        }

        RunState run = RunState.Instance;
        if (run != null)
        {
            string overload = run.IsOverloaded ? " (돌파)" : "";
            text += $"라운드 {run.round}   골드 {run.gold}\n";
            text += $"위험도 {run.Risk:F0}{overload}   보상 x{run.RewardMultiplier:F2}   카드 {run.picks.Count}장\n";
        }

        text += $"적 {Enemy.AliveCount}";
        RoundManager rounds = RoundManager.Instance;
        if (rounds != null) text += $"   증원 예산 {rounds.ReinforcementBudget}";
        return text;
    }

    string BuildBannerText()
    {
        RoundManager rounds = RoundManager.Instance;
        if (rounds == null) return null;

        switch (rounds.CurrentPhase)
        {
            case RoundManager.Phase.Result:
                string shop = rounds.NextIsShop ? "\n(상점 — 아직 없음, 건너뜀)" : "";
                return $"라운드 {RunState.Instance.round} 클리어!  +{rounds.LastGoldReward} G{shop}\nEnter로 다음 라운드";
            case RoundManager.Phase.GameOver:
                return $"게임 오버 — 라운드 {RunState.Instance.round}\nEnter로 다시 시작";
            case RoundManager.Phase.Cards:
                return null;  // 카드 선택 화면이 따로 그림
            default:
                return rounds.HasMessage ? rounds.Message : null;
        }
    }
}
