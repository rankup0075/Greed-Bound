using UnityEngine;

// 배경 한 층 (Spec 5장 전투 맵).
// 카메라를 `follow` 비율만큼 따라가서 멀리 있는 것처럼 보이게 한다.
//   follow 1   = 카메라에 붙어 다님 → 화면에서 전혀 움직이지 않음 (무한히 먼 배경)
//   follow 0   = 월드에 고정 → 지형과 똑같이 흐름
// 그림은 Tiled 모드라 한 장으로 맵 전체를 덮는다 (드로우콜 1개).
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class ParallaxLayer : MonoBehaviour
{
    [Tooltip("카메라를 따라가는 비율 (1 = 멈춰 보임, 0 = 지형과 같이 흐름)")]
    [Range(0f, 1f)] public float follow = 0.8f;

    [Tooltip("세로로도 따라갈지 — 보통 가로만 움직이면 충분하다")]
    public bool followY;

    private Transform cam;
    private Vector3 origin;

    void OnEnable()
    {
        origin = transform.position;
        cam = Camera.main != null ? Camera.main.transform : null;
    }

    void LateUpdate()
    {
        if (cam == null)
        {
            if (Camera.main == null) return;
            cam = Camera.main.transform;
        }
        transform.position = new Vector3(
            origin.x + cam.position.x * follow,
            followY ? origin.y + cam.position.y * follow : origin.y,
            origin.z);
    }

    // 맵 생성 도구가 배치한 뒤 기준 위치를 다시 잡아 준다
    public void SetOrigin(Vector3 position)
    {
        origin = position;
        transform.position = position;
    }
}
