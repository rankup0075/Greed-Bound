using UnityEngine;

// 공중 발판에 붙이는 스크립트. 아래에서 위로는 통과하고, 위에서 떨어질 때만 올라설 수 있음 (Spec 5장 발판).
// 붙이기만 하면 BoxCollider2D + PlatformEffector2D가 자동으로 추가되고 설정됨.
// 붕괴 시너지(Spec 3장): 예고 깜빡임 → 무너짐(반투명, 밟을 수 없음) → 복구.
[RequireComponent(typeof(BoxCollider2D), typeof(PlatformEffector2D))]
public class OneWayPlatform : MonoBehaviour
{
    enum CollapseState { Solid, Warning, Down }

    const float DownAlpha = 0.18f;
    static readonly Color WarningTint = new Color(1f, 0.45f, 0.3f);

    public bool IsCollapsing => state != CollapseState.Solid;

    private BoxCollider2D box;
    private SpriteRenderer spriteRenderer;
    private Color baseColor = Color.white;
    private CollapseState state = CollapseState.Solid;
    private float stateTimer;
    private float downTime;

    // 컴포넌트를 처음 붙일 때(에디터) 한 번 실행
    void Reset()
    {
        Configure();
    }

    // 게임 시작 시에도 한 번 더 적용 — Inspector에서 실수로 체크를 꺼도 발판이 망가지지 않게
    void Awake()
    {
        Configure();
        // 그림은 자식 "Art" 에 있다 (지형 타일을 Tiled 모드로 그리려고 판정과 분리했음).
        // GetComponent 로만 찾으면 붕괴 예고 깜빡임·반투명이 통째로 안 보인다
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null) baseColor = spriteRenderer.color;
    }

    void Configure()
    {
        box = GetComponent<BoxCollider2D>();
        box.usedByEffector = true;

        PlatformEffector2D effector = GetComponent<PlatformEffector2D>();
        effector.useOneWay = true;       // 한쪽 방향으로만 충돌
        effector.surfaceArc = 180f;      // 윗면 반원 방향에서 오는 충돌만 받음
        effector.rotationalOffset = 0f;  // 윗면 = 위쪽
        effector.useSideFriction = false; // 옆면에 붙어서 멈추는 현상 방지
    }

    // 붕괴 시작: warnTime 동안 깜빡임(예고) → downTime 동안 무너짐 → 복구
    public void Collapse(float warnTime, float downTime)
    {
        if (IsCollapsing) return;
        this.downTime = downTime;
        state = CollapseState.Warning;
        stateTimer = warnTime;
    }

    // 즉시 원래대로 (라운드 종료 시)
    public void Restore()
    {
        state = CollapseState.Solid;
        box.enabled = true;
        if (spriteRenderer != null) spriteRenderer.color = baseColor;
    }

    void Update()
    {
        if (state == CollapseState.Solid) return;

        stateTimer -= Time.deltaTime;
        switch (state)
        {
            case CollapseState.Warning:
                if (spriteRenderer != null)
                {
                    bool blink = Mathf.Repeat(stateTimer, 0.16f) < 0.08f;
                    spriteRenderer.color = blink ? WarningTint : baseColor;
                }
                if (stateTimer <= 0f)
                {
                    state = CollapseState.Down;
                    stateTimer = downTime;
                    box.enabled = false;  // 위에 있던 캐릭터는 떨어짐
                    if (spriteRenderer != null)
                    {
                        Color faded = baseColor;
                        faded.a = DownAlpha;
                        spriteRenderer.color = faded;
                    }
                }
                break;

            case CollapseState.Down:
                if (stateTimer <= 0f) Restore();
                break;
        }
    }
}
