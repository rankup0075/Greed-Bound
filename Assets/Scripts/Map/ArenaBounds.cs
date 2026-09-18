using UnityEngine;

// 지금 싸울 수 있는 전장의 가로 범위 (Spec 9장 전장 경계).
// 평소엔 맵 전체(±31.11). 죽음의 영역이 전투 중에 줄이고, 전투가 끝나면 원래대로 돌림.
// 적 생성·소환·군단·화염 위치는 이 범위 안으로 제한.
public static class ArenaBounds
{
    public static float MinX { get; private set; } = -ArenaLayout.MapHalfWidth;
    public static float MaxX { get; private set; } = ArenaLayout.MapHalfWidth;

    public static void Set(float minX, float maxX)
    {
        MinX = minX;
        MaxX = maxX;
    }

    public static void Reset()
    {
        MinX = -ArenaLayout.MapHalfWidth;
        MaxX = ArenaLayout.MapHalfWidth;
    }

    // 양쪽 경계에서 margin만큼 안쪽으로 제한 (범위가 margin보다 좁으면 가운데)
    public static float Clamp(float x, float margin)
    {
        float min = MinX + margin;
        float max = MaxX - margin;
        return min > max ? (MinX + MaxX) * 0.5f : Mathf.Clamp(x, min, max);
    }
}
