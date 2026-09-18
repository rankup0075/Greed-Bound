using UnityEngine;

// 튜토리얼 연습 표적 (허수아비·기둥 위 표적). 맞으면 흰색으로 번쩍이고, 체력이 0이면 조각으로 부서진다.
// 모양은 코드 메시(WorldGui.CreateQuad) — 스프라이트가 생기면 SpriteRenderer로 바꾸면 된다.
// 콜라이더는 트리거라 플레이어가 통과한다(적처럼). 근접·단검·스킬 모두 IDamageable로 닿는다.
public class TutorialTarget : MonoBehaviour, IDamageable
{
    const float FlashTime = 0.12f;

    public float MaxHealth { get; private set; }
    public float CurrentHealth { get; private set; }
    public bool IsBroken { get; private set; }

    public event System.Action<TutorialTarget> Broken;

    private MeshFilter quad;
    private Vector2 bodySize;
    private Color baseColor;
    private float flashTimer;

    // 아래 가운데가 feet. 튜토리얼 씬 안에서만 쓰는 생성기
    public static TutorialTarget Create(string name, Vector2 feet, Vector2 size, Color color, float health)
    {
        MeshFilter quad = WorldGui.CreateQuad(name, feet, size, color, -2);

        BoxCollider2D box = quad.gameObject.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = size;
        box.offset = new Vector2(0f, size.y * 0.5f);

        TutorialTarget target = quad.gameObject.AddComponent<TutorialTarget>();
        target.quad = quad;
        target.bodySize = size;
        target.baseColor = color;
        target.MaxHealth = target.CurrentHealth = health;
        return target;
    }

    void Update()
    {
        if (flashTimer <= 0f) return;
        flashTimer -= Time.deltaTime;
        WorldGui.SetQuadColor(quad, flashTimer > 0f ? Color.white : baseColor);
    }

    public float TakeHit(HitInfo hit)
    {
        if (IsBroken) return 0f;

        float before = CurrentHealth;
        CurrentHealth = Mathf.Max(0f, CurrentHealth - hit.damage);
        flashTimer = FlashTime;
        WorldGui.SetQuadColor(quad, Color.white);
        SoundManager.Play(SoundId.Hit);

        if (CurrentHealth <= 0f) Break();
        return before - CurrentHealth;
    }

    void Break()
    {
        IsBroken = true;
        DeathBurst.Spawn((Vector2)transform.position + new Vector2(0f, bodySize.y * 0.5f), bodySize, baseColor);
        SoundManager.Play(SoundId.Kill);
        Broken?.Invoke(this);
        Destroy(gameObject);
    }
}
