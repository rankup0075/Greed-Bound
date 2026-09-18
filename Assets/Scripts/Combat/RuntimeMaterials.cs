using UnityEngine;

// 코드로 만든 메시(검격 이펙트, 적 예비동작 표시 등)가 함께 쓰는 머티리얼.
// 정점 색상(vertex color)을 그대로 보여주는 2D 스프라이트용 셰이더 사용.
// 주의: 빌드 시 셰이더가 빠지면 Shader.Find가 실패할 수 있음 — 빌드 단계에서 머티리얼 에셋으로 교체 예정.
public static class RuntimeMaterials
{
    static Material spriteUnlit;
    static PhysicsMaterial2D noFriction;

    // 마찰 0 물리 재질 — 캐릭터 콜라이더에 붙임. 속도는 코드가 직접 정하므로 바닥 마찰은 필요 없고,
    // 마찰이 있으면 공중에서 벽 쪽으로 이동키를 누를 때 벽에 붙어 떨어지지 않음
    public static PhysicsMaterial2D NoFriction
    {
        get
        {
            if (noFriction == null) noFriction = new PhysicsMaterial2D("NoFriction") { friction = 0f, bounciness = 0f };
            return noFriction;
        }
    }

    public static void ApplyNoFriction(GameObject target)
    {
        foreach (Collider2D collider in target.GetComponents<Collider2D>()) collider.sharedMaterial = NoFriction;
    }

    public static Material SpriteUnlit
    {
        get
        {
            if (spriteUnlit == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                spriteUnlit = new Material(shader);
            }
            return spriteUnlit;
        }
    }
}
