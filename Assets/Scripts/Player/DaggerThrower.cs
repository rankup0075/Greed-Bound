using UnityEngine;
using UnityEngine.InputSystem;

// X — 투척 단검 (Spec 5장). 라운드 내 한정 자원: 라운드 시작마다 RefillDaggers()로 최대치 보충.
[RequireComponent(typeof(PlayerMovement), typeof(PlayerStats))]
public class DaggerThrower : MonoBehaviour
{
    [Header("Spec 5장")]
    public int baseMaxDaggers = 5;
    public float baseDamage = 13f;
    public float cooldown = 22f / 60f;       // 22프레임 = 0.367초

    [Header("연결")]
    public Dagger daggerPrefab;
    public float spawnOffset = 0.5f;         // 플레이어 중심에서 앞으로 얼마나 떨어져서 생성할지

    public int CurrentDaggers { get; private set; }
    public int MaxDaggers => baseMaxDaggers + stats.bonusDaggers;

    private PlayerMovement movement;
    private PlayerStats stats;
    private PlayerAttack attack;
    private InputAction throwAction;
    private float readyTime;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        stats = GetComponent<PlayerStats>();
        attack = GetComponent<PlayerAttack>();
        throwAction = InputSystem.actions.FindAction("Player/Throw", throwIfNotFound: true);
    }

    void Start()
    {
        // 라운드 시스템이 생기기 전까지는 게임 시작 시 한 번 보충
        RefillDaggers();
    }

    // 라운드 시작 시 라운드 관리자가 호출
    public void RefillDaggers()
    {
        CurrentDaggers = MaxDaggers;
    }

    void Update()
    {
        if (!throwAction.WasPressedThisFrame()) return;
        if (Time.time < readyTime) return;
        if (attack != null && attack.IsLocked) return;  // 근접 공격 쿨 중엔 투척 불가
        if (CurrentDaggers <= 0) return;                // TODO: 봉인 카드도 여기서 막음

        if (daggerPrefab == null)
        {
            Debug.LogWarning("DaggerThrower: Dagger Prefab이 연결되지 않았습니다.", this);
            return;
        }

        int facing = movement.Facing;
        Vector3 spawn = transform.position + new Vector3(facing * spawnOffset, 0f, 0f);
        Dagger dagger = Instantiate(daggerPrefab, spawn, Quaternion.identity);
        dagger.Launch(gameObject, facing, baseDamage * stats.daggerDamageMul, stats.daggerPierce);

        CurrentDaggers--;
        readyTime = Time.time + cooldown;
        Debug.Log($"단검 투척 — 남은 단검 {CurrentDaggers}/{MaxDaggers}");
    }
}
