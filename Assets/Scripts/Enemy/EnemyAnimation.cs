using System.Collections.Generic;
using UnityEngine;

// 적 도트 애니메이션 (Spec 8장 "픽셀아트 — Unity 구현 규칙").
// 적은 종류가 7가지인데 프리팹은 하나라, 종류별 컨트롤러를 Resources 에서 찾아 끼운다.
// 컨트롤러는 에디터 메뉴 "Greed Bound > 적 애니메이터 생성"이 만든다.
//
// 상태는 대부분 Enemy 를 보고 스스로 고르고, 밖에서 알려줘야만 아는 것
// (예고 시작·맞은 순간)만 Enemy 가 불러 준다.
[RequireComponent(typeof(Enemy))]
public class EnemyAnimation : MonoBehaviour
{
    public const string ResourceFolder = "EnemyAnimators/";

    // 종류 → 시트 접두사. pack_to_strip.py 에 넘긴 접두사와 같아야 한다 (아트 가이드 3장)
    public static string PrefixOf(EnemyType type)
    {
        switch (type)
        {
            case EnemyType.Small: return "imp";
            case EnemyType.Elite: return "elite";
            case EnemyType.Ranged: return "mage";
            case EnemyType.Shield: return "shieldman";
            case EnemyType.Charger: return "charger";
            case EnemyType.Boss: return "boss";
            default: return "soldier";
        }
    }

    const float RunSpeed = 0.08f;       // 이보다 느리면 서 있는 것으로 본다
    const float AirSpeed = 0.20f;       // 위아래 속도가 이보다 커야 점프·낙하로 본다
    const float MinStretch = 0.25f;     // 클립을 이보다 더 느리게는 늘리지 않는다 (멈춰 보임)
    const float MaxStretch = 3.0f;      // 이보다 빠르게도 안 돌린다 (깜빡임)

    private Enemy enemy;
    private Animator animator;
    private Rigidbody2D body;
    private readonly Dictionary<string, float> clipSeconds = new Dictionary<string, float>();
    private string current;
    private float holdUntil;            // 한 번 재생 동작이 끝날 때까지 다른 상태로 안 넘어가게
    private bool deathPlayed;

    public bool Ready => animator != null && animator.runtimeAnimatorController != null;

    void Awake()
    {
        enemy = GetComponent<Enemy>();
        body = GetComponent<Rigidbody2D>();
    }

    // Enemy.Initialize 가 종류를 확정한 뒤 부른다
    public void Bind(EnemyType type)
    {
        CharacterVisual.Ensure(gameObject);   // Visual 자식과 SpriteRenderer 가 있는 것을 보장
        Transform target = transform.Find(CharacterVisual.VisualName);
        if (target == null) return;

        string prefix = PrefixOf(type);
        RuntimeAnimatorController controller =
            Resources.Load<RuntimeAnimatorController>(ResourceFolder + prefix);
        if (controller == null)
        {
            // 컨트롤러가 없으면 임시 사각형 그대로 — 게임은 계속 돌아간다
            Debug.LogWarning($"적 애니메이터 없음: Resources/{ResourceFolder}{prefix}. " +
                             "메뉴 \"Greed Bound > 적 애니메이터 생성\"을 실행하세요.");
            return;
        }

        animator = target.GetComponent<Animator>();
        if (animator == null) animator = target.gameObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.speed = 1f;

        // 클립 길이를 미리 재 둔다 — 예고 시간에 맞춰 재생 속도를 조절할 때 쓴다
        clipSeconds.Clear();
        foreach (AnimationClip clip in controller.animationClips)
        {
            if (clip == null || !clip.name.StartsWith(prefix + "_")) continue;
            clipSeconds[clip.name.Substring(prefix.Length + 1)] = clip.length;
        }

        current = null;
        holdUntil = 0f;
        deathPlayed = false;
        Play("idle", 0f);
    }

    void LateUpdate()
    {
        if (!Ready) return;

        if (enemy.IsDead)
        {
            if (!deathPlayed)
            {
                deathPlayed = true;
                animator.speed = 1f;
                Play("death", 99f);   // 죽으면 다른 상태로 안 돌아간다
            }
            return;
        }

        // 돌진 중에는 예고 때 잡은 자세를 그대로 끌고 간다 — 달리기로 바뀌면 돌진으로 안 읽힌다
        if (enemy.IsCharging) return;
        if (Time.time < holdUntil) return;
        animator.speed = 1f;

        Vector2 v = body != null ? body.linearVelocity : Vector2.zero;
        if (Mathf.Abs(v.y) > AirSpeed)
            Play(v.y > 0f ? "jump" : "fall", 0f);
        else if (Mathf.Abs(v.x) > RunSpeed)
            Play("run", 0f);
        else
            Play("idle", 0f);
    }

    // 공격 예고가 시작될 때 부른다.
    // 클립을 예고 시간에 맞춰 늘리거나 줄여서 **휘두르기가 끝나는 순간에 타격이 나가게** 한다.
    // 예고가 끝난 뒤에 재생하면 이미 맞은 뒤에 휘두르는 것처럼 보인다.
    public void PlayWindup(int index, float windupSeconds)
    {
        if (!Ready || windupSeconds <= 0f) return;

        // 없는 번호면 아래로 내려가며 찾는다. 보스처럼 공격이 2종뿐인 팩에서
        // 무조건 attack1 로 떨어지면 기본 공격과 돌진이 똑같아 보인다
        string state = null;
        for (int i = Mathf.Clamp(index, 1, 3); i >= 1; i--)
        {
            if (HasState("attack" + i)) { state = "attack" + i; break; }
        }
        if (state == null) return;

        float length = clipSeconds.TryGetValue(state, out float s) ? s : windupSeconds;
        animator.speed = Mathf.Clamp(length / windupSeconds, MinStretch, MaxStretch);
        Play(state, windupSeconds);
    }

    // 예고가 끝난 뒤에도 자세를 조금 더 붙들어 둔다 (타격 직후 따라오는 동작)
    public void HoldPose(float seconds)
    {
        if (!Ready) return;
        holdUntil = Mathf.Max(holdUntil, Time.time + seconds);
    }

    public void PlayHurt(float seconds)
    {
        if (!Ready || enemy.IsDead) return;
        // 밀리지 않는 적(보스·돌진 중)은 피격 동작으로 패턴을 끊지 않는다 — 번쩍임만으로 표현.
        // 끊으면 휘두르던 동작이 중간에 잘려 "모션이 깨진" 것처럼 보인다
        if (!enemy.IsStaggerable) return;
        if (!HasState("hurt")) return;      // 피격 동작이 없는 팩은 번쩍임만
        animator.speed = 1f;
        Play("hurt", seconds);
    }

    void Play(string state, float hold)
    {
        if (!HasState(state)) return;
        if (state != current) animator.Play(state, 0, 0f);
        else if (hold > 0f) animator.Play(state, 0, 0f);   // 같은 공격을 다시 낼 때는 처음부터
        current = state;
        holdUntil = hold > 0f ? Time.time + hold : 0f;
    }

    // 컨트롤러에 상태가 있어도 클립이 지워졌으면 아무것도 재생되지 않는다.
    // 그래서 상태 이름이 아니라 **클립이 실제로 있는지**로 판단한다
    bool HasState(string state) => animator != null && clipSeconds.ContainsKey(state)
        && animator.HasState(0, Animator.StringToHash(state));
}
