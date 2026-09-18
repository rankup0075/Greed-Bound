using UnityEngine;

// 피격 번쩍임 (Spec 8장 "피격 번쩍임"). 그림(SpriteRenderer)에 붙어 머티리얼을 Sprite-Lit-Flash로 바꾸고,
// Set()으로 렌더러마다 번쩍임 색·세기를 넣음. 도트 그림도 곱하기 색과 달리 하얗게 덮임.
// 셰이더를 못 찾으면 아무것도 바꾸지 않음(예전처럼 곱하기 색만 보임).
[RequireComponent(typeof(SpriteRenderer))]
public class HitFlash : MonoBehaviour
{
    const string ShaderPath = "Shaders/SpriteLitFlash";  // Assets/Resources/Shaders/SpriteLitFlash.shader
    static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
    static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");

    static Material sharedMaterial;
    static bool shaderMissing;

    private SpriteRenderer spriteRenderer;
    private MaterialPropertyBlock block;
    private Color color = Color.white;
    private float amount = -1f;           // 첫 Set()에서 반드시 기록되게

    public bool Supported { get; private set; }

    public static HitFlash Ensure(SpriteRenderer renderer)
    {
        if (renderer == null) return null;
        HitFlash flash = renderer.GetComponent<HitFlash>();
        if (flash == null) flash = renderer.gameObject.AddComponent<HitFlash>();
        flash.Setup();
        return flash;
    }

    void Awake() => Setup();

    void Setup()
    {
        if (spriteRenderer != null) return;
        spriteRenderer = GetComponent<SpriteRenderer>();
        block = new MaterialPropertyBlock();

        Material material = SharedMaterial();
        Supported = material != null;
        if (Supported) spriteRenderer.sharedMaterial = material;
    }

    static Material SharedMaterial()
    {
        if (sharedMaterial != null || shaderMissing) return sharedMaterial;
        Shader shader = Resources.Load<Shader>(ShaderPath);
        if (shader == null || !shader.isSupported)
        {
            shaderMissing = true;
            Debug.LogWarning($"HitFlash: 셰이더 Resources/{ShaderPath}를 쓸 수 없어 피격 번쩍임을 끕니다.");
            return null;
        }
        sharedMaterial = new Material(shader) { name = "SpriteLitFlash (runtime)" };
        return sharedMaterial;
    }

    // amount 0 = 원래 그림, 1 = color로 완전히 덮음. 값이 바뀔 때만 렌더러에 기록
    public void Set(Color flashColor, float flashAmount)
    {
        Setup();
        if (!Supported) return;
        flashAmount = Mathf.Clamp01(flashAmount);
        if (Mathf.Approximately(flashAmount, amount) && (flashAmount <= 0f || flashColor == color)) return;
        color = flashColor;
        amount = flashAmount;

        spriteRenderer.GetPropertyBlock(block);
        block.SetColor(FlashColorId, color);
        block.SetFloat(FlashAmountId, amount);
        spriteRenderer.SetPropertyBlock(block);
    }
}
