using UnityEngine;
using UnityEngine.Rendering.Universal;

// Main Camera에 붙이는 스크립트 (Spec 8장 카메라).
// 플레이어를 가로로만 부드럽게 따라가고(세로 고정), 맵 끝에서 멈추며, 피격 시 흔들림.
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [Header("추적")]
    public Transform target;                           // 비워두면 시작 시 플레이어를 자동으로 찾음
    public float followLerp = 0.16f;                   // Spec: 60fps 기준 프레임당 0.16
    public float fixedY = ArenaLayout.CameraY;

    [Header("맵 경계")]
    public float mapMinX = -ArenaLayout.MapHalfWidth;
    public float mapMaxX = ArenaLayout.MapHalfWidth;

    [Header("화면 흔들림")]
    public float shakeDuration = 0.2f;
    public float shakeMagnitude = 0.15f;

    private Camera cam;
    private float followX;
    private float shakeTimer;

    [Header("픽셀아트 (Spec 8장)")]
    public int assetsPPU = 36;
    public int referenceWidth = 640;
    public int referenceHeight = 360;

    void Awake()
    {
        cam = GetComponent<Camera>();
        SetupPixelPerfect();
        if (target == null)
        {
            PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
            if (player != null) target = player.transform;
        }
    }

    void Start()
    {
        SnapToTarget();
    }

    // 전투 시작 시 호출 — 부드럽게 이동하지 않고 즉시 플레이어 위치로
    public void SnapToTarget()
    {
        if (target != null) followX = ClampX(target.position.x);
        Apply(Vector2.zero);
    }

    // 상점·로비: 기준 해상도를 넓혀(같은 PPU) 카메라가 뒤로 물러난 것처럼 넓게 보이게 하고, 맵 범위 안에서 가로 추적.
    // 지면이 화면 아래에서 떨어진 거리는 전투 맵과 같게 세로 위치를 맞춤
    public void UseWideView(int width, int height, float mapHalfWidth)
    {
        referenceWidth = width;
        referenceHeight = height;
        SetupPixelPerfect();
        fixedY = ArenaLayout.GroundTop - ArenaLayout.GroundBottomMargin + height / (float)assetsPPU * 0.5f;
        mapMinX = -mapHalfWidth;
        mapMaxX = mapHalfWidth;
        SnapToTarget();
    }

    public void Shake()
    {
        shakeTimer = shakeDuration;
    }

    // 캐릭터 이동(보간 포함)이 반영된 뒤에 따라가야 떨림이 없음
    void LateUpdate()
    {
        if (target != null)
        {
            // 프레임레이트와 무관한 lerp: 60fps에서 프레임당 0.16과 같은 속도
            float t = 1f - Mathf.Pow(1f - followLerp, Time.deltaTime * 60f);
            followX = Mathf.Lerp(followX, ClampX(target.position.x), t);
        }

        Vector2 offset = Vector2.zero;
        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.deltaTime;
            float strength = shakeMagnitude * Mathf.Clamp01(shakeTimer / shakeDuration);  // 점점 약해짐
            offset = Random.insideUnitCircle * strength;
        }

        Apply(offset);
    }

    // Pixel Perfect Camera: 640×360 / PPU 36. Stretch Fill이라 창 크기와 무관하게 보이는 범위가 같음 (씬마다 따로 설정하지 않게 실행 시 붙임)
    void SetupPixelPerfect()
    {
        if (!TryGetComponent(out PixelPerfectCamera pixelPerfect)) pixelPerfect = gameObject.AddComponent<PixelPerfectCamera>();
        pixelPerfect.assetsPPU = assetsPPU;
        pixelPerfect.refResolutionX = referenceWidth;
        pixelPerfect.refResolutionY = referenceHeight;
        pixelPerfect.cropFrame = PixelPerfectCamera.CropFrame.StretchFill;
        pixelPerfect.gridSnapping = PixelPerfectCamera.GridSnapping.None;  // 스프라이트가 들어오면 UpscaleRenderTexture 검토
    }

    // 화면 가로 절반만큼 안쪽으로 제한해서 맵 밖이 보이지 않게
    float ClampX(float x)
    {
        // Pixel Perfect Camera(Stretch Fill)라 보이는 가로 = 기준 해상도 폭. 기준을 바꾼 프레임에도 바로 맞게 직접 계산
        float halfWidth = referenceWidth / (float)assetsPPU * 0.5f;
        float min = mapMinX + halfWidth;
        float max = mapMaxX - halfWidth;
        if (min > max) return (mapMinX + mapMaxX) * 0.5f;
        return Mathf.Clamp(x, min, max);
    }

    void Apply(Vector2 offset)
    {
        transform.position = new Vector3(followX + offset.x, fixedY + offset.y, transform.position.z);
    }
}
