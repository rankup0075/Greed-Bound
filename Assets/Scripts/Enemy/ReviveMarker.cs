using UnityEngine;

// 죽음의 군세 부활 대기 표시. 죽은 적의 모습을 흐리게 남겨두고 깜빡이다가 부활 순간 사라짐.
// 부활 자체는 GameLoopQueue가 같은 시간 뒤에 처리 — 이 스크립트는 보이기만 함.
public class ReviveMarker : MonoBehaviour
{
    static readonly Color Tint = new Color(0.55f, 0.9f, 0.6f);

    private SpriteRenderer spriteRenderer;
    private float duration;
    private float age;

    public static void Spawn(Enemy enemy, float duration)
    {
        CharacterVisual visual = enemy.GetComponent<CharacterVisual>();
        SpriteRenderer source = visual != null ? visual.Renderer : null;
        if (source == null) return;

        GameObject go = new GameObject("ReviveMarker");
        go.transform.position = source.transform.position;
        go.transform.localScale = source.transform.lossyScale;  // 그림 크기·좌우 반전 그대로

        ReviveMarker marker = go.AddComponent<ReviveMarker>();
        marker.duration = duration;
        marker.spriteRenderer = go.AddComponent<SpriteRenderer>();
        marker.spriteRenderer.sprite = source.sprite;
        marker.spriteRenderer.sortingLayerID = source.sortingLayerID;
        marker.spriteRenderer.sortingOrder = source.sortingOrder - 1;
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= duration)
        {
            Destroy(gameObject);
            return;
        }

        // 부활이 가까울수록 진해지고 빠르게 깜빡임
        float t = age / duration;
        Color c = Tint;
        c.a = Mathf.Lerp(0.15f, 0.6f, t) * (0.7f + 0.3f * Mathf.Sin(age * Mathf.Lerp(6f, 30f, t)));
        spriteRenderer.color = c;
    }
}
