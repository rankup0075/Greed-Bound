using UnityEngine;

// 공중 발판에 붙이는 스크립트. 아래에서 위로는 통과하고, 위에서 떨어질 때만 올라설 수 있음 (Spec 5장 발판).
// 붙이기만 하면 BoxCollider2D + PlatformEffector2D가 자동으로 추가되고 설정됨.
[RequireComponent(typeof(BoxCollider2D), typeof(PlatformEffector2D))]
public class OneWayPlatform : MonoBehaviour
{
    // 컴포넌트를 처음 붙일 때(에디터) 한 번 실행
    void Reset()
    {
        Configure();
    }

    // 게임 시작 시에도 한 번 더 적용 — Inspector에서 실수로 체크를 꺼도 발판이 망가지지 않게
    void Awake()
    {
        Configure();
    }

    void Configure()
    {
        GetComponent<BoxCollider2D>().usedByEffector = true;

        PlatformEffector2D effector = GetComponent<PlatformEffector2D>();
        effector.useOneWay = true;       // 한쪽 방향으로만 충돌
        effector.surfaceArc = 180f;      // 윗면 반원 방향에서 오는 충돌만 받음
        effector.rotationalOffset = 0f;  // 윗면 = 위쪽
        effector.useSideFriction = false; // 옆면에 붙어서 멈추는 현상 방지
    }
}
