using UnityEngine;
using UnityEngine.InputSystem;

// 1 — 체력 물약 (Spec 6-2장 상점 품목, 5장 키 배치). 보유 수는 RunState.potions (런 전체 유지).
// 체력이 가득 찼거나 봉인된 라운드에는 마시지 않음. PlayerHealth가 없으면 자동으로 붙임.
[RequireComponent(typeof(PlayerHealth))]
public class PlayerPotion : MonoBehaviour
{
    [Header("Spec 6-2장")]
    public float healAmount = 45f;

    // 물약을 실제로 마신 순간 — 물약 애니메이션(PlayerAnimation)용
    public event System.Action Drank;

    private PlayerHealth health;
    private InputAction potionAction;

    void Awake()
    {
        health = GetComponent<PlayerHealth>();
        potionAction = InputSystem.actions.FindAction("Player/Potion", throwIfNotFound: true);
    }

    void Update()
    {
        if (!potionAction.WasPressedThisFrame()) return;

        RunState run = RunState.Instance;
        if (run == null || health.IsDead) return;

        // 못 마시는 이유를 항상 알려줌 (조용히 무시하면 버그처럼 보임)
        if (run.potions <= 0)
        {
            Notify("물약이 없습니다 (상점에서 구매)");
            SoundManager.Play(SoundId.Deny);
            return;
        }
        if (health.CurrentHealth >= health.MaxHealth)
        {
            Notify("체력이 가득 차 있습니다");  // 낭비 방지
            SoundManager.Play(SoundId.Deny);
            return;
        }
        if (run.IsSealed)
        {
            Notify("봉인: 이번 라운드는 물약을 쓸 수 없습니다");
            SoundManager.Play(SoundId.Deny);
            return;
        }

        float before = health.CurrentHealth;
        run.potions--;
        health.Heal(healAmount);
        Drank?.Invoke();
        SoundManager.Play(SoundId.Potion);
        Notify($"물약 사용 +{Mathf.RoundToInt(health.CurrentHealth - before)}  (남은 물약 {run.potions})");
    }

    // 전투 씬은 화면 안내 문구, 그 외(상점)는 Console
    static void Notify(string text)
    {
        if (RoundManager.Instance != null) RoundManager.Instance.ShowMessage(text);
        else Debug.Log(text);
    }
}
