using UnityEngine;

// 대쉬 잔상. 원본 스프라이트를 그 자리에 복사해두고 짧게 흐려지며 사라짐.
public class DashAfterimage : MonoBehaviour
{
    const float Lifetime = 0.18f;
    static readonly Color Tint = new Color(0.55f, 0.8f, 1f, 0.55f);

    private SpriteRenderer spriteRenderer;
    private float age;

    public static void Spawn(SpriteRenderer source)
    {
        GameObject go = new GameObject("DashAfterimage");
        go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        go.transform.localScale = source.transform.lossyScale;  // 좌우 반전 포함

        DashAfterimage afterimage = go.AddComponent<DashAfterimage>();
        afterimage.spriteRenderer = go.AddComponent<SpriteRenderer>();
        afterimage.spriteRenderer.sprite = source.sprite;
        afterimage.spriteRenderer.color = Tint;
        afterimage.spriteRenderer.sortingLayerID = source.sortingLayerID;
        afterimage.spriteRenderer.sortingOrder = source.sortingOrder - 1;  // 플레이어 뒤에
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= Lifetime)
        {
            Destroy(gameObject);
            return;
        }

        Color c = Tint;
        c.a = Tint.a * (1f - age / Lifetime);
        spriteRenderer.color = c;
    }
}
