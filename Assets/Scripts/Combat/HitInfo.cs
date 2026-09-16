using UnityEngine;

// 공격 한 번이 대상에게 전달하는 정보. 근접 공격·단검·폭발 등 모든 피해가 이 형식으로 들어감.
public struct HitInfo
{
    public float damage;
    public Vector2 direction;      // 공격이 밀어내는 방향 (보통 공격자 → 대상, 좌우 ±1)
    public float knockback;        // 밀어내는 거리 (unit). 0이면 밀어내지 않음
    public bool cancelAttack;      // true면 대상이 준비 중인 공격(예비동작)을 취소시킴 — 3타
    public GameObject source;      // 누가 때렸는지
}

// 피해를 받을 수 있는 모든 것(적, 허수아비, 플레이어)이 구현하는 규약.
public interface IDamageable
{
    // 실제로 들어간 피해량을 반환 (무적·사망 등으로 무시되면 0). 탐식 흡혈 등에서 사용
    float TakeHit(HitInfo hit);
}
