using UnityEngine;

// 플레이어의 영구 강화 배율 모음 (Spec 9장: 유물·상점 효과를 곱연산으로 누적).
// 모든 배율의 기본값은 1 = 강화 없음. 예) 벼려진 인장(근접 +20%) → meleeDamageMul *= 1.2f
// 체력·받는 피해·골드·무적 시간 배율은 해당 시스템을 만들 때 추가.
public class PlayerStats : MonoBehaviour
{
    [Header("체력")]
    public float maxHealthMul = 1f;     // 피의 계약 ×0.7

    [Header("근접 공격")]
    public float meleeDamageMul = 1f;   // 벼려진 인장, 묵직한 검신
    public float attackRangeMul = 1f;   // 긴 자루
    public float attackSpeedMul = 1f;   // 가벼운 손목 — 쿨을 이 값으로 나눔

    [Header("투척 단검")]
    public float daggerDamageMul = 1f;  // 독 묻은 촉, 벼려진 단검 촉
    public int bonusDaggers = 0;        // 쌍날 주머니, 단검 다발 (기본 5에 더함)
    public bool daggerPierce = false;   // 관통의 촉

    [Header("이동")]
    public float moveSpeedMul = 1f;     // 깃털 부츠, 가죽 장화
    public float jumpMul = 1f;          // 깃털 부츠, 가죽 장화 — 점프 초속도에 곱함

    [Header("보상")]
    public float goldMul = 1f;          // 황금 저울 (유물 +30%, 상점 +20%)
}
